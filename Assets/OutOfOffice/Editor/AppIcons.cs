using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace OutOfOffice.EditorTools
{
    // Puts the app icon (Puffy by the hole, rendered by IconStudio) in every icon slot a platform has. Unity fills a
    // platform's empty slots with its own logo rather than the default icon, which is how Android builds came out with
    // the Unity cube. Android 8+ launchers draw the adaptive slot: the icon as the background layer, and over it a
    // foreground of the whole icon shrunk to fit the launcher's mask, so the coffee and the Z aren't cut off.
    public static class AppIcons
    {
        const string IconPath = "Assets/OutOfOffice/Icon/AppIcon.png";
        const string AdaptivePath = "Assets/OutOfOffice/Icon/AppIconAdaptive.png";
        const float AdaptiveScale = 0.7f;  // a launcher shows about the middle two thirds of an adaptive layer

        public static void Apply(NamedBuildTarget target)
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon == null) { Debug.LogWarning("OOO no app icon at " + IconPath); return; }
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);

            Texture2D adaptive = null;
            foreach (var kind in PlayerSettings.GetSupportedIconKinds(target))
            {
                var slots = PlayerSettings.GetPlatformIcons(target, kind);
                foreach (var slot in slots)
                {
                    if (slot.maxLayerCount < 2) { slot.SetTextures(icon); continue; }
                    if (adaptive == null) adaptive = Inset(IconPath, AdaptivePath);
                    slot.SetTextures(icon, adaptive);  // background, foreground
                }
                PlayerSettings.SetPlatformIcons(target, kind, slots);
            }
        }

        // The icon shrunk into the middle of a transparent square. Rewritten only when the icon changes.
        static Texture2D Inset(string source, string path)
        {
            var src = new Texture2D(2, 2) { wrapMode = TextureWrapMode.Clamp };
            src.LoadImage(File.ReadAllBytes(source));
            int size = src.width;
            float inner = size * AdaptiveScale, offset = (size - inner) / 2f;
            var pixels = new Color32[size * size];  // transparent
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f - offset) / inner, v = (y + 0.5f - offset) / inner;
                    if (u >= 0f && u <= 1f && v >= 0f && v <= 1f) pixels[y * size + x] = src.GetPixelBilinear(u, v);
                }
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(src);
            Object.DestroyImmediate(tex);
            if (!File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(png))
            {
                File.WriteAllBytes(path, png);
                AssetDatabase.ImportAsset(path);
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
