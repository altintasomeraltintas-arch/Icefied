using System.Collections.Generic;
using UnityEngine;

namespace Icefied.View
{
    /// <summary>Resources/Icefied altındaki shader'lardan malzeme üretir ve önbelleğe alır.</summary>
    public static class Mats
    {
        static Shader _toon, _ice, _water, _fx;
        static readonly Dictionary<Color, Material> ToonCache = new Dictionary<Color, Material>();

        public static Material Ice { get; private set; }
        public static Material Water { get; private set; }

        public static void Init()
        {
            _toon = Load("Toon");
            _ice = Load("Ice");
            _water = Load("Water");
            _fx = Load("Fx");
            Ice = new Material(_ice) { name = "Ice" };
            Water = new Material(_water) { name = "Water" };
        }

        static Shader Load(string name)
        {
            var s = Resources.Load<Shader>("Icefied/" + name);
            if (s == null)
            {
                Debug.LogError($"[Icefied] Shader bulunamadı: Icefied/{name}");
                s = Shader.Find("Universal Render Pipeline/Unlit");
            }
            return s;
        }

        public static Material Toon(Color c, float gloss = 0.35f, float rim = 0.35f)
        {
            if (ToonCache.TryGetValue(c, out var m)) return m;
            m = new Material(_toon) { name = "Toon" };
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Gloss", gloss);
            m.SetFloat("_RimStrength", rim);
            ToonCache[c] = m;
            return m;
        }

        /// <summary>Paylaşılmayan (flash gibi animasyonlu) bir toon malzemesi.</summary>
        public static Material ToonUnique(Color c)
        {
            var m = new Material(_toon) { name = "ToonUnique" };
            m.SetColor("_BaseColor", c);
            return m;
        }

        public static Material Fx(Color c, int shape, bool vertexColor, bool additive = false)
        {
            var m = new Material(_fx) { name = "Fx" };
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Shape", shape);
            m.SetFloat("_UseVertexColor", vertexColor ? 1f : 0f);
            m.SetFloat("_DstBlend", additive ? (float)UnityEngine.Rendering.BlendMode.One
                                             : (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            return m;
        }

        public static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }
}
