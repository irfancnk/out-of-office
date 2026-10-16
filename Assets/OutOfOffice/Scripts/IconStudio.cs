using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace OutOfOffice
{
    // Renders employee portraits: the big title cut-out of the current outfit, and a small icon for every wardrobe item
    // (the employee wearing that item). Also composes the app icon.
    //
    // Portraits are queued and rendered a few per frame (OUT-36). Each one builds its rig far from the board, renders it
    // with Camera.Render into a temporary anti-aliased target, copies that into the portrait's own RenderTexture (shown
    // directly by the UI, so the GPU is never read back), and hides the rig at once. Only one rig exists at a time, and
    // each camera clips just behind its subject, so no other employee can ever end up in a portrait (OUT-37).
    public class IconStudio : MonoBehaviour
    {
        public static Texture HeroIcon;  // big cut-out with a transparent background, for the title screen
        public static readonly Dictionary<string, Texture> ItemIcons = new Dictionary<string, Texture>();
        public static System.Action Rendered;
        static readonly Vector3 Origin = new Vector3(400f, 400f, 0f);
        public static readonly Color PortraitBack = new Color(0.82f, 0.91f, 1f);

        // Set by the game and the HUD. A portrait costs a frame on a budget phone, so: nothing renders while a shot is in
        // play; with the wardrobe open, the hero and the open tab's icons render one a frame and the other tabs wait
        // until they are opened; elsewhere the rest trickle in one every few frames, so menus stay smooth.
        public static bool Playing, WardrobeOpen;
        public static Slot ShowingTab = Slot.Employee;
        const int TrickleFrames = 30;
        int sinceTrickle;

        const int HeroSize = 768, ItemSize = 192;

        class Job
        {
            public string Key, LookKey;
            public Outfit Outfit;
            public int Size;
            public bool Hero, Head;
            public Slot Slot;
        }

        static IconStudio instance;
        static readonly List<Job> queue = new List<Job>();
        static readonly Dictionary<string, RenderTexture> targets = new Dictionary<string, RenderTexture>();
        static readonly Dictionary<string, string> shown = new Dictionary<string, string>();  // portrait key -> look it shows
        static readonly Dictionary<string, Job> latest = new Dictionary<string, Job>();       // to redraw lost targets

        static string KeyOf(Outfit o) => o.Employee + "|" + o.Color + "|" + o.Hat + "|" + o.Face;

        public static void RenderHero(Outfit outfit) =>
            Enqueue(new Job { Key = "hero", LookKey = KeyOf(outfit), Outfit = outfit, Size = HeroSize, Hero = true });

        // One icon per wardrobe item: the player's own employee in their current outfit, with that one item swapped in,
        // so every tile shows what it would look like on you (OUT-29). Hats and face items are framed on the head,
        // where the change is; staff and ties show the whole body. Icons whose look didn't change are kept: after a new
        // hat, the hat tab's icons are already right.
        public static void RenderItems(Outfit outfit)
        {
            foreach (var item in Cosmetics.All)
            {
                var o = outfit.With(item);
                Enqueue(new Job { Key = item.Id, LookKey = KeyOf(o), Outfit = o, Size = ItemSize, Slot = item.Slot, Head = item.Slot == Slot.Hat || item.Slot == Slot.Face });
            }
        }

        // Pending jobs for the same portrait merge into the newest; a portrait that already shows the look is skipped.
        static void Enqueue(Job job)
        {
            EnsureInstance();
            for (int i = 0; i < queue.Count; i++)
                if (queue[i].Key == job.Key)
                {
                    queue.RemoveAt(i);
                    break;
                }
            if (shown.TryGetValue(job.Key, out var look) && look == job.LookKey && targets.TryGetValue(job.Key, out var rt) && rt != null && rt.IsCreated())
                return;
            queue.Add(job);
        }

        static void EnsureInstance()
        {
            if (instance != null || !Application.isPlaying) return;
            var go = new GameObject("Icon Studio");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<IconStudio>();
        }

        void LateUpdate()
        {
            if (queue.Count == 0 || (Playing && !WardrobeOpen)) return;
            int next = NextJob();
            if (next < 0) return;
            var job = queue[next];
            bool wanted = job.Hero || WardrobeOpen;  // on screen now
            if (!wanted && ++sinceTrickle < TrickleFrames) return;
            sinceTrickle = 0;
            queue.RemoveAt(next);
            Draw(job);
            Rendered?.Invoke();
        }

        // The hero first, then the open wardrobe tab, then the rest, which wait while the wardrobe is open.
        static int NextJob()
        {
            int best = -1, bestRank = int.MaxValue;
            for (int i = 0; i < queue.Count; i++)
            {
                var j = queue[i];
                int rank = j.Hero ? 0 : WardrobeOpen && j.Slot == ShowingTab ? 1 : 2;
                if (rank < bestRank) { best = i; bestRank = rank; }
            }
            return WardrobeOpen && bestRank == 2 ? -1 : best;
        }

        static void Draw(Job job)
        {
            var watch = FrameWatch.Active ? System.Diagnostics.Stopwatch.StartNew() : null;
            double built = 0;
            if (!targets.TryGetValue(job.Key, out var target) || target == null)
            {
                target = new RenderTexture(job.Size, job.Size, 0, RenderTextureFormat.ARGB32) { name = "Portrait " + job.Key };
                target.Create();
                targets[job.Key] = target;
            }
            var temp = RenderTexture.GetTemporary(job.Size, job.Size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 4);
            var rig = new GameObject("Icon Rig");
            try
            {
                var cam = job.Hero ? HeroCamera(rig.transform, Origin, temp) : ItemCamera(rig.transform, Origin, temp, job.Head);
                Pose(rig.transform, Origin, Cosmetics.Compose(job.Outfit), cam);
                if (watch != null) built = watch.Elapsed.TotalMilliseconds;
                RenderInDaylight(cam);
                if (job.Hero) Graphics.Blit(temp, target);
                else Graphics.Blit(temp, target, Corners);  // wardrobe icons get their rounded corners here
            }
            finally
            {
                RenderTexture.ReleaseTemporary(temp);
                Remove(rig);
            }
            if (watch != null) FrameWatch.Note($"{(job.Hero ? "portrait" : "icon")} build {built:0}ms total {watch.Elapsed.TotalMilliseconds:0}ms");
            shown[job.Key] = job.LookKey;
            latest[job.Key] = job;
            if (job.Hero) HeroIcon = target;
            else ItemIcons[job.Key] = target;
        }

        static Material corners;
        static Material Corners => corners != null ? corners : corners = new Material(Resources.Load<Shader>("OutOfOfficeRoundCorners")) { hideFlags = HideFlags.HideAndDontSave };

        // Portraits look the same in every chapter: lit by the first chapter's daylight, not by whatever room (a dim
        // night floor, say) sits behind the menu. The room's light is put back before the frame renders.
        static void RenderInDaylight(Camera cam)
        {
            var day = Themes.All[0];
            var sun = RenderSettings.sun;
            Color sunColor = sun != null ? sun.color : Color.white;
            float sunIntensity = sun != null ? sun.intensity : 1f;
            var ambient = RenderSettings.ambientLight;
            var probe = RenderSettings.ambientProbe;
            try
            {
                if (sun != null)
                {
                    sun.color = day.Sun;
                    sun.intensity = day.SunIntensity;
                }
                var dayProbe = new UnityEngine.Rendering.SphericalHarmonicsL2();
                dayProbe.AddAmbientLight(day.Ambient);
                RenderSettings.ambientLight = day.Ambient;
                RenderSettings.ambientProbe = dayProbe;
                cam.Render();
            }
            finally
            {
                if (sun != null)
                {
                    sun.color = sunColor;
                    sun.intensity = sunIntensity;
                }
                RenderSettings.ambientLight = ambient;
                RenderSettings.ambientProbe = probe;
            }
        }

        // The employee, placed and posed at once: the rig never lives to the next frame.
        static Mascot Pose(Transform rig, Vector3 pos, SkinDef look, Camera cam)
        {
            var m = LevelBuilder.MakeMascot(rig, pos, look).GetComponent<Mascot>();
            m.ViewCamera = cam;
            m.Mode = MascotMode.Idle;
            m.Pose(Eyes.Open, Mouth.Smile);
            for (int i = 0; i < 8; i++) m.TickPresentation(1f / 60f);
            return m;
        }

        static void Remove(GameObject rig)
        {
            rig.SetActive(false);
            Meshes.ReleaseLevel(rig);
            if (Application.isPlaying) Destroy(rig);
            else DestroyImmediate(rig);
        }

        // RenderTextures can lose their contents when the app goes to the background on some phones: redraw them.
        void OnApplicationPause(bool paused)
        {
            if (paused) return;
            foreach (var kv in targets)
                if (kv.Value != null && !kv.Value.IsCreated() && latest.TryGetValue(kv.Key, out var job))
                {
                    shown.Remove(kv.Key);
                    Enqueue(job);
                }
        }

        // The item icon camera: the whole employee, or a head-and-hat close-up with room above for tall hats.
        static Camera ItemCamera(Transform parent, Vector3 pos, RenderTexture rt, bool head)
        {
            var cam = head
                ? MakeCamera(parent, pos + new Vector3(0f, 0.04f, -0.68f), 2f, 28f, rt, PortraitBack)
                : MakeCamera(parent, pos + new Vector3(0f, 0f, -0.46f), 2.35f, 28f, rt, PortraitBack);
            cam.GetUniversalAdditionalCameraData().renderShadows = false;  // small icons: the shadow pass costs more than it shows
            return cam;
        }

        // The app icon: the employee, glasses on and running on coffee, on a corporate-blue sunburst. Saved as PNG.
        public static IEnumerator AppIcon(string path, int size = 1024)
        {
            var rig = new GameObject("App Icon Rig");
            var pos = Origin + new Vector3(0f, 60f, 0f);
            var target = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            var cam = MakeCamera(rig.transform, pos + new Vector3(0f, 0.02f, -0.5f), 2.05f, 30f, target, Themes.Hex(0x3B5BD9));
            cam.enabled = true;
            cam.farClipPlane = 30f;  // the sunburst sits 8 units behind
            cam.transform.rotation = Quaternion.Euler(-30f, 0f, 0f);
            cam.transform.position = pos + new Vector3(0f, 0.02f, -0.5f) - cam.transform.forward * 2.05f;

            // A corporate-blue sunburst behind everything (the warm skin and white shirt pop against it).
            var back = LevelBuilder.MakeSprite("Sunburst", Textures.Sunburst, new Color(0.62f, 0.78f, 1f), rig.transform, Vector3.zero, Vector2.one, -50);
            back.transform.rotation = cam.transform.rotation;
            back.transform.position = cam.transform.position + cam.transform.forward * 8f;
            float viewHeight = 2f * 8f * Mathf.Tan(15f * Mathf.Deg2Rad);
            back.transform.localScale = Vector3.one * viewHeight * 1.05f;

            // Soft round shadow under the employee.
            LevelBuilder.MakeSprite("Shadow", Textures.Glow, new Color(0.12f, 0.05f, 0.3f, 0.45f), rig.transform, pos + new Vector3(0f, 0.05f, -0.005f), Vector2.one * 1.1f, 0);

            var puck = LevelBuilder.MakeMascot(rig.transform, pos, Cosmetics.Compose(Cosmetics.Default));
            var m = puck.GetComponent<Mascot>();
            m.ViewCamera = cam;
            m.Pose(Eyes.Tired, Mouth.Smile);
            // A coffee in one corner and a sleepy Z in the other.
            var cup = LevelBuilder.MakeSprite("Coffee", UiArt.Cup, Color.white, rig.transform, Vector3.zero, Vector2.one * 0.14f, 30);
            cup.transform.position = pos + new Vector3(-0.4f, -0.12f, -0.3f);
            cup.transform.rotation = cam.transform.rotation * Quaternion.Euler(0f, 0f, 8f);
            var z = LevelBuilder.MakeSprite("Z", MascotArt.Z, Color.white, rig.transform, Vector3.zero, Vector2.one * 0.22f, 30);
            z.transform.position = pos + new Vector3(0.36f, 0.12f, -0.92f);
            z.transform.rotation = cam.transform.rotation * Quaternion.Euler(0f, 0f, -12f);

            yield return null;
            yield return null;
            yield return new WaitForEndOfFrame();
            var tex = ReadBack(target, true);
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Destroy(rig);
            target.Release();
            Debug.Log("HSICON saved " + path);
        }

        // The title screen hero: almost from the front, so the employee stands up like a character on a stage.
        static Camera HeroCamera(Transform parent, Vector3 pos, RenderTexture rt)
        {
            var focus = pos + new Vector3(0f, 0f, -0.56f);  // room above the head for tall and wide hats (OUT-29)
            var cam = MakeCamera(parent, focus, 2.65f, 26f, rt, new Color(0f, 0f, 0f, 0f));
            cam.transform.rotation = Quaternion.Euler(-72f, 0f, 0f);
            cam.transform.position = focus - cam.transform.forward * 2.65f;
            return cam;
        }

        // The same hero render, done at once and read back (for editor review shots).
        public static Texture2D RenderHeroNow(SkinDef look)
        {
            var rig = new GameObject("Hero Rig");
            var rt = new RenderTexture(HeroSize, HeroSize, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            try
            {
                var cam = HeroCamera(rig.transform, Origin, rt);
                Pose(rig.transform, Origin, look, cam);
                cam.Render();
                return ReadBack(rt, false);
            }
            finally
            {
                rt.Release();
                Remove(rig);
            }
        }

        // A head close-up, read back, for the editor's sheet of faces (OUT-42).
        public static Texture2D RenderFaceNow(SkinDef look, int size = ItemSize)
        {
            var rig = new GameObject("Face Rig");
            var rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            try
            {
                var cam = ItemCamera(rig.transform, Origin, rt, true);
                Pose(rig.transform, Origin, look, cam);
                cam.Render();
                return ReadBack(rt, false);
            }
            finally
            {
                rt.Release();
                Remove(rig);
            }
        }

        // Every item icon at once, read back, for editor review shots.
        public static void RenderItemsNow(Outfit outfit)
        {
            foreach (var item in Cosmetics.All)
            {
                var rig = new GameObject("Item Rig");
                var rt = new RenderTexture(ItemSize, ItemSize, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                try
                {
                    var cam = ItemCamera(rig.transform, Origin, rt, item.Slot == Slot.Hat || item.Slot == Slot.Face);
                    Pose(rig.transform, Origin, Cosmetics.Compose(outfit.With(item)), cam);
                    cam.Render();
                    var cut = RenderTexture.GetTemporary(ItemSize, ItemSize, 0, RenderTextureFormat.ARGB32);
                    Graphics.Blit(rt, cut, Corners);
                    if (ItemIcons.TryGetValue(item.Id, out var old) && old != null && !(old is RenderTexture)) DestroyImmediate(old);
                    ItemIcons[item.Id] = ReadBack(cut, false);
                    RenderTexture.ReleaseTemporary(cut);
                }
                finally
                {
                    rt.Release();
                    Remove(rig);
                }
            }
            Rendered?.Invoke();
        }

        // A camera that renders only when asked, and sees only what stands within a step of its target.
        static Camera MakeCamera(Transform parent, Vector3 target, float distance, float fov, RenderTexture rt, Color back)
        {
            var go = new GameObject("Icon Camera");
            go.transform.SetParent(parent, false);
            var cam = go.AddComponent<Camera>();
            cam.enabled = false;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = back;
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = distance + 1.5f;
            cam.allowHDR = false;
            cam.depth = -10f;
            cam.targetTexture = rt;
            // A little more from the front than the game camera, so the face reads.
            go.transform.rotation = Quaternion.Euler(-58f, 0f, 0f);
            go.transform.position = target - go.transform.forward * distance;
            return cam;
        }

        static Texture2D ReadBack(RenderTexture rt, bool opaque)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            if (opaque)
            {
                var px = tex.GetPixels32();
                for (int i = 0; i < px.Length; i++) px[i].a = 255;
                tex.SetPixels32(px);
            }
            tex.Apply();
            RenderTexture.active = previous;
            return tex;
        }
    }
}
