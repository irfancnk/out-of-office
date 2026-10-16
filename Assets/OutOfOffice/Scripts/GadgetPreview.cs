using System.Collections.Generic;
using UnityEngine;

namespace OutOfOffice
{
    // Live previews of gadgets for the intro cards and the field guide (OUT-11): the gadget's demo room is built in the
    // current chapter's look on a layer of its own (movers place themselves in world space, so it sits at the origin, and
    // only the preview camera draws that layer), drawn once from the game's camera angle into a texture, and taken down in
    // the same frame, before any physics step or the main camera sees it. Textures are kept for the session.
    public static class GadgetPreview
    {
        const int Size = 384;
        const float Tilt = 34f, Fov = 34f, Distance = 8.2f;
        const int Layer = 29;  // unused by anything else
        static readonly Dictionary<string, RenderTexture> cache = new Dictionary<string, RenderTexture>();

        public static bool Ready(string key) => cache.TryGetValue(key, out var rt) && rt != null && rt.IsCreated();

        public static Texture Get(string key)
        {
            if (Ready(key)) return cache[key];
            var gadget = Gadgets.Find(key);
            if (gadget == null) return null;
            var target = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32) { name = "Gadget " + key, antiAliasing = 4 };
            target.Create();
            var stage = new GameObject("Gadget Stage");
            try
            {
                var theme = Themes.Current;
                var inst = LevelBuilder.Build(gadget.Demo(), stage.transform, false, Cosmetics.Compose(Cosmetics.Default), theme);
                if (inst.Puck != null) inst.Puck.gameObject.SetActive(false);
                inst.TickMovers(0.7f, true);  // movers a little way into their swing, so they read as moving
                foreach (var t in stage.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layer;
                var cam = new GameObject("Gadget Camera").AddComponent<Camera>();
                cam.transform.SetParent(stage.transform, false);
                cam.enabled = false;
                cam.cullingMask = 1 << Layer;
                cam.fieldOfView = Fov;
                cam.nearClipPlane = 0.3f;
                cam.farClipPlane = 60f;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = theme.Ground;
                cam.targetTexture = target;
                cam.transform.rotation = Quaternion.Euler(-Tilt, 0f, 0f);
                cam.transform.position = new Vector3(0f, -0.3f, 0f) - cam.transform.forward * Distance;
                cam.Render();
                cam.targetTexture = null;
                Meshes.ReleaseLevel(inst.Root);
            }
            finally
            {
                stage.SetActive(false);  // gone from physics and view now; destroyed at the end of the frame
                if (Application.isPlaying) Object.Destroy(stage);
                else Object.DestroyImmediate(stage);
            }
            cache[key] = target;
            return target;
        }
    }
}
