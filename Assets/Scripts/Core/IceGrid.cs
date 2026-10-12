using System.Collections.Generic;

namespace Icefied.Core
{
    public enum Cell : byte
    {
        Ice = 0,   // henüz kesilmemiş buz (düşmanlar burada yaşar)
        Water = 1, // kesilmiş / ele geçirilmiş alan
        Trail = 2  // bir oyuncunun buzda bıraktığı çatlak izi (sahibi Owner dizisinde)
    }

    /// <summary>
    /// Volfied tarzı alan kesme mantığı. Unity'den bağımsız, saf C#.
    /// Koordinatlar: x = 0..Width-1 (sol→sağ), y = 0..Height-1 (alt→üst).
    /// İz hücreleri hâlâ buz sayılır: yüzde sadece gerçekten kopan alanı gösterir.
    /// </summary>
    public class IceGrid
    {
        public readonly int Width;
        public readonly int Height;
        public readonly Cell[] Cells;
        public readonly byte[] Owner;

        public int InitialIceCount { get; private set; }

        /// <summary>Buz + iz hücreleri (henüz kopmamış her şey).</summary>
        public int IceCount { get; private set; }

        public float ClaimedPercent =>
            InitialIceCount == 0 ? 0f : 100f * (InitialIceCount - IceCount) / InitialIceCount;

        static readonly int[] Dx4 = { 1, -1, 0, 0 };
        static readonly int[] Dy4 = { 0, 0, 1, -1 };

        readonly Queue<int> _queue = new Queue<int>();
        readonly bool[] _visited;
        readonly bool[] _blocked;

        public IceGrid(int width, int height)
        {
            Width = width;
            Height = height;
            Cells = new Cell[width * height];
            Owner = new byte[width * height];
            _visited = new bool[width * height];
            _blocked = new bool[width * height];
            Reset();
        }

        /// <summary>Kenarda 1 hücrelik su çerçevesi, içi tamamen buz.</summary>
        public void Reset()
        {
            IceCount = 0;
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                bool frame = x == 0 || y == 0 || x == Width - 1 || y == Height - 1;
                Cells[Index(x, y)] = frame ? Cell.Water : Cell.Ice;
                if (!frame) IceCount++;
            }
            InitialIceCount = IceCount;
        }

        public int Index(int x, int y) => y * Width + x;
        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;
        public Cell Get(int x, int y) => InBounds(x, y) ? Cells[Index(x, y)] : Cell.Water;
        public int OwnerAt(int x, int y) => InBounds(x, y) ? Owner[Index(x, y)] : -1;

        public static bool IsIceLike(Cell c) => c == Cell.Ice || c == Cell.Trail;

        public void Set(int x, int y, Cell c, int owner = 0)
        {
            int i = Index(x, y);
            bool was = IsIceLike(Cells[i]);
            bool now = IsIceLike(c);
            if (was && !now) IceCount--;
            else if (!was && now) IceCount++;
            Cells[i] = c;
            Owner[i] = (byte)owner;
        }

        public bool IsTrailOf(int x, int y, int owner)
        {
            return InBounds(x, y) && Cells[Index(x, y)] == Cell.Trail && Owner[Index(x, y)] == owner;
        }

        /// <summary>Düşmanlar Ice ve Trail üzerinde hareket edebilir.</summary>
        public bool IsEnemySpace(int x, int y) => IsIceLike(Get(x, y));

        /// <summary>Kenar = 8 komşusundan en az biri buz/iz olan su hücresi. Tekne güvenle burada dolaşır.</summary>
        public bool IsEdge(int x, int y)
        {
            if (Get(x, y) != Cell.Water) return false;
            for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                if (ox == 0 && oy == 0) continue;
                if (IsIceLike(Get(x + ox, y + oy))) return true;
            }
            return false;
        }

        /// <summary>
        /// İz kapandığında çağrılır. İz suya döner; boss'un bulunduğu bölge kalır, ulaşılamayan her şey kopar.
        /// Başka oyuncuların açık izleri duvar sayılmaz (buz gibi geçilir); kopan bölgede kalırlarsa onlar da kopar.
        /// Kopan hücreler <paramref name="detached"/>'a yazılır.
        /// </summary>
        public void CloseTrail(IList<(int x, int y)> trail, int seedX, int seedY, List<int> detached)
        {
            detached.Clear();
            foreach (var (x, y) in trail)
            {
                Set(x, y, Cell.Water);
                detached.Add(Index(x, y));
            }

            System.Array.Clear(_visited, 0, _visited.Length);
            Flood(seedX, seedY, null);

            for (int i = 0; i < Cells.Length; i++)
            {
                if (!IsIceLike(Cells[i]) || _visited[i]) continue;
                Cells[i] = Cell.Water;
                IceCount--;
                detached.Add(i);
            }
        }

        /// <summary>
        /// Bot için: verilen yol suya dönse kaç hücre koparırdı? Grid'i değiştirmez.
        /// mask verilirse kopacak hücreler true olarak işaretlenir. Boss yolun üstündeyse -1 döner.
        /// </summary>
        public int SimulateCut(List<int> path, int seedX, int seedY, bool[] mask)
        {
            System.Array.Clear(_blocked, 0, _blocked.Length);
            foreach (int i in path) _blocked[i] = true;
            if (InBounds(seedX, seedY) && _blocked[Index(seedX, seedY)]) return -1;

            System.Array.Clear(_visited, 0, _visited.Length);
            Flood(seedX, seedY, _blocked);

            if (mask != null) System.Array.Clear(mask, 0, mask.Length);
            int count = 0;
            for (int i = 0; i < Cells.Length; i++)
            {
                if (!IsIceLike(Cells[i]) || _visited[i]) continue;
                count++;
                if (mask != null) mask[i] = true;
            }
            return count;
        }

        void Flood(int sx, int sy, bool[] blocked)
        {
            _queue.Clear();
            if (!InBounds(sx, sy)) return;
            int start = Index(sx, sy);
            if (!IsIceLike(Cells[start]) || (blocked != null && blocked[start])) return;
            _visited[start] = true;
            _queue.Enqueue(start);

            while (_queue.Count > 0)
            {
                int i = _queue.Dequeue();
                int cx = i % Width, cy = i / Width;
                for (int d = 0; d < 4; d++)
                {
                    int nx = cx + Dx4[d], ny = cy + Dy4[d];
                    if (!InBounds(nx, ny)) continue;
                    int ni = Index(nx, ny);
                    if (_visited[ni] || !IsIceLike(Cells[ni]) || (blocked != null && blocked[ni])) continue;
                    _visited[ni] = true;
                    _queue.Enqueue(ni);
                }
            }
        }

        /// <summary>Ölünce, kırılınca ya da geri sarınca bir oyuncunun iz hücrelerini tekrar buza çevirir.</summary>
        public void ClearTrail(IList<(int x, int y)> trail, int owner)
        {
            foreach (var (x, y) in trail)
                if (IsTrailOf(x, y, owner)) Set(x, y, Cell.Ice);
        }
    }
}
