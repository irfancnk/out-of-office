using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace OutOfOffice
{
    // The system back button (OUT-18): it closes the top card the way its X does, and asks before the game is left
    // from the home screen. A room in play is the game's to pause.
    public partial class Hud
    {
        public System.Action QuitGame;

        readonly Dictionary<Card, System.Action> closeActions = new Dictionary<Card, System.Action>();
        Card quitCard;

        void BuildQuit()
        {
            quitCard = MakeCard("Quit", "CLOCK OUT?", new Vector2(820f, 580f), true);
            var c = quitCard.Root;
            var line = Body(Rect("Line", c, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 90f), new Vector2(720f, 120f)),
                "Leaving the office already?\nThe bed will still be there tomorrow.", 34f, Ink, Ink);
            line.textWrappingMode = TextWrappingModes.Normal;
            TextButton(c, "STAY", Palette.UiGreen, new Vector2(-180f, -110f), new Vector2(320f, 140f), 60f, null).Clicked = () => Back();
            TextButton(c, "QUIT", Palette.UiRed, new Vector2(180f, -110f), new Vector2(320f, 140f), 60f, null).Clicked = () => QuitGame?.Invoke();
            closeActions[quitCard] = () => Back();
        }

        // True when the press was used here. False means nothing of the HUD's is open: a room is in play.
        public bool PressBack()
        {
            if (testAd != null && testAd.gameObject.activeSelf) return true;  // the video has its own skip
            if (transform.Find("Reward") != null) return true;                // the reward moment finishes on its own
            Card top = null;
            foreach (var c in new[] { pauseCard, levelsCard, skinsCard, completeCard, settingsCard, questsCard, failCard, shopCard, undoCard, hireCard, energyCard, quitCard })
                if (c != null && c.Root.gameObject.activeSelf && (top == null || c.Root.GetSiblingIndex() > top.Root.GetSiblingIndex())) top = c;
            if (top != null)
            {
                if (closeActions.TryGetValue(top, out var close)) close();
                else if (top == pauseCard) Resume?.Invoke();
                else if (top == failCard || top == completeCard) Home?.Invoke();  // up to the home screen, as Android expects
                UiPress.Pressed?.Invoke();
                return true;
            }
            if (title.gameObject.activeSelf)
            {
                Remember();
                HideAll();
                ShowCard(quitCard);
                return true;
            }
            return false;
        }
    }
}
