using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace OutOfOffice
{
    // All Out of Office UI: title, in-game HUD, pause and settings, level select, skins, and the completion card.
    // Built in code with uGUI and TextMeshPro (SDF text with real outlines and drop shadows).
    // The canvas matches width on tall screens and height on wide ones, so every control stays on screen.
    public class Hud : MonoBehaviour
    {
        // Game actions. OutOfOfficeGame wires these up.
        public System.Action Play, Pause, Resume, Restart, Undo, Next, Replay, Home, OpenLab, ToggleDebug;
        public System.Action<int> SelectLevel;
        public System.Action<bool> SetSfx, SetMusic, SetReducedMotion;
        public System.Func<int> UnlockedCount, CurrentLevel;
        public System.Func<string, int> BestStars;

        public const float TopHeight = 175f;     // reference units the top bar covers (the board may scroll under it)
        public const float BottomHeight = 120f;  // reference units the bottom buttons cover
        const float RefWidth = 1080f, RefHeight = 1920f;

        // Reduced Motion: cards fade instead of sliding and popping; no idle wobble, spins, or sparkle bursts.
        public bool ReducedMotion;
        static bool Calm;  // the same setting, for the small idle animators below

        static readonly Color Ink = Themes.Hex(0x3A2856);       // deep plum for outlines on light UI
        static readonly Color CardTint = Themes.Hex(0xFFF7EC);
        static readonly Color Inset = Themes.Hex(0xF3E6D6);
        static readonly Color Shade = new Color(0.12f, 0.07f, 0.22f, 0.45f);  // translucent HUD capsules

        // One display face for the title logo, a chunky rounded face for labels and buttons, and a semibold face for sentences.
        TMP_FontAsset font, bodyFont, displayFont;
        Material boldPreset, bodyBold, displayBold;

        // The game's working title, as the logo's two lines.
        public static readonly string[] LogoWords = { "OUT OF", "OFFICE" };

        Canvas canvas;
        CanvasScaler scaler;
        RectTransform safe, hudRoot, title, titleArea, dim, banner, levelsContent, devRow, sunburst;
        Card pauseCard, levelsCard, skinsCard, completeCard, settingsCard;
        TextMeshProUGUI chapterText, levelText, shotsText, starText, hintText, bannerText, debugText, totalStars;
        TextMeshProUGUI completeLevel, completeShots, completePar, completeExtra, completeNextLabel;
        Image completeNextIcon;
        RectTransform starPill, hintPill, shotsPill, undoButton, chapterPill;
        Image titleWash;
        readonly List<Image> completeStars = new List<Image>();
        readonly List<RectTransform> hudBlockers = new List<RectTransform>();
        readonly List<RectTransform> logoLetters = new List<RectTransform>();
        readonly List<RectTransform> flying = new List<RectTransform>();
        readonly List<Switch> switches = new List<Switch>();
        Theme theme = Themes.All[0];
        Rect lastSafe;
        Vector2Int lastScreen;
        int logoTaps;
        Card returnTo;
        bool titleReturn;

        class Card
        {
            public RectTransform Root;
            public CanvasGroup Group;
            public Image Ribbon;
            public TextMeshProUGUI Title;
        }

        // A settings row with an on/off switch.
        class Switch
        {
            public string Key;
            public Image Track;
            public RectTransform Knob;
            public bool On;
        }

        public bool PanelOpen => title.gameObject.activeSelf || dim.gameObject.activeSelf;

        public static Hud Create()
        {
            if (EventSystem.current == null)
                new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            var go = new GameObject("HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var hud = go.AddComponent<Hud>();
            hud.Build();
            return hud;
        }

        public float Scale => canvas.scaleFactor;
        public float TopPixels => (Screen.height - Screen.safeArea.yMax) + TopHeight * Scale;
        public float BottomPixels => Screen.safeArea.yMin + BottomHeight * Scale;

        // True when a screen point is over UI, so it must not start an aim.
        public bool Blocks(Vector2 screen)
        {
            if (PanelOpen) return true;
            foreach (var r in hudBlockers)
                if (r.gameObject.activeInHierarchy && RectTransformUtility.RectangleContainsScreenPoint(r, screen, null)) return true;
            return false;
        }

        void Build()
        {
            displayFont = Resources.Load<TMP_FontAsset>("Fonts/LuckiestGuy SDF");
            displayBold = Resources.Load<Material>("Fonts/LuckiestGuy Bold");
            font = Resources.Load<TMP_FontAsset>("Fonts/LilitaOne SDF");
            boldPreset = Resources.Load<Material>("Fonts/LilitaOne Bold");
            bodyFont = Resources.Load<TMP_FontAsset>("Fonts/PoppinsSemi SDF");
            bodyBold = Resources.Load<Material>("Fonts/PoppinsSemi Bold");
            if (displayFont == null) { displayFont = font; displayBold = boldPreset; }
            if (font == null) { font = Resources.Load<TMP_FontAsset>("Fonts/PoppinsBold SDF"); boldPreset = Resources.Load<Material>("Fonts/PoppinsBold Bold"); }
            if (bodyFont == null) { bodyFont = font; bodyBold = boldPreset; }
            // Fill the atlases up front, so no glyph is rasterized during play.
            const string glyphs = " ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!?.,:;'-+/·&()%’";
            font.TryAddCharacters(glyphs, out _);
            bodyFont.TryAddCharacters(glyphs, out _);
            displayFont.TryAddCharacters(glyphs, out _);

            canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefWidth, RefHeight);
            FitScaler();

            safe = Rect("Safe Area", transform as RectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            ApplySafeArea();
            BuildHud();
            dim = Rect("Dim", transform as RectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Img(dim, null, new Color(0.1f, 0.06f, 0.18f, 0.5f)).raycastTarget = true;
            dim.gameObject.SetActive(false);
            BuildTitle();
            BuildPause();
            BuildSettings();
            BuildLevels();
            BuildSkins();
            BuildQuests();
            BuildSpeech();
            BuildComplete();
        }

        // Editor review shots: draw through a camera, laid out for a portrait phone with the given safe insets
        // (fractions of the screen height), with every animation at rest.
        public void PreviewLayout(Camera cam, float topInset, float bottomInset)
        {
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 1f;
            scaler.matchWidthOrHeight = 0f;
            foreach (var r in new[] { safe, titleArea })
            {
                r.anchorMin = new Vector2(0f, bottomInset);
                r.anchorMax = new Vector2(1f, 1f - topInset);
            }
            ReducedMotion = true;
            Calm = true;
        }

        // Tall screens match width; wide screens (tablets, desktop windows) match height.
        void FitScaler()
        {
            lastScreen = new Vector2Int(Screen.width, Screen.height);
            float aspect = Screen.width / (float)Mathf.Max(1, Screen.height);
            scaler.matchWidthOrHeight = aspect > RefWidth / RefHeight ? 1f : 0f;
        }

        public void SetTheme(Theme t)
        {
            theme = t;
            chapterText.fontSharedMaterial = TextMat(theme.AccentDark, 0.35f, false);
            chapterPill.GetComponent<Image>().color = theme.Accent;
            levelText.fontSharedMaterial = TextMat(Ink, 0.45f, true);
            bannerText.fontSharedMaterial = TextMat(theme.AccentDark, 0.45f, true);
            titleWash.sprite = Backdrop(theme);
        }

        // ---------- In-game HUD ----------

        void BuildHud()
        {
            hudRoot = Rect("HUD", safe, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);

            // One slim top bar: pause, the level and its shot count in a single capsule, stars. The board gets the rest.
            var pause = IconButton(hudRoot, "Pause", new Vector2(0f, 1f), new Vector2(86f, -80f), 112f, Color.white, "pause", Ink);
            pause.Clicked = () => Pause?.Invoke();
            hudBlockers.Add(pause.transform as RectTransform);

            var titleBlock = Capsule(hudRoot, "Level", new Vector2(0.5f, 1f), new Vector2(0f, -82f), new Vector2(340f, 116f), Shade);
            chapterPill = Capsule(titleBlock, "Chapter", new Vector2(0.5f, 1f), new Vector2(0f, 14f), new Vector2(200f, 40f), theme.Accent);
            chapterText = Txt(chapterPill, "TOY ROOM", 24f, Color.white, Ink, 0.3f, false);
            chapterText.characterSpacing = 4f;
            chapterPill.gameObject.SetActive(false);  // the chapter shows in the level banner; the bar stays slim
            levelText = Txt(Rect("Number", titleBlock, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(340f, 64f)),
                "LEVEL 1", 58f, Color.white, Ink, 0.4f, true);
            hudBlockers.Add(titleBlock);

            shotsPill = Rect("Shots", titleBlock, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(320f, 40f));
            shotsText = Txt(shotsPill, "SHOTS 0 · PAR 1", 28f, Color.white, Ink, 0.2f, false);

            starPill = Capsule(hudRoot, "Stars", new Vector2(1f, 1f), new Vector2(-118f, -80f), new Vector2(190f, 88f), Shade);
            Img(Rect("Icon", starPill, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(46f, 3f), new Vector2(92f, 92f)), UiArt.Cup, Color.white);
            starText = Txt(Rect("Count", starPill, Vector2.zero, Vector2.one, new Vector2(42f, 0f), new Vector2(-84f, 0f)), "0/1", 50f, Color.white, Ink, 0.35f, true);
            hudBlockers.Add(starPill);

            var undo = IconButton(hudRoot, "Undo", new Vector2(0f, 0f), new Vector2(90f, 90f), 124f, Palette.UiBlue, "undo", Color.white);
            undo.Clicked = () => Undo?.Invoke();
            undoButton = undo.transform as RectTransform;
            hudBlockers.Add(undoButton);
            var restart = IconButton(hudRoot, "Restart", new Vector2(1f, 0f), new Vector2(-90f, 90f), 124f, Palette.UiRed, "restart", Color.white);
            restart.Clicked = () => Restart?.Invoke();
            hudBlockers.Add(restart.transform as RectTransform);

            hintPill = Capsule(hudRoot, "Hint", new Vector2(0.5f, 0f), new Vector2(0f, 92f), new Vector2(560f, 80f), Shade);
            hintText = Txt(hintPill, "DRAG BACK, AIM, RELEASE", 32f, Color.white, Ink, 0.25f, false);
            hintPill.gameObject.AddComponent<CanvasGroup>();
            hintPill.gameObject.SetActive(false);

            debugText = Txt(Rect("Debug", hudRoot, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -290f), new Vector2(1000f, 50f)),
                "", 28f, Color.white, Ink, 0.3f, false);
            debugText.gameObject.SetActive(false);

            banner = Rect("Banner", hudRoot, new Vector2(0.5f, 0.6f), new Vector2(0.5f, 0.6f), Vector2.zero, new Vector2(1000f, 200f));
            bannerText = Txt(banner, "", 104f, Color.white, Ink, 0.45f, true);
            banner.gameObject.SetActive(false);
        }

        public void SetLevel(int number, string name, string hint, Theme levelTheme)
        {
            SetTheme(levelTheme);
            chapterText.text = levelTheme.Name;
            levelText.text = number > 0 ? "LEVEL " + number : "LAB";
            chapterPill.sizeDelta = new Vector2(chapterText.GetPreferredValues(levelTheme.Name).x + 44f, 40f);
            Pop(levelText.rectTransform, 0f);
            Banner(name.ToUpperInvariant(), Color.white);
            hintText.text = hint.Length > 0 ? hint : "DRAG BACK, AIM, RELEASE";
        }

        public void SetShots(int shots, int par)
        {
            string next = $"SHOTS {shots}  ·  PAR {par}";
            if (shotsText.text != next)
            {
                shotsText.text = next;
                Pop(shotsPill, 0f, 1.1f);
            }
            shotsText.color = shots > par ? Themes.Hex(0xFFD2C2) : Color.white;
        }

        public void SetStars(int collected, int total, bool animate)
        {
            starPill.gameObject.SetActive(total > 0);
            starText.text = $"{collected}/{total}";
            if (animate) Pop(starPill, 0f, 1.25f);
        }

        public void SetUndoAvailable(bool available)
        {
            undoButton.GetComponent<CanvasGroup>().alpha = available ? 1f : 0.5f;
        }

        // The level hint is a toast: it pops in, stays a few seconds, and fades, so it never covers the board for long.
        public void SetHint(bool show)
        {
            var group = hintPill.GetComponent<CanvasGroup>();
            Tweens.Kill(group);
            if (!show)
            {
                hintPill.gameObject.SetActive(false);
                return;
            }
            hintPill.gameObject.SetActive(true);
            group.alpha = 1f;
            Pop(hintPill, 0.3f);
            Tweens.Run(0.6f, k => group.alpha = 1f - k, Tweens.InOutSine, 4.5f, () => hintPill.gameObject.SetActive(false), group);
        }

        RectTransform holeMarker, holeArrow;

        // A gold marker at the top or bottom edge that points at the hole while the camera has scrolled away from it.
        // The bed marker at the screen edge nearest the off-screen bed, its chevron pointing toward it.
        public void SetHoleMarker(bool show, Vector2 screenPos, Vector2 toward)
        {
            if (holeMarker == null)
            {
                holeMarker = Rect("Hole Marker", hudRoot, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(96f, 96f));
                Img(holeMarker, UiArt.RoundButton, Palette.Blanket);
                Img(Rect("Bed", holeMarker, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(58f, 58f)), UiArt.Icon("bed"), Color.white);
                holeArrow = Rect("Arrow", holeMarker, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(54f, 54f));
                Img(holeArrow, UiArt.Icon("chevron"), Palette.Blanket);
                holeMarker.gameObject.SetActive(false);
            }
            if (holeMarker.gameObject.activeSelf != show)
            {
                holeMarker.gameObject.SetActive(show);
                if (show) Pop(holeMarker, 0f, 1.2f);
            }
            if (!show) return;
            float edge = 70f * Scale;
            var p = new Vector2(Mathf.Clamp(screenPos.x, edge, Screen.width - edge),
                Mathf.Clamp(screenPos.y, BottomPixels + edge, Screen.height - TopPixels - edge));
            RectTransformUtility.ScreenPointToLocalPointInRectangle(hudRoot, p, null, out var local);
            Vector2 d = toward.sqrMagnitude > 1e-6f ? toward.normalized : Vector2.up;
            float bob = Mathf.Sin(Time.unscaledTime * 6f) * 8f;
            holeMarker.anchorMin = holeMarker.anchorMax = new Vector2(0.5f, 0.5f);
            holeMarker.anchoredPosition = local + d * bob;
            holeArrow.anchoredPosition = d * 62f;
            holeArrow.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg - 90f);
        }

        public void SetDebug(string text)
        {
            debugText.gameObject.SetActive(text != null);
            if (text != null) debugText.text = text;
        }

        // Big centered text that pops in and fades out.
        public void Banner(string text, Color color, float hold = 0.7f)
        {
            Tweens.Kill(banner);
            banner.gameObject.SetActive(true);
            bannerText.text = text;
            bannerText.color = color;
            if (ReducedMotion)
            {
                banner.localScale = Vector3.one;
                Tweens.Run(0.2f, k => bannerText.alpha = k, Tweens.Linear, owner: banner);
            }
            else
            {
                banner.localScale = Vector3.zero;
                bannerText.alpha = 1f;
                Tweens.Run(0.45f, k => banner.localScale = Vector3.one * k, Tweens.OutBack, owner: banner);
            }
            Tweens.Run(0.3f, k => bannerText.alpha = 1f - k, Tweens.Linear, 0.45f + hold, () => banner.gameObject.SetActive(false), banner);
        }

        // A coffee flies from the board to the coffee counter, then the counter pops.
        public void FlyStar(Vector2 screen, System.Action arrived)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(hudRoot, screen, null, out var from);
            var r = Rect("Flying Coffee", hudRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), from, new Vector2(96f, 96f));
            Img(r, UiArt.Cup, Color.white);
            flying.Add(r);
            Vector2 to = (Vector2)hudRoot.InverseTransformPoint(starPill.TransformPoint(new Vector3(-58f, 3f, 0f)));
            Vector2 ctrl = Vector2.Lerp(from, to, 0.5f) + new Vector2(-180f, 120f);
            bool spin = !ReducedMotion;
            Tweens.Run(0.55f, k =>
            {
                Vector2 a = Vector2.Lerp(from, ctrl, k), b = Vector2.Lerp(ctrl, to, k);
                r.anchoredPosition = Vector2.Lerp(a, b, k);
                r.localScale = Vector3.one * Mathf.Lerp(1.3f, 0.8f, k);
                if (spin) r.localRotation = Quaternion.Euler(0f, 0f, 14f * Mathf.Sin(k * Mathf.PI * 2f));
            }, Tweens.InOutSine, 0f, () =>
            {
                flying.Remove(r);
                Destroy(r.gameObject);
                arrived?.Invoke();
            }, r);
        }

        // Stops every star still flying (undo, restart, home), without running their arrival callbacks.
        public void CancelFlyingStars()
        {
            foreach (var r in flying)
            {
                if (r == null) continue;
                Tweens.Kill(r);
                Destroy(r.gameObject);
            }
            flying.Clear();
        }

        // ---------- Title ----------

        // The title screen, laid out like a big casual mobile game: a framed header (stars and chapter progress), the
        // logo, the employee on a round stage in front of a sunny office skyline, side event buttons, the big level
        // button, and a tab bar with HOME raised in the middle.
        static readonly Color Frame = Themes.Hex(0x5B2A9E), FrameDark = Themes.Hex(0x2B0F55), FrameLight = Themes.Hex(0x9A62F0);
        static readonly Color Trim = Themes.Hex(0xFFC94A);

        RectTransform welcomePortrait, chapterFill, giftBadge;
        RawImage welcomeIcon;
        TextMeshProUGUI chapterCount, chapterName, playLabel, giftPlate, nextOutfitText;
        Image giftPlateImage;
        readonly Dictionary<Theme, Sprite> backdrops = new Dictionary<Theme, Sprite>();

        void BuildTitle()
        {
            title = Rect("Title", transform as RectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            titleWash = Img(title, null, Color.white);
            titleWash.raycastTarget = true;
            var area = titleArea = Rect("Area", title, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            ApplySafeArea();
            BuildBackdrop(area);

            // The employee, big, on a round stage.
            var stage = Rect("Stage", area, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -330f), new Vector2(720f, 210f));
            Img(Rect("Shadow", stage, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(820f, 250f)), Shapes.Circle, new Color(0.15f, 0.05f, 0.3f, 0.25f));
            Img(Rect("Side", stage, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -30f), new Vector2(720f, 210f)), Shapes.Circle, Themes.Hex(0xC98B4E));
            Img(Rect("Trim", stage, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -2f), new Vector2(720f, 210f)), Shapes.Circle, Trim);
            Img(Rect("Top", stage, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(680f, 186f)), Shapes.Circle, Themes.Hex(0xFFF3DE));
            Img(Rect("Rug", stage, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(520f, 136f)), Shapes.Ring, new Color(1f, 0.79f, 0.29f, 0.55f));
            welcomePortrait = Rect("Employee", area, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(700f, 700f));
            welcomeIcon = welcomePortrait.gameObject.AddComponent<RawImage>();
            welcomeIcon.raycastTarget = false;
            welcomePortrait.gameObject.AddComponent<Bobber>().Hud = this;
            IconStudio.Rendered += RefreshWelcomeIcon;

            // The logo, above the stage.
            var logo = Rect("Logo", area, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -400f), new Vector2(900f, 400f));
            logo.localScale = Vector3.one * 0.6f;
            logo.localRotation = Quaternion.Euler(0f, 0f, 3f);
            AddWord(logo, LogoWords[0], new Vector2(-14f, 92f), Color.white, Themes.Hex(0xFFE9C9));
            AddWord(logo, LogoWords[1], new Vector2(22f, -96f), Themes.Hex(0xFFE45C), Themes.Hex(0xFF8A2E));

            BuildHeader(area);

            // Side events: the free daily gift (left) and the next outfit unlock (right).
            var gift = SideButton(area, "Gift", new Vector2(0f, 1f), new Vector2(125f, -720f), Themes.Hex(0xFFB22E), "chest", out giftPlate, out giftPlateImage);
            gift.Clicked = () => ShowQuests();
            giftBadge = Badge(gift.transform as RectTransform, "!");
            var outfit = SideButton(area, "Next Outfit", new Vector2(1f, 1f), new Vector2(-125f, -720f), Themes.Hex(0xFF5FA2), "shirt", out nextOutfitText, out _);
            outfit.Clicked = () => ShowSkins();
            var plateStar = Rect("Star", nextOutfitText.transform.parent as RectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(26f, 2f), new Vector2(54f, 54f));
            Img(plateStar, UiArt.Star, Color.white);
            nextOutfitText.rectTransform.offsetMin = new Vector2(30f, 0f);

            // The big level button.
            var play = TextButton(area, "LEVEL 1", Palette.UiGreen, Vector2.zero, new Vector2(660f, 196f), 104f, null);
            var pr = play.transform as RectTransform;
            pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0f);
            pr.anchoredPosition = new Vector2(0f, 455f);
            play.Clicked = () => Play?.Invoke();
            playLabel = pr.Find("Label").GetComponentInChildren<TextMeshProUGUI>();
            Breathe(pr);

            BuildTabs(area);
        }

        // A sky in the chapter color, slow sun rays, drifting clouds, a pale office skyline, and a warm floor.
        void BuildBackdrop(RectTransform area)
        {
            var back = Rect("Backdrop", area, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, 1200f));
            var rays = Rect("Rays", back, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(2000f, 2000f));
            Img(rays, UiArt.Sunburst, new Color(1f, 1f, 1f, 0.22f));
            rays.gameObject.AddComponent<Spin>().Speed = 4f;
            var glow = Rect("Glow", back, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 60f), new Vector2(1100f, 1100f));
            Img(glow, Shapes.Circle, new Color(1f, 1f, 1f, 0.16f));

            var rng = new System.Random(7);
            for (int i = 0; i < 4; i++)
            {
                var cloud = Rect("Cloud", back, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(-560f + i * 380f, 420f + (i % 2) * 170f), new Vector2(260f, 110f));
                float s = 0.75f + (float)rng.NextDouble() * 0.5f;
                cloud.localScale = Vector3.one * s;
                foreach (var (x, y, d) in new[] { (-70f, -10f, 110f), (0f, 15f, 150f), (75f, -10f, 105f) })
                    Img(Rect("Puff", cloud, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(d, d)), Shapes.Circle, new Color(1f, 1f, 1f, 0.92f));
                Sliced(Img(Rect("Base", cloud, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f), new Vector2(250f, 64f)), UiArt.Pill, new Color(1f, 1f, 1f, 0.92f)), 64f, 0.5f);
                var drift = cloud.gameObject.AddComponent<Drift>();
                drift.Speed = 10f + i * 4f;
            }

            // Office towers along the horizon, with window grids.
            var skyline = Rect("Skyline", back, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -180f), new Vector2(0f, 10f));
            float bx = -40f;
            while (bx < 1120f)
            {
                float w = 130f + (float)rng.NextDouble() * 110f, h = 260f + (float)rng.NextDouble() * 420f;
                var tower = Rect("Tower", skyline, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(bx + w / 2f, h / 2f), new Vector2(w, h));
                tower.pivot = new Vector2(0.5f, 0.5f);
                Sliced(Img(tower, UiArt.Panel, new Color(1f, 1f, 1f, 0.28f)), 80f, 0.25f);
                for (float wy = h - 50f; wy > 40f; wy -= 52f)
                    for (float wx = 26f; wx < w - 30f; wx += 40f)
                        Img(Rect("Window", tower, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(wx + 8f, wy), new Vector2(22f, 28f)), Shapes.Square, new Color(1f, 1f, 1f, 0.3f));
                bx += w + 18f;
            }

            // The office floor: everything below the horizon.
            var floor = Rect("Floor", back, new Vector2(0f, 0f), new Vector2(1f, 0.5f), new Vector2(0f, -180f), Vector2.zero);
            Img(floor, Look.Gradient(Themes.Hex(0xFFE7C2), Themes.Hex(0xF7C98F)), Color.white);
            Img(Rect("Horizon", floor, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, 14f)), Shapes.Square, Themes.Hex(0xE8A96A));
        }

        // The framed bar along the top: total stars, and this chapter's star progress with a chest at the end.
        void BuildHeader(RectTransform area)
        {
            var header = Rect("Header", area, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, 130f), new Vector2(0f, 540f));
            Img(header, Shapes.Square, FrameDark);
            Sliced(Img(Rect("Face", header, Vector2.zero, Vector2.one, new Vector2(0f, 6f), new Vector2(0f, -12f)), UiArt.Button, Frame), 160f, 0.1f);
            Img(Rect("Trim", header, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 4f), new Vector2(0f, 8f)), Shapes.Square, Trim);
            const float row = 75f;  // content centre, up from the header's bottom edge

            var stars = Rect("Stars", header, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(215f, row), new Vector2(300f, 92f));
            Sliced(Img(stars, UiArt.Pill, FrameDark), 92f, 0.5f);
            Img(Rect("Icon", stars, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 4f), new Vector2(124f, 124f)), UiArt.Star, Color.white);
            totalStars = Txt(Rect("Count", stars, Vector2.zero, Vector2.one, new Vector2(10f, 2f), new Vector2(-80f, 0f)), "0", 58f, Color.white, FrameDark, 0.3f, true);
            var plus = Rect("Plus", stars, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-46f, 0f), new Vector2(78f, 78f));
            var plusImage = Img(plus, UiArt.Button, Palette.UiGreen);
            Sliced(plusImage, 78f, 0.3f);
            plusImage.raycastTarget = true;
            Txt(Rect("Label", plus, Vector2.zero, Vector2.one, new Vector2(0f, 6f), Vector2.zero), "+", 64f, Color.white, Themes.Hex(0x1F7A33), 0.3f, false);
            plus.gameObject.AddComponent<UiPress>().Clicked = () => ShowLevels();

            var bar = Rect("Chapter", header, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-330f, row), new Vector2(520f, 92f));
            bar.gameObject.AddComponent<UiPress>().Clicked = () => ShowLevels();
            var barImage = Img(bar, UiArt.Pill, FrameDark);
            Sliced(barImage, 92f, 0.5f);
            barImage.raycastTarget = true;
            var track = Rect("Track", bar, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-16f, -16f));
            Sliced(Img(track, UiArt.Pill, Themes.Hex(0x45207E)), 76f, 0.5f);
            chapterFill = Rect("Fill", track, Vector2.zero, new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero);
            Sliced(Img(chapterFill, UiArt.Button, Palette.UiGreen), 76f, 0.5f);
            chapterCount = Txt(Rect("Count", bar, Vector2.zero, Vector2.one, new Vector2(-30f, 2f), Vector2.zero), "0/0", 46f, Color.white, FrameDark, 0.35f, true);
            var chest = Rect("Chest", bar, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-10f, 8f), new Vector2(124f, 124f));
            var chestIcon = Img(chest, UiArt.Icon("chest"), Themes.Hex(0xFFC23A));
            chestIcon.gameObject.AddComponent<Outline>().effectColor = FrameDark;
            chestIcon.GetComponent<Outline>().effectDistance = new Vector2(4f, -4f);
            var plate = Rect("Name", header, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-340f, -34f), new Vector2(400f, 64f));
            Sliced(Img(plate, UiArt.Pill, FrameDark), 64f, 0.5f);
            chapterName = Txt(Rect("Text", plate, Vector2.zero, Vector2.one, new Vector2(0f, 2f), new Vector2(-30f, 0f)), "", 32f, Trim, FrameDark, 0.2f, false);
            chapterName.enableAutoSizing = true;
            chapterName.fontSizeMin = 20f;
            chapterName.fontSizeMax = 32f;
        }

        // The tab bar: rooms, wardrobe, home (raised, selected), tasks, settings.
        void BuildTabs(RectTransform area)
        {
            var nav = Rect("Tabs", area, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, -110f), new Vector2(0f, 640f));
            Img(nav, Shapes.Square, Frame).raycastTarget = true;
            Img(Rect("Shade", nav, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -40f), new Vector2(0f, 60f)), Look.Gradient(new Color(1f, 1f, 1f, 0.12f), new Color(1f, 1f, 1f, 0f)), Color.white);
            Img(Rect("Trim", nav, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -5f), new Vector2(0f, 10f)), Shapes.Square, Trim);
            Img(Rect("Edge", nav, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -14f), new Vector2(0f, 8f)), Shapes.Square, FrameDark);
            const float y = 112f;
            for (int i = 0; i < 4; i++)
                Img(Rect("Divider", area, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-324f + i * 216f, y), new Vector2(5f, 150f)), Shapes.Square, new Color(FrameDark.r, FrameDark.g, FrameDark.b, 0.6f));

            Tab(area, "ROOMS", "grid", Themes.Hex(0x34C3B5), -432f, y).Clicked = () => ShowLevels();
            Tab(area, "WARDROBE", "shirt", Themes.Hex(0xFF5FA2), -216f, y).Clicked = () => ShowSkins();
            var tasks = Tab(area, "TASKS", "gift", Themes.Hex(0xFF9F2E), 216f, y);
            tasks.Clicked = () => ShowQuests();
            questBadge = Badge(tasks.transform as RectTransform, "1");
            questBadgeText = questBadge.GetComponentInChildren<TextMeshProUGUI>();
            Tab(area, "SETTINGS", "gear", Palette.UiBlue, 432f, y).Clicked = () => ShowSettings();

            // HOME: a lit tile rising out of the bar, with a bigger button.
            var tile = Rect("Home Tile", area, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, y + 20f), new Vector2(226f, 280f));
            Sliced(Img(tile, UiArt.Button, FrameLight), 280f, 0.18f);
            var home = FramedIcon(tile, "Home", new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), 170f, Themes.Hex(0xFFC23A), "home");
            Txt(Rect("Caption", tile, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 48f), new Vector2(220f, 50f)), "HOME", 40f, Color.white, FrameDark, 0.35f, true);
            home.Clicked = () => { };
        }

        UiPress Tab(RectTransform parent, string label, string icon, Color color, float x, float y)
        {
            var press = FramedIcon(parent, label, new Vector2(0.5f, 0f), new Vector2(x, y + 24f), 118f, color, icon);
            Txt(Rect("Caption", press.transform as RectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -34f), new Vector2(210f, 44f)),
                label, 30f, Color.white, FrameDark, 0.35f, true);
            return press;
        }

        // A round button in a dark frame ring, with a white icon.
        UiPress FramedIcon(RectTransform parent, string name, Vector2 anchor, Vector2 pos, float size, Color color, string icon)
        {
            var r = Rect(name, parent, anchor, anchor, pos, new Vector2(size, size));
            var shadow = Img(Rect("Shadow", r, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -12f), Vector2.one * size * 1.3f), UiArt.SoftShadow, new Color(0.1f, 0.03f, 0.2f, 0.35f));
            Sliced(shadow, size * 1.3f, 0.5f);
            Img(r, UiArt.RoundButton, FrameDark).raycastTarget = true;
            var face = Rect("Face", r, Vector2.zero, Vector2.one, new Vector2(0f, 4f), Vector2.one * -size * 0.13f);
            Img(face, UiArt.RoundButton, color);
            var ic = Img(Rect("Icon", r, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, size * 0.06f), Vector2.one * size * 0.5f), UiArt.Icon(icon), Color.white);
            var sh = ic.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0.1f, 0.03f, 0.2f, 0.45f);
            sh.effectDistance = new Vector2(0f, -4f);
            return r.gameObject.AddComponent<UiPress>();
        }

        // A big framed side button with a dark label plate under it.
        UiPress SideButton(RectTransform parent, string name, Vector2 anchor, Vector2 pos, Color color, string icon, out TextMeshProUGUI plateText, out Image plateImage)
        {
            var press = FramedIcon(parent, name, anchor, pos, 176f, color, icon);
            var plate = Rect("Plate", press.transform as RectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -12f), new Vector2(210f, 62f));
            plateImage = Img(plate, UiArt.Pill, FrameDark);
            Sliced(plateImage, 62f, 0.5f);
            plateText = Txt(Rect("Text", plate, Vector2.zero, Vector2.one, new Vector2(0f, 2f), Vector2.zero), "", 36f, Color.white, FrameDark, 0.3f, false);
            return press;
        }

        RectTransform Badge(RectTransform on, string text)
        {
            var badge = Rect("Badge", on, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-10f, -10f), new Vector2(62f, 62f));
            Img(badge, UiArt.RoundButton, Palette.UiRed);
            Txt(Rect("Count", badge, Vector2.zero, Vector2.one, new Vector2(0f, 4f), Vector2.zero), text, 40f, Color.white, Themes.Hex(0x8A1020), 0.3f, false);
            badge.gameObject.SetActive(false);
            return badge;
        }

        Sprite Backdrop(Theme t)
        {
            if (backdrops.TryGetValue(t, out var s)) return s;
            return backdrops[t] = Look.Gradient(Color.Lerp(t.Sky, t.AccentDark, 0.3f), Color.Lerp(t.Sky, Color.white, 0.45f));
        }

        class Spin : MonoBehaviour
        {
            public float Speed;
            void Update() { if (!Calm) transform.Rotate(0f, 0f, -Speed * Time.unscaledDeltaTime); }
        }

        // Clouds sail slowly across and wrap around.
        class Drift : MonoBehaviour
        {
            public float Speed;
            void Update()
            {
                if (Calm) return;
                var r = (RectTransform)transform;
                var p = r.anchoredPosition;
                p.x += Speed * Time.unscaledDeltaTime;
                if (p.x > 760f) p.x = -760f;
                r.anchoredPosition = p;
            }
        }

        void OnDestroy()
        {
            IconStudio.Rendered -= RefreshWelcomeIcon;
            IconStudio.Rendered -= RefreshWardrobeIcons;
        }

        void RefreshWelcomeIcon()
        {
            if (welcomeIcon == null) return;
            var icon = IconStudio.HeroIcon;
            if (welcomeIcon.texture == icon) return;
            welcomeIcon.texture = icon;
            welcomeIcon.color = icon != null ? Color.white : new Color(1f, 1f, 1f, 0f);
        }

        void RefreshHome(int stars, int levelNumber, Theme levelTheme)
        {
            totalStars.text = stars.ToString();
            playLabel.text = $"LEVEL {levelNumber}";
            chapterName.text = levelTheme.Name;

            int c = Themes.ChapterOf(levelNumber - 1);
            int first = Themes.ChapterStart[c], end = Mathf.Min(Themes.ChapterStart[c + 1], Levels.All.Length), got = 0;
            for (int i = first; i < end; i++) got += BestStars?.Invoke(Levels.All[i].Id) ?? 0;
            int max = Mathf.Max(1, (end - first) * 3);
            chapterCount.text = $"{got}/{max}";
            chapterFill.anchorMax = new Vector2(Mathf.Max(0.12f, got / (float)max), 1f);
            chapterFill.gameObject.SetActive(got > 0);

            bool ready = DailyChestReady?.Invoke() ?? false;
            var left = Quests.UntilReset();
            giftPlate.text = ready ? "FREE!" : $"{left.Hours}h {left.Minutes}m";
            giftPlateImage.color = ready ? Palette.UiGreen : FrameDark;
            giftBadge.gameObject.SetActive(ready);

            int next = int.MaxValue;
            foreach (var item in Cosmetics.All)
                if (item.StarsNeeded > stars) next = Mathf.Min(next, item.StarsNeeded);
            nextOutfitText.text = next == int.MaxValue ? "ALL" : next.ToString();

            RefreshWelcomeIcon();
            Tweens.Kill(welcomePortrait);
            if (ReducedMotion) { welcomePortrait.localScale = Vector3.one; return; }
            welcomePortrait.localScale = Vector3.zero;
            Tweens.Run(0.5f, k => welcomePortrait.localScale = Vector3.one * k, Tweens.OutBack, 0.3f, owner: welcomePortrait);
        }

        // A slow bob and tilt, so the portrait feels alive.
        class Bobber : MonoBehaviour
        {
            public Hud Hud;
            Vector2 rest;
            bool started;

            void Update()
            {
                var r = (RectTransform)transform;
                if (!started) { rest = r.anchoredPosition; started = true; }
                Hud.RefreshWelcomeIcon();  // follows skin changes made from the title
                if (Hud.ReducedMotion) { r.anchoredPosition = rest; r.localRotation = Quaternion.identity; return; }
                float t = Time.unscaledTime;
                r.anchoredPosition = rest + new Vector2(0f, 14f * Mathf.Sin(t * 2.4f));
                r.localRotation = Quaternion.Euler(0f, 0f, 4f * Mathf.Sin(t * 1.7f));
                float squash = 0.03f * Mathf.Sin(t * 4.8f);
                r.localScale = new Vector3(1f + squash, 1f - squash, 1f);
            }
        }

        // Logo letters bob in a slow wave.
        class Wave : MonoBehaviour
        {
            public float Phase;
            Vector2 rest;
            bool started;
            void Update()
            {
                var r = (RectTransform)transform;
                if (!started) { rest = r.anchoredPosition; started = true; }
                r.anchoredPosition = Calm ? rest : rest + new Vector2(0f, 7f * Mathf.Sin(Time.unscaledTime * 2.2f + Phase));
            }
        }

        void AddWord(RectTransform parent, string word, Vector2 center, Color top, Color bottom)
        {
            // Lay letters out by their real widths, so narrow letters (L, I) do not leave gaps.
            const float size = 250f, gap = -4f;
            var widths = new float[word.Length];
            float total = 0f;
            var probe = Txt(Rect("Probe", parent, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero), "", size, Color.white, Ink, 0.45f, true);
            probe.font = displayFont;
            for (int i = 0; i < word.Length; i++)
            {
                widths[i] = word[i] == ' ' ? size * 0.22f : probe.GetPreferredValues(word[i].ToString()).x;
                total += widths[i] + (i > 0 ? gap : 0f);
            }
            Destroy(probe.transform.parent.gameObject);
            float cursor = center.x - total / 2f;
            for (int i = 0; i < word.Length; i++)
            {
                float x = cursor + widths[i] / 2f;
                cursor += widths[i] + gap;
                var r = Rect("Letter", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, center.y), new Vector2(widths[i] + 20f, 240f));
                // Extrusion: stacked dark copies below the face give the letter a chunky 3D side.
                for (int k = 4; k >= 1; k--)
                {
                    var depth = Rect("Depth", r, Vector2.zero, Vector2.one, new Vector2(0f, -6f * k), Vector2.zero);
                    var dc = Color.Lerp(Themes.Hex(0x2A1660), Themes.Hex(0x4A2A9A), (4 - k) / 4f);
                    var dt = Txt(depth, word[i].ToString(), size, dc, Themes.Hex(0x1B0E3D), 0.45f, false);
                    dt.font = displayFont;
                    dt.fontSharedMaterial = TextMat(Themes.Hex(0x1B0E3D), 0.45f, false, displayFont);
                }
                var t = Txt(r, word[i].ToString(), size, Color.white, Themes.Hex(0x1B0E3D), 0.45f, false);
                t.font = displayFont;
                t.fontSharedMaterial = TextMat(Themes.Hex(0x1B0E3D), 0.45f, false, displayFont);
                r.gameObject.AddComponent<Wave>().Phase = (logoLetters.Count) * 0.45f;
                t.enableVertexGradient = true;
                t.colorGradient = new VertexGradient(top, top, bottom, bottom);
                r.localRotation = Quaternion.Euler(0f, 0f, (i % 2 == 0 ? 3f : -3f));
                logoLetters.Add(r);
                var press = r.gameObject.AddComponent<UiPress>();
                t.raycastTarget = true;
                press.Clicked = () =>
                {
                    // Five taps on the logo reveal the developer tools in the pause menu.
                    if (++logoTaps >= 5) SetDevVisible(true);
                };
            }
        }

        public void ShowTitle(int stars, int levelNumber, Theme levelTheme)
        {
            HideAll();
            SetTheme(levelTheme);
            title.gameObject.SetActive(true);
            hudRoot.gameObject.SetActive(false);
            RefreshHome(stars, levelNumber, levelTheme);
            for (int i = 0; i < logoLetters.Count; i++)
            {
                var r = logoLetters[i];
                Tweens.Kill(r);
                if (ReducedMotion)
                {
                    r.localScale = Vector3.one;
                    continue;
                }
                r.localScale = Vector3.zero;
                Tweens.Run(0.55f, k => r.localScale = Vector3.one * k, Tweens.OutBack, 0.1f + i * 0.06f, owner: r);
            }
        }

        public void HideTitle()
        {
            title.gameObject.SetActive(false);
            hudRoot.gameObject.SetActive(true);
        }

        // ---------- Pause and settings ----------

        void BuildPause()
        {
            pauseCard = MakeCard("Pause", "PAUSED", new Vector2(860f, 1260f), true);
            var c = pauseCard.Root;
            TextButton(c, "RESUME", Palette.UiGreen, new Vector2(0f, 400f), new Vector2(620f, 170f), 70f, "play").Clicked = () => Resume?.Invoke();
            RoundAction(c, "RESTART", "restart", Palette.UiRed, new Vector2(-230f, 170f), Ink).Clicked = () => Restart?.Invoke();
            RoundAction(c, "HOME", "home", Palette.UiBlue, new Vector2(0f, 170f), Ink).Clicked = () => Home?.Invoke();
            RoundAction(c, "ROOMS", "grid", Palette.UiWood, new Vector2(230f, 170f), Ink).Clicked = () => ShowLevels();
            SettingRows(c, -60f);
            devRow = Rect("Dev", c, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -520f), new Vector2(700f, 110f));
            TextButton(devRow, "LAB", Palette.UiPurple, new Vector2(-160f, 0f), new Vector2(280f, 100f), 44f, null).Clicked = () => OpenLab?.Invoke();
            TextButton(devRow, "DEBUG", Palette.UiPurple, new Vector2(160f, 0f), new Vector2(280f, 100f), 44f, null).Clicked = () => ToggleDebug?.Invoke();
            SetDevVisible(Application.isEditor || Debug.isDebugBuild);
        }

        void BuildSettings()
        {
            settingsCard = MakeCard("Settings", "SETTINGS", new Vector2(860f, 720f), true);
            SettingRows(settingsCard.Root, 165f);
            CloseButton(settingsCard, () => Back());
        }

        void SettingRows(RectTransform parent, float top)
        {
            SwitchRow(parent, "sfx", "SOUND", "speaker", new Vector2(0f, top), () => SetSfx?.Invoke(!SwitchOn("sfx")));
            SwitchRow(parent, "music", "MUSIC", "music", new Vector2(0f, top - 135f), () => SetMusic?.Invoke(!SwitchOn("music")));
            SwitchRow(parent, "motion", "REDUCE MOTION", "motion", new Vector2(0f, top - 270f), () => SetReducedMotion?.Invoke(!SwitchOn("motion")));
            var note = Body(Rect("Motion Note", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, top - 352f), new Vector2(680f, 60f)),
                "Reduces camera shake and extra animations. Gameplay stays the same.", 25f, Ink, Ink);
            note.textWrappingMode = TextWrappingModes.Normal;
        }

        void SwitchRow(RectTransform parent, string key, string label, string icon, Vector2 pos, System.Action clicked)
        {
            var row = Rect(label, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(680f, 116f));
            var bg = Img(row, UiArt.Pill, Inset);
            Sliced(bg, 116f, 0.5f);
            bg.raycastTarget = true;
            var badge = Rect("Badge", row, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(62f, 0f), new Vector2(84f, 84f));
            Img(badge, UiArt.RoundButton, theme.Accent);
            Img(Rect("Icon", badge, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(52f, 52f)), UiArt.Icon(icon), Color.white);
            var t = Txt(Rect("Label", row, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero), label, 42f, Ink, Ink, 0f, false);
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.rectTransform.offsetMin = new Vector2(126f, 0f);
            t.rectTransform.offsetMax = new Vector2(-190f, 0f);

            var track = Rect("Track", row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-100f, 0f), new Vector2(150f, 80f));
            var trackImg = Img(track, UiArt.Pill, Palette.UiGreen);
            Sliced(trackImg, 80f, 0.5f);
            var knob = Rect("Knob", track, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(35f, 2f), new Vector2(72f, 72f));
            Img(knob, UiArt.RoundButton, Color.white);
            switches.Add(new Switch { Key = key, Track = trackImg, Knob = knob, On = true });
            row.gameObject.AddComponent<UiPress>().Clicked = clicked;
        }

        bool SwitchOn(string key)
        {
            foreach (var s in switches) if (s.Key == key) return s.On;
            return false;
        }

        void SetSwitch(string key, bool on)
        {
            foreach (var s in switches)
            {
                if (s.Key != key) continue;
                bool changed = s.On != on;
                s.On = on;
                s.Track.color = on ? Palette.UiGreen : Themes.Hex(0xC9BFD6);
                var knob = s.Knob;
                float to = on ? 35f : -35f;
                Tweens.Kill(knob);
                if (!changed || ReducedMotion || !knob.gameObject.activeInHierarchy) knob.anchoredPosition = new Vector2(to, 2f);
                else
                {
                    float from = knob.anchoredPosition.x;
                    Tweens.Run(0.18f, k => knob.anchoredPosition = new Vector2(Mathf.Lerp(from, to, k), 2f), Tweens.OutCubic, owner: knob);
                }
            }
        }

        void SetDevVisible(bool visible)
        {
            if (devRow != null) devRow.gameObject.SetActive(visible);
        }

        public void UpdateSettings(bool sfx, bool music, bool reduced)
        {
            ReducedMotion = reduced;
            Calm = reduced;
            SetSwitch("sfx", sfx);
            SetSwitch("music", music);
            SetSwitch("motion", reduced);
        }

        public void ShowPause()
        {
            HideAll();
            ShowCard(pauseCard);
        }

        void ShowSettings()
        {
            Remember();
            HideAll();
            ShowCard(settingsCard);
        }

        // ---------- Level select ----------

        void BuildLevels()
        {
            levelsCard = MakeCard("Levels", "ROOMS", new Vector2(960f, 1640f), true);
            var viewport = Rect("Viewport", levelsCard.Root, Vector2.zero, Vector2.one, new Vector2(0f, -40f), new Vector2(-50f, -220f));
            viewport.gameObject.AddComponent<RectMask2D>();
            Img(viewport, null, new Color(1f, 1f, 1f, 0.001f)).raycastTarget = true;
            levelsContent = Rect("Content", viewport, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            levelsContent.pivot = new Vector2(0.5f, 1f);
            var layout = levelsContent.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.spacing = 26f;
            layout.padding = new RectOffset(0, 0, 20, 50);
            layout.childControlHeight = false;
            layout.childControlWidth = false;
            levelsContent.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = levelsContent;
            scroll.horizontal = false;
            scroll.viewport = viewport;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 30f;
            CloseButton(levelsCard, () => Back());
        }

        public void ShowLevels()
        {
            Remember();
            HideAll();
            foreach (Transform c in levelsContent) Destroy(c.gameObject);
            int unlocked = UnlockedCount?.Invoke() ?? 1;
            int current = CurrentLevel?.Invoke() ?? 0;
            for (int w = 0; w < Themes.All.Length; w++)
            {
                var t = Themes.All[w];
                int first = Themes.ChapterStart[w], end = Mathf.Min(Themes.ChapterStart[w + 1], Levels.All.Length);
                int got = 0;
                for (int i = first; i < end; i++) got += BestStars?.Invoke(Levels.All[i].Id) ?? 0;

                var header = Rect("Chapter", levelsContent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(860f, 104f));
                var hb = Img(header, UiArt.Button, t.Accent);
                Sliced(hb, 104f, 0.42f);
                var name = Txt(Rect("Name", header, Vector2.zero, Vector2.one, new Vector2(0f, 6f), Vector2.zero), $"{w + 1}  {t.Name}", 50f, Color.white, t.AccentDark, 0.45f, true);
                name.alignment = TextAlignmentOptions.MidlineLeft;
                name.rectTransform.offsetMin = new Vector2(40f, 8f);
                var count = Txt(Rect("Stars", header, Vector2.zero, Vector2.one, new Vector2(0f, 6f), Vector2.zero), $"{got}/{(end - first) * 3}", 44f, Color.white, t.AccentDark, 0.45f, true);
                count.alignment = TextAlignmentOptions.MidlineRight;
                count.rectTransform.offsetMax = new Vector2(-96f, 8f);
                Img(Rect("Star", header, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-52f, 8f), new Vector2(66f, 66f)), UiArt.Star, Color.white);

                var grid = Rect("Grid", levelsContent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(860f, 10f));
                var g = grid.gameObject.AddComponent<GridLayoutGroup>();
                g.cellSize = new Vector2(152f, 172f);
                g.spacing = new Vector2(20f, 22f);
                g.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
                g.constraintCount = 5;
                g.childAlignment = TextAnchor.UpperLeft;
                g.padding = new RectOffset(4, 0, 0, 0);
                grid.sizeDelta = new Vector2(860f, Mathf.CeilToInt((end - first) / 5f) * 194f);
                for (int i = first; i < end; i++) LevelTile(grid, i, i < unlocked, i == current, t);
            }
            ShowCard(levelsCard);
        }

        void LevelTile(RectTransform parent, int index, bool open, bool current, Theme t)
        {
            var r = Rect("Level " + (index + 1), parent, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(152f, 172f));
            if (current)
            {
                var ring = Img(Rect("Current", r, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(16f, 16f)), UiArt.Button, Palette.Gold);
                Sliced(ring, 188f, 0.3f);
            }
            var face = Img(Rect("Face", r, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero), UiArt.Button, open ? t.Accent : Themes.Hex(0xCFC6DA));
            Sliced(face, 172f, 0.3f);
            face.raycastTarget = true;
            if (open)
            {
                Txt(Rect("N", r, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -62f), new Vector2(150f, 90f)), (index + 1).ToString(),
                    70f, Color.white, t.AccentDark, 0.45f, true);
                int best = BestStars?.Invoke(Levels.All[index].Id) ?? 0;
                for (int s = 0; s < 3; s++)
                    Img(Rect("Star", r, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2((s - 1) * 40f, 44f + (s == 1 ? 6f : 0f)), new Vector2(42f, 42f)),
                        s < best ? UiArt.Star : UiArt.StarEmpty, Color.white);
                r.gameObject.AddComponent<UiPress>().Clicked = () => SelectLevel?.Invoke(index);
            }
            else
            {
                Img(Rect("Lock", r, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(76f, 76f)), UiArt.Icon("lock"), Color.white);
            }
        }

        // ---------- Skins ----------

        // ---------- Dress Up ----------

        public System.Func<Outfit> CurrentOutfit;
        public System.Func<CosmeticItem, bool> OwnsItem;
        public System.Action<CosmeticItem> EquipItem;
        Slot wardrobeTab = Slot.Employee;
        RawImage wardrobePreview;
        readonly List<(Slot slot, Image bg, TextMeshProUGUI label)> wardrobeTabs = new List<(Slot, Image, TextMeshProUGUI)>();
        RectTransform wardrobeContent;

        void BuildSkins()
        {
            skinsCard = MakeCard("Wardrobe", "WARDROBE", new Vector2(980f, 1560f), true);
            var c = skinsCard.Root;
            // Live preview: Puffy in the current outfit on a sunburst disc.
            var disc = Rect("Disc", c, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -330f), new Vector2(380f, 380f));
            Img(disc, Shapes.Circle, Themes.Hex(0xE9EEF6));
            var prev = Rect("Preview", c, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -320f), new Vector2(380f, 380f));
            wardrobePreview = prev.gameObject.AddComponent<RawImage>();
            wardrobePreview.raycastTarget = false;
            prev.gameObject.AddComponent<Bobber>().Hud = this;

            // Tabs.
            var tabs = new[] { (Slot.Employee, "STAFF"), (Slot.Color, "TIES"), (Slot.Hat, "HATS"), (Slot.Face, "FACE") };
            for (int i = 0; i < tabs.Length; i++)
            {
                var (slot, label) = tabs[i];
                var r = Rect(label, c, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2((i - 1.5f) * 222f, -590f), new Vector2(206f, 96f));
                var bg = Img(r, UiArt.Button, Inset);
                Sliced(bg, 96f, 0.4f);
                bg.raycastTarget = true;
                var t = Txt(Rect("Label", r, Vector2.zero, Vector2.one, new Vector2(0f, 6f), Vector2.zero), label, 38f, Ink, Ink, 0f, false);
                wardrobeTabs.Add((slot, bg, t));
                r.gameObject.AddComponent<UiPress>().Clicked = () => { wardrobeTab = slot; RefreshSkins(); };
            }

            // Scrolling grid of items.
            var view = Rect("Viewport", c, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 470f), new Vector2(900f, 860f));
            view.gameObject.AddComponent<RectMask2D>();
            Img(view, UiArt.Pill, new Color(1f, 1f, 1f, 0.001f)).raycastTarget = true;
            wardrobeContent = Rect("Content", view, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, 800f));
            wardrobeContent.pivot = new Vector2(0.5f, 1f);
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.content = wardrobeContent;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 30f;
            CloseButton(skinsCard, () => Back());
            IconStudio.Rendered += RefreshWardrobeIcons;
        }

        public void ShowSkins()
        {
            Remember();
            HideAll();
            RefreshSkins();
            ShowCard(skinsCard);
        }

        readonly List<(CosmeticItem item, RawImage icon)> wardrobeIcons = new List<(CosmeticItem, RawImage)>();

        void RefreshWardrobeIcons()
        {
            if (wardrobePreview != null) wardrobePreview.texture = IconStudio.HeroIcon;
            foreach (var (item, icon) in wardrobeIcons)
                if (icon != null && IconStudio.ItemIcons.TryGetValue(item.Id, out var tex)) icon.texture = tex;
        }

        void RefreshSkins()
        {
            foreach (var (slot, bg, label) in wardrobeTabs)
            {
                bool on = slot == wardrobeTab;
                bg.color = on ? theme.Accent : Inset;
                label.color = on ? Color.white : Ink;
            }
            foreach (Transform c in wardrobeContent) Destroy(c.gameObject);
            wardrobeIcons.Clear();
            var outfit = CurrentOutfit?.Invoke() ?? Cosmetics.Default;
            string worn = outfit.Get(wardrobeTab);
            var items = Cosmetics.InSlot(wardrobeTab);
            const float tileW = 210f, tileH = 270f;
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                bool owned = OwnsItem?.Invoke(item) ?? false;
                bool equipped = item.Id == worn;
                int col = i % 4, row = i / 4;
                var tile = Rect(item.Id, wardrobeContent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2((col - 1.5f) * (tileW + 14f), -tileH / 2f - 10f - row * (tileH + 16f)), new Vector2(tileW, tileH));
                var bg = Img(tile, UiArt.Button, equipped ? theme.Accent : owned ? Inset : Themes.Hex(0xE2DAEA));
                Sliced(bg, tileH, 0.18f);
                bg.raycastTarget = true;
                // Rarity stripe along the top.
                var stripe = Img(Rect("Rarity", tile, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(tileW - 40f, 12f)), UiArt.Pill, Cosmetics.RarityColor(item.Rarity));
                Sliced(stripe, 12f, 0.5f);

                var frame = Rect("Portrait", tile, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -108f), new Vector2(160f, 160f));
                Img(frame, UiArt.RoundButton, Color.white);
                var maskR = Rect("Mask", frame, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(138f, 138f));
                Img(maskR, UiArt.Pill, Color.white);
                maskR.gameObject.AddComponent<Mask>().showMaskGraphic = false;
                var raw = Rect("Icon", maskR, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject.AddComponent<RawImage>();
                raw.raycastTarget = false;
                raw.color = owned ? Color.white : new Color(0.5f, 0.5f, 0.56f, 1f);
                if (IconStudio.ItemIcons.TryGetValue(item.Id, out var tex)) raw.texture = tex;
                else raw.color = item.Slot == Slot.Color ? item.Body : new Color(1f, 1f, 1f, 0.2f);
                wardrobeIcons.Add((item, raw));

                string caption = owned ? item.Name : item.FromChest ? "CHEST" : item.StarsNeeded.ToString();
                var label = Body(Rect("Name", tile, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(item.FromChest || owned ? 0f : 16f, 46f), new Vector2(tileW - 10f, 50f)),
                    caption, caption.Length > 9 ? 24f : 30f, equipped ? Color.white : Ink, Ink);
                if (equipped)
                {
                    Img(Rect("Check", tile, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -16f), new Vector2(58f, 58f)), UiArt.RoundButton, Palette.UiGreen);
                    Img(Rect("CheckIcon", tile, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -13f), new Vector2(36f, 36f)), UiArt.Icon("check"), Color.white);
                }
                if (!owned)
                {
                    Img(Rect("Lock", frame, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(60f, 60f)), UiArt.Icon(item.FromChest ? "chest" : "lock"), Color.white)
                        .gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.35f);
                    if (!item.FromChest)
                        Img(Rect("Star", tile, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-30f, 48f), new Vector2(40f, 40f)), UiArt.Star, Color.white);
                    continue;
                }
                var chosen = item;
                tile.gameObject.AddComponent<UiPress>().Clicked = () =>
                {
                    EquipItem?.Invoke(chosen);
                    RefreshSkins();
                };
            }
            int rows = (items.Count + 3) / 4;
            wardrobeContent.sizeDelta = new Vector2(0f, rows * (tileH + 16f) + 20f);
            wardrobeContent.anchoredPosition = Vector2.zero;
            RefreshWardrobeIcons();
        }

        // ---------- Daily quests ----------

        public System.Func<QuestDef[]> TodayQuests;
        public System.Func<int, int> QuestProgressOf;
        public System.Func<int, bool> QuestClaimedOf;
        public System.Func<bool> DailyChestReady;
        public System.Func<int> ChestsWaiting;
        public System.Action<int> ClaimQuest;
        public System.Action OpenDailyChest;
        Card questsCard;
        RectTransform questsList, questBadge;
        TextMeshProUGUI questTimer, questBadgeText;

        void BuildQuests()
        {
            questsCard = MakeCard("Quests", "DAILY TASKS", new Vector2(940f, 1240f), true);
            questTimer = Body(Rect("Timer", questsCard.Root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(800f, 60f)),
                "", 36f, Ink, Ink);
            questsList = Rect("List", questsCard.Root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -200f), new Vector2(860f, 980f));
            questsList.pivot = new Vector2(0.5f, 1f);
            CloseButton(questsCard, () => Back());
        }

        public void ShowQuests()
        {
            Remember();
            HideAll();
            RefreshQuests();
            ShowCard(questsCard);
        }

        public void RefreshQuests()
        {
            if (questsList == null) return;
            foreach (Transform c in questsList) Destroy(c.gameObject);
            var left = Quests.UntilReset();
            questTimer.text = $"New tasks in {left.Hours}h {left.Minutes}m";

            // The free daily chest.
            bool ready = DailyChestReady?.Invoke() ?? false;
            var daily = QuestRow(0f, ready ? Themes.Hex(0xFFF1C9) : Inset);
            Img(Rect("Chest", daily, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(90f, 0f), new Vector2(110f, 110f)), UiArt.Icon("chest"), Themes.Hex(0xC9783C));
            Txt(Rect("Title", daily, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(330f, 22f), new Vector2(360f, 60f)), "FREE GIFT", 46f, Ink, Ink, 0f, false)
                .alignment = TextAlignmentOptions.MidlineLeft;
            Body(Rect("Sub", daily, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(330f, -28f), new Vector2(360f, 50f)), ready ? "Employee appreciation, daily." : "Appreciated again tomorrow.", 30f, Ink, Ink)
                .alignment = TextAlignmentOptions.MidlineLeft;
            QuestButton(daily, ready ? "OPEN" : "TOMORROW", ready, () => OpenDailyChest?.Invoke());

            var quests = TodayQuests?.Invoke();
            if (quests == null) return;
            for (int i = 0; i < quests.Length; i++)
            {
                var q = quests[i];
                int progress = QuestProgressOf?.Invoke(i) ?? 0;
                bool done = progress >= q.Target, claimed = QuestClaimedOf?.Invoke(i) ?? false;
                var row = QuestRow(-(i + 1) * 240f, claimed ? Themes.Hex(0xE4F6DE) : Inset);
                Body(Rect("Quest", row, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(260f, 36f), new Vector2(460f, 60f)), q.Text, 38f, Ink, Ink)
                    .alignment = TextAlignmentOptions.MidlineLeft;
                // Progress bar.
                var bar = Rect("Bar", row, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(260f, -30f), new Vector2(460f, 34f));
                Sliced(Img(bar, UiArt.Pill, new Color(0.2f, 0.12f, 0.35f, 0.18f)), 34f, 0.5f);
                float k = Mathf.Clamp01(progress / (float)q.Target);
                var fill = Rect("Fill", bar, new Vector2(0f, 0f), new Vector2(Mathf.Max(0.08f, k), 1f), Vector2.zero, Vector2.zero);
                Sliced(Img(fill, UiArt.Pill, done ? Palette.UiGreen : Palette.Gold), 34f, 0.5f);
                fill.gameObject.SetActive(k > 0f);
                Body(Rect("Count", bar, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero), $"{progress}/{q.Target}", 26f, Ink, Ink);
                int index = i;
                QuestButton(row, claimed ? "DONE" : "CLAIM", done && !claimed, () => ClaimQuest?.Invoke(index));
            }
        }

        RectTransform QuestRow(float y, Color color)
        {
            var r = Rect("Row", questsList, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y - 110f), new Vector2(860f, 220f));
            Sliced(Img(r, UiArt.Button, color), 220f, 0.2f);
            return r;
        }

        void QuestButton(RectTransform row, string label, bool active, System.Action click)
        {
            var b = Rect("Button", row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-130f, 0f), new Vector2(220f, 110f));
            var img = Img(b, UiArt.Button, active ? Palette.Gold : Themes.Hex(0xCFC6DA));
            Sliced(img, 110f, 0.36f);
            img.raycastTarget = active;
            Txt(Rect("Label", b, Vector2.zero, Vector2.one, new Vector2(0f, 6f), Vector2.zero), label, label.Length > 6 ? 34f : 46f, Color.white, Color.Lerp(img.color, Ink, 0.6f), 0.35f, false);
            if (active) b.gameObject.AddComponent<UiPress>().Clicked = click;
        }

        // A red count bubble on the title's QUESTS button.
        public void SetQuestBadge(int count)
        {
            if (questBadge == null) return;
            questBadge.gameObject.SetActive(count > 0);
            questBadgeText.text = count.ToString();
        }

        // ---------- Speech bubble ----------

        RectTransform speech;
        TextMeshProUGUI speechText;
        float speechUntil;

        void BuildSpeech()
        {
            speech = Rect("Speech", hudRoot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560f, 120f));
            speech.pivot = new Vector2(0.5f, 0f);
            var shadow = Img(Rect("Shadow", speech, Vector2.zero, Vector2.one, new Vector2(0f, -10f), new Vector2(40f, 40f)), UiArt.SoftShadow, new Color(0.08f, 0.03f, 0.22f, 0.35f));
            Sliced(shadow, 160f, 0.5f);
            var tail = Rect("Tail", speech, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(40f, 40f));
            tail.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Img(tail, Shapes.Square, Color.white);
            Sliced(Img(speech, UiArt.Pill, Color.white), 120f, 0.5f);
            speechText = Body(Rect("Text", speech, Vector2.zero, Vector2.one, new Vector2(0f, 2f), new Vector2(-48f, -24f)), "", 34f, Themes.Hex(0x3A2856), Themes.Hex(0x3A2856));
            speechText.textWrappingMode = TextWrappingModes.Normal;
            speech.gameObject.SetActive(false);
        }

        // Puffy says something for a few seconds; the bubble sizes itself to the line.
        public void Say(string text, float seconds = 3.2f)
        {
            if (speech == null || string.IsNullOrEmpty(text)) return;
            speechText.text = text;
            var size = speechText.GetPreferredValues(text, 560f, 400f);
            speech.sizeDelta = new Vector2(Mathf.Clamp(size.x + 60f, 220f, 620f), Mathf.Max(96f, Mathf.Min(size.y, 170f) + 40f));
            speech.gameObject.SetActive(true);
            speechUntil = Time.unscaledTime + seconds;
            Tweens.Kill(speech);
            if (ReducedMotion) speech.localScale = Vector3.one;
            else
            {
                speech.localScale = Vector3.zero;
                Tweens.Run(0.35f, k => speech.localScale = Vector3.one * k, Tweens.OutBack, 0f, owner: speech);
            }
        }

        public void HideSpeech()
        {
            if (speech != null) speech.gameObject.SetActive(false);
        }

        // Keeps the bubble above Puffy's head, inside the screen.
        public void SetSpeechAnchor(Vector2 screen, bool visible)
        {
            if (speech == null || !speech.gameObject.activeSelf) return;
            if (!visible || Time.unscaledTime > speechUntil)
            {
                speech.gameObject.SetActive(false);
                return;
            }
            RectTransformUtility.ScreenPointToLocalPointInRectangle(hudRoot, screen, null, out var local);
            var half = hudRoot.rect.size * 0.5f;
            float w = speech.sizeDelta.x * 0.5f;
            local.x = Mathf.Clamp(local.x, -half.x + w + 20f, half.x - w - 20f);
            local.y = Mathf.Min(local.y, half.y - speech.sizeDelta.y - 190f);
            speech.anchoredPosition = local;
        }

        // ---------- Completion ----------

        void BuildComplete()
        {
            completeCard = MakeCard("Complete", "PERFECT!", new Vector2(900f, 1000f), true);
            var c = completeCard.Root;
            sunburst = Rect("Sunburst", c, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 200f), new Vector2(900f, 900f));
            Img(sunburst, UiArt.Sunburst, new Color(1f, 0.82f, 0.35f, 0.6f));
            sunburst.sizeDelta = new Vector2(780f, 780f);
            sunburst.SetSiblingIndex(2);  // above the panel, below the ribbon and the stars
            completeLevel = Txt(Rect("Level", c, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 360f), new Vector2(800f, 60f)),
                "LEVEL 1 CLEAR", 44f, Ink, Ink, 0f, false);
            float[] xs = { -230f, 0f, 230f }, ys = { 150f, 195f, 150f }, sizes = { 210f, 260f, 210f }, rot = { 12f, 0f, -12f };
            for (int i = 0; i < 3; i++)
            {
                var s = Img(Rect("Star", c, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(xs[i], ys[i]), new Vector2(sizes[i], sizes[i])), UiArt.Star, Color.white);
                s.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rot[i]);
                completeStars.Add(s);
            }
            completeShots = StatChip(c, new Vector2(-150f, -60f), "SHOTS");
            completePar = StatChip(c, new Vector2(150f, -60f), "PAR");
            completeExtra = Txt(Rect("Extra", c, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -170f), new Vector2(800f, 60f)),
                "", 40f, Themes.Hex(0xF29B1D), Ink, 0f, false);
            var next = TextButton(c, "NEXT", Palette.UiGreen, new Vector2(80f, -330f), new Vector2(500f, 170f), 76f, "play");
            next.Clicked = () => Next?.Invoke();
            completeNextLabel = next.GetComponentInChildren<TextMeshProUGUI>();
            completeNextIcon = next.transform.Find("Icon").GetComponent<Image>();
            IconButton(c, "Replay", new Vector2(0.5f, 0.5f), new Vector2(-275f, -322f), 160f, Palette.UiBlue, "restart", Color.white).Clicked = () => Replay?.Invoke();
        }

        TextMeshProUGUI StatChip(RectTransform parent, Vector2 pos, string label)
        {
            var chip = Rect(label, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(260f, 130f));
            Sliced(Img(chip, UiArt.Pill, Inset), 130f, 0.3f);
            Txt(Rect("Label", chip, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -32f), new Vector2(260f, 40f)), label, 30f,
                Themes.Hex(0x9A8AAE), Ink, 0f, false).characterSpacing = 6f;
            return Txt(Rect("Value", chip, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 44f), new Vector2(260f, 70f)), "0", 60f, Ink, Ink, 0f, false);
        }

        // finalLevel: the last room there is, so NEXT becomes HOME instead of replaying it.
        public void ShowComplete(int levelNumber, int stars, int shots, int par, bool allPickups, bool finalLevel = false)
        {
            HideAll();
            completeCard.Title.text = stars >= 3 ? "PERFECT!" : stars == 2 ? "GREAT!" : "NICE!";
            completeLevel.text = levelNumber > 0 ? $"LEVEL {levelNumber} CLEAR" : "LAB CLEAR";
            completeShots.text = shots.ToString();
            completePar.text = par.ToString();
            completeShots.color = shots <= par ? Themes.Hex(0x2EAD4B) : Ink;
            completeExtra.text = finalLevel ? "OFFICE CLEARED! MORE ROOMS SOON" : allPickups ? "FULLY CAFFEINATED!" : shots <= par ? "UNDER PAR!" : "";
            completeNextLabel.text = finalLevel ? "HOME" : "NEXT";
            completeNextIcon.sprite = UiArt.Icon(finalLevel ? "home" : "play");
            ShowCard(completeCard);
            for (int i = 0; i < completeStars.Count; i++)
            {
                var s = completeStars[i];
                bool earned = i < stars;
                s.sprite = earned ? UiArt.Star : UiArt.StarEmpty;
                int idx = i;
                float baseRot = i == 0 ? 12f : i == 2 ? -12f : 0f;
                Tweens.Kill(s);
                if (ReducedMotion)
                {
                    s.rectTransform.localScale = Vector3.one;
                    s.rectTransform.localRotation = Quaternion.Euler(0f, 0f, baseRot);
                    s.color = new Color(1f, 1f, 1f, 0f);
                    Tweens.Run(0.2f, k => s.color = new Color(1f, 1f, 1f, k), Tweens.Linear, 0.2f + i * 0.1f, owner: s);
                    continue;
                }
                s.color = Color.white;
                s.rectTransform.localScale = Vector3.zero;
                Tweens.Run(0.5f, k =>
                {
                    s.rectTransform.localScale = Vector3.one * k;
                    s.rectTransform.localRotation = Quaternion.Euler(0f, 0f, baseRot + (1f - k) * 50f);
                }, Tweens.OutBack, 0.35f + i * 0.22f, () =>
                {
                    if (!earned) return;
                    Haptics.Play(Haptics.Style.Rigid, 0.6f + 0.2f * idx);
                    StarBurst(s.rectTransform);
                }, s);
            }
        }

        // ---------- Cards ----------

        Card MakeCard(string name, string titleText, Vector2 size, bool ribbon)
        {
            var r = Rect(name, transform as RectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            var card = new Card { Root = r, Group = r.gameObject.AddComponent<CanvasGroup>() };
            var shadow = Img(Rect("Shadow", r, Vector2.zero, Vector2.one, new Vector2(0f, -22f), new Vector2(70f, 70f)), UiArt.SoftShadow, new Color(0.1f, 0.05f, 0.2f, 0.45f));
            Sliced(shadow, size.y, 0.12f);
            var bg = Img(Rect("Panel", r, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero), UiArt.Panel, CardTint);
            Sliced(bg, size.y, 0.06f);
            bg.raycastTarget = true;
            if (ribbon)
            {
                var head = Rect("Ribbon", r, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, 6f), new Vector2(Mathf.Min(800f, size.x + 60f), 190f));
                card.Ribbon = Img(head, UiArt.Ribbon, theme.Accent);
                card.Ribbon.type = Image.Type.Sliced;
                card.Title = Txt(Rect("Title", head, Vector2.zero, Vector2.one, new Vector2(0f, 30f), new Vector2(-200f, -40f)), titleText, 76f, Color.white, Ink, 0.45f, true);
            }
            r.gameObject.SetActive(false);
            return card;
        }

        void CloseButton(Card card, System.Action clicked)
        {
            var size = card.Root.sizeDelta;
            IconButton(card.Root, "Close", new Vector2(0.5f, 0.5f), new Vector2(size.x / 2f - 30f, size.y / 2f - 30f), 110f, Palette.UiRed, "close", Color.white).Clicked = clicked;
        }

        void Remember()
        {
            titleReturn = title.gameObject.activeSelf;
            returnTo = pauseCard.Root.gameObject.activeSelf ? pauseCard : null;
        }

        void Back()
        {
            HideAll();
            if (titleReturn) title.gameObject.SetActive(true);
            else if (returnTo != null) ShowCard(returnTo);
            titleReturn = false;
            returnTo = null;
        }

        public void HideAll()
        {
            foreach (var s in completeStars) Tweens.Kill(s);
            foreach (var c in new[] { pauseCard, levelsCard, skinsCard, completeCard, settingsCard, questsCard })
            {
                Tweens.Kill(c.Root);
                c.Root.gameObject.SetActive(false);
            }
            Tweens.Kill(dim);
            dim.gameObject.SetActive(false);
            title.gameObject.SetActive(false);
        }

        void ShowCard(Card card)
        {
            if (card.Ribbon != null) card.Ribbon.color = theme.Accent;
            dim.gameObject.SetActive(true);
            dim.SetAsLastSibling();
            card.Root.SetAsLastSibling();
            var dimImage = dim.GetComponent<Image>();
            Tweens.Run(0.2f, k => dimImage.color = new Color(0.1f, 0.06f, 0.18f, 0.5f * k), Tweens.Linear, owner: dim);
            card.Root.gameObject.SetActive(true);
            card.Root.anchoredPosition = Vector2.zero;
            var group = card.Group;
            var root = card.Root;
            if (ReducedMotion)
            {
                root.localScale = Vector3.one;
                group.alpha = 0f;
                Tweens.Run(0.15f, k => group.alpha = k, Tweens.Linear, owner: root);
                return;
            }
            group.alpha = 0f;
            root.localScale = Vector3.one * 0.85f;
            Tweens.Run(0.4f, k =>
            {
                root.localScale = Vector3.one * Mathf.LerpUnclamped(0.85f, 1f, k);
                group.alpha = Mathf.Clamp01(k * 3f);
            }, Tweens.OutBack, owner: root);
        }

        void Update()
        {
            if (Screen.safeArea != lastSafe) ApplySafeArea();
            if (Screen.width != lastScreen.x || Screen.height != lastScreen.y) FitScaler();
            if (title.gameObject.activeSelf && !ReducedMotion)
            {
                // Idle wave on the logo.
                for (int i = 0; i < logoLetters.Count; i++)
                {
                    var r = logoLetters[i];
                    if (r.localScale.x < 0.99f && r.localScale.x > 0f && Tweens.Running(r)) continue;
                    float w = Mathf.Sin(Time.unscaledTime * 2.6f - i * 0.55f);
                    r.localScale = new Vector3(1f - 0.03f * w, 1f + 0.04f * w, 1f);
                }
            }
            if (completeCard.Root.gameObject.activeSelf && !ReducedMotion)
                sunburst.localRotation = Quaternion.Euler(0f, 0f, Time.unscaledTime * 12f);
        }

        void ApplySafeArea()
        {
            lastSafe = Screen.safeArea;
            var min = lastSafe.position;
            var max = lastSafe.position + lastSafe.size;
            foreach (var r in new[] { safe, titleArea })
            {
                if (r == null) continue;
                r.anchorMin = new Vector2(min.x / Screen.width, min.y / Screen.height);
                r.anchorMax = new Vector2(max.x / Screen.width, max.y / Screen.height);
            }
        }

        // Little stars that fly out of a UI element.
        void StarBurst(RectTransform from)
        {
            if (ReducedMotion) return;
            for (int i = 0; i < 8; i++)
            {
                var r = Rect("Sparkle", from.parent as RectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), from.anchoredPosition,
                    new Vector2(50f, 50f));
                Img(r, UiArt.Star, Color.white);
                Vector2 dir = Quaternion.Euler(0f, 0f, i * 45f + Random.Range(-15f, 15f)) * Vector2.up;
                Vector2 start = from.anchoredPosition;
                float dist = Random.Range(160f, 260f);
                Tweens.Run(0.6f, k =>
                {
                    r.anchoredPosition = start + dir * (dist * k);
                    r.localScale = Vector3.one * (1f - k);
                    r.localRotation = Quaternion.Euler(0f, 0f, k * 180f);
                }, Tweens.OutCubic, 0f, () => Destroy(r.gameObject), r);
            }
        }

        void Pop(RectTransform r, float delay, float amount = 1.15f)
        {
            Tweens.Kill(r);
            r.localScale = Vector3.one;
            if (ReducedMotion) return;
            Tweens.Run(0.45f, k => r.localScale = Vector3.one * Mathf.LerpUnclamped(amount, 1f, k), Tweens.OutElastic, delay, owner: r);
        }

        // A slow, gentle scale pulse to draw the eye to the main button.
        void Breathe(RectTransform r)
        {
            r.gameObject.AddComponent<Breather>().Hud = this;
        }

        class Breather : MonoBehaviour
        {
            public Hud Hud;

            void Update()
            {
                if (Hud.ReducedMotion || Tweens.Running(this) || Tweens.Running(GetComponent<UiPress>())) return;
                float s = 1f + 0.025f * Mathf.Sin(Time.unscaledTime * 3f);
                transform.localScale = new Vector3(s, s, 1f);
            }
        }

        // ---------- Builders ----------

        RectTransform Capsule(RectTransform parent, string name, Vector2 anchor, Vector2 pos, Vector2 size, Color color)
        {
            var r = Rect(name, parent, anchor, anchor, pos, size);
            Sliced(Img(r, UiArt.Pill, color), size.y, 0.5f);
            return r;
        }

        UiPress IconButton(RectTransform parent, string name, Vector2 anchor, Vector2 pos, float size, Color color, string icon, Color iconColor)
        {
            var r = Rect(name, parent, anchor, anchor, pos, new Vector2(size, size));
            r.gameObject.AddComponent<CanvasGroup>();
            var shadow = Img(Rect("Shadow", r, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(size * 1.25f, size * 1.25f)),
                UiArt.SoftShadow, new Color(0.1f, 0.05f, 0.2f, 0.28f));
            Sliced(shadow, size * 1.25f, 0.5f);
            var img = Img(r, UiArt.RoundButton, color);
            img.raycastTarget = true;
            var ic = Img(Rect("Icon", r, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, size * 0.05f), Vector2.one * size * 0.56f), UiArt.Icon(icon), iconColor);
            if (iconColor == Color.white) ic.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.22f);
            shadow.transform.SetAsFirstSibling();
            return r.gameObject.AddComponent<UiPress>();
        }

        // A round icon button with a caption under it.
        UiPress RoundAction(RectTransform parent, string label, string icon, Color color, Vector2 pos, Color? captionOutline = null)
        {
            var press = IconButton(parent, label, new Vector2(0.5f, 0.5f), pos, 150f, color, icon, Color.white);
            bool onCard = captionOutline.HasValue;
            Txt(Rect("Caption", press.transform as RectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -32f), new Vector2(260f, 50f)),
                label, 34f, onCard ? Ink : Color.white, captionOutline ?? Ink, onCard ? 0f : 0.4f, !onCard);
            return press;
        }

        UiPress TextButton(RectTransform parent, string text, Color color, Vector2 pos, Vector2 size, float fontSize, string icon)
        {
            var r = Rect(text, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            var shadow = Img(Rect("Shadow", r, Vector2.zero, Vector2.one, new Vector2(0f, -12f), new Vector2(40f, 40f)), UiArt.SoftShadow, new Color(0.1f, 0.05f, 0.2f, 0.3f));
            Sliced(shadow, size.y + 40f, 0.4f);
            var img = Img(r, UiArt.Button, color);
            Sliced(img, size.y, 0.36f);
            img.raycastTarget = true;
            Color dark = Color.Lerp(color, Ink, 0.55f);
            var label = Rect("Label", r, Vector2.zero, Vector2.one, new Vector2(icon != null ? -36f : 0f, 8f), Vector2.zero);
            Txt(label, text, fontSize, Color.white, dark, 0.45f, true);
            if (icon != null)
            {
                float s = size.y * 0.36f;
                var ic = Img(Rect("Icon", r, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-size.y * 0.5f, 10f), new Vector2(s, s)), UiArt.Icon(icon), Color.white);
                ic.gameObject.AddComponent<Shadow>().effectColor = new Color(dark.r, dark.g, dark.b, 0.8f);
                ic.GetComponent<Shadow>().effectDistance = new Vector2(0f, -5f);
            }
            return r.gameObject.AddComponent<UiPress>();
        }

        TextMeshProUGUI Txt(RectTransform parent, string text, float size, Color face, Color outline, float width = 0.3f, bool underlay = true)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            var r = go.GetComponent<RectTransform>();
            r.SetParent(parent, false);
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.offsetMin = r.offsetMax = Vector2.zero;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.font = font;
            t.fontSharedMaterial = TextMat(outline, width, underlay);
            t.text = text;
            t.fontSize = size;
            t.color = face;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.overflowMode = TextOverflowModes.Overflow;
            t.raycastTarget = false;
            t.characterSpacing = 2f;
            return t;
        }

        // Sentence text in the rounded body face.
        TextMeshProUGUI Body(RectTransform parent, string text, float size, Color face, Color outline, float width = 0f, bool underlay = false)
        {
            var t = Txt(parent, text, size, face, outline, width, underlay);
            t.font = bodyFont;
            t.fontSharedMaterial = TextMat(outline, width, underlay, bodyFont);
            t.characterSpacing = 0f;
            return t;
        }

        readonly Dictionary<(Color, float, bool, TMP_FontAsset), Material> fontMaterials = new Dictionary<(Color, float, bool, TMP_FontAsset), Material>();

        // One shared material per font, outline color, width, and shadow, so text batches well.
        Material TextMat(Color outline, float width, bool underlay, TMP_FontAsset face = null)
        {
            face ??= font;
            var key = (outline, width, underlay, face);
            if (fontMaterials.TryGetValue(key, out var m) && m != null) return m;
            var preset = face == bodyFont ? bodyBold : face == displayFont ? displayBold : boldPreset;
            // Sentences get minimal outlines; the chunky UI face keeps most of its outline, like a casual game's labels.
            if (face == bodyFont) width *= 0.5f;
            else if (face != displayFont) width *= 0.8f;
            m = new Material(underlay && preset != null ? preset : face.material) { name = "Text " + fontMaterials.Count };
            m.SetTexture(ShaderUtilities.ID_MainTex, face.atlasTexture);
            m.SetFloat(ShaderUtilities.ID_OutlineWidth, width);
            m.SetColor(ShaderUtilities.ID_OutlineColor, outline);
            // Outline, dilation, and the drop shadow must all fit inside the atlas padding (about 0.8 of the SDF
            // spread here); beyond that a glyph's quad samples its neighbors in the atlas and letters smear.
            float dilate = width > 0f ? 0.05f : 0f;
            m.SetFloat(ShaderUtilities.ID_FaceDilate, dilate);
            if (underlay)
            {
                m.EnableKeyword(ShaderUtilities.Keyword_Underlay);
                m.SetColor(ShaderUtilities.ID_UnderlayColor, outline);
                m.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0f);
                m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -Mathf.Clamp(0.8f - width - dilate, 0.15f, 0.35f));
                m.SetFloat(ShaderUtilities.ID_UnderlayDilate, width);
                m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0f);
            }
            else m.DisableKeyword(ShaderUtilities.Keyword_Underlay);
            ShaderUtilities.UpdateShaderRatios(m);
            return fontMaterials[key] = m;
        }

        // 9-slice with corners scaled to a fraction of the element height.
        static void Sliced(Image img, float height, float radiusFraction)
        {
            img.type = Image.Type.Sliced;
            var border = img.sprite.border;
            float spriteRadius = Mathf.Max(1f, border.w);
            img.pixelsPerUnitMultiplier = spriteRadius / Mathf.Max(1f, height * radiusFraction);
        }

        static Image Img(RectTransform r, Sprite sprite, Color color)
        {
            var img = r.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            return img;
        }

        static RectTransform Rect(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var r = go.GetComponent<RectTransform>();
            r.SetParent(parent, false);
            r.anchorMin = anchorMin;
            r.anchorMax = anchorMax;
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = pos;
            r.sizeDelta = size;
            return r;
        }
    }
}
