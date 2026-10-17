using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace OutOfOffice.EditorTools
{
    // Fast static checks on a room (OUT-12): the level editor shows them live while you build, and Run All Checks fails
    // on any error in a shipped room. Warnings point at things that are usually a mistake but can be on purpose.
    // LevelCheck is the slow, physical proof; this is the instant one.
    public static class LevelLint
    {
        public enum Part { Level, Block, Zone, Rail, Star, Hole, Start }

        public struct Problem
        {
            public bool Error;
            public string Text;
            public Part Part;
            public int Index;  // which block, zone, rail or star; -1 for the room itself
            public override string ToString() => (Error ? "ERROR " : "warning ") + Text;
        }

        public const int MaxNameLength = 26;  // the level banner's width (PhysicsTests shot-limits)
        static readonly Regex IdPattern = new Regex("^[a-z0-9][a-z0-9_-]*$");

        public static List<Problem> Check(LevelDef d)
        {
            var list = new List<Problem>();
            void Add(bool error, string text, Part part = Part.Level, int index = -1) =>
                list.Add(new Problem { Error = error, Text = text, Part = part, Index = index });

            if (string.IsNullOrEmpty(d.Id) || !IdPattern.IsMatch(d.Id)) Add(true, $"id '{d.Id}' must be lower-case letters, digits, - or _");
            if (string.IsNullOrWhiteSpace(d.Name)) Add(true, "the room has no name");
            else if (d.Name.Length > MaxNameLength) Add(true, $"name is {d.Name.Length} characters; the banner fits {MaxNameLength}");
            if (d.Par < 1) Add(true, "par must be at least 1");
            if (d.MaxShotsOverride > 0 && d.MaxShotsOverride < d.Par + 1) Add(true, $"shot limit {d.MaxShotsOverride} leaves no spare shot over par {d.Par}");
            if (d.Stars.Count != 3) Add(false, $"{d.Stars.Count} coffees; rooms usually have 3");
            if (string.IsNullOrWhiteSpace(d.Hint)) Add(false, "no hint");

            var outline = d.Shape.Outline;
            if (outline.Count < 3) { Add(true, "the board outline has fewer than 3 points"); return list; }
            if (!Inside(outline, d.Hole, Rules.HoleRadius)) Add(true, "the bed is outside the board or against its edge", Part.Hole);
            if (!Inside(outline, d.Start, Rules.PuckRadius)) Add(true, "the start is outside the board or against its edge", Part.Start);
            if ((d.Hole - d.Start).magnitude < 2f) Add(false, "the bed is right next to the start", Part.Hole);

            for (int i = 0; i < d.Stars.Count; i++)
            {
                if (!Inside(outline, d.Stars[i], 0f)) Add(true, $"coffee {i + 1} is off the board", Part.Star, i);
                int b = SolidAt(d, d.Stars[i], 0f);
                if (b >= 0) Add(true, $"coffee {i + 1} is inside a {Name(d.Blocks[b].Kind)}: it can never be collected", Part.Star, i);
            }

            int hb = SolidAt(d, d.Hole, 0f);
            if (hb >= 0) Add(true, $"the bed is under a {Name(d.Blocks[hb].Kind)}", Part.Hole);
            else if ((hb = SolidAt(d, d.Hole, Rules.HoleRadius * 0.5f)) >= 0) Add(false, $"a {Name(d.Blocks[hb].Kind)} crowds the bed", Part.Block, hb);
            int sb = SolidAt(d, d.Start, Rules.PuckRadius);
            if (sb >= 0) Add(true, $"the puck starts inside a {Name(d.Blocks[sb].Kind)}", Part.Start);

            for (int i = 0; i < d.Blocks.Count; i++)
            {
                var b = d.Blocks[i];
                if (b.Size.x <= 0f || b.Size.y <= 0f) Add(true, $"{Name(b.Kind)} {i + 1} has no size", Part.Block, i);
                if (!Inside(outline, b.Center, 0f) && b.Reception == 0) Add(false, $"{Name(b.Kind)} {i + 1} sits off the board", Part.Block, i);
                if (b.Kind == BlockKind.Gate && !d.Zones.Exists(z => z.Kind == ZoneKind.Switch && z.Group == b.Group))
                    Add(true, $"gate {i + 1} (group {b.Group}) has no badge reader to open it", Part.Block, i);
                if (b.Kind == BlockKind.PopWall && b.BlinkPeriod <= 0f) Add(true, $"pop-up wall {i + 1} has no period", Part.Block, i);
                if (b.Moves && b.Kind == BlockKind.Crate) Add(true, $"crate {i + 1} cannot also be a mover", Part.Block, i);
                for (int j = i + 1; j < d.Blocks.Count; j++)
                {
                    var o = d.Blocks[j];
                    if ((b.Moves || o.Moves) && Sweep(b).Overlaps(Sweep(o)))
                        Add(false, $"{Name(b.Kind)} {i + 1} and {Name(o.Kind)} {j + 1} can run into each other", Part.Block, b.Moves ? i : j);
                }
            }

            for (int i = 0; i < d.Zones.Count; i++)
            {
                var z = d.Zones[i];
                if (z.Size.x <= 0f || z.Size.y <= 0f) Add(true, $"{Name(z.Kind)} {i + 1} has no size", Part.Zone, i);
                if (!Inside(outline, z.Center, 0f)) Add(false, $"{Name(z.Kind)} {i + 1} sits off the board", Part.Zone, i);
                if (z.Kind == ZoneKind.Portal)
                {
                    bool linked = z.Link >= 0 && z.Link < d.Zones.Count && z.Link != i && d.Zones[z.Link].Kind == ZoneKind.Portal && d.Zones[z.Link].Link == i;
                    if (!linked) Add(true, $"portal {i + 1} is not paired with another portal that leads back to it", Part.Zone, i);
                }
                if (z.Kind == ZoneKind.Switch && !d.Blocks.Exists(b => b.Kind == BlockKind.Gate && b.Group == z.Group))
                    Add(false, $"badge reader {i + 1} (group {z.Group}) opens no gate", Part.Zone, i);
                if (z.Kind == ZoneKind.Spikes && z.Period <= 0f) Add(true, $"spikes {i + 1} have no period", Part.Zone, i);
            }

            for (int i = 0; i < d.Rails.Count; i++)
                if (d.Rails[i].Path == null || d.Rails[i].Path.Count < 2) Add(true, $"rail {i + 1} needs at least two points", Part.Rail, i);
            return list;
        }

        public static bool HasErrors(List<Problem> problems) => problems.Exists(p => p.Error);

        static string Name(BlockKind k) => k == BlockKind.Hazard ? "cactus" : k.ToString().ToLowerInvariant();
        static string Name(ZoneKind k) => k.ToString().ToLowerInvariant();

        // The first still, solid block that covers p (grown by `pad`), or -1. Crates, pop-up walls, gates and glass
        // can move or vanish, and movers come and go, so only blocks that are always there count.
        public static int SolidAt(LevelDef d, Vector2 p, float pad)
        {
            for (int i = 0; i < d.Blocks.Count; i++)
            {
                var b = d.Blocks[i];
                if (b.Moves || b.Kind == BlockKind.Crate || b.Kind == BlockKind.PopWall || b.Kind == BlockKind.Gate || b.Kind == BlockKind.Breakable) continue;
                if (Covers(b, p, pad)) return i;
            }
            return -1;
        }

        public static bool Covers(BlockDef b, Vector2 p, float pad)
        {
            if (b.Shape == BlockShape.Post) return (p - b.Center).magnitude <= b.Size.x * 0.5f + pad;
            Vector2 local = Quaternion.Euler(0f, 0f, -b.Angle) * (p - b.Center);
            return Mathf.Abs(local.x) <= b.Size.x * 0.5f + pad && Mathf.Abs(local.y) <= b.Size.y * 0.5f + pad;
        }

        // The area a block covers over its whole swing or spin.
        public static Rect Sweep(BlockDef b)
        {
            float r = b.Spin != 0f || b.Shape == BlockShape.Post ? 0.5f * (b.Shape == BlockShape.Post ? b.Size.x : b.Size.magnitude) : 0f;
            Vector2 half = r > 0f ? new Vector2(r, r) : HalfExtents(b);
            Vector2 move = new Vector2(Mathf.Abs(b.MoveOffset.x), Mathf.Abs(b.MoveOffset.y));
            if (b.Period <= 0f) move = Vector2.zero;
            return new Rect(b.Center - half - move, 2f * (half + move));
        }

        static Vector2 HalfExtents(BlockDef b)
        {
            float a = b.Angle * Mathf.Deg2Rad, c = Mathf.Abs(Mathf.Cos(a)), s = Mathf.Abs(Mathf.Sin(a));
            return 0.5f * new Vector2(b.Size.x * c + b.Size.y * s, b.Size.x * s + b.Size.y * c);
        }

        // p is inside the outline and at least `margin` from its edge.
        public static bool Inside(IList<Vector2> poly, Vector2 p, float margin)
        {
            bool inside = false;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
            {
                Vector2 a = poly[i], b = poly[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x) inside = !inside;
            }
            if (!inside || margin <= 0f) return inside;
            for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
                if (DistanceToSegment(p, poly[j], poly[i]) < margin) return false;
            return true;
        }

        public static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return (p - (a + ab * t)).magnitude;
        }
    }
}
