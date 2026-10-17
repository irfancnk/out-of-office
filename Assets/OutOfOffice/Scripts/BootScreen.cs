using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OutOfOffice
{
    // The game's own launch screen, in place of Unity's splash: the logo and a loading bar on the brand purple. It is
    // drawn on the very first frame while the rest of the game builds behind it a step at a time, then fades away over
    // the title. It is light on purpose: one gradient, a few glyphs, a pill bar.
    public class BootScreen : MonoBehaviour
    {
        // The window shows ProjectSettings' splash background color before the engine draws anything; it matches Bottom.
        public static readonly Color Top = Themes.Hex(0x4A2190), Bottom = Themes.Hex(0x2B0F55);
        static readonly Color Ink = Themes.Hex(0x1B0E3D), Trim = Themes.Hex(0xFFC94A);

        CanvasGroup group;
        RectTransform fill;
        float fade = -1f;

        public static BootScreen Show()
        {
            var go = new GameObject("Boot Screen", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = (float)Screen.width / Screen.height > 1080f / 1920f ? 1f : 0f;
            var boot = go.AddComponent<BootScreen>();
            boot.Build((RectTransform)go.transform);
            return boot;
        }

        void Build(RectTransform root)
        {
            group = gameObject.AddComponent<CanvasGroup>();
            var bg = Rect("Background", root, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject.AddComponent<Image>();
            bg.sprite = Look.Gradient(Top, Bottom);
            bg.raycastTarget = true;  // nothing underneath takes a tap until the game is ready

            var font = Resources.Load<TMP_FontAsset>("Fonts/TitanOne SDF");
            var preset = Resources.Load<Material>("Fonts/TitanOne Bold");
            var caption = Resources.Load<TMP_FontAsset>("Fonts/NunitoBlack SDF");
            if (font == null || caption == null) return;
            var logo = new Material(preset != null ? preset : font.material);
            logo.SetColor(ShaderUtilities.ID_OutlineColor, Ink);
            logo.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.3f);
            logo.SetColor(ShaderUtilities.ID_UnderlayColor, Ink);
            logo.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.42f);
            logo.SetFloat(ShaderUtilities.ID_UnderlayDilate, 0.3f);
            ShaderUtilities.UpdateShaderRatios(logo);
            var mid = new Vector2(0.5f, 0.5f);
            Text(Rect("Out Of", root, mid, mid, new Vector2(0f, 300f), new Vector2(1000f, 240f)), "OUT OF", 210f, font, logo,
                Color.white, Themes.Hex(0xFFE9C2));
            Text(Rect("Office", root, mid, mid, new Vector2(0f, 110f), new Vector2(1000f, 260f)), "OFFICE", 236f, font, logo,
                Themes.Hex(0xFFD23F), Themes.Hex(0xFF8A1F));

            var track = Rect("Track", root, mid, mid, new Vector2(0f, -200f), new Vector2(560f, 44f));
            Pill(track, new Color(0f, 0f, 0f, 0.35f));
            fill = Rect("Fill", track, Vector2.zero, new Vector2(0.06f, 1f), Vector2.zero, Vector2.zero);
            fill.offsetMin = new Vector2(6f, 6f);
            fill.offsetMax = new Vector2(-6f, -6f);
            Pill(fill, Trim);
            Text(Rect("Caption", root, mid, mid, new Vector2(0f, -280f), new Vector2(800f, 60f)), "CLOCKING IN...", 34f, caption, caption.material,
                new Color(1f, 1f, 1f, 0.75f), new Color(1f, 1f, 1f, 0.75f)).characterSpacing = 4f;
        }

        // How far the game has built itself, 0..1.
        public void Progress(float k)
        {
            if (fill != null) fill.anchorMax = new Vector2(Mathf.Clamp(k, 0.06f, 1f), 1f);
        }

        // Fills the bar and fades out over the title, then goes away.
        public void Finish()
        {
            Progress(1f);
            group.blocksRaycasts = false;
            fade = 0f;
        }

        void Update()
        {
            if (fade < 0f) return;
            fade += Time.unscaledDeltaTime / 0.35f;
            group.alpha = 1f - fade * fade;
            if (fade >= 1f) Destroy(gameObject);
        }

        static void Pill(RectTransform r, Color color)
        {
            var img = r.gameObject.AddComponent<Image>();
            img.sprite = UiArt.Pill;
            img.type = Image.Type.Sliced;
            img.color = color;
            img.raycastTarget = false;
        }

        static TextMeshProUGUI Text(RectTransform r, string text, float size, TMP_FontAsset font, Material material, Color top, Color bottom)
        {
            var t = r.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = font;
            t.fontSharedMaterial = material;
            t.text = text;
            t.fontSize = size;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            t.enableVertexGradient = true;
            t.colorGradient = new VertexGradient(top, top, bottom, bottom);
            return t;
        }

        static RectTransform Rect(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var r = (RectTransform)go.transform;
            r.SetParent(parent, false);
            r.anchorMin = anchorMin;
            r.anchorMax = anchorMax;
            r.anchoredPosition = pos;
            r.sizeDelta = size;
            return r;
        }
    }
}
