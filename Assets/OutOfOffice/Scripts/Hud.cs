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
    public partial class Hud : MonoBehaviour
    {
        // Game actions. OutOfOfficeGame wires these up.
        public System.Action Play, Pause, Resume, Restart, Undo, Next, Replay, Home, OpenLab, ToggleDebug;
        public System.Action<int> SelectLevel;
        public System.Action<bool> SetSfx, SetMusic, SetReducedMotion;
        public System.Func<int> UnlockedCount, CurrentLevel;
        public System.Func<string, int> BestStars;

        public const float TopHeight = 175f;     // reference units the top bar covers (the board may scroll under it)
        public const float BottomHeight = 170f;  // reference units the bottom buttons cover (OUT-40: 160 px buttons)
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
        TextMeshProUGUI pauseNote;
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
            var events = EventSystem.current;
            if (events == null) events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)).GetComponent<EventSystem>();
            // A tap may wander a couple of millimetres before it counts as a drag (Unity's default is 10 pixels, a hair
            // on a phone screen), so a slightly shaky tap on a list tile still chooses it.
            events.pixelDragThreshold = Mathf.Max(10, Mathf.RoundToInt((Screen.dpi > 0f ? Screen.dpi : 300f) * 0.08f));
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
            BuildFail();
            BuildUndoOffer();
            BuildHire();
            BuildShop();
            BuildEnergyCard();
            BuildQuit();
            BuildGadgetCard();  // the first-time experience (OUT-11)
            BuildGuide();
            BuildHand();
            BuildBoosters();  // OUT-13
            BuildOdds();  // OUT-14
            BuildClipShop();
            BuildAchievements();  // OUT-9, on the tasks card
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
            Calm = UiPress.Calm = true;
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
            chapterText = Txt(chapterPill, Themes.All[0].Name, 24f, Color.white, Ink, 0.3f, false);
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

            // Undo and restart: big, thumb-sized buttons in the bottom corners (OUT-40), clear of the board.
            var undo = IconButton(hudRoot, "Undo", new Vector2(0f, 0f), new Vector2(100f, 96f), 160f, Palette.UiBlue, "undo", Color.white);
            undo.Clicked = () => Undo?.Invoke();
            undoButton = undo.transform as RectTransform;
            undoBadge = Badge(undoButton, "3");  // free undos left (OUT-41)
            undoBadgeText = undoBadge.GetComponentInChildren<TextMeshProUGUI>();
            var badgeGem = UiArt.Icon3D("gem");
            if (badgeGem != null)
            {
                undoBadgeGem = Img(Rect("Gem", undoBadge, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 2f), new Vector2(60f, 60f)), badgeGem, Color.white).gameObject;
                undoBadgeGem.SetActive(false);
            }
            hudBlockers.Add(undoButton);
            var restart = IconButton(hudRoot, "Restart", new Vector2(1f, 0f), new Vector2(-100f, 96f), 160f, Palette.UiRed, "restart", Color.white);
            restart.Clicked = () => Restart?.Invoke();
            BuildRestartChip(restart.transform as RectTransform);  // what starting over takes, once it takes a play (OUT-48)
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
            // Joke room names run long (OUT-28): wrap to two lines and shrink to fit the banner instead of overflowing.
            bannerText.textWrappingMode = TextWrappingModes.Normal;
            bannerText.enableAutoSizing = true;
            bannerText.fontSizeMin = 54f;
            bannerText.fontSizeMax = 104f;
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

        // max: the level's shot limit (OUT-27); 0 for no limit (the lab). The count turns orange on the last shot.
        public void SetShots(int shots, int par, int max = 0)
        {
            string next = max > 0 ? $"SHOTS {shots}/{max}  ·  PAR {par}" : $"SHOTS {shots}  ·  PAR {par}";
            if (shotsText.text != next)
            {
                shotsText.text = next;
                Pop(shotsPill, 0f, 1.1f);
            }
            bool last = max > 0 && shots >= max - 1;
            shotsText.color = last ? Themes.Hex(0xFFB15C) : shots > par ? Themes.Hex(0xFFD2C2) : Color.white;
        }

        // ---------- Out of shots (OUT-27) ----------

        Card failCard;
        TextMeshProUGUI failLine;

        // Continue options on the out-of-shots card (OUT-31, OUT-32). A continue keeps the play going; a retry starts a
        // new one, and its button says what that takes (OUT-48).
        public System.Action ContinueWithAd, ContinueWithGems;
        Offer continueAd, continueGems, retryOffer;

        void BuildFail()
        {
            failCard = MakeCard("Out Of Shots", "OUT OF SHOTS", new Vector2(860f, 1080f), true);
            var c = failCard.Root;
            failLine = Txt(Rect("Line", c, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 300f), new Vector2(760f, 140f)),
                "", 44f, Ink, Ink, 0f, false);
            failLine.textWrappingMode = TextWrappingModes.Normal;
            continueAd = OfferButton(c, "+3 SHOTS", Palette.UiBlue, Vector2.zero, new Vector2(720f, 150f), 60f);
            continueAd.Press.Clicked = () => ContinueWithAd?.Invoke();
            continueGems = OfferButton(c, "+3 SHOTS", GemPurple, Vector2.zero, new Vector2(720f, 150f), 60f);
            continueGems.Press.Clicked = () => ContinueWithGems?.Invoke();
            retryOffer = OfferButton(c, "RETRY", Palette.UiGreen, new Vector2(90f, -310f), new Vector2(520f, 160f), 72f);
            retryOffer.Press.Clicked = () => Restart?.Invoke();
            IconButton(c, "Home", new Vector2(0.5f, 0.5f), new Vector2(-270f, -302f), 160f, Palette.UiBlue, "home", Color.white).Clicked = () => Home?.Invoke();
        }

        // shots: what a continue gives. adOk / gemsOk: whether each offer may be shown (caps, first-time experience).
        // adFree: the ad's reward comes without the video. retryEnergy: what a retry takes (0: nothing).
        public void ShowOutOfShots(int max, string line, int shots = 3, bool adOk = false, bool gemsOk = false, int gemCost = Catalog.ContinueCost,
            bool adFree = false, int retryEnergy = 0)
        {
            HideAll();
            failLine.text = $"All {max} shots used.\n{line}";
            continueAd.Root.gameObject.SetActive(adOk);
            continueGems.Root.gameObject.SetActive(gemsOk);
            continueAd.Label.text = continueGems.Label.text = $"+{shots} SHOTS";
            SetPrice(continueAd, adFree ? Price.Free : Price.Ad);
            SetPrice(continueGems, Price.Gems, gemCost);
            SetPrice(retryOffer, retryEnergy > 0 ? Price.Energy : Price.None, retryEnergy);
            StackOffers(adOk && gemsOk ? 205f : 130f, 24f, continueAd, continueGems);
            ShowCard(failCard);
        }

        // ---------- Paid undo (OUT-41) ----------

        public System.Action UndoWithAd, UndoWithGems, UndoDeclined;
        Card undoCard;
        Offer undoAd, undoGems, undoRestart;
        TextMeshProUGUI undoLine;
        RectTransform undoBadge;
        TextMeshProUGUI undoBadgeText;
        GameObject undoBadgeGem;
        int undoBadgeShown = -1;

        void BuildUndoOffer()
        {
            undoCard = MakeCard("Undo Offer", "NO FREE UNDOS", new Vector2(860f, 1000f), true);
            var c = undoCard.Root;
            undoLine = Txt(Rect("Line", c, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 290f), new Vector2(760f, 150f)), "", 42f, Ink, Ink, 0f, false);
            undoLine.textWrappingMode = TextWrappingModes.Normal;
            undoAd = OfferButton(c, "UNDO", Palette.UiBlue, Vector2.zero, new Vector2(720f, 150f), 60f);
            undoAd.Press.Clicked = () => UndoWithAd?.Invoke();
            undoGems = OfferButton(c, "UNDO", GemPurple, Vector2.zero, new Vector2(720f, 150f), 60f);
            undoGems.Press.Clicked = () => UndoWithGems?.Invoke();
            undoRestart = OfferButton(c, "RESTART", Palette.UiGreen, new Vector2(0f, -300f), new Vector2(560f, 140f), 58f);
            undoRestart.Press.Clicked = () => Restart?.Invoke();
            CloseButton(undoCard, () => UndoDeclined?.Invoke());
        }

        // restartEnergy: what starting over takes (OUT-48). When it takes a play, an undo is the cheaper way back, and
        // the card says so.
        public void ShowUndoOffer(bool adOk, int gemCost = UndoAllowance.GemCost, bool adFree = false, int restartEnergy = 0)
        {
            HideAll();
            undoLine.text = restartEnergy > 0 ? "Your free undos are used.\nAn undo keeps this play going." : "Your free undos are used.\nTime travel costs extra.";
            undoAd.Root.gameObject.SetActive(adOk);
            SetPrice(undoAd, adFree ? Price.Free : Price.Ad);
            SetPrice(undoGems, Price.Gems, gemCost);  // perks can make it cheaper (OUT-45)
            SetPrice(undoRestart, restartEnergy > 0 ? Price.Energy : Price.None, restartEnergy);
            StackOffers(adOk ? 190f : 115f, 24f, undoAd, undoGems);
            ShowCard(undoCard);
        }

        // The small count on the undo button: free undos left, or a gem once they cost. Hidden where undos are unlimited.
        public void SetUndoCount(int freeLeft, bool limited)
        {
            if (undoBadge == null) return;
            undoBadge.gameObject.SetActive(limited);
            if (!limited) return;
            undoBadgeText.text = freeLeft > 0 ? freeLeft.ToString() : "";
            if (undoBadgeGem != null) undoBadgeGem.SetActive(freeLeft == 0);
            undoBadge.GetComponent<Image>().color = freeLeft > 0 ? Palette.UiRed : Themes.Hex(0x8A5CF0);
            if (undoBadgeShown >= 0 && freeLeft != undoBadgeShown) Pop(undoBadge, 0f, 1.3f);
            undoBadgeShown = freeLeft;
        }

        // ---------- Hiring (OUT-45) ----------

        // A locked employee up close: who they are, their perk, and how they join. Gem hires have a price button;
        // the game spends the gems (false when short: it opens the shop, which comes back here).
        public System.Func<CosmeticItem, bool> Hire;
        Card hireCard;
        RawImage hirePortrait;
        TextMeshProUGUI hireName, hireTitle, hirePerkName, hirePerkText, hireNote, hirePrice;
        RectTransform hireBuy;
        CosmeticItem hiring;

        void BuildHire()
        {
            hireCard = MakeCard("Hire", "NEW HIRE", new Vector2(860f, 1200f), true);
            var c = hireCard.Root;
            var mid = new Vector2(0.5f, 0.5f);
            var frame = Rect("Portrait", c, mid, mid, new Vector2(0f, 300f), new Vector2(300f, 300f));
            Sliced(Img(frame, UiArt.Button, Color.white), 300f, 0.22f);
            hirePortrait = Rect("Icon", frame, mid, mid, new Vector2(0f, 6f), new Vector2(272f, 272f)).gameObject.AddComponent<RawImage>();
            hirePortrait.raycastTarget = false;
            hireName = Txt(Rect("Name", c, mid, mid, new Vector2(0f, 104f), new Vector2(760f, 80f)), "", 64f, Ink, Ink, 0f, false);
            hireTitle = Body(Rect("Title", c, mid, mid, new Vector2(0f, 44f), new Vector2(760f, 50f)), "", 32f, Ink, Ink);
            var perk = Rect("Perk", c, mid, mid, new Vector2(0f, -70f), new Vector2(760f, 130f));
            Sliced(Img(perk, UiArt.Button, Themes.Hex(0xFFF1C9)), 130f, 0.25f);
            hirePerkName = Txt(Rect("Perk Name", perk, mid, mid, new Vector2(0f, 26f), new Vector2(720f, 50f)), "", 40f, Themes.Hex(0x9C5A00), Ink, 0f, false);
            hirePerkText = Body(Rect("Perk Text", perk, mid, mid, new Vector2(0f, -22f), new Vector2(720f, 44f)), "", 30f, Ink, Ink);
            var buy = TextButton(c, "HIRE · 600", Themes.Hex(0x8A5CF0), new Vector2(0f, -270f), new Vector2(520f, 150f), 54f, null);
            buy.Clicked = HireClicked;
            hireBuy = buy.transform as RectTransform;
            hirePrice = buy.GetComponentInChildren<TextMeshProUGUI>();
            hirePrice.rectTransform.offsetMax = new Vector2(-90f, 0f);  // room for the gem
            var gemIcon = UiArt.Icon3D("gem");
            if (gemIcon != null) Img(Rect("Gem", hireBuy, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-70f, 8f), new Vector2(90f, 90f)), gemIcon, Color.white);
            hireNote = Body(Rect("Note", c, mid, mid, new Vector2(0f, -270f), new Vector2(760f, 80f)), "", 40f, Ink, Ink);
            CloseButton(hireCard, () => Back());
        }

        public void ShowHire(CosmeticItem item)
        {
            Remember();
            HideAll();
            hiring = item;
            hirePortrait.texture = IconStudio.ItemIcons.TryGetValue(item.Id, out var tex) ? tex : null;
            hirePortrait.color = tex != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            hireName.text = item.Name;
            hireTitle.text = $"{item.Title} · {Cosmetics.DepartmentName(item.Dept)}";
            var perk = StaffPerks.For(item);
            hirePerkName.text = perk.Name;
            hirePerkText.text = perk.Description;
            bool owned = OwnsItem?.Invoke(item) ?? false;
            bool buyable = item.ForGems && !owned;
            hireBuy.gameObject.SetActive(buyable);
            hirePrice.text = $"HIRE · {item.GemCost}";
            hireNote.gameObject.SetActive(!buyable);
            hireNote.text = owned ? "ON THE TEAM" : item.FromChest ? "FOUND IN CHESTS" : $"JOINS AT {item.StarsNeeded} STARS";
            if (hireCard.Title != null) hireCard.Title.text = item.ForGems ? "HIRE" : "NEW HIRE";
            ShowCard(hireCard);
        }

        void HireClicked()
        {
            var item = hiring;
            if (item == null || !(Hire?.Invoke(item) ?? false)) return;
            EquipItem?.Invoke(item);
            Back();  // to the wardrobe, where they now wear the new hire
        }

        // ---------- Back (OUT-45) ----------

        // Cards opened from cards come back in order: wardrobe, hire card, shop, and back again.
        readonly Stack<Card> backStack = new Stack<Card>();

        Card OpenCard()
        {
            foreach (var c in new[] { pauseCard, failCard, undoCard, hireCard, gadgetCard, guideCard, oddsCard, clipCard, achievementsCard, skinsCard, shopCard, energyCard, levelsCard, questsCard, settingsCard })
                if (c != null && c.Root.gameObject.activeSelf) return c;
            return null;
        }

        // ---------- Shop (OUT-31, OUT-32, OUT-33) ----------

        public System.Func<int> GemBalance;
        public System.Func<bool> StoreAvailable, StoreIsTest, PassActive, PassClaimReady, AdOfferReady, AdFree;
        public System.Func<int> PassDaysLeft;
        public System.Func<Product, string> PriceOf;
        public System.Func<Product, bool> CanBuyProduct;
        public System.Func<bool> PassFirst;
        public System.Action<Product> BuyProduct;
        public System.Action ClaimPass, WatchAdForGems, ShopOpened;
        public System.Action<bool> BuyChestWithGems;  // true: an Executive Safe, else a Supply Closet (OUT-14)

        Card shopCard;
        RectTransform shopContent, shopBadge;
        TextMeshProUGUI shopBalance, shopNote, shopPlate;
        Image shopPlateImage;

        void BuildShop()
        {
            shopCard = MakeCard("Shop", "SHOP", new Vector2(960f, 1600f), true);
            var c = shopCard.Root;
            var pill = Rect("Balance", c, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(320f, 84f));
            Sliced(Img(pill, UiArt.Pill, Themes.Hex(0x3B2A6E)), 84f, 0.5f);
            var gemIcon = UiArt.Icon3D("gem");
            if (gemIcon != null) Img(Rect("Gem", pill, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(40f, 4f), new Vector2(96f, 96f)), gemIcon, Color.white);
            shopBalance = Txt(Rect("Count", pill, Vector2.zero, Vector2.one, new Vector2(30f, 2f), Vector2.zero), "0", 52f, Color.white, Ink, 0.3f, false);
            shopNote = Body(Rect("Note", c, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -222f), new Vector2(860f, 44f)), "", 26f, Themes.Hex(0x8A7AA8), Ink);
            var view = Rect("Viewport", c, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 680f), new Vector2(900f, 1300f));
            view.gameObject.AddComponent<RectMask2D>();
            Img(view, UiArt.Pill, new Color(1f, 1f, 1f, 0.001f)).raycastTarget = true;
            shopContent = Rect("Content", view, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, 1300f));
            shopContent.pivot = new Vector2(0.5f, 1f);
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.content = shopContent;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 30f;
            CloseButton(shopCard, () => Back());
        }

        public void ShowShop()
        {
            Remember();
            HideAll();
            RefreshShop();
            ShowCard(shopCard);
            ShopOpened?.Invoke();
        }

        public void RefreshShop()
        {
            RefreshShopButton();
            if (shopContent == null) return;
            foreach (Transform t in shopContent) Destroy(t.gameObject);
            shopBalance.text = (GemBalance?.Invoke() ?? 0).ToString();
            bool store = StoreAvailable?.Invoke() ?? false, test = StoreIsTest?.Invoke() ?? false;
            shopNote.text = !store ? "Coming soon: purchases open with the store release." : test ? "TEST MODE: no real money is charged." : "";

            var rows = new List<System.Action<RectTransform>>();
            void PassRow(RectTransform r)
            {
                bool active = PassActive?.Invoke() ?? false, claim = PassClaimReady?.Invoke() ?? false;
                ShopRow(r, "shirt", Catalog.Pass.Title, active ? $"Active: {PassDaysLeft?.Invoke() ?? 0} days left. {Catalog.Pass.Blurb}" : Catalog.Pass.Blurb,
                    claim ? "CLAIM" : active ? "ACTIVE" : PriceOf?.Invoke(Catalog.Pass) ?? "", claim || (!active && store), Palette.Gold,
                    () => { if (claim) ClaimPass?.Invoke(); else BuyProduct?.Invoke(Catalog.Pass); });
            }
            void GemRows()
            {
                foreach (var p in Catalog.GemPacks)
                {
                    var pack = p;
                    rows.Add(r => ShopRow(r, "gem", pack.Title, pack.Blurb, PriceOf?.Invoke(pack) ?? "", store, Palette.UiGreen, () => BuyProduct?.Invoke(pack)));
                }
            }
            bool passFirst = PassFirst?.Invoke() ?? true;
            if (passFirst) rows.Add(PassRow);
            rows.Add(r =>
            {
                bool can = CanBuyProduct?.Invoke(Catalog.Starter) ?? false;
                bool paid = PaidChests?.Invoke() ?? true;
                string blurb = paid ? Catalog.Starter.Blurb : $"{Catalog.Starter.Gems + Chests.ClosetGems} gems. Once only.";
                ShopRow(r, "chest", Catalog.Starter.Title, blurb, can ? PriceOf?.Invoke(Catalog.Starter) ?? "" : "OWNED", can && store, Palette.UiGreen,
                    () => BuyProduct?.Invoke(Catalog.Starter), paid ? () => ShowOdds(ChestTier.SupplyCloset) : (System.Action)null);
            });
            // Energy (OUT-48): a play, a full bar, or a play for a video. With the pass there's nothing to buy.
            bool unlimited = EnergyUnlimited?.Invoke() ?? false;
            int gemsNow = GemBalance?.Invoke() ?? 0, points = EnergyPoints?.Invoke() ?? Energy.Max;
            rows.Add(r => ShopRow(r, "bolt", $"+{Energy.PlayCost} ENERGY", unlimited ? "Unlimited with your Office Pass." : "One more play, right now.",
                unlimited ? "ACTIVE" : $"{Catalog.PlayGems} GEMS", !unlimited && gemsNow >= Catalog.PlayGems, GemPurple, () => EnergyWithGems?.Invoke(false)));
            rows.Add(r => ShopRow(r, "bolt", "FULL ENERGY", points >= Energy.Max ? "Your bar is full." : "Fill the bar: six plays.",
                unlimited ? "ACTIVE" : $"{Catalog.RefillGems} GEMS", !unlimited && points < Energy.Max && gemsNow >= Catalog.RefillGems, GemPurple, () => EnergyWithGems?.Invoke(true)));
            rows.Add(r =>
            {
                bool ready = AdOfferReady?.Invoke() ?? false, free = AdFree?.Invoke() ?? false;
                ShopRow(r, "tv", "FREE ENERGY", free ? $"+{Catalog.AdEnergy} energy, no video needed." : $"Watch a short video: +{Catalog.AdEnergy} energy.",
                    unlimited ? "ACTIVE" : ready ? (free ? "TAKE" : "WATCH") : "LATER", ready && !unlimited, Palette.UiBlue, () => EnergyWithAd?.Invoke());
            });
            // Boosters (OUT-13): a box of every one unlocked so far.
            if (System.Array.Exists(Boosters.All, b => BoosterUnlocked?.Invoke(b.Kind) ?? false))
                rows.Add(r => ShopRow(r, "briefcase", "BOOSTER BOX", $"{Boosters.BoxEach} of every booster you've unlocked.", $"{Boosters.BoxGems} GEMS",
                    gemsNow >= Boosters.BoxGems, GemPurple, () => BuyBoosterBox?.Invoke()));
            if (!passFirst) GemRows();
            rows.Add(r =>
            {
                bool ready = AdOfferReady?.Invoke() ?? false, free = AdFree?.Invoke() ?? false;
                ShopRow(r, "tv", $"FREE {Catalog.AdGems} GEMS", free ? "Ad-free: yours without the video." : "Watch a short video.", ready ? (free ? "TAKE" : "WATCH") : "LATER",
                    ready, Palette.UiBlue, () => WatchAdForGems?.Invoke());
            });
            // Chests (OUT-14): two tiers for gems, each with its odds, where paid chests are allowed; and the Clip Shop.
            if (PaidChests?.Invoke() ?? true)
            {
                rows.Add(r => ShopRow(r, "chest", Chests.Name(ChestTier.SupplyCloset), Chests.Promise(ChestTier.SupplyCloset), $"{Chests.ClosetGems} GEMS",
                    gemsNow >= Chests.ClosetGems, GemPurple, () => BuyChestWithGems?.Invoke(false), () => ShowOdds(ChestTier.SupplyCloset)));
                rows.Add(r => ShopRow(r, "chest", Chests.Name(ChestTier.ExecutiveSafe), Chests.Promise(ChestTier.ExecutiveSafe), $"{Chests.SafeGems} GEMS",
                    gemsNow >= Chests.SafeGems, Themes.Hex(0x2B2738), () => BuyChestWithGems?.Invoke(true), () => ShowOdds(ChestTier.ExecutiveSafe)));
            }
            rows.Add(r => ShopRow(r, "clip", "CLIP SHOP", $"{ClipBalance?.Invoke() ?? 0} clips. Trade them for the item you want.", "OPEN", true, Palette.UiBlue, ShowClipShop));
            rows.Add(r =>
            {
                bool can = CanBuyProduct?.Invoke(Catalog.NoAds) ?? false;
                ShopRow(r, "tv", Catalog.NoAds.Title, Catalog.NoAds.Blurb, can ? PriceOf?.Invoke(Catalog.NoAds) ?? "" : "OWNED", can && store, Palette.UiGreen,
                    () => BuyProduct?.Invoke(Catalog.NoAds));
            });
            if (passFirst) GemRows();
            else rows.Add(PassRow);

            const float rowH = 190f, gap = 16f;
            for (int i = 0; i < rows.Count; i++)
            {
                var r = Rect("Row", shopContent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -rowH / 2f - i * (rowH + gap)), new Vector2(880f, rowH));
                rows[i](r);
            }
            shopContent.sizeDelta = new Vector2(0f, rows.Count * (rowH + gap));
        }

        // odds: shows "What's inside" for a row that sells a chest (OUT-14).
        void ShopRow(RectTransform r, string icon, string title, string blurb, string button, bool active, Color color, System.Action click, System.Action odds = null)
        {
            Sliced(Img(r, UiArt.Button, Inset), 190f, 0.2f);
            var art = UiArt.Icon3D(icon);
            Img(Rect("Icon", r, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(95f, 4f), new Vector2(140f, 140f)), art ?? UiArt.Icon(icon), art != null ? Color.white : Ink);
            var t = Txt(Rect("Title", r, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(380f, 38f), new Vector2(420f, 56f)), title, 42f, Ink, Ink, 0f, false);
            t.alignment = TextAlignmentOptions.MidlineLeft;
            t.enableAutoSizing = true;
            t.fontSizeMin = 26f;
            t.fontSizeMax = 42f;
            var b = Body(Rect("Blurb", r, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(380f, -28f), new Vector2(420f, 80f)), blurb, 26f, Ink, Ink);
            b.alignment = TextAlignmentOptions.TopLeft;
            b.textWrappingMode = TextWrappingModes.Normal;
            var btn = Rect("Button", r, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-125f, 0f), new Vector2(210f, 110f));
            var img = Img(btn, UiArt.Button, active ? color : Themes.Hex(0xCFC6DA));
            Sliced(img, 110f, 0.36f);
            img.raycastTarget = active;
            var label = Txt(Rect("Label", btn, Vector2.zero, Vector2.one, new Vector2(0f, 6f), new Vector2(-16f, 0f)), button, 40f, Color.white, Color.Lerp(img.color, Ink, 0.6f), 0.35f, false);
            label.enableAutoSizing = true;
            label.fontSizeMin = 22f;
            label.fontSizeMax = 40f;
            if (active) btn.gameObject.AddComponent<UiPress>().Clicked = click;
            if (odds != null)
            {
                var chip = Rect("Odds", r, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(95f, -64f), new Vector2(124f, 46f));
                var chipImage = Img(chip, UiArt.Pill, FrameDark);
                Sliced(chipImage, 46f, 0.5f);
                chipImage.raycastTarget = true;
                Txt(chip, "ODDS", 26f, Color.white, FrameDark, 0.2f, false);
                chip.gameObject.AddComponent<UiPress>().Clicked = odds;
            }
        }

        // The home button's gem count, and a badge when the pass reward is waiting.
        public void RefreshShopButton()
        {
            if (shopPlate == null) return;
            shopPlate.text = (GemBalance?.Invoke() ?? 0).ToString();
            bool claim = PassClaimReady?.Invoke() ?? false;
            shopBadge.gameObject.SetActive(claim);
            shopPlateImage.color = claim ? Palette.UiGreen : FrameDark;
        }

        // ---------- Test ad (the editor, and builds without a real ad network) ----------

        RectTransform testAd, testAdFill;
        TextMeshProUGUI testAdCount, testAdReward;

        // A full-screen stand-in for a rewarded video: what it pays, a 3 s progress bar, then the reward (OUT-48).
        // SKIP gives nothing.
        public void ShowTestAd(string reward, System.Action<bool> done)
        {
            if (testAd == null)
            {
                var c = new Vector2(0.5f, 0.5f);
                testAd = Rect("Test Ad", transform as RectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
                Img(testAd, Shapes.Square, new Color(0.05f, 0.04f, 0.12f, 0.97f)).raycastTarget = true;
                var rays = Rect("Rays", testAd, c, c, new Vector2(0f, 340f), new Vector2(820f, 820f));
                Img(rays, UiArt.Sunburst, new Color(0.35f, 0.55f, 1f, 0.35f));
                rays.gameObject.AddComponent<Spin>().Speed = 10f;
                Img(Rect("TV", testAd, c, c, new Vector2(0f, 340f), new Vector2(380f, 380f)), UiArt.Icon3D("tv") ?? UiArt.Icon("play"), Color.white);
                Txt(Rect("Kind", testAd, c, c, new Vector2(0f, 100f), new Vector2(900f, 60f)), "REWARDED VIDEO · TEST", 40f, Trim, Ink, 0.3f, false);
                testAdReward = Txt(Rect("Reward", testAd, c, c, new Vector2(0f, -10f), new Vector2(940f, 130f)), "", 96f, Color.white, Ink, 0.45f, true);
                testAdReward.enableAutoSizing = true;
                testAdReward.fontSizeMin = 50f;
                testAdReward.fontSizeMax = 96f;
                var track = Rect("Track", testAd, c, c, new Vector2(0f, -170f), new Vector2(720f, 46f));
                Sliced(Img(track, UiArt.Pill, new Color(1f, 1f, 1f, 0.14f)), 46f, 0.5f);
                testAdFill = Rect("Fill", track, Vector2.zero, new Vector2(0.06f, 1f), Vector2.zero, Vector2.zero);
                Sliced(Img(testAdFill, UiArt.Pill, Palette.Gold), 46f, 0.5f);
                testAdCount = Body(Rect("Count", testAd, c, c, new Vector2(0f, -250f), new Vector2(720f, 60f)), "", 34f, new Color(0.8f, 0.78f, 0.92f), Ink);
            }
            foreach (Transform child in testAd) if (child.name == "Skip") Destroy(child.gameObject);
            var skip = TextButton(testAd, "SKIP: NO REWARD", Themes.Hex(0x6E6488), new Vector2(0f, -430f), new Vector2(460f, 120f), 44f, null);
            skip.name = "Skip";
            bool finished = false;
            void End(bool watched)
            {
                if (finished) return;
                finished = true;
                Tweens.Kill(testAd);
                testAd.gameObject.SetActive(false);
                done?.Invoke(watched);
            }
            skip.Clicked = () => End(false);
            testAdReward.text = reward;
            testAd.gameObject.SetActive(true);
            testAd.SetAsLastSibling();
            Tweens.Run(3f, k =>
            {
                testAdFill.anchorMax = new Vector2(Mathf.Max(0.06f, k), 1f);
                testAdCount.text = $"Your reward in {Mathf.Max(1, Mathf.CeilToInt(3f * (1f - k) - 0.001f))}...";
            }, Tweens.Linear, 0f, () => End(true), testAd);
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
            OwnCanvas(welcomePortrait, true);  // it bobs every frame
            welcomeIcon = welcomePortrait.gameObject.AddComponent<RawImage>();
            welcomeIcon.raycastTarget = false;
            heroBobber = welcomePortrait.gameObject.AddComponent<Bobber>();
            heroBobber.Hud = this;
            IconStudio.Rendered += RefreshWelcomeIcon;
            // Poke the employee (OUT-26): a squish and a giggle; five quick pokes and they spin round. The hit area is
            // the body only, so it never covers the side buttons.
            var poke = Rect("Poke", welcomePortrait, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -20f), new Vector2(380f, 520f));
            Img(poke, Shapes.Square, new Color(1f, 1f, 1f, 0f)).raycastTarget = true;
            poke.gameObject.AddComponent<UiPress>().Clicked = PokeHero;

            // The logo, above the stage.
            var logo = Rect("Logo", area, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -400f), new Vector2(900f, 400f));
            OwnCanvas(logo, true);  // the letters wave every frame
            logo.localScale = Vector3.one * 0.6f;
            logo.localRotation = Quaternion.Euler(0f, 0f, 3f);
            AddWord(logo, LogoWords[0], new Vector2(-14f, 92f), Color.white, Themes.Hex(0xFFE9C9));
            AddWord(logo, LogoWords[1], new Vector2(22f, -96f), Themes.Hex(0xFFE45C), Themes.Hex(0xFF8A2E));

            BuildHeader(area);

            // Side events: the free daily gift (left) and the next outfit unlock (right).
            var gift = SideButton(area, "Gift", new Vector2(0f, 1f), new Vector2(125f, -720f), Themes.Hex(0xFFB22E), "chest", out giftPlate, out giftPlateImage);
            gift.Clicked = () => ShowQuests();
            giftBadge = Badge(gift.transform as RectTransform, "!");
            // The coffee punch card, under the gift: every new coffee is a stamp, a full card opens a chest.
            var card = SideButton(area, "Coffee Card", new Vector2(0f, 1f), new Vector2(125f, -960f), Themes.Hex(0xC9784A), "chest", out coffeeCardPlate, out coffeeCardPlateImage);
            var cardIcon = card.transform.Find("Icon").GetComponent<Image>();
            cardIcon.sprite = UiArt.Icon3D("coffee") ?? UiArt.Cup;
            if (UiArt.Icon3D("coffee") == null) cardIcon.rectTransform.sizeDelta *= 1.25f;
            card.Clicked = () =>
            {
                if (CoffeeCardReady?.Invoke() ?? false) ClaimCoffeeCard?.Invoke();
                else ShowLevels();  // go and find more coffees
            };
            coffeeCardBadge = Badge(card.transform as RectTransform, "!");
            // The shop (OUT-32), under the next outfit: the gem balance, and a badge when the Office Pass reward waits.
            var shop = SideButton(area, "Shop", new Vector2(1f, 1f), new Vector2(-125f, -960f), Themes.Hex(0x8A5CF0), "gem", out shopPlate, out shopPlateImage);
            shop.Clicked = () => ShowShop();
            shopBadge = Badge(shop.transform as RectTransform, "!");
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
            BuildPlayChip(pr);

            BuildTabs(area);
        }

        // A sky in the chapter color, slow sun rays, drifting clouds, a pale office skyline, and a warm floor.
        void BuildBackdrop(RectTransform area)
        {
            var back = Rect("Backdrop", area, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, 1200f));
            var rays = Rect("Rays", back, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(2000f, 2000f));
            Img(rays, UiArt.Sunburst, new Color(1f, 1f, 1f, 0.22f));
            rays.gameObject.AddComponent<Spin>().Speed = 4f;
            OwnCanvas(rays);
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
                OwnCanvas(cloud);
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

            // Stars: the count, which opens the rooms. It gave its + to the energy meter beside it (OUT-48).
            var stars = Rect("Stars", header, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(165f, row), new Vector2(210f, 92f));
            var starsImage = Img(stars, UiArt.Pill, FrameDark);
            Sliced(starsImage, 92f, 0.5f);
            starsImage.raycastTarget = true;
            stars.gameObject.AddComponent<UiPress>().Clicked = () => ShowLevels();
            Img(Rect("Icon", stars, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 4f), new Vector2(124f, 124f)), UiArt.Star, Color.white);
            totalStars = Txt(Rect("Count", stars, Vector2.zero, Vector2.one, new Vector2(30f, 2f), new Vector2(-88f, 0f)), "0", 58f, Color.white, FrameDark, 0.3f, true);

            // Energy (OUT-48) between the stars and the chapter: what plays are left, and when the next point comes.
            BuildEnergyMeter(header, new Vector2(440f, row), 230f);

            var bar = Rect("Chapter", header, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-290f, row), new Vector2(440f, 92f));
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
            var chestIcon = Img(chest, UiArt.Icon3D("chest") ?? UiArt.Icon("chest"), ChapterChestGold);
            if (UiArt.Icon3D("chest") == null)
            {
                chestIcon.gameObject.AddComponent<Outline>().effectColor = FrameDark;
                chestIcon.GetComponent<Outline>().effectDistance = new Vector2(4f, -4f);
            }
            // The chest at the end of the bar is the reward for every star in the chapter. Until then it opens the
            // level list like the rest of the bar.
            chestIcon.raycastTarget = true;
            chapterChestIcon = chestIcon;
            chest.gameObject.AddComponent<UiPress>().Clicked = () =>
            {
                if (chapterChestReady) ClaimChapterChest?.Invoke(chapterChestChapter);
                else ShowLevels();
            };
            chapterChestBadge = Badge(chest, "!");
            chapterChestShake = chest.gameObject.AddComponent<Rattle>();
            chapterChestShake.enabled = false;
            var plate = Rect("Name", header, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-300f, -34f), new Vector2(380f, 64f));
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
            var wardrobe = Tab(area, "WARDROBE", "shirt", Themes.Hex(0xFF5FA2), -216f, y);
            wardrobe.Clicked = () => ShowSkins();
            wardrobeButton = wardrobe.transform as RectTransform;  // the tutorial points here (OUT-11)
            var tasks = Tab(area, "TASKS", "gift", Themes.Hex(0xFF9F2E), 216f, y);
            tasksButton = tasks.transform as RectTransform;
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
            // The 3D render (OUT-30) fills more of the button and carries its own shading; the flat icon gets a drop shadow.
            var icon3D = UiArt.Icon3D(icon);
            var ic = Img(Rect("Icon", r, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, size * (icon3D != null ? 0.04f : 0.06f)),
                Vector2.one * size * (icon3D != null ? 0.8f : 0.5f)), icon3D ?? UiArt.Icon(icon), Color.white);
            if (icon3D == null)
            {
                var sh = ic.gameObject.AddComponent<Shadow>();
                sh.effectColor = new Color(0.1f, 0.03f, 0.2f, 0.45f);
                sh.effectDistance = new Vector2(0f, -4f);
            }
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
            IconStudio.Rendered -= RefreshClipIcons;
        }

        void RefreshWelcomeIcon()
        {
            if (welcomeIcon == null) return;
            var icon = IconStudio.HeroIcon;
            if (welcomeIcon.texture == icon) return;
            welcomeIcon.texture = icon;
            welcomeIcon.color = icon != null ? Color.white : new Color(1f, 1f, 1f, 0f);
        }

        public System.Action<bool> HeroPoked;  // true on the fifth quick poke
        int pokes;
        float lastPoke = -10f;

        void PokeHero()
        {
            pokes = Time.unscaledTime - lastPoke < 0.8f ? pokes + 1 : 1;
            lastPoke = Time.unscaledTime;
            bool spin = pokes >= 5;
            if (spin) pokes = 0;
            HeroPoked?.Invoke(spin);
            if (heroBobber == null || ReducedMotion) return;
            var b = heroBobber;
            Tweens.Kill(b);
            if (spin) Tweens.Run(0.75f, k => b.ExtraSpin = -360f * k, Tweens.OutCubic, 0f, () => b.ExtraSpin = 0f, b);
            else Tweens.Run(0.45f, k => b.ExtraSquish = 0.13f * Mathf.Sin(k * Mathf.PI * 2f) * (1f - k), Tweens.Linear, 0f, () => b.ExtraSquish = 0f, b);
        }

        Bobber heroBobber;

        public System.Func<int> CoffeeCardStamps;
        public System.Func<bool> CoffeeCardReady;
        public System.Action ClaimCoffeeCard;
        TextMeshProUGUI coffeeCardPlate;
        Image coffeeCardPlateImage;
        RectTransform coffeeCardBadge;

        public void RefreshCoffeeCard()
        {
            if (coffeeCardPlate == null) return;
            bool ready = CoffeeCardReady?.Invoke() ?? false;
            coffeeCardPlate.text = ready ? "FREE!" : $"{CoffeeCardStamps?.Invoke() ?? 0}/{Progress.StampsPerCard}";
            coffeeCardPlateImage.color = ready ? Palette.UiGreen : FrameDark;
            coffeeCardBadge.gameObject.SetActive(ready);
        }

        public System.Func<int, bool> ChapterChestClaimed;
        public System.Action<int> ClaimChapterChest;
        static Color ChapterChestGold => UiArt.Icon3D("chest") != null ? Color.white : Themes.Hex(0xFFC23A);  // the 3D chest has its own colors
        Image chapterChestIcon;
        RectTransform chapterChestBadge;
        Rattle chapterChestShake;
        int chapterChestChapter;
        bool chapterChestFull, chapterChestReady;

        // Ready: rattles with a badge. Claimed: faded. Otherwise: shown as the prize to aim for.
        public void RefreshChapterChest()
        {
            if (chapterChestIcon == null) return;
            bool claimed = ChapterChestClaimed?.Invoke(chapterChestChapter) ?? false;
            chapterChestReady = chapterChestFull && !claimed;
            chapterChestIcon.color = claimed ? new Color(0.75f, 0.7f, 0.8f, 0.55f) : ChapterChestGold;
            chapterChestBadge.gameObject.SetActive(chapterChestReady);
            chapterChestShake.enabled = chapterChestReady;
            if (!chapterChestReady) chapterChestShake.transform.localRotation = Quaternion.identity;
        }

        // A quick shake every couple of seconds, like something inside wants out.
        class Rattle : MonoBehaviour
        {
            void Update()
            {
                float t = Time.unscaledTime % 2.2f;
                float angle = Calm || t > 0.5f ? 0f : Mathf.Sin(t * 40f) * 9f * (1f - t / 0.5f);
                transform.localRotation = Quaternion.Euler(0f, 0f, angle);
            }
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
            chapterChestChapter = c;
            chapterChestFull = got >= max;
            RefreshChapterChest();
            RefreshCoffeeCard();
            RefreshShopButton();
            RefreshEnergyMeter();

            bool ready = DailyChestReady?.Invoke() ?? false;
            var left = UntilQuestReset?.Invoke() ?? System.TimeSpan.Zero;
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
            public float ExtraSpin, ExtraSquish;  // from a poke (OUT-26)
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
                r.localRotation = Quaternion.Euler(0f, 0f, 4f * Mathf.Sin(t * 1.7f) + ExtraSpin);
                float squash = 0.03f * Mathf.Sin(t * 4.8f);
                r.localScale = new Vector3(1f + squash + ExtraSquish, 1f - squash - ExtraSquish * 1.15f, 1f);
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
            backStack.Clear();
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

        // The home screen is opaque, and the wardrobe and the shop leave only a dimmed sliver: the board under them need
        // not be drawn.
        public bool CoversBoard => (title != null && title.gameObject.activeSelf) || (skinsCard != null && skinsCard.Root.gameObject.activeSelf)
            || (shopCard != null && shopCard.Root.gameObject.activeSelf);

        public void HideTitle()
        {
            backStack.Clear();  // a room starts: nothing to go back to
            title.gameObject.SetActive(false);
            hudRoot.gameObject.SetActive(true);
        }

        // ---------- Pause and settings ----------

        void BuildPause()
        {
            pauseCard = MakeCard("Pause", "PAUSED", new Vector2(860f, 1260f), true);
            var c = pauseCard.Root;
            TextButton(c, "RESUME", Palette.UiGreen, new Vector2(0f, 400f), new Vector2(620f, 170f), 70f, "play").Clicked = () => Resume?.Invoke();
            RoundAction(c, "RESTART", "restart", Palette.UiRed, new Vector2(-282f, 170f), Ink).Clicked = () => Restart?.Invoke();
            RoundAction(c, "HOME", "home", Palette.UiBlue, new Vector2(-94f, 170f), Ink).Clicked = () => Home?.Invoke();
            RoundAction(c, "ROOMS", "grid", Palette.UiWood, new Vector2(94f, 170f), Ink).Clicked = () => ShowLevels();
            RoundAction(c, "GUIDE", "wetfloor", Themes.Hex(0xFFB62E), new Vector2(282f, 170f), Ink).Clicked = () => ShowGuide();  // the field guide (OUT-11)
            pauseNote = Body(Rect("Note", c, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 282f), new Vector2(760f, 50f)),
                $"Restarting or leaving ends this play: {Energy.PlayCost} energy to start again.", 26f, Themes.Hex(0x8A6A9E), Ink);
            pauseNote.gameObject.SetActive(false);
            SettingRows(c, -60f);
            devRow = Rect("Dev", c, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -520f), new Vector2(700f, 110f));
            TextButton(devRow, "LAB", Palette.UiPurple, new Vector2(-160f, 0f), new Vector2(280f, 100f), 44f, null).Clicked = () => OpenLab?.Invoke();
            TextButton(devRow, "DEBUG", Palette.UiPurple, new Vector2(160f, 0f), new Vector2(280f, 100f), 44f, null).Clicked = () => ToggleDebug?.Invoke();
            SetDevVisible(Application.isEditor || Debug.isDebugBuild);
        }

        void BuildSettings() => BuildSettingsCard();  // sliders, haptics, graphics and more (OUT-16, Hud.Settings.cs)

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
            Calm = UiPress.Calm = reduced;
            SetSwitch("sfx", sfx);
            SetSwitch("music", music);
            SetSwitch("motion", reduced);
        }

        // playAtStake: a shot has been taken in a play, so restarting or leaving now ends it (OUT-48).
        public void ShowPause(bool playAtStake = false)
        {
            HideAll();
            pauseNote.gameObject.SetActive(playAtStake);
            ShowCard(pauseCard);
        }

        public void ShowSettings()
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
        TextMeshProUGUI wardrobePerk;
        readonly List<(Slot slot, Image bg, TextMeshProUGUI label)> wardrobeTabs = new List<(Slot, Image, TextMeshProUGUI)>();
        RectTransform wardrobeContent;

        void BuildSkins()
        {
            skinsCard = MakeCard("Wardrobe", "WARDROBE", new Vector2(980f, 1560f), true);
            var c = skinsCard.Root;
            // Live preview: Puffy in the current outfit on a sunburst disc.
            var disc = Rect("Disc", c, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -365f), new Vector2(430f, 430f));
            Img(disc, Shapes.Circle, Themes.Hex(0xE9EEF6));
            var prev = Rect("Preview", c, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -350f), new Vector2(470f, 470f));  // big enough to see a hat or a face item (OUT-29)
            OwnCanvas(prev);  // it bobs every frame
            wardrobePreview = prev.gameObject.AddComponent<RawImage>();
            wardrobePreview.raycastTarget = false;
            prev.gameObject.AddComponent<Bobber>().Hud = this;
            // What the worn employee brings (OUT-45), on a plate over the foot of the stage.
            var plate = Rect("Perk", c, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -556f), new Vector2(760f, 54f));
            Sliced(Img(plate, UiArt.Pill, new Color(0.23f, 0.16f, 0.43f, 0.92f)), 54f, 0.5f);
            wardrobePerk = Body(Rect("Text", plate, Vector2.zero, Vector2.one, new Vector2(0f, 1f), new Vector2(-30f, 0f)), "", 27f, Color.white, Ink);

            // Tabs.
            var tabs = new[] { (Slot.Employee, "STAFF"), (Slot.Color, "TIES"), (Slot.Hat, "HATS"), (Slot.Face, "FACE") };
            for (int i = 0; i < tabs.Length; i++)
            {
                var (slot, label) = tabs[i];
                var r = Rect(label, c, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2((i - 1.5f) * 222f, -634f), new Vector2(206f, 96f));
                var bg = Img(r, UiArt.Button, Inset);
                Sliced(bg, 96f, 0.4f);
                bg.raycastTarget = true;
                var t = Txt(Rect("Label", r, Vector2.zero, Vector2.one, new Vector2(0f, 6f), Vector2.zero), label, 38f, Ink, Ink, 0f, false);
                wardrobeTabs.Add((slot, bg, t));
                r.gameObject.AddComponent<UiPress>().Clicked = () => { wardrobeTab = slot; RefreshSkins(); };
            }

            // Scrolling grid of items.
            var view = Rect("Viewport", c, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 440f), new Vector2(900f, 800f));
            view.gameObject.AddComponent<RectMask2D>();
            Img(view, UiArt.Pill, new Color(1f, 1f, 1f, 0.001f)).raycastTarget = true;
            wardrobeContent = Rect("Content", view, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, 800f));
            wardrobeContent.pivot = new Vector2(0.5f, 1f);
            OwnCanvas(wardrobeContent, true);  // scrolling moves only this canvas
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.content = wardrobeContent;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 30f;
            CloseButton(skinsCard, () => Back());
            IconStudio.Rendered += RefreshWardrobeIcons;
        }

        public void ShowSkins(Slot tab)
        {
            wardrobeTab = tab;
            ShowSkins();
        }

        public void ShowSkins()
        {
            Remember();
            HideAll();
            RefreshSkins();
            ShowCard(skinsCard);
            IconStudio.WardrobeOpen = true;  // its icons render first, and faster
            Opened?.Invoke("wardrobe");
        }

        // Wardrobe tiles are built once per tab and afterwards only repainted (OUT-36): an equip or a tab switch no
        // longer rebuilds every tile. A tab is rebuilt only when what the player owns in it changes. Icons come with
        // their rounded corners already cut (IconStudio), so tiles need no stencil mask.
        class SkinTile
        {
            public CosmeticItem Item;
            public bool Owned;
            public Image Bg;
            public RawImage Icon;
            public TextMeshProUGUI Label;
            public TextMeshProUGUI Sub;  // staff: the job title
            public GameObject Check;
        }

        readonly Dictionary<Slot, (RectTransform root, List<SkinTile> tiles)> skinTabs = new Dictionary<Slot, (RectTransform, List<SkinTile>)>();

        void RefreshWardrobeIcons()
        {
            if (wardrobePreview != null && wardrobePreview.texture != IconStudio.HeroIcon) wardrobePreview.texture = IconStudio.HeroIcon;
            foreach (var tab in skinTabs.Values)
                foreach (var t in tab.tiles)
                    if (IconStudio.ItemIcons.TryGetValue(t.Item.Id, out var tex) && t.Icon.texture != tex)
                    {
                        t.Icon.texture = tex;
                        t.Icon.color = t.Owned ? Color.white : new Color(0.5f, 0.5f, 0.56f, 1f);
                    }
        }

        void RefreshSkins(bool tabChanged = true)
        {
            foreach (var (slot, bg, label) in wardrobeTabs)
            {
                bool on = slot == wardrobeTab;
                bg.color = on ? theme.Accent : Inset;
                label.color = on ? Color.white : Ink;
            }
            IconStudio.ShowingTab = wardrobeTab;
            if (wardrobePerk != null)
            {
                var perk = StaffPerks.For((CurrentOutfit?.Invoke() ?? Cosmetics.Default).Employee);
                wardrobePerk.text = perk.Name.Length > 0 ? $"<b>{perk.Name}</b> · {perk.Description}" : "";
            }
            if (!skinTabs.TryGetValue(wardrobeTab, out var tab) || tab.root == null || OwnershipChanged(tab.tiles)) tab = BuildSkinTab(wardrobeTab);
            foreach (var kv in skinTabs) kv.Value.root.gameObject.SetActive(kv.Key == wardrobeTab);
            string worn = (CurrentOutfit?.Invoke() ?? Cosmetics.Default).Get(wardrobeTab);
            foreach (var t in tab.tiles) PaintTile(t, t.Item.Id == worn);
            if (wardrobeTab == Slot.Employee) RefreshStaffHeaders();
            wardrobeContent.sizeDelta = new Vector2(0f, tab.root.sizeDelta.y);
            if (tabChanged) wardrobeContent.anchoredPosition = Vector2.zero;
            RefreshWardrobeIcons();
        }

        bool OwnershipChanged(List<SkinTile> tiles)
        {
            foreach (var t in tiles)
                if (t.Owned != (OwnsItem?.Invoke(t.Item) ?? false)) return true;
            return false;
        }

        void PaintTile(SkinTile t, bool equipped)
        {
            t.Bg.color = equipped ? theme.Accent : t.Owned ? Inset : Themes.Hex(0xE2DAEA);
            t.Label.color = equipped ? Color.white : Ink;
            if (t.Sub != null) t.Sub.color = equipped ? new Color(1f, 1f, 1f, 0.85f) : new Color(Ink.r, Ink.g, Ink.b, 0.7f);
            t.Check.SetActive(equipped);
        }

        const float SkinTileW = 210f, SkinTileH = 270f, StaffTileH = 300f, StaffHeaderH = 84f;

        // The staff tab's department headers (OUT-43), with how many of each department you have.
        readonly List<(Department dept, TextMeshProUGUI count)> staffHeaders = new List<(Department, TextMeshProUGUI)>();

        // Tiles of a big tab still to build: a dozen are built at once, the rest a few a frame (OUT-36, OUT-43).
        readonly List<(RectTransform root, CosmeticItem item, Vector2 pos, float height, List<SkinTile> into)> pendingTiles =
            new List<(RectTransform, CosmeticItem, Vector2, float, List<SkinTile>)>();
        const int TilesAtOnce = 12, TilesPerFrame = 3;

        // Builds every tile still waiting, at once (review shots, where Update does not run).
        public void FinishWardrobeTiles()
        {
            while (pendingTiles.Count > 0)
            {
                int before = pendingTiles.Count;
                BuildPendingTiles();
                if (pendingTiles.Count == before) break;  // the rest belong to tabs not on screen
            }
        }

        void BuildPendingTiles()
        {
            if (pendingTiles.Count == 0) return;
            pendingTiles.RemoveAll(p => p.root == null);  // tabs rebuilt since
            // Only the tab on screen builds; the others wait until they are opened.
            string worn = (CurrentOutfit?.Invoke() ?? Cosmetics.Default).Get(wardrobeTab);
            int built = 0;
            for (int i = 0; i < pendingTiles.Count && built < TilesPerFrame; )
            {
                var (r, item, pos, height, into) = pendingTiles[i];
                if (!r.gameObject.activeInHierarchy) { i++; continue; }
                pendingTiles.RemoveAt(i);
                var t = SkinTileFor(r, item, pos, height);
                into.Add(t);
                PaintTile(t, item.Id == worn);
                built++;
            }
            if (built > 0) RefreshWardrobeIcons();
        }

        (RectTransform root, List<SkinTile> tiles) BuildSkinTab(Slot slot)
        {
            var watch = FrameWatch.Active ? System.Diagnostics.Stopwatch.StartNew() : null;
            if (skinTabs.TryGetValue(slot, out var old) && old.root != null)
            {
                old.root.gameObject.SetActive(false);
                Destroy(old.root.gameObject);
            }
            var items = Cosmetics.InSlot(slot);
            bool staff = slot == Slot.Employee;
            float tileH = staff ? StaffTileH : SkinTileH;
            var root = Rect("Tab " + slot, wardrobeContent, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            root.pivot = new Vector2(0.5f, 1f);
            var tiles = new List<SkinTile>(items.Count);
            if (staff) staffHeaders.Clear();

            // Lay the tab out: four tiles a row, and on the staff tab a header over each department (they come sorted).
            float y = -10f;
            int col = 0;
            Department? dept = null;
            foreach (var item in items)
            {
                if (staff && item.Dept != dept)
                {
                    if (col != 0) { y -= tileH + 16f; col = 0; }
                    dept = item.Dept;
                    staffHeaders.Add((item.Dept, StaffHeader(root, item.Dept, y)));
                    y -= StaffHeaderH;
                }
                var pos = new Vector2((col - 1.5f) * (SkinTileW + 14f), y - tileH / 2f);
                if (tiles.Count < TilesAtOnce) tiles.Add(SkinTileFor(root, item, pos, tileH));
                else pendingTiles.Add((root, item, pos, tileH, tiles));
                if (++col == 4) { col = 0; y -= tileH + 16f; }
            }
            if (col != 0) y -= tileH + 16f;
            root.sizeDelta = new Vector2(0f, -y + 10f);
            skinTabs[slot] = (root, tiles);
            if (watch != null) FrameWatch.Note($"{tiles.Count} tiles {watch.Elapsed.TotalMilliseconds:0}ms");
            return (root, tiles);
        }

        static Color DepartmentColor(Department d) =>
            d == Department.Interns ? Themes.Hex(0xFF8A3D) : d == Department.Employees ? Themes.Hex(0x5B8DEF) : d == Department.Engineering ? Themes.Hex(0x2F6FE0)
            : d == Department.HR ? Themes.Hex(0xE86A9A) : d == Department.Sales ? Themes.Hex(0x3DAE6B) : d == Department.Finance ? Themes.Hex(0x7E8A3A)
            : d == Department.Management ? Themes.Hex(0x4A5A86) : d == Department.CLevel ? Themes.Hex(0x34344A) : Themes.Hex(0xD9A21E);

        TextMeshProUGUI StaffHeader(RectTransform root, Department dept, float y)
        {
            var bar = Rect("Header " + dept, root, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y - StaffHeaderH / 2f + 4f), new Vector2(880f, 64f));
            Sliced(Img(bar, UiArt.Pill, DepartmentColor(dept)), 64f, 0.5f);
            var name = Txt(Rect("Name", bar, Vector2.zero, Vector2.one, new Vector2(30f, 2f), new Vector2(-60f, 0f)), Cosmetics.DepartmentName(dept), 38f, Color.white, Ink, 0.3f, false);
            name.alignment = TextAlignmentOptions.MidlineLeft;
            var count = Txt(Rect("Count", bar, Vector2.zero, Vector2.one, new Vector2(-30f, 2f), new Vector2(-60f, 0f)), "", 34f, Color.white, Ink, 0.3f, false);
            count.alignment = TextAlignmentOptions.MidlineRight;
            return count;
        }

        void RefreshStaffHeaders()
        {
            foreach (var (dept, count) in staffHeaders)
            {
                if (count == null) continue;
                int have = 0, all = 0;
                foreach (var item in Cosmetics.All)
                    if (item.Slot == Slot.Employee && item.Dept == dept)
                    {
                        all++;
                        if (OwnsItem?.Invoke(item) ?? false) have++;
                    }
                count.text = $"{have}/{all}";
            }
        }

        SkinTile SkinTileFor(RectTransform parent, CosmeticItem item, Vector2 pos, float tileH)
        {
            const float tileW = SkinTileW;
            bool owned = OwnsItem?.Invoke(item) ?? false;
            bool staff = item.Slot == Slot.Employee;
            var tile = Rect(item.Id, parent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), pos, new Vector2(tileW, tileH));
            var t = new SkinTile { Item = item, Owned = owned };
            t.Bg = Img(tile, UiArt.Button, Inset);
            Sliced(t.Bg, tileH, 0.18f);
            t.Bg.raycastTarget = true;
            // Rarity stripe along the top.
            var stripe = Img(Rect("Rarity", tile, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -16f), new Vector2(tileW - 40f, 12f)), UiArt.Pill, Cosmetics.RarityColor(item.Rarity));
            Sliced(stripe, 12f, 0.5f);

            var frame = Rect("Portrait", tile, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -112f), new Vector2(184f, 184f));
            Sliced(Img(frame, UiArt.Button, Color.white), 184f, 0.22f);
            // A rounded square, cut into the icon itself: no hat or chin cut off by a circle.
            t.Icon = Rect("Icon", frame, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(166f, 166f)).gameObject.AddComponent<RawImage>();
            t.Icon.raycastTarget = false;
            t.Icon.color = item.Slot == Slot.Color ? item.Body : new Color(1f, 1f, 1f, 0.2f);  // until the icon is drawn

            // Staff show their job title under the name (OUT-43), even before they join.
            if (staff && !string.IsNullOrEmpty(item.Title))
            {
                t.Sub = Body(Rect("Title", tile, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(tileW - 10f, 30f)), item.Title, 18f, Ink, Ink);
                t.Sub.fontSize = Mathf.Clamp((tileW - 26f) / (0.68f * item.Title.Length), 12f, 19f);
            }
            float captionY = staff ? 62f : 46f;
            string caption = owned ? item.Name : item.ForGems ? item.GemCost.ToString() : item.FromChest ? "CHEST" : item.StarsNeeded.ToString();
            t.Label = Body(Rect("Name", tile, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(item.FromChest || owned ? 0f : 16f, captionY), new Vector2(tileW - 10f, 50f)),
                caption, 30f, Ink, Ink);
            // Long names shrink to fit the tile (OUT-29), by their length: TMP's auto-size runs several layouts per label,
            // which made opening a tab freeze on a budget phone (OUT-36). Caps in this face run about 0.6 em wide.
            t.Label.fontSize = Mathf.Clamp((tileW - 22f) / (0.62f * Mathf.Max(1, caption.Length)), 15f, 30f);

            var check = Rect("Check", tile, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -16f), new Vector2(58f, 58f));
            Img(check, UiArt.RoundButton, Palette.UiGreen);
            Img(Rect("CheckIcon", check, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 3f), new Vector2(36f, 36f)), UiArt.Icon("check"), Color.white);
            t.Check = check.gameObject;
            if (!owned)
            {
                var gem = UiArt.Icon3D("gem");
                var lockIcon = item.ForGems && gem != null ? gem : UiArt.Icon(item.FromChest ? "chest" : "lock");
                Img(Rect("Lock", frame, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(item.ForGems ? 84f : 60f, item.ForGems ? 84f : 60f)), lockIcon, Color.white)
                    .gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.35f);
                if (!item.FromChest)
                    Img(Rect("Star", tile, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(-34f, captionY + 2f), new Vector2(item.ForGems ? 50f : 40f, item.ForGems ? 50f : 40f)),
                        item.ForGems && gem != null ? gem : UiArt.Star, Color.white);
                // Staff can be looked at before they join: who they are, their perk, and how to hire them (OUT-45).
                if (staff) tile.gameObject.AddComponent<UiPress>().Clicked = () => ShowHire(item);
                return t;
            }
            tile.gameObject.AddComponent<UiPress>().Clicked = () =>
            {
                EquipItem?.Invoke(item);
                RefreshSkins(false);
            };
            return t;
        }

        // ---------- Daily quests ----------

        public System.Func<QuestDef[]> TodayQuests;
        public System.Func<int, int> QuestProgressOf;
        public System.Func<int, bool> QuestClaimedOf;
        public System.Func<bool> DailyChestReady;
        public System.Func<System.TimeSpan> UntilQuestReset;
        public System.Func<int> ChestsWaiting;
        public System.Action<int> ClaimQuest;
        public System.Action OpenDailyChest;
        Card questsCard;
        RectTransform questsList, questBadge;
        TextMeshProUGUI questTimer, questBadgeText;

        void BuildQuests()
        {
            questsCard = MakeCard("Quests", "DAILY TASKS", new Vector2(940f, 1400f), true);  // room for ACHIEVEMENTS under the tasks (OUT-9)
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
            Opened?.Invoke("tasks");
        }

        public void RefreshQuests()
        {
            if (questsList == null) return;
            foreach (Transform c in questsList) Destroy(c.gameObject);
            var left = UntilQuestReset?.Invoke() ?? System.TimeSpan.Zero;
            questTimer.text = $"New tasks in {left.Hours}h {left.Minutes}m";

            // The free daily chest.
            bool ready = DailyChestReady?.Invoke() ?? false;
            var daily = QuestRow(0f, ready ? Themes.Hex(0xFFF1C9) : Inset);
            Img(Rect("Chest", daily, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(90f, 0f), new Vector2(110f, 110f)), UiArt.Icon3D("chest") ?? UiArt.Icon("chest"), UiArt.Icon3D("chest") != null ? Color.white : Themes.Hex(0xC9783C));
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

        public bool Speaking => speech != null && speech.gameObject.activeSelf;
        public string SpeechLine => speechText != null ? speechText.text : "";

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
        // newStamps: coffees that stamped the punch card for the first time on this run.
        public void ShowComplete(int levelNumber, int stars, int shots, int par, bool allPickups, bool finalLevel = false, int newStamps = 0)
        {
            HideAll();
            completeCard.Title.text = stars >= 3 ? "PERFECT!" : stars == 2 ? "GREAT!" : "NICE!";
            completeLevel.text = levelNumber > 0 ? $"LEVEL {levelNumber} CLEAR" : "LAB CLEAR";
            completeShots.text = shots.ToString();
            completePar.text = par.ToString();
            completeShots.color = shots <= par ? Themes.Hex(0x2EAD4B) : Ink;
            string stamped = newStamps > 0 ? $"+{newStamps} COFFEE STAMP{(newStamps > 1 ? "S" : "")}" : "";
            completeExtra.text = finalLevel ? "OFFICE CLEARED! MORE ROOMS SOON"
                : stamped.Length > 0 ? (allPickups ? "FULLY CAFFEINATED! " : "") + stamped
                : allPickups ? "FULLY CAFFEINATED!" : shots <= par ? "UNDER PAR!" : "";
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
            closeActions[card] = clicked;  // the back button does what the X does (OUT-18)
            var size = card.Root.sizeDelta;
            IconButton(card.Root, "Close", new Vector2(0.5f, 0.5f), new Vector2(size.x / 2f - 30f, size.y / 2f - 30f), 110f, Palette.UiRed, "close", Color.white).Clicked = clicked;
        }

        void Remember()
        {
            var open = OpenCard();
            if (open != null) backStack.Push(open);  // opened from a card: back goes there
            else
            {
                backStack.Clear();
                titleReturn = title.gameObject.activeSelf;
            }
        }

        void Back()
        {
            HideAll();
            if (backStack.Count > 0)
            {
                var card = backStack.Pop();
                if (card == skinsCard) RefreshSkins(false);
                else if (card == shopCard) RefreshShop();
                ShowCard(card);
                if (card == skinsCard) IconStudio.WardrobeOpen = true;
                return;
            }
            if (titleReturn) title.gameObject.SetActive(true);
            titleReturn = false;
        }

        public void HideAll()
        {
            SetBoosterTray(false);
            IconStudio.WardrobeOpen = false;
            foreach (var s in completeStars) Tweens.Kill(s);
            foreach (var c in new[] { pauseCard, levelsCard, skinsCard, completeCard, settingsCard, questsCard, failCard, shopCard, undoCard, hireCard, energyCard, quitCard, gadgetCard, guideCard, oddsCard, clipCard, achievementsCard })
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
            BuildPendingTiles();
            if (Screen.width != lastScreen.x || Screen.height != lastScreen.y) FitScaler();
            TickEnergy();
            TickHand(Time.unscaledDeltaTime);
            TickGuide();
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
            // Restart and undo use their 3D renders (OUT-39), which carry their own shading and outline.
            var icon3D = icon == "restart" || icon == "undo" ? UiArt.Icon3D(icon) : null;
            var ic = Img(Rect("Icon", r, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, size * (icon3D != null ? 0.04f : 0.05f)),
                Vector2.one * size * (icon3D != null ? 0.74f : 0.56f)), icon3D ?? UiArt.Icon(icon), icon3D != null ? Color.white : iconColor);
            if (icon3D == null && iconColor == Color.white) ic.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.22f);
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

        // A moving piece gets its own canvas (OUT-36): its motion then never re-batches the rest of the screen.
        static void OwnCanvas(RectTransform r, bool clickable = false)
        {
            var parent = r.GetComponentInParent<Canvas>();
            var c = r.gameObject.AddComponent<Canvas>();
            if (parent != null) c.additionalShaderChannels = parent.rootCanvas.additionalShaderChannels;
            if (clickable) r.gameObject.AddComponent<GraphicRaycaster>();
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
