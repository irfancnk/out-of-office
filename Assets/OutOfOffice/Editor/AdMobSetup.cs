using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace OutOfOffice.EditorTools
{
    // Before a phone build (OUT-31): writes the AdMob app ids into the plugin's settings asset (the plugin puts
    // them in the Android manifest and the iOS Info.plist), and resolves Google's Android libraries into the
    // Gradle templates. Batch builds don't run the dependency manager's automatic resolution, so it runs here.
    public static class AdMobSetup
    {
        public static void Apply(bool developmentBuild)
        {
            bool testAds = developmentBuild || !AdMobConfig.HasRealIds;
            var settingsType = FindType("GoogleMobileAds.Editor.GoogleMobileAdsSettings");
            var load = settingsType?.GetMethod("LoadInstance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (!(load?.Invoke(null, null) is ScriptableObject settings))
            {
                Debug.LogError("OOO ADMOB settings not found: is the Google Mobile Ads plugin imported?");
                return;
            }
            var so = new SerializedObject(settings);
            so.FindProperty("adMobAndroidAppId").stringValue = AdMobConfig.AppIdFor(false, testAds);
            so.FindProperty("adMobIOSAppId").stringValue = AdMobConfig.AppIdFor(true, testAds);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            if (!developmentBuild && !AdMobConfig.HasRealIds)
                Debug.LogWarning("OOO ADMOB release build with Google's TEST ids: fill in AdMobConfig to earn from ads.");
            Debug.Log($"OOO ADMOB app ids set (test ads {testAds}): android {AdMobConfig.AppIdFor(false, testAds)}, ios {AdMobConfig.AppIdFor(true, testAds)}");

            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android)
            {
                var resolver = FindType("GooglePlayServices.PlayServicesResolver");
                var resolve = resolver?.GetMethod("ResolveSync", BindingFlags.Static | BindingFlags.Public, null, new[] { typeof(bool) }, null);
                object ok = resolve?.Invoke(null, new object[] { true });
                Debug.Log($"OOO ADMOB android dependencies resolved: {ok ?? "resolver not found"}");
            }
        }

        static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName, false);
                if (t != null) return t;
            }
            return null;
        }
    }
}
