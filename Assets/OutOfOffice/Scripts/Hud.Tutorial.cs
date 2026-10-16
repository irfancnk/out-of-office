using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OutOfOffice
{
    // The first-time experience (OUT-11): a hand that shows how to drag and let go, "NEW IN THE OFFICE" cards the first
    // time a gadget turns up, the field guide that keeps them, and a pointer on the home screen for the first chest and
    // the first visit to the wardrobe.
    public partial class Hud
    {
        public System.Action<string> Opened;            // "tasks", "wardrobe": a screen the tutorial points at was opened
        public System.Func<string, bool> GadgetSeen;
        public System.Func<string, Texture> GadgetArt;   // renders the preview if needed
        public System.Func<string, bool> GadgetArtReady;

        static Sprite HandIcon => UiArt.Icon3D("hand");

        // ---------- The hand ----------

        RectTransform handLayer, hand, handCaption;
        TextMeshProUGUI handCaptionText;
        Image handImage;
        readonly List<Image> trail = new List<Image>();
        Vector2 handFrom, handTo;
        float handTime;
        bool handOn;

        void BuildHand()
        {
            handLayer = Rect("Tutorial", hudRoot, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            OwnCanvas(handLayer);  // it moves every frame
            for (int i = 0; i < 7; i++)
            {
                var dot = Img(Rect("Trail", handLayer, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(30f, 30f)), Shapes.Circle, new Color(1f, 1f, 1f, 0f));
                trail.Add(dot);
            }
            hand = Rect("Hand", handLayer, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(230f, 230f));
            hand.pivot = new Vector2(0.46f, 0.86f);  // the fingertip touches the point
            handImage = Img(hand, HandIcon ?? Shapes.Circle, Color.white);
            handCaption = Capsule(handLayer, "Caption", Vector2.zero, Vector2.zero, new Vector2(620f, 84f), Shade);
            handCaptionText = Txt(handCaption, "", 38f, Color.white, Ink, 0.3f, false);
            handLayer.gameObject.SetActive(false);
        }

        // Screen points: where the drag starts (the employee) and where it ends.
        public void ShowHand(Vector2 fromScreen, Vector2 toScreen, string caption)
        {
            if (handLayer == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(handLayer, fromScreen, null, out var from);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(handLayer, toScreen, null, out var to);
            var size = handLayer.rect.size;
            handFrom = from + size * 0.5f;  // anchored bottom-left
            handTo = to + size * 0.5f;
            handCaptionText.text = caption;
            handCaption.sizeDelta = new Vector2(handCaptionText.GetPreferredValues(caption).x + 80f, 84f);
            float captionY = Mathf.Max(handFrom.y, handTo.y) + 230f;
            handCaption.anchoredPosition = new Vector2(Mathf.Clamp(handFrom.x, 340f, size.x - 340f), Mathf.Min(captionY, size.y - 300f));
            if (!handOn) handTime = 0f;
            handOn = true;
            handLayer.gameObject.SetActive(true);
        }

        public void HideHand()
        {
            handOn = false;
            if (handLayer != null) handLayer.gameObject.SetActive(false);
        }

        // A 2.6 s loop: the hand comes in, presses, drags back along a dotted line, holds, lets go, and rests.
        void TickHand(float dt)
        {
            if (handOn && hand != null)
            {
                handTime += dt;
                float t = handTime % 2.6f;
                Vector2 pos = handFrom;
                float scale = 1f, alpha = 1f, drag = 0f;
                if (t < 0.35f) { float k = t / 0.35f; alpha = k; pos = handFrom + new Vector2(60f, -60f) * (1f - k); }
                else if (t < 0.5f) scale = Mathf.Lerp(1f, 0.86f, (t - 0.35f) / 0.15f);
                else if (t < 1.5f) { drag = Tweens.InOutSine((t - 0.5f) / 1f); scale = 0.86f; }
                else if (t < 1.8f) { drag = 1f; scale = 0.86f; }
                else if (t < 2.1f) { float k = (t - 1.8f) / 0.3f; drag = 1f; scale = Mathf.Lerp(0.86f, 1.1f, k); alpha = 1f - k; }
                else alpha = 0f;
                if (t >= 0.5f && t < 2.1f) pos = Vector2.Lerp(handFrom, handTo, drag);
                hand.anchoredPosition = pos;
                hand.localScale = Vector3.one * scale;
                handImage.color = new Color(1f, 1f, 1f, alpha);
                bool showTrail = t >= 0.5f && t < 2.1f;
                for (int i = 0; i < trail.Count; i++)
                {
                    float f = (i + 1f) / (trail.Count + 1f);
                    var r = trail[i].rectTransform;
                    r.anchoredPosition = Vector2.Lerp(handFrom, pos, f);
                    trail[i].color = new Color(1f, 1f, 1f, showTrail && f <= 1f ? 0.75f * Mathf.Min(1f, alpha * 1.5f) : 0f);
                }
            }
            TickPointer(dt);
        }

        // ---------- Pointing at a home-screen button ----------

        RectTransform pointer, wardrobeButton, tasksButton;
        RectTransform pointed;

        public void PointAt(string target)
        {
            pointed = target == "tasks" ? tasksButton : target == "wardrobe" ? wardrobeButton : null;
            if (pointed == null)
            {
                if (pointer != null) pointer.gameObject.SetActive(false);
                return;
            }
            if (pointer == null)
            {
                pointer = Rect("Pointer", titleArea, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(190f, 190f));
                pointer.pivot = new Vector2(0.46f, 0.86f);
                pointer.localRotation = Quaternion.Euler(0f, 0f, 180f);  // pointing down at the button
                Img(pointer, HandIcon ?? Shapes.Circle, Color.white);
                OwnCanvas(pointer);
            }
            pointer.gameObject.SetActive(true);
            pointer.SetAsLastSibling();
        }

        void TickPointer(float dt)
        {
            if (pointer == null || pointed == null || !pointer.gameObject.activeSelf) return;
            if (!title.gameObject.activeSelf) return;
            Vector3 world = pointed.TransformPoint(new Vector3(0f, pointed.rect.height * 0.5f, 0f));
            Vector2 local = titleArea.InverseTransformPoint(world);
            float bob = Calm ? 0f : 22f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3.2f));
            pointer.anchoredPosition = local + titleArea.rect.size * 0.5f + new Vector2(0f, 6f + bob);
        }

        // ---------- "New in the office" ----------

        Card gadgetCard;
        RawImage gadgetArt;
        Image gadgetBack;
        RectTransform gadgetArtRect;
        TextMeshProUGUI gadgetName, gadgetText, gadgetCount;
        RectTransform gadgetNew;
        System.Action gadgetDone;

        void BuildGadgetCard()
        {
            gadgetCard = MakeCard("Gadget", "NEW IN THE OFFICE", new Vector2(860f, 1200f), true);
            var c = gadgetCard.Root;
            var frame = Rect("Frame", c, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 175f), new Vector2(560f, 560f));
            Sliced(Img(frame, UiArt.Button, Ink), 560f, 0.12f);
            var mask = Rect("Mask", frame, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(-24f, -24f));
            var maskImage = Img(mask, UiArt.Button, Color.white);
            Sliced(maskImage, 536f, 0.11f);
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            gadgetBack = Img(Rect("Back", mask, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero), Shapes.Square, Inset);  // under icons with a clear background
            gadgetArtRect = Rect("Art", mask, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            gadgetArt = gadgetArtRect.gameObject.AddComponent<RawImage>();
            gadgetArt.raycastTarget = false;
            gadgetNew = Rect("New", frame, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f, -16f), new Vector2(170f, 74f));
            gadgetNew.localRotation = Quaternion.Euler(0f, 0f, 9f);
            Sliced(Img(gadgetNew, UiArt.Pill, Palette.UiRed), 74f, 0.5f);
            Txt(gadgetNew, "NEW!", 44f, Color.white, Themes.Hex(0x8A1020), 0.3f, false);
            gadgetCount = Txt(Rect("Count", frame, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-70f, 30f), new Vector2(160f, 50f)), "", 32f, Ink, Ink, 0f, false);
            gadgetName = Txt(Rect("Name", c, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -170f), new Vector2(760f, 90f)), "", 66f, Ink, Ink, 0f, false);
            gadgetName.enableAutoSizing = true;
            gadgetName.fontSizeMin = 40f;
            gadgetName.fontSizeMax = 66f;
            gadgetText = Body(Rect("Text", c, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -265f), new Vector2(740f, 110f)), "", 34f, Ink, Ink);
            gadgetText.textWrappingMode = TextWrappingModes.Normal;
            TextButton(c, "GOT IT", Palette.UiGreen, new Vector2(0f, -440f), new Vector2(460f, 150f), 64f, null).Clicked = CloseGadget;
            closeActions[gadgetCard] = CloseGadget;
        }

        void CloseGadget()
        {
            var done = gadgetDone;
            gadgetDone = null;
            Back();
            done?.Invoke();
        }

        // heading: the ribbon ("NEW IN THE OFFICE", or the field guide's). counter: "1/2" when several arrive at once.
        public void ShowGadget(Gadget g, string heading, string counter, bool isNew, System.Action done) =>
            ShowIntro(GadgetArt?.Invoke(g.Key), false, heading, g.Name, g.Text, counter, isNew, done);

        // The intro card for anything new: a gadget with its live preview, or a booster (OUT-13) with its icon, which is
        // drawn smaller on a light backdrop.
        public void ShowIntro(Texture art, bool icon, string heading, string name, string text, string counter, bool isNew, System.Action done)
        {
            Remember();
            HideAll();
            gadgetCard.Title.text = heading;
            gadgetArt.texture = art;
            gadgetArt.color = art != null ? Color.white : new Color(1f, 1f, 1f, 0f);
            gadgetArtRect.offsetMin = icon ? new Vector2(70f, 70f) : Vector2.zero;
            gadgetArtRect.offsetMax = icon ? new Vector2(-70f, -70f) : Vector2.zero;
            gadgetName.text = name;
            gadgetText.text = text;
            gadgetCount.text = counter;
            gadgetNew.gameObject.SetActive(isNew);
            gadgetDone = done;
            ShowCard(gadgetCard);
            if (!ReducedMotion) Pop(gadgetNew, 0.35f, 1.4f);
        }

        // ---------- The field guide ----------

        Card guideCard;
        RectTransform guideContent;
        TextMeshProUGUI guideNote;
        readonly List<(string key, RawImage image)> guidePending = new List<(string, RawImage)>();

        void BuildGuide()
        {
            guideCard = MakeCard("Field Guide", "FIELD GUIDE", new Vector2(960f, 1600f), true);
            var c = guideCard.Root;
            guideNote = Body(Rect("Note", c, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(860f, 50f)), "", 30f, Themes.Hex(0x8A6A9E), Ink);
            var view = Rect("Viewport", c, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 700f), new Vector2(900f, 1340f));
            view.gameObject.AddComponent<RectMask2D>();
            Img(view, UiArt.Pill, new Color(1f, 1f, 1f, 0.001f)).raycastTarget = true;
            guideContent = Rect("Content", view, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, new Vector2(0f, 1340f));
            guideContent.pivot = new Vector2(0.5f, 1f);
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.content = guideContent;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.scrollSensitivity = 30f;
            CloseButton(guideCard, () => Back());
        }

        public void ShowGuide()
        {
            Remember();
            HideAll();
            RefreshGuide();
            ShowCard(guideCard);
        }

        void RefreshGuide()
        {
            foreach (Transform t in guideContent) Destroy(t.gameObject);
            guidePending.Clear();
            int seen = 0;
            const float tileW = 280f, tileH = 340f, gap = 20f;
            for (int i = 0; i < Gadgets.All.Length; i++)
            {
                var g = Gadgets.All[i];
                bool met = GadgetSeen?.Invoke(g.Key) ?? false;
                if (met) seen++;
                int col = i % 3, row = i / 3;
                var tile = Rect(g.Key, guideContent, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                    new Vector2((col - 1) * (tileW + gap), -tileH / 2f - row * (tileH + gap)), new Vector2(tileW, tileH));
                var bg = Img(tile, UiArt.Button, met ? Inset : Themes.Hex(0xE2D8EA));
                Sliced(bg, tileH, 0.1f);
                bg.raycastTarget = true;
                var mask = Rect("Mask", tile, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -132f), new Vector2(240f, 240f));
                Sliced(Img(mask, UiArt.Button, Color.white), 240f, 0.12f);
                mask.gameObject.AddComponent<Mask>().showMaskGraphic = met;
                var label = Txt(Rect("Name", tile, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 46f), new Vector2(250f, 60f)),
                    met ? g.Name : "???", 28f, Ink, Ink, 0f, false);
                label.enableAutoSizing = true;
                label.fontSizeMin = 16f;
                label.fontSizeMax = 28f;
                if (!met)
                {
                    Txt(Rect("Unknown", mask, Vector2.zero, Vector2.one, new Vector2(0f, 6f), Vector2.zero), "?", 130f, Themes.Hex(0xBCAFCB), Themes.Hex(0xBCAFCB), 0f, false);
                    continue;
                }
                var art = Rect("Art", mask, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject.AddComponent<RawImage>();
                art.raycastTarget = false;
                if (GadgetArtReady?.Invoke(g.Key) ?? false) art.texture = GadgetArt?.Invoke(g.Key);
                else
                {
                    art.color = new Color(1f, 1f, 1f, 0f);
                    guidePending.Add((g.Key, art));
                }
                var gadget = g;
                tile.gameObject.AddComponent<UiPress>().Clicked = () => ShowGadget(gadget, "FIELD GUIDE", "", false, null);
            }
            int rows = (Gadgets.All.Length + 2) / 3;
            guideContent.sizeDelta = new Vector2(0f, rows * (tileH + gap));
            guideNote.text = $"{seen} of {Gadgets.All.Length} gadgets met. Tap one to read it again.";
        }

        // One preview a frame while the guide is open, so opening it never stalls.
        void TickGuide()
        {
            if (guidePending.Count == 0 || guideCard == null || !guideCard.Root.gameObject.activeSelf) return;
            var (key, image) = guidePending[0];
            guidePending.RemoveAt(0);
            if (image == null) return;
            image.texture = GadgetArt?.Invoke(key);
            image.color = Color.white;
        }
    }
}
