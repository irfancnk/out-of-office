using System.Collections.Generic;
using UnityEngine;

namespace OutOfOffice.EditorTools
{
    // Fast layout rules for a room (OUT-12): no physics, so the level editor runs them on every edit. Errors are things
    // that break a room (a portal with no partner, the bed outside the floor) and fail the checks; warnings are
    // likely mistakes worth a look (a desk on top of the bed, two moving desks sweeping through each other).
    // LevelCheck still has the last word on whether a room can be finished.
    public static class LevelLint
    {
        public struct Problem
        {
            public bool Error;
            public string Text;
            public Vector2 At;
            public override string ToString() => (Error ? "ERROR " : "warning ") + Text;
        }

        const float PuckRadius = Rules.PuckRadius;
        const float BedRadius = Rules.HoleRadius;

        static readonly HashSet<BlockKind> Solid = new HashSet<BlockKind>
            { BlockKind.Wall, BlockKind.Bouncer, BlockKind.Sticky, BlockKind.Kicker, BlockKind.Breakable, BlockKind.Crate };

        public static List<Problem> Check(LevelDef def)
        {
            var list = new List<Problem>();
            void Error(string text, Vector2 at) => list.Add(new Problem { Error = true, Text = text, At = at });
            void Warn(string text, Vector2 at) => list.Add(new Problem { Error = false, Text = text, At = at });
            var outline = def.Shape.Outline;

            if (!LevelFiles.ValidId(def.Id)) Error($"id \"{def.Id}\" must be up to 24 lower-case letters, digits or dashes", def.Hole);
            if (string.IsNullOrEmpty(def.Name)) Error("the room needs a name", def.Hole);
            if (def.Name != null && def.Name.Length > 26) Error($"the name is {def.Name.Length} letters; the banner fits 26", def.Hole);
            if (def.Par < 1) Error("par must be at least 1", def.Hole);
            if (def.MaxShots < def.Par + 1) Error($"the shot limit {def.MaxShots} leaves no spare shot over par {def.Par}", def.Hole);
            if (def.Difficulty < 0 || def.Difficulty > 5) Error("difficulty is 0 (not rated) or 1 to 5", def.Hole);
            if (!string.IsNullOrEmpty(def.Teaches) && !System.Array.Exists(Gadgets.All, g => g.Key == def.Teaches))
                Error($"teaches \"{def.Teaches}\", which is not a gadget key", def.Hole);
            foreach (var tag in def.Tags) if (string.IsNullOrWhiteSpace(tag)) Error("an empty tag", def.Hole);

            if (Geometry.SignedArea(outline) <= 0f || outline.Count < 3) Error("the floor outline is empty", Vector2.zero);
            if (!Inside(outline, def.Hole, BedRadius)) Error("the bed is not fully on the floor", def.Hole);
            if (!Inside(outline, def.Start, PuckRadius)) Error("the start is not on the floor", def.Start);
            if ((def.Start - def.Hole).magnitude < BedRadius + PuckRadius) Error("the start is on the bed", def.Start);
            for (int i = 0; i < def.Stars.Count; i++)
                if (!Inside(outline, def.Stars[i], 0.2f)) Error($"coffee {i + 1} is off the floor", def.Stars[i]);

            for (int i = 0; i < def.Blocks.Count; i++)
            {
                var b = def.Blocks[i];
                string what = $"{b.Kind} {i + 1}";
                if (b.Size.x <= 0f || b.Size.y <= 0f) Error($"{what} has no size", b.Center);
                if (!Inside(outline, b.Center, 0f)) Error($"{what} is off the floor", b.Center);
                if (b.Period < 0f) Error($"{what} has a negative period", b.Center);
                if (b.Period == 0f && (b.MoveOffset.x != 0f || b.MoveOffset.y != 0f)) Error($"{what} has a swing but no period", b.Center);
                if (b.Kind == BlockKind.PopWall && b.BlinkPeriod <= 0f) Error($"{what} is a pop-up wall with no period", b.Center);
                if (b.Kind == BlockKind.Gate && !def.Zones.Exists(z => z.Kind == ZoneKind.Switch && z.Group == b.Group))
                    Error($"{what} is a gate no switch opens (group {b.Group})", b.Center);
                if (b.Reception == 0 && Solid.Contains(b.Kind) && Covers(b, def.Hole, BedRadius * 0.5f))
                    Warn($"{what} sits on the bed", def.Hole);
                if (Solid.Contains(b.Kind) && !b.Moves && Covers(b, def.Start, PuckRadius)) Error($"{what} sits on the start", def.Start);
                for (int s = 0; s < def.Stars.Count; s++)
                    if (Solid.Contains(b.Kind) && !b.Moves && b.Kind != BlockKind.Crate && Covers(b, def.Stars[s], 0f))
                        Warn($"coffee {s + 1} is inside {what}", def.Stars[s]);
            }

            // Two moving blocks whose swept areas cross can pass through each other, which looks broken.
            for (int i = 0; i < def.Blocks.Count; i++)
            for (int j = i + 1; j < def.Blocks.Count; j++)
            {
                BlockDef a = def.Blocks[i], b = def.Blocks[j];
                if (!a.Moves || !b.Moves) continue;
                if (Swept(a).Overlaps(Swept(b))) Warn($"moving {a.Kind} {i + 1} and {b.Kind} {j + 1} sweep through each other", (a.Center + b.Center) * 0.5f);
            }

            var switches = new HashSet<int>();
            for (int i = 0; i < def.Zones.Count; i++)
            {
                var z = def.Zones[i];
                string what = $"{z.Kind} zone {i + 1}";
                if (z.Size.x <= 0f || z.Size.y <= 0f) Error($"{what} has no size", z.Center);
                if (!Inside(outline, z.Center, 0f)) Error($"{what} is off the floor", z.Center);
                if (z.Kind == ZoneKind.Portal)
                {
                    if (z.Link < 0 || z.Link >= def.Zones.Count || def.Zones[z.Link].Kind != ZoneKind.Portal || def.Zones[z.Link].Link != i)
                        Error($"{what} has no partner portal linked back to it", z.Center);
                }
                if (z.Kind == ZoneKind.Spikes && z.Period <= 0f) Error($"{what} has no period", z.Center);
                if (z.Kind == ZoneKind.Switch)
                {
                    switches.Add(z.Group);
                    if (!def.Blocks.Exists(b => b.Kind == BlockKind.Gate && b.Group == z.Group)) Warn($"{what} opens no gate (group {z.Group})", z.Center);
                }
                if ((z.Kind == ZoneKind.Gap || z.Kind == ZoneKind.Spikes) && (z.Center - def.Hole).magnitude < BedRadius)
                    Error($"{what} is on the bed", def.Hole);
            }

            for (int i = 0; i < def.Rails.Count; i++)
            {
                var r = def.Rails[i];
                if (r.Path == null || r.Path.Count < 2) Error($"rail {i + 1} has fewer than 2 points", Vector2.zero);
                else if (r.Width <= 0f) Error($"rail {i + 1} has no width", r.Path[0]);
            }

            if (def.Stars.Count != 3) Warn($"{def.Stars.Count} coffees; rooms usually have 3", def.Hole);
            return list;
        }

        public static int Errors(List<Problem> problems)
        {
            int n = 0;
            foreach (var p in problems) if (p.Error) n++;
            return n;
        }

        // Is p on the floor, with `margin` to spare from the edge?
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

        // Does the block, where it starts, come within `pad` of p?
        public static bool Covers(BlockDef b, Vector2 p, float pad)
        {
            if (b.Shape == BlockShape.Post) return (p - b.Center).magnitude < b.Size.x * 0.5f + pad;
            Vector2 local = Rotate(p - b.Center, -b.Angle);
            Vector2 half = b.Size * 0.5f + new Vector2(pad, pad);
            return Mathf.Abs(local.x) < half.x && Mathf.Abs(local.y) < half.y;
        }

        // The area a moving block can reach, as a box (spinning blocks sweep their whole circle).
        public static Rect Swept(BlockDef b)
        {
            float reach = b.Spin != 0f || b.Angle != 0f ? b.Size.magnitude * 0.5f : 0f;
            Vector2 half = reach > 0f ? new Vector2(reach, reach) : b.Size * 0.5f;
            Vector2 swing = new Vector2(Mathf.Abs(b.MoveOffset.x), Mathf.Abs(b.MoveOffset.y));
            return new Rect(b.Center - half - swing, (half + swing) * 2f);
        }

        public static Vector2 Rotate(Vector2 v, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad, c = Mathf.Cos(r), s = Mathf.Sin(r);
            return new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
        }

        static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude) : 0f;
            return (p - (a + ab * t)).magnitude;
        }
    }
}
