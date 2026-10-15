#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace OutOfOffice.EditorTools
{
    // Lets iPhones with ProMotion run the game at 120 Hz.
    public static class IosPostProcess
    {
        [PostProcessBuild]
        public static void OnPostprocessBuild(BuildTarget target, string path)
        {
            if (target != BuildTarget.iOS) return;
            string plistPath = Path.Combine(path, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            plist.root.SetBoolean("CADisableMinimumFrameDurationOnPhone", true);
            plist.WriteToFile(plistPath);
        }
    }
}
#endif
