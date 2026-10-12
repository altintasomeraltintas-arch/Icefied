using System;
using System.Collections.Generic;
using Icefied.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Icefied.View
{
    /// <summary>
    /// Grid'deki buz hücrelerinden kalın bir buz tabakası mesh'i üretir ve
    /// su/buz shader'larının kullandığı maske texture'ını günceller (R = buz, G = iz, B = iz mavi oyuncunun).
    /// </summary>
    public class IceBoardView
    {
        readonly IceGrid _grid;
        readonly float _cell;
        readonly float _top;
        readonly float _bottom;
        readonly Mesh _mesh;
        readonly Texture2D _mask;
        readonly Color32[] _pixels;

        public GameObject Root { get; }

        public IceBoardView(IceGrid grid, float cell, float top, float bottom, Transform parent)
        {
            _grid = grid;
            _cell = cell;
            _top = top;
            _bottom = bottom;

            Root = new GameObject("IceBoard");
            Root.transform.SetParent(parent, false);
            _mesh = new Mesh { name = "IceSlab", indexFormat = IndexFormat.UInt32 };
            _mesh.MarkDynamic();
            Root.AddComponent<MeshFilter>().sharedMesh = _mesh;
            var mr = Root.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mats.Ice;
            mr.shadowCastingMode = ShadowCastingMode.Off;

            _mask = new Texture2D(grid.Width, grid.Height, TextureFormat.RGBA32, false, true)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                name = "IceMask"
            };
            _pixels = new Color32[grid.Width * grid.Height];

            float w = grid.Width * cell, h = grid.Height * cell;
            Shader.SetGlobalTexture("_IceMask", _mask);
            Shader.SetGlobalVector("_BoardRect", new Vector4(-w * 0.5f, -h * 0.5f, w, h));
            Shader.SetGlobalVector("_MaskTexel", new Vector4(1f / grid.Width, 1f / grid.Height, 0, 0));
            Shader.SetGlobalFloat("_IceTopY", top);

            Refresh();
        }

        public void Refresh()
        {
            BuildSlab(_mesh, (x, y) => _grid.Get(x, y) == Cell.Ice, _grid.Width, _grid.Height, _cell, _top, _bottom, Vector3.zero);

            var cells = _grid.Cells;
            for (int i = 0; i < cells.Length; i++)
            {
                byte r = cells[i] == Cell.Ice ? (byte)255 : (byte)0;
                byte g = cells[i] == Cell.Trail ? (byte)255 : (byte)0;
                byte b = cells[i] == Cell.Trail && _grid.Owner[i] == 1 ? (byte)255 : (byte)0;
                _pixels[i] = new Color32(r, g, b, 255);
            }
            _mask.SetPixels32(_pixels);
            _mask.Apply(false);
        }

        public void Destroy()
        {
            UnityEngine.Object.Destroy(Root);
            UnityEngine.Object.Destroy(_mesh);
            UnityEngine.Object.Destroy(_mask);
        }

        // ---------------------------------------------------------------- mesh

        static readonly List<Vector3> V = new List<Vector3>(8192);
        static readonly List<Vector3> N = new List<Vector3>(8192);
        static readonly List<int> T = new List<int>(16384);

        /// <summary>
        /// solid(x,y) true olan hücreleri üst yüzey + dış duvarlar olarak mesh'e döker.
        /// Satır bazlı birleştirme yapar, böylece birkaç yüz quad ile biter.
        /// pivot: tüm vertex'lerden çıkarılan dünya ofseti (kopan parçaları kendi merkezinde döndürmek için).
        /// </summary>
        public static void BuildSlab(Mesh mesh, Func<int, int, bool> solid, int width, int height,
                                     float cell, float top, float bottom, Vector3 pivot)
        {
            V.Clear(); N.Clear(); T.Clear();
            float ox = -width * 0.5f * cell - pivot.x;
            float oz = -height * 0.5f * cell - pivot.z;
            float yt = top - pivot.y, yb = bottom - pivot.y;

            bool S(int x, int y) => x >= 0 && y >= 0 && x < width && y < height && solid(x, y);
            float X(int x) => ox + x * cell;
            float Z(int y) => oz + y * cell;

            for (int y = 0; y < height; y++)
            {
                // üst yüzey: yatay koşular
                int x = 0;
                while (x < width)
                {
                    if (!S(x, y)) { x++; continue; }
                    int x0 = x;
                    while (x < width && S(x, y)) x++;
                    AddQuad(new Vector3(X(x0), yt, Z(y)), new Vector3(X(x0), yt, Z(y + 1)),
                            new Vector3(X(x), yt, Z(y + 1)), new Vector3(X(x), yt, Z(y)), Vector3.up);
                }

                // güney duvarlar (-z)
                x = 0;
                while (x < width)
                {
                    if (!(S(x, y) && !S(x, y - 1))) { x++; continue; }
                    int x0 = x;
                    while (x < width && S(x, y) && !S(x, y - 1)) x++;
                    AddQuad(new Vector3(X(x0), yb, Z(y)), new Vector3(X(x0), yt, Z(y)),
                            new Vector3(X(x), yt, Z(y)), new Vector3(X(x), yb, Z(y)), Vector3.back);
                }

                // kuzey duvarlar (+z)
                x = 0;
                while (x < width)
                {
                    if (!(S(x, y) && !S(x, y + 1))) { x++; continue; }
                    int x0 = x;
                    while (x < width && S(x, y) && !S(x, y + 1)) x++;
                    AddQuad(new Vector3(X(x0), yb, Z(y + 1)), new Vector3(X(x0), yt, Z(y + 1)),
                            new Vector3(X(x), yt, Z(y + 1)), new Vector3(X(x), yb, Z(y + 1)), Vector3.forward);
                }
            }

            for (int x = 0; x < width; x++)
            {
                // batı duvarlar (-x)
                int y = 0;
                while (y < height)
                {
                    if (!(S(x, y) && !S(x - 1, y))) { y++; continue; }
                    int y0 = y;
                    while (y < height && S(x, y) && !S(x - 1, y)) y++;
                    AddQuad(new Vector3(X(x), yb, Z(y0)), new Vector3(X(x), yt, Z(y0)),
                            new Vector3(X(x), yt, Z(y)), new Vector3(X(x), yb, Z(y)), Vector3.left);
                }

                // doğu duvarlar (+x)
                y = 0;
                while (y < height)
                {
                    if (!(S(x, y) && !S(x + 1, y))) { y++; continue; }
                    int y0 = y;
                    while (y < height && S(x, y) && !S(x + 1, y)) y++;
                    AddQuad(new Vector3(X(x + 1), yb, Z(y0)), new Vector3(X(x + 1), yt, Z(y0)),
                            new Vector3(X(x + 1), yt, Z(y)), new Vector3(X(x + 1), yb, Z(y)), Vector3.right);
                }
            }

            mesh.Clear();
            mesh.SetVertices(V);
            mesh.SetNormals(N);
            mesh.SetTriangles(T, 0);
            mesh.RecalculateBounds();
        }

        /// <summary>4 köşe sırayla verilir; yüz normal yönünden görünecek şekilde sarım otomatik düzeltilir.</summary>
        static void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 normal)
        {
            int i = V.Count;
            V.Add(a); V.Add(b); V.Add(c); V.Add(d);
            N.Add(normal); N.Add(normal); N.Add(normal); N.Add(normal);
            bool front = Vector3.Dot(Vector3.Cross(b - a, c - a), normal) > 0f;
            if (front)
            {
                T.Add(i); T.Add(i + 1); T.Add(i + 2);
                T.Add(i); T.Add(i + 2); T.Add(i + 3);
            }
            else
            {
                T.Add(i); T.Add(i + 2); T.Add(i + 1);
                T.Add(i); T.Add(i + 3); T.Add(i + 2);
            }
        }

        public static Mesh FlatQuad(float size, Color color)
        {
            float h = size * 0.5f;
            var m = new Mesh { name = "FlatQuad" };
            m.SetVertices(new List<Vector3> { new Vector3(-h, 0, -h), new Vector3(-h, 0, h), new Vector3(h, 0, h), new Vector3(h, 0, -h) });
            m.SetNormals(new List<Vector3> { Vector3.up, Vector3.up, Vector3.up, Vector3.up });
            m.SetUVs(0, new List<Vector2> { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) });
            m.SetColors(new List<Color> { color, color, color, color });
            m.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            m.RecalculateBounds();
            return m;
        }
    }
}
