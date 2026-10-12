using System.Collections.Generic;
using Icefied.Core;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Icefied.Game
{
    /// <summary>
    /// PvP rakibi. Kenar boyunca bir giriş noktasına gider, içeri N hücre girer, döner ve kenara çıkıp
    /// dikdörtgen bir kesim kapatır. Planı yaparken kesimin büyüklüğüne, içindeki penguenlere ve düşmanlara
    /// uzaklığa bakar; ara sıra rakibin açık izine saldırır. Kusurları (düşünme/tepki gecikmesi, küçük kesim,
    /// düşmana fazla yaklaşma) beceri değeriyle (0..1) ölçeklenir.
    /// </summary>
    public class BotBrain : IBoatBrain
    {
        enum Mode { Idle, Cut, Attack, Retreat }

        readonly float _skill;
        readonly float _thinkDelay, _reaction, _margin, _aggression, _sloppy;
        readonly int _maxDepth, _maxWidth, _candidates;

        VersusController _m;
        BoatAgent _me, _rival;

        Mode _mode = Mode.Idle;
        readonly List<Vector2Int> _route = new List<Vector2Int>();
        int _idx;
        Vector2Int _attackCell;
        float _think, _danger, _stuck;
        Vector2Int _lastCell;

        // BFS arabellekleri
        int[] _dist, _parent;
        readonly Queue<int> _queue = new Queue<int>();
        readonly List<int> _edges = new List<int>();
        readonly List<int> _pathIdx = new List<int>();
        readonly List<Vector2Int> _cut = new List<Vector2Int>();
        bool[] _mask;

        static readonly Vector2Int[] Dirs = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

        public BotBrain(float skill)
        {
            _skill = Mathf.Clamp01(skill);
            _thinkDelay = Mathf.Lerp(1.1f, 0.35f, _skill);
            _reaction = Mathf.Lerp(0.35f, 0.08f, _skill);
            _margin = Mathf.Lerp(0.3f, 3f, _skill);
            _aggression = Mathf.Lerp(0.1f, 0.45f, _skill);
            _sloppy = Mathf.Lerp(0.45f, 0.08f, _skill);
            _maxDepth = Mathf.RoundToInt(Mathf.Lerp(10f, 24f, _skill));
            _maxWidth = Mathf.RoundToInt(Mathf.Lerp(12f, 30f, _skill));
            _candidates = Mathf.RoundToInt(Mathf.Lerp(20f, 60f, _skill));
        }

        public void Bind(VersusController m, BoatAgent me, BoatAgent rival)
        {
            _m = m;
            _me = me;
            _rival = rival;
            int n = m.Grid.Width * m.Grid.Height;
            _dist = new int[n];
            _parent = new int[n];
            _mask = new bool[n];
            _think = Random.Range(0.6f, 1.2f);
        }

        // ================================================================ IBoatBrain

        public bool Held => _mode != Mode.Retreat;

        public Vector2Int Dir
        {
            get
            {
                if (_mode == Mode.Idle || _mode == Mode.Retreat) return Vector2Int.zero;
                while (_idx < _route.Count && _route[_idx] == _me.Cell) _idx++;
                if (_idx >= _route.Count) return Vector2Int.zero;
                var d = _route[_idx] - _me.Cell;
                return Mathf.Abs(d.x) + Mathf.Abs(d.y) == 1 ? d : Vector2Int.zero;
            }
        }

        // ================================================================ tick

        public void Tick(float dt)
        {
            if (_me.Stun > 0f)
            {
                // Kırıldık: planı bırak, sersemlik geçince yeniden düşün.
                Clear(Mode.Idle);
                _think = Mathf.Max(_think, _thinkDelay * 0.5f);
                return;
            }
            if (_me.Frozen > 0f) return;

            switch (_mode)
            {
                case Mode.Idle:
                    if (_me.Drawing) { _mode = Mode.Retreat; break; }
                    _think -= dt;
                    if (_think <= 0f && !_me.Moving) Plan();
                    break;

                case Mode.Cut:
                case Mode.Attack:
                    TickRoute(dt);
                    break;

                case Mode.Retreat:
                    if (!_me.Drawing) Finish();
                    break;
            }
        }

        void TickRoute(float dt)
        {
            // Rota bitti mi? (kesimde son hücre kapanış suyu)
            bool routeDone = _route.Count == 0 || _me.Cell == _route[_route.Count - 1];

            if (_mode == Mode.Attack)
            {
                bool targetGone = !_m.Grid.IsTrailOf(_attackCell.x, _attackCell.y, _rival.Id) || _rival.Invuln > 0f;
                if (routeDone || (targetGone && _me.Drawing)) { _mode = Mode.Retreat; return; }
                if (targetGone && !_me.Drawing) { Finish(); return; }
            }
            else if (routeDone)
            {
                if (_me.Drawing) _mode = Mode.Retreat; else Finish();
                return;
            }

            // Takıldık mı? (kenar değişti, yol kapandı...)
            if (_me.Moving || _me.Cell != _lastCell) _stuck = 0f;
            else _stuck += dt;
            _lastCell = _me.Cell;
            if (_stuck > 0.3f)
            {
                if (_me.Drawing) _mode = Mode.Retreat; else Finish();
                return;
            }

            // Çizerken tehlike: düşman ize ya da tekneye yaklaştıysa (tepki gecikmesiyle) geri sar.
            if (_me.Drawing && _mode == Mode.Cut)
            {
                int remaining = _route.Count - _idx;
                if (remaining > 3 && InDanger()) _danger += dt;
                else _danger = 0f;
                if (_danger > _reaction) _mode = Mode.Retreat;
            }
        }

        bool InDanger()
        {
            Vector2 bp = _me.Pos;
            foreach (var e in _m.AllEnemies)
            {
                if (!e.Alive) continue;
                float near = e.Radius + 1.5f + _margin * 0.5f;
                if (e.IsBoss && e.IsWindingUp && _m.ChargeTarget(e.Pos) == bp) return true;
                if ((e.Pos - bp).sqrMagnitude < (near + 1f) * (near + 1f)) return true;
                foreach (var c in _me.Trail)
                {
                    float dx = c.x + 0.5f - e.Pos.x, dy = c.y + 0.5f - e.Pos.y;
                    if (dx * dx + dy * dy < near * near) return true;
                }
            }
            return false;
        }

        void Clear(Mode mode)
        {
            _mode = mode;
            _route.Clear();
            _idx = 0;
            _danger = 0f;
            _stuck = 0f;
        }

        void Finish()
        {
            Clear(Mode.Idle);
            _think = _thinkDelay * Random.Range(0.6f, 1.4f);
        }

        // ================================================================ planlama

        void Plan()
        {
            var g = _m.Grid;
            EdgeBfs(g, _m.Grid.Index(_me.Cell.x, _me.Cell.y), 70);
            if (_edges.Count == 0) { Finish(); return; }

            if (_rival.Drawing && _rival.Invuln <= 0f && Random.value < _aggression && PlanAttack(g)) return;
            if (!PlanCut(g)) Finish();
        }

        /// <summary>Kenar hücreleri üzerinde BFS: _dist ve _parent doldurulur, ulaşılan hücreler _edges'e yazılır.</summary>
        void EdgeBfs(IceGrid g, int start, int maxDist)
        {
            System.Array.Fill(_dist, -1);
            _edges.Clear();
            _queue.Clear();
            _dist[start] = 0;
            _parent[start] = -1;
            _queue.Enqueue(start);
            while (_queue.Count > 0)
            {
                int i = _queue.Dequeue();
                _edges.Add(i);
                if (_dist[i] >= maxDist) continue;
                int x = i % g.Width, y = i / g.Width;
                foreach (var d in Dirs)
                {
                    int nx = x + d.x, ny = y + d.y;
                    if (!g.IsEdge(nx, ny)) continue;
                    int ni = g.Index(nx, ny);
                    if (_dist[ni] >= 0) continue;
                    _dist[ni] = _dist[i] + 1;
                    _parent[ni] = i;
                    _queue.Enqueue(ni);
                }
            }
        }

        /// <summary>BFS ağacından başlangıçtan verilen kenar hücresine giden rotayı _route'a yazar (başlangıç hariç).</summary>
        void BuildTravel(IceGrid g, int target)
        {
            _route.Clear();
            for (int i = target; i >= 0 && _parent[i] >= 0; i = _parent[i])
                _route.Add(new Vector2Int(i % g.Width, i / g.Width));
            _route.Reverse();
        }

        bool PlanCut(IceGrid g)
        {
            var seed = _m.BossSeed();
            float speed = BoatAgent.BaseSpeed;
            int initial = g.InitialIceCount;

            float bestScore = float.MinValue, secondScore = float.MinValue;
            int bestStart = -1, secondStart = -1;
            var best = new List<Vector2Int>();
            var second = new List<Vector2Int>();

            for (int n = 0; n < _candidates; n++)
            {
                // Yakın kenar hücrelerini tercih et.
                int pick = Mathf.Min(_edges.Count - 1, (int)(Mathf.Pow(Random.value, 1.6f) * _edges.Count));
                int s = _edges[pick];
                var sc = new Vector2Int(s % g.Width, s / g.Width);
                var d = Dirs[Random.Range(0, 4)];
                if (g.Get(sc.x + d.x, sc.y + d.y) != Cell.Ice) continue;

                var p = new Vector2Int(d.y, d.x) * (Random.value < 0.5f ? 1 : -1);
                int depth = Random.Range(3, _maxDepth + 1);
                int width = Random.Range(3, _maxWidth + 1);
                if (!TraceRect(g, sc, d, p, depth, width)) continue;

                _pathIdx.Clear();
                for (int i = 0; i < _cut.Count - 1; i++) _pathIdx.Add(g.Index(_cut[i].x, _cut[i].y));
                int cells = g.SimulateCut(_pathIdx, seed.x, seed.y, _mask);
                if (cells <= 0) continue;

                float travel = _dist[s];
                if (!SafePath(travel, speed)) continue;

                float points = cells * 1000f / initial;
                foreach (var e in _m.AllEnemies)
                {
                    if (!e.Alive || e.IsBoss) continue;
                    int ex = Mathf.FloorToInt(e.Pos.x), ey = Mathf.FloorToInt(e.Pos.y);
                    if (g.InBounds(ex, ey) && _mask[g.Index(ex, ey)]) points += e.IsGolden ? 150f : 50f;
                }
                foreach (var gc in _m.GiftCells)
                    if (g.InBounds(gc.x, gc.y) && _mask[g.Index(gc.x, gc.y)]) points += 30f;

                float time = (travel + _cut.Count) / speed + 0.6f;
                float score = points / time;

                if (score > bestScore)
                {
                    secondScore = bestScore; secondStart = bestStart; second.Clear(); second.AddRange(best);
                    bestScore = score; bestStart = s; best.Clear(); best.AddRange(_cut);
                }
                else if (score > secondScore)
                {
                    secondScore = score; secondStart = s; second.Clear(); second.AddRange(_cut);
                }
            }

            if (bestStart < 0) return false;
            bool sloppy = secondStart >= 0 && Random.value < _sloppy;
            int start = sloppy ? secondStart : bestStart;
            var cut = sloppy ? second : best;

            BuildTravel(g, start);
            _route.AddRange(cut);
            _idx = 0;
            _mode = Mode.Cut;
            _lastCell = _me.Cell;
            return true;
        }

        /// <summary>
        /// Kenar hücresi s'den d yönünde depth kadar içeri, p yönünde width kadar yana, sonra -d yönünde suya kadar.
        /// İlk su hücresinde kesim kapanır. Sonuç _cut'a yazılır (son eleman kapanış suyu).
        /// </summary>
        bool TraceRect(IceGrid g, Vector2Int s, Vector2Int d, Vector2Int p, int depth, int width)
        {
            _cut.Clear();
            var c = s;
            int limit = depth + width + depth + 4;
            for (int leg = 0; leg < 3; leg++)
            {
                var dir = leg == 0 ? d : leg == 1 ? p : -d;
                int steps = leg == 0 ? depth : leg == 1 ? width : limit;
                for (int k = 0; k < steps; k++)
                {
                    c += dir;
                    if (!g.InBounds(c.x, c.y)) return false;
                    var cell = g.Get(c.x, c.y);
                    if (cell == Cell.Trail) return false;
                    _cut.Add(c);
                    if (cell == Cell.Water) return _cut.Count > 1 && g.IsEdge(c.x, c.y);
                }
            }
            return false;
        }

        /// <summary>Planlanan izin her hücresi, açık kalacağı süre boyunca düşmanların erişebileceği mesafeden uzak mı?</summary>
        bool SafePath(float travel, float speed)
        {
            int count = _cut.Count;
            foreach (var e in _m.AllEnemies)
            {
                if (!e.Alive) continue;
                float reach = e.IsBoss && e.ChargeInterval > 0f ? 0.6f : 0.35f;
                for (int k = 0; k < count - 1; k++)
                {
                    var c = _cut[k];
                    float exposure = (count - k) / speed + (k == 0 ? travel / speed * 0.3f : 0f);
                    float dx = c.x + 0.5f - e.Pos.x, dy = c.y + 0.5f - e.Pos.y;
                    float safe = e.Radius + _margin + e.Speed * exposure * reach;
                    if (dx * dx + dy * dy < safe * safe) return false;
                }
            }
            return true;
        }

        /// <summary>Rakibin açık izine kenardan düz bir çizgiyle ulaşılabiliyorsa saldırı rotası kurar.</summary>
        bool PlanAttack(IceGrid g)
        {
            int bestDist = int.MaxValue;
            int bestStart = -1;
            Vector2Int bestDir = default, bestHit = default;
            int maxReach = Mathf.RoundToInt(Mathf.Lerp(8f, 16f, _skill));

            foreach (int s in _edges)
            {
                if (_dist[s] > 30) continue;
                var sc = new Vector2Int(s % g.Width, s / g.Width);
                foreach (var d in Dirs)
                {
                    var c = sc;
                    for (int k = 1; k <= maxReach; k++)
                    {
                        c += d;
                        var cell = g.Get(c.x, c.y);
                        if (cell == Cell.Trail)
                        {
                            if (g.OwnerAt(c.x, c.y) == _rival.Id && _dist[s] + k < bestDist)
                            {
                                bestDist = _dist[s] + k;
                                bestStart = s;
                                bestDir = d;
                                bestHit = c;
                            }
                            break;
                        }
                        if (cell != Cell.Ice) break;
                    }
                }
            }

            if (bestStart < 0) return false;
            BuildTravel(g, bestStart);
            var p = new Vector2Int(bestStart % g.Width, bestStart / g.Width);
            while (p != bestHit)
            {
                p += bestDir;
                _route.Add(p);
            }
            _attackCell = bestHit;
            _idx = 0;
            _mode = Mode.Attack;
            _lastCell = _me.Cell;
            return true;
        }
    }
}
