using Icefied.Core;
using UnityEngine;

namespace Icefied.Game
{
    /// <summary>
    /// Buz üzerinde yaşayan düşman. Pozisyon hücre uzayında tutulur (hücre (x,y) merkezi = x+0.5, y+0.5).
    /// Penguenler Qix gibi çapraz seker; boss dolaşır ve (varsa) oyuncunun izine saldırır.
    /// </summary>
    public class Enemy : MonoBehaviour
    {
        public bool IsBoss;
        public float Radius;
        public float Speed;
        public float ChargeInterval;
        public Vector2 Pos;
        public float Heading;            // derece, 0 = +x (sağ), 90 = +y (yukarı)
        public bool Alive = true;
        public Transform Body;

        float _targetHeading;
        float _turnTimer;
        float _chargeTimer;
        float _windup;
        float _charging;
        float _anim;
        float _yaw;
        Renderer[] _renderers;
        MaterialPropertyBlock _mpb;
        float _flash;
        Color _flashColor;

        public bool IsWindingUp => _windup > 0f;
        public bool IsGolden;
        Vector2 _lastTarget;

        /// <summary>Hücre/saniye cinsinden yaklaşık anlık hız (bot tehlike tahmini için).</summary>
        public Vector2 Velocity
        {
            get
            {
                if (_windup > 0f) return Vector2.zero;
                float rad = Heading * Mathf.Deg2Rad;
                return new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * (Speed * (_charging > 0f ? 2.6f : 1f));
            }
        }

        public void Init(bool boss, Vector2 pos, float radius, float speed, float chargeInterval)
        {
            IsBoss = boss;
            Pos = pos;
            Radius = radius;
            Speed = speed;
            ChargeInterval = chargeInterval;
            Body = transform.Find("Body");
            Heading = boss ? Random.Range(0f, 360f) : 45f + 90f * Random.Range(0, 4) + Random.Range(-12f, 12f);
            _targetHeading = Heading;
            _turnTimer = Random.Range(1f, 2.5f);
            _chargeTimer = chargeInterval * Random.Range(0.6f, 1f);
            _anim = Random.Range(0f, 10f);
            _yaw = 90f - Heading;
            _renderers = GetComponentsInChildren<Renderer>();
            _mpb = new MaterialPropertyBlock();
        }

        public void Tick(MatchBase lc, float dt, bool frozen)
        {
            if (!Alive) return;
            if (frozen) { SetFlash(0.45f, new Color(0.6f, 0.9f, 1f)); return; }

            var grid = lc.Grid;
            float speed = Speed;

            if (IsBoss)
            {
                if (ChargeInterval > 0f) speed = UpdateCharge(lc, dt);
                if (_windup <= 0f && _charging <= 0f)
                {
                    _turnTimer -= dt;
                    if (_turnTimer <= 0f)
                    {
                        _targetHeading = Heading + Random.Range(-120f, 120f);
                        _turnTimer = Random.Range(1.2f, 2.6f);
                    }
                    Heading = Mathf.MoveTowardsAngle(Heading, _targetHeading, 110f * dt);
                }
                if (_windup > 0f) return;
            }

            float rad = Heading * Mathf.Deg2Rad;
            Vector2 next = Pos + new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * (speed * dt);

            if (!CanOccupy(grid, Pos, Radius))
            {
                // Bir kesimden sonra kenara sıkıştıysa en yakın açık buza doğru it.
                Pos = Vector2.MoveTowards(Pos, lc.NearestIceCenter(Pos), speed * dt);
                return;
            }

            bool hitX = !CanOccupy(grid, new Vector2(next.x, Pos.y), Radius);
            if (hitX) Heading = 180f - Heading; else Pos.x = next.x;
            bool hitY = !CanOccupy(grid, new Vector2(Pos.x, next.y), Radius);
            if (hitY) Heading = -Heading; else Pos.y = next.y;
            if (hitX || hitY)
            {
                Heading = Mathf.Repeat(Heading, 360f);
                _targetHeading = Heading;
                if (_charging > 0f) _charging = 0f;
            }
        }

        float UpdateCharge(MatchBase lc, float dt)
        {
            if (_charging > 0f)
            {
                _charging -= dt;
                if (_charging <= 0f) _chargeTimer = ChargeInterval * Random.Range(0.8f, 1.3f);
                return Speed * 2.6f;
            }

            if (_windup > 0f)
            {
                _windup -= dt;
                SetFlash(0.5f * (0.5f + 0.5f * Mathf.Sin(Time.time * 40f)), new Color(1f, 0.25f, 0.2f));
                if (_windup <= 0f)
                {
                    Vector2 d = (lc.ChargeTarget(Pos) ?? _lastTarget) - Pos;
                    Heading = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                    _targetHeading = Heading;
                    _charging = 1.1f;
                    GameRoot.I.Sfx.Play(GameRoot.I.Sfx.Roar, 0.8f);
                }
                return 0f;
            }

            _chargeTimer -= dt;
            var target = lc.ChargeTarget(Pos);
            if (_chargeTimer <= 0f && target.HasValue)
            {
                _lastTarget = target.Value;
                _windup = 0.7f;
                GameRoot.I.Sfx.Play(GameRoot.I.Sfx.Warn, 0.6f);
            }
            return Speed;
        }

        public void SyncVisual(MatchBase lc, float dt, bool frozen)
        {
            if (!Alive) return;
            transform.position = lc.CellToWorld(Pos, MatchBase.IceTop);

            float targetYaw = 90f - Heading;
            _yaw = Mathf.LerpAngle(_yaw, targetYaw, 1f - Mathf.Exp(-10f * dt));
            transform.rotation = Quaternion.Euler(0, _yaw, 0);

            if (!frozen) _anim += dt * (IsBoss ? 6f : 16f) * (_charging > 0f ? 2f : 1f);
            if (Body != null)
            {
                if (IsBoss)
                {
                    float s = Mathf.Sin(_anim);
                    Body.localRotation = Quaternion.Euler(Mathf.Sin(_anim * 0.5f) * 3f, 0, s * 3f);
                    Body.localPosition = new Vector3(_windup > 0f ? Mathf.Sin(Time.time * 60f) * 0.03f : 0f,
                                                     Mathf.Abs(s) * 0.04f, 0);
                }
                else
                {
                    Body.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(_anim) * 12f);
                    Body.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(_anim)) * 0.025f, 0);
                }
            }

            if (!frozen && _windup <= 0f) SetFlash(0f, Color.white);
            ApplyFlash();
        }

        void SetFlash(float amount, Color color)
        {
            _flash = amount;
            _flashColor = color;
        }

        void ApplyFlash()
        {
            if (_renderers == null) return;
            _mpb.SetFloat("_Flash", _flash);
            _mpb.SetColor("_FlashColor", _flashColor);
            foreach (var r in _renderers)
                if (r != null && r.sharedMaterial != null && r.sharedMaterial.HasProperty("_Flash"))
                    r.SetPropertyBlock(_mpb);
        }

        /// <summary>Daire tamamen buz (veya iz) hücreleri üzerinde mi?</summary>
        public static bool CanOccupy(IceGrid grid, Vector2 p, float r)
        {
            int x0 = Mathf.FloorToInt(p.x - r), x1 = Mathf.FloorToInt(p.x + r);
            int y0 = Mathf.FloorToInt(p.y - r), y1 = Mathf.FloorToInt(p.y + r);
            float r2 = r * r;
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                float cx = Mathf.Clamp(p.x, x, x + 1) - p.x;
                float cy = Mathf.Clamp(p.y, y, y + 1) - p.y;
                if (cx * cx + cy * cy >= r2) continue;
                if (!grid.IsEnemySpace(x, y)) return false;
            }
            return true;
        }

        /// <summary>Daire verilen oyuncunun izine değiyor mu?</summary>
        public bool TouchesTrail(IceGrid grid, int owner)
        {
            float r = Radius * 0.9f;
            int x0 = Mathf.FloorToInt(Pos.x - r), x1 = Mathf.FloorToInt(Pos.x + r);
            int y0 = Mathf.FloorToInt(Pos.y - r), y1 = Mathf.FloorToInt(Pos.y + r);
            float r2 = r * r;
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                if (!grid.IsTrailOf(x, y, owner)) continue;
                float cx = Mathf.Clamp(Pos.x, x, x + 1) - Pos.x;
                float cy = Mathf.Clamp(Pos.y, y, y + 1) - Pos.y;
                if (cx * cx + cy * cy < r2) return true;
            }
            return false;
        }
    }
}
