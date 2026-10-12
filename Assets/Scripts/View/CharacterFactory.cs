using Icefied.Core;
using UnityEngine;

namespace Icefied.View
{
    /// <summary>
    /// Tüm karakterleri Unity primitive'lerinden kurar (asset gerekmez).
    /// Her model "Body" adlı bir alt pivot içerir; animasyonlar ona uygulanır. İleri yön = +Z.
    /// </summary>
    public static class CharacterFactory
    {
        static readonly Color Navy = Mats.Hex("#2A3B66");
        static readonly Color White = Mats.Hex("#F7FBFF");
        static readonly Color Orange = Mats.Hex("#FF9A2E");
        static readonly Color Black = Mats.Hex("#14161F");

        public static readonly Color RedHull = Mats.Hex("#FF4B4B");
        public static readonly Color BlueHull = Mats.Hex("#3D8BFF");

        static Mesh _blobMesh;
        static Material _blobMat;

        public static Transform Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Color color,
                                     Vector3 euler = default, float gloss = 0.35f)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.Destroy(go.GetComponent<Collider>());
            var t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = pos;
            t.localScale = scale;
            t.localRotation = Quaternion.Euler(euler);
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = Mats.Toon(color, gloss);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return t;
        }

        static Transform Sphere(Transform p, Vector3 pos, Vector3 scale, Color c, Vector3 euler = default, float gloss = 0.35f)
            => Part(PrimitiveType.Sphere, p, pos, scale, c, euler, gloss);

        static Transform Cube(Transform p, Vector3 pos, Vector3 scale, Color c, Vector3 euler = default)
            => Part(PrimitiveType.Cube, p, pos, scale, c, euler);

        static Transform Cyl(Transform p, Vector3 pos, Vector3 scale, Color c, Vector3 euler = default)
            => Part(PrimitiveType.Cylinder, p, pos, scale, c, euler);

        static (Transform root, Transform body) Root(string name, Transform parent, float scale)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            var body = new GameObject("Body").transform;
            body.SetParent(root, false);
            body.localScale = Vector3.one * scale;
            return (root, body);
        }

        public static Transform BlobShadow(Transform parent, float size, float alpha = 0.32f)
        {
            if (_blobMesh == null)
            {
                _blobMesh = IceBoardView.FlatQuad(1f, Color.white);
                _blobMat = Mats.Fx(new Color(0.02f, 0.12f, 0.25f, 1f), 2, true);
            }
            var go = new GameObject("Shadow");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0, 0.006f, 0);
            go.transform.localScale = Vector3.one * size;
            go.AddComponent<MeshFilter>().sharedMesh = _blobMesh;
            var mr = go.AddComponent<MeshRenderer>();
            var mat = new Material(_blobMat);
            mat.SetColor("_BaseColor", new Color(0.02f, 0.12f, 0.25f, alpha));
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        static void Eye(Transform p, Vector3 pos, float size)
        {
            Sphere(p, pos, Vector3.one * size, Black, default, 0.9f);
            Sphere(p, pos + new Vector3(size * 0.18f, size * 0.22f, size * 0.38f), Vector3.one * size * 0.35f, Color.white);
        }

        // ------------------------------------------------------------------ boat

        public static Transform Boat(Transform parent, float scale, Color hull)
        {
            var (root, b) = Root("Boat", parent, scale);
            var red = hull;
            var darkRed = new Color(hull.r * 0.78f, hull.g * 0.72f, hull.b * 0.8f);
            var yellow = Mats.Hex("#FFC93C");
            var glass = Mats.Hex("#3FA9F5");

            Cube(b, new Vector3(0, 0.06f, -0.05f), new Vector3(0.62f, 0.26f, 0.95f), red);
            Cube(b, new Vector3(0, 0.06f, 0.42f), new Vector3(0.44f, 0.26f, 0.44f), red, new Vector3(0, 45, 0));
            Cube(b, new Vector3(0, -0.05f, 0.1f), new Vector3(0.5f, 0.1f, 1.1f), darkRed);
            Cube(b, new Vector3(0, 0.2f, -0.05f), new Vector3(0.64f, 0.05f, 0.97f), White);
            Cube(b, new Vector3(0, 0.2f, 0.42f), new Vector3(0.46f, 0.05f, 0.46f), White, new Vector3(0, 45, 0));
            Cube(b, new Vector3(0, 0.36f, -0.2f), new Vector3(0.44f, 0.3f, 0.42f), White);
            Cube(b, new Vector3(0, 0.42f, 0.015f), new Vector3(0.36f, 0.1f, 0.02f), glass);
            Cube(b, new Vector3(0, 0.53f, -0.2f), new Vector3(0.5f, 0.05f, 0.48f), red);
            Cyl(b, new Vector3(0.1f, 0.66f, -0.3f), new Vector3(0.13f, 0.12f, 0.13f), yellow);
            Cyl(b, new Vector3(0.1f, 0.79f, -0.3f), new Vector3(0.14f, 0.02f, 0.14f), Navy);
            BlobShadow(root, scale * 1.2f, 0.22f);
            return root;
        }

        // ------------------------------------------------------------------ penguin

        public static Transform Penguin(Transform parent, float scale, bool golden = false)
        {
            var (root, b) = Root(golden ? "GoldenPenguin" : "Penguin", parent, scale);
            var Navy = golden ? Mats.Hex("#F2B320") : CharacterFactory.Navy;
            Sphere(b, new Vector3(0, 0.45f, 0), new Vector3(0.72f, 0.92f, 0.66f), Navy);
            Sphere(b, new Vector3(0, 0.42f, 0.16f), new Vector3(0.54f, 0.72f, 0.42f), White);
            Sphere(b, new Vector3(0, 0.98f, 0), new Vector3(0.58f, 0.55f, 0.55f), Navy);
            Sphere(b, new Vector3(0, 0.95f, 0.15f), new Vector3(0.44f, 0.38f, 0.32f), White);
            Eye(b, new Vector3(-0.1f, 1.02f, 0.28f), 0.1f);
            Eye(b, new Vector3(0.1f, 1.02f, 0.28f), 0.1f);
            Sphere(b, new Vector3(0, 0.92f, 0.35f), new Vector3(0.17f, 0.1f, 0.24f), Orange);
            Sphere(b, new Vector3(-0.14f, 0.03f, 0.1f), new Vector3(0.2f, 0.07f, 0.3f), Orange);
            Sphere(b, new Vector3(0.14f, 0.03f, 0.1f), new Vector3(0.2f, 0.07f, 0.3f), Orange);
            Sphere(b, new Vector3(-0.37f, 0.52f, 0), new Vector3(0.12f, 0.44f, 0.3f), Navy, new Vector3(0, 0, -22));
            Sphere(b, new Vector3(0.37f, 0.52f, 0), new Vector3(0.12f, 0.44f, 0.3f), Navy, new Vector3(0, 0, 22));
            Sphere(b, new Vector3(-0.2f, 0.88f, 0.27f), new Vector3(0.08f, 0.05f, 0.04f), Mats.Hex("#FF9FB1"));
            Sphere(b, new Vector3(0.2f, 0.88f, 0.27f), new Vector3(0.08f, 0.05f, 0.04f), Mats.Hex("#FF9FB1"));
            if (golden)
            {
                var gold = Mats.Hex("#FFD84A");
                Cyl(b, new Vector3(0, 1.3f, 0), new Vector3(0.34f, 0.06f, 0.34f), gold);
                for (int i = 0; i < 5; i++)
                {
                    float a = i * Mathf.PI * 2f / 5f;
                    Sphere(b, new Vector3(Mathf.Cos(a) * 0.14f, 1.4f, Mathf.Sin(a) * 0.14f), Vector3.one * 0.09f, gold, default, 0.9f);
                }
            }
            BlobShadow(root, scale * 0.9f);
            return root;
        }

        // ------------------------------------------------------------------ bosses

        public static Transform Boss(BossKind kind, Transform parent, float scale)
        {
            switch (kind)
            {
                case BossKind.Walrus: return Walrus(parent, scale);
                case BossKind.PolarBear: return Bear(parent, scale, false);
                default: return Bear(parent, scale, true);
            }
        }

        static Transform Walrus(Transform parent, float scale)
        {
            var (root, b) = Root("Walrus", parent, scale);
            var brown = Mats.Hex("#B7795A");
            var light = Mats.Hex("#E7B48F");
            var ivory = Mats.Hex("#FFF6DE");
            Sphere(b, new Vector3(0, 0.55f, -0.25f), new Vector3(1.6f, 1.1f, 1.9f), brown);
            Sphere(b, new Vector3(0, 0.98f, 0.6f), new Vector3(1.05f, 0.9f, 0.95f), brown);
            Sphere(b, new Vector3(-0.18f, 0.82f, 1.0f), new Vector3(0.44f, 0.38f, 0.4f), light);
            Sphere(b, new Vector3(0.18f, 0.82f, 1.0f), new Vector3(0.44f, 0.38f, 0.4f), light);
            Sphere(b, new Vector3(0, 0.98f, 1.1f), new Vector3(0.24f, 0.15f, 0.15f), Mats.Hex("#5A3426"), default, 0.8f);
            Cyl(b, new Vector3(-0.17f, 0.5f, 1.12f), new Vector3(0.09f, 0.26f, 0.09f), ivory, new Vector3(12, 0, 0));
            Cyl(b, new Vector3(0.17f, 0.5f, 1.12f), new Vector3(0.09f, 0.26f, 0.09f), ivory, new Vector3(12, 0, 0));
            Eye(b, new Vector3(-0.26f, 1.2f, 0.98f), 0.14f);
            Eye(b, new Vector3(0.26f, 1.2f, 0.98f), 0.14f);
            Sphere(b, new Vector3(-0.78f, 0.12f, 0.45f), new Vector3(0.55f, 0.12f, 0.75f), brown, new Vector3(0, -30, 0));
            Sphere(b, new Vector3(0.78f, 0.12f, 0.45f), new Vector3(0.55f, 0.12f, 0.75f), brown, new Vector3(0, 30, 0));
            Sphere(b, new Vector3(-0.35f, 0.1f, -1.15f), new Vector3(0.5f, 0.1f, 0.6f), brown, new Vector3(0, 35, 0));
            Sphere(b, new Vector3(0.35f, 0.1f, -1.15f), new Vector3(0.5f, 0.1f, 0.6f), brown, new Vector3(0, -35, 0));
            BlobShadow(root, scale * 2.4f);
            return root;
        }

        static Transform Bear(Transform parent, float scale, bool yeti)
        {
            var (root, b) = Root(yeti ? "Yeti" : "PolarBear", parent, scale);
            var fur = yeti ? Mats.Hex("#7E8CFF") : Mats.Hex("#FFE7BF");
            var snout = yeti ? Mats.Hex("#C9D0FF") : Mats.Hex("#FFF6E4");
            var dark = yeti ? Mats.Hex("#1E2257") : Black;

            Sphere(b, new Vector3(0, 0.75f, -0.15f), new Vector3(1.45f, 1.25f, 1.95f), fur);
            Sphere(b, new Vector3(0, 1.22f, 0.8f), new Vector3(0.95f, 0.9f, 0.9f), fur);
            Sphere(b, new Vector3(0, 1.08f, 1.2f), new Vector3(0.48f, 0.4f, 0.5f), snout);
            Sphere(b, new Vector3(0, 1.2f, 1.44f), new Vector3(0.2f, 0.14f, 0.13f), dark, default, 0.9f);
            Sphere(b, new Vector3(-0.34f, 1.62f, 0.72f), new Vector3(0.3f, 0.3f, 0.16f), fur);
            Sphere(b, new Vector3(0.34f, 1.62f, 0.72f), new Vector3(0.3f, 0.3f, 0.16f), fur);
            Eye(b, new Vector3(-0.22f, 1.38f, 1.15f), 0.13f);
            Eye(b, new Vector3(0.22f, 1.38f, 1.15f), 0.13f);
            for (int i = 0; i < 4; i++)
            {
                float x = i % 2 == 0 ? -0.48f : 0.48f;
                float z = i < 2 ? 0.55f : -0.75f;
                Sphere(b, new Vector3(x, 0.3f, z), new Vector3(0.48f, 0.62f, 0.52f), fur);
            }

            if (yeti)
            {
                var horn = Mats.Hex("#FFE7A8");
                Cyl(b, new Vector3(-0.42f, 1.75f, 0.75f), new Vector3(0.14f, 0.22f, 0.14f), horn, new Vector3(0, 0, 35));
                Cyl(b, new Vector3(-0.6f, 2.0f, 0.75f), new Vector3(0.1f, 0.14f, 0.1f), horn, new Vector3(0, 0, 10));
                Cyl(b, new Vector3(0.42f, 1.75f, 0.75f), new Vector3(0.14f, 0.22f, 0.14f), horn, new Vector3(0, 0, -35));
                Cyl(b, new Vector3(0.6f, 2.0f, 0.75f), new Vector3(0.1f, 0.14f, 0.1f), horn, new Vector3(0, 0, -10));
                Cube(b, new Vector3(-0.22f, 1.55f, 1.18f), new Vector3(0.26f, 0.06f, 0.06f), dark, new Vector3(0, 0, -20));
                Cube(b, new Vector3(0.22f, 1.55f, 1.18f), new Vector3(0.26f, 0.06f, 0.06f), dark, new Vector3(0, 0, 20));
            }

            BlobShadow(root, scale * 2.4f);
            return root;
        }

        // ------------------------------------------------------------------ gift

        public static Transform Gift(GiftKind kind, Transform parent, float scale)
        {
            var (root, b) = Root("Gift", parent, scale);
            var box = kind == GiftKind.Freeze ? Mats.Hex("#38C6FF") : Mats.Hex("#FFC93C");
            var ribbon = kind == GiftKind.Freeze ? White : Mats.Hex("#FF4B6E");
            Cube(b, new Vector3(0, 0.5f, 0), new Vector3(0.9f, 0.8f, 0.9f), box);
            Cube(b, new Vector3(0, 0.95f, 0), new Vector3(1.0f, 0.18f, 1.0f), box);
            Cube(b, new Vector3(0, 0.55f, 0), new Vector3(0.2f, 0.92f, 0.96f), ribbon);
            Cube(b, new Vector3(0, 0.55f, 0), new Vector3(0.96f, 0.92f, 0.2f), ribbon);
            Sphere(b, new Vector3(-0.17f, 1.12f, 0), new Vector3(0.34f, 0.2f, 0.18f), ribbon, new Vector3(0, 0, 25));
            Sphere(b, new Vector3(0.17f, 1.12f, 0), new Vector3(0.34f, 0.2f, 0.18f), ribbon, new Vector3(0, 0, -25));
            BlobShadow(root, scale * 1.4f);
            return root;
        }
    }
}
