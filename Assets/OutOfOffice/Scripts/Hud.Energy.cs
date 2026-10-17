using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OutOfOffice
{
    // Energy and offers (OUT-48): the energy meter on the home screen, the energy card, a price tag on every offer
    // button (a video, gems, or a play's energy), and the reward moment when an ad or a purchase pays out.
    public partial class Hud
    {
        // ---------- Wiring ----------

        public System.Func<int> EnergyPoints;
        public System.Func<bool> EnergyUnlimited;
        public System.Func<System.TimeSpan> EnergyUntilNext, EnergyUntilPlay;
        public System.Func<int> NextPlayCost;       // what the LEVEL button's start takes: -1 nothing to show, 0 free (a play is running), else energy
        public System.Action OpenEnergy;            // the meter on the home screen
        public System.Action EnergyWithAd, EnergyPass, EnergyClosed;
        public System.Action<bool> EnergyWithGems;  // true: fill the bar

        static readonly Color GemPurple = Themes.Hex(0x8A5CF0);
        static readonly Color EnergyGold = Themes.Hex(0xFFB62E);
        static Sprite BoltIcon => UiArt.Icon3D("bolt") ?? UiArt.Icon("bolt");

        // ---------- Offer buttons: what you get, then what it takes ----------

        public enum Price { None, Ad, Free, Gems, Energy }

        class Offer
        {
            public RectTransform Root, LabelRect, Chip, ChipTextRect;
            public TextMeshProUGUI Label, ChipText;
            public Image ChipIcon;
            public UiPress Press;
            public float ChipWidth;
        }

        // A wide button with its price in a tag at the right end, so every offer in the game reads the same way.
        Offer OfferButton(RectTransform parent, string label, Color color, Vector2 pos, Vector2 size, float fontSize)
        {
            var press = TextButton(parent, label, color, pos, size, fontSize, null);
            var r = press.transform as RectTransform;
            var o = new Offer { Root = r, Press = press, LabelRect = r.Find("Label") as RectTransform };
            o.Label = o.LabelRect.GetComponentInChildren<TextMeshProUGUI>();
            o.Label.enableAutoSizing = true;
            o.Label.fontSizeMin = fontSize * 0.6f;
            o.Label.fontSizeMax = fontSize;
            float h = Mathf.Round(size.y * 0.6f);
            o.ChipWidth = h * 2.2f;
            o.Chip = Rect("Price", r, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-o.ChipWidth / 2f - size.y * 0.17f, 7f), new Vector2(o.ChipWidth, h));
            Sliced(Img(o.Chip, UiArt.Pill, new Color(0.08f, 0.03f, 0.18f, 0.3f)), h, 0.5f);
            o.ChipIcon = Img(Rect("Icon", o.Chip, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(h * 0.52f, 3f), new Vector2(h * 1.15f, h * 1.15f)), null, Color.white);
            o.ChipTextRect = Rect("Text", o.Chip, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            o.ChipText = Txt(o.ChipTextRect, "", h * 0.62f, Color.white, Color.Lerp(color, Ink, 0.6f), 0.35f, false);
            o.ChipText.enableAutoSizing = true;
            o.ChipText.fontSizeMin = h * 0.32f;
            o.ChipText.fontSizeMax = h * 0.62f;
            return o;
        }

        void SetPrice(Offer o, Price price, int amount = 0)
        {
            bool tag = price != Price.None;
            o.Chip.gameObject.SetActive(tag);
            float cut = tag ? o.ChipWidth + o.Root.sizeDelta.y * 0.25f : 0f;
            o.LabelRect.sizeDelta = new Vector2(-cut - 24f, 0f);
            o.LabelRect.anchoredPosition = new Vector2(-cut / 2f, 8f);
            if (!tag) return;
            Sprite icon = price == Price.Ad ? UiArt.Icon3D("tv") ?? UiArt.Icon("play")
                : price == Price.Gems ? UiArt.Icon3D("gem")
                : price == Price.Energy ? BoltIcon : null;
            o.ChipIcon.sprite = icon;
            o.ChipIcon.gameObject.SetActive(icon != null);
            o.ChipText.text = price == Price.Ad ? "AD" : price == Price.Free ? "FREE" : amount.ToString();
            float h = o.Chip.sizeDelta.y;
            o.ChipTextRect.offsetMin = new Vector2(icon != null ? h * 1.02f : h * 0.2f, 3f);
            o.ChipTextRect.offsetMax = new Vector2(-h * 0.2f, 3f);
        }

        // Stacks the visible offers from the top down, so a missing one (no ad ready, say) leaves no hole.
        static void StackOffers(float top, float gap, params Offer[] offers)
        {
            float y = top;
            foreach (var o in offers)
            {
                if (!o.Root.gameObject.activeSelf) continue;
                float h = o.Root.sizeDelta.y;
                o.Root.anchoredPosition = new Vector2(o.Root.anchoredPosition.x, y - h / 2f);
                y -= h + gap;
            }
        }

        // ---------- The meter on the home screen, and what the next start costs ----------

        RectTransform energyPill, playChip, restartChip;
        TextMeshProUGUI energyCount, energyTimer, playChipText, restartChipText;
        Image energyTimerPlate, playChipImage, playChipIcon;
        float energyTick;
        string energyShown;

        void BuildEnergyMeter(RectTransform header, Vector2 pos, float width)
        {
            energyPill = Rect("Energy", header, Vector2.zero, Vector2.zero, pos, new Vector2(width, 92f));
            var pill = Img(energyPill, UiArt.Pill, FrameDark);
            Sliced(pill, 92f, 0.5f);
            pill.raycastTarget = true;
            energyPill.gameObject.AddComponent<UiPress>().Clicked = () => OpenEnergy?.Invoke();
            Img(Rect("Icon", energyPill, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 6f), new Vector2(126f, 126f)), BoltIcon,
                UiArt.Icon3D("bolt") != null ? Color.white : EnergyGold);
            energyCount = Txt(Rect("Count", energyPill, Vector2.zero, Vector2.one, new Vector2(-8f, 2f), new Vector2(-144f, 0f)), "30", 54f, Color.white, FrameDark, 0.3f, true);
            var plus = Rect("Plus", energyPill, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-42f, 0f), new Vector2(72f, 72f));
            Sliced(Img(plus, UiArt.Button, Palette.UiGreen), 72f, 0.3f);
            Txt(Rect("Label", plus, Vector2.zero, Vector2.one, new Vector2(0f, 6f), Vector2.zero), "+", 60f, Color.white, Themes.Hex(0x1F7A33), 0.3f, false);
            var plate = Rect("Timer", header, Vector2.zero, Vector2.zero, new Vector2(pos.x + 8f, -34f), new Vector2(width - 16f, 60f));
            energyTimerPlate = Img(plate, UiArt.Pill, FrameDark);
            Sliced(energyTimerPlate, 60f, 0.5f);
            energyTimer = Txt(Rect("Text", plate, Vector2.zero, Vector2.one, new Vector2(0f, 2f), new Vector2(-24f, 0f)), "FULL", 30f, Trim, FrameDark, 0.2f, false);
            energyTimer.enableAutoSizing = true;
            energyTimer.fontSizeMin = 18f;
            energyTimer.fontSizeMax = 30f;
        }

        // On the big LEVEL button: the energy this start takes, or FREE while a play is running.
        void BuildPlayChip(RectTransform play)
        {
            playChip = Rect("Cost", play, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-46f, -2f), new Vector2(176f, 86f));
            playChipImage = Img(playChip, UiArt.Pill, FrameDark);
            Sliced(playChipImage, 86f, 0.5f);
            playChipIcon = Img(Rect("Icon", playChip, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(26f, 4f), new Vector2(100f, 100f)), BoltIcon, Color.white);
            playChipText = Txt(Rect("Text", playChip, Vector2.zero, Vector2.one, new Vector2(30f, 2f), new Vector2(-74f, 0f)), "5", 46f, Color.white, FrameDark, 0.3f, false);
            playChipText.enableAutoSizing = true;
            playChipText.fontSizeMin = 26f;
            playChipText.fontSizeMax = 46f;
            playChip.gameObject.SetActive(false);
        }

        // On the restart button: the energy starting over takes, once a shot has been taken in a play.
        void BuildRestartChip(RectTransform restart)
        {
            restartChip = Rect("Cost", restart, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(14f, -6f), new Vector2(122f, 64f));
            Sliced(Img(restartChip, UiArt.Pill, FrameDark), 64f, 0.5f);
            Img(Rect("Icon", restartChip, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(22f, 3f), new Vector2(74f, 74f)), BoltIcon, Color.white);
            restartChipText = Txt(Rect("Text", restartChip, Vector2.zero, Vector2.one, new Vector2(24f, 2f), new Vector2(-52f, 0f)), "5", 38f, Color.white, FrameDark, 0.3f, false);
            restartChip.gameObject.SetActive(false);
        }

        public void SetRestartCost(int energy)
        {
            if (restartChip == null) return;
            bool show = energy > 0, was = restartChip.gameObject.activeSelf;
            restartChip.gameObject.SetActive(show);
            restartChipText.text = energy.ToString();
            if (show && !was) Pop(restartChip, 0f, 1.35f);
        }

        public void RefreshEnergy()
        {
            RefreshEnergyMeter();
            if (energyCard != null && energyCard.Root.gameObject.activeSelf) RefreshEnergyCard();
        }

        void RefreshEnergyMeter()
        {
            if (energyPill == null) return;
            int points = EnergyPoints?.Invoke() ?? Energy.Max;
            bool unlimited = EnergyUnlimited?.Invoke() ?? false;
            string count = points.ToString();
            if (energyCount.text != count)
            {
                energyCount.text = count;
                if (energyShown != null) Pop(energyPill, 0f, 1.12f);  // a refill or a spend, not the first fill-in
            }
            energyShown = count;
            energyTimer.text = unlimited ? "UNLIMITED" : points >= Energy.Max ? "FULL" : "+1 IN " + Energy.Clock(EnergyUntilNext?.Invoke() ?? System.TimeSpan.Zero);
            energyTimer.color = unlimited ? FrameDark : Trim;
            energyTimerPlate.color = unlimited ? Palette.Gold : FrameDark;

            int cost = NextPlayCost?.Invoke() ?? -1;
            playChip.gameObject.SetActive(cost >= 0);
            if (cost < 0) return;
            playChipIcon.gameObject.SetActive(cost > 0);
            playChipText.text = cost > 0 ? cost.ToString() : "FREE";
            playChipText.rectTransform.parent.GetComponent<RectTransform>().offsetMin = new Vector2(cost > 0 ? 60f : 14f, 2f);
            // Green while a play runs on; red when the bar can't pay for the start.
            playChipImage.color = cost == 0 ? Themes.Hex(0x1F9E3A) : points < cost && !unlimited ? Palette.UiRed : FrameDark;
        }

        // Twice a second while the meter or the card shows: the refill timer counts down.
        void TickEnergy()
        {
            if (Time.unscaledTime < energyTick) return;
            energyTick = Time.unscaledTime + 0.5f;
            if (title.gameObject.activeSelf) RefreshEnergyMeter();
            if (energyCard != null && energyCard.Root.gameObject.activeSelf) RefreshEnergyCard();
        }

        // ---------- The energy card ----------

        Card energyCard;
        TextMeshProUGUI energyCardCount, energyCardLine;
        Offer energyAd, energyOne, energyFill, energyPass;

        void BuildEnergyCard()
        {
            energyCard = MakeCard("Energy", "OUT OF ENERGY", new Vector2(880f, 1240f), true);
            var c = energyCard.Root;
            var rays = Rect("Rays", c, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-120f, 365f), new Vector2(460f, 460f));
            Img(rays, UiArt.Sunburst, new Color(1f, 0.74f, 0.2f, 0.5f));
            rays.gameObject.AddComponent<Spin>().Speed = 16f;
            Img(Rect("Bolt", c, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-120f, 365f), new Vector2(250f, 250f)), BoltIcon,
                UiArt.Icon3D("bolt") != null ? Color.white : EnergyGold);
            energyCardCount = Txt(Rect("Count", c, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(110f, 370f), new Vector2(380f, 150f)), "0/30", 104f, EnergyGold, Ink, 0.45f, true);
            energyCardCount.enableAutoSizing = true;
            energyCardCount.fontSizeMin = 60f;
            energyCardCount.fontSizeMax = 104f;
            energyCardLine = Body(Rect("Line", c, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 175f), new Vector2(780f, 140f)), "", 34f, Ink, Ink);
            energyCardLine.textWrappingMode = TextWrappingModes.Normal;
            energyAd = OfferButton(c, $"+{Catalog.AdEnergy} ENERGY", Palette.UiBlue, Vector2.zero, new Vector2(720f, 150f), 60f);
            energyAd.Press.Clicked = () => EnergyWithAd?.Invoke();
            energyOne = OfferButton(c, $"+{Energy.PlayCost} ENERGY", GemPurple, Vector2.zero, new Vector2(720f, 140f), 56f);
            energyOne.Press.Clicked = () => EnergyWithGems?.Invoke(false);
            energyFill = OfferButton(c, "FILL THE BAR", GemPurple, Vector2.zero, new Vector2(720f, 140f), 56f);
            energyFill.Press.Clicked = () => EnergyWithGems?.Invoke(true);
            energyPass = OfferButton(c, "UNLIMITED: OFFICE PASS", Themes.Hex(0xF2A81D), Vector2.zero, new Vector2(720f, 124f), 44f);
            energyPass.Press.Clicked = () => EnergyPass?.Invoke();
            SetPrice(energyPass, Price.None);
            CloseButton(energyCard, () => { EnergyClosed?.Invoke(); Back(); });
        }

        public bool EnergyCardOpen => energyCard != null && energyCard.Root.gameObject.activeSelf;

        public void ShowEnergy()
        {
            Remember();
            HideAll();
            RefreshEnergyCard();
            ShowCard(energyCard);
        }

        void RefreshEnergyCard()
        {
            int points = EnergyPoints?.Invoke() ?? Energy.Max;
            bool unlimited = EnergyUnlimited?.Invoke() ?? false, empty = !unlimited && points < Energy.PlayCost;
            energyCard.Title.text = empty ? "OUT OF ENERGY" : "ENERGY";
            energyCardCount.text = unlimited ? "FREE" : $"{points}/{Energy.Max}";
            const string rule = "A win carries the play on for free.";
            if (unlimited) energyCardLine.text = "Unlimited with the Office Pass.\nPlay as much as you like.";
            else if (empty) energyCardLine.text = $"A play takes {Energy.PlayCost}. Next play in {Energy.Clock(EnergyUntilPlay?.Invoke() ?? System.TimeSpan.Zero)}.\n{rule}";
            else if (points >= Energy.Max) energyCardLine.text = $"Full bar! A play takes {Energy.PlayCost}.\n{rule}";
            else energyCardLine.text = $"A play takes {Energy.PlayCost}. +1 every {(int)Energy.Regen.TotalMinutes} min.\n{rule}";

            bool ad = AdOfferReady?.Invoke() ?? false, free = AdFree?.Invoke() ?? false;
            energyAd.Root.gameObject.SetActive(ad && !unlimited);
            SetPrice(energyAd, free ? Price.Free : Price.Ad);
            energyOne.Root.gameObject.SetActive(!unlimited);
            SetPrice(energyOne, Price.Gems, Catalog.PlayGems);
            energyFill.Root.gameObject.SetActive(!unlimited && points < Energy.Max);
            SetPrice(energyFill, Price.Gems, Catalog.RefillGems);
            energyPass.Root.gameObject.SetActive(!unlimited && (StoreAvailable?.Invoke() ?? false));
            StackOffers(70f, 26f, energyAd, energyOne, energyFill, energyPass);
        }

        // ---------- The reward moment ----------

        // An ad watched or a purchase made: the prize pops up big over spinning rays and a burst of sparkles, says what
        // it is, then floats off as the counters update. Taps wait for the moment, so it can't be skipped into a
        // second purchase by accident.
        public System.Action<string> Celebrated;  // the sound of the moment, by icon (OUT-17)

        public void Celebrate(string icon, string text, Color glow, System.Action done = null)
        {
            Celebrated?.Invoke(icon);
            var layer = Rect("Reward", transform as RectTransform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            OwnCanvas(layer, true);  // it animates every frame for a second and a half
            layer.SetAsLastSibling();
            var shade = Img(layer, Shapes.Square, new Color(0.08f, 0.04f, 0.16f, 0f));
            shade.raycastTarget = true;
            var center = new Vector2(0.5f, 0.5f);
            var rays = Rect("Rays", layer, center, center, new Vector2(0f, 110f), new Vector2(1100f, 1100f));
            var raysImage = Img(rays, UiArt.Sunburst, new Color(glow.r, glow.g, glow.b, 0f));
            var prize = Rect("Prize", layer, center, center, new Vector2(0f, 110f), new Vector2(420f, 420f));
            var prizeImage = Img(prize, UiArt.Icon3D(icon) ?? UiArt.Icon(icon), Color.white);
            var label = Rect("Label", layer, center, center, new Vector2(0f, -190f), new Vector2(1000f, 170f));
            var words = Txt(label, text, 124f, Color.white, Ink, 0.45f, true);
            words.enableAutoSizing = true;
            words.fontSizeMin = 60f;
            words.fontSizeMax = 124f;
            prize.localScale = label.localScale = Vector3.zero;
            bool calm = ReducedMotion;
            Tweens.Run(0.25f, k =>
            {
                shade.color = new Color(0.08f, 0.04f, 0.16f, 0.6f * k);
                raysImage.color = new Color(glow.r, glow.g, glow.b, 0.8f * k);
            }, Tweens.Linear, owner: layer);
            Tweens.Run(calm ? 0.2f : 0.55f, k => prize.localScale = Vector3.one * k, calm ? (System.Func<float, float>)Tweens.Linear : Tweens.OutBack, 0.05f, () =>
            {
                if (!calm) RewardBurst(layer, prize.anchoredPosition, glow);
            }, layer);
            Tweens.Run(calm ? 0.2f : 0.45f, k => label.localScale = Vector3.one * k, calm ? (System.Func<float, float>)Tweens.Linear : Tweens.OutBack, 0.22f, owner: layer);
            if (!calm)
                Tweens.Run(1.7f, k =>
                {
                    rays.localRotation = Quaternion.Euler(0f, 0f, -45f * k);
                    prize.localRotation = Quaternion.Euler(0f, 0f, 7f * Mathf.Sin(k * 10f) * (1f - k));
                }, Tweens.Linear, owner: layer);
            Tweens.Run(0.35f, k =>
            {
                float a = 1f - k;
                shade.color = new Color(0.08f, 0.04f, 0.16f, 0.6f * a);
                raysImage.color = new Color(glow.r, glow.g, glow.b, 0.8f * a);
                prizeImage.color = new Color(1f, 1f, 1f, a);
                words.alpha = a;
                prize.localScale = Vector3.one * Mathf.Lerp(1f, 0.45f, k);
                prize.anchoredPosition = new Vector2(0f, 110f + 300f * k);
            }, Tweens.InOutSine, calm ? 0.9f : 1.45f, () =>
            {
                Destroy(layer.gameObject);
                done?.Invoke();
            }, layer);
        }

        void RewardBurst(RectTransform parent, Vector2 at, Color glow)
        {
            for (int i = 0; i < 12; i++)
            {
                bool twinkle = i % 3 == 0;
                var r = Rect("Sparkle", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), at, Vector2.one * (twinkle ? 80f : 62f));
                Img(r, twinkle ? UiArt.Twinkle : UiArt.Star, twinkle ? Color.Lerp(glow, Color.white, 0.4f) : Color.white);
                Vector2 dir = Quaternion.Euler(0f, 0f, i * 30f + Random.Range(-12f, 12f)) * Vector2.up;
                float dist = Random.Range(280f, 440f);
                Tweens.Run(0.8f, k =>
                {
                    r.anchoredPosition = at + dir * (dist * k);
                    r.localScale = Vector3.one * (1.25f - k);
                    r.localRotation = Quaternion.Euler(0f, 0f, k * 220f);
                }, Tweens.OutCubic, i * 0.015f, () => Destroy(r.gameObject), parent);
            }
        }

        // What each rewarded placement pays, for the ad screen.
        public static string RewardFor(string placement) => placement switch
        {
            "energy" => $"+{Catalog.AdEnergy} ENERGY",
            "continue" => $"+{Experiments.ContinueShots} SHOTS",
            "undo" => "ONE UNDO",
            "shop_gems" => $"+{Catalog.AdGems} GEMS",
            _ => "A REWARD",
        };
    }
}
