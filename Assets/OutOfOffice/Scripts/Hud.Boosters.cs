using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OutOfOffice
{
    // Boosters in a room (OUT-13): a briefcase above the restart button opens a tray of the unlocked boosters, each
    // with how many are left (or its gem price when none are), greyed when it can't be used right now. Boosters that
    // last the try show as small icons under the pause button.
    public partial class Hud
    {
        public System.Func<BoosterKind, int> BoosterCount;
        public System.Func<BoosterKind, bool> BoosterUnlocked, BoosterActive, BoosterUsable;
        public System.Action<BoosterKind> UseBooster;
        public System.Action BuyBoosterBox;

        class BoosterView
        {
            public BoosterDef Def;
            public RectTransform Root, Badge, Price;
            public CanvasGroup Group;
            public TextMeshProUGUI Count;
        }

        RectTransform boosterButton, boosterTray, boosterActiveRow, boosterNew;
        readonly List<BoosterView> boosterViews = new List<BoosterView>();
        readonly Dictionary<BoosterKind, Image> boosterActiveIcons = new Dictionary<BoosterKind, Image>();
        bool boosterTrayOpen, boostersShown;

        void BuildBoosters()
        {
            var button = IconButton3D(hudRoot, "Boosters", new Vector2(1f, 0f), new Vector2(-100f, 292f), 124f, Themes.Hex(0xFFE0A3), UiArt.Icon3D("briefcase"));
            button.Clicked = () => SetBoosterTray(!boosterTrayOpen);
            boosterButton = button.transform as RectTransform;
            boosterNew = Badge(boosterButton, "!");
            hudBlockers.Add(boosterButton);

            boosterTray = Rect("Booster Tray", hudRoot, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-100f, 292f), Vector2.zero);
            foreach (var def in Boosters.All)
            {
                var item = Rect(def.Key, boosterTray, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(116f, 116f));
                var group = item.gameObject.AddComponent<CanvasGroup>();
                var name = Capsule(item, "Name", new Vector2(0f, 0.5f), new Vector2(-138f, 0f), new Vector2(250f, 62f), Shade);
                var label = Txt(name, def.Name, 28f, Color.white, Ink, 0.25f, false);
                label.enableAutoSizing = true;
                label.fontSizeMin = 18f;
                label.fontSizeMax = 28f;
                var press = IconButton3D(item, "Button", new Vector2(0.5f, 0.5f), Vector2.zero, 116f, Themes.Hex(0xFFF3DE), UiArt.Icon3D(def.Icon) ?? UiArt.Icon("gift"));
                var kind = def.Kind;
                press.Clicked = () => UseBooster?.Invoke(kind);
                var badge = Rect("Count", item, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-6f, -6f), new Vector2(54f, 54f));
                Img(badge, UiArt.RoundButton, Palette.UiGreen);
                var count = Txt(Rect("Text", badge, Vector2.zero, Vector2.one, new Vector2(0f, 3f), Vector2.zero), "3", 34f, Color.white, Themes.Hex(0x1F7A33), 0.3f, false);
                var price = Rect("Price", item, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -8f), new Vector2(112f, 46f));
                Sliced(Img(price, UiArt.Pill, FrameDark), 46f, 0.5f);
                var gem = UiArt.Icon3D("gem");
                if (gem != null) Img(Rect("Gem", price, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(22f, 3f), new Vector2(54f, 54f)), gem, Color.white);
                Txt(Rect("Text", price, Vector2.zero, Vector2.one, new Vector2(18f, 2f), new Vector2(-40f, 0f)), Boosters.GemCost.ToString(), 30f, Color.white, FrameDark, 0.25f, false);
                hudBlockers.Add(item);
                boosterViews.Add(new BoosterView { Def = def, Root = item, Badge = badge, Price = price, Group = group, Count = count });
            }
            boosterTray.gameObject.SetActive(false);

            boosterActiveRow = Rect("Active Boosters", hudRoot, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(60f, -196f), new Vector2(0f, 64f));
            foreach (var def in Boosters.All)
            {
                if (def.Kind == BoosterKind.Overtime || def.Kind == BoosterKind.DoOver) continue;  // those act at once
                var r = Rect(def.Key, boosterActiveRow, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(64f, 64f));
                var bg = Img(r, UiArt.RoundButton, new Color(1f, 0.95f, 0.85f, 0.92f));
                Img(Rect("Icon", r, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-6f, -6f)), UiArt.Icon3D(def.Icon) ?? UiArt.Icon("gift"), Color.white);
                r.gameObject.SetActive(false);
                boosterActiveIcons[def.Kind] = bg;
            }
            SetBoostersShown(false);
        }

        // A round button with a 3D icon (the briefcase and the boosters).
        UiPress IconButton3D(RectTransform parent, string name, Vector2 anchor, Vector2 pos, float size, Color color, Sprite icon)
        {
            var r = Rect(name, parent, anchor, anchor, pos, new Vector2(size, size));
            r.gameObject.AddComponent<CanvasGroup>();
            var shadow = Img(Rect("Shadow", r, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), Vector2.one * size * 1.25f),
                UiArt.SoftShadow, new Color(0.1f, 0.05f, 0.2f, 0.28f));
            Sliced(shadow, size * 1.25f, 0.5f);
            Img(r, UiArt.RoundButton, color).raycastTarget = true;
            Img(Rect("Icon", r, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, size * 0.04f), Vector2.one * size * 0.8f), icon, Color.white);
            shadow.transform.SetAsFirstSibling();
            return r.gameObject.AddComponent<UiPress>();
        }

        // In rooms once any booster is unlocked; never in the first rooms or the lab.
        public void SetBoostersShown(bool shown)
        {
            boostersShown = shown;
            if (boosterButton != null) boosterButton.gameObject.SetActive(shown);
            if (!shown) SetBoosterTray(false);
            RefreshBoosters();
        }

        public void MarkNewBooster(bool fresh)
        {
            if (boosterNew != null) boosterNew.gameObject.SetActive(fresh);
        }

        public void SetBoosterTray(bool open)
        {
            open &= boostersShown;
            boosterTrayOpen = open;
            if (boosterTray == null) return;
            boosterTray.gameObject.SetActive(open);
            if (!open) return;
            boosterTray.SetAsLastSibling();
            MarkNewBooster(false);
            RefreshBoosters();
            int i = 0;
            foreach (var v in boosterViews)
            {
                if (!v.Root.gameObject.activeSelf) continue;
                var r = v.Root;
                Tweens.Kill(r);
                if (ReducedMotion) { r.localScale = Vector3.one; continue; }
                r.localScale = Vector3.zero;
                Tweens.Run(0.32f, k => r.localScale = Vector3.one * k, Tweens.OutBack, 0.04f * i++, owner: r);
            }
        }

        public void RefreshBoosters()
        {
            int slot = 0;
            foreach (var v in boosterViews)
            {
                var k = v.Def.Kind;
                bool open = BoosterUnlocked?.Invoke(k) ?? false;
                v.Root.gameObject.SetActive(open);
                if (!open) continue;
                v.Root.anchoredPosition = new Vector2(0f, 164f * ++slot);
                int count = BoosterCount?.Invoke(k) ?? 0;
                v.Badge.gameObject.SetActive(count > 0);
                v.Count.text = count.ToString();
                v.Price.gameObject.SetActive(count <= 0);
                bool usable = BoosterUsable?.Invoke(k) ?? false;
                v.Group.alpha = usable ? 1f : 0.45f;
            }
            foreach (var pair in boosterActiveIcons)
                pair.Value.gameObject.SetActive(boostersShown && (BoosterActive?.Invoke(pair.Key) ?? false));
            float x = 0f;
            foreach (Transform t in boosterActiveRow)
            {
                if (!t.gameObject.activeSelf) continue;
                ((RectTransform)t).anchoredPosition = new Vector2(x + 32f, 0f);
                x += 72f;
            }
        }
    }
}
