using System.Collections.Generic;
using UnityEngine;
using Fields = System.Collections.Generic.Dictionary<string, object>;

namespace OutOfOffice
{
    // The level file format (OUT-12): one JSON file per room in Resources/Levels, named after its id, and
    // chapters.json listing the chapters in order with their rooms. ToJson and FromJson are exact inverses: a level
    // read from its file and written back gives the same text, and builds the same board, to the bit.
    //
    //   {
    //     "id": "bank", "name": "Bounce Off the Walls", "hint": "BOUNCE OFF THE WALL", "par": 2,
    //     "hole": [2.8, 5.8], "start": [-2.6, -6.2],
    //     "blocks": [{ "kind": "Wall", "at": [2.1, -1.6], "size": [4.8, 0.6] }, ...],
    //     "stars": [[-2.6, -3.4], [0.9, 0.4], [3.5, 3.6]]
    //   }
    //
    // Anything left at its default is left out. Coordinates are in board units: the default floor is 9 x 16,
    // x in [-4.5, 4.5] and y in [-8, 8]. Angles are degrees, counter-clockwise; zone angle 0 points up.
    public static class LevelData
    {
        public const int Version = 1;

        // ---------- Writing ----------

        public static string ToJson(LevelDef def) => JsonLite.Write(ToObj(def));

        public static JsonLite.Obj ToObj(LevelDef def)
        {
            var o = new JsonLite.Obj();
            o.Add("id", def.Id).Add("name", def.Name);
            if (!string.IsNullOrEmpty(def.Hint)) o.Add("hint", def.Hint);
            o.Add("par", def.Par);
            if (def.MaxShotsOverride > 0) o.Add("maxShots", def.MaxShotsOverride);
            if (def.Difficulty != 0) o.Add("difficulty", def.Difficulty);
            if (def.IsBoss) o.Add("boss", true);
            if (!string.IsNullOrEmpty(def.Teaches)) o.Add("teaches", def.Teaches);
            if (def.Tags.Count > 0) o.Add("tags", new List<object>(def.Tags));
            o.Add("hole", V(def.Hole)).Add("start", V(def.Start));
            if (!IsDefaultShape(def.Shape)) o.Add("shape", ShapeObj(def.Shape));
            if (def.Blocks.Count > 0) o.Add("blocks", Map(def.Blocks, BlockObj));
            if (def.Zones.Count > 0) o.Add("zones", Map(def.Zones, ZoneObj));
            if (def.Rails.Count > 0) o.Add("rails", Map(def.Rails, RailObj));
            if (def.Stars.Count > 0) o.Add("stars", Map(def.Stars, V));
            if (def.Bridges.Count > 0) o.Add("bridges", Map(def.Bridges, r => (object)new List<object> { r.x, r.y, r.width, r.height }));
            return o;
        }

        static List<object> Map<T>(List<T> items, System.Func<T, object> f)
        {
            var list = new List<object>(items.Count);
            foreach (var it in items) list.Add(f(it));
            return list;
        }

        static object V(Vector2 v) => new List<object> { v.x, v.y };

        // Exact, unlike Vector2 ==, which treats anything within 1e-5 as equal.
        static bool IsZero(Vector2 v) => v.x == 0f && v.y == 0f;

        static readonly ShapeDef DefaultShape = ShapeDef.Rect(9f, 16f);

        static bool IsDefaultShape(ShapeDef s) =>
            s.Kind == DefaultShape.Kind && !s.Flipped && IsZero(s.Offset) && SameArgs(s.Args, DefaultShape.Args);

        static bool SameArgs(float[] a, float[] b)
        {
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        public static JsonLite.Obj ShapeObj(ShapeDef s)
        {
            var o = new JsonLite.Obj().Add("kind", s.Kind.ToString());
            var args = new List<object>();
            if (s.Kind == ShapeKind.Polygon)
            {
                o.Add("corner", s.Args[0]);
                var pts = new List<object>();
                for (int i = 1; i + 1 < s.Args.Length; i += 2) pts.Add(new List<object> { s.Args[i], s.Args[i + 1] });
                o.Add("points", pts);
            }
            else
            {
                foreach (float a in s.Args) args.Add(a);
                o.Add("args", args);
            }
            if (s.Flipped) o.Add("flipped", true);
            if (!IsZero(s.Offset)) o.Add("offset", V(s.Offset));
            return o;
        }

        public static JsonLite.Obj BlockObj(BlockDef b)
        {
            var o = new JsonLite.Obj().Add("kind", b.Kind.ToString());
            if (b.Shape != BlockShape.Box) o.Add("shape", b.Shape.ToString());
            o.Add("at", V(b.Center));
            if (b.Shape == BlockShape.Post) o.Add("radius", b.Size.x * 0.5f);
            if (b.Shape != BlockShape.Post || b.Size.x != b.Size.y) o.Add("size", V(b.Size));
            if (b.Corner != -1f) o.Add("corner", b.Corner);
            if (b.Angle != 0f) o.Add("angle", b.Angle);
            if (b.Period != 0f || !IsZero(b.MoveOffset)) o.Add("move", V(b.MoveOffset)).Add("period", b.Period);
            if (b.Phase != 0f) o.Add("phase", b.Phase);
            if (b.Spin != 0f) o.Add("spin", b.Spin);
            if (b.Group != -1) o.Add("group", b.Group);
            if (b.BlinkPeriod != 0f) o.Add("blink", b.BlinkPeriod);
            if (b.BlinkPhase != 0f) o.Add("blinkPhase", b.BlinkPhase);
            if (b.Reception != 0) o.Add("reception", b.Reception);
            return o;
        }

        public static JsonLite.Obj ZoneObj(ZoneDef z)
        {
            var o = new JsonLite.Obj().Add("kind", z.Kind.ToString()).Add("at", V(z.Center)).Add("size", V(z.Size));
            if (z.Angle != 0f) o.Add("angle", z.Angle);
            if (z.Link != -1) o.Add("link", z.Link);
            if (z.Group != -1) o.Add("group", z.Group);
            if (z.Turn != 1f) o.Add("turn", z.Turn);
            if (z.Period != 0f) o.Add("period", z.Period);
            if (z.Phase != 0f) o.Add("phase", z.Phase);
            return o;
        }

        public static JsonLite.Obj RailObj(RailDef r)
        {
            var o = new JsonLite.Obj().Add("kind", r.Kind.ToString());
            if (r.Width != 0.5f) o.Add("width", r.Width);
            if (r.IsArc) o.Add("arc", new List<object> { r.ArcArgs[0], r.ArcArgs[1], r.ArcArgs[2], r.ArcArgs[3], r.ArcArgs[4] });
            else
            {
                if (r.Bend != 0f) o.Add("bend", r.Bend);
                o.Add("points", Map(r.Points, V));
            }
            if (r.Flipped) o.Add("flipped", true);
            return o;
        }

        // ---------- Reading ----------

        public class FormatException : System.Exception
        {
            public FormatException(string message) : base(message) { }
        }

        public static LevelDef FromJson(string json)
        {
            object root;
            try { root = JsonLite.Parse(json); }
            catch (JsonLite.ParseException e) { throw new FormatException(e.Message); }
            if (!(root is Fields f)) throw new FormatException("a level file must hold one object");
            return FromFields(f);
        }

        public static LevelDef FromFields(Fields f)
        {
            Known(f, "level", "id", "name", "hint", "par", "maxShots", "difficulty", "boss", "teaches", "tags", "hole", "start", "shape",
                "blocks", "zones", "rails", "stars", "bridges");
            string id = Str(f, "id", null);
            if (string.IsNullOrEmpty(id)) throw new FormatException("a level needs an \"id\"");
            var hole = Vec(f, "hole", null, id);
            var def = new LevelDef(id, Str(f, "name", id), hole.x, hole.y)
            {
                Hint = Str(f, "hint", ""),
                Par = Int(f, "par", 1),
                Difficulty = Int(f, "difficulty", 0),
                IsBoss = Bool(f, "boss", false),
                Teaches = Str(f, "teaches", ""),
                Start = Vec(f, "start", new Vector2(0f, -6f), id),
            };
            int maxShots = Int(f, "maxShots", 0);
            if (maxShots > 0) def.WithMaxShots(maxShots);
            foreach (var t in List(f, "tags")) def.Tags.Add(t as string ?? throw new FormatException($"{id}: tags must be strings"));
            if (f.TryGetValue("shape", out var shape)) def.Shape = ParseShape(Obj(shape, id + " shape"), id);
            foreach (var b in List(f, "blocks")) def.Blocks.Add(ParseBlock(Obj(b, id + " block"), id));
            foreach (var z in List(f, "zones")) def.Zones.Add(ParseZone(Obj(z, id + " zone"), id));
            foreach (var r in List(f, "rails")) def.Rails.Add(ParseRail(Obj(r, id + " rail"), id));
            foreach (var s in List(f, "stars")) def.Stars.Add(ToVec(s, id + " star"));
            foreach (var br in List(f, "bridges"))
            {
                var n = Floats(br, id + " bridge");
                if (n.Length != 4) throw new FormatException($"{id}: a bridge is [x, y, width, height]");
                def.Bridges.Add(new Rect(n[0], n[1], n[2], n[3]));
            }
            return def;
        }

        public static ShapeDef ParseShape(Fields f, string id)
        {
            Known(f, id + " shape", "kind", "args", "corner", "points", "flipped", "offset");
            var kind = Enum<ShapeKind>(f, "kind", id);
            float[] args;
            if (kind == ShapeKind.Polygon)
            {
                var pts = List(f, "points");
                if (pts.Count < 3) throw new FormatException($"{id}: a polygon shape needs at least 3 points");
                args = new float[1 + pts.Count * 2];
                args[0] = Float(f, "corner", 0.7f);
                for (int i = 0; i < pts.Count; i++)
                {
                    var p = ToVec(pts[i], id + " shape point");
                    args[1 + i * 2] = p.x;
                    args[2 + i * 2] = p.y;
                }
            }
            else
            {
                args = Floats(f.TryGetValue("args", out var a) ? a : null, id + " shape args");
                int need = kind == ShapeKind.Circle ? 1 : kind == ShapeKind.Chamfered ? 4 : 3;
                if (args.Length != need) throw new FormatException($"{id}: a {kind} shape takes {need} args");
            }
            return ShapeDef.Make(kind, args, Bool(f, "flipped", false), Vec(f, "offset", Vector2.zero, id));
        }

        public static BlockDef ParseBlock(Fields f, string id)
        {
            Known(f, id + " block", "kind", "shape", "at", "radius", "size", "corner", "angle", "move", "period", "phase", "spin", "group",
                "blink", "blinkPhase", "reception");
            var shape = f.ContainsKey("shape") ? Enum<BlockShape>(f, "shape", id) : BlockShape.Box;
            Vector2 size;
            if (shape == BlockShape.Post && !f.ContainsKey("size"))
            {
                float r = Float(f, "radius", 0.5f);
                size = new Vector2(r * 2f, r * 2f);
            }
            else size = Vec(f, "size", null, id);
            return new BlockDef(Enum<BlockKind>(f, "kind", id), Vec(f, "at", null, id), size)
            {
                Shape = shape,
                Corner = Float(f, "corner", -1f),
                Angle = Float(f, "angle", 0f),
                MoveOffset = Vec(f, "move", Vector2.zero, id),
                Period = Float(f, "period", 0f),
                Phase = Float(f, "phase", 0f),
                Spin = Float(f, "spin", 0f),
                Group = Int(f, "group", -1),
                BlinkPeriod = Float(f, "blink", 0f),
                BlinkPhase = Float(f, "blinkPhase", 0f),
                Reception = Int(f, "reception", 0),
            };
        }

        public static ZoneDef ParseZone(Fields f, string id)
        {
            Known(f, id + " zone", "kind", "at", "size", "angle", "link", "group", "turn", "period", "phase");
            return new ZoneDef
            {
                Kind = Enum<ZoneKind>(f, "kind", id),
                Center = Vec(f, "at", null, id),
                Size = Vec(f, "size", null, id),
                Angle = Float(f, "angle", 0f),
                Link = Int(f, "link", -1),
                Group = Int(f, "group", -1),
                Turn = Float(f, "turn", 1f),
                Period = Float(f, "period", 0f),
                Phase = Float(f, "phase", 0f),
            };
        }

        public static RailDef ParseRail(Fields f, string id)
        {
            Known(f, id + " rail", "kind", "width", "arc", "bend", "points", "flipped");
            var kind = Enum<BlockKind>(f, "kind", id);
            float width = Float(f, "width", 0.5f);
            bool flipped = Bool(f, "flipped", false);
            if (f.TryGetValue("arc", out var arc))
            {
                var a = Floats(arc, id + " rail arc");
                if (a.Length != 5) throw new FormatException($"{id}: a rail arc is [center x, center y, radius, from deg, to deg]");
                return RailDef.Curve(kind, a[0], a[1], a[2], a[3], a[4], flipped, width);
            }
            var pts = new List<Vector2>();
            foreach (var p in List(f, "points")) pts.Add(ToVec(p, id + " rail point"));
            if (pts.Count < 2) throw new FormatException($"{id}: a rail needs at least 2 points or an arc");
            return RailDef.Bent(kind, Float(f, "bend", 0f), pts, flipped, width);
        }

        // ---------- Chapters ----------

        public class ChapterEntry
        {
            public string Theme;   // Themes.All[i].Name; chapter i must use theme i (saves key chapter rewards by index)
            public readonly List<string> Levels = new List<string>();
        }

        public class Manifest
        {
            public int Version = LevelData.Version;
            public string Tutorial = "";   // the orientation room, played before chapter 1 (OUT-52)
            public readonly List<ChapterEntry> Chapters = new List<ChapterEntry>();
        }

        public static string ManifestToJson(Manifest m)
        {
            var chapters = new List<object>();
            foreach (var c in m.Chapters)
                chapters.Add(new JsonLite.Obj().Add("theme", c.Theme).Add("levels", new List<object>(c.Levels)));
            var o = new JsonLite.Obj().Add("version", m.Version);
            if (!string.IsNullOrEmpty(m.Tutorial)) o.Add("tutorial", m.Tutorial);
            return JsonLite.Write(o.Add("chapters", chapters));
        }

        public static Manifest ManifestFromJson(string json)
        {
            object root;
            try { root = JsonLite.Parse(json); }
            catch (JsonLite.ParseException e) { throw new FormatException("chapters.json " + e.Message); }
            if (!(root is Fields f)) throw new FormatException("chapters.json must hold one object");
            Known(f, "chapters.json", "version", "tutorial", "chapters");
            var m = new Manifest { Version = Int(f, "version", Version), Tutorial = Str(f, "tutorial", "") };
            if (m.Version > Version) throw new FormatException($"chapters.json is version {m.Version}; this build reads up to {Version}");
            foreach (var c in List(f, "chapters"))
            {
                var cf = Obj(c, "chapter");
                Known(cf, "chapter", "theme", "levels");
                var entry = new ChapterEntry { Theme = Str(cf, "theme", "") };
                foreach (var id in List(cf, "levels")) entry.Levels.Add(id as string ?? throw new FormatException("chapter levels must be ids"));
                m.Chapters.Add(entry);
            }
            return m;
        }

        // ---------- Field helpers ----------

        // An unknown field is almost always a typo ("perod"), which would otherwise be silently ignored.
        static void Known(Fields f, string what, params string[] names)
        {
            foreach (var key in f.Keys)
                if (System.Array.IndexOf(names, key) < 0) throw new FormatException($"{what}: unknown field \"{key}\"");
        }

        static Fields Obj(object v, string what) => v as Fields ?? throw new FormatException($"{what} must be an object");

        static List<object> List(Fields f, string key) =>
            !f.TryGetValue(key, out var v) || v == null ? new List<object>() : v as List<object> ?? throw new FormatException($"\"{key}\" must be a list");

        static string Str(Fields f, string key, string fallback) =>
            !f.TryGetValue(key, out var v) ? fallback : v as string ?? throw new FormatException($"\"{key}\" must be text");

        static bool Bool(Fields f, string key, bool fallback) =>
            !f.TryGetValue(key, out var v) ? fallback : v is bool b ? b : throw new FormatException($"\"{key}\" must be true or false");

        static float Float(Fields f, string key, float fallback) =>
            !f.TryGetValue(key, out var v) ? fallback : v is JsonLite.Number n ? n.Float : throw new FormatException($"\"{key}\" must be a number");

        static int Int(Fields f, string key, int fallback) =>
            !f.TryGetValue(key, out var v) ? fallback : v is JsonLite.Number n ? n.Int : throw new FormatException($"\"{key}\" must be a number");

        static T Enum<T>(Fields f, string key, string id) where T : struct
        {
            string s = Str(f, key, null);
            if (s == null || !System.Enum.TryParse(s, true, out T value) || int.TryParse(s, out _))
                throw new FormatException($"{id}: \"{key}\" must be one of {string.Join(", ", System.Enum.GetNames(typeof(T)))}, not \"{s}\"");
            return value;
        }

        static Vector2 Vec(Fields f, string key, Vector2? fallback, string id)
        {
            if (!f.TryGetValue(key, out var v))
                return fallback ?? throw new FormatException($"{id}: \"{key}\" is required");
            return ToVec(v, $"{id} {key}");
        }

        static Vector2 ToVec(object v, string what)
        {
            var n = Floats(v, what);
            if (n.Length != 2) throw new FormatException($"{what} must be [x, y]");
            return new Vector2(n[0], n[1]);
        }

        static float[] Floats(object v, string what)
        {
            if (!(v is List<object> list)) throw new FormatException($"{what} must be a list of numbers");
            var n = new float[list.Count];
            for (int i = 0; i < n.Length; i++)
                n[i] = list[i] is JsonLite.Number x ? x.Float : throw new FormatException($"{what} must be a list of numbers");
            return n;
        }
    }
}
