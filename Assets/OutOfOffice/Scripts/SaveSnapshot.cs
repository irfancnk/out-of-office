using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace OutOfOffice
{
    // The whole save as one versioned document (OUT-35): what a cloud save uploads, and what a restore or a second
    // device merges. PlayerPrefs can't list its keys, so the snapshot walks the documented key list in Progress.
    [Serializable]
    public class SaveSnapshot
    {
        public int Version = 1;
        public long SavedAt;      // UTC ticks, from the guarded or server clock
        public string InstallId = "";
        public List<string> IntKeys = new List<string>();
        public List<int> IntValues = new List<int>();
        public List<string> StringKeys = new List<string>();
        public List<string> StringValues = new List<string>();

        static readonly string[] StringKeyNames =
        {
            "buca.current", "buca.skin", "buca.outfit.employee", "buca.outfit.color", "buca.outfit.hat", "buca.outfit.face",
            "buca.clock.seen", "buca.install", "buca.install.at", "buca.pass.until",
        };

        static readonly string[] IntKeyNames =
        {
            "buca.save", "buca.level", "buca.unlocked", "buca.chests.opened", "buca.quest.day", "buca.daily.chest", "buca.clock.rollbacks",
            "buca.sfx", "buca.music", "buca.reduce", "buca.card.stamps", "buca.card.claimed", "buca.card.version", "buca.gems",
            "buca.pass.day", "buca.ads.day", "buca.ads.count", "buca.sessions",
        };

        public static IEnumerable<string> IntKeysOf()
        {
            foreach (var k in IntKeyNames) yield return k;
            for (int i = 0; i < Quests.PerDay; i++) { yield return "buca.quest.p" + i; yield return "buca.quest.c" + i; }
            for (int i = 0; i < 20; i++) yield return "buca.stars." + i;  // version 1 saves
            foreach (var l in Levels.All) { yield return "buca.best." + l.Id; yield return "buca.shots." + l.Id; yield return "buca.coffee." + l.Id; }
            for (int c = 0; c < Themes.All.Length; c++) yield return "buca.chapter.chest." + c;
            foreach (var item in Cosmetics.All) yield return "buca.own." + item.Id;
            foreach (var p in Catalog.All) yield return "buca.iap." + p.Id;
        }

        public static SaveSnapshot Capture(IPrefs prefs, DateTime utcNow, string installId)
        {
            var s = new SaveSnapshot { SavedAt = utcNow.Ticks, InstallId = installId };
            foreach (var k in IntKeysOf())
                if (prefs.HasKey(k)) { s.IntKeys.Add(k); s.IntValues.Add(prefs.GetInt(k, 0)); }
            foreach (var k in StringKeyNames)
                if (prefs.HasKey(k)) { s.StringKeys.Add(k); s.StringValues.Add(prefs.GetString(k, "")); }
            return s;
        }

        public void ApplyTo(IPrefs prefs)
        {
            for (int i = 0; i < IntKeys.Count; i++) prefs.SetInt(IntKeys[i], IntValues[i]);
            for (int i = 0; i < StringKeys.Count; i++) prefs.SetString(StringKeys[i], StringValues[i]);
            prefs.Save();
        }

        public string ToJson() => JsonUtility.ToJson(this);
        public static SaveSnapshot FromJson(string json) => string.IsNullOrEmpty(json) ? null : JsonUtility.FromJson<SaveSnapshot>(json);

        public int Int(string key, int fallback = 0) { int i = IntKeys.IndexOf(key); return i < 0 ? fallback : IntValues[i]; }
        public string Str(string key, string fallback = "") { int i = StringKeys.IndexOf(key); return i < 0 ? fallback : StringValues[i]; }

        // Two saves of the same player (two devices, or a restore over newer play). The newer one is the base.
        // Progress and purchases keep the best of both, so nothing earned or paid for is ever lost. Balances
        // (gems, stamps) are never added together, which would duplicate them: they come from the newer save.
        public static SaveSnapshot Merge(SaveSnapshot a, SaveSnapshot b)
        {
            if (a == null) return b;
            if (b == null) return a;
            var newer = a.SavedAt >= b.SavedAt ? a : b;
            var older = ReferenceEquals(newer, a) ? b : a;
            var m = JsonUtility.FromJson<SaveSnapshot>(newer.ToJson());
            for (int i = 0; i < older.IntKeys.Count; i++)
            {
                string k = older.IntKeys[i];
                int theirs = older.IntValues[i];
                int idx = m.IntKeys.IndexOf(k);
                bool best = k == "buca.unlocked" || k.StartsWith("buca.best.") || k.StartsWith("buca.coffee.") || k.StartsWith("buca.own.")
                            || k.StartsWith("buca.iap.") || k.StartsWith("buca.chapter.chest.");
                bool fewest = k.StartsWith("buca.shots.");
                if (idx < 0) { if (best || fewest) { m.IntKeys.Add(k); m.IntValues.Add(theirs); } continue; }
                int ours = m.IntValues[idx];
                if (best) m.IntValues[idx] = Mathf.Max(ours, theirs);
                else if (fewest && theirs > 0 && (ours == 0 || theirs < ours)) m.IntValues[idx] = theirs;
            }
            // The pass: whichever save runs longest.
            long.TryParse(older.Str("buca.pass.until", "0"), out long theirPass);
            long.TryParse(m.Str("buca.pass.until", "0"), out long ourPass);
            if (theirPass > ourPass)
            {
                int i = m.StringKeys.IndexOf("buca.pass.until");
                if (i < 0) { m.StringKeys.Add("buca.pass.until"); m.StringValues.Add(theirPass.ToString()); }
                else m.StringValues[i] = theirPass.ToString();
            }
            return m;
        }
    }

    // Where snapshots are kept. A real cloud provider (Unity Gaming Services Cloud Save or Firebase) needs the
    // owner's project; until then the backup lives on the device, which covers restore and gives the merge a test bed.
    public interface ICloudSave
    {
        void Upload(SaveSnapshot snapshot);
        SaveSnapshot Download();
    }

    public class LocalBackup : ICloudSave
    {
        readonly string path;
        public LocalBackup(string path) { this.path = path; }

        public void Upload(SaveSnapshot snapshot)
        {
            string tmp = path + ".tmp";
            File.WriteAllText(tmp, snapshot.ToJson());
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);  // never a half-written backup
        }

        public SaveSnapshot Download() => File.Exists(path) ? SaveSnapshot.FromJson(File.ReadAllText(path)) : null;
    }
}
