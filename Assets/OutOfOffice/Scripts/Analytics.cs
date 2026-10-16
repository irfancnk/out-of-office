using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
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
        // Read once on the main thread: Debug.isDebugBuild throws on the SDK threads some callbacks arrive on.
        static readonly bool DebugBuild = Debug.isDebugBuild;

        // Writes buffered events now: the app may be closing.
        public static void Flush()
        {
            foreach (var sink in Sinks)
            {
                try { (sink as LocalLogSink)?.Flush(); }
                catch (Exception) { }
            }
        }

        public static void Event(string name, params (string Key, object Value)[] fields)
        {
            var data = new Dictionary<string, object>(fields.Length + 4);
            foreach (var (key, value) in fields) data[key] = value;
            data["iid"] = InstallId;
            data["sid"] = Session;
            data["t"] = DateTime.UtcNow.ToString("o");
            FrameWatch.Note(name);
            Sent?.Invoke(name, data);
            foreach (var sink in Sinks)
            {
                try { sink.Send(name, data); }
                catch (Exception) { }  // a broken sink never breaks the game
            }
            if (!DebugBuild) return;
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
    // Lines are buffered and written off the main thread every few seconds (OUT-36), so an event never stalls a frame.
    public class LocalLogSink : IAnalyticsSink
    {
        public readonly string Path;
        public readonly long MaxBytes;
        readonly List<string> pending = new List<string>();
        readonly object fileLock = new object();
        DateTime nextWrite;
        bool writing;

        public LocalLogSink(string path, long maxBytes = 512 * 1024)
        {
            Path = path;
            MaxBytes = maxBytes;
        }

        public void Send(string name, IReadOnlyDictionary<string, object> data)
        {
            bool due;
            lock (pending)
            {
                pending.Add(Analytics.ToJson(name, data));
                due = pending.Count >= 64 || DateTime.UtcNow >= nextWrite;
            }
            if (due) Flush(false);
        }

        // Writes what is buffered: on a worker thread, or right here (wait) when the app may be closing.
        public void Flush(bool wait = true)
        {
            if (wait)
            {
                WritePending();
                return;
            }
            lock (pending)
            {
                if (writing) return;
                writing = true;
            }
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try { WritePending(); }
                catch (Exception) { }  // no log is better than no game
                finally { lock (pending) writing = false; }
            });
        }

        // Lines leave the buffer under the file lock, so they reach the file in the order they were sent.
        void WritePending()
        {
            lock (fileLock)
            {
                string[] lines;
                lock (pending)
                {
                    nextWrite = DateTime.UtcNow.AddSeconds(5);
                    if (pending.Count == 0) return;
                    lines = pending.ToArray();
                    pending.Clear();
                }
                var info = new FileInfo(Path);
                long size = info.Exists ? info.Length : 0;
                var chunk = new StringBuilder();
                foreach (var line in lines)
                {
                    if (size > MaxBytes)
                    {
                        if (chunk.Length > 0) File.AppendAllText(Path, chunk.ToString());
                        chunk.Clear();
                        string old = Path + ".1";
                        if (File.Exists(old)) File.Delete(old);
                        File.Move(Path, old);
                        size = 0;
                    }
                    chunk.Append(line).Append('\n');
                    size += Encoding.UTF8.GetByteCount(line) + 1;
                }
                if (chunk.Length > 0) File.AppendAllText(Path, chunk.ToString());
            }
        }
    }
}
