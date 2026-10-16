using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace OutOfOffice
{
    // Renders Puffy portraits: the big title cut-out of the current outfit, and a small icon for every wardrobe item
    // (Puffy wearing that item). Rigs are built far from the board, rendered by their own cameras for a frame,
    // read back, and removed. Also composes the app icon.
    public class IconStudio : MonoBehaviour
    {
        public static Texture2D HeroIcon;  // big cut-out with a transparent background, for the title screen
        public static readonly Dictionary<string, Texture2D> ItemIcons = new Dictionary<string, Texture2D>();
        public static System.Action Rendered;
        static readonly Vector3 Origin = new Vector3(400f, 400f, 0f);
        public static readonly Color PortraitBack = new Color(0.82f, 0.91f, 1f);
        static int slot;

        class Job
        {
            public string Key;
            public SkinDef Look;
            public int Size;
            public bool Hero, Head;
        }

        public static void RenderHero(SkinDef look) =>
            Run(new List<Job> { new Job { Key = "hero", Look = look, Size = HeroSize, Hero = true } });

        // One icon per wardrobe item: the player's own employee in their current outfit, with that one item swapped in,
        // so every tile shows what it would look like on you (OUT-29). Hats and face items are framed on the head,
        // where the change is; staff and ties show the whole body. `force` re-renders after an outfit change.
        public static void RenderItems(Outfit outfit, bool force = false)
        {
            var jobs = new List<Job>();
            foreach (var item in Cosmetics.All)
            {
                if (!force && ItemIcons.ContainsKey(item.Id)) continue;
                var o = outfit.With(item);
                jobs.Add(new Job { Key = item.Id, Look = Cosmetics.Compose(o), Size = ItemSize, Head = item.Slot == Slot.Hat || item.Slot == Slot.Face });
            }
            if (jobs.Count > 0) Run(jobs);
        }

        const int ItemSize = 192;

        // The item icon camera: the whole employee, or a head-and-hat close-up with room above for tall hats.
        static Camera ItemCamera(Transform parent, Vector3 pos, RenderTexture rt, bool head) => head
            ? MakeCamera(parent, pos + new Vector3(0f, 0.04f, -0.68f), 2f, 28f, rt, PortraitBack)
            : MakeCamera(parent, pos + new Vector3(0f, 0f, -0.46f), 2.35f, 28f, rt, PortraitBack);

        static void Run(List<Job> jobs)
        {
            var go = new GameObject("Icon Studio");
            go.AddComponent<IconStudio>().StartCoroutine(RunRoutine(go, jobs));
        }

        static IEnumerator RunRoutine(GameObject owner, List<Job> jobs)
        {
            const int batch = 12;
            for (int start = 0; start < jobs.Count; start += batch)
            {
                int n = Mathf.Min(batch, jobs.Count - start);
                var rigs = new List<GameObject>();
                var targets = new RenderTexture[n];
                for (int i = 0; i < n; i++)
                {
                    var job = jobs[start + i];
                    int s = slot++ % 400;
                    var pos = Origin + new Vector3(s % 20 * 6f, s / 20 * 6f, 0f);
                    var rig = new GameObject("Icon Rig");
                    rigs.Add(rig);
                    var puck = LevelBuilder.MakeMascot(rig.transform, pos, job.Look);
                    targets[i] = new RenderTexture(job.Size, job.Size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                    Camera cam;
                    if (job.Hero) cam = HeroCamera(rig.transform, pos, targets[i]);
                    else cam = ItemCamera(rig.transform, pos, targets[i], job.Head);
                    var m = puck.GetComponent<Mascot>();
                    m.ViewCamera = cam;
                    m.Pose(Eyes.Open, Mouth.Smile);
                }
                yield return null;  // Puck and Mascot place the body and face
                yield return new WaitForEndOfFrame();
                for (int i = 0; i < n; i++)
                {
                    var job = jobs[start + i];
                    var tex = ReadBack(targets[i], false);
                    if (job.Hero)
                    {
                        if (HeroIcon != null) Destroy(HeroIcon);
                        HeroIcon = tex;
                    }
                    else
                    {
                        if (ItemIcons.TryGetValue(job.Key, out var old) && old != null) Destroy(old);
                        ItemIcons[job.Key] = tex;
                    }
                    targets[i].Release();
                }
                foreach (var r in rigs) Destroy(r);
                Rendered?.Invoke();
            }
            Destroy(owner);
        }

        // The app icon: the employee, glasses on and running on coffee, on a corporate-blue sunburst. Saved as PNG.
        public static IEnumerator AppIcon(string path, int size = 1024)
        {
            var rig = new GameObject("App Icon Rig");
            var pos = Origin + new Vector3(0f, 60f, 0f);
            var target = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 8 };
            var cam = MakeCamera(rig.transform, pos + new Vector3(0f, 0.02f, -0.5f), 2.05f, 30f, target, Themes.Hex(0x3B5BD9));
            cam.transform.rotation = Quaternion.Euler(-30f, 0f, 0f);
            cam.transform.position = pos + new Vector3(0f, 0.02f, -0.5f) - cam.transform.forward * 2.05f;

            // A corporate-blue sunburst behind everything (the warm skin and white shirt pop against it).
            var back = LevelBuilder.MakeSprite("Sunburst", Textures.Sunburst, new Color(0.62f, 0.78f, 1f), rig.transform, Vector3.zero, Vector2.one, -50);
            back.transform.rotation = cam.transform.rotation;
            back.transform.position = cam.transform.position + cam.transform.forward * 8f;
            float viewHeight = 2f * 8f * Mathf.Tan(15f * Mathf.Deg2Rad);
            back.transform.localScale = Vector3.one * viewHeight * 1.05f;

            // Soft round shadow under Puffy.
            var shadow = LevelBuilder.MakeSprite("Shadow", Textures.Glow, new Color(0.12f, 0.05f, 0.3f, 0.45f), rig.transform, pos + new Vector3(0f, 0.05f, -0.005f), Vector2.one * 1.1f, 0);

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

        const int HeroSize = 768;

        // The title screen hero: almost from the front, so the employee stands up like a character on a stage.
        static Camera HeroCamera(Transform parent, Vector3 pos, RenderTexture rt)
        {
            var focus = pos + new Vector3(0f, 0f, -0.47f);  // room above the head for tall hats (OUT-29)
            var cam = MakeCamera(parent, focus, 2.4f, 26f, rt, new Color(0f, 0f, 0f, 0f));
            cam.transform.rotation = Quaternion.Euler(-72f, 0f, 0f);
            cam.transform.position = focus - cam.transform.forward * 2.4f;
            return cam;
        }

        // The same hero render, done at once (for editor review shots, where coroutines do not run).
        public static Texture2D RenderHeroNow(SkinDef look)
        {
            var rig = new GameObject("Hero Rig");
            var pos = Origin + new Vector3(0f, -40f, 0f);
            var rt = new RenderTexture(HeroSize, HeroSize, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            try
            {
                var cam = HeroCamera(rig.transform, pos, rt);
                var m = LevelBuilder.MakeMascot(rig.transform, pos, look).GetComponent<Mascot>();
                m.ViewCamera = cam;
                m.Mode = MascotMode.Idle;
                m.Pose(Eyes.Open, Mouth.Smile);
                for (int i = 0; i < 8; i++) m.TickPresentation(1f / 60f);
                cam.Render();
                return ReadBack(rt, false);
            }
            finally
            {
                rt.Release();
                DestroyImmediate(rig);
            }
        }

        // Every item icon at once, for editor review shots (coroutines don't run there).
        public static void RenderItemsNow(Outfit outfit)
        {
            foreach (var item in Cosmetics.All)
            {
                var rig = new GameObject("Item Rig");
                var pos = Origin + new Vector3(0f, -60f, 0f);
                var rt = new RenderTexture(ItemSize, ItemSize, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                try
                {
                    var cam = ItemCamera(rig.transform, pos, rt, item.Slot == Slot.Hat || item.Slot == Slot.Face);
                    var m = LevelBuilder.MakeMascot(rig.transform, pos, Cosmetics.Compose(outfit.With(item))).GetComponent<Mascot>();
                    m.ViewCamera = cam;
                    m.Mode = MascotMode.Idle;
                    m.Pose(Eyes.Open, Mouth.Smile);
                    for (int i = 0; i < 8; i++) m.TickPresentation(1f / 60f);
                    cam.Render();
                    if (ItemIcons.TryGetValue(item.Id, out var old) && old != null) DestroyImmediate(old);
                    ItemIcons[item.Id] = ReadBack(rt, false);
                }
                finally
                {
                    rt.Release();
                    DestroyImmediate(rig);
                }
            }
            Rendered?.Invoke();
        }

        static Camera MakeCamera(Transform parent, Vector3 target, float distance, float fov, RenderTexture rt, Color back)
        {
            var go = new GameObject("Icon Camera");
            go.transform.SetParent(parent, false);
            var cam = go.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = back;
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 30f;
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
