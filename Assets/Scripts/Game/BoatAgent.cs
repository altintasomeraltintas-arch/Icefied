using System;
using System.Collections.Generic;
using Icefied.Core;
using Icefied.View;
using UnityEngine;

namespace Icefied.Game
{
    /// <summary>Tekneyi kim sürüyor: oyuncunun parmağı ya da bot.</summary>
    public interface IBoatBrain
    {
        Vector2Int Dir { get; }
        bool Held { get; }
    }

    public class HumanBrain : IBoatBrain
    {
        readonly DirectionInput _input;
        public HumanBrain(DirectionInput input) { _input = input; }
        public Vector2Int Dir => _input.Dir;
        public bool Held => _input.Held;
    }

    /// <summary>
    /// Bir tekne: kenar boyunca gezme, buza girince iz çizme, parmak kalkınca geri sarma,
    /// suya varınca kapanış, rakip izine girince kırma. Tek oyunculu ve PvP ortak kullanır.
    /// </summary>
    public class BoatAgent
    {
        public const float BaseSpeed = 20f;     // hücre / saniye
        const float RewindMultiplier = 2.4f;
        public const float HitRadius = 0.8f;    // hücre

        public readonly int Id;
        public readonly Color Color;
        public IBoatBrain Brain;

        public Vector2Int Cell, Target, Dir, TrailStart;
        public float T;
        public bool Moving, Drawing, Rewinding;
        public readonly List<(int x, int y)> Trail = new List<(int x, int y)>();

        public float Stun, Invuln, Frozen, SpeedBoost;
        public Transform View;
        public bool PlaySounds = true;

        /// <summary>Suya varınca (iz kapanınca) çağrılır.</summary>
        public Action<BoatAgent> OnClose;
        /// <summary>(saldıran, kurban): rakip izine girildi.</summary>
        public Action<BoatAgent, BoatAgent> OnBreakOther;
        /// <summary>İz sahibi id → tekne.</summary>
        public Func<int, BoatAgent> FindOwner;

        Transform _body;
        Vector2 _visual;
        float _yaw, _wake;
        int _crunch;
        MaterialPropertyBlock _mpb;
        Renderer[] _renderers;

        public Vector2 Pos => Vector2.Lerp(Cell, Target, T) + new Vector2(0.5f, 0.5f);
        public bool CanAct => Stun <= 0f && Frozen <= 0f;
        public bool Vulnerable => Drawing && Invuln <= 0f;

        public BoatAgent(int id, Color color, IBoatBrain brain, Vector2Int start, Transform view, float yaw)
        {
            Id = id;
            Color = color;
            Brain = brain;
            Cell = Target = TrailStart = start;
            View = view;
            _body = view.Find("Body");
            _visual = Pos;
            _yaw = yaw;
            _renderers = view.GetComponentsInChildren<Renderer>();
            _mpb = new MaterialPropertyBlock();
        }

        // ================================================================ movement

        public void Step(MatchBase m, float dt)
        {
            Stun -= dt;
            Frozen -= dt;
            SpeedBoost -= dt;
            Invuln -= dt;
            if (!CanAct) return;

            if (Drawing && !Rewinding && !Brain.Held) Rewinding = true;

            float speed = BaseSpeed * (SpeedBoost > 0f ? 1.5f : 1f) * (Rewinding ? RewindMultiplier : 1f);
            float step = speed * dt;
            int guard = 0;

            while (step > 0f && guard++ < 16)
            {
                if (!Moving)
                {
                    if (Rewinding && Brain.Held && Brain.Dir != Vector2Int.zero) Rewinding = false;
                    if (!TryStartMove(m)) break;
                }

                float remain = 1f - T;
                if (step < remain)
                {
                    T += step;
                    step = 0f;
                }
                else
                {
                    step -= remain;
                    T = 0f;
                    Moving = false;
                    Cell = Target;
                    Arrive(m);
                    if (!m.IsPlaying || !CanAct) break;
                }
            }

            if (!Moving && !Drawing) EnsureOnEdge(m);
        }

        bool CanMove(MatchBase m, Vector2Int d)
        {
            var g = m.Grid;
            var n = Cell + d;
            if (!g.InBounds(n.x, n.y)) return false;
            var c = g.Get(n.x, n.y);

            if (c == Core.Cell.Trail)
            {
                int owner = g.OwnerAt(n.x, n.y);
                if (owner == Id) return false;
                var victim = FindOwner?.Invoke(owner);
                return victim == null || victim.Invuln <= 0f;
            }
            if (Drawing) return true;
            return c == Core.Cell.Ice || (c == Core.Cell.Water && g.IsEdge(n.x, n.y));
        }

        bool TryStartMove(MatchBase m)
        {
            var g = m.Grid;
            if (Rewinding)
            {
                if (Trail.Count == 0)
                {
                    Rewinding = false;
                    Drawing = false;
                    return false;
                }
                var prev = Trail.Count >= 2 ? Trail[Trail.Count - 2] : (TrailStart.x, TrailStart.y);
                Target = new Vector2Int(prev.Item1, prev.Item2);
                Moving = true;
                return true;
            }

            var want = Brain.Dir;
            if (want == Vector2Int.zero) return false;

            Vector2Int d;
            if (CanMove(m, want)) d = want;
            else if (Dir != Vector2Int.zero && Dir != -want && CanMove(m, Dir)) d = Dir;
            else return false;

            Dir = d;
            Target = Cell + d;

            if (g.Get(Target.x, Target.y) == Core.Cell.Trail)
            {
                var victim = FindOwner?.Invoke(g.OwnerAt(Target.x, Target.y));
                OnBreakOther?.Invoke(this, victim);
                if (g.Get(Target.x, Target.y) == Core.Cell.Trail) { Target = Cell; return false; }
            }

            if (g.Get(Target.x, Target.y) == Core.Cell.Ice)
            {
                if (!Drawing)
                {
                    Drawing = true;
                    TrailStart = Cell;
                    Trail.Clear();
                    if (PlaySounds) GameRoot.I.Sfx.Play(GameRoot.I.Sfx.Crunch, 0.8f);
                }
                g.Set(Target.x, Target.y, Core.Cell.Trail, Id);
                Trail.Add((Target.x, Target.y));
                m.MarkBoardDirty();
            }

            Moving = true;
            return true;
        }

        void Arrive(MatchBase m)
        {
            var g = m.Grid;
            if (Rewinding)
            {
                var last = Trail[Trail.Count - 1];
                if (g.IsTrailOf(last.x, last.y, Id)) g.Set(last.x, last.y, Core.Cell.Ice);
                Trail.RemoveAt(Trail.Count - 1);
                m.MarkBoardDirty();
                if (Trail.Count == 0)
                {
                    Drawing = false;
                    Rewinding = false;
                }
                return;
            }

            if (!Drawing) return;

            if (g.Get(Cell.x, Cell.y) == Core.Cell.Water) OnClose?.Invoke(this);
            else if (PlaySounds && ++_crunch % 4 == 0) GameRoot.I.Sfx.Play(GameRoot.I.Sfx.Crunch, 0.25f, 0.2f);
        }

        /// <summary>Tekne çizmiyorsa ve artık kenarda değilse (yanındaki buz koptuysa) en yakın kenara kayar.</summary>
        public void EnsureOnEdge(MatchBase m)
        {
            if (m.Grid.IsEdge(Cell.x, Cell.y)) return;
            Cell = Target = m.NearestEdgeCell(Cell);
            T = 0f;
            Moving = false;
        }

        /// <summary>İzi siler, tekneyi izin başladığı yere (ya da en yakın kenara) döndürür.</summary>
        public void ResetTrail(MatchBase m)
        {
            m.Grid.ClearTrail(Trail, Id);
            Trail.Clear();
            Cell = Target = TrailStart;
            T = 0f;
            Moving = false;
            Drawing = false;
            Rewinding = false;
            EnsureOnEdge(m);
            m.MarkBoardDirty();
        }

        // ================================================================ visuals

        public void SyncVisual(MatchBase m, float dt, bool playing)
        {
            Vector2 p = Pos;
            float gap = Vector2.Distance(_visual, p);
            _visual = gap < 1.5f ? p : Vector2.MoveTowards(_visual, p, Mathf.Max(gap * 10f, 20f) * dt);
            Vector3 wp = m.CellToWorld(_visual, 0f);
            View.position = wp;

            Vector2Int face = Moving ? Target - Cell : Dir;
            if (Rewinding) face = -face;
            if (face != Vector2Int.zero)
            {
                float targetYaw = Mathf.Atan2(face.x, face.y) * Mathf.Rad2Deg;
                _yaw = Mathf.LerpAngle(_yaw, targetYaw, 1f - Mathf.Exp(-16f * dt));
            }
            View.rotation = Quaternion.Euler(0, _yaw, 0);

            float time = Time.time + Id * 1.7f;
            if (Stun > 0f)
            {
                _body.localPosition = new Vector3(0, 0.02f, 0);
                _body.localRotation = Quaternion.Euler(Mathf.Sin(time * 25f) * 8f, time * 720f, Mathf.Cos(time * 25f) * 8f);
            }
            else
            {
                _body.localPosition = new Vector3(0, Mathf.Sin(time * 4f) * 0.012f, 0);
                _body.localRotation = Quaternion.Euler(Mathf.Sin(time * 3f) * 2.5f, 0, Mathf.Sin(time * 2.3f) * 3f);
            }
            _body.gameObject.SetActive(Stun > 0f || Invuln <= 0f || !playing || ((int)(Time.time * 14f) & 1) == 0);

            float flash = Frozen > 0f ? 0.55f : 0f;
            _mpb.SetFloat("_Flash", flash);
            _mpb.SetColor("_FlashColor", new Color(0.6f, 0.9f, 1f));
            foreach (var r in _renderers)
                if (r != null && r.sharedMaterial != null && r.sharedMaterial.HasProperty("_Flash"))
                    r.SetPropertyBlock(_mpb);

            if (Moving && playing && CanAct)
            {
                _wake -= dt;
                if (_wake <= 0f)
                {
                    _wake = 0.03f;
                    Vector3 fwd = View.forward;
                    GameRoot.I.Fx.Wake(wp - fwd * 0.22f, -fwd);
                }
            }
        }
    }
}
