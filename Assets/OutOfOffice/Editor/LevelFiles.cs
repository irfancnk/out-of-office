using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace OutOfOffice.EditorTools
{
    // Reading and writing the level files on disk (OUT-12). Files are always written in the one canonical form
    // LevelData.ToJson gives, with LF line ends, so a save only changes the lines that really changed.
    public static class LevelFiles
    {
        public const string Dir = "Assets/OutOfOffice/Resources/" + Levels.Folder;
        public static string ManifestPath => $"{Dir}/{Levels.ManifestName}.json";
        public static string PathOf(string id) => $"{Dir}/{id}.json";

        // Ids are save keys and file names: short, lower case, letters, digits and dashes.
        public static bool ValidId(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length > 24 || id == Levels.ManifestName) return false;
            foreach (char c in id)
                if (!(c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '-')) return false;
            return true;
        }

        public static string Read(string path) => File.Exists(path) ? Normalize(File.ReadAllText(path)) : null;

        // Git on Windows may check text out with CRLF; the format is defined with LF.
        public static string Normalize(string text) => text.Replace("\r\n", "\n");

        // Returns true when the file changed.
        public static bool Write(string path, string text)
        {
            if (Read(path) == text) return false;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, text, new System.Text.UTF8Encoding(false));
            return true;
        }

        public static bool Save(LevelDef def) => Write(PathOf(def.Id), LevelData.ToJson(def));

        public static LevelData.Manifest ReadManifest()
        {
            string text = Read(ManifestPath);
            return text == null ? new LevelData.Manifest() : LevelData.ManifestFromJson(text);
        }

        public static bool SaveManifest(LevelData.Manifest m) => Write(ManifestPath, LevelData.ManifestToJson(m));

        public static void Delete(string id)
        {
            AssetDatabase.DeleteAsset(PathOf(id));
        }

        // Every level file in the folder, by id (chapters.json excluded).
        public static List<string> AllIds()
        {
            var ids = new List<string>();
            if (!Directory.Exists(Dir)) return ids;
            foreach (var path in Directory.GetFiles(Dir, "*.json"))
            {
                string id = Path.GetFileNameWithoutExtension(path);
                if (id != Levels.ManifestName) ids.Add(id);
            }
            ids.Sort(System.StringComparer.Ordinal);
            return ids;
        }

        // After files change: import them (so builds and Resources see them) and reload the game's level list.
        public static void Refresh()
        {
            AssetDatabase.Refresh();
            Levels.Reload();
        }

        // Ids that have shipped in a build. Saves key on them, so they must never disappear (OUT-12).
        public const string ShippedIdsPath = "Assets/OutOfOffice/Editor/ShippedLevelIds.txt";

        public static List<string> ShippedIds()
        {
            var ids = new List<string>();
            if (!File.Exists(ShippedIdsPath)) return ids;
            foreach (var raw in File.ReadAllLines(ShippedIdsPath))
            {
                string line = raw.Trim();
                if (line.Length > 0 && !line.StartsWith("#")) ids.Add(line);
            }
            return ids;
        }

        public static string Describe(Vector2 v) => $"({v.x:0.##}, {v.y:0.##})";
    }
}
