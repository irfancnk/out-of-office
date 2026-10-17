using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OutOfOffice
{
    // The settings card (OUT-16, OUT-19): sound and music sliders, haptics strength, a graphics tier with "auto",
    // battery saver, and reduce motion. The pause card keeps its quick on/off switches, which mute.
    public partial class Hud
    {
        public System.Action<float, bool> SetSfxVolume, SetMusicVolume;  // value, and whether the slider was let go
        public System.Action<int> SetHaptics, SetGraphics;                // haptics 0..2; graphics -1 auto, else a tier
        public System.Action<bool> SetBatterySaver;

        class SliderView
        {
            public string Key;
            public RectTransform Fill, Knob;
            public TextMeshProUGUI Percent;
        }

        class ChoiceView
        {
            public string Key;
            public Image[] Segments;
            public TextMeshProUGUI[] Labels;
        }

        readonly List<SliderView> sliders = new List<SliderView>();
        readonly List<ChoiceView> choices = new List<ChoiceView>();
        TextMeshProUGUI graphicsNote;

        void BuildSettingsCard()
        {
            settingsCard = MakeCard("Settings", "SETTINGS", new Vector2(880f, 1320f), true);
            var c = settingsCard.Root;
            const float top = 450f, step = 128f;
            SliderRow(c, "sfx", "SOUND", "speaker", new Vector2(0f, top), (v, done) => SetSfxVolume?.Invoke(v, done));
            SliderRow(c, "music", "MUSIC", "music", new Vector2(0f, top - step), (v, done) => SetMusicVolume?.Invoke(v, done));
            ChoiceRow(c, "haptics", "HAPTICS", "vibrate", new Vector2(0f, top - 2f * step), new[] { "OFF", "LIGHT", "FULL" }, i => SetHaptics?.Invoke(i));
            ChoiceRow(c, "graphics", "GRAPHICS", "display", new Vector2(0f, top - 3f * step), new[] { "AUTO", "LOW", "MID", "HIGH" }, i => SetGraphics?.Invoke(i - 1));
            graphicsNote = Body(Rect("Graphics Note", c, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, top - 3f * step - 82f), new Vector2(700f, 44f)),
                "", 24f, Themes.Hex(0x8A6A9E), Ink);
            FitWidth(graphicsNote, 16f);  // the longest note (cooling down) shrinks to fit the card
            SwitchRow(c, "saver", "BATTERY SAVER", "battery", new Vector2(0f, top - 4f * step - 40f), () => SetBatterySaver?.Invoke(!SwitchOn("saver")));
            SwitchRow(c, "motion", "REDUCE MOTION", "motion", new Vector2(0f, top - 5f * step - 40f), () => SetReducedMotion?.Invoke(!SwitchOn("motion")));
            var note = Body(Rect("Motion Note", c, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, top - 5f * step - 150f), new Vector2(700f, 100f)),
                "Battery saver runs menus at 30 fps. Reduce motion calms shake and extra animation. Gameplay never changes.", 24f, Ink, Ink);
            note.textWrappingMode = TextWrappingModes.Normal;
            // The orientation room again (OUT-52), and the legal pages a store release needs (OUT-54).
            var links = top - 5f * step - 290f;
            TextButton(c, "TUTORIAL", Palette.UiBlue, new Vector2(-262f, links), new Vector2(240f, 100f), 34f, null).Clicked = () => ReplayTutorial?.Invoke();
            TextButton(c, "PRIVACY", Themes.Hex(0x8C7BA6), new Vector2(0f, links), new Vector2(240f, 100f), 34f, null).Clicked = () => OpenLink?.Invoke(Links.Privacy);
            TextButton(c, "TERMS", Themes.Hex(0x8C7BA6), new Vector2(262f, links), new Vector2(240f, 100f), 34f, null).Clicked = () => OpenLink?.Invoke(Links.Terms);
            CloseButton(settingsCard, () => Back());
        }

        // A row with a draggable level, 0 to 100%.
        void SliderRow(RectTransform parent, string key, string label, string icon, Vector2 pos, System.Action<float, bool> changed)
        {
            var row = SettingRow(parent, label, icon, pos, out var text);
            text.rectTransform.offsetMax = new Vector2(-414f, 0f);
            var track = Rect("Track", row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-282f, -2f), new Vector2(250f, 30f));
            Sliced(Img(track, UiArt.Pill, Themes.Hex(0xC9BFD6)), 30f, 0.5f);
            var fill = Rect("Fill", track, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            Sliced(Img(fill, UiArt.Pill, Palette.UiGreen), 30f, 0.5f);
            var knob = Rect("Knob", track, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero, new Vector2(66f, 66f));
            Img(knob, UiArt.RoundButton, Color.white);
            var percent = Txt(Rect("Percent", row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-69f, 0f), new Vector2(90f, 60f)), "100%", 28f, Ink, Ink, 0f, false);
            percent.alignment = TextAlignmentOptions.MidlineRight;
            var view = new SliderView { Key = key, Fill = fill, Knob = knob, Percent = percent };
            sliders.Add(view);
            var hit = Rect("Hit", row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-282f, 0f), new Vector2(300f, 112f));
            Img(hit, Shapes.Square, new Color(1f, 1f, 1f, 0.001f)).raycastTarget = true;
            var drag = hit.gameObject.AddComponent<SliderDrag>();
            drag.Track = track;
            drag.Changed = (v, done) =>
            {
                ShowSlider(view, v);
                changed(v, done);
            };
        }

        // A row with a few named options, one picked.
        void ChoiceRow(RectTransform parent, string key, string label, string icon, Vector2 pos, string[] options, System.Action<int> picked)
        {
            var row = SettingRow(parent, label, icon, pos, out var text);
            float width = options.Length > 3 ? 360f : 300f, seg = width / options.Length;
            text.rectTransform.offsetMax = new Vector2(-(width + 40f), 0f);
            var view = new ChoiceView { Key = key, Segments = new Image[options.Length], Labels = new TextMeshProUGUI[options.Length] };
            for (int i = 0; i < options.Length; i++)
            {
                int index = i;
                var r = Rect(options[i], row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-24f - width + seg * (i + 0.5f), 0f), new Vector2(seg - 8f, 78f));
                var img = Img(r, UiArt.Pill, Themes.Hex(0xE4DAEF));
                Sliced(img, 78f, 0.5f);
                img.raycastTarget = true;
                var t = Txt(Rect("Label", r, Vector2.zero, Vector2.one, new Vector2(0f, 2f), new Vector2(-8f, 0f)), options[i], 26f, Ink, Ink, 0f, false);
                t.enableAutoSizing = true;
                t.fontSizeMin = 16f;
                t.fontSizeMax = 26f;
                view.Segments[i] = img;
                view.Labels[i] = t;
                r.gameObject.AddComponent<UiPress>().Clicked = () => picked(index);
            }
            choices.Add(view);
        }

        RectTransform SettingRow(RectTransform parent, string label, string icon, Vector2 pos, out TextMeshProUGUI text)
        {
            var row = Rect(label, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, new Vector2(680f, 116f));
            Sliced(Img(row, UiArt.Pill, Inset), 116f, 0.5f);
            var badge = Rect("Badge", row, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(60f, 0f), new Vector2(82f, 82f));
            Img(badge, UiArt.RoundButton, theme.Accent);
            Img(Rect("Icon", badge, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 4f), new Vector2(50f, 50f)), UiArt.Icon(icon), Color.white);
            text = Txt(Rect("Label", row, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero), label, 36f, Ink, Ink, 0f, false);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.enableAutoSizing = true;
            text.fontSizeMin = 22f;
            text.fontSizeMax = 36f;
            text.rectTransform.offsetMin = new Vector2(118f, 0f);
            return row;
        }

        static void ShowSlider(SliderView s, float v)
        {
            v = Mathf.Clamp01(v);
            s.Fill.anchorMax = new Vector2(Mathf.Max(0.1f, v), 1f);
            s.Fill.gameObject.SetActive(v > 0.001f);
            s.Knob.anchorMin = s.Knob.anchorMax = new Vector2(v, 0.5f);
            s.Percent.text = Mathf.RoundToInt(v * 100f) + "%";
        }

        void SetChoice(string key, int value)
        {
            foreach (var c in choices)
            {
                if (c.Key != key) continue;
                for (int i = 0; i < c.Segments.Length; i++)
                {
                    bool on = i == value;
                    c.Segments[i].color = on ? theme.Accent : Themes.Hex(0xE4DAEF);
                    c.Labels[i].color = on ? Color.white : Ink;
                }
            }
        }

        // Everything the settings card shows, from the saved settings.
        public void UpdateMoreSettings(float sfx, float music, int haptics, int graphicsPick, bool saver, string tierNote)
        {
            foreach (var s in sliders) ShowSlider(s, s.Key == "sfx" ? sfx : music);
            SetChoice("haptics", haptics);
            SetChoice("graphics", graphicsPick + 1);
            SetSwitch("saver", saver);
            if (graphicsNote != null) graphicsNote.text = tierNote;
        }

        // Drags (and taps) along a slider track, reporting 0..1, and once more when let go.
        class SliderDrag : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
        {
            public RectTransform Track;
            public System.Action<float, bool> Changed;

            float At(PointerEventData e)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(Track, e.position, e.pressEventCamera, out var local);
                var r = Track.rect;
                return Mathf.Clamp01((local.x - r.xMin) / Mathf.Max(1f, r.width));
            }

            public void OnPointerDown(PointerEventData e) => Changed?.Invoke(At(e), false);
            public void OnDrag(PointerEventData e) => Changed?.Invoke(At(e), false);
            public void OnPointerUp(PointerEventData e) => Changed?.Invoke(At(e), true);
        }
    }
}
