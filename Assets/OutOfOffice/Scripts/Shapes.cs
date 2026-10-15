using UnityEngine;

namespace OutOfOffice
{
    // Sprites generated at runtime so the game needs no art assets.
    public static class Shapes
    {
        public const float RoundedCorner = 0.2f;  // world units

        static Sprite circle, square, rounded, ring;

        // 1 world unit across.
        public static Sprite Circle
        {
            get
            {
                if (circle == null) circle = MakeCircle(128);
                return circle;
            }
        }

        // Thin ring, 1 world unit across. Used for impact shockwaves.
        public static Sprite Ring
        {
            get
            {
                if (ring == null) ring = MakeRing(128, 0.16f);
                return ring;
            }
        }

        // Two-tone dot for aim guides: a light core inside a dark navy rim, so it reads on light and dark floors alike.
        // Tint multiplies both tones: the core takes the tint, the rim stays dark.
        public static Sprite OutlinedDot
        {
            get
            {
                if (outlinedDot == null) outlinedDot = MakeOutlined(96, 0f, 0.58f);
                return outlinedDot;
            }
        }

        // Two-tone ring, 1 world unit across, with the same light band and dark edges.
        public static Sprite OutlinedRing
        {
            get
            {
                if (outlinedRing == null) outlinedRing = MakeOutlined(256, 0.74f, 0.6f);
                return outlinedRing;
            }
        }

        static Sprite outlinedDot, outlinedRing, confetti, darkDot, darkRing;

        // The inverse pair for pale floors: a dark navy core inside a white halo.
        public static Sprite DarkDot => darkDot != null ? darkDot : darkDot = MakeOutlined(96, 0f, 0.58f, true);
        public static Sprite DarkRing => darkRing != null ? darkRing : darkRing = MakeOutlined(256, 0.74f, 0.6f, true);

        // A little paper strip for celebrations.
        public static Sprite Confetti
        {
            get
            {
                if (confetti != null) return confetti;
                const int size = 64;
                var px = new Color32[size * size];
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Max(0f, Mathf.Abs(x + 0.5f - 32f) - 20f), dy = Mathf.Max(0f, Mathf.Abs(y + 0.5f - 32f) - 8f);
                    float d = Mathf.Sqrt(dx * dx + dy * dy) - 4f;
                    px[y * size + x] = White(Mathf.Clamp01(0.5f - d));
                }
                return confetti = Sprite.Create(Texture(size, px), new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            }
        }
        static readonly Color32 Rim = new Color32(38, 42, 88, 255);

        // A disc (hole = 0) or a ring (hole = inner radius / outer radius). `core` is the share of the band that is light.
        static Sprite MakeOutlined(int size, float hole, float core, bool darkCore = false)
        {
            var px = new Color32[size * size];
            float r = size * 0.5f, inner = r * hole;
            float mid = (r + inner) * 0.5f, half = (r - inner) * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                float band = hole > 0f ? Mathf.Abs(d - mid) : d;          // distance from the middle of the band
                float width = hole > 0f ? half : r;
                float alpha = Mathf.Clamp01(width - band) * 0.9f;
                float light = Mathf.Clamp01(width * core - band + 0.5f);
                var c = darkCore ? Color32.Lerp(new Color32(255, 255, 255, 255), Rim, light) : Color32.Lerp(Rim, new Color32(255, 255, 255, 255), light);
                c.a = (byte)(Mathf.Max(alpha, light) * 255f);
                px[y * size + x] = c;
            }
            return Sprite.Create(Texture(size, px), new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        // 1 world unit across.
        public static Sprite Square
        {
            get
            {
                if (square == null) square = MakeSquare(8);
                return square;
            }
        }

        // 9-sliced rounded rectangle. Use with SpriteDrawMode.Sliced and set SpriteRenderer.size.
        public static Sprite Rounded
        {
            get
            {
                if (rounded == null) rounded = MakeRounded(64, 12);
                return rounded;
            }
        }

        static Sprite MakeCircle(int size)
        {
            var px = new Color32[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                px[y * size + x] = White(Mathf.Clamp01(r - d));
            }
            return Sprite.Create(Texture(size, px), new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        static Sprite MakeRing(int size, float width)
        {
            var px = new Color32[size * size];
            float r = size * 0.5f, inner = r * (1f - width * 2f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r));
                px[y * size + x] = White(Mathf.Clamp01(r - d) * Mathf.Clamp01(d - inner));
            }
            return Sprite.Create(Texture(size, px), new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        static Sprite MakeSquare(int size)
        {
            var px = new Color32[size * size];
            for (int i = 0; i < px.Length; i++) px[i] = White(1f);
            return Sprite.Create(Texture(size, px), new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        static Sprite MakeRounded(int size, int radius)
        {
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float cx = x + 0.5f, cy = y + 0.5f;
                float dx = Mathf.Max(radius - cx, 0f, cx - (size - radius));
                float dy = Mathf.Max(radius - cy, 0f, cy - (size - radius));
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                px[y * size + x] = White(Mathf.Clamp01(radius - d));
            }
            float ppu = radius / RoundedCorner;
            var border = new Vector4(radius, radius, radius, radius);
            return Sprite.Create(Texture(size, px), new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                ppu, 0, SpriteMeshType.FullRect, border);
        }

        static Texture2D Texture(int size, Color32[] px)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            tex.SetPixels32(px);
            tex.Apply(false, true);
            return tex;
        }

        static Color32 White(float alpha) => new Color32(255, 255, 255, (byte)(alpha * 255f));
    }
}
