using System.Collections.Generic;
using UnityEngine;

namespace OutOfOffice
{
    // Procedural UI sprites at 2x resolution, all anti-aliased with signed distances.
    // Shapes are grayscale where noted, so Image.color tints them without losing the shading.
    public static class UiArt
    {
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        public const float ButtonRadius = 80f, ButtonLip = 22f;  // pixels in the button sprite

        // Glossy candy button: dark edge, face with a soft gradient and a top gloss band, darker bottom lip. Grayscale.
        public static Sprite Button => Get("button", () =>
        {
            const int S = 256;
            const float R = ButtonRadius, lip = ButtonLip, edge = 5f;
            return Make(S, S, (x, y) =>
            {
                var p = new Vector2(x, y);
                float outer = RoundRect(p, new Vector2(0, 0), new Vector2(S, S), R);
                float face = RoundRect(p, new Vector2(0, lip), new Vector2(S, S), R);
                float t = Mathf.Clamp01((y - lip) / (S - lip));
                float v = Mathf.Lerp(0.8f, 0.96f, t);
                float gloss = RoundRect(p, new Vector2(edge + 18, S - R * 0.85f), new Vector2(S - edge - 18, S - edge - 9), R * 0.45f);
                v = Mathf.Lerp(v, 1f, 0.9f * Mathf.Clamp01(-gloss / 6f));
                v = Mathf.Lerp(0.6f, v, Cover(face + edge * 0.6f));
                v = Mathf.Lerp(0.42f, v, Cover(outer + edge));
                return new Color(v, v, v, Cover(outer));
            }, new Vector4(R + 4, R + lip + 4, R + 4, R + 4));
        });

        // Round version of the button, for icon buttons. Grayscale.
        public static Sprite RoundButton => Get("roundButton", () =>
        {
            const int S = 256;
            const float lip = 16f, edge = 5f;
            var c = new Vector2(S / 2f, S / 2f + lip / 2f);
            float r = S / 2f - lip / 2f - 1f;
            return Make(S, S, (x, y) =>
            {
                var p = new Vector2(x, y);
                float body = Mathf.Min((p - c).magnitude - r, (p - (c - new Vector2(0, lip))).magnitude - r);
                float face = (p - c).magnitude - r;
                float t = Mathf.Clamp01((y - (c.y - r)) / (2f * r));
                float v = Mathf.Lerp(0.8f, 0.97f, t);
                float gloss = (new Vector2((p.x - c.x) / 0.78f, (p.y - (c.y + r * 0.52f)) / 0.36f)).magnitude - r * 0.55f;
                v = Mathf.Lerp(v, 1f, 0.85f * Mathf.Clamp01(-gloss / 8f));
                v = Mathf.Lerp(0.6f, v, Cover(face + edge * 0.6f));
                v = Mathf.Lerp(0.42f, v, Cover(body + edge));
                return new Color(v, v, v, Cover(body));
            });
        });

        // Soft white card with a faint bottom shade. Tint it cream.
        public static Sprite Panel => Get("panel", () =>
        {
            const int S = 256;
            const float R = 90f;
            return Make(S, S, (x, y) =>
            {
                var p = new Vector2(x, y);
                float d = RoundRect(p, Vector2.zero, new Vector2(S, S), R);
                float v = Mathf.Lerp(0.94f, 1f, Mathf.Clamp01(y / (float)S * 1.6f));
                v = Mathf.Lerp(0.86f, v, Cover(d + 6f));
                return new Color(v, v, v, Cover(d));
            }, new Vector4(R + 4, R + 4, R + 4, R + 4));
        });

        // Flat capsule (radius = half the height when sliced at the matching multiplier).
        public static Sprite Pill => Get("pill", () =>
        {
            const int S = 128;
            return Make(S, S, (x, y) => new Color(1f, 1f, 1f, Cover(RoundRect(new Vector2(x, y), Vector2.zero, new Vector2(S, S), S / 2f))),
                new Vector4(S / 2f, S / 2f, S / 2f, S / 2f));
        });

        // Blurred rounded rectangle for drop shadows under cards and buttons.
        public static Sprite SoftShadow => Get("softShadow", () =>
        {
            const int S = 256;
            const float R = 90f, blur = 36f;
            return Make(S, S, (x, y) =>
            {
                float d = RoundRect(new Vector2(x, y), new Vector2(blur, blur), new Vector2(S - blur, S - blur), R - blur * 0.5f);
                float a = 1f - Smooth(-blur * 0.6f, blur, d);
                return new Color(0f, 0f, 0f, a * a);
            }, new Vector4(R + blur, R + blur, R + blur, R + blur));
        });

        // Title ribbon with folded tails. Grayscale. Slice horizontally only.
        public static Sprite Ribbon => Get("ribbon", () =>
        {
            const int W = 512, H = 200;
            var tailL = new List<Vector2> { new Vector2(4, 24), new Vector2(118, 24), new Vector2(118, 132), new Vector2(4, 132), new Vector2(40, 78) };
            var tailR = new List<Vector2>();
            foreach (var q in tailL) tailR.Add(new Vector2(W - q.x, q.y));
            return Make(W, H, (x, y) =>
            {
                var p = new Vector2(x, y);
                float band = RoundRect(p, new Vector2(60, 52), new Vector2(W - 60, H - 6), 26f);
                float tails = Mathf.Min(SignedDistance(p, tailL), SignedDistance(p, tailR)) - 4f;
                float t = Mathf.Clamp01((y - 52f) / (H - 58f));
                float v = Mathf.Lerp(0.8f, 0.97f, t);
                float gloss = RoundRect(p, new Vector2(84, H - 52), new Vector2(W - 84, H - 20), 14f);
                v = Mathf.Lerp(v, 1f, 0.8f * Mathf.Clamp01(-gloss / 5f));
                v = Mathf.Lerp(0.62f, v, Cover(band + 7f));
                v = Mathf.Lerp(0.42f, v, Cover(band + 3f));
                float bandA = Cover(band);
                float tailV = Mathf.Lerp(0.42f, 0.6f, Cover(tails + 5f));
                float a = Mathf.Max(bandA, Cover(tails));
                float col = bandA > 0.001f ? Mathf.Lerp(tailV, v, bandA) : tailV;
                return new Color(col, col, col, a);
            }, new Vector4(150, 0, 150, 0));
        });

        // Rays for the completion card, slowly rotating behind the stars.
        public static Sprite Sunburst => Get("sunburst", () =>
        {
            const int S = 512;
            return Make(S, S, (x, y) =>
            {
                var v = new Vector2(x - S / 2f, y - S / 2f);
                float r = v.magnitude / (S / 2f);
                float ang = Mathf.Atan2(v.y, v.x);
                float ray = Mathf.Clamp01((Mathf.Cos(ang * 12f) - 0.35f) * 3f);
                float fade = Mathf.Clamp01(1f - r) * Mathf.Clamp01(r * 4f);
                return new Color(1f, 1f, 1f, ray * fade * fade);
            });
        });

        public static Sprite Star => Get("star", () => StarSprite(new Color(1f, 0.93f, 0.42f), new Color(1f, 0.62f, 0.14f), new Color(0.86f, 0.43f, 0.04f), true));
        public static Sprite StarEmpty => Get("starEmpty", () => StarSprite(new Color(0.86f, 0.84f, 0.92f), new Color(0.76f, 0.73f, 0.84f), new Color(0.6f, 0.57f, 0.7f), false));

        // ---------- Icons (white, tint with Image.color) ----------

        public static Sprite Icon(string name) => Get("icon_" + name, () => IconSprite(name));

        static Sprite IconSprite(string name)
        {
            const int S = 192;
            var c = new Vector2(S / 2f, S / 2f);
            List<Vector2> poly = null, poly2 = null;
            switch (name)
            {
                case "play":
                    poly = new List<Vector2> { new Vector2(62, 40), new Vector2(150, 96), new Vector2(62, 152) };
                    break;
                case "home":
                    poly = new List<Vector2> { new Vector2(96, 160), new Vector2(166, 98), new Vector2(26, 98) };
                    break;
                case "speaker":
                    poly = new List<Vector2> { new Vector2(30, 76), new Vector2(62, 76), new Vector2(100, 44), new Vector2(100, 148), new Vector2(62, 116), new Vector2(30, 116) };
                    break;
            }
            return Make(S, S, (x, y) =>
            {
                var p = new Vector2(x, y);
                float d;
                switch (name)
                {
                    case "pause":
                        d = Mathf.Min(RoundRect(p, new Vector2(52, 40), new Vector2(84, 152), 12f), RoundRect(p, new Vector2(108, 40), new Vector2(140, 152), 12f));
                        break;
                    case "play":
                        d = SignedDistance(p, poly) - 12f;
                        break;
                    case "restart":
                        d = ArcArrow(p, c, 50f, 13f, 70f, 400f, true);
                        break;
                    case "undo":
                        d = ArcArrow(p + new Vector2(0, 16), c, 50f, 13f, 10f, 180f, false);
                        break;
                    case "home":
                    {
                        float roof = SignedDistance(p, poly) - 8f;
                        float body = RoundRect(p, new Vector2(48, 30), new Vector2(144, 110), 10f);
                        float door = RoundRect(p, new Vector2(80, 22), new Vector2(112, 76), 8f);
                        d = Mathf.Max(Mathf.Min(roof, body), -door);
                        break;
                    }
                    case "grid":
                        d = Mathf.Min(Mathf.Min(RoundRect(p, new Vector2(34, 104), new Vector2(88, 158), 14f), RoundRect(p, new Vector2(104, 104), new Vector2(158, 158), 14f)),
                            Mathf.Min(RoundRect(p, new Vector2(34, 34), new Vector2(88, 88), 14f), RoundRect(p, new Vector2(104, 34), new Vector2(158, 88), 14f)));
                        break;
                    case "gear":
                    {
                        Vector2 v = p - c;
                        float ang = Mathf.Atan2(v.y, v.x);
                        float teeth = Mathf.Clamp(Mathf.Cos(ang * 8f) * 2.5f, -1f, 1f);
                        float outer = v.magnitude - (58f + 12f * teeth);
                        d = Mathf.Max(outer, -(v.magnitude - 22f));
                        break;
                    }
                    case "speaker":
                    {
                        d = SignedDistance(p, poly) - 6f;
                        Vector2 v = p - new Vector2(100, 96);
                        float ang = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
                        if (Mathf.Abs(ang) < 50f)
                        {
                            d = Mathf.Min(d, Mathf.Abs(v.magnitude - 30f) - 8f);
                            d = Mathf.Min(d, Mathf.Abs(v.magnitude - 58f) - 8f);
                        }
                        break;
                    }
                    case "music":
                    {
                        float head1 = Ellipse(p, new Vector2(62, 52), new Vector2(26, 20));
                        float head2 = Ellipse(p, new Vector2(138, 66), new Vector2(26, 20));
                        float stem1 = RoundRect(p, new Vector2(78, 52), new Vector2(92, 150), 4f);
                        float stem2 = RoundRect(p, new Vector2(154, 66), new Vector2(168, 164), 4f);
                        float beam = Segment(p, new Vector2(85, 146), new Vector2(161, 160)) - 13f;
                        d = Mathf.Min(Mathf.Min(head1, head2), Mathf.Min(Mathf.Min(stem1, stem2), beam));
                        break;
                    }
                    case "motion":
                    {
                        float ball = (p - new Vector2(122, 96)).magnitude - 40f;
                        float l1 = Segment(p, new Vector2(30, 120), new Vector2(66, 120)) - 9f;
                        float l2 = Segment(p, new Vector2(18, 96), new Vector2(62, 96)) - 9f;
                        float l3 = Segment(p, new Vector2(30, 72), new Vector2(66, 72)) - 9f;
                        d = Mathf.Min(ball, Mathf.Min(l1, Mathf.Min(l2, l3)));
                        break;
                    }
                    case "lock":
                    {
                        float body = RoundRect(p, new Vector2(46, 30), new Vector2(146, 112), 16f);
                        Vector2 v = p - new Vector2(96, 112);
                        float shackle = v.y < 0f ? 99f : Mathf.Abs(v.magnitude - 30f) - 11f;
                        float side = Mathf.Min(RoundRect(p, new Vector2(55, 100), new Vector2(77, 118), 4f), RoundRect(p, new Vector2(115, 100), new Vector2(137, 118), 4f));
                        float hole = (p - new Vector2(96, 76)).magnitude - 11f;
                        d = Mathf.Max(Mathf.Min(body, Mathf.Min(shackle, side)), -hole);
                        break;
                    }
                    case "check":
                        d = Mathf.Min(Segment(p, new Vector2(44, 100), new Vector2(82, 62)), Segment(p, new Vector2(82, 62), new Vector2(150, 136))) - 17f;
                        break;
                    case "chest":
                    {
                        // A treasure chest: a box, a rounded lid with a seam, and a lock plate.
                        float box = RoundRect(p, new Vector2(30, 36), new Vector2(162, 104), 12f);
                        float lid = Mathf.Max(Ellipse(p, new Vector2(96, 112), new Vector2(66, 50)), -(p.y - 112f));
                        lid = Mathf.Min(lid, RoundRect(p, new Vector2(30, 104), new Vector2(162, 116), 4f));
                        float seam = Mathf.Abs(p.y - 108f) - 3f;
                        float plate = RoundRect(p, new Vector2(82, 76), new Vector2(110, 120), 6f);
                        d = Mathf.Max(Mathf.Min(box, lid), -Mathf.Max(seam, -plate * 1f));
                        d = Mathf.Min(d, plate);
                        break;
                    }
                    case "wetfloor":
                    {
                        // A caution triangle with an exclamation mark.
                        var tri = new List<Vector2> { new Vector2(96, 172), new Vector2(18, 30), new Vector2(174, 30) };
                        float outer = SignedDistance(p, tri) - 10f;
                        float inner = SignedDistance(p, tri) + 14f;
                        float bang = Mathf.Min(RoundRect(p, new Vector2(86, 82), new Vector2(106, 140), 9f), (p - new Vector2(96, 58)).magnitude - 11f);
                        d = Mathf.Min(Mathf.Max(outer, -inner), bang);
                        break;
                    }
                    case "bed":
                    {
                        // A little bed: frame, mattress, pillow, and headboard on the left.
                        float frame = RoundRect(p, new Vector2(20, 40), new Vector2(172, 92), 10f);
                        float legs = Mathf.Min(RoundRect(p, new Vector2(24, 20), new Vector2(42, 50), 5f), RoundRect(p, new Vector2(150, 20), new Vector2(168, 50), 5f));
                        float head = RoundRect(p, new Vector2(20, 40), new Vector2(46, 150), 10f);
                        float pillow = RoundRect(p, new Vector2(54, 96), new Vector2(98, 124), 12f);
                        float blanket = RoundRect(p, new Vector2(96, 92), new Vector2(172, 120), 10f);
                        d = Mathf.Min(Mathf.Min(frame, legs), Mathf.Min(head, Mathf.Min(pillow, blanket)));
                        break;
                    }
                    case "gift":
                    {
                        // A gift box: box and lid, two thin slits marking the ribbon, and a bow on top.
                        float body = Mathf.Min(RoundRect(p, new Vector2(34, 24), new Vector2(158, 108), 10f), RoundRect(p, new Vector2(24, 112), new Vector2(168, 138), 10f));
                        float slit = Mathf.Abs(Mathf.Abs(p.x - 96f) - 13f) - 3f;
                        float lidGap = Mathf.Abs(p.y - 110f) - 2f;
                        body = Mathf.Max(body, -Mathf.Min(slit, lidGap));
                        float bow = Mathf.Min(Ellipse(p, new Vector2(72, 156), new Vector2(24, 15)), Ellipse(p, new Vector2(120, 156), new Vector2(24, 15)));
                        d = Mathf.Min(body, bow);
                        break;
                    }
                    case "mail":
                    {
                        // An envelope with its flap.
                        float env = RoundRect(p, new Vector2(24, 40), new Vector2(168, 150), 14f);
                        float flap = Mathf.Min(Segment(p, new Vector2(34, 140), new Vector2(96, 92)), Segment(p, new Vector2(96, 92), new Vector2(158, 140))) - 7f;
                        d = Mathf.Max(env, -flap);
                        break;
                    }
                    case "shirt":
                    {
                        // A little t-shirt, for Dress Up.
                        float body = RoundRect(p, new Vector2(56, 30), new Vector2(136, 136), 10f);
                        float sleeves = Mathf.Min(Segment(p, new Vector2(60, 132), new Vector2(26, 104)), Segment(p, new Vector2(132, 132), new Vector2(166, 104))) - 18f;
                        float neck = (p - new Vector2(96, 146)).magnitude - 20f;
                        d = Mathf.Max(Mathf.Min(body, sleeves), -neck);
                        break;
                    }
                    case "close":
                        d = Mathf.Min(Segment(p, new Vector2(52, 52), new Vector2(140, 140)), Segment(p, new Vector2(52, 140), new Vector2(140, 52))) - 16f;
                        break;
                    case "chevron":
                    {
                        // A thick rounded chevron pointing up.
                        d = Mathf.Min(Segment(p, new Vector2(30, 70), new Vector2(96, 136)), Segment(p, new Vector2(96, 136), new Vector2(162, 70))) - 22f;
                        break;
                    }
                    case "swirl":
                    {
                        // Three curved arms spiraling into the middle.
                        Vector2 v = p - c;
                        float r = v.magnitude, a = Mathf.Atan2(v.y, v.x);
                        float arms = Mathf.Repeat((a + r * 0.045f) * 3f / (Mathf.PI * 2f), 1f);
                        float width = Mathf.Lerp(0.12f, 0.3f, Mathf.Clamp01(r / 90f));
                        d = (Mathf.Abs(arms - 0.5f) - width) * r * 1.2f;
                        d = Mathf.Max(d, r - 90f);
                        if (r < 16f) d = r - 16f;
                        break;
                    }
                    case "cracks":
                    {
                        // A few branching crack lines.
                        float l1 = Mathf.Min(Segment(p, new Vector2(20, 120), new Vector2(70, 100)), Segment(p, new Vector2(70, 100), new Vector2(96, 60)));
                        float l2 = Mathf.Min(Segment(p, new Vector2(96, 60), new Vector2(150, 44)), Segment(p, new Vector2(70, 100), new Vector2(122, 128)));
                        float l3 = Mathf.Min(Segment(p, new Vector2(122, 128), new Vector2(172, 150)), Segment(p, new Vector2(96, 60), new Vector2(84, 20)));
                        d = Mathf.Min(l1, Mathf.Min(l2, l3)) - 7f;
                        break;
                    }
                    case "slash":
                        d = Segment(p, new Vector2(40, 40), new Vector2(152, 152)) - 11f;
                        break;
                    case "skin":
                    {
                        float outer = (p - c).magnitude - 64f;
                        float ring = Mathf.Abs((p - c).magnitude - 36f) - 7f;
                        d = Mathf.Max(outer, -ring);
                        break;
                    }
                    default:
                        d = (p - c).magnitude - 70f;
                        break;
                }
                return new Color(1f, 1f, 1f, Cover(d));
            });
        }

        // A ring arc from a0 to a1 degrees with an arrowhead at the end (clockwise when cw is true).
        static float ArcArrow(Vector2 p, Vector2 c, float r, float halfWidth, float a0, float a1, bool cw)
        {
            Vector2 v = p - c;
            float ang = Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg;
            float span = a1 - a0;
            float rel = Mathf.Repeat(ang - a0, 360f);
            float ring = rel <= span ? Mathf.Abs(v.magnitude - r) - halfWidth
                : Mathf.Min((p - (c + Dir(a0) * r)).magnitude, (p - (c + Dir(a1) * r)).magnitude) - halfWidth;
            float endA = cw ? a0 : a1;
            Vector2 radial = Dir(endA), tangent = cw ? new Vector2(radial.y, -radial.x) : new Vector2(-radial.y, radial.x);
            Vector2 b = c + radial * r;
            var head = new List<Vector2> { b + tangent * 34f, b - radial * 30f, b + radial * 30f };
            float arrow = SignedDistance(p, head) - 4f;
            // Shorten the ring under the head so the arrow tip reads clearly.
            return Mathf.Min(ring, arrow);
        }

        static Vector2 Dir(float deg) => new Vector2(Mathf.Cos(deg * Mathf.Deg2Rad), Mathf.Sin(deg * Mathf.Deg2Rad));

        // The level pickup: a takeaway coffee with a coral sleeve, a foam top, and a curl of steam.
        static readonly List<Vector2> cupBody = new List<Vector2> { new Vector2(72f, 40f), new Vector2(164f, 40f), new Vector2(182f, 172f), new Vector2(54f, 172f) };

        public static Sprite Cup => Get("cup", () =>
        {
            const int S = 256;
            Color ink = new Color(0.17f, 0.11f, 0.26f), paper = new Color(1f, 0.97f, 0.92f), paperShade = new Color(0.9f, 0.84f, 0.78f);
            Color sleeve = new Color(1f, 0.45f, 0.4f), sleeveDark = new Color(0.86f, 0.3f, 0.3f), coffee = new Color(0.46f, 0.27f, 0.16f);
            return Make(S, S, (x, y) =>
            {
                var p = new Vector2(x, y);
                // Body: a tapered rounded cup, wider at the top.
                float body = SignedDistance(p, cupBody) - 10f;
                float lid = RoundRect(p, new Vector2(34f, 176f), new Vector2(202f, 202f), 12f);
                float handle = Mathf.Abs((p - new Vector2(196f, 110f)).magnitude - 30f) - 11f;
                if (x < 186f) handle = 99f;
                float steam = 99f;
                for (int i = 0; i < 18; i++)
                {
                    float t0 = i / 18f, t1 = (i + 1) / 18f;
                    Vector2 a = new Vector2(118f + 16f * Mathf.Sin(t0 * 6.5f), 212f + 36f * t0), b = new Vector2(118f + 16f * Mathf.Sin(t1 * 6.5f), 212f + 36f * t1);
                    steam = Mathf.Min(steam, Segment(p, a, b) - Mathf.Lerp(7f, 3f, t0));
                }
                float shape = Mathf.Min(Mathf.Min(body, lid), handle);
                Color c = Color.Lerp(paperShade, paper, Mathf.Clamp01((p.x - 60f) / 90f));
                if (body < 0f && y > 70f && y < 132f) c = Color.Lerp(sleeveDark, sleeve, Mathf.Clamp01((p.x - 60f) / 90f));
                if (lid < 0f) c = Color.Lerp(coffee, paper, Mathf.Clamp01((y - 186f) / 14f));
                if (handle < 0f && body >= 0f) c = paperShade;
                c = Color.Lerp(c, Color.white, Cover(Ellipse(p, new Vector2(84f, 140f), new Vector2(9f, 30f))) * 0.7f);
                c = Color.Lerp(ink, c, Cover(shape + 8f));
                c.a = Cover(shape);
                if (steam < 0.5f && c.a < 0.5f) return new Color(1f, 1f, 1f, Cover(steam) * 0.85f);
                return c;
            });
        });

        // A soft four-point twinkle, for glints (not a star: stars are the level rating).
        public static Sprite Twinkle => Get("twinkle", () =>
        {
            const int S = 128;
            return Make(S, S, (x, y) =>
            {
                float dx = Mathf.Abs(x - 64f) / 64f, dy = Mathf.Abs(y - 64f) / 64f;
                float d = Mathf.Pow(dx, 0.5f) + Mathf.Pow(dy, 0.5f);
                float a = Mathf.Clamp01((1f - d) * 3f);
                return new Color(1f, 1f, 1f, a * a);
            });
        });

        static Sprite StarSprite(Color top, Color bottom, Color edge, bool shine)
        {
            const int S = 256;
            var poly = Geometry.Star(new Vector2(S / 2f, S / 2f - 6f), S * 0.46f, S * 0.235f, 5);
            return Make(S, S, (x, y) =>
            {
                var p = new Vector2(x, y);
                float d = SignedDistance(p, poly) - 14f;  // rounded points
                Color col = Color.Lerp(bottom, top, Mathf.Clamp01(y / (float)S * 1.2f - 0.1f));
                // Upper-left facets a little lighter, for a gem-cut look.
                Vector2 rel = p - new Vector2(S / 2f, S / 2f - 6f);
                if (rel.x < 0f && rel.y > -rel.x * 0.3f) col = Color.Lerp(col, Color.white, 0.12f);
                if (shine)
                {
                    float glint = Ellipse(p, new Vector2(S * 0.4f, S * 0.66f), new Vector2(16f, 26f));
                    col = Color.Lerp(col, Color.white, 0.8f * Cover(glint + 2f));
                }
                col = Color.Lerp(edge, col, Cover(d + 11f));
                col.a = Cover(d);
                return col;
            });
        }

        // ---------- Helpers ----------

        static Sprite Get(string key, System.Func<Sprite> make)
        {
            if (cache.TryGetValue(key, out var s) && s != null) return s;
            return cache[key] = make();
        }

        static Sprite Make(int w, int h, System.Func<float, float, Color> shade, Vector4 border = default)
        {
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = shade(x + 0.5f, y + 0.5f);
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 0,
            };
            tex.SetPixels(px);
            tex.Apply(true, true);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        // GLSL-style smoothstep (Mathf.SmoothStep interpolates values instead).
        static float Smooth(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        // Pixel coverage of a signed distance (negative inside), with a one-pixel soft edge.
        static float Cover(float d) => Mathf.Clamp01(0.5f - d);

        static float RoundRect(Vector2 p, Vector2 min, Vector2 max, float r)
        {
            Vector2 c = (min + max) * 0.5f, h = (max - min) * 0.5f - Vector2.one * r;
            Vector2 q = new Vector2(Mathf.Abs(p.x - c.x) - h.x, Mathf.Abs(p.y - c.y) - h.y);
            return Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - r;
        }

        static float Ellipse(Vector2 p, Vector2 c, Vector2 r)
        {
            Vector2 q = new Vector2((p.x - c.x) / r.x, (p.y - c.y) / r.y);
            return (q.magnitude - 1f) * Mathf.Min(r.x, r.y);
        }

        static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 e = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, e) / e.sqrMagnitude);
            return (p - a - e * t).magnitude;
        }

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
