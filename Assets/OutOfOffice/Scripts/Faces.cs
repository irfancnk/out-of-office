using UnityEngine;

namespace OutOfOffice
{
    // Every employee's own face (OUT-42): the shape and colour of the eyes, the brows, the nose, the resting mouth,
    // and marks like freckles, a mole, laugh lines, eye bags or stubble. Expressions stay shared (happy, closed,
    // dizzy, star), while the open, wide and tired eyes use the person's own shape and iris, so every employee is
    // recognisable from the face alone.
    public enum EyeShape { Round, Almond, Big, Narrow, Droopy, Cat }
    public enum Iris { Brown, Hazel, Blue, Green, Grey, Amber, Dark, Violet }
    public enum BrowStyle { Arched, Thick, Thin, Straight, Bushy }
    public enum NoseStyle { Soft, Button, Long, Round, Pointy }
    public enum MouthStyle { Smile, Smirk, Grin, Polite, Lipstick }
    [System.Flags] public enum FaceMarks { None = 0, Freckles = 1, Mole = 2, Wrinkles = 4, EyeBags = 8, Stubble = 16, Dimples = 32, Rosy = 64, Lashes = 128 }

    public class FaceDef
    {
        public EyeShape Eyes;
        public Iris Iris;
        public BrowStyle Brows;
        public NoseStyle Nose;
        public MouthStyle Mouth;
        public FaceMarks Marks;

        public static readonly FaceDef Default = new FaceDef();

        public FaceDef() { }

        public FaceDef(EyeShape eyes, Iris iris, BrowStyle brows, NoseStyle nose, MouthStyle mouth, FaceMarks marks = FaceMarks.None)
        {
            Eyes = eyes;
            Iris = iris;
            Brows = brows;
            Nose = nose;
            Mouth = mouth;
            Marks = marks;
        }

        public string Key => $"{Eyes}.{Iris}.{Brows}.{Nose}.{Mouth}.{(int)Marks}";
    }

    // The face feature sprites, drawn like the rest of MascotArt: signed distances in 128 px, cached by style.
    public static partial class MascotArt
    {
        public enum Lid { Open, Tired }
        const int FeatureRes = 128, MarksRes = 160;

        struct EyeLook
        {
            public float Rx, Ry, Shear, Lid, IrisScale, Lash, Flick;
            public Color Warm, Deep;
        }

        static EyeLook LookOf(FaceDef f)
        {
            var l = new EyeLook { Rx = 36f, Ry = 46f, Lid = 99f, IrisScale = 1f, Lash = 4.5f, Flick = 1f };
            switch (f.Eyes)
            {
                case EyeShape.Big: l.Rx = 39f; l.Ry = 50f; l.IrisScale = 1.12f; break;
                case EyeShape.Almond: l.Rx = 40f; l.Ry = 35f; l.Shear = 0.1f; break;
                case EyeShape.Narrow: l.Rx = 37f; l.Ry = 27f; l.IrisScale = 0.92f; break;
                case EyeShape.Droopy: l.Rx = 37f; l.Ry = 42f; l.Shear = -0.13f; l.Lid = 92f; break;
                case EyeShape.Cat: l.Rx = 41f; l.Ry = 36f; l.Shear = 0.2f; l.Flick = 1.7f; break;
            }
            if ((f.Marks & FaceMarks.Lashes) != 0) { l.Lash = 6.5f; l.Flick = Mathf.Max(l.Flick, 1.4f); }
            (l.Warm, l.Deep) = IrisColors(f.Iris);
            return l;
        }

        static (Color warm, Color deep) IrisColors(Iris i)
        {
            switch (i)
            {
                case Iris.Hazel: return (new Color(0.74f, 0.64f, 0.3f), new Color(0.36f, 0.3f, 0.12f));
                case Iris.Blue: return (new Color(0.47f, 0.74f, 1f), new Color(0.12f, 0.3f, 0.62f));
                case Iris.Green: return (new Color(0.52f, 0.84f, 0.46f), new Color(0.1f, 0.4f, 0.22f));
                case Iris.Grey: return (new Color(0.76f, 0.81f, 0.87f), new Color(0.3f, 0.36f, 0.46f));
                case Iris.Amber: return (new Color(1f, 0.74f, 0.26f), new Color(0.56f, 0.3f, 0.06f));
                case Iris.Dark: return (new Color(0.42f, 0.28f, 0.21f), new Color(0.13f, 0.08f, 0.06f));
                case Iris.Violet: return (new Color(0.78f, 0.58f, 1f), new Color(0.34f, 0.18f, 0.58f));
                default: return (new Color(0.78f, 0.52f, 0.3f), new Color(0.33f, 0.2f, 0.14f));
            }
        }

        // The person's open (or tired) eye. Drawn as the left eye; Mascot mirrors it for the right.
        public static Sprite EyeFor(FaceDef f, Lid lid)
        {
            if (f == null) return lid == Lid.Tired ? EyeTired : EyeOpen;
            var l = LookOf(f);
            string key = $"eye {f.Eyes} {f.Iris} {(f.Marks & FaceMarks.Lashes) != 0} {lid}";
            return Get(key, (x, y) => StyledEye(x, y, l, lid == Lid.Tired ? 62f + l.Ry * 0.4f + (x - 64f) * 0.08f : l.Lid), FeatureRes);
        }

        static Color StyledEye(float x, float y, EyeLook l, float lidLine)
        {
            float yy = y - (64f - x) * l.Shear;  // tilts the outer corner (left) up, or down for droopy eyes
            float eye = Ellipse(x, yy, 64, 62, l.Rx, l.Ry);
            float shape = Mathf.Max(eye, yy - lidLine);
            float top = lidLine < 99f
                ? Mathf.Max(Mathf.Abs(yy - lidLine) - l.Lash, Mathf.Abs(x - 64f) - (l.Rx + 3f))
                : Mathf.Max(Mathf.Abs(eye) - l.Lash, 62f - yy);
            // A flick at the outer corner; longer for cat eyes and long lashes.
            float sx = 64f - l.Rx * 0.74f, sy = lidLine < 99f ? lidLine - 3f : 62f + l.Ry * 0.66f;
            float flick = Segment(x, yy, sx, sy, sx - 11f * l.Flick, sy + 7f * l.Flick) - 3.5f;
            float lash = Mathf.Min(top, flick);
            if (l.Lash > 5f && lidLine >= 99f)
                foreach (float a in new[] { 0.68f, 0.6f })
                {
                    float ca = Mathf.Cos(a * Mathf.PI), sa = Mathf.Sin(a * Mathf.PI);
                    float px = 64f + ca * l.Rx, py = 62f + sa * l.Ry;
                    lash = Mathf.Min(lash, Segment(x, yy, px, py, px + ca * 9f, py + sa * 9f) - 2.6f);
                }
            if (shape > 0f)
            {
                if (lash < 1.5f) { var ink = Ink; ink.a = Cover(lash); return ink; }
                return new Color(1f, 1f, 1f, 0f);
            }
            float s = l.IrisScale;
            float t = Mathf.Clamp01((y - 16f) / 92f);
            Color c = Color.Lerp(l.Warm, l.Deep, Mathf.SmoothStep(0f, 1f, t));
            c = Color.Lerp(c, Color.Lerp(l.Warm, Color.white, 0.25f), Cover(Mathf.Abs(Ellipse(x, y, 64, 56, 24 * s, 30 * s)) - 3f) * 0.45f);
            c = Color.Lerp(c, Ink, Cover(Ellipse(x, y, 64, 60, 15 * s, 19 * s)) * 0.85f);
            c = Color.Lerp(c, Color.white, Cover(Ellipse(x, y, 64 - 15 * s, 62 + 18 * s, 13 * s, 15 * s)));
            c = Color.Lerp(c, Color.white, Cover(Circle(x, y, 64 + 14 * s, 38, 6.5f * s)) * 0.9f);
            c = Color.Lerp(c, Ink, Cover(-eye - 3f));
            c = Color.Lerp(c, Ink, Cover(lash));
            return WithRim(c, shape, 4f);
        }

        // Brows, white (tinted by the hair colour).
        public static Sprite BrowFor(BrowStyle b)
        {
            if (b == BrowStyle.Arched) return Brow;
            return Get("brow " + b, (x, y) =>
            {
                float d;
                switch (b)
                {
                    case BrowStyle.Thick: d = BrowArc(x, y, 0.34f, 0.66f) - 13f; break;
                    case BrowStyle.Thin: d = BrowArc(x, y, 0.29f, 0.71f) - 5f; break;
                    case BrowStyle.Straight: d = Segment(x, y, 26, 63, 102, 65) - 10.5f; break;
                    default:
                    {
                        // Bushy: a thick brow with fluffy lumps along its edges.
                        d = BrowArc(x, y, 0.33f, 0.67f) - 10.5f;
                        for (int i = 0; i < 7; i++)
                        {
                            float a = Mathf.Lerp(0.33f, 0.67f, i / 6f) * Mathf.PI;
                            float r = 14f + 2.5f * Mathf.Sin(i * 2.3f);
                            d = Mathf.Min(d, Circle(x, y, 64 + Mathf.Cos(a) * 84f, Mathf.Sin(a) * 84f - 18f, r));
                        }
                        break;
                    }
                }
                return new Color(1f, 1f, 1f, Cover(d));
            }, FeatureRes);
        }

        static float BrowArc(float x, float y, float from, float to)
        {
            float best = 99f;
            for (int i = 0; i < 12; i++)
            {
                float a0 = Mathf.Lerp(from, to, i / 12f) * Mathf.PI, a1 = Mathf.Lerp(from, to, (i + 1) / 12f) * Mathf.PI;
                best = Mathf.Min(best, Segment(x, y, 64 + Mathf.Cos(a0) * 84f, Mathf.Sin(a0) * 84f - 18f, 64 + Mathf.Cos(a1) * 84f, Mathf.Sin(a1) * 84f - 18f));
            }
            return best;
        }

        // Noses, white masks tinted by the skin's shade. Size: how big Mascot draws it.
        public static Sprite NoseFor(NoseStyle n, out float size)
        {
            switch (n)
            {
                case NoseStyle.Button: size = 0.045f; break;
                case NoseStyle.Long: size = 0.075f; break;
                case NoseStyle.Round: size = 0.065f; break;
                case NoseStyle.Pointy: size = 0.075f; break;
                default: size = 0.05f; return Nose;
            }
            return Get("nose " + n, (x, y) =>
            {
                switch (n)
                {
                    case NoseStyle.Button: return new Color(1f, 1f, 1f, Mathf.Clamp01(-Circle(x, y, 64, 58, 22) / 6f) * 0.85f);
                    case NoseStyle.Long:
                    {
                        float d = Segment(x, y, 64, 92, 64, 36) - 13f;
                        return new Color(1f, 1f, 1f, Mathf.Clamp01(-d / 6f) * Mathf.Lerp(0.85f, 0.3f, Mathf.Clamp01((y - 36f) / 56f)));
                    }
                    case NoseStyle.Round: return new Color(1f, 1f, 1f, Mathf.Clamp01(-Ellipse(x, y, 64, 56, 40, 34) / 12f) * 0.8f);
                    default:
                    {
                        // A cartoon nose line: down the bridge and a little hook under the tip.
                        float d = Mathf.Min(Segment(x, y, 66, 104, 50, 40), Segment(x, y, 50, 40, 74, 34)) - 5f;
                        return new Color(1f, 1f, 1f, Cover(d) * 0.95f);
                    }
                }
            }, FeatureRes);
        }

        // The resting mouth. Smile, Smirk, Grin (teeth) and Polite are ink; Lipstick is coloured.
        public static Sprite RestMouthFor(MouthStyle m, out float size)
        {
            switch (m)
            {
                case MouthStyle.Lipstick: size = 0.105f; return Lips;
                case MouthStyle.Grin: size = 0.11f; break;
                case MouthStyle.Polite: size = 0.085f; break;
                case MouthStyle.Smirk: size = 0.1f; break;
                default: size = 0.1f; return MouthSmile;
            }
            return Get("mouth " + m, (x, y) =>
            {
                switch (m)
                {
                    case MouthStyle.Smirk:
                    {
                        float best = 99f;
                        for (int i = 0; i < 16; i++)
                        {
                            float a0 = Mathf.Lerp(1.26f, 1.8f, i / 16f) * Mathf.PI, a1 = Mathf.Lerp(1.26f, 1.8f, (i + 1) / 16f) * Mathf.PI;
                            float x0 = 64 + Mathf.Cos(a0) * 58f, x1 = 64 + Mathf.Cos(a1) * 58f;
                            float y0 = 112 + Mathf.Sin(a0) * 58f + (x0 - 64f) * 0.3f, y1 = 112 + Mathf.Sin(a1) * 58f + (x1 - 64f) * 0.3f;
                            best = Mathf.Min(best, Segment(x, y, x0, y0, x1, y1));
                        }
                        return Stroke(best - 7.5f, 0f);
                    }
                    case MouthStyle.Grin:
                    {
                        // A wide closed grin: a crescent of teeth under a flat top lip.
                        float shape = Mathf.Max(Circle(x, y, 64, 104, 54), y - 74f);
                        var c = Color.Lerp(Ink, new Color(0.99f, 0.98f, 0.95f), Cover(shape + 5f));
                        c = Color.Lerp(c, new Color(0.82f, 0.78f, 0.74f), Cover(Mathf.Max(Mathf.Abs(x - 64f) - 1.5f, y - 72f)) * Cover(shape + 6f));
                        c.a = Cover(shape);
                        return c;
                    }
                    default:
                    {
                        float best = 99f;
                        for (int i = 0; i < 12; i++)
                        {
                            float a0 = Mathf.Lerp(1.32f, 1.68f, i / 12f) * Mathf.PI, a1 = Mathf.Lerp(1.32f, 1.68f, (i + 1) / 12f) * Mathf.PI;
                            best = Mathf.Min(best, Segment(x, y, 64 + Mathf.Cos(a0) * 70f, 120 + Mathf.Sin(a0) * 70f, 64 + Mathf.Cos(a1) * 70f, 120 + Mathf.Sin(a1) * 70f));
                        }
                        return Stroke(best - 8f, 0f);
                    }
                }
            }, FeatureRes);
        }

        // Every mark of a face in one sprite, laid out in face units (half a unit across, centred on the face).
        public static Sprite MarksFor(FaceMarks marks)
        {
            marks &= ~(FaceMarks.Rosy | FaceMarks.Lashes);  // those change the cheeks and the eyes instead
            if (marks == FaceMarks.None) return null;
            return Get("marks " + (int)marks, (x, y) =>
            {
                Color c = new Color(0f, 0f, 0f, 0f);
                void Over(Color ink, float alpha)
                {
                    if (alpha <= 0f) return;
                    c = new Color(Mathf.Lerp(c.r, ink.r, alpha), Mathf.Lerp(c.g, ink.g, alpha), Mathf.Lerp(c.b, ink.b, alpha), Mathf.Max(c.a, alpha));
                }
                if ((marks & FaceMarks.Stubble) != 0)
                {
                    // A five o'clock shadow round the mouth and chin: a soft grey-blue shade with a fine grain.
                    float area = Mathf.Max(Ellipse(x, y, 64, 31, 36, 14), -Ellipse(x, y, 64, 38.5f, 13, 5));
                    if (area < 0f && y > 18f)
                    {
                        float grain = Mathf.Repeat(Mathf.Sin(x * 12.9898f + y * 78.233f) * 43758.55f, 1f);
                        Over(new Color(0.22f, 0.24f, 0.3f), Mathf.Clamp01(-area / 6f) * (0.15f + 0.13f * grain));
                    }
                }
                if ((marks & FaceMarks.Freckles) != 0)
                {
                    float best = 99f;
                    foreach (float side in new[] { -1f, 1f })
                    {
                        best = Mathf.Min(best, Circle(x, y, 64 + side * 24f, 54, 2.2f));
                        best = Mathf.Min(best, Circle(x, y, 64 + side * 31f, 50, 2f));
                        best = Mathf.Min(best, Circle(x, y, 64 + side * 37f, 55, 2.1f));
                        best = Mathf.Min(best, Circle(x, y, 64 + side * 29f, 58, 1.8f));
                    }
                    best = Mathf.Min(best, Mathf.Min(Circle(x, y, 59, 59, 1.7f), Circle(x, y, 69, 60, 1.7f)));
                    Over(new Color(0.66f, 0.38f, 0.2f), Cover(best) * 0.85f);
                }
                if ((marks & FaceMarks.EyeBags) != 0)
                    foreach (float side in new[] { -1f, 1f })
                    {
                        float cx = 64 + side * 30f;
                        // A soft crescent of shade under each eye.
                        float lune = Mathf.Max(Circle(x, y, cx, 56, 14), -Circle(x, y, cx, 59.5f, 14));
                        Over(new Color(0.42f, 0.27f, 0.36f), Mathf.Clamp01(-lune / 2.5f) * 0.38f);
                    }
                if ((marks & FaceMarks.Wrinkles) != 0)
                {
                    float d = 99f;
                    foreach (float side in new[] { -1f, 1f })
                    {
                        float ox = 64 + side * 47f;
                        foreach (float dy in new[] { 0.42f, 0f, -0.42f })
                            d = Mathf.Min(d, Segment(x, y, ox + side * 2f, 66 + dy * 3f, ox + side * 9f, 66 + dy * 9f) - 1.2f);
                        // Laugh lines from the nose to the mouth corners.
                        d = Mathf.Min(d, Mathf.Min(Segment(x, y, 64 + side * 15f, 50, 64 + side * 21f, 42), Segment(x, y, 64 + side * 21f, 42, 64 + side * 20f, 33)) - 1.2f);
                    }
                    Over(new Color(0.36f, 0.22f, 0.17f), Cover(d) * 0.42f);
                }
                if ((marks & FaceMarks.Dimples) != 0)
                    foreach (float side in new[] { -1f, 1f })
                    {
                        float d = Mathf.Max(Mathf.Abs(Circle(x, y, 64 + side * 25f, 41, 3.5f)) - 1.1f, side * (64f + side * 25f - x));
                        Over(new Color(0.42f, 0.24f, 0.2f), Cover(d) * 0.5f);
                    }
                if ((marks & FaceMarks.Mole) != 0)
                    Over(new Color(0.28f, 0.16f, 0.11f), Cover(Circle(x, y, 87, 34, 2.6f)) * 0.95f);
                return c;
            }, MarksRes);
        }

        // ---- More face stickers (OUT-44) ----

        // A gold monocle over the right eye, on a fine chain.
        public static Sprite Monocle => Get("monocle", (x, y) =>
        {
            float ring = Mathf.Abs(Circle(x, y, 92, 66, 22)) - 3f;
            float chain = 99f;
            for (int i = 0; i < 6; i++) chain = Mathf.Min(chain, Circle(x, y, 108 + i * 2.2f, 50 - i * 8f, 1.8f));
            var gold = Color.Lerp(new Color(0.8f, 0.55f, 0.15f), new Color(1f, 0.86f, 0.4f), Mathf.Clamp01((y - 44f) / 44f));
            if (Mathf.Min(ring, chain) < 0.5f) { gold.a = Cover(Mathf.Min(ring, chain)); return gold; }
            float shine = Segment(x, y, 80, 70, 88, 80) - 2.5f;
            return new Color(1f, 1f, 1f, Cover(Circle(x, y, 92, 66, 22)) * 0.14f + Cover(shine) * 0.4f);
        });

        // Red heart-shaped shades.
        public static Sprite HeartShades => Get("heartShades", (x, y) =>
        {
            float l = Heart(x - 36, y - 62, 24f), r = Heart(x - 92, y - 62, 24f);
            float bridge = Segment(x, y, 56, 70, 72, 70) - 3.5f;
            float d = Mathf.Min(Mathf.Min(l, r), bridge);
            Color c = Color.Lerp(new Color(0.85f, 0.1f, 0.3f), new Color(1f, 0.45f, 0.6f), Mathf.Clamp01((y - 46f) / 36f));
            c = Color.Lerp(new Color(0.45f, 0.04f, 0.16f), c, Cover(Mathf.Min(l, r) + 4f));
            c = Color.Lerp(c, Color.white, Cover(Mathf.Min(Circle(x, y, 28, 72, 4f), Circle(x, y, 84, 72, 4f))));
            c.a = Cover(d);
            return c;
        });

        static float Heart(float dx, float dy, float r)
        {
            // Two round lobes over a point, in the style of a sticker.
            float lobes = Mathf.Min(Circle(dx, dy, -r * 0.4f, r * 0.22f, r * 0.52f), Circle(dx, dy, r * 0.4f, r * 0.22f, r * 0.52f));
            float point = Mathf.Max(Mathf.Abs(dx) - (dy + r * 1.05f) * 0.62f, dy - r * 0.25f);  // narrowing to a tip below
            return Mathf.Min(lobes, point);
        }

        // Aviators: gold wire frames, teardrop lenses from dark to amber, and a double bridge.
        public static Sprite Aviators => Get("aviators", (x, y) =>
        {
            float l = Ellipse(x, y + (x < 64 ? (x - 36f) * 0.25f : 0f), 36, 62, 24, 18), r = Ellipse(x, y - (x > 64 ? (x - 92f) * 0.25f : 0f), 92, 62, 24, 18);
            float lens = Mathf.Min(l, r);
            float frame = Mathf.Min(Mathf.Abs(lens) - 2.2f, Mathf.Min(Segment(x, y, 58, 74, 70, 74), Segment(x, y, 58, 68, 70, 68)) - 1.6f);
            if (frame < 0.5f)
            {
                var gold = new Color(0.86f, 0.68f, 0.3f);
                gold.a = Cover(frame);
                return gold;
            }
            // Mirrored lenses: amber at the bottom, nearly black at the top, with a white glint.
            Color c = Color.Lerp(new Color(0.85f, 0.5f, 0.12f), new Color(0.08f, 0.07f, 0.12f), Mathf.Clamp01((y - 46f) / 30f));
            c = Color.Lerp(c, Color.white, Cover(Mathf.Min(Segment(x, y, 24, 70, 32, 60), Segment(x, y, 80, 70, 88, 60)) - 2.5f) * 0.7f);
            c.a = Cover(lens);
            return c;
        });

        // Small round gold specs: very wise, very online.
        public static Sprite RoundSpecs => Get("roundSpecs", (x, y) =>
        {
            float l = Circle(x, y, 38, 64, 19), r = Circle(x, y, 90, 64, 19);
            float ring = Mathf.Min(Mathf.Abs(l) - 2.4f, Mathf.Abs(r) - 2.4f);
            float bridge = Mathf.Abs(Circle(x, y, 64, 62, 9)) - 2f;
            if (y < 62f) bridge = 99f;
            float d = Mathf.Min(ring, bridge);
            var gold = new Color(0.78f, 0.6f, 0.24f);
            if (d < 0.5f) { gold.a = Cover(d); return gold; }
            return new Color(1f, 1f, 1f, Cover(Mathf.Min(l, r)) * 0.12f + Cover(Mathf.Min(Segment(x, y, 30, 70, 36, 76), Segment(x, y, 82, 70, 88, 76)) - 2f) * 0.4f);
        });

        // A thick handlebar mustache with upturned, curled ends.
        public static Sprite Handlebar => Get("handlebar", (x, y) =>
        {
            float body = Mathf.Min(Ellipse(x, y, 48, 64, 18, 11), Ellipse(x, y, 80, 64, 18, 11));
            float d = body;
            foreach (float side in new[] { -1f, 1f })
            {
                float cx = 64 + side * 44f;
                float curl = Mathf.Abs(Circle(x, y, cx, 76, 8)) - 4f;
                if (side * (x - cx) < 0f && y < 76f) curl = 99f;
                d = Mathf.Min(d, Mathf.Min(curl, Segment(x, y, 64 + side * 30f, 62, cx, 68) - 5f));
            }
            Color c = Color.Lerp(new Color(0.18f, 0.1f, 0.06f), new Color(0.38f, 0.24f, 0.13f), Mathf.Clamp01((y - 54f) / 18f));
            c.a = Cover(d);
            return c;
        });

        // A shiny red clown nose.
        public static Sprite ClownNose => Get("clownNose", (x, y) =>
        {
            float d = Circle(x, y, 64, 62, 44);
            Color c = Color.Lerp(new Color(0.78f, 0.06f, 0.12f), new Color(1f, 0.28f, 0.28f), Mathf.Clamp01((y - 30f) / 60f));
            c = Color.Lerp(c, Color.white, Cover(Ellipse(x, y, 50, 82, 13, 9)) * 0.85f);
            return WithRim(c, d, 4f);
        });

        // A plaster across the bridge of the nose, at a jaunty angle.
        public static Sprite BandAid => Get("bandAid", (x, y) =>
        {
            float ca = Mathf.Cos(0.35f), sa = Mathf.Sin(0.35f);
            float u = (x - 64) * ca + (y - 64) * sa, v = -(x - 64) * sa + (y - 64) * ca;
            float strip = RoundRect(u + 64, v + 64, 64, 64, 54, 15, 14);
            float pad = RoundRect(u + 64, v + 64, 64, 64, 15, 11, 3);
            Color c = new Color(0.93f, 0.68f, 0.48f);
            c = Color.Lerp(c, new Color(1f, 0.93f, 0.88f), Cover(pad));
            float holes = 99f;
            for (int i = -1; i <= 1; i += 2)
                for (int j = -1; j <= 1; j += 2)
                    holes = Mathf.Min(holes, Circle(u + 64, v + 64, 64 + i * 34f, 64 + j * 6f, 1.8f));
            c = Color.Lerp(c, new Color(0.8f, 0.6f, 0.45f), Cover(holes));
            c = Color.Lerp(new Color(0.55f, 0.35f, 0.24f), c, Cover(strip + 3.5f));
            c.a = Cover(strip);
            return c;
        });

        // VR goggles: a chunky visor with a glowing blue screen.
        public static Sprite VrGoggles => Get("vrGoggles", (x, y) =>
        {
            float box = RoundRect(x, y, 64, 64, 58, 24, 14);
            Color c = Color.Lerp(new Color(0.16f, 0.17f, 0.24f), new Color(0.32f, 0.34f, 0.44f), Mathf.Clamp01((y - 40f) / 48f));
            float screen = RoundRect(x, y, 64, 62, 48, 15, 9);
            c = Color.Lerp(c, Color.Lerp(new Color(0.1f, 0.5f, 1f), new Color(0.45f, 0.95f, 1f), Mathf.Clamp01((y - 47f) / 30f)), Cover(screen));
            c = Color.Lerp(c, Color.white, Cover(Segment(x, y, 26, 70, 44, 70) - 2.5f) * 0.7f);
            return WithRim(c, box, 4f);
        });
    }
}
