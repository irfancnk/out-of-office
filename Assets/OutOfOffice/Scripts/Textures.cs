using System.Collections.Generic;
using UnityEngine;

namespace OutOfOffice
{
    // Procedural textures and UI sprites, so the game ships with no art files except the font.
    public static class Textures
    {
        static Texture2D woodFrame, hazard, paintedFrame;
        static readonly Dictionary<Color, Texture2D> hutStripes = new Dictionary<Color, Texture2D>();

        // ---------- Office floors ----------
        // Quiet, seamless surfaces at believable scale: detail stays well below the contrast of gameplay objects.

        static readonly Texture2D[] officeFloors = new Texture2D[7];
        static Texture2D fabric;

        public static Texture2D OfficeFloor(Setting setting)
        {
            int index = (int)setting;
            if (officeFloors[index] != null) return officeFloors[index];
            Texture2D tex;
            switch (setting)
            {
                case Setting.OpenPlan: tex = CarpetTiles(Hex(0xB9C2CF), 4, 1); break;
                case Setting.Kitchen: tex = Tiles(Hex(0xF2EADB), Hex(0xD8CCB7), 8, 7); break;
                case Setting.Meeting: tex = Carpet(Hex(0x5A6488), 0.05f, 13); break;
                case Setting.Manager: tex = Wood(512, 6, new Color(0.86f, 0.66f, 0.47f), new Color(0.77f, 0.56f, 0.38f), 0.32f, true, 21); break;
                case Setting.HR: tex = Vinyl(Hex(0xD3DCCB), 29); break;
                case Setting.Executive: tex = Tiles(Hex(0xEFEBE5), Hex(0xDAD3CA), 2, 37, true); break;
                default: tex = Carpet(Hex(0x363D5E), 0.06f, 41); break;
            }
            return officeFloors[index] = tex;
        }

        // A big soft two-tone checker: bright, flat, and quiet behind the gameplay pieces.
        static Texture2D CleanFloor(Color a, Color b)
        {
            const int size = 256, cells = 4;
            const float cell = size / (float)cells;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float fx = (x + 0.5f) / cell, fy = (y + 0.5f) / cell;
                bool odd = ((int)fx + (int)fy) % 2 == 1;
                // A thin soft seam between cells, a touch darker than both tones.
                float ex = Mathf.Abs(fx - Mathf.Round(fx)) * cell, ey = Mathf.Abs(fy - Mathf.Round(fy)) * cell;
                float seam = 1f - Mathf.Clamp01(Mathf.Min(ex, ey) - 0.6f);
                Color c = odd ? b : a;
                c = Color.Lerp(c, b * 0.95f, seam * 0.5f);
                c.a = 1f;
                px[y * size + x] = c;
            }
            return Make(size, px, TextureWrapMode.Repeat);
        }

        // Fine woven fabric for desk partitions, tinted by the wall color.
        public static Texture2D Fabric
        {
            get
            {
                if (fabric != null) return fabric;
                const int size = 256;
                var px = new Color[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float weave = (Mathf.Sin(x * 1.6f) * Mathf.Sin(y * 1.6f)) * 0.03f;
                    float n = (Hash(x * 7919 + y * 104729) - 0.5f) * 0.08f;
                    float v = 0.94f + weave + n;
                    px[y * size + x] = new Color(v, v, v, 1f);
                }
                return fabric = Make(size, px, TextureWrapMode.Repeat);
            }
        }

        // Tileable smooth noise in 0..1 (sampled on a torus so the texture wraps).
        static float Wrapped(int x, int y, int size, float freq, int seed)
        {
            float u = x / (float)size * Mathf.PI * 2f, v = y / (float)size * Mathf.PI * 2f;
            float r = freq / Mathf.PI;
            return Mathf.PerlinNoise(seed + Mathf.Cos(u) * r, seed * 1.7f + Mathf.Sin(u) * r + Mathf.Cos(v) * r) * 0.5f
                 + Mathf.PerlinNoise(seed * 2.3f + Mathf.Sin(v) * r, Mathf.Cos(u) * r * 0.7f) * 0.5f;
        }

        // Broadloom carpet: a fine speckled pile with faint rows and a few soft tufts.
        static Texture2D Carpet(Color baseColor, float speckle, int seed)
        {
            const int size = 512;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float pile = (Hash(x * 7919 + y * 104729 + seed) - 0.5f) * speckle * 2f;
                float rows = Mathf.Sin(y * 0.9f) * 0.012f;
                float soft = (Wrapped(x, y, size, 6f, seed) - 0.5f) * 0.05f;
                Color c = baseColor * (1f + pile + rows + soft);
                c.a = 1f;
                px[y * size + x] = c;
            }
            return Make(size, px, TextureWrapMode.Repeat);
        }

        // Carpet tiles: square tiles laid with alternating pile direction, so they shade a touch differently.
        static Texture2D CarpetTiles(Color baseColor, int tiles, int seed)
        {
            const int size = 512;
            int T = size / tiles;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int tx = x / T, ty = y / T;
                bool turned = (tx + ty) % 2 == 0;
                float pile = (Hash(x * 7919 + y * 104729 + seed) - 0.5f) * 0.09f;
                float grain = (turned ? Mathf.Sin(x * 1.3f) : Mathf.Sin(y * 1.3f)) * 0.015f;
                float shade = turned ? 0.008f : -0.006f;
                int lx = x % T, ly = y % T;
                float seam = lx < 1 || ly < 1 ? -0.03f : 0f;
                Color c = baseColor * (1f + pile + grain + shade + seam);
                c.a = 1f;
                px[y * size + x] = c;
            }
            return Make(size, px, TextureWrapMode.Repeat);
        }

        // Ceramic or stone tiles with thin, restrained grout. `stone` adds soft veining for polished floors.
        static Texture2D Tiles(Color tile, Color grout, int tiles, int seed, bool stone = false)
        {
            const int size = 512;
            int T = size / tiles;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int tx = x / T, ty = y / T, lx = x % T, ly = y % T;
                float vary = (Hash(tx * 31 + ty * 17 + seed) - 0.5f) * 0.035f;
                float n = (Wrapped(x, y, size, 10f, seed) - 0.5f) * 0.03f;
                Color c = tile * (1f + vary + n);
                if (stone)
                {
                    float vein = Mathf.Abs(Mathf.Sin((x * 0.012f + Wrapped(x, y, size, 4f, seed + 5) * 6f) * 2f));
                    c = Color.Lerp(c, c * 0.93f, Mathf.Pow(1f - vein, 40f) * 0.5f);
                }
                // A soft bevel on each tile edge, then the grout line.
                float edge = Mathf.Min(Mathf.Min(lx, T - 1 - lx), Mathf.Min(ly, T - 1 - ly));
                if (edge < 4f) c *= 0.97f + 0.0075f * edge;
                if (lx < 2 || ly < 2) c = grout;
                c.a = 1f;
                px[y * size + x] = c;
            }
            return Make(size, px, TextureWrapMode.Repeat);
        }

        // Soft-textured vinyl: a calm mottle with sparse flecks.
        static Texture2D Vinyl(Color baseColor, int seed)
        {
            const int size = 512;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float mottle = (Wrapped(x, y, size, 7f, seed) - 0.5f) * 0.08f;
                float h = Hash(x * 7919 + y * 104729 + seed);
                float fleck = h > 0.992f ? 0.08f : h < 0.006f ? -0.08f : 0f;
                Color c = baseColor * (1f + mottle + fleck + (h - 0.5f) * 0.015f);
                c.a = 1f;
                px[y * size + x] = c;
            }
            return Make(size, px, TextureWrapMode.Repeat);
        }

        static Color Hex(int rgb) => Themes.Hex(rgb);

        // Painted wood with a light grain. Tinted per chapter.
        public static Texture2D PaintedFrame => paintedFrame != null ? paintedFrame
            : paintedFrame = Wood(512, 3, Color.white, new Color(0.88f, 0.88f, 0.88f), 0.5f, false, 57);

        // Beach-hut paint: vertical bands of white and the given color.
        public static Texture2D HutStripes(Color color)
        {
            if (hutStripes.TryGetValue(color, out var t) && t != null) return t;
            const int size = 128;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float s = Mathf.Repeat(x / (size / 4f), 1f);
                float k = Mathf.Clamp01((Mathf.Abs(s - 0.5f) - 0.25f) * 40f + 0.5f);
                px[y * size + x] = Color.Lerp(color, new Color(1f, 0.98f, 0.95f), k);
            }
            return hutStripes[color] = Make(size, px, TextureWrapMode.Repeat);
        }

        static Texture2D caution;

        // Yellow-and-black caution tape stripes.
        public static Texture2D CautionTape
        {
            get
            {
                if (caution != null) return caution;
                const int size = 128;
                var yellow = new Color(1f, 0.82f, 0.18f);
                var black = new Color(0.16f, 0.15f, 0.2f);
                var px = new Color[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float t = Mathf.Repeat((x + y) / 16f, 2f);
                    float k = Mathf.Clamp01((Mathf.Abs(t - 1f) - 0.5f) * 8f + 0.5f);
                    px[y * size + x] = Color.Lerp(yellow, black, k);
                }
                return caution = Make(size, px, TextureWrapMode.Repeat);
            }
        }

        // Candy-stripe paint for hazards: readable as "danger" without looking scary.
        public static Texture2D HazardStripes
        {
            get
            {
                if (hazard != null) return hazard;
                const int size = 256;
                var red = new Color(1f, 0.42f, 0.45f);
                var cream = new Color(1f, 0.95f, 0.9f);
                var px = new Color[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float t = Mathf.Repeat((x + y) / 16f, 2f);
                    float k = Mathf.Clamp01((Mathf.Abs(t - 1f) - 0.45f) * 6f + 0.5f);
                    px[y * size + x] = Color.Lerp(cream, red, k);
                }
                return hazard = Make(size, px, TextureWrapMode.Repeat);
            }
        }
        static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();

        // Honey wood with stronger grain, for the tray rim.
        public static Texture2D WoodFrame => woodFrame != null ? woodFrame
            : woodFrame = Wood(512, 3, new Color(0.95f, 0.72f, 0.45f), new Color(0.78f, 0.50f, 0.27f), 0.8f, false, 11);

        static Texture2D Wood(int size, int planks, Color light, Color dark, float strength, bool gaps, int seed)
        {
            var px = new Color[size * size];
            int plankW = size / planks;
            for (int x = 0; x < size; x++)
            {
                int plank = x / plankW;
                float shade = 1f + (Hash(plank + seed) - 0.5f) * 0.1f;
                float offset = Hash(plank * 7 + seed) * 300f;
                int joint = (int)(Hash(plank * 13 + seed) * size);
                for (int y = 0; y < size; y++)
                {
                    float yy = y + offset;
                    float warp = Mathf.PerlinNoise(x * 0.015f + plank * 3.1f, yy * 0.004f) * 10f;
                    float rings = Mathf.Sin(x * 0.11f + warp) * 0.5f + 0.5f;
                    float fine = Mathf.PerlinNoise(x * 0.25f + seed, yy * 0.015f);
                    float g = Mathf.Clamp01(rings * rings * 0.7f + fine * 0.4f) * strength;
                    Color c = Color.Lerp(light, dark, g) * shade;
                    if (gaps)
                    {
                        int lx = x % plankW;
                        if (lx < 2 || lx > plankW - 2) c *= 0.82f;
                        if (Mathf.Abs(y - joint) < 2) c *= 0.85f;
                    }
                    c.a = 1f;
                    px[y * size + x] = c;
                }
            }
            return Make(size, px, TextureWrapMode.Repeat);
        }

        static Texture2D Make(int size, Color[] px, TextureWrapMode wrap)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                wrapMode = wrap,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4,
            };
            tex.SetPixels(px);
            tex.Apply(true, true);
            return tex;
        }

        static float Hash(int n)
        {
            n = (n << 13) ^ n;
            return ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 2147483647f;
        }

        // ---------- UI sprites ----------

        // A chunky 3D-looking button or panel: light face, darker bottom lip, outline. Grayscale, tint with Image.color.
        public static Sprite Button => Cached("button", () => Panel(128, 44, 12, 4));

        // Flat rounded panel with a soft inner gradient, for cards and pills.
        public static Sprite Card => Cached("card", () => Panel(128, 40, 8, 4));

        public static Sprite Star => Cached("star", () => StarSprite(new Color(1f, 0.89f, 0.33f), new Color(1f, 0.68f, 0.16f), new Color(0.78f, 0.44f, 0.05f)));
        public static Sprite StarEmpty => Cached("starEmpty", () => StarSprite(new Color(0.93f, 0.88f, 0.82f), new Color(0.84f, 0.78f, 0.71f), new Color(0.62f, 0.54f, 0.46f)));
        public static Sprite RestartIcon => Cached("restart", () => Icon(IconShape.Restart));
        public static Sprite NextIcon => Cached("next", () => Icon(IconShape.Next));
        public static Sprite Dot => Cached("dot", () => Icon(IconShape.Dot));
        public static Sprite PauseIcon => Cached("pause", () => Icon(IconShape.Pause));
        public static Sprite UndoIcon => Cached("undo", () => Icon(IconShape.Undo));
        public static Sprite LockIcon => Cached("lock", () => Icon(IconShape.Lock));
        public static Sprite CheckIcon => Cached("check", () => Icon(IconShape.Check));

        public static Sprite Glow => Cached("glow", () => GlowSprite(128));
        public static Sprite GlowRing => Cached("glowRing", () => GlowRingSprite(256));

        // Round sunburst with a cream rim and anti-aliased edges, for the title screen (no stencil mask needed).
        public static Sprite SunburstDisc => Cached("sunburstDisc", () =>
        {
            const int size = 512;
            var px = new Color[size * size];
            Color center = Hex(0xB59BFF), mid = Hex(0x7A55F2), rim = Hex(0xFFE9A8);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size - 0.5f, dy = (y + 0.5f) / size - 0.5f;
                float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;   // 0 center, 1 edge
                Color c = Color.Lerp(center, mid, Mathf.Clamp01(r / 0.9f));
                float rays = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Cos(Mathf.Atan2(dy, dx) * 12f) * 3f));
                c = Color.Lerp(c, Color.Lerp(c, Color.white, 0.14f), rays * Mathf.Clamp01(1f - r * 0.6f));
                float aa = 1.5f / size * 2f;
                c = Color.Lerp(c, rim, Mathf.Clamp01((r - 0.93f) / aa));
                c.a = Mathf.Clamp01((1f - r) / aa);
                px[y * size + x] = c;
            }
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            tex.SetPixels(px);
            tex.Apply(true, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        });

        // App icon backdrop: a violet radial glow with soft sunburst rays and a gentle vignette.
        public static Sprite Sunburst => Cached("sunburst", () =>
        {
            const int size = 1024;
            var px = new Color[size * size];
            Color center = Hex(0xB59BFF), mid = Hex(0x7A55F2), edge = Hex(0x3A238F);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = (x + 0.5f) / size - 0.5f, dy = (y + 0.5f) / size - 0.46f;
                float r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;
                Color c = r < 0.45f ? Color.Lerp(center, mid, r / 0.45f) : Color.Lerp(mid, edge, Mathf.Clamp01((r - 0.45f) / 0.9f));
                float rays = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(Mathf.Cos(Mathf.Atan2(dy, dx) * 14f) * 3f));
                c = Color.Lerp(c, Color.Lerp(c, Color.white, 0.12f), rays * Mathf.Clamp01(1f - r * 0.7f));
                c.a = 1f;
                px[y * size + x] = c;
            }
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            tex.SetPixels(px);
            tex.Apply(true, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        });

        static Sprite Cached(string key, System.Func<Sprite> make)
        {
            if (sprites.TryGetValue(key, out var s) && s != null) return s;
            return sprites[key] = make();
        }

        static Sprite Panel(int size, int radius, int lip, int outline)
        {
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float outer = RoundRect(p, new Vector2(0, 0), new Vector2(size, size), radius);
                float face = RoundRect(p, new Vector2(0, lip), new Vector2(size, size), radius);
                float a = Mathf.Clamp01(0.5f - outer);
                if (a <= 0f) { px[y * size + x] = Color.clear; continue; }
                float v;
                if (outer > -outline) v = 0.55f;                    // outline
                else if (face > -outline * 0.5f) v = 0.72f;          // bottom lip
                else
                {
                    float t = (y - lip) / (float)(size - lip);
                    v = Mathf.Lerp(0.9f, 1f, t);                       // face gradient
                    if (face > -outline - 5f && y > size * 0.55f) v = 1f;  // rim highlight
                    if (y > size - radius * 0.9f && face < -outline - 7f) v = Mathf.Min(1f, v + 0.04f);
                }
                px[y * size + x] = new Color(v, v, v, a);
            }
            var tex = UiTexture(size, px);
            int b = radius + 2;
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect,
                new Vector4(b, b + lip, b, b));
        }

        static Sprite StarSprite(Color top, Color bottom, Color edge)
        {
            const int size = 128;
            var poly = Geometry.Star(new Vector2(size / 2f, size / 2f - 3f), size * 0.42f, size * 0.22f, 5);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float d = SignedDistance(p, poly) - 6f;  // rounded points
                float a = Mathf.Clamp01(0.5f - d);
                if (a <= 0f) { px[y * size + x] = Color.clear; continue; }
                Color c = Color.Lerp(bottom, top, y / (float)size);
                if (d > -6f) c = edge;
                float hl = Vector2.Distance(p, new Vector2(size * 0.4f, size * 0.62f)) / (size * 0.18f);
                if (d < -9f && hl < 1f) c = Color.Lerp(c, Color.white, (1f - hl) * 0.55f);
                c.a = a;
                px[y * size + x] = c;
            }
            return Sprite.Create(UiTexture(size, px), new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        enum IconShape { Restart, Next, Dot, Pause, Lock, Check, Undo }

        static Sprite Icon(IconShape shape)
        {
            const int size = 96;
            var c = new Vector2(size / 2f, size / 2f);
            var px = new Color[size * size];
            List<Vector2> head = null, tri1 = null, tri2 = null;
            if (shape == IconShape.Restart)
            {
                // Arrowhead where the arc ends (60 degrees), pointing along the arc into the gap.
                float a = 60f * Mathf.Deg2Rad;
                Vector2 radial = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Vector2 tangent = new Vector2(-Mathf.Sin(a), Mathf.Cos(a));
                Vector2 b = c + radial * 26f;
                head = new List<Vector2> { b + tangent * 16f, b - radial * 15f, b + radial * 15f };
            }
            else if (shape == IconShape.Undo)
            {
                // An arc over the top with the arrowhead on the left end, pointing down: the classic undo arrow.
                float a = 200f * Mathf.Deg2Rad;
                Vector2 radial = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                Vector2 tangent = new Vector2(-Mathf.Sin(a), Mathf.Cos(a));
                Vector2 b = c + new Vector2(0f, -6f) + radial * 24f;
                head = new List<Vector2> { b + tangent * 17f, b - radial * 15f, b + radial * 15f };
            }
            else if (shape == IconShape.Next)
            {
                tri1 = new List<Vector2> { new Vector2(22, 24), new Vector2(50, 48), new Vector2(22, 72) };
                tri2 = new List<Vector2> { new Vector2(48, 24), new Vector2(76, 48), new Vector2(48, 72) };
            }
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float d;
                switch (shape)
                {
                    case IconShape.Restart:
                    {
                        // A ring with a gap between 60 and 105 degrees, plus the arrowhead.
                        Vector2 v = p - c;
                        float ang = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
                        float ring = ang > 60f && ang < 105f ? 99f : Mathf.Abs(v.magnitude - 26f) - 6.5f;
                        d = Mathf.Min(ring, SignedDistance(p, head) - 2f);
                        break;
                    }
                    case IconShape.Next:
                        d = Mathf.Min(SignedDistance(p, tri1), SignedDistance(p, tri2)) - 3f;
                        break;
                    case IconShape.Undo:
                    {
                        Vector2 v = p - (c + new Vector2(0f, -6f));
                        float ang = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
                        if (ang < 0f) ang += 360f;
                        float ring = ang > 200f && ang < 340f ? 99f : Mathf.Abs(v.magnitude - 24f) - 6.5f;
                        d = Mathf.Min(ring, SignedDistance(p, head) - 2f);
                        break;
                    }
                    case IconShape.Pause:
                        d = Mathf.Min(RoundRect(p, new Vector2(26, 22), new Vector2(42, 74), 5f), RoundRect(p, new Vector2(54, 22), new Vector2(70, 74), 5f));
                        break;
                    case IconShape.Lock:
                    {
                        float body = RoundRect(p, new Vector2(24, 14), new Vector2(72, 54), 8f);
                        Vector2 v = p - new Vector2(48, 54);
                        float shackle = v.y < 0f ? 99f : Mathf.Abs(v.magnitude - 15f) - 5f;
                        d = Mathf.Min(body, shackle);
                        break;
                    }
                    case IconShape.Check:
                    {
                        d = Mathf.Min(Segment(p, new Vector2(24, 50), new Vector2(42, 30)), Segment(p, new Vector2(42, 30), new Vector2(74, 68))) - 8f;
                        break;
                    }
                    default:
                        d = Vector2.Distance(p, c) - size * 0.42f;
                        break;
                }
                float a = Mathf.Clamp01(0.5f - d);
                px[y * size + x] = new Color(1f, 1f, 1f, a);
            }
            return Sprite.Create(UiTexture(size, px), new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        static Sprite GlowSprite(int size)
        {
            var px = new Color[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                float a = Mathf.Clamp01(1f - d);
                px[y * size + x] = new Color(1f, 1f, 1f, a * a);
            }
            return Sprite.Create(UiTexture(size, px), new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        // Soft ring that is clear in the middle, so it can glow around a hole without tinting it.
        static Sprite GlowRingSprite(int size)
        {
            var px = new Color[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                float a = Mathf.Clamp01(1f - Mathf.Abs(d - 0.5f) / 0.5f) * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((d - 0.36f) / 0.1f));
                px[y * size + x] = new Color(1f, 1f, 1f, a * a);
            }
            return Sprite.Create(UiTexture(size, px), new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        static Texture2D UiTexture(int size, Color[] px)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            tex.SetPixels(px);
            tex.Apply(false, true);
            return tex;
        }

        // Signed distance from p to a rounded rectangle (negative inside).
        static float RoundRect(Vector2 p, Vector2 min, Vector2 max, float r)
        {
            Vector2 c = (min + max) * 0.5f, h = (max - min) * 0.5f - Vector2.one * r;
            Vector2 q = new Vector2(Mathf.Abs(p.x - c.x) - h.x, Mathf.Abs(p.y - c.y) - h.y);
            return Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
        }

        static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 e = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, e) / e.sqrMagnitude);
            return (p - a - e * t).magnitude;
        }

        // Signed distance from p to a polygon (negative inside).
        static float SignedDistance(Vector2 p, List<Vector2> poly)
        {
            float d = float.MaxValue;
            bool inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                Vector2 a = poly[j], b = poly[i];
                Vector2 e = b - a, w = p - a;
                float t = Mathf.Clamp01(Vector2.Dot(w, e) / e.sqrMagnitude);
                d = Mathf.Min(d, (w - e * t).magnitude);
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            return inside ? -d : d;
        }
    }
}
