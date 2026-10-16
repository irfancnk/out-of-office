using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace OutOfOffice.EditorTools
{
    // Batch entry point for deploy_android.sh. Writes an APK signed with the Android debug key, for testing on a phone.
    // Environment: OOO_ANDROID_OUT (APK path), OOO_BUNDLE_ID, OOO_DEV (1 = development build: logs, profiler).
    public static class BuildAndroid
    {
        public static void Run()
        {
            string outPath = Env("OOO_ANDROID_OUT", "Builds/Android/OutOfOffice.apk");
            string bundleId = Env("OOO_BUNDLE_ID", "com.irfancankaleli.outofoffice");
            bool dev = Env("OOO_DEV", "1") == "1";

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
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/OutOfOffice/Icon/AppIcon.png");
            if (icon != null) PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            EditorUserBuildSettings.buildAppBundle = false;  // an APK installs straight onto a phone; Play wants an AAB later

            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) scenes = new[] { "Assets/Scenes/SampleScene.unity" };
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath)));

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
                      $"APK {bytes / (1024f * 1024f):0.0} MB, dev {dev}, out {outPath}");
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        static string Env(string name, string fallback)
        {
            string v = Environment.GetEnvironmentVariable(name);
            return string.IsNullOrEmpty(v) ? fallback : v;
        }
    }
}
