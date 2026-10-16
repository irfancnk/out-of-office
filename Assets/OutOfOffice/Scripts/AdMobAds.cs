using System;
using System.Collections.Generic;
using GoogleMobileAds.Api;
using GoogleMobileAds.Ump.Api;
using UnityEngine;

namespace OutOfOffice
{
    // Rewarded videos from Google AdMob (OUT-31), on phones. First the consent check (Google's User Messaging
    // Platform: the GDPR message for players in Europe and the UK), then the SDK starts and one ad per placement
    // is kept loaded, so a "watch ad" button never waits. A failed load retries on the next request, 30 s later.
    public class AdMobAds : IAdProvider
    {
        readonly Dictionary<string, RewardedAd> ready = new Dictionary<string, RewardedAd>();
        readonly HashSet<string> loading = new HashSet<string>();
        float retryAt;
        bool started, initialized;

        public bool IsTest => AdMobConfig.UseTestAds;

        public bool RewardedReady
        {
            get
            {
                foreach (var ad in ready.Values) if (ad != null && ad.CanShowAd()) return true;
                if (initialized && Time.realtimeSinceStartup >= retryAt) LoadAll();
                return false;
            }
        }

        public void Start()
        {
            if (started) return;
            started = true;
            MobileAds.RaiseAdEventsOnUnityMainThread = true;
            // Returning players who already answered can load ads at once; the update below refreshes consent.
            if (ConsentInformation.CanRequestAds()) Initialize();
            Debug.Log("OOO ADS consent update requested");
            ConsentInformation.Update(new ConsentRequestParameters(), updateError => MainThread.Post(() =>
            {
                if (updateError != null) Analytics.Event("ad_consent_fail", ("error", updateError.Message ?? ""));
                ConsentForm.LoadAndShowConsentFormIfRequired(formError => MainThread.Post(() =>
                {
                    if (formError != null) Analytics.Event("ad_consent_form_fail", ("error", formError.Message ?? ""));
                    Analytics.Event("ad_consent", ("can_request", ConsentInformation.CanRequestAds()));
                    if (ConsentInformation.CanRequestAds()) Initialize();
                }));
            }));
        }


        float nextPoll;

        // Called every frame by the game. Starts the SDK as soon as consent allows ads, even if the consent library's
        // callback never arrives (seen on Android with this plugin version), and retries failed loads.
        public void Poll()
        {
            if (!started || initialized || Time.realtimeSinceStartup < nextPoll) return;
            nextPoll = Time.realtimeSinceStartup + 1f;
            if (ConsentInformation.CanRequestAds())
            {
                Analytics.Event("ad_consent", ("can_request", true), ("via", "poll"));
                Initialize();
            }
        }
        void Initialize()
        {
            if (initialized) return;
            initialized = true;
            Debug.Log("OOO ADS initializing the SDK");
            MobileAds.Initialize(status => MainThread.Post(() =>
            {
                Analytics.Event("ad_sdk_ready", ("test", IsTest));
                LoadAll();
            }));
        }

        void LoadAll()
        {
            foreach (var placement in AdMobConfig.Placements) Load(placement);
        }

        void Load(string placement)
        {
            if (loading.Contains(placement) || (ready.TryGetValue(placement, out var have) && have != null && have.CanShowAd())) return;
            loading.Add(placement);
            RewardedAd.Load(AdMobConfig.RewardedUnit(placement), new AdRequest(), (ad, error) => MainThread.Post(() =>
            {
                loading.Remove(placement);
                if (error != null || ad == null)
                {
                    retryAt = Time.realtimeSinceStartup + 30f;
                    Analytics.Event("ad_load_fail", ("placement", placement), ("error", error?.GetMessage() ?? "no ad"));
                    return;
                }
                ready[placement] = ad;
                Analytics.Event("ad_loaded", ("placement", placement));
            }));
        }

        public void ShowRewarded(string placement, Action<bool> done)
        {
            // This placement's ad, or any other one that is ready.
            if (!ready.TryGetValue(placement, out var ad) || ad == null || !ad.CanShowAd())
            {
                ad = null;
                foreach (var kv in ready)
                    if (kv.Value != null && kv.Value.CanShowAd()) { ad = kv.Value; placement = kv.Key; break; }
            }
            if (ad == null)
            {
                done?.Invoke(false);
                LoadAll();
                return;
            }
            ready.Remove(placement);
            bool earned = false, finished = false;
            void Finish(bool ok)
            {
                if (finished) return;
                finished = true;
                ad.Destroy();
                done?.Invoke(ok);
                Load(placement);  // the next one, ready for next time
            }
            ad.OnAdFullScreenContentClosed += () => MainThread.Post(() => Finish(earned));
            ad.OnAdFullScreenContentFailed += error => MainThread.Post(() =>
            {
                Analytics.Event("ad_show_fail", ("placement", placement), ("error", error?.GetMessage() ?? ""));
                Finish(false);
            });
            ad.Show(reward => earned = true);  // set before the close event, which is posted after it
        }
    }
}
