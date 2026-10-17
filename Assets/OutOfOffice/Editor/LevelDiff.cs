using System.Collections.Generic;
using UnityEngine;

namespace OutOfOffice.EditorTools
{
    // Compares two rooms field by field, exactly (no tolerance), including the built outline and rail paths
    // (OUT-12). Empty means the two build the same board. Used to prove the level files load into exactly the boards
    // the C# code made, and that a save and a load lose nothing.
    public static class LevelDiff
    {
        public static List<string> Compare(LevelDef a, LevelDef b, bool includeChapter = false)
        {
            var d = new List<string>();
            void Eq<T>(string what, T x, T y) { if (!EqualityComparer<T>.Default.Equals(x, y)) d.Add($"{what}: {x} vs {y}"); }
            void V(string what, Vector2 x, Vector2 y) { if (x.x != y.x || x.y != y.y) d.Add($"{what}: ({x.x:R}, {x.y:R}) vs ({y.x:R}, {y.y:R})"); }
            void F(string what, float x, float y) { if (x != y) d.Add($"{what}: {x:R} vs {y:R}"); }
            void Points(string what, IList<Vector2> x, IList<Vector2> y)
            {
                if (x.Count != y.Count) { d.Add($"{what}: {x.Count} points vs {y.Count}"); return; }
                for (int i = 0; i < x.Count; i++)
                    if (x[i].x != y[i].x || x[i].y != y[i].y) { V($"{what}[{i}]", x[i], y[i]); return; }
            }
            void Floats(string what, float[] x, float[] y)
            {
                if ((x == null) != (y == null)) { d.Add($"{what}: one is missing"); return; }
                if (x == null) return;
                if (x.Length != y.Length) { d.Add($"{what}: {x.Length} values vs {y.Length}"); return; }
                for (int i = 0; i < x.Length; i++) F($"{what}[{i}]", x[i], y[i]);
            }

            Eq("id", a.Id, b.Id);
            Eq("name", a.Name, b.Name);
            Eq("hint", a.Hint ?? "", b.Hint ?? "");
            Eq("par", a.Par, b.Par);
            Eq("max shots", a.MaxShots, b.MaxShots);
            Eq("max shots override", a.MaxShotsOverride, b.MaxShotsOverride);
            Eq("difficulty", a.Difficulty, b.Difficulty);
            Eq("boss", a.IsBoss, b.IsBoss);
            Eq("teaches", a.Teaches ?? "", b.Teaches ?? "");
            Eq("tags", string.Join(",", a.Tags), string.Join(",", b.Tags));
            if (includeChapter) Eq("chapter", a.Chapter, b.Chapter);
            V("hole", a.Hole, b.Hole);
            V("start", a.Start, b.Start);

            Eq("shape kind", a.Shape.Kind, b.Shape.Kind);
            Floats("shape args", a.Shape.Args, b.Shape.Args);
            Eq("shape flipped", a.Shape.Flipped, b.Shape.Flipped);
            V("shape offset", a.Shape.Offset, b.Shape.Offset);
            Points("outline", a.Shape.Outline, b.Shape.Outline);

            Eq("blocks", a.Blocks.Count, b.Blocks.Count);
            for (int i = 0; i < Mathf.Min(a.Blocks.Count, b.Blocks.Count); i++)
            {
                BlockDef x = a.Blocks[i], y = b.Blocks[i];
                string w = $"block {i}";
                Eq(w + " kind", x.Kind, y.Kind);
                Eq(w + " shape", x.Shape, y.Shape);
                V(w + " at", x.Center, y.Center);
                V(w + " size", x.Size, y.Size);
                F(w + " corner", x.Corner, y.Corner);
                F(w + " angle", x.Angle, y.Angle);
                V(w + " move", x.MoveOffset, y.MoveOffset);
                F(w + " period", x.Period, y.Period);
                F(w + " phase", x.Phase, y.Phase);
                F(w + " spin", x.Spin, y.Spin);
                Eq(w + " group", x.Group, y.Group);
                F(w + " blink", x.BlinkPeriod, y.BlinkPeriod);
                F(w + " blink phase", x.BlinkPhase, y.BlinkPhase);
                Eq(w + " reception", x.Reception, y.Reception);
            }

            Eq("zones", a.Zones.Count, b.Zones.Count);
            for (int i = 0; i < Mathf.Min(a.Zones.Count, b.Zones.Count); i++)
            {
                ZoneDef x = a.Zones[i], y = b.Zones[i];
                string w = $"zone {i}";
                Eq(w + " kind", x.Kind, y.Kind);
                V(w + " at", x.Center, y.Center);
                V(w + " size", x.Size, y.Size);
                F(w + " angle", x.Angle, y.Angle);
                Eq(w + " link", x.Link, y.Link);
                Eq(w + " group", x.Group, y.Group);
                F(w + " turn", x.Turn, y.Turn);
                F(w + " period", x.Period, y.Period);
                F(w + " phase", x.Phase, y.Phase);
            }

            Eq("rails", a.Rails.Count, b.Rails.Count);
            for (int i = 0; i < Mathf.Min(a.Rails.Count, b.Rails.Count); i++)
            {
                RailDef x = a.Rails[i], y = b.Rails[i];
                string w = $"rail {i}";
                Eq(w + " kind", x.Kind, y.Kind);
                F(w + " width", x.Width, y.Width);
                Points(w + " path", x.Path, y.Path);
            }

            Points("stars", a.Stars, b.Stars);
            Eq("bridges", a.Bridges.Count, b.Bridges.Count);
            for (int i = 0; i < Mathf.Min(a.Bridges.Count, b.Bridges.Count); i++)
            {
                Rect x = a.Bridges[i], y = b.Bridges[i];
                if (x.x != y.x || x.y != y.y || x.width != y.width || x.height != y.height) d.Add($"bridge {i}: {x} vs {y}");
            }
            return d;
        }
    }
}
