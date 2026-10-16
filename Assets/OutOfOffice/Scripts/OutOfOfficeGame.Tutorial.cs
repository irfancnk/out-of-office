using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OutOfOffice
{
    // The first-time experience (OUT-11): the hand on the very first room until it's beaten, new gadgets introduced
    // as they turn up, a longer aim line on a room failed twice in a row, and home-screen pointers to the first free
    // chest and the wardrobe. Every step is a "ftue_step" event, so the first session can be measured. No ads or
    // purchases are ever offered here.
    public partial class OutOfOfficeGame
    {
        const float BoostedAim = 1.5f;
        float aimBoost = 1f;  // > 1 on a room failed twice in a row
        float handReadyAt;
        bool handShown;

        void WireTutorial()
        {
            hud.GadgetSeen = key => progress.SeenGadget(key);
            hud.GadgetArt = key => GadgetPreview.Get(key);
            hud.GadgetArtReady = key => GadgetPreview.Ready(key);
            hud.Opened = screen =>
            {
                if (screen == "tasks") progress.MarkFtue("home_tasks");
                if (screen == "wardrobe") progress.MarkFtue("home_wardrobe");
                hud.PointAt(null);
            };
        }

        // New gadgets in this room, a card each, with the room held still behind them.
        void IntroduceGadgets()
        {
            if (levelIndex < 0) return;
            var fresh = new List<Gadget>();
            foreach (var g in Gadgets.In(Def))
                if (!progress.SeenGadget(g.Key)) fresh.Add(g);
            if (fresh.Count == 0) return;
            CancelAim(false);
            stateBeforePause = State.Ready;
            state = State.Paused;
            Time.timeScale = 0f;
            ShowGadgetCard(fresh, 0);
        }

        void ShowGadgetCard(List<Gadget> fresh, int i)
        {
            if (i >= fresh.Count)
            {
                Time.timeScale = 1f;
                if (state == State.Paused) state = State.Ready;
                return;
            }
            var g = fresh[i];
            progress.MarkGadget(g.Key);
            progress.MarkFtue("gadget_" + g.Key);
            sound.Play(Sfx.Reward, 0.45f, 1.2f, 0.6f);
            hud.ShowGadget(g, "NEW IN THE OFFICE", fresh.Count > 1 ? $"{i + 1}/{fresh.Count}" : "", true, () => ShowGadgetCard(fresh, i + 1));
        }

        // Every frame: the hand on the first room, while the employee waits for a first shot.
        void TickTutorial()
        {
            bool want = level != null && levelIndex == 0 && state == State.Ready && shots == 0 && !hud.PanelOpen && !progress.FtueDone("first_win");
            if (levelIndex == 0 && state == State.Aiming) progress.MarkFtue("first_aim");
            if (!want)
            {
                if (handShown) hud.HideHand();
                handShown = false;
                handReadyAt = Time.unscaledTime + 0.8f;  // a moment's pause before it shows again
                return;
            }
            if (Time.unscaledTime < handReadyAt) return;
            // From the employee, straight back away from the bed: the shot it teaches goes for the bed.
            Vector2 puck = level.Puck.transform.position;
            Vector2 from = cam.WorldToScreenPoint(new Vector3(puck.x, puck.y, 0f));
            Vector2 bed = cam.WorldToScreenPoint(new Vector3(Def.Hole.x, Def.Hole.y, 0f));
            Vector2 dir = (bed - from).sqrMagnitude > 1f ? (bed - from).normalized : Vector2.up;
            hud.ShowHand(from, from - dir * Screen.height * 0.16f, "DRAG BACK, THEN LET GO");
            if (!handShown) progress.MarkFtue("hand");
            handShown = true;
        }

        // On the home screen: the first free chest, then the wardrobe once something is in it.
        void PointTheWay()
        {
            if (!progress.FtueDone("home_tasks") && progress.Best(Levels.All[0].Id) > 0 && progress.ChestsWaiting > 0) hud.PointAt("tasks");
            else if (!progress.FtueDone("home_wardrobe") && progress.ChestsOpened > 0) hud.PointAt("wardrobe");
            else hud.PointAt(null);
        }

        // A room failed twice in a row gets a longer aim line until it's beaten, and says so.
        void ApplyFailHelp()
        {
            aimBoost = levelIndex >= 0 && progress.FailStreak(Def.Id) >= 2 ? BoostedAim : 1f;
            if (aimBoost > 1f) StartCoroutine(SayAimBoost());
        }

        IEnumerator SayAimBoost()
        {
            yield return new WaitForSeconds(1.8f);
            if (state == State.Ready) hud.Banner("LONGER AIM LINE!", Palette.Gold, 0.6f);
        }

        void CountFail()
        {
            if (levelIndex >= 0) progress.SetFailStreak(Def.Id, progress.FailStreak(Def.Id) + 1);
        }

        void CountWin()
        {
            if (levelIndex < 0) return;
            progress.SetFailStreak(Def.Id, 0);
            if (levelIndex == 0) progress.MarkFtue("first_win");
        }
    }
}
