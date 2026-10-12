using System;
using Icefied.View;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Icefied.UI
{
    /// <summary>Asset'siz UI: yuvarlak köşeli paneller, yıldız/kalp sprite'ları ve "tombul" mobil butonlar.</summary>
    public static class UIFactory
    {
        public static Font Font { get; private set; }
        public static Sprite Rounded { get; private set; }
        public static Sprite Circle { get; private set; }
        public static Sprite Ring { get; private set; }
        public static Sprite Star { get; private set; }
        public static Sprite Heart { get; private set; }

        public static void Init()
        {
            Font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Rounded = MakeRounded(128, 52);
            Circle = MakeSdf(128, (x, y) => Mathf.Sqrt(x * x + y * y) - 62f);
            Ring = MakeSdf(128, (x, y) => Mathf.Abs(Mathf.Sqrt(x * x + y * y) - 54f) - 7f);
            Star = MakeSupersampled(128, InStar);
            Heart = MakeSupersampled(128, InHeart);
        }

        // ------------------------------------------------------------ sprites

        static Texture2D NewTex(int size)
        {
            return new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };
        }

        static Sprite MakeRounded(int size, float radius)
        {
            var tex = NewTex(size);
            var px = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float qx = Mathf.Abs(x + 0.5f - half) - (half - radius);
                float qy = Mathf.Abs(y + 0.5f - half) - (half - radius);
                float outside = new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude;
                float d = outside + Mathf.Min(Mathf.Max(qx, qy), 0) - radius;
                px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(0.5f - d) * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            float b = radius + 2;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                                 SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        static Sprite MakeSdf(int size, Func<float, float, float> sdf)
        {
            var tex = NewTex(size);
            var px = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = sdf(x + 0.5f - half, y + 0.5f - half);
                px[y * size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(0.5f - d) * 255));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        static Sprite MakeSupersampled(int size, Func<float, float, bool> inside)
        {
            var tex = NewTex(size);
            var px = new Color32[size * size];
            const int ss = 4;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int hits = 0;
                for (int sy = 0; sy < ss; sy++)
                for (int sx = 0; sx < ss; sx++)
                {
                    float u = (x + (sx + 0.5f) / ss) / size * 2f - 1f;
                    float v = (y + (sy + 0.5f) / ss) / size * 2f - 1f;
                    if (inside(u, v)) hits++;
                }
                px[y * size + x] = new Color32(255, 255, 255, (byte)(hits * 255 / (ss * ss)));
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        static readonly Vector2[] StarPts = BuildStar();

        static Vector2[] BuildStar()
        {
            var pts = new Vector2[10];
            for (int i = 0; i < 10; i++)
            {
                float r = i % 2 == 0 ? 0.96f : 0.45f;
                float a = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                pts[i] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r - 0.05f);
            }
            return pts;
        }

        static bool InStar(float x, float y)
        {
            bool inside = false;
            for (int i = 0, j = StarPts.Length - 1; i < StarPts.Length; j = i++)
            {
                var a = StarPts[i];
                var b = StarPts[j];
                if ((a.y > y) != (b.y > y) && x < (b.x - a.x) * (y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            return inside;
        }

        static bool InHeart(float u, float v)
        {
            float x = u * 1.25f, y = v * 1.25f + 0.15f;
            float a = x * x + y * y - 1f;
            return a * a * a - x * x * y * y * y <= 0f;
        }

        // ------------------------------------------------------------ layout helpers

        public static RectTransform Rect(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        public static RectTransform Place(this RectTransform r, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            r.anchorMin = r.anchorMax = anchor;
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = pos;
            r.sizeDelta = size;
            return r;
        }

        public static RectTransform Stretch(this RectTransform r, float inset = 0f)
        {
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = new Vector2(inset, inset);
            r.offsetMax = new Vector2(-inset, -inset);
            return r;
        }

        public static Image Img(Transform parent, string name, Sprite sprite, Color color, float cornerScale = 1f)
        {
            var r = Rect(name, parent);
            var img = r.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            if (sprite != null && sprite.border != Vector4.zero)
            {
                img.type = Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = 1f / cornerScale;
            }
            return img;
        }

        public static Text Label(Transform parent, string text, int size, Color color, Color outline, float outlineSize = 4f)
        {
            var r = Rect("Label", parent);
            var t = r.gameObject.AddComponent<Text>();
            t.font = Font;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = FontStyle.Bold;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            if (outlineSize > 0f)
            {
                var o = r.gameObject.AddComponent<Outline>();
                o.effectColor = outline;
                o.effectDistance = new Vector2(outlineSize, -outlineSize);
                var s = r.gameObject.AddComponent<Shadow>();
                s.effectColor = new Color(outline.r, outline.g, outline.b, outline.a * 0.8f);
                s.effectDistance = new Vector2(0, -outlineSize * 1.6f);
            }
            r.Stretch();
            return t;
        }

        public static Color Darken(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, c.a);

        /// <summary>Alt gölgeli, parlak üst yarılı, basınca ezilen mobil oyun butonu.</summary>
        public static RectTransform Button(Transform parent, string label, Color face, Vector2 size, Action onClick, int fontSize = 76)
        {
            var root = Rect("Button", parent);
            root.sizeDelta = size;
            var hit = root.gameObject.AddComponent<Image>();
            hit.color = new Color(1, 1, 1, 0);

            var shadow = Img(root, "Shadow", Rounded, Darken(face, 0.62f), 0.8f);
            shadow.rectTransform.Stretch();
            shadow.rectTransform.offsetMin += new Vector2(0, -16);
            shadow.rectTransform.offsetMax += new Vector2(0, -16);

            var faceImg = Img(root, "Face", Rounded, face, 0.8f);
            faceImg.rectTransform.Stretch();

            var gloss = Img(root, "Gloss", Rounded, new Color(1, 1, 1, 0.28f), 0.55f);
            gloss.rectTransform.anchorMin = new Vector2(0.05f, 0.52f);
            gloss.rectTransform.anchorMax = new Vector2(0.95f, 0.9f);
            gloss.rectTransform.offsetMin = gloss.rectTransform.offsetMax = Vector2.zero;

            var text = Label(root, label, fontSize, Color.white, Darken(face, 0.5f), 4f);
            text.rectTransform.offsetMin = new Vector2(0, 6);

            var btn = root.gameObject.AddComponent<Button>();
            btn.targetGraphic = hit;
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() =>
            {
                GameRoot.I.Sfx.Play(GameRoot.I.Sfx.Click);
                onClick?.Invoke();
            });
            root.gameObject.AddComponent<Pressable>();
            return root;
        }
    }

    /// <summary>Basılınca ezilip yaylanarak geri dönen buton hissi.</summary>
    public class Pressable : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        public bool Breathe = false;
        float _target = 1f;
        float _scale = 1f;
        float _vel;

        public void OnPointerDown(PointerEventData e) => _target = 0.9f;
        public void OnPointerUp(PointerEventData e) => _target = 1f;

        void Update()
        {
            _scale = Mathf.SmoothDamp(_scale, _target, ref _vel, 0.06f, Mathf.Infinity, Time.unscaledDeltaTime);
            float breathe = Breathe ? 1f + Mathf.Sin(Time.unscaledTime * 3.2f) * 0.035f : 1f;
            transform.localScale = Vector3.one * (_scale * breathe);
        }
    }
}
