using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

namespace OutOfOffice.EditorTools
{
    // Builds the TextMeshPro font assets the HUD loads from Resources/Fonts (OUT-50): one dynamic 1024 atlas per face,
    // with padding wide enough for thick outlines and drop shadows, and a "Bold" preset with the drop shadow switched on
    // (it keeps the shadow shader variant in the build). Titan One is drawn at 92%: it is wider and heavier than the
    // face it replaced, so labels keep their size and still fit their boxes.
    // Unity -batchmode -quit -projectPath . -executeMethod OutOfOffice.EditorTools.FontAssets.Run
    public static class FontAssets
    {
        const string Dir = "Assets/OutOfOffice/Resources/Fonts/";

        [MenuItem("Out of Office/Build Font Assets")]
        public static void Run()
        {
            Make("TitanOne", 64, 24, 0.92f);
            Make("NunitoExtraBold", 64, 20, 1f);
            Make("NunitoBlack", 64, 20, 1f);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("FONTS built");
        }

        static void Make(string name, int size, int padding, float scale)
        {
            string path = Dir + name + " SDF.asset", preset = Dir + name + " Bold.mat";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.DeleteAsset(preset);
            var font = AssetDatabase.LoadAssetAtPath<Font>(Dir + name + ".ttf");
            var asset = TMP_FontAsset.CreateFontAsset(font, size, padding, GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true);
            asset.name = name + " SDF";
            AssetDatabase.CreateAsset(asset, path);
            var so = new SerializedObject(asset);
            so.FindProperty("m_ClearDynamicDataOnBuild").boolValue = true;
            so.FindProperty("m_FaceInfo.m_Scale").floatValue = scale;
            so.ApplyModifiedPropertiesWithoutUndo();
            var atlas = asset.atlasTexture;
            atlas.name = name + " Atlas";
            AssetDatabase.AddObjectToAsset(atlas, asset);
            var material = asset.material;
            material.name = name + " Material";
            AssetDatabase.AddObjectToAsset(material, asset);
            EditorUtility.SetDirty(asset);

            var shadow = new Material(AssetDatabase.LoadAssetAtPath<Material>(Dir + "LilitaOne Bold.mat")) { name = name + " Bold" };
            shadow.shader = material.shader;
            shadow.SetTexture(ShaderUtilities.ID_MainTex, atlas);
            shadow.SetFloat(ShaderUtilities.ID_GradientScale, padding + 1);
            shadow.SetFloat(ShaderUtilities.ID_TextureWidth, 1024);
            shadow.SetFloat(ShaderUtilities.ID_TextureHeight, 1024);
            AssetDatabase.CreateAsset(shadow, preset);
        }
    }
}
