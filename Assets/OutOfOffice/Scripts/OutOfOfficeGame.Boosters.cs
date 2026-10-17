using System.Collections.Generic;
using UnityEngine;

namespace OutOfOffice
{
    // Boosters in play (OUT-13): what each one does to the try, when it can be used, and buying one with gems when the
    // briefcase is empty. Every try starts with none active.
    public partial class OutOfOfficeGame
    {
        readonly HashSet<BoosterKind> activeBoosters = new HashSet<BoosterKind>();
        float frozenAt;  // the movers' clock while a Coffee Break holds it
        int doOvers;     // Do-Overs on this try: a hole-in-one doesn't count after one
        readonly List<BoosterFx> boosterFx = new List<BoosterFx>();  // cups over stopped gadgets, the bed's ring (OUT-51)

        bool Boosted(BoosterKind k) => activeBoosters.Contains(k);
        float MoverClock => Boosted(BoosterKind.CoffeeBreak) ? frozenAt : levelTime;
        float BoosterAim => Boosted(BoosterKind.Laser) ? Boosters.LaserReach : 1f;
        bool BoostersHere => levelIndex >= 0 && System.Array.Exists(Boosters.All, b => progress.BoosterUnlocked(b.Kind) && progress.BoosterIntroduced(b.Kind));

        void WireBoosters()
        {
            hud.BoosterCount = k => progress.BoosterCount(k);
            hud.BoosterUnlocked = k => progress.BoosterUnlocked(k) && progress.BoosterIntroduced(k);
            hud.BoosterActive = Boosted;
            hud.BoosterUsable = CanBoost;
            hud.UseBooster = TryBooster;
            hud.BuyBoosterBox = () =>
            {
                if (!progress.SpendGems(Boosters.BoxGems, "booster_box")) { hud.ShowShop(); return; }
                foreach (var b in Boosters.All)
                    if (progress.BoosterUnlocked(b.Kind)) progress.AddBoosters(b.Kind, Boosters.BoxEach, "box");
                sound.Play(Sfx.Reward, 0.6f, 1.1f, 0.8f);
                Haptics.Play(Haptics.Style.Success);
                hud.RefreshShop();
                hud.RefreshBoosters();
                hud.Celebrate("briefcase", "BOOSTER BOX!", Palette.Gold);
            };
        }

        // A fresh try: nothing active.
        void ResetBoosters()
        {
            activeBoosters.Clear();
            doOvers = 0;
            if (level != null) level.OnBreak = false;
            foreach (var fx in boosterFx) if (fx != null) Destroy(fx.gameObject);
            boosterFx.Clear();
            hud.SetBoostersShown(BoostersHere);
        }

        bool CanBoost(BoosterKind k)
        {
            if (!BoostersHere || !progress.BoosterUnlocked(k)) return false;
            if (state != State.Ready && state != State.Aiming) return false;
            switch (k)
            {
                case BoosterKind.DoOver: return history.Count > 0;  // a shot to take back
                case BoosterKind.Overtime: return ShotLimit > 0;    // stacks
                case BoosterKind.CoffeeBreak: return !Boosted(k) && Boosters.BreakMatters(Def);  // never spent for nothing (OUT-51)
                default: return !Boosted(k);
            }
        }

        // Uses one; with none left, buys one for gems (the shop when short), so it is never a dead end.
        void TryBooster(BoosterKind k)
        {
            var def = Boosters.Find(k);
            if (!CanBoost(k))
            {
                sound.Play(Sfx.Cancel, 0.4f);
                if (k == BoosterKind.CoffeeBreak && BoostersHere && !Boosters.BreakMatters(Def)) hud.Banner("NOTHING MOVING HERE", Palette.Gold, 0.4f);
                return;
            }
            bool bought = false;
            if (progress.BoosterCount(k) <= 0)
            {
                if (!progress.SpendGems(Boosters.GemCost, "booster_" + def.Key))
                {
                    hud.SetBoosterTray(false);
                    hud.ShowShop();
                    return;
                }
                progress.AddBoosters(k, 1, "gems");
                bought = true;
            }
            progress.UseBooster(k);
            CancelAim(false);
            switch (k)
            {
                case BoosterKind.Laser:
                    activeBoosters.Add(k);
                    UsePerk();
                    break;
                case BoosterKind.Overtime:
                    extraShots += Boosters.OvertimeShots;
                    hud.SetShots(shots, Def.Par, ShotLimit);
                    break;
                case BoosterKind.CoffeeBreak:
                    activeBoosters.Add(k);
                    frozenAt = levelTime;
                    level.OnBreak = true;
                    level.TickMovers(levelTime, true, MoverClock);
                    boosterFx.Add(BoosterFx.BreakSigns(level));
                    break;
                case BoosterKind.DoOver:
                    DoOver();
                    break;
                case BoosterKind.MagnetBed:
                    activeBoosters.Add(k);
                    boosterFx.Add(BoosterFx.BedRing(level, Def.Hole, Boosters.MagnetRadius));
                    break;
            }
            progress.BumpStat(Achievements.BoostersUsed);
            Analytics.Event("booster_use", ("booster", def.Key), ("level", Def.Id), ("shots", shots), ("bought", bought), ("left", progress.BoosterCount(k)));
            sound.Play(Sfx.Reward, 0.6f, 1.25f, 0.8f);
            Haptics.Play(Haptics.Style.Success);
            hud.SetBoosterTray(false);
            hud.Banner(def.Name + "!", Palette.Gold, 0.5f);
            if (mascot != null) mascot.Express(Eyes.Happy, Mouth.Grin, 0.6f, 1.5f);
            hud.RefreshBoosters();
        }

        // Back to before the last shot, like an undo, and the shot comes off the count. It doesn't use a free undo.
        void DoOver()
        {
            int before = history.Peek().Shots;
            int undosBefore = undos;
            Rewind();
            shots = before;
            undos = undosBefore;
            doOvers++;
            hud.SetShots(shots, Def.Par, ShotLimit);
            hud.SetRestartCost(StartOverCost);
        }

        // In the physics step while a shot runs: the Magnet Bed's pull, on the floor only.
        void ApplyBoosterPhysics()
        {
            if (!Boosted(BoosterKind.MagnetBed) || level.Puck.Airborne) return;
            level.Puck.Body.linearVelocity += Boosters.BedPull(level.Puck.Body.position, Def.Hole, Time.fixedDeltaTime);
        }

        // After a restore (undo, a penalty), movers go back to where the Coffee Break holds them.
        void HoldMovers()
        {
            if (Boosted(BoosterKind.CoffeeBreak)) level.TickMovers(levelTime, true, MoverClock);
        }
    }
}
