using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace OutOfOffice
{
    // Where events go (OUT-34). A Firebase or Unity Gaming Services sink plugs in here once the owner sets up the
    // project; the local log already records everything on the device for QA and later upload.
    public interface IAnalyticsSink
    {
        void Send(string name, IReadOnlyDictionary<string, object> data);
    }

    // Gameplay and money events for design decisions. Every event carries the anonymous install id, the session
    // number and the UTC time. Editor and development builds also print each event; release builds don't.
    public static class Analytics
    {
        public static event Action<string, IReadOnlyDictionary<string, object>> Sent;
        public static readonly List<IAnalyticsSink> Sinks = new List<IAnalyticsSink>();
        public static string InstallId = "", Platform = "";
        public static int Session;

        public static void Event(string name, params (string Key, object Value)[] fields)
        {
            var data = new Dictionary<string, object>(fields.Length + 4);
            foreach (var (key, value) in fields) data[key] = value;
            data["iid"] = InstallId;
            data["sid"] = Session;
            data["t"] = DateTime.UtcNow.ToString("o");
            Sent?.Invoke(name, data);
            foreach (var sink in Sinks)
            {
                try { sink.Send(name, data); }
                catch (Exception) { }  // a broken sink never breaks the game
            }
            if (!Debug.isDebugBuild) return;
            var line = new StringBuilder("HSEVENT ").Append(name);
            foreach (var (key, value) in fields) line.Append(' ').Append(key).Append('=').Append(value);
            Debug.Log(line.ToString());
        }

        // One JSON object per line.
        public static string ToJson(string name, IReadOnlyDictionary<string, object> data)
        {
            var sb = new StringBuilder("{\"e\":").Append(Quote(name));
            foreach (var kv in data)
            {
                sb.Append(',').Append(Quote(kv.Key)).Append(':');
                switch (kv.Value)
                {
                    case null: sb.Append("null"); break;
                    case bool b: sb.Append(b ? "true" : "false"); break;
                    case int i: sb.Append(i); break;
                    case long l: sb.Append(l); break;
                    case float f: sb.Append(f.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture)); break;
                    default: sb.Append(Quote(kv.Value.ToString())); break;
                }
            }
            return sb.Append('}').ToString();
        }

        static string Quote(string s) => "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n") + "\"";

        // Crash capture: unhandled exceptions and errors become "error" events, with a stable hash of the stack so
        // the same crash groups together.
        public static void CaptureErrors()
        {
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
        }

        static bool inLog;

        static void OnLog(string message, string stack, LogType type)
        {
            if (inLog || (type != LogType.Exception && type != LogType.Error)) return;
            inLog = true;
            try
            {
                uint hash = 2166136261;
                foreach (char c in stack ?? "") { hash ^= c; hash *= 16777619; }
                string text = message ?? "";
                Event("error", ("type", type.ToString()), ("message", text.Length > 160 ? text.Substring(0, 160) : text), ("stack", hash.ToString("x8")));
            }
            finally { inLog = false; }
        }
    }

    // Appends events to a capped JSON-lines file on the device; the oldest half rolls into a ".1" file when full.
    public class LocalLogSink : IAnalyticsSink
    {
        public readonly string Path;
        public readonly long MaxBytes;

        public LocalLogSink(string path, long maxBytes = 512 * 1024)
        {
            Path = path;
            MaxBytes = maxBytes;
        }

        public void Send(string name, IReadOnlyDictionary<string, object> data)
        {
            var info = new FileInfo(Path);
            if (info.Exists && info.Length > MaxBytes)
            {
                string old = Path + ".1";
                if (File.Exists(old)) File.Delete(old);
                File.Move(Path, old);
            }
            File.AppendAllText(Path, Analytics.ToJson(name, data) + "\n");
        }
    }
}
