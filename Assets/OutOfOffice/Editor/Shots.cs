using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace OutOfOffice.EditorTools
{
    // Renders representative frames with the game's camera and lighting, for visual review outside Play mode:
    // Unity -batchmode -executeMethod OutOfOffice.EditorTools.Shots.RunBatch  (writes PNGs to OOO_SHOTS or <temp>/out-of-office-shots)
    public static class Shots
    {
        const float CameraTilt = 34f, CameraFov = 36f;

        public static void RunBatch()
        {
            int code = 0;
            try { RenderAll(); }
            catch (System.Exception e) { Debug.LogException(e); code = 3; }
            EditorApplication.Exit(code);
        }

        [MenuItem("Out of Office/Render Review Shots")]
        public static void RenderAll()
        {
            string dir = System.Environment.GetEnvironmentVariable("OOO_SHOTS");
            if (string.IsNullOrEmpty(dir)) dir = Path.Combine(Path.GetTempPath(), "out-of-office-shots");
            Directory.CreateDirectory(dir);
            var oldAmbient = RenderSettings.ambientLight;
            var oldMode = RenderSettings.ambientMode;
            try
            {
                foreach (var id in new[] { "first", "easy", "kicker", "rubber", "pinball", "door", "ring", "figure8", "hairpin", "crossroads" })
                    LevelShot(id, dir, false);
                foreach (var id in new[] { "ring", "figure8", "hairpin", "crossroads" })
                    LevelShot(id, dir, true);
                TitleShot(dir);
                Closeups(dir);
                Icon(Path.Combine(dir, "icon.png"));
            }
            finally
            {
                RenderSettings.ambientMode = oldMode;
                RenderSettings.ambientLight = oldAmbient;
            }
            Debug.Log("OOO SHOTS done " + dir);
        }

        static int IndexOf(string id)
        {
            for (int i = 0; i < Levels.All.Length; i++) if (Levels.All[i].Id == id) return i;
            return 0;
        }

        static (GameObject root, Camera cam) Stage(Theme theme)
        {
            var root = new GameObject("Shot Stage");
            var cam = new GameObject("Shot Camera").AddComponent<Camera>();
            cam.transform.SetParent(root.transform);
            cam.enabled = false;
            cam.fieldOfView = CameraFov;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 250f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = theme.Ground;
            cam.transform.rotation = Quaternion.Euler(-CameraTilt, 0f, 0f);
            var sun = new GameObject("Shot Sun").AddComponent<Light>();
            sun.transform.SetParent(root.transform);
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.55f;
            sun.color = theme.Sun;
            sun.intensity = theme.SunIntensity;
            sun.transform.rotation = Quaternion.LookRotation(new Vector3(0.4f, -0.5f, 1f), Vector3.up);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = theme.Ambient;
            var probe = new SphericalHarmonicsL2();
            probe.AddAmbientLight(theme.Ambient);
            RenderSettings.ambientProbe = probe;
            return (root, cam);
        }

        // A phone-shaped frame of a level at its start, framed as in the game (at most 9.6 units across, following the
        // employee). With `overview`, the whole board fits instead.
        static void LevelShot(string id, string dir, bool overview)
        {
            int index = IndexOf(id);
            var def = Levels.All[index];
            var theme = Themes.ForLevel(index);
            var (root, cam) = Stage(theme);
            try
            {
                var inst = LevelBuilder.Build(def, root.transform, true, Cosmetics.Compose(Cosmetics.Default), theme);
                const int w = 590, h = 1278;
                cam.aspect = w / (float)h;
                Rect floor = Geometry.Bounds(def.Shape.Outline);
                Vector3 forward = cam.transform.forward;
                float half = overview ? floor.width / 2f + 0.25f : Mathf.Min(floor.width + 0.5f, 9.6f) / 2f;
                float lo = 4f, hi = 150f;
                for (int i = 0; i < 32; i++)
                {
                    float d = (lo + hi) * 0.5f;
                    cam.transform.position = new Vector3(floor.center.x, floor.center.y, 0f) - forward * d;
                    float row = GroundAt(cam, new Vector2(0.5f, 0.18f)).y;
                    Vector3 l = cam.WorldToViewportPoint(new Vector3(floor.center.x - half, row, -Rules.WallHeight));
                    Vector3 r = cam.WorldToViewportPoint(new Vector3(floor.center.x + half, row, -Rules.WallHeight));
                    bool fits = l.x >= 0.005f && r.x <= 0.995f;
                    if (overview && fits)
                    {
                        float top = cam.WorldToViewportPoint(new Vector3(floor.center.x, floor.yMax, -Rules.WallHeight)).y;
                        float bottom = cam.WorldToViewportPoint(new Vector3(floor.center.x, floor.yMin, 0f)).y;
                        fits = top <= 0.97f && bottom >= 0.03f;
                    }
                    if (fits) hi = d; else lo = d;
                }
                Vector2 look = overview ? floor.center : new Vector2(
                    Mathf.Clamp(def.Start.x, floor.xMin - 0.25f + half, Mathf.Max(floor.xMin - 0.25f + half, floor.xMax + 0.25f - half)),
                    def.Start.y + 3f);
                if (!overview && floor.width + 0.5f <= 9.6f) look = new Vector2(floor.center.x, Mathf.Lerp(def.Start.y, def.Hole.y, 0.35f));
                cam.transform.position = new Vector3(look.x, look.y, 0f) - forward * hi;
                Animate(inst, cam);
                Render(cam, w, h, Path.Combine(dir, (overview ? "map-" : "level-") + id + ".png"));
            }
            finally { Object.DestroyImmediate(root); }
        }

        // The title screen over the first board, at an iPhone's size and safe area.
        static void TitleShot(string dir)
        {
            var theme = Themes.ForLevel(0);
            var (root, cam) = Stage(theme);
            Hud hud = null;
            try
            {
                var inst = LevelBuilder.Build(Levels.All[0], root.transform, true, Cosmetics.Compose(Cosmetics.Default), theme);
                Rect b = inst.Bounds;
                Aim(cam, new Vector3(b.center.x, b.center.y, 0f), 24f);
                inst.Puck.gameObject.SetActive(false);
                IconStudio.HeroIcon = IconStudio.RenderHeroNow(Cosmetics.Compose(Cosmetics.Default));
                hud = Hud.Create();
                hud.PreviewLayout(cam, 0.055f, 0.04f);
                hud.UnlockedCount = () => 9;
                hud.CurrentLevel = () => 8;
                hud.BestStars = id => id == "first" || id == "easy" ? 3 : id == "bank" || id == "ring" ? 2 : 0;
                hud.SetQuestBadge(2);
                hud.UntilQuestReset = () => new System.TimeSpan(7, 42, 0);
                hud.ShowTitle(23, 9, theme);
                Tweens.FinishAll();
                Render(cam, 1080, 2340, Path.Combine(dir, "title.png"), Canvas.ForceUpdateCanvases);

                hud.HideTitle();
                hud.SetLevel(9, "Infinite Loop", "CROSS THE BRIDGE IN THE MIDDLE", theme);
                hud.SetShots(1, 3);
                hud.SetStars(2, 3, false);
                hud.SetUndoAvailable(true);
                Tweens.FinishAll();
                Render(cam, 1080, 2340, Path.Combine(dir, "ui-hud.png"), Canvas.ForceUpdateCanvases);

                hud.ShowComplete(9, 3, 2, 3, true);
                Tweens.FinishAll();
                Render(cam, 1080, 2340, Path.Combine(dir, "ui-complete.png"), Canvas.ForceUpdateCanvases);

                hud.ShowComplete(Levels.All.Length, 2, 3, 3, false, true);
                Tweens.FinishAll();
                Render(cam, 1080, 2340, Path.Combine(dir, "ui-complete-final.png"), Canvas.ForceUpdateCanvases);

                // Home with the first chapter's chest earned and waiting.
                hud.BestStars = id => 3;
                hud.ChapterChestClaimed = c => false;
                hud.ShowTitle(Levels.All.Length * 3, 5, Themes.All[0]);
                Tweens.FinishAll();
                Render(cam, 1080, 2340, Path.Combine(dir, "title-chapter-chest.png"), Canvas.ForceUpdateCanvases);
                hud.HideTitle();

                hud.ShowLevels();
                Tweens.FinishAll();
                Render(cam, 1080, 2340, Path.Combine(dir, "ui-levels.png"), Canvas.ForceUpdateCanvases);
            }
            finally
            {
                if (hud != null) Object.DestroyImmediate(hud.gameObject);
                Object.DestroyImmediate(root);
            }
        }

        // Lets the cosmetic rigs settle for a few frames: the employee's face and pose, coworker faces.
        static void Animate(LevelInstance inst, Camera cam)
        {
            var mascot = inst.Puck.GetComponent<Mascot>();
            mascot.ViewCamera = cam;
            mascot.Mode = MascotMode.Idle;
            mascot.Pose(Eyes.Tired, Mouth.Smile);
            for (int i = 0; i < 10; i++)
            {
                mascot.TickPresentation(1f / 60f);
                foreach (var f in inst.Root.GetComponentsInChildren<CoworkerFace>()) f.Tick(cam);
            }
        }

        static void Closeups(string dir)
        {
            var theme = Themes.All[0];
            var (root, cam) = Stage(theme);
            try
            {
                cam.backgroundColor = Themes.Hex(0xE9E2D6);
                var floor = GameObject.CreatePrimitive(PrimitiveType.Quad);
                floor.transform.SetParent(root.transform);
                floor.transform.localScale = Vector3.one * 40f;
                floor.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                floor.GetComponent<MeshRenderer>().sharedMaterial = Look.Flat(Themes.Hex(0xE4DACB), 0.2f);

                // The employee in each look, standing, aiming, tucked mid-roll, and asleep.
                var outfits = new[]
                {
                    Cosmetics.Default,
                    new Outfit { Employee = "e.leo", Color = "c.cherry", Hat = "h.tuft", Face = "f.none" },
                    new Outfit { Employee = "e.mia", Color = "c.puffy", Hat = "h.tuft", Face = "f.none" },
                    new Outfit { Employee = "e.sam", Color = "c.minty", Hat = "h.headphones", Face = "f.glasses" },
                };
                for (int o = 0; o < outfits.Length; o++)
                {
                    var p = LevelBuilder.MakeMascot(root.transform, new Vector3(o * 1.1f - 1.65f, 0f, 0f), Cosmetics.Compose(outfits[o]));
                    var m = p.GetComponent<Mascot>();
                    m.ViewCamera = cam;
                    m.Mode = MascotMode.Idle;
                    m.Pose(o == 1 ? Eyes.Open : o == 2 ? Eyes.Happy : Eyes.Tired, o == 2 ? Mouth.Grin : Mouth.Smile);
                    for (int i = 0; i < 4; i++) m.TickPresentation(1f / 60f);
                }
                Aim(cam, new Vector3(0f, 0.1f, -0.4f), 6.2f);
                Render(cam, 1200, 700, Path.Combine(dir, "employees.png"));
                foreach (Transform c in root.transform) if (c.name == "Puck") Object.DestroyImmediate(c.gameObject);
                for (int i = root.transform.childCount - 1; i >= 0; i--)
                    if (root.transform.GetChild(i).name == "Puck") Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

                // Poses of the default employee.
                for (int pose = 0; pose < 4; pose++)
                {
                    var p = LevelBuilder.MakeMascot(root.transform, new Vector3(pose * 1.1f - 1.65f, 0f, 0f), Cosmetics.Compose(Cosmetics.Default));
                    var m = p.GetComponent<Mascot>();
                    m.ViewCamera = cam;
                    switch (pose)
                    {
                        case 0: m.Mode = MascotMode.Aiming; m.AimDir = Vector2.up; m.AimPower = 0.8f; break;
                        case 1:
                            m.Mode = MascotMode.Moving;
                            p.Body.simulated = true;
                            m.Pose(Eyes.Wide, Mouth.O);
                            break;
                        case 2: m.Mode = MascotMode.Idle; m.Pose(Eyes.Squeeze, Mouth.Flat); break;
                        default: m.Sleep(true); break;
                    }
                    m.TickPresentation(1f / 60f);
                    for (int i = 0; i < 30; i++)
                    {
                        if (pose == 1) p.transform.position += Vector3.right * 0.004f;
                        m.TickPresentation(1f / 60f);
                    }
                }
                Render(cam, 1200, 700, Path.Combine(dir, "poses.png"));
                for (int i = root.transform.childCount - 1; i >= 0; i--)
                    if (root.transform.GetChild(i).name == "Puck") Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

                // Every hat and face item on the default employee, to check each one sits on the new head.
                var items = Cosmetics.All.FindAll(it => it.Slot == Slot.Hat || it.Slot == Slot.Face);
                const int perRow = 7;
                int rows = (items.Count + perRow - 1) / perRow;
                for (int i = 0; i < items.Count; i++)
                {
                    var o = new Outfit { Employee = "e.ava", Color = "c.bluebell", Hat = "h.tuft", Face = "f.none" };
                    if (items[i].Slot == Slot.Hat) o.Hat = items[i].Id; else o.Face = items[i].Id;
                    var p = LevelBuilder.MakeMascot(root.transform, new Vector3((i % perRow - (perRow - 1) * 0.5f) * 1.05f, ((rows - 1) * 0.5f - i / perRow) * 1.25f, 0f), Cosmetics.Compose(o));
                    var m = p.GetComponent<Mascot>();
                    m.ViewCamera = cam;
                    m.Mode = MascotMode.Idle;
                    m.Pose(Eyes.Open, Mouth.Smile);
                    for (int f = 0; f < 4; f++) m.TickPresentation(1f / 60f);
                }
                Aim(cam, new Vector3(0f, 0f, -0.4f), 3.2f + rows * 1.9f);
                Render(cam, 3200, 2200, Path.Combine(dir, "wardrobe.png"));
                for (int i = root.transform.childCount - 1; i >= 0; i--)
                    if (root.transform.GetChild(i).name == "Puck") Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

                // Obstacles: the swivel chair, the boss's chair, and a pacing coworker.
                var props = new LevelDef("props", "Props", 0f, 6f);
                props.Post(BlockKind.Bouncer, -1.5f, 0f, 0.5f);
                props.Kicker(0f, 0f, 0.5f);
                props.SpikeBall(1.5f, 0f, 0.4f).Moving(0.01f, 0f, 3f);
                var inst = LevelBuilder.Build(props, root.transform, false, Cosmetics.Compose(Cosmetics.Default), theme);
                inst.Puck.gameObject.SetActive(false);
                foreach (var f in inst.Root.GetComponentsInChildren<CoworkerFace>()) f.Tick(cam);
                Aim(cam, new Vector3(0f, 0f, -0.3f), 5.8f);
                Render(cam, 1200, 700, Path.Combine(dir, "obstacles.png"));
                Object.DestroyImmediate(inst.Root);

                // The slingshot at a full pull and a half pull, as OutOfOfficeGame lays it out.
                for (int s = 0; s < 2; s++)
                {
                    float power = s == 0 ? 1f : 0.45f;
                    Vector2 rest = new Vector2(s == 0 ? -1.2f : 1.2f, 0.4f), dir2 = s == 0 ? Vector2.up : new Vector2(0.5f, 0.87f).normalized;
                    Vector2 pulled = -dir2 * (Mathf.Sqrt(power) * 0.6f);
                    var p = LevelBuilder.MakeMascot(root.transform, rest, Cosmetics.Compose(Cosmetics.Default));
                    var puck = p.GetComponent<Puck>();
                    puck.VisualOffset = pulled;
                    puck.AimSquash = power * 0.08f;
                    puck.AimAxis = dir2;
                    var m = p.GetComponent<Mascot>();
                    m.ViewCamera = cam;
                    m.Mode = MascotMode.Aiming;
                    m.AimDir = dir2;
                    m.AimPower = power;
                    var sling = new Slingshot(root.transform);
                    for (int i = 0; i < 30; i++)
                    {
                        sling.Aim(rest, dir2, rest + pulled - dir2 * (Rules.PuckRadius + 0.05f), power, 1f / 60f);
                        m.TickPresentation(1f / 60f);
                    }
                    puck.Visual.localPosition = new Vector3(pulled.x, pulled.y, 0f);
                }
                cam.transform.rotation = Quaternion.Euler(-CameraTilt, 0f, 0f);
                cam.transform.position = new Vector3(0f, 0.2f, -0.2f) - cam.transform.forward * 6.5f;
                Render(cam, 1200, 800, Path.Combine(dir, "slingshot.png"));
            }
            finally { Object.DestroyImmediate(root); }
        }

        // The app icon, as IconStudio.AppIcon frames it, rendered synchronously so it can be refreshed in batch mode.
        static void Icon(string path)
        {
            var (root, cam) = Stage(Themes.All[0]);
            try
            {
                cam.fieldOfView = 30f;
                cam.backgroundColor = Themes.Hex(0x3B5BD9);
                cam.transform.rotation = Quaternion.Euler(-30f, 0f, 0f);
                var focus = new Vector3(0f, 0.02f, -0.5f);
                cam.transform.position = focus - cam.transform.forward * 2.05f;
                var back = LevelBuilder.MakeSprite("Sunburst", Textures.Sunburst, new Color(0.62f, 0.78f, 1f), root.transform, Vector3.zero, Vector2.one, -50);
                back.transform.rotation = cam.transform.rotation;
                back.transform.position = cam.transform.position + cam.transform.forward * 8f;
                back.transform.localScale = Vector3.one * 2f * 8f * Mathf.Tan(15f * Mathf.Deg2Rad) * 1.05f;
                LevelBuilder.MakeSprite("Shadow", Textures.Glow, new Color(0.12f, 0.05f, 0.3f, 0.45f), root.transform, new Vector3(0f, 0.05f, -0.005f), Vector2.one * 1.1f, 0);
                var puck = LevelBuilder.MakeMascot(root.transform, Vector3.zero, Cosmetics.Compose(Cosmetics.Default));
                var m = puck.GetComponent<Mascot>();
                m.ViewCamera = cam;
                m.Mode = MascotMode.Idle;
                m.Pose(Eyes.Tired, Mouth.Smile);
                for (int i = 0; i < 6; i++) m.TickPresentation(1f / 60f);
                var cup = LevelBuilder.MakeSprite("Coffee", UiArt.Cup, Color.white, root.transform, Vector3.zero, Vector2.one * 0.14f, 30);
                cup.transform.position = new Vector3(-0.4f, -0.12f, -0.3f);
                cup.transform.rotation = cam.transform.rotation * Quaternion.Euler(0f, 0f, 8f);
                var z = LevelBuilder.MakeSprite("Z", MascotArt.Z, Color.white, root.transform, Vector3.zero, Vector2.one * 0.22f, 30);
                z.transform.position = new Vector3(0.36f, 0.12f, -0.92f);
                z.transform.rotation = cam.transform.rotation * Quaternion.Euler(0f, 0f, -12f);
                Render(cam, 1024, 1024, path);
            }
            finally { Object.DestroyImmediate(root); }
        }

        static void Aim(Camera cam, Vector3 target, float distance)
        {
            cam.transform.rotation = Quaternion.Euler(-CameraTilt, 0f, 0f);
            cam.transform.position = target - cam.transform.forward * distance;
        }

        static Vector3 GroundAt(Camera cam, Vector2 viewport)
        {
            Ray ray = cam.ViewportPointToRay(new Vector3(viewport.x, viewport.y, 0f));
            return ray.origin + ray.direction * (-ray.origin.z / ray.direction.z);
        }

        static void Render(Camera camera, int w, int h, string path, System.Action beforeRender = null)
        {
            var target = new RenderTexture(w, h, 24) { antiAliasing = 4 };
            var previous = RenderTexture.active;
            var image = new Texture2D(w, h, TextureFormat.RGB24, false);
            try
            {
                camera.aspect = w / (float)h;
                camera.targetTexture = target;
                beforeRender?.Invoke();
                camera.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = previous;
                target.Release();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(image);
            }
        }
    }
}
