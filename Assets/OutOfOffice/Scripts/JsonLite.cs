using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace OutOfOffice
{
    // A small JSON reader and writer for the level files (OUT-12). JsonUtility can't omit default fields or keep
    // numbers short, and level files are read and diffed by people, so they want both. No reflection: safe under IL2CPP.
    //
    // Reading gives Dictionary<string, object> for objects, List<object> for arrays, string, bool, null, and
    // JsonLite.Number for numbers (kept as text, so a float parses straight from what was written, with no double step).
    public static class JsonLite
    {
        public readonly struct Number
        {
            public readonly string Text;
            public Number(string text) => Text = text;
            public float Float => float.Parse(Text, NumberStyles.Float, CultureInfo.InvariantCulture);
            public int Int => (int)System.Math.Round(double.Parse(Text, NumberStyles.Float, CultureInfo.InvariantCulture));
            public override string ToString() => Text;
        }

        public class ParseException : System.Exception
        {
            public ParseException(string message, int line) : base($"line {line}: {message}") { }
        }

        public static object Parse(string text)
        {
            var r = new Reader(text);
            r.SkipSpace();
            object value = r.Value();
            r.SkipSpace();
            if (!r.End) throw r.Error("text after the end of the document");
            return value;
        }

        class Reader
        {
            readonly string s;
            int i;
            public Reader(string text) { s = text ?? ""; }
            public bool End => i >= s.Length;

            public ParseException Error(string what)
            {
                int line = 1;
                for (int k = 0; k < i && k < s.Length; k++) if (s[k] == '\n') line++;
                return new ParseException(what, line);
            }

            public void SkipSpace()
            {
                while (i < s.Length)
                {
                    char c = s[i];
                    if (c == ' ' || c == '\t' || c == '\n' || c == '\r' || c == '﻿') i++;
                    else break;
                }
            }

            public object Value()
            {
                if (End) throw Error("unexpected end of the document");
                char c = s[i];
                switch (c)
                {
                    case '{': return Obj();
                    case '[': return Arr();
                    case '"': return Str();
                    case 't': Word("true"); return true;
                    case 'f': Word("false"); return false;
                    case 'n': Word("null"); return null;
                }
                if (c == '-' || (c >= '0' && c <= '9')) return Num();
                throw Error($"unexpected '{c}'");
            }

            void Word(string w)
            {
                if (string.CompareOrdinal(s, i, w, 0, w.Length) != 0) throw Error($"expected {w}");
                i += w.Length;
            }

            Dictionary<string, object> Obj()
            {
                var d = new Dictionary<string, object>();
                i++;
                SkipSpace();
                if (!End && s[i] == '}') { i++; return d; }
                while (true)
                {
                    SkipSpace();
                    if (End || s[i] != '"') throw Error("expected a field name");
                    string key = Str();
                    SkipSpace();
                    if (End || s[i] != ':') throw Error($"expected ':' after \"{key}\"");
                    i++;
                    SkipSpace();
                    if (d.ContainsKey(key)) throw Error($"field \"{key}\" appears twice");
                    d[key] = Value();
                    SkipSpace();
                    if (End) throw Error("unclosed object");
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == '}') { i++; return d; }
                    throw Error("expected ',' or '}'");
                }
            }

            List<object> Arr()
            {
                var list = new List<object>();
                i++;
                SkipSpace();
                if (!End && s[i] == ']') { i++; return list; }
                while (true)
                {
                    SkipSpace();
                    list.Add(Value());
                    SkipSpace();
                    if (End) throw Error("unclosed array");
                    if (s[i] == ',') { i++; continue; }
                    if (s[i] == ']') { i++; return list; }
                    throw Error("expected ',' or ']'");
                }
            }

            string Str()
            {
                var sb = new StringBuilder();
                i++;
                while (true)
                {
                    if (End) throw Error("unclosed string");
                    char c = s[i++];
                    if (c == '"') return sb.ToString();
                    if (c != '\\') { sb.Append(c); continue; }
                    if (End) throw Error("unclosed string");
                    char e = s[i++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (i + 4 > s.Length) throw Error("bad \\u escape");
                            sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                            i += 4;
                            break;
                        default: throw Error($"bad escape \\{e}");
                    }
                }
            }

            Number Num()
            {
                int start = i;
                if (s[i] == '-') i++;
                while (i < s.Length && "0123456789.eE+-".IndexOf(s[i]) >= 0) i++;
                string text = s.Substring(start, i - start);
                if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out _)) throw Error($"bad number {text}");
                return new Number(text);
            }
        }

        // ---------- Writing ----------

        // An object whose fields keep the order they were added in, so files always come out the same way.
        public class Obj
        {
            public readonly List<KeyValuePair<string, object>> Fields = new List<KeyValuePair<string, object>>();
            public Obj Add(string key, object value) { Fields.Add(new KeyValuePair<string, object>(key, value)); return this; }
        }

        // The shortest text that reads back as exactly this float. -0 writes as 0.
        public static string Format(float v)
        {
            if (v == 0f) return "0";
            string r = v.ToString("R", CultureInfo.InvariantCulture);
            if (float.Parse(r, NumberStyles.Float, CultureInfo.InvariantCulture) != v) r = v.ToString("G9", CultureInfo.InvariantCulture);
            return r;
        }

        // Pretty-prints with each value on one line when it fits in `width`, so small things (a block, a point list)
        // stay on one line and a level reads top to bottom like the old builder code did.
        public static string Write(object value, int width = 120)
        {
            var sb = new StringBuilder();
            WriteValue(sb, value, 0, 0, width);
            sb.Append('\n');
            return sb.ToString();
        }

        static void WriteValue(StringBuilder sb, object value, int indent, int column, int width)
        {
            string inline = Inline(value);
            if (column + inline.Length <= width || !(value is Obj || value is System.Collections.IList)) { sb.Append(inline); return; }
            string pad = new string(' ', indent + 2);
            if (value is Obj o)
            {
                sb.Append("{\n");
                for (int k = 0; k < o.Fields.Count; k++)
                {
                    sb.Append(pad).Append(Quote(o.Fields[k].Key)).Append(": ");
                    WriteValue(sb, o.Fields[k].Value, indent + 2, indent + 2 + o.Fields[k].Key.Length + 4, width);
                    sb.Append(k + 1 < o.Fields.Count ? ",\n" : "\n");
                }
                sb.Append(' ', indent).Append('}');
            }
            else if (value is System.Collections.IList list)
            {
                sb.Append("[\n");
                for (int k = 0; k < list.Count; k++)
                {
                    sb.Append(pad);
                    WriteValue(sb, list[k], indent + 2, indent + 2, width);
                    sb.Append(k + 1 < list.Count ? ",\n" : "\n");
                }
                sb.Append(' ', indent).Append(']');
            }
            else sb.Append(inline);
        }

        static string Inline(object value)
        {
            switch (value)
            {
                case null: return "null";
                case string str: return Quote(str);
                case bool b: return b ? "true" : "false";
                case float f: return Format(f);
                case int n: return n.ToString(CultureInfo.InvariantCulture);
                case Number num: return num.Text;
                case Obj o:
                {
                    if (o.Fields.Count == 0) return "{}";
                    var sb = new StringBuilder("{ ");
                    for (int k = 0; k < o.Fields.Count; k++)
                    {
                        if (k > 0) sb.Append(", ");
                        sb.Append(Quote(o.Fields[k].Key)).Append(": ").Append(Inline(o.Fields[k].Value));
                    }
                    return sb.Append(" }").ToString();
                }
                case System.Collections.IList list:
                {
                    var sb = new StringBuilder("[");
                    for (int k = 0; k < list.Count; k++)
                    {
                        if (k > 0) sb.Append(", ");
                        sb.Append(Inline(list[k]));
                    }
                    return sb.Append(']').ToString();
                }
            }
            throw new System.ArgumentException("JsonLite can't write a " + value.GetType().Name);
        }

        public static string Quote(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4"));
                        else sb.Append(c);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }
    }
}
