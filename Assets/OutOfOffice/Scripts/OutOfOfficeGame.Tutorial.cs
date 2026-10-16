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

        // Something new to show: its art (made when the card opens), the card's words, and what meeting it does.
        struct Intro
        {
            public System.Func<Texture> Art;
            public bool Icon;
            public string Heading, Name, Text;
            public System.Action Met;
        }

        // New things in this room, a card each, with the room held still behind them: gadgets it uses for the first
        // time, then any booster unlocked here (OUT-13), which comes with three to start.
        void IntroduceGadgets()
        {
            if (levelIndex < 0) return;
            var intros = new List<Intro>();
            foreach (var g in Gadgets.In(Def))
            {
                if (progress.SeenGadget(g.Key)) continue;
                var gadget = g;
                intros.Add(new Intro
                {
                    Art = () => GadgetPreview.Get(gadget.Key), Heading = "NEW IN THE OFFICE", Name = gadget.Name, Text = gadget.Text,
                    Met = () => { progress.MarkGadget(gadget.Key); progress.MarkFtue("gadget_" + gadget.Key); },
                });
            }
            foreach (var b in Boosters.All)
            {
                if (!progress.BoosterUnlocked(b.Kind) || progress.BoosterIntroduced(b.Kind)) continue;
                var booster = b;
                intros.Add(new Intro
                {
                    Art = () => UiArt.Icon3D(booster.Icon)?.texture, Icon = true, Heading = "NEW BOOSTER", Name = booster.Name,
                    Text = booster.Text + $" Here are {Boosters.StartGift} to start.",
                    Met = () =>
                    {
                        progress.IntroduceBooster(booster.Kind);
                        progress.MarkFtue("booster_" + booster.Key);
                        hud.MarkNewBooster(true);
                        hud.SetBoostersShown(BoostersHere);
                    },
                });
            }
            if (intros.Count == 0) return;
            CancelAim(false);
            stateBeforePause = State.Ready;
            state = State.Paused;
            Time.timeScale = 0f;
            ShowIntroCard(intros, 0);
        }

        void ShowIntroCard(List<Intro> intros, int i)
        {
            if (i >= intros.Count)
            {
                Time.timeScale = 1f;
                if (state == State.Paused) state = State.Ready;
                return;
            }
            var intro = intros[i];
            intro.Met();
            sound.Play(Sfx.Reward, 0.45f, 1.2f, 0.6f);
            hud.ShowIntro(intro.Art(), intro.Icon, intro.Heading, intro.Name, intro.Text, intros.Count > 1 ? $"{i + 1}/{intros.Count}" : "", true,
                () => ShowIntroCard(intros, i + 1));
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
