using System.Collections;
using System.Collections.Generic;
using Icefied.Core;
using Icefied.View;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Icefied.Game
{
    /// <summary>
    /// Tek oyunculu level ve PvP maçının ortak tabanı: buz tahtası, düşmanlar, hediyeler,
    /// kopan parçaların batması ve ilgili animasyonlar.
    /// </summary>
    public abstract class MatchBase : MonoBehaviour
    {
        public const float CellSize = 0.16f;
        public const float IceTop = 0.26f;
        public const float IceBottom = -0.7f;

        public LevelData Data { get; protected set; }
        public IceGrid Grid { get; protected set; }
        public float Percent => Grid.ClaimedPercent;

        /// <summary>Düşmanların donma süresi (tek oyunculu Freeze hediyesi).</summary>
        public float FreezeTimer { get; protected set; }

        public abstract bool IsPlaying { get; }
        public Vector3 BoardCenter => transform.position;

        protected GameRoot Root;
        protected IceBoardView Board;
        protected Enemy Boss;
        protected readonly List<Enemy> Enemies = new List<Enemy>();
        protected readonly List<Gift> Gifts = new List<Gift>();
        protected readonly List<int> Detached = new List<int>();
        protected bool[] DetachedMask;
        bool _boardDirty;

        /// <summary>Bot için salt-okunur erişim.</summary>
        public IReadOnlyList<Enemy> AllEnemies => Enemies;

        public IEnumerable<Vector2Int> GiftCells
        {
            get { foreach (var g in Gifts) yield return g.Cell; }
        }

        protected class Gift
        {
            public Transform T;
            public Vector2Int Cell;
            public GiftKind Kind;
        }

        // ================================================================ setup

        protected void SetupBoard(GameRoot root, LevelData data)
        {
            Root = root;
            Data = data;
            Grid = new IceGrid(data.Width, data.Height);
            DetachedMask = new bool[data.Width * data.Height];
            Board = new IceBoardView(Grid, CellSize, IceTop, IceBottom, transform);
            Mats.Water.SetColor("_DeepColor", data.WaterDeep);
            Mats.Water.SetColor("_ShallowColor", data.WaterShallow);
        }

        protected void SpawnBoss(Vector2 pos)
        {
            var t = CharacterFactory.Boss(Data.Boss, transform, Data.BossRadius * CellSize * 1.0f);
            Boss = t.gameObject.AddComponent<Enemy>();
            Boss.Init(true, pos, Data.BossRadius, Data.BossSpeed, Data.ChargeInterval);
            Enemies.Add(Boss);
        }

        protected Enemy SpawnPenguin(Vector2 pos, bool golden = false)
        {
            var t = CharacterFactory.Penguin(transform, golden ? 0.8f : 0.7f, golden);
            var e = t.gameObject.AddComponent<Enemy>();
            e.Init(false, pos, golden ? 1.7f : 1.55f, Data.PenguinSpeed * (golden ? 1.35f : 1f), 0f);
            e.IsGolden = golden;
            Enemies.Add(e);
            return e;
        }

        protected void SpawnGift(GiftKind kind, Vector2 p)
        {
            var cell = new Vector2Int((int)p.x, (int)p.y);
            var t = CharacterFactory.Gift(kind, transform, 0.55f);
            t.position = CellToWorld(cell + new Vector2(0.5f, 0.5f), IceTop);
            t.rotation = Quaternion.Euler(0, Random.Range(-25f, 25f), 0);
            Gifts.Add(new Gift { T = t, Cell = cell, Kind = kind });
        }

        /// <summary>Buz üzerinde düşmanlardan, hediyelerden, verilen noktalardan ve açık izlerden uzak bir nokta bulur.</summary>
        protected bool TryFindSpawn(float radius, out Vector2 pos, IList<Vector2> avoid = null, float avoidDist = 28f,
                                    float yMin = 0.25f, float yMax = 1f)
        {
            for (int i = 0; i < 300; i++)
            {
                var p = new Vector2(Random.Range(4f, Data.Width - 4f),
                                    Random.Range(Data.Height * yMin + 2f, Data.Height * yMax - 4f));
                if (!Enemy.CanOccupy(Grid, p, radius + 1f)) continue;
                if (NearTrail(p, 10)) continue;
                bool clear = true;
                if (avoid != null)
                    foreach (var a in avoid)
                        if (Vector2.Distance(p, a) < avoidDist) { clear = false; break; }
                foreach (var e in Enemies)
                    if (e.Alive && Vector2.Distance(p, e.Pos) < e.Radius + radius + 4f) { clear = false; break; }
                foreach (var g in Gifts)
                    if (Vector2.Distance(p, g.Cell) < 6f) { clear = false; break; }
                if (!clear) continue;
                pos = p;
                return true;
            }
            pos = default;
            return false;
        }

        bool NearTrail(Vector2 p, int r)
        {
            int cx = (int)p.x, cy = (int)p.y;
            for (int y = cy - r; y <= cy + r; y++)
            for (int x = cx - r; x <= cx + r; x++)
                if (Grid.Get(x, y) == Cell.Trail) return true;
            return false;
        }

        protected virtual void OnDestroy()
        {
            Board?.Destroy();
        }

        // ================================================================ helpers

        public Vector3 CellToWorld(Vector2 p, float y)
        {
            return new Vector3((p.x - Data.Width * 0.5f) * CellSize, y, (p.y - Data.Height * 0.5f) * CellSize);
        }

        public Vector2 NearestIceCenter(Vector2 from)
        {
            int cx = (int)from.x, cy = (int)from.y;
            for (int r = 0; r < 24; r++)
            for (int y = cy - r; y <= cy + r; y++)
            for (int x = cx - r; x <= cx + r; x++)
            {
                if (Mathf.Abs(x - cx) != r && Mathf.Abs(y - cy) != r) continue;
                if (Grid.Get(x, y) == Cell.Ice) return new Vector2(x + 0.5f, y + 0.5f);
            }
            return from;
        }

        /// <summary>En yakın kenar hücresi (teknenin güvenle durabileceği su). Bulamazsa kendisini döner.</summary>
        public Vector2Int NearestEdgeCell(Vector2Int from)
        {
            for (int r = 0; r < Mathf.Max(Data.Width, Data.Height); r++)
            {
                float best = float.MaxValue;
                var bestCell = from;
                bool found = false;
                for (int y = from.y - r; y <= from.y + r; y++)
                for (int x = from.x - r; x <= from.x + r; x++)
                {
                    if (Mathf.Abs(x - from.x) != r && Mathf.Abs(y - from.y) != r) continue;
                    if (!Grid.IsEdge(x, y)) continue;
                    float d = (x - from.x) * (x - from.x) + (y - from.y) * (y - from.y);
                    if (d < best) { best = d; bestCell = new Vector2Int(x, y); found = true; }
                }
                if (found) return bestCell;
            }
            return from;
        }

        /// <summary>Boss'un saldıracağı hedef (çizim yapan en yakın tekne). Yoksa null.</summary>
        public virtual Vector2? ChargeTarget(Vector2 from) => null;

        public void MarkBoardDirty() => _boardDirty = true;

        protected void RefreshBoardNow()
        {
            Board.Refresh();
            _boardDirty = false;
        }

        protected virtual void LateUpdate()
        {
            if (_boardDirty) RefreshBoardNow();
        }

        /// <summary>Boss'un bulunduğu (ya da ona en yakın) buz hücresi: kesimde kalacak bölgenin tohumu.</summary>
        public Vector2Int BossSeed()
        {
            var c = new Vector2Int(Mathf.FloorToInt(Boss.Pos.x), Mathf.FloorToInt(Boss.Pos.y));
            if (IceGrid.IsIceLike(Grid.Get(c.x, c.y))) return c;
            var n = NearestIceCenter(Boss.Pos);
            return new Vector2Int((int)n.x, (int)n.y);
        }

        protected void TickEnemies(float dt)
        {
            bool frozen = FreezeTimer > 0f;
            foreach (var e in Enemies) e.Tick(this, dt, frozen);
        }

        protected void SyncWorldVisuals(float dt)
        {
            bool frozen = FreezeTimer > 0f;
            foreach (var e in Enemies) e.SyncVisual(this, dt, frozen);

            float time = Time.time;
            foreach (var g in Gifts)
            {
                var body = g.T.Find("Body");
                body.localPosition = new Vector3(0, 0.03f + Mathf.Sin(time * 3f + g.Cell.x) * 0.03f, 0);
                body.localRotation = Quaternion.Euler(0, time * 40f, 0);
            }
        }

        // ================================================================ cut results

        protected void MarkDetached()
        {
            System.Array.Clear(DetachedMask, 0, DetachedMask.Length);
            foreach (int i in Detached) DetachedMask[i] = true;
        }

        /// <summary>Kopan alandaki penguenleri parçaya bağlar ve döndürür.</summary>
        protected List<Enemy> CatchEnemies(Transform piece)
        {
            var caught = new List<Enemy>();
            foreach (var e in Enemies)
            {
                if (!e.Alive || e.IsBoss) continue;
                int x = Mathf.FloorToInt(e.Pos.x), y = Mathf.FloorToInt(e.Pos.y);
                if (!Grid.InBounds(x, y) || !DetachedMask[Grid.Index(x, y)]) continue;
                e.Alive = false;
                caught.Add(e);
                if (piece != null) e.transform.SetParent(piece, true);
                StartCoroutine(Panic(e.transform));
                Root.Fx.Glow(e.transform.position + Vector3.up * 0.3f, new Color(1f, 0.85f, 0.3f), 10);
            }
            return caught;
        }

        protected List<Gift> TakeDetachedGifts()
        {
            var taken = new List<Gift>();
            for (int i = Gifts.Count - 1; i >= 0; i--)
            {
                var g = Gifts[i];
                if (!DetachedMask[Grid.Index(g.Cell.x, g.Cell.y)]) continue;
                Gifts.RemoveAt(i);
                taken.Add(g);
                PlayGiftCollect(g);
            }
            return taken;
        }

        void PlayGiftCollect(Gift g)
        {
            Root.Sfx.Play(Root.Sfx.Collect);
            var color = g.Kind == GiftKind.Freeze ? Mats.Hex("#38C6FF") : Mats.Hex("#FFC93C");
            Root.Fx.Glow(g.T.position + Vector3.up * 0.2f, color, 18, 0.25f, 0.35f);
            var tr = g.T;
            Vector3 from = tr.position;
            Tween.Do(0.55f, Ease.OutCubic, k =>
            {
                if (tr == null) return;
                tr.position = from + Vector3.up * (k * 1.2f);
                tr.localScale = Vector3.one * (1f - Ease.InCubic(k));
                tr.rotation = Quaternion.Euler(0, k * 540f, 0);
                if (k >= 1f) Destroy(tr.gameObject);
            });
        }

        protected Transform SpawnPiece(out Vector3 center, Color flash)
        {
            center = Vector3.zero;
            if (Detached.Count == 0) return null;

            Vector2 sum = Vector2.zero;
            int w = Grid.Width;
            foreach (int i in Detached) sum += new Vector2(i % w + 0.5f, i / w + 0.5f);
            center = CellToWorld(sum / Detached.Count, IceTop);

            var go = new GameObject("IcePiece");
            go.transform.SetParent(transform, false);
            go.transform.position = center;
            var mesh = new Mesh { name = "IcePiece", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            var mask = (bool[])DetachedMask.Clone();
            IceBoardView.BuildSlab(mesh, (x, y) => mask[y * w + x], Grid.Width, Grid.Height, CellSize, IceTop, IceBottom,
                                   center);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mat = new Material(Mats.Ice);
            mat.SetColor("_FlashColor", flash);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;

            int samples = Mathf.Clamp(Detached.Count / 40, 6, 28);
            var points = new List<Vector3>(samples);
            for (int s = 0; s < samples; s++)
            {
                int i = Detached[Random.Range(0, Detached.Count)];
                points.Add(CellToWorld(new Vector2(i % w + 0.5f, i / w + 0.5f), 0.02f));
            }

            StartCoroutine(SinkPiece(go.transform, mesh, mat, points, Detached.Count));
            return go.transform;
        }

        IEnumerator SinkPiece(Transform tr, Mesh mesh, Material mat, List<Vector3> splashPoints, int cells)
        {
            Vector3 start = tr.position;
            Vector3 away = start - BoardCenter;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = Vector3.right;
            away.Normalize();
            Vector3 axis = Vector3.Cross(away, Vector3.up);
            float tilt = Mathf.Lerp(28f, 12f, Mathf.Clamp01(cells / 1500f));

            float t = 0f;
            while (t < 0.18f)
            {
                t += Time.deltaTime;
                float k = t / 0.18f;
                mat.SetFloat("_Flash", (1f - k) * 0.85f);
                tr.position = start + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.05f;
                yield return null;
            }
            mat.SetFloat("_Flash", 0f);

            foreach (var p in splashPoints) Root.Fx.Splash(p, 5, 1f, 0.25f);

            t = 0f;
            const float sinkDur = 1.5f;
            int splashed = 0;
            while (t < sinkDur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / sinkDur);
                float e = Ease.InCubic(k);
                tr.position = start + Vector3.down * (e * 2.4f) + away * (k * 0.35f);
                tr.rotation = Quaternion.AngleAxis(Ease.OutCubic(k) * tilt, axis);
                if (splashed < 2 && k > 0.35f + splashed * 0.25f)
                {
                    splashed++;
                    foreach (var p in splashPoints) Root.Fx.Splash(p, 2, 0.7f, 0.3f);
                }
                yield return null;
            }

            Destroy(tr.gameObject);
            Destroy(mesh);
            Destroy(mat);
        }

        IEnumerator Panic(Transform enemy)
        {
            var body = enemy.Find("Body");
            float t = 0f;
            while (t < 1.6f && body != null)
            {
                t += Time.deltaTime;
                body.localPosition = new Vector3(0, Mathf.Abs(Mathf.Sin(t * 18f)) * 0.08f, 0);
                body.localRotation = Quaternion.Euler(0, t * 500f, Mathf.Sin(t * 30f) * 20f);
                yield return null;
            }
        }

        // ================================================================ end-of-match animations

        protected IEnumerator BossDefeat(Enemy boss)
        {
            boss.Alive = false;
            var tr = boss.transform;
            Vector3 start = tr.position;
            float t = 0f;
            while (t < 0.35f)
            {
                t += Time.deltaTime;
                tr.position = start + new Vector3(Mathf.Sin(t * 70f) * 0.04f, 0, 0);
                yield return null;
            }
            Root.Sfx.Play(Root.Sfx.Roar, 0.8f);
            t = 0f;
            const float dur = 1.1f;
            while (t < dur)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / dur);
                float y = Mathf.Sin(k * Mathf.PI) * 1.4f - Ease.InCubic(k) * 2.2f;
                tr.position = start + Vector3.up * y;
                tr.rotation = Quaternion.Euler(k * 200f, k * 720f, 0);
                tr.localScale = Vector3.one * Mathf.Lerp(1f, 0.6f, k);
                yield return null;
            }
            Root.Fx.Splash(start, 30, 1.6f, 0.5f);
            Root.Fx.Shake(0.12f, 0.35f);
            tr.gameObject.SetActive(false);
        }

        protected IEnumerator JumpIntoSea(Enemy e)
        {
            e.Alive = false;
            var tr = e.transform;
            Vector3 start = tr.position;
            Vector3 dir = start - BoardCenter;
            dir.y = 0;
            dir = dir.sqrMagnitude < 0.01f ? Vector3.forward : dir.normalized;
            Vector3 end = start + dir * 0.8f + Vector3.down * 1.5f;
            yield return new WaitForSeconds(Random.Range(0f, 0.4f));
            float t = 0f;
            while (t < 0.8f)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / 0.8f);
                tr.position = Vector3.Lerp(start, end, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.7f;
                tr.rotation = Quaternion.LookRotation(dir) * Quaternion.Euler(k * 160f, 0, 0);
                yield return null;
            }
            Root.Fx.Splash(new Vector3(end.x, 0.02f, end.z), 10, 1f);
            tr.gameObject.SetActive(false);
        }

        /// <summary>Denizden zıplayıp buza çıkan penguen.</summary>
        protected IEnumerator JumpOntoIce(Enemy e)
        {
            e.Alive = false;
            var tr = e.transform;
            Vector3 end = CellToWorld(e.Pos, IceTop);
            Vector3 dir = end - BoardCenter;
            dir.y = 0;
            dir = dir.sqrMagnitude < 0.01f ? Vector3.forward : dir.normalized;
            Vector3 start = end + dir * 1.6f + Vector3.down * 0.6f;
            Root.Fx.Splash(new Vector3(start.x, 0.02f, start.z), 8, 1f);
            float t = 0f;
            while (t < 0.6f && tr != null)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / 0.6f);
                tr.position = Vector3.Lerp(start, end, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 0.9f;
                yield return null;
            }
            if (tr != null) e.Alive = true;
        }
    }
}
