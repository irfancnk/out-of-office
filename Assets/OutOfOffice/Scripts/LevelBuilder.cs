using System.Collections.Generic;
using UnityEngine;

namespace OutOfOffice
{
    // Turns a LevelDef into GameObjects: flat 2D physics plus 3D toy-style meshes.
    // Every visual outline comes from the same numbers as its collider, so what you see is what you hit.
    public static class LevelBuilder
    {
        const float ShadowZ = -0.004f;
        static PhysicsMaterial2D puckMat, woodMat, rubberMat, softMat, crateMat, crystalMat;

        public static LevelInstance Build(LevelDef def, Transform parent, bool decorate = true, int skin = 0, Theme theme = null) =>
            Build(def, parent, decorate, Skins.All[Mathf.Clamp(skin, 0, Skins.All.Length - 1)], theme);

        public static LevelInstance Build(LevelDef def, Transform parent, bool decorate, SkinDef look, Theme theme)
        {
            EnsureMaterials();
            if (theme != null) Themes.Current = theme;
            theme = Themes.Current;
            Physics2D.bounceThreshold = Rules.BounceThreshold;

            var root = new GameObject("Level " + def.Name);
            root.transform.SetParent(parent, false);
            var inst = new LevelInstance { Def = def, Root = root };
            var outline = def.Shape.Outline;

            // The tray: the chapter's floor (play mat, lawn, sand, or knit), rim, soft shade along the inside of the rim.
            Look.AddMesh("Floor", Meshes.Floor(outline, def.Hole, 0f), Look.Flat(Color.white, theme.FloorGloss, theme.Floor()),
                root.transform, Vector3.zero);
            Look.AddMesh("Rim", Meshes.Rim(outline, Rules.WallThickness, Rules.WallHeight, 0.2f, Rules.BaseDepth),
                Look.Lit(theme.Frame, 0.5f, theme.FrameTexture()), root.transform, Vector3.zero);
            Look.AddShadow("Rim Shade", Meshes.SoftShadow(outline, -0.5f, 0.24f), root.transform, new Vector3(0f, 0f, -0.002f));

            Geometry.Miters(outline, out var normals, out var miter);
            var outer = new List<Vector2>();
            for (int i = 0; i < outline.Count; i++) outer.Add(outline[i] + normals[i] * (Rules.WallThickness * miter[i]));
            inst.Bounds = Geometry.Bounds(outer);

            // The tray's soft drop shadow on the ground. The ground itself comes with the scenery.
            Look.AddShadow("Tray Shadow", Meshes.SoftShadow(outer, 1.6f, 0.38f), root.transform,
                new Vector3(0.25f, -0.35f, Rules.BaseDepth - 0.006f));  // above the rug and sea layers

            // The rim collider follows the floor outline exactly. One edge chain, so there are no seams to snag on.
            var boundary = new GameObject("Boundary");
            boundary.transform.SetParent(root.transform, false);
            var edge = boundary.AddComponent<EdgeCollider2D>();
            // The chain opens and closes in the middle of the longest straight edge, with ghost points on both
            // sides, so even the join is perfectly smooth.
            int longest = 0;
            for (int i = 1; i < outline.Count; i++)
                if ((outline[(i + 1) % outline.Count] - outline[i]).sqrMagnitude > (outline[(longest + 1) % outline.Count] - outline[longest]).sqrMagnitude) longest = i;
            Vector2 a0 = outline[longest], a1 = outline[(longest + 1) % outline.Count], mid = (a0 + a1) * 0.5f;
            var chain = new List<Vector2> { mid };
            for (int i = 1; i <= outline.Count; i++) chain.Add(outline[(longest + i) % outline.Count]);
            chain.Add(mid);
            edge.SetPoints(chain);
            edge.useAdjacentStartPoint = true;
            edge.adjacentStartPoint = a0;
            edge.useAdjacentEndPoint = true;
            edge.adjacentEndPoint = a1;
            edge.sharedMaterial = woodMat;
            boundary.AddComponent<BlockTag>().Init(BlockKind.Wall, null);

            foreach (var r in def.Bridges) AddBridge(root.transform, r);

            int colorIndex = 0;
            foreach (var b in def.Blocks) AddBlock(inst, b, ref colorIndex, theme);
            foreach (var r in def.Rails) AddRail(inst, r, theme);
            for (int i = 0; i < def.Zones.Count; i++) AddZone(inst, def.Zones[i], i);
            ConnectSwitches(inst);
            foreach (var s in def.Stars) AddStar(inst, s);
            AddHole(inst, def);

            Look.AddMesh("Start", Meshes.Ring(Rules.PuckRadius + 0.08f, Rules.PuckRadius + 0.17f), Look.Lit(Palette.StartMark, 0.3f),
                root.transform, (Vector3)def.Start + new Vector3(0f, 0f, -0.003f), false);

            inst.Puck = MakePuck(def, root.transform, look ?? Skins.All[0]);
            inst.Puck.Level = inst;
            if (decorate) Scenery.Build(inst, theme, outline, outer);
            Look.CombineStatic(root.transform, inst.Puck.transform);
            return inst;
        }

        // Wooden planks across the floor, with water on the ground on both sides. The rim is the railing.
        static void AddBridge(Transform parent, Rect r)
        {
            int planks = Mathf.Max(3, Mathf.RoundToInt(r.height / 0.46f));
            float step = r.height / planks;
            for (int i = 0; i < planks; i++)
            {
                var wood = Themes.Hex(i % 2 == 0 ? 0xD99A5B : 0xE6AC6E);
                Look.AddMesh("Plank", Meshes.RoundedPrism(r.width - 0.06f, step - 0.07f, 0.05f, 0.012f, 0.008f), Look.Lit(wood, 0.3f),
                    parent, new Vector3(r.center.x, r.yMin + step * (i + 0.5f), -0.001f), false);
            }
            var water = Look.Lit(Themes.Hex(0x3FA6F0), 0.6f);
            var foam = Look.Lit(Color.white, 0.4f);
            float side = r.width / 2f + Rules.WallThickness + 2.6f;
            foreach (float s in new[] { -1f, 1f })
            {
                // Just above the ground top (BaseDepth) and below the tray shadow (BaseDepth - 0.006).
                var c = new Vector3(r.center.x + s * side, r.center.y, Rules.BaseDepth - 0.002f);
                Look.AddMesh("Water", Meshes.RoundedPrism(5.2f, r.height + 5f, 1.8f, 0.001f, 0f, true, 8), water, parent, c, false);
                for (int k = 0; k < 3; k++)
                    Look.AddMesh("Ripple", Meshes.RoundedPrism(1.3f - k * 0.25f, 0.13f, 0.065f, 0.001f, 0f), foam, parent,
                        c + new Vector3(s * (0.4f + k * 0.5f), (k - 1) * 1.1f, -0.002f), false);
            }
        }

        static void EnsureMaterials()
        {
            if (puckMat != null) return;
            // The puck has no bounce and full friction, so each obstacle's material decides the contact
            // (Box2D uses the larger restitution and the geometric mean of the frictions).
            puckMat = new PhysicsMaterial2D("Puck") { bounciness = 0f, friction = 1f };
            woodMat = new PhysicsMaterial2D("Wood") { bounciness = Rules.WoodBounce, friction = Rules.WoodFriction };
            rubberMat = new PhysicsMaterial2D("Rubber") { bounciness = Rules.RubberBounce, friction = Rules.RubberFriction };
            softMat = new PhysicsMaterial2D("Soft") { bounciness = Rules.SoftBounce, friction = Rules.SoftFriction };
            crateMat = new PhysicsMaterial2D("Crate") { bounciness = Rules.CrateBounce, friction = Rules.CrateFriction };
            crystalMat = new PhysicsMaterial2D("Crystal") { bounciness = Rules.CrystalBounce, friction = Rules.CrystalFriction };
        }

        static PhysicsMaterial2D MaterialFor(BlockKind kind) =>
            kind == BlockKind.Bouncer || kind == BlockKind.Kicker ? rubberMat : kind == BlockKind.Sticky ? softMat : kind == BlockKind.Crate ? crateMat
            : kind == BlockKind.Breakable ? crystalMat : woodMat;

        static float HeightFor(BlockKind kind) =>
            kind == BlockKind.Hazard ? Rules.HazardHeight
            : kind == BlockKind.Bouncer || kind == BlockKind.Kicker ? Rules.BouncerHeight
            : kind == BlockKind.Sticky ? Rules.StickyHeight
            : kind == BlockKind.Crate ? 0.7f
            : Rules.WallHeight;

        static Material Paint(BlockKind kind, ref int colorIndex, int group, Theme theme)
        {
            switch (kind)
            {
                case BlockKind.Wall: return WallPaint(theme, theme.WallColors[colorIndex++ % theme.WallColors.Length]);
                case BlockKind.Hazard: return Look.Lit(Color.white, 0.75f, Textures.HazardStripes);
                case BlockKind.Breakable: return Look.Lit(Themes.Hex(0xE7C47C), 0.3f);  // a stack of manila folders
                case BlockKind.Bouncer: return Look.Lit(Palette.Rubber, 0.75f);  // glossy rubber
                case BlockKind.Kicker: return Look.Soft(Palette.Kicker, 0.8f, null, 0.25f);
                case BlockKind.PopWall: return Look.Toy(Palette.PopWall);
                case BlockKind.Sticky: return Look.Lit(Palette.Sticky, 0.95f);
                case BlockKind.Gate: return Look.Lit(Palette.GateColors[Mathf.Max(0, group) % Palette.GateColors.Length], 0.82f);
                case BlockKind.Crate: return Look.Lit(Palette.Crate, 0.25f);  // a cardboard archive box
                default: return Look.Toy(Palette.For(kind));
            }
        }

        // Wall paint in the zone's style. The collider never changes with the style.
        static Material WallPaint(Theme theme, Color c)
        {
            switch (theme.Walls)
            {
                case WallStyle.Partition: return Look.Lit(c, 0.1f, Textures.Fabric);
                case WallStyle.Counter: return Look.Lit(c, 0.6f);
                case WallStyle.Wood: return Look.Lit(Color.Lerp(c, Color.white, 0.2f), 0.45f, Textures.WoodFrame);
                case WallStyle.Cabinet: return Look.Lit(c, 0.75f);
                default: return Look.Lit(c, 0.55f);
            }
        }

        static Color TrimColor(Theme theme) =>
            theme.Walls == WallStyle.Painted ? Palette.Gold : theme.Walls == WallStyle.Counter ? Themes.Hex(0xF6F3EE)
            : theme.Walls == WallStyle.Wood ? Themes.Hex(0x5C3A26) : Themes.Hex(0xE4E7EC);

        // Office furniture dressing for a wall block: a work surface on top in the zone's style, and a few small props
        // laid out along it (monitors, mugs, paper, plants...). Props stay inside the footprint and low, so they never
        // read as obstacles, and they are static, so the level merges them into a handful of meshes.
        static void Furnish(Transform visual, Vector2 size, float height, Theme theme, Vector2 seed, float round)
        {
            float top = -height;
            bool along = size.x >= size.y;
            float length = Mathf.Max(size.x, size.y), width = Mathf.Min(size.x, size.y);
            // Work surface: slightly inset so the wall's edge still reads.
            Color surface = theme.Walls == WallStyle.Counter ? Themes.Hex(0xF6F3EE)
                : theme.Walls == WallStyle.Wood ? Themes.Hex(0x6B4430)
                : theme.Walls == WallStyle.Cabinet ? Themes.Hex(0xC9D0D6)
                : theme.Walls == WallStyle.Painted ? Themes.Hex(0x1F2740) : Themes.Hex(0xE7E3DC);
            var surfaceMat = Look.Lit(surface, theme.Walls == WallStyle.Painted ? 0.85f : 0.45f);
            if (round > 0f)
                Look.AddMesh("Top", Meshes.Lathe(Scenery.Cylinder(round - 0.06f, 0.03f), 40), surfaceMat, visual, new Vector3(0f, 0f, top), false);
            else if (width > 0.3f)
                Look.AddMesh("Top", Meshes.SoftBox(new Vector3(size.x - 0.1f, size.y - 0.1f, 0.03f), 0.012f), surfaceMat, visual, new Vector3(0f, 0f, top - 0.015f), false);
            if (theme.Walls == WallStyle.Painted && width > 0.3f && round < 0f)
                Look.AddMesh("Trim", Meshes.SoftBox(new Vector3(size.x - 0.04f, size.y - 0.04f, 0.012f), 0.006f), Look.Lit(Palette.Gold, 0.9f), visual, new Vector3(0f, 0f, top + 0.006f), false);
            if (width < 0.45f && round < 0f) return;  // too thin to carry props

            int h = Mathf.Abs(Mathf.RoundToInt(seed.x * 73f + seed.y * 191f));
            float spacing = 1.25f;
            int count = round > 0f ? Mathf.Clamp(Mathf.RoundToInt(round * 2f), 2, 6) : Mathf.Clamp(Mathf.FloorToInt((length - 0.3f) / spacing), 0, 8);
            for (int i = 0; i < count; i++)
            {
                Vector3 p;
                float turn;
                if (round > 0f)
                {
                    float a = (i + 0.5f) * Mathf.PI * 2f / count;
                    p = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * (round * 0.62f);
                    turn = a * Mathf.Rad2Deg + 90f;
                }
                else
                {
                    float t = (i - (count - 1) / 2f) * spacing;
                    p = along ? new Vector3(t, 0f, 0f) : new Vector3(0f, t, 0f);
                    turn = along ? 0f : 90f;
                }
                p.z = top - 0.03f;
                Prop(visual, theme.Walls, (h + i * 7) % 5, p, turn);
            }
        }

        static readonly Dictionary<int, Material> propMats = new Dictionary<int, Material>();

        static Material PropMat(int rgb, float gloss = 0.5f)
        {
            int key = rgb * 31 + Mathf.RoundToInt(gloss * 10f);
            if (!propMats.TryGetValue(key, out var m) || m == null) propMats[key] = m = Look.Lit(Themes.Hex(rgb), gloss);
            return m;
        }

        // One small desk prop. Kind picks from the zone's set; the base sits at p, facing `turn` degrees.
        static void Prop(Transform parent, WallStyle style, int kind, Vector3 p, float turn)
        {
            var holder = new GameObject("Prop").transform;
            holder.SetParent(parent, false);
            holder.localPosition = p;
            holder.localRotation = Quaternion.Euler(0f, 0f, turn);
            holder.localScale = Vector3.one * 1.6f;  // chunky enough to read at phone size
            void Box(Vector3 size, int color, Vector3 at, float gloss = 0.5f, float r = 0.015f) =>
                Look.AddMesh("Part", Meshes.SoftBox(size, r), PropMat(color, gloss), holder, at - new Vector3(0f, 0f, size.z / 2f), false);
            void Cyl(float radius, float hgt, int color, Vector3 at, float gloss = 0.5f) =>
                Look.AddMesh("Part", Meshes.Lathe(Scenery.Cylinder(radius, hgt), 14), PropMat(color, gloss), holder, at, false);
            void Ball(float radius, int color, Vector3 at, float gloss = 0.4f) =>
                Look.AddMesh("Part", Meshes.Lathe(Scenery.Sphere(radius), 12), PropMat(color, gloss), holder, at, false);

            switch (style)
            {
                case WallStyle.Counter:  // kitchen counter: mugs, a kettle, a fruit bowl, a toaster
                    switch (kind)
                    {
                        case 0: case 3: Cyl(0.07f, 0.14f, 0xFFFFFF, new Vector3(-0.1f, 0.02f, 0f)); Cyl(0.07f, 0.14f, 0xE86A5A, new Vector3(0.1f, -0.03f, 0f)); break;
                        case 1: Cyl(0.13f, 0.22f, 0xC9CED8, Vector3.zero, 0.9f); Ball(0.04f, 0x2B2630, new Vector3(0f, 0f, -0.24f)); break;
                        case 2: Cyl(0.18f, 0.08f, 0x3E9E8A, Vector3.zero); Ball(0.06f, 0xFFB300, new Vector3(-0.05f, 0f, -0.1f)); Ball(0.06f, 0xE84A3C, new Vector3(0.06f, 0.03f, -0.1f)); break;
                        default: Box(new Vector3(0.3f, 0.18f, 0.18f), 0xD8DCE3, Vector3.zero, 0.85f); break;
                    }
                    break;
                case WallStyle.Cabinet:  // HR: paper trays, folder stacks, a stamp
                    switch (kind)
                    {
                        case 0: case 3: for (int i = 0; i < 3; i++) Box(new Vector3(0.36f, 0.26f, 0.025f), i % 2 == 0 ? 0xF6F2E8 : 0xE7C47C, new Vector3(0.01f * i, 0f, -0.035f * i), 0.3f, 0.005f); break;
                        case 1: Box(new Vector3(0.4f, 0.3f, 0.05f), 0x5A6475, Vector3.zero, 0.6f); Box(new Vector3(0.34f, 0.25f, 0.02f), 0xF6F2E8, new Vector3(0f, 0f, -0.05f), 0.3f, 0.005f); break;
                        case 2: Cyl(0.05f, 0.1f, 0xD63A3A, Vector3.zero); Ball(0.05f, 0x2B2630, new Vector3(0f, 0f, -0.11f)); break;
                        default: Cyl(0.07f, 0.13f, 0x5E9E4A, Vector3.zero); break;
                    }
                    break;
                case WallStyle.Wood:  // meeting room / manager: laptops, notepads, a desk lamp, a water jug
                    switch (kind)
                    {
                        case 0: case 3: Box(new Vector3(0.38f, 0.26f, 0.02f), 0x9AA3B2, Vector3.zero, 0.8f); Box(new Vector3(0.38f, 0.02f, 0.22f), 0x2B2F3A, new Vector3(0f, 0.13f, -0.02f), 0.8f); break;
                        case 1: Box(new Vector3(0.24f, 0.32f, 0.02f), 0xFFE680, Vector3.zero, 0.3f, 0.005f); Cyl(0.012f, 0.012f, 0x2B2630, new Vector3(0.14f, 0f, -0.02f)); break;
                        case 2: Cyl(0.08f, 0.03f, 0x2E6B4F, Vector3.zero); Cyl(0.015f, 0.22f, 0xC9A45C, new Vector3(0f, 0f, -0.03f), 0.9f); Ball(0.08f, 0x2E8B5F, new Vector3(0f, -0.06f, -0.24f), 0.7f); break;
                        default: Cyl(0.07f, 0.22f, 0xBFE3F2, Vector3.zero, 0.95f); break;
                    }
                    break;
                case WallStyle.Painted:  // executive: a trophy, a fancy plant, a tidy stack of one report
                    switch (kind)
                    {
                        case 0: case 3: Cyl(0.06f, 0.06f, 0x2B2630, Vector3.zero, 0.8f); Cyl(0.03f, 0.12f, 0xC9A45C, new Vector3(0f, 0f, -0.06f), 0.95f); Ball(0.07f, 0xC9A45C, new Vector3(0f, 0f, -0.2f), 0.95f); break;
                        case 1: Cyl(0.11f, 0.13f, 0xF4F1EA, Vector3.zero, 0.6f); Ball(0.15f, 0x3E8A4F, new Vector3(0f, 0f, -0.15f)); break;
                        default: Box(new Vector3(0.24f, 0.32f, 0.02f), 0xFFFFFF, Vector3.zero, 0.3f, 0.005f); break;
                    }
                    break;
                default:  // open plan desks: a monitor and keyboard, a mug, a little plant, paper
                    switch (kind)
                    {
                        case 0: case 3:
                            Box(new Vector3(0.44f, 0.04f, 0.28f), 0x2B2F3A, new Vector3(0f, 0.1f, -0.06f), 0.85f);
                            Box(new Vector3(0.4f, 0.02f, 0.24f), 0x7FB3E8, new Vector3(0f, 0.078f, -0.08f), 0.95f, 0.005f);
                            Box(new Vector3(0.08f, 0.08f, 0.06f), 0x2B2F3A, new Vector3(0f, 0.11f, 0f), 0.6f);
                            Box(new Vector3(0.34f, 0.12f, 0.02f), 0xE9ECF2, new Vector3(0f, -0.1f, 0f), 0.5f, 0.008f);
                            break;
                        case 1: Cyl(0.065f, 0.13f, 0xFFFFFF, Vector3.zero); Ball(0.04f, 0x6B3F2A, new Vector3(0f, 0f, -0.12f)); break;
                        case 2: Cyl(0.08f, 0.1f, 0xD9784E, Vector3.zero); Ball(0.1f, 0x4FA35A, new Vector3(0f, 0f, -0.14f)); break;
                        default: for (int i = 0; i < 2; i++) Box(new Vector3(0.24f, 0.32f, 0.02f), 0xFBF8F1, new Vector3(0.02f * i, -0.02f * i, -0.022f * i), 0.3f, 0.005f); break;
                    }
                    break;
            }
        }

        static void AddBlock(LevelInstance inst, BlockDef b, ref int colorIndex, Theme theme)
        {
            var go = new GameObject(b.Kind.ToString());
            go.transform.SetParent(inst.Root.transform, false);
            go.transform.localPosition = b.Center;
            go.transform.localRotation = Quaternion.Euler(0f, 0f, b.Angle);
            float rc = b.CornerRadius;
            float height = HeightFor(b.Kind);

            Collider2D col;
            if (b.Shape == BlockShape.Post)
            {
                var circle = go.AddComponent<CircleCollider2D>();
                circle.radius = rc;
                col = circle;
            }
            else
            {
                var box = go.AddComponent<BoxCollider2D>();
                box.edgeRadius = rc;
                box.size = b.Size - Vector2.one * (2f * rc);
                col = box;
            }
            col.sharedMaterial = MaterialFor(b.Kind);
            inst.BlockColliders.Add(col);

            MeshRenderer visual;
            var paint = Paint(b.Kind, ref colorIndex, b.Group, theme);
            List<Vector2> footprint;
            if (b.Shape == BlockShape.Post)
            {
                if (b.Kind == BlockKind.Bouncer) visual = OfficeChair(go.transform, rc, height, false);
                else if (b.Kind == BlockKind.Kicker)
                {
                    visual = OfficeChair(go.transform, rc, height, true);
                    KickerDetails(go.transform, rc, height);
                }
                else if (b.Kind == BlockKind.Hazard) visual = b.Moves ? Coworker(go.transform, rc) : Cactus(go.transform, rc);
                else
                {
                    float bevel = Mathf.Min(0.16f, rc * 0.5f);
                    var profile = new List<Vector2> { new Vector2(rc, 0f), new Vector2(rc, height - bevel) };
                    for (int i = 1; i <= 5; i++)
                    {
                        float a = 0.5f * Mathf.PI * i / 5f;
                        profile.Add(new Vector2(rc - bevel + bevel * Mathf.Cos(a), height - bevel + bevel * Mathf.Sin(a)));
                    }
                    profile.Add(new Vector2(0f, height));
                    visual = Look.AddMesh("Visual", Meshes.Lathe(profile, 40), paint, go.transform, Vector3.zero);
                    if (b.Kind == BlockKind.Wall) Furnish(visual.transform, new Vector2(rc * 2f, rc * 2f), height, theme, b.Center, rc);
                }
                footprint = Geometry.Circle(Vector2.zero, rc, 40);
            }
            else
            {
                float bevel = Mathf.Min(0.16f, Mathf.Min(b.Size.x, b.Size.y) * 0.3f, rc * 0.9f + 0.02f);
                float bodyHeight = b.Kind == BlockKind.Hazard ? height * 0.55f : height;
                visual = Look.AddMesh("Visual", Meshes.RoundedPrism(b.Size.x, b.Size.y, rc, bodyHeight, bevel), paint, go.transform, Vector3.zero);
                if (b.Kind == BlockKind.Hazard) AddSpikes(visual.transform, b.Size, rc, bodyHeight);
                if (b.Kind == BlockKind.Breakable) CrystalDetails(visual.transform, b.Size, rc, height);
                if (b.Kind == BlockKind.Wall) Furnish(visual.transform, b.Size, height, theme, b.Center, -1f);
                if (b.Kind == BlockKind.Bouncer)
                    Look.AddMesh("Stripe", Meshes.RoundedPrism(Mathf.Max(0.05f, b.Size.x - 0.24f), Mathf.Max(0.05f, b.Size.y * 0.3f), Mathf.Min(0.08f, b.Size.y * 0.15f), 0.02f, 0.01f),
                        Look.Lit(Palette.RubberCap, 0.6f), visual.transform, new Vector3(0f, 0f, -height), false);
                footprint = Geometry.Fillet(new List<Vector2>
                {
                    new Vector2(-b.Size.x / 2f, -b.Size.y / 2f), new Vector2(b.Size.x / 2f, -b.Size.y / 2f),
                    new Vector2(b.Size.x / 2f, b.Size.y / 2f), new Vector2(-b.Size.x / 2f, b.Size.y / 2f),
                }, rc);
                if (b.Kind == BlockKind.Crate) AddCrateBraces(visual.transform, b.Size, height);
            }
            go.AddComponent<BlockTag>().Init(b.Kind, visual);
            Look.AddShadow("Shadow", Meshes.SoftShadow(footprint, 0.4f, 0.3f), go.transform, new Vector3(0.06f, -0.08f, ShadowZ));

            if (b.Kind == BlockKind.Gate)
            {
                var gate = new GateInstance { Def = b, Collider = col, Visual = visual.transform };
                inst.Gates.Add(gate);
                var anim = go.AddComponent<GateAnim>();
                anim.Init(gate, height);
                AddSymbol(visual.transform, b.Group, new Vector3(0f, 0f, -height - 0.005f), 0.34f);
            }

            if (b.Kind == BlockKind.Breakable)
                inst.Breakables.Add(new BreakableInstance { Collider = col, Visual = go });

            if (b.Kind == BlockKind.PopWall)
            {
                var pop = new PopWallInstance { Def = b, Collider = col, Visual = visual.transform };
                inst.PopWalls.Add(pop);
                var warn = MakeSprite("Outline", Shapes.Rounded, new Color(Palette.PopWall.r, Palette.PopWall.g, Palette.PopWall.b, 0.5f), go.transform,
                    new Vector3(0f, 0f, -0.004f), Vector2.one, 1);
                warn.drawMode = SpriteDrawMode.Sliced;
                warn.size = b.Size;
                go.AddComponent<PopWallAnim>().Init(pop, inst, warn);
            }

            if (b.Kind == BlockKind.Crate)
            {
                var rb = go.AddComponent<Rigidbody2D>();
                rb.gravityScale = 0f;
                rb.mass = Rules.CrateMass;
                rb.linearDamping = Rules.CrateDrag;
                rb.angularDamping = Rules.CrateAngularDrag;
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
                rb.interpolation = RigidbodyInterpolation2D.Interpolate;
                inst.Crates.Add(new CrateInstance { Body = rb, StartPosition = b.Center, StartRotation = b.Angle });
                return;
            }

            if (b.Moves)
            {
                var rb = go.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.interpolation = RigidbodyInterpolation2D.Interpolate;
                var mover = go.AddComponent<Mover>();
                mover.Init(rb, b);
                inst.Movers.Add(mover);
            }
        }

        static Material spikeMetal;

        // Rows of shiny cone spikes on top of a hazard block. They stay inside the footprint, so the spikes show
        // exactly where touching sends the puck back.
        static void AddSpikes(Transform visual, Vector2 size, float rc, float baseHeight)
        {
            spikeMetal ??= Look.Lit(Themes.Hex(0xEEF2F8), 0.92f);
            float minDim = Mathf.Min(size.x, size.y), maxDim = Mathf.Max(size.x, size.y);
            bool alongX = size.x >= size.y;
            float r = Mathf.Clamp(minDim * 0.3f, 0.09f, 0.16f);
            int rows = minDim >= 0.9f ? 2 : 1;
            float spacing = r * 2.1f;
            int count = Mathf.Max(1, Mathf.FloorToInt((maxDim - 0.1f) / spacing));
            var cone = Meshes.Lathe(new List<Vector2> { new Vector2(r, 0f), new Vector2(r * 0.55f, r * 1.4f), new Vector2(0.01f, r * 2.6f), new Vector2(0f, r * 2.6f) }, 12);
            for (int row = 0; row < rows; row++)
            for (int i = 0; i < count; i++)
            {
                float a = (i - (count - 1) / 2f) * spacing;
                float c = rows == 1 ? 0f : (row - 0.5f) * minDim * 0.45f;
                Vector2 p = alongX ? new Vector2(a, c) : new Vector2(c, a);
                if (RoundRectDistance(p, size, rc) > -r * 0.9f) continue;
                var s = Look.AddMesh("Spike", cone, spikeMetal, visual, new Vector3(p.x, p.y, -baseHeight + 0.02f), false);
                float lean = (i % 2 == 0 ? 1f : -1f) * 8f;
                s.transform.localRotation = alongX ? Quaternion.Euler(0f, lean, 0f) : Quaternion.Euler(lean, 0f, 0f);
            }
        }

        // A desk cactus in a terracotta pot: the round hazard. Spines reach the collider radius, so the prickles show
        // exactly where touching sends the employee back.
        static MeshRenderer Cactus(Transform parent, float r)
        {
            spikeMetal ??= Look.Lit(Themes.Hex(0xEEF2F8), 0.92f);
            // A square terracotta pot with a soft-block cactus and two little arms.
            var potMat = Look.Lit(Themes.Hex(0xD9784E), 0.35f);
            Look.AddMesh("Pot", Meshes.SoftBox(new Vector3(r * 1.45f, r * 1.45f, 0.26f), 0.05f), potMat, parent, new Vector3(0f, 0f, -0.13f));
            Look.AddMesh("Pot Rim", Meshes.SoftBox(new Vector3(r * 1.6f, r * 1.6f, 0.07f), 0.03f), potMat, parent, new Vector3(0f, 0f, -0.28f), false);
            float body = r * 0.68f;
            var green = Look.Lit(Themes.Hex(0x4FA35A), 0.45f);
            var visual = Look.AddMesh("Visual", Meshes.SoftBox(new Vector3(body * 1.7f, body * 1.7f, body * 1.9f), body * 0.42f), green, parent, new Vector3(0f, 0f, -0.28f - body * 0.95f));
            foreach (float side in new[] { -1f, 1f })
                Look.AddMesh("Arm", Meshes.SoftBox(new Vector3(body * 0.5f, body * 0.5f, body * 0.8f), body * 0.2f), green, visual.transform,
                    new Vector3(side * body * 1.0f, 0f, -body * 0.2f * (side + 1.5f)), false);
            // Spines radiate from the body's center.
            var spines = new GameObject("Spines").transform;
            spines.SetParent(visual.transform, false);
            float len = r - body * 0.9f + 0.02f;
            var cone = Meshes.Lathe(new List<Vector2> { new Vector2(0.018f, 0f), new Vector2(0.004f, len), new Vector2(0f, len) }, 6);
            var dirs = new List<Vector3>();
            for (int i = 0; i < 12; i++) { float a = i * Mathf.PI * 2f / 12f; dirs.Add(new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0.1f).normalized); }
            for (int i = 0; i < 7; i++) { float a = (i + 0.5f) * Mathf.PI * 2f / 7f; dirs.Add(new Vector3(Mathf.Cos(a) * 0.65f, Mathf.Sin(a) * 0.65f, -0.75f).normalized); }
            foreach (var d in dirs)
            {
                var s = Look.AddMesh("Spine", cone, spikeMetal, spines, d * body * 0.9f, false);
                s.transform.localRotation = Quaternion.FromToRotation(Vector3.back, d);
            }
            Look.AddMesh("Flower", Meshes.SoftBox(new Vector3(0.11f, 0.11f, 0.06f), 0.025f), Look.Lit(Themes.Hex(0xFF5E8A), 0.5f), spines, new Vector3(0f, 0f, -body * 0.98f), false);
            return visual;
        }

        // The Urgent Manager: a coworker in a red tie and a grey sweater vest who paces between two points, with an
        // urgent "!" beside the head. A moving hazard: bumping into the manager sends the employee back.
        static MeshRenderer Coworker(Transform parent, float r)
        {
            var holder = new GameObject("Manager").transform;
            holder.SetParent(parent, false);
            holder.localScale = Vector3.one * (r / Rules.PuckRadius);
            var look = new SkinDef { Body = Themes.Hex(0xC8303C), Cheek = Themes.Hex(0xF08A8A), Extra = Color.white, Accessory = Accessory.Tuft,
                SkinTone = Themes.Hex(0xE6B793), HairColor = Themes.Hex(0x9BA0AA), Hair = HairStyle.Short };
            BuildPuckLook(holder, look);
            // A phone held up to the ear: always on a call, never available.
            var hand = holder.Find("Roll/Arm R");
            if (hand != null)
            {
                hand.localRotation = Quaternion.Euler(0f, 150f, 0f);
                var phone = Look.AddMesh("Phone", Meshes.RoundedPrism(0.06f, 0.11f, 0.015f, 0.018f, 0.006f), Look.Person(Themes.Hex(0x22252D), 0.6f), hand,
                    new Vector3(0f, -0.02f, 0.16f), false);
                phone.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            holder.gameObject.AddComponent<Wiggle>().Init(Wiggle.Mode.Bob, 0.04f, 9f);
            holder.gameObject.AddComponent<CoworkerFace>();
            var head = holder.Find("Roll/Head/Skull");
            return head != null ? head.GetComponent<MeshRenderer>() : holder.GetComponentInChildren<MeshRenderer>();
        }

        // An office chair seen from above: a five-spoke base on casters, a gas lift, a cushioned seat with a piped edge,
        // armrests, and a curved backrest at the far side. Everything stays inside the round collider. The seat is the
        // part that wobbles and swivels on a hit. Swivel chairs: bright blue plastic with a white edge (the bouncy one).
        // The boss's chair: glossy red with a high back, a headrest, and a gold base.
        static MeshRenderer OfficeChair(Transform parent, float r, float h, bool boss)
        {
            var metal = boss ? Look.Lit(Themes.Hex(0xFFC93C), 0.7f) : Look.Lit(Themes.Hex(0xD5DCE6), 0.6f);
            var caster = Look.Lit(Themes.Hex(0x4A5060), 0.5f);
            var seatMat = boss ? Look.Lit(Themes.Hex(0xF2404E), 0.55f) : Look.Lit(Themes.Hex(0x3D9BFF), 0.5f);
            var seam = boss ? Look.Lit(Themes.Hex(0xFFE08A), 0.5f) : Look.Lit(Color.white, 0.5f);
            var plastic = boss ? Look.Lit(Themes.Hex(0xC8303D), 0.5f) : Look.Lit(Themes.Hex(0x2C78E0), 0.5f);

            // Base: five spokes from the hub, each ending in a caster just inside the collider.
            float reach = r * 0.82f;
            for (int i = 0; i < 5; i++)
            {
                float a = i * Mathf.PI * 2f / 5f - Mathf.PI / 2f;
                var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                var spoke = Look.AddMesh("Spoke", Meshes.RoundedPrism(reach, 0.075f, 0.035f, 0.045f, 0.02f), metal, parent,
                    dir * (reach * 0.5f) + new Vector3(0f, 0f, -0.05f), false);
                spoke.transform.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg);
                Look.AddMesh("Caster", Meshes.SoftBox(Vector3.one * 0.08f, 0.03f), caster, parent, dir * reach + new Vector3(0f, 0f, -0.04f), false);
            }
            Look.AddMesh("Hub", Meshes.SoftBox(new Vector3(r * 0.34f, r * 0.34f, 0.1f), 0.03f), metal, parent, new Vector3(0f, 0f, -0.05f), false);
            const float seatZ = 0.26f, seatH = 0.13f;
            Look.AddMesh("Gas Lift", Meshes.SoftBox(new Vector3(0.08f, 0.08f, seatZ), 0.025f), metal, parent, new Vector3(0f, 0f, -seatZ / 2f), false);

            // The seat cushion, with a piped seam around its edge.
            float sw = r * 1.6f, sd = r * 1.42f;
            var visual = Look.AddMesh("Visual", Meshes.RoundedPrism(sw, sd, r * 0.55f, seatH, 0.075f), seatMat, parent, new Vector3(0f, -r * 0.06f, -seatZ));
            Look.AddMesh("Piping", Meshes.RoundedPrism(sw + 0.025f, sd + 0.025f, r * 0.55f + 0.012f, 0.03f, 0.012f), seam, visual.transform,
                new Vector3(0f, 0f, -seatH * 0.35f), false);

            // Armrests on short posts at both sides.
            foreach (float side in new[] { -1f, 1f })
            {
                Look.AddMesh("Arm Post", Meshes.RoundedPrism(0.05f, 0.05f, 0.02f, 0.13f, 0.01f), plastic, visual.transform,
                    new Vector3(side * sw * 0.47f, sd * 0.05f, -seatH * 0.5f), false);
                Look.AddMesh("Arm Pad", Meshes.RoundedPrism(0.085f, sd * 0.62f, 0.04f, 0.045f, 0.02f), plastic, visual.transform,
                    new Vector3(side * sw * 0.47f, sd * 0.02f, -seatH * 0.5f - 0.13f));
            }

            // The backrest: a curved shell wrapping the far side, leaning back a little.
            float backH = boss ? 0.62f : 0.42f;
            var back = new GameObject("Back").transform;
            back.SetParent(visual.transform, false);
            back.localPosition = new Vector3(0f, sd * 0.38f, -seatH * 0.4f);
            back.localRotation = Quaternion.Euler(10f, 0f, 0f);
            var arc = new List<Vector2>();
            float br = r * 0.78f;
            for (int i = 0; i <= 12; i++)
            {
                float a = Mathf.Lerp(28f, 152f, i / 12f) * Mathf.Deg2Rad;
                arc.Add(new Vector2(Mathf.Cos(a) * br, Mathf.Sin(a) * br * 0.55f - br * 0.42f));
            }
            Look.AddMesh("Backrest", Meshes.Rod(arc, boss ? 0.11f : 0.085f, backH, 0.045f), seatMat, back, Vector3.zero);
            if (boss)
            {
                // A headrest pillow and a gold nameplate.
                var top = new Vector3(0f, br * 0.55f - br * 0.42f, -backH);
                Look.AddMesh("Headrest", Meshes.RoundedPrism(r * 0.95f, 0.15f, 0.07f, 0.1f, 0.05f), seatMat, back, top + new Vector3(0f, 0.01f, 0.02f));
                Look.AddMesh("Nameplate", Meshes.RoundedPrism(r * 0.42f, 0.02f, 0.008f, 0.07f, 0.006f), metal, back,
                    top + new Vector3(0f, -0.065f, 0.2f), false);
            }
            else
            {
                // A lighter mesh panel inside the frame of the back.
                var panel = new List<Vector2>();
                for (int i = 0; i <= 10; i++)
                {
                    float a = Mathf.Lerp(42f, 138f, i / 10f) * Mathf.Deg2Rad;
                    panel.Add(new Vector2(Mathf.Cos(a) * (br - 0.035f), Mathf.Sin(a) * (br - 0.035f) * 0.55f - br * 0.42f));
                }
                var mesh = Look.AddMesh("Mesh Panel", Meshes.Rod(panel, 0.03f, backH - 0.1f, 0.012f), Look.Lit(Themes.Hex(0x6C7586), 0.1f, Textures.Fabric), back,
                    new Vector3(0f, 0f, -0.05f), false);
                mesh.receiveShadows = true;
            }
            return visual;
        }

        // A stack of manila folders tied with red tape: a lighter top folder, paper edges, and an URGENT tab.
        static void CrystalDetails(Transform visual, Vector2 size, float rc, float height)
        {
            var paper = Look.Lit(Themes.Hex(0xFBF8F1), 0.3f);
            for (int i = 0; i < 3; i++)
            {
                float z = -height * (0.28f + 0.26f * i);
                Look.AddMesh("Paper", Meshes.RoundedPrism(size.x + 0.04f, size.y + 0.04f, Mathf.Max(0.02f, rc), 0.025f, 0.01f), paper, visual,
                    new Vector3(0.02f * (i - 1), -0.015f * i, z), false);
            }
            Look.AddMesh("Top", Meshes.RoundedPrism(Mathf.Max(0.05f, size.x - 0.1f), Mathf.Max(0.05f, size.y - 0.1f), Mathf.Max(0.01f, rc - 0.05f), 0.02f, 0.01f),
                Look.Lit(Themes.Hex(0xF2D48F), 0.3f), visual, new Vector3(0f, 0f, -height + 0.005f), false);
            bool alongX = size.x >= size.y;
            var tape = Look.AddMesh("Red Tape", Meshes.RoundedPrism(alongX ? 0.12f : size.x + 0.06f, alongX ? size.y + 0.06f : 0.12f, 0.03f, height + 0.03f, 0.02f),
                Look.Lit(Themes.Hex(0xD63A3A), 0.5f), visual, Vector3.zero, false);
            tape.receiveShadows = false;
        }

        // The boss's chair warns before it kicks: a red ring on the carpet just outside its base, gently breathing.
        static void KickerDetails(Transform parent, float r, float h)
        {
            var ring = MakeSprite("Warning Ring", Shapes.Ring, new Color(0.88f, 0.22f, 0.26f, 0.6f), parent, new Vector3(0f, 0f, -0.006f), Vector2.one * (r * 2.3f), 1);
            ring.gameObject.AddComponent<Wiggle>().Init(Wiggle.Mode.Pulse, 0.05f, 2.4f);
            MakeSprite("Glow", Textures.Glow, new Color(1f, 0.4f, 0.32f, 0.22f), parent, new Vector3(0f, 0f, -0.005f), Vector2.one * (r * 3.2f), 1);
        }

        // Signed distance from p to a centered rounded rectangle of the given size (negative inside).
        static float RoundRectDistance(Vector2 p, Vector2 size, float r)
        {
            Vector2 q = new Vector2(Mathf.Abs(p.x) - (size.x * 0.5f - r), Mathf.Abs(p.y) - (size.y * 0.5f - r));
            return Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
        }

        // Archive box: packing tape across the lid and a handle slot on the near side.
        static void AddCrateBraces(Transform parent, Vector2 size, float height)
        {
            var tape = Look.Lit(Themes.Hex(0xE9D9B0), 0.6f);
            Look.AddMesh("Tape", Meshes.RoundedPrism(size.x + 0.02f, size.y * 0.22f, 0.02f, 0.012f, 0.005f), tape, parent, new Vector3(0f, 0f, -height), false);
            Look.AddMesh("Label", Meshes.RoundedPrism(size.x * 0.4f, size.y * 0.16f, 0.02f, 0.012f, 0.005f), Look.Lit(Color.white, 0.4f), parent,
                new Vector3(0f, -size.y * 0.3f, -height), false);
        }

        static void AddRail(LevelInstance inst, RailDef r, Theme theme)
        {
            var go = new GameObject("Rod " + r.Kind);
            go.transform.SetParent(inst.Root.transform, false);
            var col = go.AddComponent<EdgeCollider2D>();
            col.SetPoints(r.Path);
            col.edgeRadius = r.Width * 0.5f;
            col.sharedMaterial = MaterialFor(r.Kind);
            inst.BlockColliders.Add(col);

            int unused = 0;
            var mat = r.Kind == BlockKind.Wall ? Look.Lit(theme.Rod, 0.7f, theme.RodTexture?.Invoke()) : Paint(r.Kind, ref unused, -1, theme);
            float height = HeightFor(r.Kind);
            var visual = Look.AddMesh("Visual", Meshes.Rod(r.Path, r.Width, height, 0.2f), mat, go.transform, Vector3.zero);
            // A light cap along the top, like the trim on an office partition.
            if (r.Kind == BlockKind.Wall)
                Look.AddMesh("Cap", Meshes.Rod(r.Path, r.Width * 0.62f, 0.03f, 0.015f), Look.Lit(TrimColor(theme), 0.7f), visual.transform,
                    new Vector3(0f, 0f, -height), false);
            go.AddComponent<BlockTag>().Init(r.Kind, visual);
            Look.AddShadow("Shadow", Meshes.SoftShadow(Geometry.Capsule(r.Path, r.Width * 0.5f), 0.4f, 0.3f), go.transform,
                new Vector3(0.06f, -0.08f, ShadowZ));
        }

        // The destination: a small, inviting office nap bed where the hole used to be (the capture rule is unchanged:
        // the employee's center has to reach the middle of the mattress). Pillow at the far end, a warm blanket folded
        // at the foot that unfolds over the sleeper, and a soft glow so it reads as "go here" at a glance.
        static void AddHole(LevelInstance inst, LevelDef def)
        {
            var bed = new GameObject("Bed").transform;
            bed.SetParent(inst.Root.transform, false);
            bed.localPosition = (Vector3)def.Hole;
            const float W = 1.24f, L = 1.5f;
            var frame = Look.Lit(Themes.Hex(0xE7D2B6), 0.45f, Textures.WoodFrame);
            var linen = Look.Lit(Themes.Hex(0xF6F3EC), 0.3f);
            var blanket = Look.Soft(Palette.Blanket, 0.35f, null, 0.12f);

            var glow = MakeSprite("Glow", Textures.Glow, new Color(1f, 0.8f, 0.5f, 0.45f), bed, new Vector3(0f, 0f, -0.004f), new Vector2(W * 2.2f, L * 2f), 1);
            glow.gameObject.AddComponent<Wiggle>().Init(Wiggle.Mode.Pulse, 0.05f, 2f);
            Look.AddShadow("Shadow", Meshes.SoftShadow(new List<Vector2>
            {
                new Vector2(-W / 2f, -L / 2f), new Vector2(W / 2f, -L / 2f), new Vector2(W / 2f, L / 2f), new Vector2(-W / 2f, L / 2f),
            }, 0.35f, 0.3f), bed, new Vector3(0.06f, -0.08f, ShadowZ));
            Look.AddMesh("Frame", Meshes.RoundedPrism(W, L, 0.14f, 0.07f, 0.04f), frame, bed, Vector3.zero);
            Look.AddMesh("Mattress", Meshes.RoundedPrism(W - 0.12f, L - 0.12f, 0.16f, 0.07f, 0.06f), linen, bed, new Vector3(0f, 0f, -0.06f));
            Look.AddMesh("Headboard", Meshes.RoundedPrism(W, 0.12f, 0.05f, 0.42f, 0.05f), frame, bed, new Vector3(0f, L / 2f + 0.02f, 0f));
            var pillow = Look.AddMesh("Pillow", Meshes.RoundedPrism(0.74f, 0.34f, 0.16f, 0.11f, 0.1f), Look.Lit(Color.white, 0.25f), bed, new Vector3(0f, L / 2f - 0.27f, -0.12f));
            pillow.transform.localRotation = Quaternion.Euler(-8f, 0f, 0f);

            // The blanket: pivot at the foot edge, folded short; it unfolds toward the pillow when someone lies down.
            var fold = new GameObject("Blanket").transform;
            fold.SetParent(bed, false);
            fold.localPosition = new Vector3(0f, -L / 2f + 0.08f, -0.12f);
            // A pillowy duvet: the bevel equals the height, so every edge is domed and soft.
            Look.AddMesh("Cover", Meshes.RoundedPrism(W - 0.04f, 1f, 0.2f, 0.3f, 0.29f), blanket, fold, new Vector3(0f, 0.5f, 0f));
            Look.AddMesh("Stripe", Meshes.RoundedPrism(W - 0.06f, 0.07f, 0.03f, 0.302f, 0.25f), Look.Lit(Color.white, 0.4f), fold, new Vector3(0f, 0.86f, 0f), false);
            fold.localScale = new Vector3(1f, 0.32f, 0.35f);

            // A small rest sign over the pillow.
            var z = MakeSprite("Rest", MascotArt.Z, new Color(0.45f, 0.5f, 0.9f, 0.9f), bed, new Vector3(0.38f, L / 2f - 0.1f, -0.75f), Vector2.one * 0.26f, 6);
            z.gameObject.AddComponent<Wiggle>().Init(Wiggle.Mode.Bob, 0.12f, 1.6f);
        }

        // The level pickup: a takeaway coffee floating over a soft glow, with steam curling up. It bobs and turns.
        // The faint ring shows how close Puffy has to pass to grab it.
        static void AddStar(LevelInstance inst, Vector2 pos)
        {
            var go = new GameObject("Coffee Pickup");
            go.transform.SetParent(inst.Root.transform, false);
            go.transform.localPosition = new Vector3(pos.x, pos.y, 0f);
            var glow = MakeSprite("Glow", Textures.Glow, new Color(1f, 0.8f, 0.5f, 0.6f), go.transform, new Vector3(0f, 0f, -0.01f), Vector2.one * 2.1f, 1);
            glow.gameObject.AddComponent<Wiggle>().Init(Wiggle.Mode.Pulse, 0.08f, 2.4f);
            MakeSprite("Reach", Shapes.Ring, new Color(1f, 0.6f, 0.45f, 0.22f), go.transform, new Vector3(0f, 0f, -0.012f), Vector2.one * (Rules.StarReach * 2f), 1);
            var holder = new GameObject("Bob").transform;
            holder.SetParent(go.transform, false);
            holder.localPosition = new Vector3(0f, 0f, -0.25f);
            holder.localScale = Vector3.one * 1.35f;  // big enough to read at phone size
            var spin = new GameObject("Spin").transform;
            spin.SetParent(holder, false);
            var paper = Look.Lit(Themes.Hex(0xFFF6EA), 0.5f);
            var sleeve = Look.Lit(Themes.Hex(0xFF6B5E), 0.55f);
            // A soft-block takeaway cup: paper body, a red sleeve, a white lid, and the sip hole.
            Look.AddMesh("Cup", Meshes.SoftBox(new Vector3(0.38f, 0.38f, 0.5f), 0.08f), paper, spin, new Vector3(0f, 0f, -0.27f));
            Look.AddMesh("Sleeve", Meshes.SoftBox(new Vector3(0.4f, 0.4f, 0.19f), 0.06f), sleeve, spin, new Vector3(0f, 0f, -0.26f), false);
            Look.AddMesh("Lid", Meshes.SoftBox(new Vector3(0.44f, 0.44f, 0.09f), 0.04f), Look.Lit(Color.white, 0.7f), spin, new Vector3(0f, 0f, -0.55f), false);
            Look.AddMesh("Sip", Meshes.SoftBox(new Vector3(0.1f, 0.06f, 0.02f), 0.01f), Look.Flat(Themes.Hex(0x5A3322), 0.3f), spin, new Vector3(0.11f, 0f, -0.6f), false);
            spin.gameObject.AddComponent<Wiggle>().Init(Wiggle.Mode.Spin, 0f, 1.2f);
            holder.gameObject.AddComponent<Wiggle>().Init(Wiggle.Mode.Bob, 0.1f, 2.4f);
            for (int i = 0; i < 2; i++)
            {
                var steam = MakeSprite("Steam", Textures.Glow, new Color(1f, 1f, 1f, 0.55f), holder, new Vector3(0.05f - 0.1f * i, 0f, -0.8f - 0.15f * i), new Vector2(0.18f, 0.3f), 6);
                steam.gameObject.AddComponent<Wiggle>().Init(Wiggle.Mode.Bob, 0.2f, 3f + i);
            }
            inst.Stars.Add(new StarPickup { Position = pos, Visual = go });
        }

        static void AddZone(LevelInstance inst, ZoneDef z, int index)
        {
            var zone = new ZoneInstance(z, index);
            inst.Zones.Add(zone);

            var go = new GameObject(z.Kind.ToString());
            go.transform.SetParent(inst.Root.transform, false);
            go.transform.localPosition = z.Center;
            go.transform.localRotation = Quaternion.Euler(0f, 0f, z.Angle);
            var vis = go.AddComponent<ZoneVisual>();
            vis.Zone = zone;
            zone.Visual = vis;
            zone.Level = inst;
            float w = z.Size.x, len = z.Size.y;

            switch (z.Kind)
            {
                case ZoneKind.Boost:
                {
                    // A dark pad in a glowing orange frame, with chevrons that light up in a running wave.
                    vis.Init(z.Kind, Palette.Boost);
                    Look.AddMesh("Frame", Meshes.RoundedPrism(w, len, 0.3f, 0.05f, 0.03f), Look.Lit(Palette.Boost, 0.8f), go.transform, Vector3.zero, false);
                    Look.AddMesh("Pad", Meshes.RoundedPrism(w - 0.18f, len - 0.18f, 0.22f, 0.06f, 0.02f), Look.Lit(Themes.Hex(0x34385E), 0.85f),
                        go.transform, Vector3.zero, false);
                    MakeSprite("Glow", Textures.Glow, new Color(1f, 0.7f, 0.25f, 0.35f), go.transform, new Vector3(0f, 0f, -0.065f),
                        new Vector2(w * 1.1f, len * 1.1f), 1);
                    float cw = Mathf.Min(w * 0.62f, len * 0.5f);
                    int n = Mathf.Clamp(Mathf.RoundToInt(len / (cw * 0.55f)), 2, 5);
                    for (int i = 0; i < n; i++)
                    {
                        float y = (i - (n - 1) / 2f) * (len - 0.4f) / n;
                        vis.Sparkles.Add(MakeSprite("Chevron", UiArt.Icon("chevron"), Palette.Boost, go.transform, new Vector3(0f, y, -0.07f),
                            Vector2.one * (cw / 1.92f), 3));
                    }
                    break;
                }
                case ZoneKind.Mud:
                {
                    // A thick shag rug: a soft mustard pile with darker tufts and a fringe of tassels at the ends.
                    vis.Init(z.Kind, Palette.Mud);
                    Look.AddMesh("Rug", Meshes.RoundedPrism(w, len, Mathf.Min(0.18f, Mathf.Min(w, len) * 0.2f), 0.06f, 0.04f),
                        Look.Lit(Palette.Mud, 0.1f, Textures.Fabric), go.transform, Vector3.zero, false);
                    var rng = new System.Random(index * 31 + 7);
                    var tuftMat = Look.Lit(Color.Lerp(Palette.Mud, Color.black, 0.12f), 0.1f);
                    var tuft = Meshes.Lathe(Scenery.Sphere(0.07f), 8);
                    int count = Mathf.Clamp(Mathf.RoundToInt(w * len * 2f), 8, 40);
                    for (int i = 0; i < count; i++)
                    {
                        float bx = ((float)rng.NextDouble() - 0.5f) * (w - 0.3f);
                        float by = ((float)rng.NextDouble() - 0.5f) * (len - 0.3f);
                        var t = Look.AddMesh("Tuft", tuft, tuftMat, go.transform, new Vector3(bx, by, -0.04f), false);
                        t.transform.localScale = new Vector3(1f, 1f, 0.5f);
                    }
                    var tassel = Meshes.RoundedPrism(0.05f, 0.16f, 0.02f, 0.02f, 0.01f);
                    var cream = Look.Lit(Themes.Hex(0xF3E6C8), 0.2f);
                    bool alongY = len >= w;
                    float across = alongY ? w : len, along = alongY ? len : w;
                    int tassels = Mathf.Max(3, Mathf.FloorToInt(across / 0.18f));
                    foreach (float end in new[] { -1f, 1f })
                        for (int i = 0; i < tassels; i++)
                        {
                            float c = (i - (tassels - 1) / 2f) * (across - 0.2f) / tassels;
                            var p = alongY ? new Vector3(c, end * (along / 2f + 0.07f), -0.01f) : new Vector3(end * (along / 2f + 0.07f), c, -0.01f);
                            var m = Look.AddMesh("Tassel", tassel, cream, go.transform, p, false);
                            if (!alongY) m.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                        }
                    break;
                }
                case ZoneKind.Ice:
                {
                    // A freshly polished, wet floor: a glossy pale panel, yellow-and-black caution tape around it, a
                    // painted warning sign, soft reflection streaks, and the odd glint. Slippery before you even launch.
                    vis.Init(z.Kind, Palette.Ice);
                    float rc = Mathf.Min(0.3f, Mathf.Min(w, len) * 0.15f);
                    Look.AddMesh("Polish", Meshes.RoundedPrism(w, len, rc, 0.02f, 0.01f), Look.Flat(Themes.Hex(0xDDF1FA), 0.99f), go.transform, Vector3.zero, false);
                    var caution = Look.Flat(Color.white, 0.6f, Textures.CautionTape);
                    const float band = 0.16f;
                    Look.AddMesh("Tape", Meshes.RoundedPrism(w, band, 0.02f, 0.03f, 0.01f), caution, go.transform, new Vector3(0f, len / 2f - band / 2f, 0f), false);
                    Look.AddMesh("Tape", Meshes.RoundedPrism(w, band, 0.02f, 0.03f, 0.01f), caution, go.transform, new Vector3(0f, -len / 2f + band / 2f, 0f), false);
                    var side = Look.AddMesh("Tape", Meshes.RoundedPrism(len - band * 2f, band, 0.02f, 0.03f, 0.01f), caution, go.transform, new Vector3(w / 2f - band / 2f, 0f, 0f), false);
                    side.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    side = Look.AddMesh("Tape", Meshes.RoundedPrism(len - band * 2f, band, 0.02f, 0.03f, 0.01f), caution, go.transform, new Vector3(-w / 2f + band / 2f, 0f, 0f), false);
                    side.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    float sign = Mathf.Min(1.4f, Mathf.Min(w, len) * 0.45f);
                    MakeSprite("Wet Floor", UiArt.Icon("wetfloor"), new Color(0.98f, 0.8f, 0.15f, 0.55f), go.transform, new Vector3(0f, 0f, -0.035f), Vector2.one * (sign / 1.92f), 2);
                    var rng = new System.Random(index * 17 + 3);
                    int streaks = Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(w, len) / 1.8f), 2, 5);
                    for (int i = 0; i < streaks; i++)
                    {
                        float along = (i + 0.5f) / streaks - 0.5f;
                        Vector3 p = w >= len ? new Vector3(along * (w - 0.8f), len * 0.22f, -0.03f) : new Vector3(w * 0.22f, along * (len - 0.8f), -0.03f);
                        var streak = MakeSprite("Streak", Shapes.Rounded, new Color(1f, 1f, 1f, 0.5f), go.transform, p, Vector2.one, 2);
                        streak.drawMode = SpriteDrawMode.Sliced;
                        streak.size = new Vector2(0.08f, Mathf.Clamp(Mathf.Min(w, len) * 0.25f, 0.3f, 1f));
                        streak.transform.localRotation = Quaternion.Euler(0f, 0f, 35f);
                    }
                    int count = Mathf.Clamp(Mathf.RoundToInt(w * len / 6f), 2, 8);
                    for (int i = 0; i < count; i++)
                    {
                        float bx = ((float)rng.NextDouble() - 0.5f) * (w - 0.8f);
                        float by = ((float)rng.NextDouble() - 0.5f) * (len - 0.8f);
                        vis.Sparkles.Add(MakeSprite("Glint", UiArt.Twinkle, Color.white, go.transform, new Vector3(bx, by, -0.04f), Vector2.one * 0.32f, 3));
                    }
                    break;
                }
                case ZoneKind.Gap:
                {
                    // A dark pit with wooden lips. Grounded pucks fall in; jumping pucks fly over.
                    vis.Init(z.Kind, Palette.Gap);
                    var pit = Look.AddMesh("Pit", Meshes.RoundedPrism(w, len, 0.15f, 0.005f, 0f), Look.Unlit(Palette.Gap), go.transform,
                        new Vector3(0f, 0f, -0.001f), false);
                    pit.receiveShadows = false;
                    var lip = Look.Lit(Palette.Rod, 0.6f, Textures.WoodFrame);
                    Look.AddMesh("Back Lip", Meshes.RoundedPrism(w, 0.16f, 0.06f, 0.05f, 0.03f), lip, go.transform, new Vector3(0f, len / 2f, 0f), false);
                    Look.AddMesh("Front Lip", Meshes.RoundedPrism(w, 0.16f, 0.06f, 0.05f, 0.03f), lip, go.transform, new Vector3(0f, -len / 2f, 0f), false);
                    Look.AddShadow("Pit Shade", Meshes.SoftShadow(new List<Vector2>
                    {
                        new Vector2(-w / 2f, -len / 2f), new Vector2(w / 2f, -len / 2f), new Vector2(w / 2f, len / 2f), new Vector2(-w / 2f, len / 2f),
                    }, -0.4f, 0.5f), go.transform, new Vector3(0f, 0f, -0.002f));
                    break;
                }
                case ZoneKind.Ramp:
                {
                    vis.Init(z.Kind, Palette.Ramp);
                    Look.AddMesh("Ramp", Meshes.Wedge(w, len, Rules.RampHeight), Look.Toy(Palette.Ramp), go.transform, Vector3.zero);
                    Look.AddMesh("Lip", Meshes.RoundedPrism(w, 0.14f, 0.06f, 0.04f, 0.03f), Look.Toy(Color.white),
                        go.transform, new Vector3(0f, len / 2f - 0.07f, -Rules.RampHeight), false);
                    float slope = Mathf.Atan2(Rules.RampHeight, len) * Mathf.Rad2Deg;
                    float aw = Mathf.Min(w * 0.45f, 1.4f);
                    foreach (float p in new[] { 0.25f, 0.5f, 0.75f })
                    {
                        var a = MakeSprite("Arrow", UiArt.Icon("chevron"), Palette.RampArrow, go.transform,
                            new Vector3(0f, (p - 0.5f) * len, -p * Rules.RampHeight - 0.012f), Vector2.one * (aw / 1.92f), 3);
                        a.transform.localRotation = Quaternion.Euler(-slope, 0f, 0f);
                        vis.Sparkles.Add(a);
                    }
                    // Side stripes, so the slope reads as a ramp from any angle.
                    foreach (float sx in new[] { -1f, 1f })
                    {
                        var stripe = Look.AddMesh("Edge", Meshes.Wedge(0.12f, len, Rules.RampHeight + 0.01f), Look.Toy(Color.white), go.transform,
                            new Vector3(sx * (w / 2f - 0.06f), 0f, 0f), false);
                    }

                    // One-way front edge: the puck rolls off the front, but bounces off it from the outside.
                    var front = new GameObject("Ramp Front");
                    front.transform.SetParent(go.transform, false);
                    var e = front.AddComponent<EdgeCollider2D>();
                    e.SetPoints(new List<Vector2> { new Vector2(-w / 2f, len / 2f), new Vector2(w / 2f, len / 2f) });
                    e.usedByEffector = true;
                    e.sharedMaterial = woodMat;
                    var effector = front.AddComponent<PlatformEffector2D>();
                    effector.useOneWay = true;
                    effector.surfaceArc = 180f;
                    effector.useSideFriction = false;
                    front.AddComponent<BlockTag>().Init(BlockKind.Wall, null);
                    inst.BlockColliders.Add(e);
                    break;
                }
                case ZoneKind.Portal:
                {
                    Color c = Palette.Portal[(index / 2) % Palette.Portal.Length];
                    vis.Init(z.Kind, c);
                    float r = w * 0.5f;
                    MakeSprite("Glow", Textures.Glow, new Color(c.r, c.g, c.b, 0.55f), go.transform, new Vector3(0f, 0f, -0.002f),
                        Vector2.one * (r * 3.2f), 1);
                    Look.AddMesh("Inside", Meshes.Ring(0f, r * 0.85f), Look.Lit(Palette.PortalInner, 0.9f), go.transform,
                        new Vector3(0f, 0f, -0.004f), false);
                    var torus = new List<Vector2>();
                    for (int i = 0; i <= 12; i++)
                    {
                        float a = Mathf.PI * i / 12f;
                        torus.Add(new Vector2(r * 0.92f + Mathf.Cos(a) * r * 0.1f, Mathf.Sin(a) * r * 0.12f));
                    }
                    Look.AddMesh("Ring", Meshes.Lathe(torus, 40), Look.Toy(c), go.transform, Vector3.zero, false);
                    // A small arrow on the rim shows the facing, which turns the exit direction.
                    Look.AddMesh("Facing", Meshes.Extrude(Chevron(0.32f), 0.03f), Look.Toy(Color.white), go.transform,
                        new Vector3(0f, r * 1.25f, -0.03f), false);
                    var spin = new GameObject("Swirl").transform;
                    spin.SetParent(go.transform, false);
                    spin.localPosition = new Vector3(0f, 0f, -0.01f);
                    MakeSprite("Swirl", UiArt.Icon("swirl"), Color.Lerp(c, Color.white, 0.35f), spin, Vector3.zero, Vector2.one * (r * 1.7f / 1.92f), 2);
                    MakeSprite("Core", Textures.Glow, new Color(1f, 1f, 1f, 0.7f), spin, new Vector3(0f, 0f, -0.002f), Vector2.one * (r * 0.7f), 3);
                    vis.Spinner = spin;
                    break;
                }
                case ZoneKind.Conveyor:
                {
                    // A dark rubber belt with rollers at both ends; white chevrons ride on it at the belt's real speed.
                    vis.Init(z.Kind, Color.white);
                    Look.AddMesh("Frame", Meshes.RoundedPrism(w, len, 0.12f, 0.05f, 0.03f), Look.Lit(Themes.Hex(0xC9CED8), 0.85f), go.transform, Vector3.zero, false);
                    Look.AddMesh("Belt", Meshes.RoundedPrism(w - 0.16f, len - 0.16f, 0.08f, 0.06f, 0.02f), Look.Lit(Themes.Hex(0x3E4256), 0.35f),
                        go.transform, Vector3.zero, false);
                    foreach (float sy in new[] { -1f, 1f })
                    {
                        var roller = Look.AddMesh("Roller", Meshes.Rod(new List<Vector2> { new Vector2(-w / 2f + 0.1f, 0f), new Vector2(w / 2f - 0.1f, 0f) }, 0.14f, 0.075f, 0.06f),
                            Look.Lit(Themes.Hex(0xFFC64D), 0.8f), go.transform, new Vector3(0f, sy * (len / 2f - 0.07f), 0f), false);
                    }
                    float cw = Mathf.Min(w * 0.5f, len * 0.55f, 1.3f);
                    int count = Mathf.Max(2, Mathf.FloorToInt((len - 0.3f) / (cw * 0.6f)));
                    for (int i = 0; i < count; i++)
                        vis.Sparkles.Add(MakeSprite("Chevron", UiArt.Icon("chevron"), new Color(1f, 1f, 1f, 0.85f), go.transform,
                            new Vector3(0f, 0f, -0.07f), Vector2.one * (cw / 1.92f), 3));
                    vis.Length = len - 0.3f;
                    break;
                }
                case ZoneKind.Jump:
                {
                    // A trampoline: a violet rim around a dark bouncy mat, with rings that pulse out from the middle.
                    vis.Init(z.Kind, Palette.Trampoline);
                    float r = w * 0.5f;
                    var mat = Look.AddMesh("Mat", Meshes.Ring(0f, r * 0.86f), Look.Flat(Themes.Hex(0x2E2A55), 0.6f), go.transform, new Vector3(0f, 0f, -0.035f), false);
                    vis.Parts.Add(mat);
                    var rim = new List<Vector2>();
                    for (int i = 0; i <= 12; i++)
                    {
                        float a = Mathf.PI * i / 12f;
                        rim.Add(new Vector2(r * 0.93f + Mathf.Cos(a) * r * 0.09f, Mathf.Sin(a) * 0.12f));
                    }
                    Look.AddMesh("Rim", Meshes.Lathe(rim, 40), Look.Toy(Palette.Trampoline), go.transform, Vector3.zero, false);
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI * 2f / 8f;
                        Look.AddMesh("Pad", Meshes.Lathe(Scenery.Sphere(0.05f), 10), Look.Toy(Color.white), go.transform,
                            new Vector3(Mathf.Cos(a) * r * 0.93f, Mathf.Sin(a) * r * 0.93f, -0.1f), false);
                    }
                    for (int i = 0; i < 3; i++)
                        vis.Sparkles.Add(MakeSprite("Ring", Shapes.Ring, new Color(1f, 1f, 1f, 0.4f), go.transform, new Vector3(0f, 0f, -0.04f), Vector2.one * r, 3));
                    MakeSprite("Arrow", UiArt.Icon("chevron"), new Color(1f, 1f, 1f, 0.75f), go.transform, new Vector3(0f, 0f, -0.045f), Vector2.one * (r * 0.8f / 1.92f), 4);
                    break;
                }
                case ZoneKind.OneWay:
                {
                    // A green gate: two posts and a glowing bar with arrows. Pass along the arrows, bounce off the back.
                    vis.Init(z.Kind, Palette.OneWay);
                    var bar = MakeSprite("Bar", Shapes.Rounded, new Color(Palette.OneWay.r, Palette.OneWay.g, Palette.OneWay.b, 0.55f), go.transform,
                        new Vector3(0f, 0f, -0.02f), Vector2.one, 2);
                    bar.drawMode = SpriteDrawMode.Sliced;
                    bar.size = new Vector2(w, 0.3f);
                    foreach (float sx in new[] { -1f, 1f })
                        Look.AddMesh("Post", Meshes.Lathe(Scenery.Cylinder(0.14f, 0.55f), 16), Look.Toy(Palette.OneWay), go.transform, new Vector3(sx * w / 2f, 0f, 0f));
                    int n = Mathf.Clamp(Mathf.FloorToInt(w / 0.9f), 1, 5);
                    for (int i = 0; i < n; i++)
                        vis.Sparkles.Add(MakeSprite("Chevron", UiArt.Icon("chevron"), Color.white, go.transform,
                            new Vector3((i - (n - 1) / 2f) * (w - 0.4f) / Mathf.Max(1, n), 0f, -0.03f), Vector2.one * (0.5f / 1.92f), 3));
                    var gate = new GameObject("Gate Edge");
                    gate.transform.SetParent(go.transform, false);
                    var e = gate.AddComponent<EdgeCollider2D>();
                    e.SetPoints(new List<Vector2> { new Vector2(-w / 2f, 0f), new Vector2(w / 2f, 0f) });
                    e.edgeRadius = 0.06f;
                    e.usedByEffector = true;
                    e.sharedMaterial = woodMat;
                    var effector = gate.AddComponent<PlatformEffector2D>();
                    effector.useOneWay = true;
                    effector.surfaceArc = 180f;
                    effector.useSideFriction = false;
                    // Solid on the arrows' far side only: travel along the arrows passes, travel against them bounces.
                    gate.AddComponent<BlockTag>().Init(BlockKind.Wall, null);
                    inst.BlockColliders.Add(e);
                    break;
                }
                case ZoneKind.Wind:
                {
                    // A breezy lane: a pale blue wash, a floor vent with a spinning fan at the upwind end, and white
                    // gusts streaming along the push.
                    vis.Init(z.Kind, Palette.Wind);
                    var wash = MakeSprite("Wash", Shapes.Rounded, new Color(Palette.Wind.r, Palette.Wind.g, Palette.Wind.b, 0.22f), go.transform,
                        new Vector3(0f, 0f, -0.002f), Vector2.one, 1);
                    wash.drawMode = SpriteDrawMode.Sliced;
                    wash.size = new Vector2(w, len);
                    var vent = Look.AddMesh("Vent", Meshes.RoundedPrism(w - 0.1f, 0.5f, 0.2f, 0.06f, 0.03f), Look.Lit(Themes.Hex(0xEAF6FF), 0.7f),
                        go.transform, new Vector3(0f, -len / 2f + 0.3f, 0f), false);
                    var slot = Look.Lit(Themes.Hex(0x4A5A78), 0.4f);
                    int slots = Mathf.Max(2, Mathf.FloorToInt((w - 0.5f) / 0.32f));
                    for (int i = 0; i < slots; i++)
                        Look.AddMesh("Slot", Meshes.RoundedPrism(0.12f, 0.32f, 0.05f, 0.01f, 0f), slot, vent.transform,
                            new Vector3((i - (slots - 1) / 2f) * 0.32f, 0f, -0.062f), false);
                    var fan = new GameObject("Fan").transform;
                    fan.SetParent(go.transform, false);
                    fan.localPosition = new Vector3(0f, -len / 2f + 0.3f, -0.09f);
                    for (int i = 0; i < 3; i++)
                    {
                        var blade = Look.AddMesh("Blade", Meshes.RoundedPrism(0.12f, 0.36f, 0.05f, 0.02f, 0.01f), Look.Toy(Palette.Wind), fan,
                            Quaternion.Euler(0f, 0f, i * 120f) * new Vector3(0f, 0.17f, 0f), false);
                        blade.transform.localRotation = Quaternion.Euler(0f, 0f, i * 120f);
                    }
                    Look.AddMesh("Hub", Meshes.Lathe(Scenery.Sphere(0.08f), 12), Look.Toy(Color.white), fan, Vector3.zero, false);
                    fan.gameObject.AddComponent<Wiggle>().Init(Wiggle.Mode.Spin, 0f, 14f);
                    int gusts = Mathf.Clamp(Mathf.RoundToInt(w * len / 1.6f), 4, 16);
                    for (int i = 0; i < gusts; i++)
                    {
                        var g = MakeSprite("Gust", Textures.Glow, new Color(1f, 1f, 1f, 0f), go.transform, Vector3.zero, new Vector2(0.14f, 1.1f), 3);
                        g.transform.localPosition = new Vector3(((i * 0.618f) % 1f - 0.5f) * (w - 0.4f), 0f, -0.05f);
                        vis.Sparkles.Add(g);
                    }
                    // Faint arrows painted on the floor show which way it blows.
                    int arrows = Mathf.Clamp(Mathf.FloorToInt((len - 0.8f) / 1.6f), 1, 5);
                    float aw = Mathf.Min(w * 0.45f, 1.1f);
                    for (int i = 0; i < arrows; i++)
                        MakeSprite("Arrow", UiArt.Icon("chevron"), new Color(0.36f, 0.7f, 1f, 0.4f), go.transform,
                            new Vector3(0f, -len / 2f + 0.9f + (i + 0.5f) * (len - 0.9f) / arrows, -0.004f), Vector2.one * (aw / 1.92f), 2);
                    vis.Length = len - 0.7f;
                    break;
                }
                case ZoneKind.Vortex:
                {
                    // A swirling coffee spill: glossy coffee in a ring of milky foam, with latte-art swirls turning the
                    // way it pulls.
                    vis.Init(z.Kind, Palette.Water);
                    float r = w * 0.5f;
                    Look.AddMesh("Water", Meshes.Ring(0f, r), Look.Soft(Palette.Water, 0.95f, null, 0.1f), go.transform, new Vector3(0f, 0f, -0.004f), false);
                    MakeSprite("Deep", Textures.Glow, new Color(0.22f, 0.12f, 0.06f, 0.75f), go.transform, new Vector3(0f, 0f, -0.006f), Vector2.one * (r * 1.3f), 2);
                    var foam = new List<Vector2>();
                    for (int i = 0; i <= 12; i++)
                    {
                        float a = Mathf.PI * i / 12f;
                        foam.Add(new Vector2(r + Mathf.Cos(a) * 0.09f, Mathf.Sin(a) * 0.07f));
                    }
                    Look.AddMesh("Foam", Meshes.Lathe(foam, 64), Look.Lit(Themes.Hex(0xEEDCC2), 0.6f), go.transform, Vector3.zero, false);
                    var spin = new GameObject("Swirl").transform;
                    spin.SetParent(go.transform, false);
                    spin.localPosition = new Vector3(0f, 0f, -0.008f);
                    var sw = MakeSprite("Swirl", UiArt.Icon("swirl"), new Color(0.96f, 0.88f, 0.76f, 0.6f), spin, Vector3.zero, Vector2.one * (r * 1.9f / 1.92f), 3);
                    sw.flipX = z.Turn < 0f;
                    MakeSprite("Swirl Small", UiArt.Icon("swirl"), new Color(0.98f, 0.92f, 0.82f, 0.5f), spin, Vector3.zero, Vector2.one * (r * 1.0f / 1.92f), 3).flipX = z.Turn < 0f;
                    vis.Spinner = spin;
                    for (int i = 0; i < 8; i++)
                        vis.Sparkles.Add(MakeSprite("Bubble", Textures.Glow, new Color(1f, 1f, 1f, 0.7f), go.transform, Vector3.zero, Vector2.one * 0.14f, 4));
                    break;
                }
                case ZoneKind.Spikes:
                {
                    // A steel plate with holes; silver spikes pop up through them on a timer, with a red warning glow.
                    vis.Init(z.Kind, Palette.Hazard);
                    var plate = Look.AddMesh("Plate", Meshes.RoundedPrism(w, len, 0.12f, 0.04f, 0.02f), Look.Lit(Themes.Hex(0x8A93A6), 0.75f), go.transform, Vector3.zero, false);
                    vis.Parts.Add(plate);
                    var warn = MakeSprite("Warn", Shapes.Rounded, new Color(1f, 0.25f, 0.25f, 0f), go.transform, new Vector3(0f, 0f, -0.045f), Vector2.one, 2);
                    warn.drawMode = SpriteDrawMode.Sliced;
                    warn.size = new Vector2(w - 0.06f, len - 0.06f);
                    vis.Sparkles.Add(warn);
                    var spikes = new GameObject("Spikes").transform;
                    spikes.SetParent(go.transform, false);
                    spikeMetal ??= Look.Lit(Themes.Hex(0xEEF2F8), 0.92f);
                    const float sr = 0.09f, step = 0.3f;
                    var cone = Meshes.Lathe(new List<Vector2> { new Vector2(sr, 0f), new Vector2(sr * 0.55f, sr * 1.4f), new Vector2(0.01f, sr * 2.8f), new Vector2(0f, sr * 2.8f) }, 10);
                    int nx = Mathf.Max(1, Mathf.FloorToInt((w - 0.15f) / step)), ny = Mathf.Max(1, Mathf.FloorToInt((len - 0.15f) / step));
                    var hole = Look.Lit(Themes.Hex(0x2C3140), 0.3f);
                    for (int iy = 0; iy < ny; iy++)
                    for (int ix = 0; ix < nx; ix++)
                    {
                        var p = new Vector3((ix - (nx - 1) / 2f) * step, (iy - (ny - 1) / 2f) * step, 0f);
                        Look.AddMesh("Hole", Meshes.Ring(0f, sr * 0.9f), hole, plate.transform, p + new Vector3(0f, 0f, -0.042f), false);
                        Look.AddMesh("Spike", cone, spikeMetal, spikes, p + new Vector3(0f, 0f, -0.04f), false);
                    }
                    vis.Spinner = spikes;
                    break;
                }
                case ZoneKind.Magnet:
                {
                    vis.Init(z.Kind, Palette.Magnet);
                    float reach = w * 0.5f;
                    // The post is solid; the pull region is drawn as a faint pulsing ring.
                    var post = go.AddComponent<CircleCollider2D>();
                    post.radius = 0.45f;
                    post.sharedMaterial = woodMat;
                    go.AddComponent<BlockTag>().Init(BlockKind.Wall, null);
                    inst.BlockColliders.Add(post);
                    var ring = MakeSprite("Reach", Shapes.Ring, new Color(0.75f, 0.48f, 0.27f, 0.4f), go.transform, new Vector3(0f, 0f, -0.006f),
                        Vector2.one * (reach * 2f), 1);
                    vis.Sparkles.Add(ring);
                    MakeSprite("Field", Textures.Glow, new Color(0.8f, 0.5f, 0.28f, 0.18f), go.transform, new Vector3(0f, 0f, -0.005f),
                        Vector2.one * (reach * 2f), 1);
                    Look.AddShadow("Shadow", Meshes.SoftShadow(Geometry.Circle(Vector2.zero, 0.45f, 32), 0.35f, 0.3f), go.transform,
                        new Vector3(0.06f, -0.08f, ShadowZ));
                    // The coffee machine: the real department head. A dark machine with a steaming cup at its spout.
                    var dark = Look.Lit(Themes.Hex(0x343842), 0.7f);
                    Look.AddMesh("Machine", Meshes.RoundedPrism(0.7f, 0.56f, 0.1f, 0.78f, 0.06f), dark, go.transform, new Vector3(0f, 0.04f, 0f));
                    Look.AddMesh("Top", Meshes.RoundedPrism(0.6f, 0.46f, 0.08f, 0.04f, 0.02f), Look.Lit(Themes.Hex(0xC9CED8), 0.9f), go.transform, new Vector3(0f, 0.04f, -0.78f), false);
                    Look.AddMesh("Light", Meshes.Lathe(Scenery.Sphere(0.035f), 8), Look.Soft(Themes.Hex(0x7CF29B), 0.9f, null, 0.8f), go.transform, new Vector3(0.2f, -0.25f, -0.62f), false);
                    var cup = new List<Vector2> { new Vector2(0.07f, 0f), new Vector2(0.09f, 0.14f), new Vector2(0f, 0.14f) };
                    Look.AddMesh("Cup", Meshes.Lathe(cup, 16), Look.Lit(Color.white, 0.5f), go.transform, new Vector3(0f, -0.32f, -0.05f), false);
                    var steam = MakeSprite("Steam", Textures.Glow, new Color(1f, 1f, 1f, 0.5f), go.transform, new Vector3(0f, -0.32f, -0.45f), new Vector2(0.18f, 0.32f), 6);
                    steam.gameObject.AddComponent<Wiggle>().Init(Wiggle.Mode.Bob, 0.15f, 2.6f);
                    break;
                }
                case ZoneKind.Switch:
                {
                    Color c = Palette.GateColors[Mathf.Max(0, z.Group) % Palette.GateColors.Length];
                    vis.Init(z.Kind, c);
                    float r = w * 0.5f;
                    var baseRing = new List<Vector2> { new Vector2(r, 0f), new Vector2(r, 0.06f), new Vector2(r - 0.08f, 0.08f), new Vector2(r - 0.1f, 0.08f) };
                    Look.AddMesh("Base", Meshes.Lathe(baseRing, 32), Look.Lit(Palette.Metal, 0.7f), go.transform, Vector3.zero, false);
                    var button = new List<Vector2> { new Vector2(r * 0.72f, 0f), new Vector2(r * 0.72f, 0.12f), new Vector2(r * 0.62f, 0.16f), new Vector2(0f, 0.16f) };
                    var b = Look.AddMesh("Button", Meshes.Lathe(button, 32), Look.Lit(c, 0.8f), go.transform, Vector3.zero, false);
                    vis.Parts.Add(b);
                    AddSymbol(b.transform, z.Group, new Vector3(0f, 0f, -0.165f), r * 0.6f);
                    // An inviting glow ring while it waits to be pressed.
                    vis.Sparkles.Add(MakeSprite("Invite", Shapes.Ring, new Color(c.r, c.g, c.b, 0.6f), go.transform, new Vector3(0f, 0f, -0.01f),
                        Vector2.one * (w * 1.15f), 2));
                    break;
                }
            }
        }

        // A dotted line on the floor from each switch to its gates, in the shared color.
        static void ConnectSwitches(LevelInstance inst)
        {
            foreach (var z in inst.Zones)
            {
                if (z.Kind != ZoneKind.Switch) continue;
                Color c = Palette.GateColors[Mathf.Max(0, z.Def.Group) % Palette.GateColors.Length];
                foreach (var g in inst.Gates)
                {
                    if (g.Def.Group != z.Def.Group) continue;
                    Vector2 a = z.Def.Center, b = g.Def.Center;
                    float len = (b - a).magnitude;
                    int dots = Mathf.FloorToInt(len / 0.45f);
                    for (int i = 1; i < dots; i++)
                    {
                        Vector2 p = Vector2.Lerp(a, b, i / (float)dots);
                        MakeSprite("Link", Shapes.Circle, new Color(c.r, c.g, c.b, 0.4f), inst.Root.transform, new Vector3(p.x, p.y, -0.006f),
                            Vector2.one * 0.11f, 1);
                    }
                }
            }
        }

        // A white symbol shared by a switch and its gates: circle, star, or triangle.
        static void AddSymbol(Transform parent, int group, Vector3 pos, float size)
        {
            List<Vector2> shape;
            switch (Mathf.Max(0, group) % 3)
            {
                case 0: shape = Geometry.Circle(Vector2.zero, size * 0.5f, 24); break;
                case 1: shape = Geometry.Star(Vector2.zero, size * 0.55f, size * 0.25f, 5); break;
                default: shape = Geometry.Circle(Vector2.zero, size * 0.55f, 3); break;
            }
            Look.AddMesh("Symbol", Meshes.Extrude(shape, 0.015f), Look.Toy(Color.white), parent, pos, false);
        }

        static List<Vector2> Chevron(float cw)
        {
            float d = cw * 0.45f, th = cw * 0.22f;
            return new List<Vector2>
            {
                new Vector2(-cw / 2f, -d / 2f), new Vector2(0f, d / 2f), new Vector2(cw / 2f, -d / 2f),
                new Vector2(cw / 2f - th, -d / 2f), new Vector2(0f, d / 2f - th * 1.6f), new Vector2(-cw / 2f + th, -d / 2f),
            };
        }

        static Puck MakePuck(LevelDef def, Transform parent, SkinDef skin) => MakePuck(def.Start, def.Hole, parent, skin);

        // A mascot outside any level (menus and icons). Its body is not simulated.
        public static Puck MakeMascot(Transform parent, Vector3 position, SkinDef skin)
        {
            EnsureMaterials();
            var p = MakePuck(Vector2.zero, Vector2.one * 999f, parent, skin);
            p.transform.position = position;
            p.Body.simulated = false;
            return p;
        }

        static Puck MakePuck(Vector2 start, Vector2 hole, Transform parent, SkinDef skin)
        {
            var go = new GameObject("Puck");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = start;

            var body = go.AddComponent<Rigidbody2D>();
            body.gravityScale = 0f;
            body.linearDamping = Rules.Wood.Drag;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            body.sleepMode = RigidbodySleepMode2D.NeverSleep;

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = Rules.PuckRadius;
            col.sharedMaterial = puckMat;

            // The look is cosmetic: every skin shares the collider above.
            var visual = new GameObject("Visual").transform;
            visual.SetParent(go.transform, false);
            var spin = new GameObject("Spin").transform;
            spin.SetParent(visual, false);
            BuildPuckLook(spin, skin);

            var shadow = MakeSprite("Blob Shadow", Textures.Glow, new Color(0.25f, 0.13f, 0.05f, 0.4f), go.transform,
                new Vector3(0.04f, -0.05f, -0.006f), Vector2.one * (Rules.PuckRadius * 2.6f), 2);

            var puck = go.AddComponent<Puck>();
            puck.Init(body, col, visual, spin, shadow, hole);
            go.AddComponent<Mascot>().Init(puck, skin);
            return puck;
        }

        // The employee: a chibi office worker in soft blocks (big cube head, compact white-shirted body, short limbs) built as
        // named parts under one "Roll" pivot at the collision ball's center. Mascot blends the parts between a relaxed
        // standing pose and a tucked cannonball while rolling; everything (hair, glasses, tie, hat) hangs off this
        // one hierarchy, so nothing can slide off its anchor. Every look shares the puck's round collider.
        public static void BuildPuckLook(Transform parent, SkinDef skin) => People.Build(parent, skin);


        // Hats and head items, in soft blocks like the people. The pivot sits on top of the head, square to it; -Z is up.
        public static void BuildAccessory(Transform t, SkinDef skin)
        {
            var extra = Look.Soft(skin.Extra, 0.6f, null, 0.2f);
            var white = Look.Lit(new Color(1f, 0.98f, 0.95f), 0.4f);
            var gold = Look.Lit(Palette.Gold, 0.9f);
            MeshRenderer Box(string name, Vector3 size, Material m, Vector3 center, Vector3 euler, float round = -1f, Transform under = null)
            {
                float r = round >= 0f ? round : Mathf.Min(size.x, Mathf.Min(size.y, size.z)) * 0.32f;
                var mr = Look.AddMesh(name, Meshes.SoftBox(size, r), m, under ?? t, center, false);
                mr.transform.localRotation = Quaternion.Euler(euler);
                return mr;
            }
            switch (skin.Accessory)
            {
                case Accessory.Tuft:
                    break;  // no hat: the hair shows
                case Accessory.Bow:
                {
                    // Both loops meet at the knot; the whole ribbon has one attachment point.
                    t.localPosition += new Vector3(-0.1f, 0f, 0.01f);
                    var ribbon = Look.Lit(skin.Extra, 0.32f);
                    Box("Left Loop", new Vector3(0.15f, 0.07f, 0.1f), ribbon, new Vector3(-0.075f, 0f, -0.02f), new Vector3(0f, -14f, 0f));
                    Box("Right Loop", new Vector3(0.15f, 0.07f, 0.1f), ribbon, new Vector3(0.075f, 0f, -0.02f), new Vector3(0f, 14f, 0f));
                    Box("Knot", new Vector3(0.06f, 0.08f, 0.07f), ribbon, new Vector3(0f, -0.01f, -0.025f), Vector3.zero);
                    break;
                }
                case Accessory.Sprout:
                    Box("Stem", new Vector3(0.03f, 0.03f, 0.13f), extra, new Vector3(0f, 0f, -0.06f), Vector3.zero);
                    Box("Leaf", new Vector3(0.13f, 0.07f, 0.03f), extra, new Vector3(0.07f, 0f, -0.13f), new Vector3(0f, -25f, 0f));
                    Box("Leaf", new Vector3(0.13f, 0.07f, 0.03f), extra, new Vector3(-0.07f, 0f, -0.13f), new Vector3(0f, 25f, 0f));
                    break;
                case Accessory.Antenna:
                {
                    Box("Stem", new Vector3(0.026f, 0.026f, 0.2f), Look.Lit(new Color(0.35f, 0.35f, 0.45f), 0.7f), new Vector3(0f, 0f, -0.1f), Vector3.zero);
                    var ball = Box("Ball", Vector3.one * 0.1f, extra, new Vector3(0f, 0f, -0.23f), Vector3.zero);
                    ball.gameObject.AddComponent<Wiggle>().Init(Wiggle.Mode.Pulse, 0.12f, 6f);
                    break;
                }
                case Accessory.CatEars:
                    foreach (float side in new[] { 1f, -1f })
                    {
                        // A block turned on its corner reads as a pointed ear.
                        Box("Ear", new Vector3(0.13f, 0.07f, 0.13f), Look.Lit(skin.HairColor, 0.55f), new Vector3(0.17f * side, 0.02f, -0.03f), new Vector3(0f, 45f, 0f), 0.025f);
                        Box("Inner", new Vector3(0.07f, 0.02f, 0.07f), Look.Soft(skin.Cheek, 0.3f, null, 0.3f), new Vector3(0.17f * side, -0.03f, -0.035f), new Vector3(0f, 45f, 0f), 0.01f);
                    }
                    break;
                case Accessory.Beanie:
                    Box("Hat", new Vector3(0.6f, 0.58f, 0.16f), extra, new Vector3(0f, 0f, -0.04f), Vector3.zero, 0.07f);
                    Box("Brim", new Vector3(0.62f, 0.6f, 0.07f), white, new Vector3(0f, 0f, 0.05f), Vector3.zero, 0.03f);
                    Box("Pom", Vector3.one * 0.12f, white, new Vector3(0f, 0f, -0.17f), new Vector3(0f, 0f, 20f), 0.045f);
                    break;
                case Accessory.PartyHat:
                {
                    // A stepped party hat: three stacked blocks, striped, with a pom on top.
                    var stripes = Look.Lit(Color.white, 0.5f, Textures.HutStripes(skin.Extra));
                    Box("Hat", new Vector3(0.24f, 0.24f, 0.12f), stripes, new Vector3(0f, 0f, -0.05f), Vector3.zero, 0.04f);
                    Box("Hat", new Vector3(0.17f, 0.17f, 0.11f), stripes, new Vector3(0f, 0.01f, -0.15f), Vector3.zero, 0.035f);
                    Box("Hat", new Vector3(0.1f, 0.1f, 0.1f), stripes, new Vector3(0f, 0.02f, -0.24f), Vector3.zero, 0.03f);
                    Box("Pom", Vector3.one * 0.08f, Look.Lit(Themes.Hex(0xFFD45C), 0.5f), new Vector3(0f, 0.02f, -0.31f), new Vector3(0f, 0f, 30f), 0.03f);
                    break;
                }
                case Accessory.Crown:
                {
                    var band = Look.Lit(skin.Extra, 0.9f);
                    Box("Band", new Vector3(0.34f, 0.32f, 0.09f), band, new Vector3(0f, 0f, -0.03f), Vector3.zero, 0.03f);
                    for (int i = 0; i < 3; i++)
                        Box("Point", new Vector3(0.07f, 0.03f, 0.07f), band, new Vector3((i - 1) * 0.12f, -0.15f, -0.1f), new Vector3(0f, 45f, 0f), 0.012f);
                    for (int i = 0; i < 3; i++)
                        Box("Gem", new Vector3(0.045f, 0.02f, 0.045f), Look.Lit(i % 2 == 0 ? Themes.Hex(0xFF5E9A) : Themes.Hex(0x5AB2FF), 0.9f),
                            new Vector3((i - 1) * 0.11f, -0.165f, -0.03f), new Vector3(0f, 45f, 0f), 0.01f);
                    break;
                }
                case Accessory.Horns:
                    foreach (float side in new[] { 1f, -1f })
                    {
                        Box("Horn", new Vector3(0.09f, 0.09f, 0.1f), extra, new Vector3(0.14f * side, 0f, -0.03f), new Vector3(0f, 20f * side, 0f));
                        Box("Horn Tip", new Vector3(0.06f, 0.06f, 0.08f), extra, new Vector3(0.17f * side, 0f, -0.11f), new Vector3(0f, 32f * side, 0f));
                    }
                    break;
                case Accessory.Flower:
                {
                    var head = new GameObject("Flower").transform;
                    head.SetParent(t, false);
                    head.localPosition = new Vector3(0.15f, 0.02f, -0.02f);
                    head.localRotation = Quaternion.Euler(0f, 25f, 0f);
                    for (int i = 0; i < 4; i++)
                    {
                        float a = i * Mathf.PI * 0.5f;
                        Box("Petal", new Vector3(0.08f, 0.08f, 0.04f), white, new Vector3(Mathf.Cos(a) * 0.06f, Mathf.Sin(a) * 0.06f, -0.03f), new Vector3(0f, 0f, 45f), 0.02f, head);
                    }
                    Box("Center", new Vector3(0.07f, 0.07f, 0.05f), Look.Lit(Themes.Hex(0xFFC83D), 0.5f), new Vector3(0f, 0f, -0.05f), Vector3.zero, 0.02f, head);
                    Box("Leaf", new Vector3(0.12f, 0.06f, 0.025f), extra, new Vector3(0.02f, -0.07f, 0.0f), new Vector3(0f, 0f, 30f), 0.01f, head);
                    break;
                }
                case Accessory.Halo:
                {
                    // A square halo: four bars on a rounded frame.
                    var halo = new GameObject("Halo").transform;
                    halo.SetParent(t, false);
                    halo.localPosition = new Vector3(0f, 0f, -0.16f);
                    var glow = Look.Lit(skin.Extra, 0.9f);
                    foreach (var (x, y, w, h) in new[] { (0f, 0.14f, 0.32f, 0.05f), (0f, -0.14f, 0.32f, 0.05f), (0.14f, 0f, 0.05f, 0.32f), (-0.14f, 0f, 0.05f, 0.32f) })
                        Box("Bar", new Vector3(w, h, 0.045f), glow, new Vector3(x, y, 0f), Vector3.zero, 0.02f, halo);
                    halo.gameObject.AddComponent<Wiggle>().Init(Wiggle.Mode.Bob, 0.04f, 3f);
                    break;
                }
                case Accessory.Cap:
                    // A ball cap: a soft block crown with a flat brim toward the face.
                    Box("Crown", new Vector3(0.56f, 0.54f, 0.12f), extra, new Vector3(0f, 0.01f, -0.02f), Vector3.zero, 0.05f);
                    Box("Brim", new Vector3(0.44f, 0.22f, 0.03f), extra, new Vector3(0f, -0.34f, 0.02f), new Vector3(-8f, 0f, 0f), 0.012f);
                    Box("Button", new Vector3(0.06f, 0.06f, 0.03f), white, new Vector3(0f, 0f, -0.09f), Vector3.zero, 0.012f);
                    break;
                case Accessory.BunnyEars:
                    foreach (float side in new[] { 1f, -1f })
                    {
                        Box("Ear", new Vector3(0.1f, 0.06f, 0.3f), extra, new Vector3(0.1f * side, 0.02f, -0.15f), new Vector3(0f, 12f * side, 0f), 0.03f);
                        Box("Inner", new Vector3(0.055f, 0.02f, 0.22f), Look.Lit(skin.Cheek, 0.4f), new Vector3(0.1f * side, -0.02f, -0.15f), new Vector3(0f, 12f * side, 0f), 0.01f);
                    }
                    break;
                case Accessory.Headphones:
                {
                    var dark = Look.Lit(Themes.Hex(0x2B2440), 0.6f);
                    Box("Band", new Vector3(0.62f, 0.07f, 0.05f), dark, new Vector3(0f, 0.02f, -0.02f), Vector3.zero, 0.02f);
                    foreach (float side in new[] { 1f, -1f })
                    {
                        Box("Band Side", new Vector3(0.05f, 0.07f, 0.2f), dark, new Vector3(0.31f * side, 0.02f, 0.08f), Vector3.zero, 0.02f);
                        Box("Cup", new Vector3(0.08f, 0.18f, 0.18f), extra, new Vector3(0.315f * side, 0.02f, 0.2f), Vector3.zero, 0.04f);
                    }
                    break;
                }
                case Accessory.ChefHat:
                    Box("Band", new Vector3(0.46f, 0.44f, 0.1f), extra, new Vector3(0f, 0f, -0.03f), Vector3.zero, 0.035f);
                    Box("Puff", new Vector3(0.52f, 0.5f, 0.24f), extra, new Vector3(0f, 0f, -0.18f), Vector3.zero, 0.11f);
                    break;
                case Accessory.TopHat:
                    Box("Brim", new Vector3(0.62f, 0.6f, 0.035f), extra, new Vector3(0f, 0.02f, 0.0f), Vector3.zero, 0.015f);
                    Box("Crown", new Vector3(0.38f, 0.36f, 0.28f), extra, new Vector3(0f, 0.02f, -0.15f), Vector3.zero, 0.05f);
                    Box("Ribbon", new Vector3(0.395f, 0.375f, 0.06f), Look.Lit(Themes.Hex(0xFF4D6D), 0.6f), new Vector3(0f, 0.02f, -0.05f), Vector3.zero, 0.03f);
                    break;
                case Accessory.WizardHat:
                    Box("Brim", new Vector3(0.64f, 0.62f, 0.035f), extra, new Vector3(0f, 0.02f, 0.0f), Vector3.zero, 0.015f);
                    Box("Cone", new Vector3(0.34f, 0.34f, 0.16f), extra, new Vector3(0f, 0.03f, -0.09f), new Vector3(8f, 0f, 0f), 0.05f);
                    Box("Cone", new Vector3(0.23f, 0.23f, 0.14f), extra, new Vector3(0f, 0.06f, -0.22f), new Vector3(14f, 0f, 0f), 0.04f);
                    Box("Cone", new Vector3(0.13f, 0.13f, 0.12f), extra, new Vector3(0f, 0.11f, -0.33f), new Vector3(20f, 0f, 0f), 0.035f);
                    for (int i = 0; i < 3; i++)
                        Box("Star", new Vector3(0.045f, 0.02f, 0.045f), gold, new Vector3(0.06f * (i - 1), -0.17f + 0.04f * i, -0.08f - 0.09f * i), new Vector3(0f, 45f, 0f), 0.01f);
                    break;
                case Accessory.Propeller:
                {
                    Box("Cap", new Vector3(0.26f, 0.26f, 0.08f), Look.Lit(Themes.Hex(0xFF5E57), 0.6f), new Vector3(0f, 0f, -0.01f), Vector3.zero, 0.035f);
                    Box("Pin", new Vector3(0.025f, 0.025f, 0.08f), white, new Vector3(0f, 0f, -0.08f), Vector3.zero, 0.01f);
                    var blades = new GameObject("Blades").transform;
                    blades.SetParent(t, false);
                    blades.localPosition = new Vector3(0f, 0f, -0.12f);
                    foreach (float a in new[] { 0f, 90f })
                        Box("Blade", new Vector3(0.32f, 0.06f, 0.02f), extra, Vector3.zero, new Vector3(0f, 0f, a), 0.01f, blades);
                    blades.gameObject.AddComponent<Wiggle>().Init(Wiggle.Mode.Spin, 0f, 9f);
                    break;
                }
            }
        }

        // Flat sprite, used for aim dots, glows, and effects.
        public static SpriteRenderer MakeSprite(string name, Sprite sprite, Color color, Transform parent,
            Vector3 localPos, Vector2 scale, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = color;
            sr.sortingOrder = order;
            return sr;
        }
    }
}
