using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace OutOfOffice.EditorTools
{
    // Batch entry point for deploy_android.sh. By default it writes an APK signed with the Android debug key, for testing
    // on a phone. With OOO_RELEASE=1 it writes the Play Store build (OUT-54): an App Bundle signed with the upload key,
    // never a development build, and it stops with a clear list of what is missing rather than ship test ids.
    // Environment: OOO_ANDROID_OUT (output path), OOO_BUNDLE_ID, OOO_DEV (1 = development build: logs, profiler),
    // OOO_RELEASE, OOO_VERSION (default Version), OOO_VERSION_CODE (default: the UTC date and hour, yyMMddHH, so every
    // release build counts up), and for the upload key OOO_KEYSTORE, OOO_KEYSTORE_PASS, OOO_KEY_ALIAS, OOO_KEY_PASS.
    // The key's passwords only ever come from the environment; nothing about the key is stored in the repository.
    public static class BuildAndroid
    {
        public const string Version = "1.0.0";

        public static void Run()
        {
            string outPath = Env("OOO_ANDROID_OUT", "Builds/Android/OutOfOffice.apk");
            string bundleId = Env("OOO_BUNDLE_ID", "com.irfancankaleli.outofoffice");
            bool release = Env("OOO_RELEASE", "0") == "1";
            bool dev = !release && Env("OOO_DEV", "1") == "1";
            PlayerSettings.bundleVersion = Env("OOO_VERSION", Version);
            if (release)
            {
                if (!SetUpReleaseSigning()) { EditorApplication.Exit(3); return; }
                outPath = Path.ChangeExtension(outPath, ".aab");
            }
            else PlayerSettings.Android.useCustomKeystore = false;

            PlayerSettings.productName = "Out of Office";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, bundleId);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            // Same performance setup as iOS: incremental GC, IL2CPP, stripped engine code. ARM64 only: every phone
            // Google Play still serves new apps to has it.
            PlayerSettings.gcIncremental = true;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.Android, Il2CppCompilerConfiguration.Release);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Android, ManagedStrippingLevel.Low);
            PlayerSettings.stripEngineCode = true;
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            AppIcons.Apply(NamedBuildTarget.Android);  // every Android slot, or the launcher shows the Unity logo
            EditorUserBuildSettings.buildAppBundle = release;  // an APK installs straight onto a phone; Play takes an App Bundle

            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) scenes = new[] { "Assets/Scenes/SampleScene.unity" };
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)));
            AdMobSetup.Apply(dev);  // ad app ids into the manifest, Google's Android libraries into Gradle (OUT-31)

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outPath,
                target = BuildTarget.Android,
                options = dev ? BuildOptions.Development : BuildOptions.None,
            });
            // The APK's own size: report.summary.totalSize also counts the symbols folder written next to it.
            long bytes = File.Exists(outPath) ? new FileInfo(outPath).Length : 0;
            Debug.Log($"OOO ANDROID BUILD {report.summary.result}, errors {report.summary.totalErrors}, " +
                      $"APK {bytes / (1024f * 1024f):0.0} MB, dev {dev}, release {release}, version {PlayerSettings.bundleVersion} ({PlayerSettings.Android.bundleVersionCode}), out {outPath}");
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        // The upload key and the version code for a Play Store build. False, with every missing piece listed, when it
        // can't be a real release yet.
        static bool SetUpReleaseSigning()
        {
            string keystore = Env("OOO_KEYSTORE", ""), alias = Env("OOO_KEY_ALIAS", "");
            string storePass = Env("OOO_KEYSTORE_PASS", ""), keyPass = Env("OOO_KEY_PASS", "");
            var missing = new List<string>();
            if (keystore.Length == 0 || !File.Exists(keystore)) missing.Add("OOO_KEYSTORE: the path to your upload keystore (.jks or .keystore)");
            if (alias.Length == 0) missing.Add("OOO_KEY_ALIAS: the key's alias in that keystore");
            if (storePass.Length == 0) missing.Add("OOO_KEYSTORE_PASS: the keystore password");
            if (keyPass.Length == 0) missing.Add("OOO_KEY_PASS: the key password");
            if (!AdMobConfig.HasRealIds) missing.Add("real AdMob app and ad unit ids in Assets/OutOfOffice/Scripts/AdMobConfig.cs");
            if (missing.Count > 0)
            {
                Debug.LogError("OOO RELEASE can't build yet. Missing:\n  - " + string.Join("\n  - ", missing) + "\nSee RELEASE.md.");
                return false;
            }
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = keystore;
            PlayerSettings.Android.keystorePass = storePass;
            PlayerSettings.Android.keyaliasName = alias;
            PlayerSettings.Android.keyaliasPass = keyPass;
            PlayerSettings.Android.bundleVersionCode = int.Parse(Env("OOO_VERSION_CODE", DateTime.UtcNow.ToString("yyMMddHH")));
            return true;
        }

        static string Env(string name, string fallback)
        {
            string v = Environment.GetEnvironmentVariable(name);
            return string.IsNullOrEmpty(v) ? fallback : v;
        }
    }
}
