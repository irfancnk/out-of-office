using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace OutOfOffice.EditorTools
{
    // Batch entry point for deploy_ios.sh. Writes an Xcode project.
    // Environment: OOO_IOS_OUT (output folder), OOO_BUNDLE_ID, OOO_TEAM_ID (optional).
    public static class BuildIOS
    {
        public static void Run()
        {
            string outDir = Env("OOO_IOS_OUT", "Builds/iOS");
            string bundleId = Env("OOO_BUNDLE_ID", "com.irfancankaleli.outofoffice");
            string team = Env("OOO_TEAM_ID", "");

            PlayerSettings.productName = "Out of Office";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, bundleId);
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            // Performance: incremental GC (no long collection pauses mid-shot), optimized IL2CPP, stripped engine code.
            PlayerSettings.gcIncremental = true;
            PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.iOS, Il2CppCompilerConfiguration.Release);
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.iOS, ManagedStrippingLevel.Low);
            PlayerSettings.stripEngineCode = true;
            // App icon: Puffy by the hole, in every iOS slot. Unity scales it to every required size.
            AppIcons.Apply(NamedBuildTarget.iOS);
            if (team.Length > 0)
            {
                PlayerSettings.iOS.appleDeveloperTeamID = team;
                PlayerSettings.iOS.appleEnableAutomaticSigning = true;
            }

            AdMobSetup.Apply(false);  // ad app ids into Info.plist; CocoaPods brings the iOS ad SDK (OUT-31)
            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0) scenes = new[] { "Assets/Scenes/SampleScene.unity" };

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outDir,
                target = BuildTarget.iOS,
                options = BuildOptions.None,
            });
            Debug.Log($"OOO iOS BUILD {report.summary.result}, errors {report.summary.totalErrors}, out {outDir}");
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        static string Env(string name, string fallback)
        {
            string v = Environment.GetEnvironmentVariable(name);
            return string.IsNullOrEmpty(v) ? fallback : v;
        }
    }
}
