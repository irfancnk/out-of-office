using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OutOfOffice
{
    // Chests (OUT-14): "What's inside" shows the exact odds of every rarity for a chest, how many items are left, and
    // how close the pity guarantees are; the Clip Shop trades the clips duplicates paid for a chest item of choice.
    public partial class Hud
    {
        public System.Func<bool> PaidChests;
        public System.Func<ChestTier, float[]> ChestOdds;
        public System.Func<Rarity, int> ChestItemsLeft;
        public System.Func<(int epic, int legendary)> Pity;
        public System.Func<int> ClipBalance;
        public System.Action<CosmeticItem> BuyWithClips;

        static readonly Rarity[] RarityOrder = { Rarity.Legendary, Rarity.Epic, Rarity.Rare, Rarity.Common };

        // ---------- What's inside ----------

        Card oddsCard;
        TextMeshProUGUI oddsTier, oddsPromise, oddsPity, oddsNote;
        readonly TextMeshProUGUI[] oddsPercent = new TextMeshProUGUI[4], oddsLeft = new TextMeshProUGUI[4];

        void BuildOdds()
        {
            oddsCard = MakeCard("Odds", "WHAT'S INSIDE", new Vector2(860f, 1240f), true);
            var c = oddsCard.Root;
            oddsTier = Txt(Rect("Tier", c, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 420f), new Vector2(760f, 80f)), "", 56f, Ink, Ink, 0f, false);
            oddsPromise = Body(Rect("Promise", c, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 352f), new Vector2(760f, 50f)), "", 30f, Themes.Hex(0x8A6A9E), Ink);
            for (int i = 0; i < 4; i++)
            {
                var rarity = RarityOrder[i];
                var row = Rect(rarity.ToString(), c, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 230f - i * 128f), new Vector2(720f, 112f));
                Sliced(Img(row, UiArt.Pill, Inset), 112f, 0.5f);
                var chip = Rect("Rarity", row, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(150f, 0f), new Vector2(250f, 70f));
                Sliced(Img(chip, UiArt.Pill, Cosmetics.RarityColor(rarity)), 70f, 0.5f);
                Txt(chip, Cosmetics.RarityName(rarity), 32f, Color.white, Ink, 0.25f, false);
                oddsLeft[i] = Body(Rect("Left", row, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(40f, 0f), new Vector2(220f, 50f)), "", 26f, Ink, Ink);
                oddsPercent[i] = Txt(Rect("Percent", row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-95f, 0f), new Vector2(160f, 70f)), "", 50f, Ink, Ink, 0f, false);
                oddsPercent[i].alignment = TextAlignmentOptions.MidlineRight;
            }
            oddsPity = Body(Rect("Pity", c, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -300f), new Vector2(760f, 100f)), "", 30f, Ink, Ink);
            oddsPity.textWrappingMode = TextWrappingModes.Normal;
            oddsNote = Body(Rect("Note", c, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -430f), new Vector2(760f, 120f)),
                "Every item of a rarity you don't own yet is equally likely. Once you own them all, a chest gives paper clips instead.",
                24f, Themes.Hex(0x8A6A9E), Ink);
            oddsNote.textWrappingMode = TextWrappingModes.Normal;
            CloseButton(oddsCard, () => Back());
        }

        public void ShowOdds(ChestTier tier)
        {
            Remember();
            HideAll();
            oddsTier.text = Chests.Name(tier);
            oddsPromise.text = Chests.Promise(tier);
            var odds = ChestOdds?.Invoke(tier) ?? new float[4];
            for (int i = 0; i < 4; i++)
            {
                int r = (int)RarityOrder[i];
                float p = odds[r] * 100f;
                oddsPercent[i].text = p <= 0f ? "0%" : p < 1f ? p.ToString("0.0") + "%" : Mathf.RoundToInt(p) + "%";
                int left = ChestItemsLeft?.Invoke(RarityOrder[i]) ?? 0;
                oddsLeft[i].text = left > 0 ? $"{left} to find" : "all found";
            }
            var (epic, legendary) = Pity?.Invoke() ?? (Chests.EpicPity, Chests.LegendaryPity);
            oddsPity.text = $"Epic or better guaranteed within {Mathf.Max(1, epic)} opens.\nLegendary guaranteed within {Mathf.Max(1, legendary)} opens.";
            Analytics.Event("chest_odds", ("tier", tier.ToString()));
            ShowCard(oddsCard);
        }

        // ---------- The Clip Shop ----------

        Card clipCard;
        RectTransform clipContent;
        TextMeshProUGUI clipBalance, clipNote;
        readonly List<(string id, RawImage image)> clipIcons = new List<(string, RawImage)>();

        void BuildClipShop()
        {
            clipCard = MakeCard("Clip Shop", "CLIP SHOP", new Vector2(960f, 1600f), true);
            var c = clipCard.Root;
            var pill = Rect("Balance", c, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(320f, 84f));
            Sliced(Img(pill, UiArt.Pill, Themes.Hex(0x3B2A6E)), 84f, 0.5f);
            var clip = UiArt.Icon3D("clip");
            if (clip != null) Img(Rect("Clip", pill, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(40f, 4f), new Vector2(96f, 96f)), clip, Color.white);
            clipBalance = Txt(Rect("Count", pill, Vector2.zero, Vector2.one, new Vector2(30f, 2f), Vector2.zero), "0", 52f, Color.white, Ink, 0.3f, false);
            clipNote = Body(Rect("Note", c, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -224f), new Vector2(860f, 50f)),
                "Duplicates turn into clips. Spend them on what you want.", 26f, Themes.Hex(0x8A6A9E), Ink);
            var view = Rect("Viewport", c, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 690f), new Vector2(900f, 1300f));
            view.gameObject.AddComponent<RectMask2D>();
            Img(view, UiArt.Pill, new Color(1f, 1f, 1f, 0.001f)).raycastTarget = true;
            clipContent = Rect("Content", view, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, 1300f));
            clipContent.pivot = new Vector2(0.5f, 1f);
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.content = clipContent;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 30f;
            CloseButton(clipCard, () => Back());
            IconStudio.Rendered += RefreshClipIcons;
        }

        public void ShowClipShop()
        {
            Remember();
            HideAll();
            RefreshClipShop();
            ShowCard(clipCard);
            IconStudio.WardrobeOpen = true;  // the item icons draw first, and faster
        }

        public void RefreshClipShop()
        {
            if (clipContent == null) return;
            foreach (Transform t in clipContent) Destroy(t.gameObject);
            clipIcons.Clear();
            int balance = ClipBalance?.Invoke() ?? 0;
            clipBalance.text = balance.ToString();
            var items = new List<CosmeticItem>();
            foreach (var i in Cosmetics.All) if (i.FromChest && !(OwnsItem?.Invoke(i) ?? false)) items.Add(i);
            items.Sort((a, b) => a.Rarity != b.Rarity ? b.Rarity.CompareTo(a.Rarity) : string.CompareOrdinal(a.Name, b.Name));
            const float tileW = 280f, tileH = 360f, gap = 20f;
            for (int n = 0; n < items.Count; n++)
            {
                var item = items[n];
                int price = Chests.ClipPrice[(int)item.Rarity];
                bool afford = balance >= price;
                int col = n % 3, row = n / 3;
                var tile = Rect(item.Id, clipContent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2((col - 1) * (tileW + gap), -tileH / 2f - row * (tileH + gap)), new Vector2(tileW, tileH));
                var bg = Img(tile, UiArt.Button, Inset);
                Sliced(bg, tileH, 0.1f);
                bg.raycastTarget = true;
                var band = Rect("Rarity", tile, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(200f, 40f));
                Sliced(Img(band, UiArt.Pill, Cosmetics.RarityColor(item.Rarity)), 40f, 0.5f);
                Txt(band, Cosmetics.RarityName(item.Rarity), 22f, Color.white, Ink, 0.2f, false);
                var icon = Rect("Icon", tile, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(178f, 178f));
                var raw = icon.gameObject.AddComponent<RawImage>();
                raw.raycastTarget = false;
                clipIcons.Add((item.Id, raw));
                var name = Txt(Rect("Name", tile, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 104f), new Vector2(256f, 50f)), item.Name, 26f, Ink, Ink, 0f, false);
                name.enableAutoSizing = true;
                name.fontSizeMin = 14f;
                name.fontSizeMax = 26f;
                var buy = Rect("Price", tile, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 46f), new Vector2(200f, 70f));
                var buyImage = Img(buy, UiArt.Button, afford ? Palette.UiGreen : Themes.Hex(0xCFC6DA));
                Sliced(buyImage, 70f, 0.36f);
                var clip = UiArt.Icon3D("clip");
                if (clip != null) Img(Rect("Clip", buy, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(34f, 2f), new Vector2(64f, 64f)), clip, Color.white);
                Txt(Rect("Text", buy, Vector2.zero, Vector2.one, new Vector2(20f, 4f), new Vector2(-50f, 0f)), price.ToString(), 36f, Color.white, Color.Lerp(buyImage.color, Ink, 0.6f), 0.3f, false);
                if (afford)
                {
                    buyImage.raycastTarget = true;
                    var chosen = item;
                    buy.gameObject.AddComponent<UiPress>().Clicked = () => BuyWithClips?.Invoke(chosen);
                }
            }
            int rows = (items.Count + 2) / 3;
            clipContent.sizeDelta = new Vector2(0f, Mathf.Max(1, rows) * (tileH + gap));
            clipNote.text = items.Count == 0 ? "You own every chest item. Clips wait for new ones." : "Duplicates turn into clips. Spend them on what you want.";
            RefreshClipIcons();
        }

        void RefreshClipIcons()
        {
            foreach (var (id, image) in clipIcons)
            {
                if (image == null) continue;
                IconStudio.ItemIcons.TryGetValue(id, out var tex);
                image.texture = tex;
                image.color = tex != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            }
        }
    }
}
