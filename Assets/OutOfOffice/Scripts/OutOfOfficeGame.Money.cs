using System.IO;
using UnityEngine;

namespace OutOfOffice
{
    // The game's money and data plumbing (OUT-31..35, energy OUT-48): the server-corrected clock, analytics sessions and the local
    // event log, A/B buckets, the save backup, the shop, rewarded ads and continues. Real ad, store and backend
    // providers drop in behind Ads.Provider, Store.Provider and ICloudSave; development builds use test stand-ins.
    public partial class OutOfOfficeGame
    {
        IPrefs prefs;
        ServerClock serverClock;
        ICloudSave backup;
        int extraShots;  // shots added by continues on this attempt
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
        AdMobAds admob;
#endif

        // Every frame: SDK callbacks onto the main thread, and the ad SDK's start-up check.
        void TickServices()
        {
            MainThread.Drain();
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            admob?.Poll();
#endif
        }

        void SetUpServices()
        {
            prefs = new UnityPrefs();
            serverClock = new ServerClock();
            progress = new Progress(prefs, serverClock);

            Analytics.InstallId = Experiments.InstallId = progress.InstallId;
            Analytics.Session = progress.StartSession();
            Analytics.Platform = Application.platform.ToString();
            try { Analytics.Sinks.Add(new LocalLogSink(Path.Combine(Application.persistentDataPath, "analytics.jsonl"))); }
            catch (System.Exception) { }  // no log is better than no game
            Analytics.CaptureErrors();
            // Plain log lines (analytics events in development builds) skip the stack trace: capturing one costs a frame on a phone.
            if (!Application.isEditor) Application.SetStackTraceLogType(LogType.Log, StackTraceLogType.None);
            FrameWatch.Attach(gameObject, () => state + (IconStudio.WardrobeOpen ? " wardrobe" : ""));
            backup = new LocalBackup(Path.Combine(Application.persistentDataPath, "save-backup.json"));
            Analytics.Event("session_start", ("session", Analytics.Session), ("days", progress.DaysSinceInstall), ("device", SystemInfo.deviceModel),
                ("os", SystemInfo.operatingSystem), ("version", Application.version), ("gems", progress.Gems), ("pass", progress.PassActive),
                ("unlocked", progress.Unlocked), ("stars", progress.TotalStars));
            StartCoroutine(ServerTime.Sync(serverClock));
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            // Real rewarded videos on phones (Google's test ads in development builds); the editor keeps TestAds.
            admob = new AdMobAds();
            Ads.Provider = admob;
            admob.Start();
#endif
        }

        void BackUpSave()
        {
            try { backup?.Upload(SaveSnapshot.Capture(prefs, progress.UtcNow, progress.InstallId)); }
            catch (System.Exception e) { Analytics.Event("backup_fail", ("error", e.GetType().Name)); }
        }

        void WireShop()
        {
            TestAds.Present = (placement, done) => hud.ShowTestAd(Hud.RewardFor(placement), done);

            hud.GemBalance = () => progress.Gems;
            hud.StoreAvailable = () => Store.Provider.Available;
            hud.StoreIsTest = () => Store.Provider.IsTest;
            hud.PriceOf = p => Store.Provider.PriceOf(p);
            hud.CanBuyProduct = p => progress.CanBuy(p);
            hud.PassActive = () => progress.PassActive;
            hud.PassDaysLeft = () => progress.PassDaysLeft;
            hud.PassClaimReady = () => progress.PassClaimReady;
            hud.AdOfferReady = () => Ads.CanOffer(progress, Ads.Now);
            hud.AdFree = () => progress.AdFree;
            hud.PassFirst = () => Experiments.ShopPassFirst;
            hud.ShopOpened = () => Analytics.Event("store_open", ("gems", progress.Gems), ("pass", progress.PassActive));

            hud.BuyProduct = product => Store.Buy(progress, product, ok =>
            {
                if (!ok) { sound.Play(Sfx.Cancel, 0.4f); return; }
                sound.Play(Sfx.Reward, 0.6f, 1.1f, 0.8f);
                Haptics.Play(Haptics.Style.Success);
                hud.RefreshShop();
                hud.RefreshEnergy();
                // The reward moment (OUT-48); a pack with a chest opens it instead.
                if (product.Chest) OpenChest(product.Title + "!");
                else if (product.PassDays > 0) hud.Celebrate("bolt", "UNLIMITED ENERGY", Palette.Gold);
                else if (product.Gems > 0) hud.Celebrate("gem", $"+{product.Gems:N0} GEMS", Themes.Hex(0xB990FF));
                else if (product == Catalog.NoAds) hud.Celebrate("tv", "NO MORE AD VIDEOS", Palette.UiBlue);
            });
            hud.ClaimPass = () =>
            {
                if (!progress.ClaimPass()) return;
                hud.RefreshShop();
                OpenChest("OFFICE PASS: DAILY BONUS");
            };
            hud.WatchAdForGems = () => Ads.ShowRewarded(progress, "shop_gems", ok =>
            {
                if (ok)
                {
                    progress.AddGems(Catalog.AdGems, "ad");
                    sound.Play(Sfx.Reward, 0.6f, 1.1f, 0.8f);
                    hud.Celebrate("gem", $"+{Catalog.AdGems} GEMS", Themes.Hex(0xB990FF));
                }
                hud.RefreshShop();
            });
            hud.BuyChestWithGems = () =>
            {
                if (!progress.SpendGems(Catalog.ChestCost, "chest")) return;
                hud.RefreshShop();
                OpenChest("BOUGHT WITH GEMS");
            };

            // Hiring the C-suite and the bosses with gems (OUT-45). Short of gems: the shop, then back to the hire card.
            hud.Hire = item =>
            {
                if (progress.Owns(item)) return true;
                if (!item.ForGems || !progress.SpendGems(item.GemCost, "hire"))
                {
                    if (item.ForGems) hud.ShowShop();
                    return false;
                }
                progress.Grant(item);
                Analytics.Event("hire", ("item", item.Id), ("gems", item.GemCost), ("balance", progress.Gems));
                sound.Play(Sfx.Reward, 0.6f, 1.1f, 0.8f);
                Haptics.Play(Haptics.Style.Success);
                return true;
            };

            // Undo past the free ones (OUT-41).
            hud.UndoWithAd = () => Ads.ShowRewarded(progress, "undo", ok => { if (ok) CloseUndoOffer("ad"); });
            hud.UndoWithGems = () =>
            {
                if (UndoAllowance.PayWithGems(progress, UndoGemCost)) CloseUndoOffer("gems");
                else hud.ShowShop();  // not enough gems: the shop, then back to the offer
            };
            hud.UndoDeclined = () => CloseUndoOffer(null);

            hud.ContinueWithAd = () => Ads.ShowRewarded(progress, "continue", ok => { if (ok) Continue("ad"); });
            hud.ContinueWithGems = () =>
            {
                if (progress.SpendGems(ContinueGemCost, "continue")) Continue("gems");
                else hud.ShowShop();  // not enough gems: the shop, then back to this card
            };
        }

        // The out-of-shots card with whatever continues may be offered right now. None in the first rooms.
        void ShowOutOfShotsOffers()
        {
            bool afterFirstRooms = progress.Unlocked >= Ads.FirstRoom;
            bool ad = afterFirstRooms && Ads.CanOffer(progress, Ads.Now);
            int shots = Experiments.ContinueShots;
            Analytics.Event("continue_offer", ("level", Def.Id), ("ad", ad), ("gems", afterFirstRooms), ("balance", progress.Gems), ("shots", shots));
            hud.ShowOutOfShots(ShotLimit, Quips.OutOfShots(), shots, ad, afterFirstRooms, ContinueGemCost, progress.AdFree, StartOverCost);
        }

        // Back into the same attempt with more shots.
        void Continue(string how)
        {
            if (state != State.Failed) return;
            int shots = Experiments.ContinueShots;
            extraShots += shots;
            if (progress.PlaysCost(levelIndex) && !energy.Unlimited) energy.Revive();  // the same play goes on (OUT-48)
            Analytics.Event("continue", ("level", Def.Id), ("how", how), ("shots", shots), ("limit", ShotLimit));
            hud.HideAll();
            hud.SetShots(this.shots, Def.Par, ShotLimit);
            sound.Play(Sfx.Reward, 0.6f, 1.2f, 0.8f);
            mascot.Express(Eyes.Happy, Mouth.Grin, 0.8f, 2f);
            hud.Banner($"+{shots} SHOTS!", Palette.Gold, 0.5f);
            hud.SetRestartCost(StartOverCost);
            state = State.Ready;
        }
    }
}
