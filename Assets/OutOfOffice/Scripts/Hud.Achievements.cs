using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OutOfOffice
{
    // Achievements (OUT-9): the list (claimable first, then the closest to done, claimed last), each with its progress
    // and gem reward; a button on the tasks card with a count; and a toast that slides in when one is earned in play.
    public partial class Hud
    {
        public System.Func<AchievementDef, int> AchievementValue;
        public System.Func<AchievementDef, bool> AchievementDone, AchievementClaimed;
        public System.Action<AchievementDef> ClaimAchievement;

        static Sprite TrophyIcon => UiArt.Icon3D("trophy") ?? UiArt.Icon("check");

        Card achievementsCard;
        RectTransform achievementsContent, achievementsBadge;
        TextMeshProUGUI achievementsNote, achievementsBadgeText;

        void BuildAchievements()
        {
            achievementsCard = MakeCard("Achievements", "ACHIEVEMENTS", new Vector2(960f, 1600f), true);
            var c = achievementsCard.Root;
            achievementsNote = Body(Rect("Note", c, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(860f, 50f)), "", 30f, Themes.Hex(0x8A6A9E), Ink);
            var view = Rect("Viewport", c, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 700f), new Vector2(900f, 1340f));
            view.gameObject.AddComponent<RectMask2D>();
            Img(view, UiArt.Pill, new Color(1f, 1f, 1f, 0.001f)).raycastTarget = true;
            achievementsContent = Rect("Content", view, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, 1340f));
            achievementsContent.pivot = new Vector2(0.5f, 1f);
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.content = achievementsContent;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 30f;
            CloseButton(achievementsCard, () => Back());

            // On the tasks card, under the day's tasks.
            var open = TextButton(questsCard.Root, "ACHIEVEMENTS", Themes.Hex(0xF2A81D), new Vector2(40f, -585f), new Vector2(620f, 130f), 52f, null);
            open.Clicked = () => ShowAchievements();
            var button = open.transform as RectTransform;
            Img(Rect("Trophy", button, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(-6f, 10f), new Vector2(150f, 150f)), TrophyIcon, Color.white);
            achievementsBadge = Badge(button, "1");
            achievementsBadgeText = achievementsBadge.GetComponentInChildren<TextMeshProUGUI>();
        }

        public void ShowAchievements()
        {
            Remember();
            HideAll();
            RefreshAchievements();
            ShowCard(achievementsCard);
        }

        public void RefreshAchievements()
        {
            if (achievementsContent == null) return;
            foreach (Transform t in achievementsContent) Destroy(t.gameObject);
            var list = new List<AchievementDef>(Achievements.All);
            int Rank(AchievementDef a) => (AchievementClaimed?.Invoke(a) ?? false) ? 2 : (AchievementDone?.Invoke(a) ?? false) ? 0 : 1;
            float Ratio(AchievementDef a) => (AchievementValue?.Invoke(a) ?? 0) / (float)Mathf.Max(1, a.Target);
            list.Sort((x, y) => Rank(x) != Rank(y) ? Rank(x).CompareTo(Rank(y)) : Ratio(y).CompareTo(Ratio(x)));
            int claimed = 0;
            const float rowH = 170f, gap = 14f;
            for (int i = 0; i < list.Count; i++)
            {
                var a = list[i];
                int value = AchievementValue?.Invoke(a) ?? 0;
                bool done = AchievementDone?.Invoke(a) ?? false, got = AchievementClaimed?.Invoke(a) ?? false;
                if (got) claimed++;
                var row = Rect(a.Key, achievementsContent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -rowH / 2f - i * (rowH + gap)), new Vector2(880f, rowH));
                Sliced(Img(row, UiArt.Button, done && !got ? Themes.Hex(0xFFF1C9) : Inset), rowH, 0.2f);
                var icon = Img(Rect("Trophy", row, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(84f, 4f), new Vector2(124f, 124f)), TrophyIcon, Color.white);
                if (!done) icon.color = new Color(0.55f, 0.5f, 0.62f, 0.55f);
                var name = Txt(Rect("Name", row, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(370f, 42f), new Vector2(430f, 52f)), a.Name, 34f, Ink, Ink, 0f, false);
                name.alignment = TextAlignmentOptions.MidlineLeft;
                name.enableAutoSizing = true;
                name.fontSizeMin = 20f;
                name.fontSizeMax = 34f;
                var text = Body(Rect("Text", row, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(370f, 2f), new Vector2(430f, 40f)), a.Text, 24f, Ink, Ink);
                text.alignment = TextAlignmentOptions.MidlineLeft;
                var bar = Rect("Bar", row, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(370f, -44f), new Vector2(430f, 30f));
                Sliced(Img(bar, UiArt.Pill, Themes.Hex(0xD9CCE6)), 30f, 0.5f);
                float k = Mathf.Clamp01(value / (float)Mathf.Max(1, a.Target));
                if (k > 0f)
                {
                    var fill = Rect("Fill", bar, Vector2.zero, new Vector2(Mathf.Max(0.08f, k), 1f), Vector2.zero, Vector2.zero);
                    Sliced(Img(fill, UiArt.Pill, done ? Palette.UiGreen : Themes.Hex(0xF2A81D)), 30f, 0.5f);
                }
                Body(Rect("Count", bar, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero), $"{value:N0}/{a.Target:N0}", 20f, Ink, Ink);

                var b = Rect("Button", row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-118f, 0f), new Vector2(200f, 100f));
                var bImage = Img(b, UiArt.Button, got ? Themes.Hex(0xCFC6DA) : done ? Palette.UiGreen : Themes.Hex(0xE4DAEF));
                Sliced(bImage, 100f, 0.36f);
                if (got)
                    Txt(Rect("Label", b, Vector2.zero, Vector2.one, new Vector2(0f, 4f), Vector2.zero), "DONE", 34f, Color.white, Color.Lerp(bImage.color, Ink, 0.6f), 0.3f, false);
                else
                {
                    var gem = UiArt.Icon3D("gem");
                    if (gem != null) Img(Rect("Gem", b, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(40f, 4f), new Vector2(70f, 70f)), gem, Color.white);
                    Txt(Rect("Label", b, Vector2.zero, Vector2.one, new Vector2(26f, 4f), new Vector2(-60f, 0f)), (done ? "+" : "") + a.Gems, 38f,
                        done ? Color.white : Ink, done ? Themes.Hex(0x1F7A33) : Ink, done ? 0.3f : 0f, false);
                }
                if (done && !got)
                {
                    bImage.raycastTarget = true;
                    var claim = a;
                    b.gameObject.AddComponent<UiPress>().Clicked = () => ClaimAchievement?.Invoke(claim);
                    if (!ReducedMotion) b.gameObject.AddComponent<Breather>().Hud = this;
                }
            }
            achievementsContent.sizeDelta = new Vector2(0f, list.Count * (rowH + gap));
            achievementsNote.text = $"{claimed} of {Achievements.All.Length} done. Each one pays gems.";
        }

        public void SetAchievementsBadge(int ready)
        {
            if (achievementsBadge == null) return;
            achievementsBadge.gameObject.SetActive(ready > 0);
            achievementsBadgeText.text = ready.ToString();
        }

        // ---------- The toast ----------

        RectTransform toast;
        TextMeshProUGUI toastTitle, toastText;
        Image toastIcon;
        readonly Queue<(string icon, string title, string text)> toasts = new Queue<(string, string, string)>();
        bool toasting;

        // A pill slides down under the top bar for a moment; it never takes a tap.
        public void Toast(string icon, string title, string text)
        {
            toasts.Enqueue((icon, title, text));
            if (!toasting) NextToast();
        }

        void NextToast()
        {
            if (toasts.Count == 0) { toasting = false; return; }
            toasting = true;
            var (icon, title, text) = toasts.Dequeue();
            if (toast == null)
            {
                toast = Rect("Toast", safe, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -250f), new Vector2(760f, 140f));
                OwnCanvas(toast);
                Sliced(Img(toast, UiArt.Pill, FrameDark), 140f, 0.5f);
                toastIcon = Img(Rect("Icon", toast, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(80f, 6f), new Vector2(150f, 150f)), null, Color.white);
                toastTitle = Txt(Rect("Title", toast, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(400f, 30f), new Vector2(520f, 46f)), "", 30f, Trim, FrameDark, 0.2f, false);
                toastTitle.alignment = TextAlignmentOptions.MidlineLeft;
                toastText = Txt(Rect("Text", toast, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(400f, -18f), new Vector2(520f, 60f)), "", 44f, Color.white, FrameDark, 0.3f, false);
                toastText.alignment = TextAlignmentOptions.MidlineLeft;
                toastText.enableAutoSizing = true;
                toastText.fontSizeMin = 24f;
                toastText.fontSizeMax = 44f;
            }
            toastIcon.sprite = UiArt.Icon3D(icon) ?? UiArt.Icon("check");
            toastTitle.text = title;
            toastText.text = text;
            toast.gameObject.SetActive(true);
            toast.SetAsLastSibling();
            Tweens.Kill(toast);
            float hidden = 220f, shown = -250f;
            Tweens.Run(0.4f, k => toast.anchoredPosition = new Vector2(0f, Mathf.LerpUnclamped(hidden, shown, k)), Tweens.OutBack, owner: toast);
            Tweens.Run(0.35f, k => toast.anchoredPosition = new Vector2(0f, Mathf.Lerp(shown, hidden, k)), Tweens.InOutSine, 2.6f, () =>
            {
                toast.gameObject.SetActive(false);
                NextToast();
            }, toast);
        }
    }
}
