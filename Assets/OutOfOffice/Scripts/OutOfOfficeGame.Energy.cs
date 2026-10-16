using UnityEngine;

namespace OutOfOffice
{
    // Energy for plays (OUT-48): what starting a room takes, the energy card and its refills, and what the HUD shows
    // about it. The bar and the play rules live in Progress.Energy.
    public partial class OutOfOfficeGame
    {
        System.Action afterRefill;  // what the player was starting when the energy card opened
        bool pausedForEnergy;       // the card opened over a room in play (a restart without the energy), which waits under it

        Energy energy => progress.Energy;

        // Starting over in this room now: a play's energy once a shot has been taken (or after running out of shots),
        // otherwise nothing.
        int StartOverCost => progress.PlaysCost(levelIndex) && !energy.Unlimited && (energy.AttemptOpen || !energy.InPlay) ? Energy.PlayCost : 0;

        // What the LEVEL button's start takes: -1 nothing to show (first rooms, the pass), 0 free (a play runs on), else energy.
        int NextPlayCost
        {
            get
            {
                if (!progress.PlaysCost(Mathf.Max(0, progress.CurrentIndex)) || energy.Unlimited) return -1;
                return energy.InPlay && !energy.AttemptOpen ? 0 : Energy.PlayCost;
            }
        }

        void WireEnergy()
        {
            hud.EnergyPoints = () => energy.Points;
            hud.EnergyUnlimited = () => energy.Unlimited;
            hud.EnergyUntilNext = () => energy.UntilNext;
            hud.EnergyUntilPlay = () => energy.UntilPlay;
            hud.NextPlayCost = () => NextPlayCost;
            hud.OpenEnergy = () => { Tap(); ShowEnergyCard(null); };
            hud.EnergyWithAd = () => Ads.ShowRewarded(progress, "energy", ok =>
            {
                if (ok) GiveEnergy(Catalog.AdEnergy, "ad");
                else hud.RefreshEnergy();
            });
            hud.EnergyWithGems = full =>
            {
                if (energy.Unlimited) return;
                int amount = full ? Mathf.Max(0, Energy.Max - energy.Points) : Energy.PlayCost;
                if (amount <= 0) return;
                if (!progress.SpendGems(full ? Catalog.RefillGems : Catalog.PlayGems, full ? "energy_fill" : "energy"))
                {
                    hud.ShowShop();  // short of gems: the shop, then back to where this was asked
                    return;
                }
                GiveEnergy(amount, full ? "gems_fill" : "gems");
            };
            hud.EnergyPass = () => { Tap(); hud.ShowShop(); };
            hud.EnergyClosed = () =>
            {
                afterRefill = null;
                if (!pausedForEnergy) return;
                pausedForEnergy = false;
                if (state != State.Paused) return;
                Time.timeScale = 1f;
                state = stateBeforePause == State.Aiming ? State.Ready : stateBeforePause;
            };
        }

        // Pays for a room's start when it needs a play. Leaving a room after a shot ends its play, so the check comes
        // first: without the energy for a new play the energy card opens, nothing is given up, and `retry` runs once
        // a refill makes the start possible.
        bool PayForStart(int index, System.Action retry)
        {
            bool counted = progress.PlaysCost(index), leaving = energy.AttemptOpen;
            if (!energy.CanStart(counted, leaving))
            {
                ShowEnergyCard(retry);
                return false;
            }
            if (leaving) energy.EndPlay(state == State.Failed ? "retry" : "restart", Def.Id);
            if (leaving) BreakStreak();  // starting over after a shot ends a win streak (OUT-9)
            energy.Start(counted, index >= 0 ? Levels.All[index].Id : "lab");
            return true;
        }

        void ShowEnergyCard(System.Action then)
        {
            afterRefill = then;
            Analytics.Event("energy_card", ("points", energy.Points), ("starting", then != null), ("ad", Ads.CanOffer(progress, Ads.Now)), ("gems", progress.Gems));
            if (then != null) sound.Play(Sfx.Cancel, 0.4f);  // the start didn't happen
            if (state == State.Ready || state == State.Aiming || state == State.Moving)
            {
                // Over a room in play: it holds still until the card closes or a refill starts the room again.
                CancelAim(false);
                stateBeforePause = state;
                state = State.Paused;
                Time.timeScale = 0f;
                sound.StopContinuous();
                pausedForEnergy = true;
            }
            hud.ShowEnergy();
        }

        // A refill paid out: the reward moment, then on with whatever start the energy card was opened for.
        void GiveEnergy(int amount, string source)
        {
            bool resume = afterRefill != null && hud.EnergyCardOpen;
            energy.Add(amount, source);
            sound.Play(Sfx.Reward, 0.6f, 1.15f, 0.8f);
            Haptics.Play(Haptics.Style.Success);
            hud.RefreshEnergy();
            hud.RefreshShop();
            hud.Celebrate("bolt", $"+{amount} ENERGY", Themes.Hex(0xFFC531), () =>
            {
                var then = afterRefill;
                if (!resume || then == null || !energy.CanStart(true, false)) return;
                afterRefill = null;
                pausedForEnergy = false;
                hud.HideAll();
                then();
            });
        }
    }
}
