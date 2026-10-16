using UnityEngine;

namespace OutOfOffice
{
    // AdMob ids (OUT-31). Fill the real ones in once the AdMob account exists; until then, and in every development
    // build, Google's public test ids are used. Test ads are safe to tap on any device: never tap a real ad in the
    // game yourself, or AdMob may close the account.
    public static class AdMobConfig
    {
        // ---- The owner's ids (from AdMob: Apps > App settings, and Apps > Ad units). Empty = not set up yet. ----
        public const string AndroidAppId = "";
        public const string IosAppId = "";
        public const string AndroidRewardedContinue = "";
        public const string AndroidRewardedShop = "";
        public const string IosRewardedContinue = "";
        public const string IosRewardedShop = "";

        // ---- Google's public test ids (developers.google.com/admob/unity/test-ads) ----
        public const string TestAndroidAppId = "ca-app-pub-3940256099942544~3347511713";
        public const string TestIosAppId = "ca-app-pub-3940256099942544~1458002511";
        public const string TestAndroidRewarded = "ca-app-pub-3940256099942544/5224354917";
        public const string TestIosRewarded = "ca-app-pub-3940256099942544/1712485313";

        public static bool HasRealIds => AndroidAppId.Length > 0 || IosAppId.Length > 0;

        // Development builds always show test ads, even with real ids filled in.
        public static bool UseTestAds => Debug.isDebugBuild || !HasRealIds;

        public static string AppIdFor(bool ios, bool testAds) =>
            ios ? (testAds || IosAppId.Length == 0 ? TestIosAppId : IosAppId) : (testAds || AndroidAppId.Length == 0 ? TestAndroidAppId : AndroidAppId);

        // The rewarded ad unit for a placement ("continue" or "shop_gems") on this platform.
        public static string RewardedUnit(string placement)
        {
            bool ios = Application.platform == RuntimePlatform.IPhonePlayer;
            if (UseTestAds) return ios ? TestIosRewarded : TestAndroidRewarded;
            string unit = ios ? (placement == "continue" ? IosRewardedContinue : IosRewardedShop)
                              : (placement == "continue" ? AndroidRewardedContinue : AndroidRewardedShop);
            return unit.Length > 0 ? unit : (ios ? TestIosRewarded : TestAndroidRewarded);
        }

        public static readonly string[] Placements = { "continue", "shop_gems" };
    }
}
