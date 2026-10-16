using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace OutOfOffice.EditorTools
{
    // Focused regression checks for the cosmetic rig. Run in the deployment copy, outside Play mode.
    public static class PresentationChecks
    {
        public static void RunBatch()
        {
            int failures = Run();
            failures += PhysicsTests.Run();
            EditorApplication.Exit(failures == 0 ? 0 : 5);
        }

        public static int Run()
        {
            int failures = 0;
            void Check(bool ok, string name)
            {
                Debug.Log("HSPRESENT " + name + (ok ? " PASS" : " FAIL"));
                if (!ok) failures++;
            }

            float circumference = Mathf.PI * 2f * Rules.PuckRadius;
            Check(Quaternion.Angle(Mascot.RollDelta(Vector2.right * circumference), Quaternion.identity) < 0.02f, "one circumference = one turn");
            Check(Vector3.Distance(Mascot.RollDelta(Vector2.right * circumference / 4f) * Vector3.back, Vector3.right) < 0.001f, "roll axis follows travel");
            Quaternion low = Quaternion.identity, high = Quaternion.identity;
            for (int i = 0; i < 30; i++) low = Mascot.RollDelta(Vector2.up * 2f / 30f) * low;
            for (int i = 0; i < 120; i++) high = Mascot.RollDelta(Vector2.up * 2f / 120f) * high;
            Check(Quaternion.Angle(low, high) < 0.05f, "30/120 Hz equivalent distance");
            Check(Quaternion.Angle(Mascot.RollDelta(Vector2.down * 2f) * low, Quaternion.identity) < 0.05f, "reverse travel unwinds roll");

            var root = new GameObject("Presentation Check");
            var oldAmbient = RenderSettings.ambientLight;
            var oldMode = RenderSettings.ambientMode;
            try
            {
                var camera = new GameObject("Portrait Camera").AddComponent<Camera>();
                camera.transform.SetParent(root.transform);
                camera.enabled = false;
                camera.transform.rotation = Quaternion.Euler(-48f, 0f, 0f);
                camera.transform.position = new Vector3(0f, 0f, -0.30f) - camera.transform.forward * 2.2f;
                camera.fieldOfView = 30f;
                camera.nearClipPlane = 0.05f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = Themes.Hex(0xE7DFD4);
                var light = new GameObject("Portrait Light").AddComponent<Light>();
                light.transform.SetParent(root.transform);
                light.type = LightType.Directional;
                light.intensity = 0.9f;
                light.transform.rotation = Quaternion.LookRotation(new Vector3(0.3f, -0.4f, 1f));
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.62f, 0.60f, 0.58f);

                for (int skin = 0; skin < Skins.All.Length; skin++)
                {
                    var p = LevelBuilder.MakeMascot(root.transform, Vector3.zero, Skins.All[skin]);
                    var m = p.GetComponent<Mascot>();
                    m.ViewCamera = camera;
                    m.Mode = MascotMode.Moving;
                    p.Body.simulated = true;
                    var roll = p.Spin.Find("Roll");
                    var accessory = roll.Find("Head/Accessory");
                    var headT = roll.Find("Head");
                    var tieT = roll.Find("Torso/Tie");
                    var faceT = p.transform.Find("Face");
                    var anchor = accessory.localPosition;
                    m.TickPresentation(1f / 60f);
                    bool attached = true;
                    for (int frame = 0; frame < 100; frame++)
                    {
                        p.transform.position += (Vector3)(frame < 50 ? Vector2.right : Vector2.up) * 0.03f;
                        if (frame == 50) m.OnImpact(Vector2.left, 12f);
                        m.TickPresentation(1f / 60f);
                        attached &= (accessory.localPosition - anchor).sqrMagnitude < 1e-10f;
                        attached &= float.IsFinite(roll.rotation.x);
                        // The face stays printed on the head surface; the tie stays on the shirt.
                        float faceGap = (faceT.position - headT.position).magnitude / headT.lossyScale.x;
                        attached &= Mathf.Abs(faceGap - (Mascot.HeadRadius + 0.012f)) < 0.02f;
                        attached &= tieT != null && tieT.parent == roll.Find("Torso");
                    }
                    Check(attached, Skins.All[skin].Id + " attachment after turn and impact");
                    Check(Mathf.Abs(((CircleCollider2D)p.Collider).radius - Rules.PuckRadius) < 1e-6f, "collider unchanged " + skin);
                    p.PlaceAt(Vector2.zero);
                    Check(Quaternion.Angle(roll.localRotation, Quaternion.identity) < 0.02f, "reset clears roll " + skin);
                    p.Body.simulated = false;
                    m.Mode = MascotMode.Idle;
                    m.Pose(Eyes.Open, Mouth.Smile);
                    m.TickPresentation(1f / 60f);
                    if (skin < 2) Render(camera, Path.Combine(Path.GetTempPath(), "outofoffice-" + Skins.All[skin].Id + ".png"));  // review images, any OS
                    UnityEngine.Object.DestroyImmediate(p.gameObject);
                }
                foreach (Setting setting in Enum.GetValues(typeof(Setting)))
                {
                    var surface = Textures.OfficeFloor(setting);
                    Check(surface != null && surface.mipmapCount > 1 && surface.wrapMode == TextureWrapMode.Repeat, "mipmapped floor " + setting);
                }
            }
            finally
            {
                RenderSettings.ambientMode = oldMode;
                RenderSettings.ambientLight = oldAmbient;
                UnityEngine.Object.DestroyImmediate(root);
            }
            Debug.Log("HSPRESENT done failures=" + failures);
            return failures;
        }

        static void Render(Camera camera, string path)
        {
            var target = new RenderTexture(768, 768, 24) { antiAliasing = 4 };
            var previous = RenderTexture.active;
            var image = new Texture2D(768, 768, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 768, 768), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                target.Release();
                UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(image);
            }
        }
    }
}
