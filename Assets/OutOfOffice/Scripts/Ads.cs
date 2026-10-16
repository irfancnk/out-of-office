using System;
using UnityEngine;

namespace OutOfOffice
{
    // A network that serves rewarded videos (OUT-31). The real one (AdMob or Unity LevelPlay) needs the owner's
    // account, app id and ad unit ids; until then development builds use TestAds.
    public interface IAdProvider
    {
        bool IsTest { get; }
        bool RewardedReady { get; }
        void ShowRewarded(string placement, Action<bool> done);
    }

    // Development builds only: a full-screen "TEST AD" with a countdown, shown by the HUD.
    public class TestAds : IAdProvider
    {
        public static Action<string, Action<bool>> Present;  // set by the game: shows the overlay for a placement, reports finished or skipped
        public bool IsTest => true;
        public bool RewardedReady => Present != null;
        public void ShowRewarded(string placement, Action<bool> done)
        {
            if (Present == null) done?.Invoke(false);
            else Present(placement, done);
        }
    }

    public class NoAds : IAdProvider
    {
        public bool IsTest => false;
        public bool RewardedReady => false;
        public void ShowRewarded(string placement, Action<bool> done) => done?.Invoke(false);
    }

    // Rewarded ads only: the player always chooses to watch. Caps keep them from taking over the game, and no ad
    // is offered before the player has reached the sixth room (the first-time experience stays clean, OUT-11).
    public static class Ads
    {
        public const int DailyCap = 10;  // energy (OUT-48) added a reason to watch; still a handful a day
        public const float Cooldown = 30f;
        public const int FirstRoom = 6;  // offers start once this many rooms are open

        public static IAdProvider Provider = Debug.isDebugBuild ? new TestAds() : (IAdProvider)new NoAds();
        static float lastShown = -999f;

        public static float Now => Time.realtimeSinceStartup;

        // Whether to show the offer at all. AdFree players get the reward without the video, still within the cap.
        public static bool CanOffer(Progress p, float now) =>
            p.Unlocked >= FirstRoom && p.AdsToday < DailyCap && (p.AdFree || (now - lastShown >= Cooldown && Provider.RewardedReady));

        public static void ShowRewarded(Progress p, string placement, Action<bool> done, float? now = null)
        {
            float t = now ?? Now;
            if (!CanOffer(p, t)) { done?.Invoke(false); return; }
            Analytics.Event("ad_show", ("placement", placement), ("ad_free", p.AdFree), ("test", Provider.IsTest), ("today", p.AdsToday));
            void Finish(bool watched)
            {
                if (watched)
                {
                    lastShown = t;
                    p.CountAd();
                    Analytics.Event("ad_reward", ("placement", placement), ("ad_free", p.AdFree));
                }
                else Analytics.Event("ad_fail", ("placement", placement));
                done?.Invoke(watched);
            }
            if (p.AdFree) Finish(true);
            else Provider.ShowRewarded(placement, Finish);
        }

        // For tests: forget the cooldown.
        public static void ResetCooldown() => lastShown = -999f;
    }
}
