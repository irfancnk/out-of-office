using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace OutOfOffice
{
    // The parsed catalog: chapters in order, and every room they name by id.
    public class LevelCatalog
    {
        public readonly List<ChapterDef> Chapters = new List<ChapterDef>();
        public readonly Dictionary<string, LevelDef> Rooms = new Dictionary<string, LevelDef>();
    }

    // Rooms and chapters as data (OUT-12). The format is plain JSON a designer can read and diff: one room per file, one
    // block, zone or rail per line, defaults left out. Numbers are written in their shortest exact form, so a room
    // read back is bit-for-bit the room that was written. The same text can come from the app or from live ops.
    //
    //   { "id": "first", "name": "Day One, Already Tired", "par": 1, "hint": "DRAG BACK, AIM, RELEASE",
    //     "hole": [0, 4.5], "start": [0, -6], "shape": { "kind": "rect", "args": [9, 16, 0.7] },
    //     "blocks": [ { "kind": "Wall", "at": [-2.95, 0.8], "size": [3.1, 0.6] }, ... ],
    //     "zones": [...], "rails": [...], "stars": [[0, -2.4], ...], "bridges": [[x, y, w, h], ...] }
    public static class LevelData
    {
        public const int FormatVersion = 1;

        // ---------- Rooms ----------

        public static LevelDef ParseLevel(string json) => FromJson(Json.Parse(json) as Dictionary<string, object>);

        public static LevelDef FromJson(Dictionary<string, object> o)
        {
            if (o == null) throw new System.FormatException("a room must be a JSON object");
            var hole = Vec(o, "hole", Vector2.zero);
            var def = new LevelDef(Str(o, "id", ""), Str(o, "name", ""), hole.x, hole.y)
            {
                Par = Int(o, "par", 1),
                Hint = Str(o, "hint", ""),
                Start = Vec(o, "start", new Vector2(0f, -6f)),
                Difficulty = Enum(o, "difficulty", Difficulty.Normal),
                Boss = Bool(o, "boss", false),
                Teaches = Str(o, "teaches", ""),
            };
            int maxShots = Int(o, "maxShots", 0);
            if (maxShots > 0) def.WithMaxShots(maxShots);
            foreach (var t in List(o, "tags")) if (t is string s) def.Tags.Add(s);

            if (o.TryGetValue("shape", out var sv) && sv is Dictionary<string, object> shape)
                def.Shape = ShapeDef.Make(Str(shape, "kind", ShapeDef.RectKind), Floats(shape, "args"), Vec(shape, "shift", Vector2.zero), Bool(shape, "mirror", false));

            foreach (var item in List(o, "blocks"))
            {
                if (!(item is Dictionary<string, object> b)) continue;
                var block = new BlockDef(Enum(b, "kind", BlockKind.Wall), Vec(b, "at", Vector2.zero), Vec(b, "size", Vector2.one))
                {
                    Shape = Bool(b, "post", false) ? BlockShape.Post : BlockShape.Box,
                    Corner = Float(b, "corner", -1f),
                    Angle = Float(b, "angle", 0f),
                    MoveOffset = Vec(b, "move", Vector2.zero),
                    Period = Float(b, "period", 0f),
                    Phase = Float(b, "phase", 0f),
                    Spin = Float(b, "spin", 0f),
                    Group = Int(b, "group", -1),
                    Reception = Int(b, "reception", 0),
                };
                var blink = Floats(b, "blink");
                if (blink.Length >= 2) { block.BlinkPeriod = blink[0]; block.BlinkPhase = blink[1]; }
                def.Blocks.Add(block);
            }

            foreach (var item in List(o, "zones"))
            {
                if (!(item is Dictionary<string, object> z)) continue;
                def.Zones.Add(new ZoneDef
                {
                    Kind = Enum(z, "kind", ZoneKind.Boost),
                    Center = Vec(z, "at", Vector2.zero),
                    Size = Vec(z, "size", Vector2.one),
                    Angle = Float(z, "angle", 0f),
                    Link = Int(z, "link", -1),
                    Group = Int(z, "group", -1),
                    Turn = Float(z, "turn", 1f),
                    Period = Float(z, "period", 0f),
                    Phase = Float(z, "phase", 0f),
                });
            }

            foreach (var item in List(o, "rails"))
            {
                if (!(item is Dictionary<string, object> r)) continue;
                var rail = new RailDef { Kind = Enum(r, "kind", BlockKind.Wall), Width = Float(r, "width", 0.5f), Mirror = Bool(r, "mirror", false) };
                var arc = Floats(r, "arc");
                if (arc.Length >= 5)
                {
                    rail.IsArc = true;
                    rail.ArcCenter = new Vector2(arc[0], arc[1]);
                    rail.ArcRadius = arc[2];
                    rail.ArcFrom = arc[3];
                    rail.ArcTo = arc[4];
                }
                else
                {
                    rail.Bend = Float(r, "bend", 0f);
                    rail.Points = new List<Vector2>();
                    foreach (var p in List(r, "points")) rail.Points.Add(ToVec(p, Vector2.zero));
                }
                def.Rails.Add(rail.Rebuild());
            }

            foreach (var s in List(o, "stars")) def.Stars.Add(ToVec(s, Vector2.zero));
            foreach (var item in List(o, "bridges"))
            {
                var f = ToFloats(item);
                if (f.Length >= 4) def.Bridges.Add(new Rect(f[0], f[1], f[2], f[3]));
            }
            return def;
        }

        public static string ToJson(LevelDef d)
        {
            var w = new StringBuilder();
            w.Append("{\n");
            Field(w, "id", Quote(d.Id));
            Field(w, "name", Quote(d.Name));
            Field(w, "hint", Quote(d.Hint));
            Field(w, "par", d.Par.ToString(CultureInfo.InvariantCulture));
            if (d.MaxShotsOverride > 0) Field(w, "maxShots", d.MaxShotsOverride.ToString(CultureInfo.InvariantCulture));
            if (d.Difficulty != Difficulty.Normal) Field(w, "difficulty", Quote(d.Difficulty.ToString()));
            if (d.Boss) Field(w, "boss", "true");
            if (!string.IsNullOrEmpty(d.Teaches)) Field(w, "teaches", Quote(d.Teaches));
            if (d.Tags.Count > 0)
            {
                var tags = new List<string>();
                foreach (var t in d.Tags) tags.Add(Quote(t));
                Field(w, "tags", "[" + string.Join(", ", tags) + "]");
            }
            Field(w, "hole", V(d.Hole));
            Field(w, "start", V(d.Start));

            var shape = new StringBuilder("{ \"kind\": ").Append(Quote(d.Shape.Kind)).Append(", \"args\": ").Append(Fs(d.Shape.Args));
            if (d.Shape.Offset != Vector2.zero) shape.Append(", \"shift\": ").Append(V(d.Shape.Offset));
            if (d.Shape.Flipped) shape.Append(", \"mirror\": true");
            Field(w, "shape", shape.Append(" }").ToString());

            var blocks = new List<string>();
            foreach (var b in d.Blocks) blocks.Add(BlockJson(b));
            Items(w, "blocks", blocks);

            var zones = new List<string>();
            foreach (var z in d.Zones) zones.Add(ZoneJson(z));
            Items(w, "zones", zones);

            var rails = new List<string>();
            foreach (var r in d.Rails) rails.Add(RailJson(r));
            Items(w, "rails", rails);

            var stars = new List<string>();
            foreach (var s in d.Stars) stars.Add(V(s));
            Field(w, "stars", "[" + string.Join(", ", stars) + "]");

            var bridges = new List<string>();
            foreach (var r in d.Bridges) bridges.Add(Fs(new[] { r.x, r.y, r.width, r.height }));
            Field(w, "bridges", "[" + string.Join(", ", bridges) + "]", last: true);
            w.Append("}\n");
            return w.ToString();
        }

        public static string BlockJson(BlockDef b)
        {
            var s = new StringBuilder("{ \"kind\": ").Append(Quote(b.Kind.ToString()));
            if (b.Shape == BlockShape.Post) s.Append(", \"post\": true");
            s.Append(", \"at\": ").Append(V(b.Center)).Append(", \"size\": ").Append(V(b.Size));
            if (b.Corner != -1f) s.Append(", \"corner\": ").Append(F(b.Corner));
            if (b.Angle != 0f) s.Append(", \"angle\": ").Append(F(b.Angle));
            if (b.MoveOffset != Vector2.zero) s.Append(", \"move\": ").Append(V(b.MoveOffset));
            if (b.Period != 0f) s.Append(", \"period\": ").Append(F(b.Period));
            if (b.Phase != 0f) s.Append(", \"phase\": ").Append(F(b.Phase));
            if (b.Spin != 0f) s.Append(", \"spin\": ").Append(F(b.Spin));
            if (b.Group != -1) s.Append(", \"group\": ").Append(b.Group.ToString(CultureInfo.InvariantCulture));
            if (b.BlinkPeriod != 0f || b.BlinkPhase != 0f) s.Append(", \"blink\": ").Append(Fs(new[] { b.BlinkPeriod, b.BlinkPhase }));
            if (b.Reception != 0) s.Append(", \"reception\": ").Append(b.Reception.ToString(CultureInfo.InvariantCulture));
            return s.Append(" }").ToString();
        }

        public static string ZoneJson(ZoneDef z)
        {
            var s = new StringBuilder("{ \"kind\": ").Append(Quote(z.Kind.ToString()));
            s.Append(", \"at\": ").Append(V(z.Center)).Append(", \"size\": ").Append(V(z.Size));
            if (z.Angle != 0f) s.Append(", \"angle\": ").Append(F(z.Angle));
            if (z.Link != -1) s.Append(", \"link\": ").Append(z.Link.ToString(CultureInfo.InvariantCulture));
            if (z.Group != -1) s.Append(", \"group\": ").Append(z.Group.ToString(CultureInfo.InvariantCulture));
            if (z.Turn != 1f) s.Append(", \"turn\": ").Append(F(z.Turn));
            if (z.Period != 0f) s.Append(", \"period\": ").Append(F(z.Period));
            if (z.Phase != 0f) s.Append(", \"phase\": ").Append(F(z.Phase));
            return s.Append(" }").ToString();
        }

        public static string RailJson(RailDef r)
        {
            var s = new StringBuilder("{ \"kind\": ").Append(Quote(r.Kind.ToString()));
            if (r.IsArc) s.Append(", \"arc\": ").Append(Fs(new[] { r.ArcCenter.x, r.ArcCenter.y, r.ArcRadius, r.ArcFrom, r.ArcTo }));
            else
            {
                s.Append(", \"bend\": ").Append(F(r.Bend)).Append(", \"points\": [");
                var pts = r.Points ?? new List<Vector2>();
                for (int i = 0; i < pts.Count; i++) s.Append(i > 0 ? ", " : "").Append(V(pts[i]));
                s.Append("]");
            }
            if (r.Width != 0.5f) s.Append(", \"width\": ").Append(F(r.Width));
            if (r.Mirror) s.Append(", \"mirror\": true");
            return s.Append(" }").ToString();
        }

        // Parses one block, zone or rail line, as the level editor copies and pastes them.
        public static BlockDef ParseBlock(string json) => ParseLevel("{\"blocks\":[" + json + "]}").Blocks[0];
        public static ZoneDef ParseZone(string json) => ParseLevel("{\"zones\":[" + json + "]}").Zones[0];
        public static RailDef ParseRail(string json) => ParseLevel("{\"rails\":[" + json + "]}").Rails[0];

        // A deep copy through the data format: what the editor's templates, duplicates and undo use.
        public static LevelDef Clone(LevelDef d) => ParseLevel(ToJson(d));

        // ---------- The catalog ----------

        //   { "version": 1, "chapters": [ { "key": "open-plan", "theme": "open-plan", "levels": ["first", "easy", ...] }, ... ] }
        public static LevelCatalog ParseCatalog(string json, System.Func<string, LevelDef> loadRoom)
        {
            var catalog = new LevelCatalog();
            if (!(Json.Parse(json) is Dictionary<string, object> o)) throw new System.FormatException("the catalog must be a JSON object");
            foreach (var item in List(o, "chapters"))
            {
                if (!(item is Dictionary<string, object> c)) continue;
                var chapter = new ChapterDef { Key = Str(c, "key", ""), ThemeKey = Str(c, "theme", "") };
                chapter.Theme = Themes.ByKey(chapter.ThemeKey) ?? Themes.All[0];
                foreach (var id in List(c, "levels"))
                {
                    if (!(id is string s)) continue;
                    chapter.LevelIds.Add(s);
                    if (!catalog.Rooms.ContainsKey(s)) catalog.Rooms[s] = loadRoom?.Invoke(s);
                }
                catalog.Chapters.Add(chapter);
            }
            return catalog;
        }

        public static string CatalogJson(IList<ChapterDef> chapters)
        {
            var w = new StringBuilder("{\n  \"version\": ").Append(FormatVersion).Append(",\n  \"chapters\": [\n");
            for (int c = 0; c < chapters.Count; c++)
            {
                var ch = chapters[c];
                w.Append("    { \"key\": ").Append(Quote(ch.Key)).Append(", \"theme\": ").Append(Quote(ch.ThemeKey)).Append(", \"levels\": [\n");
                for (int i = 0; i < ch.LevelIds.Count; i++)
                    w.Append("      ").Append(Quote(ch.LevelIds[i])).Append(i + 1 < ch.LevelIds.Count ? ",\n" : "\n");
                w.Append("    ] }").Append(c + 1 < chapters.Count ? ",\n" : "\n");
            }
            return w.Append("  ]\n}\n").ToString();
        }

        // ---------- Writing ----------

        static void Field(StringBuilder w, string key, string value, bool last = false) =>
            w.Append("  ").Append(Quote(key)).Append(": ").Append(value).Append(last ? "\n" : ",\n");

        static void Items(StringBuilder w, string key, List<string> items)
        {
            if (items.Count == 0) { Field(w, key, "[]"); return; }
            w.Append("  ").Append(Quote(key)).Append(": [\n");
            for (int i = 0; i < items.Count; i++) w.Append("    ").Append(items[i]).Append(i + 1 < items.Count ? ",\n" : "\n");
            w.Append("  ],\n");
        }

        // Shortest text that reads back as the same float.
        public static string F(float f)
        {
            string s = f.ToString("R", CultureInfo.InvariantCulture);
            if (float.Parse(s, CultureInfo.InvariantCulture) != f) s = f.ToString("G9", CultureInfo.InvariantCulture);
            return s;
        }

        static string V(Vector2 v) => "[" + F(v.x) + ", " + F(v.y) + "]";

        static string Fs(float[] a)
        {
            var parts = new string[a.Length];
            for (int i = 0; i < a.Length; i++) parts[i] = F(a[i]);
            return "[" + string.Join(", ", parts) + "]";
        }

        public static string Quote(string s)
        {
            var b = new StringBuilder("\"");
            foreach (char c in s ?? "")
            {
                switch (c)
                {
                    case '"': b.Append("\\\""); break;
                    case '\\': b.Append("\\\\"); break;
                    case '\n': b.Append("\\n"); break;
                    case '\r': b.Append("\\r"); break;
                    case '\t': b.Append("\\t"); break;
                    default:
                        if (c < 0x20) b.Append("\\u").Append(((int)c).ToString("x4"));
                        else b.Append(c);
                        break;
                }
            }
            return b.Append('"').ToString();
        }

        // ---------- Reading ----------

        static string Str(Dictionary<string, object> o, string key, string fallback) =>
            o.TryGetValue(key, out var v) && v is string s ? s : fallback;

        static bool Bool(Dictionary<string, object> o, string key, bool fallback) =>
            o.TryGetValue(key, out var v) && v is bool b ? b : fallback;

        static float Float(Dictionary<string, object> o, string key, float fallback) =>
            o.TryGetValue(key, out var v) && v is Json.Number n ? n.Float : fallback;

        static int Int(Dictionary<string, object> o, string key, int fallback) =>
            o.TryGetValue(key, out var v) && v is Json.Number n ? n.Int : fallback;

        static T Enum<T>(Dictionary<string, object> o, string key, T fallback) where T : struct =>
            o.TryGetValue(key, out var v) && v is string s && System.Enum.TryParse(s, out T e) ? e : fallback;

        static List<object> List(Dictionary<string, object> o, string key) =>
            o.TryGetValue(key, out var v) && v is List<object> l ? l : new List<object>();

        static Vector2 Vec(Dictionary<string, object> o, string key, Vector2 fallback) =>
            o.TryGetValue(key, out var v) ? ToVec(v, fallback) : fallback;

        static Vector2 ToVec(object v, Vector2 fallback)
        {
            var f = ToFloats(v);
            return f.Length >= 2 ? new Vector2(f[0], f[1]) : fallback;
        }

        static float[] Floats(Dictionary<string, object> o, string key) => o.TryGetValue(key, out var v) ? ToFloats(v) : new float[0];

        static float[] ToFloats(object v)
        {
            if (!(v is List<object> l)) return new float[0];
            var f = new float[l.Count];
            for (int i = 0; i < l.Count; i++) f[i] = l[i] is Json.Number n ? n.Float : 0f;
            return f;
        }
    }

    // A small strict JSON reader. Numbers keep their text, so a float parses straight from what was written.
    public static class Json
    {
        public sealed class Number
        {
            public readonly string Text;
            public Number(string text) => Text = text;
            public float Float => float.Parse(Text, NumberStyles.Float, CultureInfo.InvariantCulture);
            public int Int => int.TryParse(Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i) ? i : Mathf.RoundToInt(Float);
            public override string ToString() => Text;
        }

        public static object Parse(string text)
        {
            int i = 0;
            var value = Value(text, ref i);
            Space(text, ref i);
            if (i < text.Length) throw Error(text, i, "unexpected text after the end");
            return value;
        }

        static object Value(string s, ref int i)
        {
            Space(s, ref i);
            if (i >= s.Length) throw Error(s, i, "unexpected end");
            char c = s[i];
            if (c == '{') return Obj(s, ref i);
            if (c == '[') return Arr(s, ref i);
            if (c == '"') return Str(s, ref i);
            if (c == '-' || (c >= '0' && c <= '9')) return Num(s, ref i);
            if (Word(s, ref i, "true")) return true;
            if (Word(s, ref i, "false")) return false;
            if (Word(s, ref i, "null")) return null;
            throw Error(s, i, $"unexpected '{c}'");
        }

        static Dictionary<string, object> Obj(string s, ref int i)
        {
            var o = new Dictionary<string, object>();
            i++;
            Space(s, ref i);
            if (i < s.Length && s[i] == '}') { i++; return o; }
            while (true)
            {
                Space(s, ref i);
                if (i >= s.Length || s[i] != '"') throw Error(s, i, "expected a key");
                string key = Str(s, ref i);
                Space(s, ref i);
                if (i >= s.Length || s[i] != ':') throw Error(s, i, "expected ':'");
                i++;
                o[key] = Value(s, ref i);
                Space(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == '}') { i++; return o; }
                throw Error(s, i, "expected ',' or '}'");
            }
        }

        static List<object> Arr(string s, ref int i)
        {
            var l = new List<object>();
            i++;
            Space(s, ref i);
            if (i < s.Length && s[i] == ']') { i++; return l; }
            while (true)
            {
                l.Add(Value(s, ref i));
                Space(s, ref i);
                if (i < s.Length && s[i] == ',') { i++; continue; }
                if (i < s.Length && s[i] == ']') { i++; return l; }
                throw Error(s, i, "expected ',' or ']'");
            }
        }

        static string Str(string s, ref int i)
        {
            var b = new StringBuilder();
            i++;
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return b.ToString();
                if (c != '\\') { b.Append(c); continue; }
                if (i >= s.Length) break;
                char e = s[i++];
                switch (e)
                {
                    case 'n': b.Append('\n'); break;
                    case 'r': b.Append('\r'); break;
                    case 't': b.Append('\t'); break;
                    case 'b': b.Append('\b'); break;
                    case 'f': b.Append('\f'); break;
                    case 'u':
                        if (i + 4 > s.Length) throw Error(s, i, "bad \\u escape");
                        b.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        i += 4;
                        break;
                    default: b.Append(e); break;
                }
            }
            throw Error(s, i, "unterminated string");
        }

        static Number Num(string s, ref int i)
        {
            int start = i;
            if (s[i] == '-') i++;
            while (i < s.Length && "0123456789.eE+-".IndexOf(s[i]) >= 0) i++;
            string text = s.Substring(start, i - start);
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _)) throw Error(s, start, $"bad number '{text}'");
            return new Number(text);
        }

        static bool Word(string s, ref int i, string word)
        {
            if (string.CompareOrdinal(s, i, word, 0, word.Length) != 0) return false;
            i += word.Length;
            return true;
        }

        static void Space(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        static System.FormatException Error(string s, int i, string what)
        {
            int line = 1;
            for (int k = 0; k < i && k < s.Length; k++) if (s[k] == '\n') line++;
            return new System.FormatException($"JSON line {line}: {what}");
        }
    }
}
