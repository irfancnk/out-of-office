using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace OutOfOffice.EditorTools
{
    // Where the level data lives on disk, and the editor's reading and writing of it (OUT-12).
    public static class LevelFiles
    {
        public const string LevelsDir = "Assets/OutOfOffice/Resources/Levels";
        public const string RoomsDir = LevelsDir + "/Rooms";
        public const string CatalogFile = LevelsDir + "/catalog.json";
        // Every room id that has ever shipped. Saves key on them, so the checks fail if one disappears (OUT-12).
        public const string ShippedIdsFile = "Assets/OutOfOffice/Editor/ShippedLevelIds.txt";
        public const string ShippedIdsHeader =
            "# Room ids that have shipped to players. Saves store stars and coffees under these ids (buca.best.<id>),\n" +
            "# so a room listed here must never be renamed or removed from the catalog. Add new ids when they ship.\n";

        public static string RoomPath(string id) => RoomsDir + "/" + id + ".json";

        public static List<string> ShippedIds()
        {
            var ids = new List<string>();
            if (!File.Exists(ShippedIdsFile)) return ids;
            foreach (var line in File.ReadAllLines(ShippedIdsFile))
            {
                string t = line.Trim();
                if (t.Length > 0 && !t.StartsWith("#")) ids.Add(t);
            }
            return ids;
        }

        // The catalog as the files hold it right now (not the copy the game has cached).
        public static LevelCatalog ReadCatalog() =>
            File.Exists(CatalogFile) ? LevelData.ParseCatalog(File.ReadAllText(CatalogFile), ReadRoom) : new LevelCatalog();

        public static LevelDef ReadRoom(string id)
        {
            string path = RoomPath(id);
            return File.Exists(path) ? LevelData.ParseLevel(File.ReadAllText(path)) : null;
        }

        public static void WriteRoom(LevelDef def)
        {
            Directory.CreateDirectory(RoomsDir);
            File.WriteAllText(RoomPath(def.Id), LevelData.ToJson(def));
        }

        public static void WriteCatalog(IList<ChapterDef> chapters) => File.WriteAllText(CatalogFile, LevelData.CatalogJson(chapters));

        // Room files on disk, by id.
        public static List<string> RoomFileIds()
        {
            var ids = new List<string>();
            if (!Directory.Exists(RoomsDir)) return ids;
            foreach (var f in Directory.GetFiles(RoomsDir, "*.json")) ids.Add(Path.GetFileNameWithoutExtension(f));
            ids.Sort(System.StringComparer.Ordinal);
            return ids;
        }

        // Imports what changed and drops the game's cached rooms, so the next use (Play mode, LevelCheck) sees it.
        public static void Refresh()
        {
            AssetDatabase.Refresh();
            Levels.Reload();
        }

        // ---------- Exact comparison ----------

        // Null when the two rooms are the same in every field, including every outline and rail point bit for bit;
        // otherwise the first difference.
        public static string Diff(LevelDef a, LevelDef b)
        {
            if (a == null || b == null) return a == b ? null : "one room is missing";
            var d = new StringBuilder();
            void Check(string what, bool same) { if (!same && d.Length == 0) d.Append(what); }
            Check("id", a.Id == b.Id);
            Check("name", a.Name == b.Name);
            Check("hint", a.Hint == b.Hint);
            Check("par", a.Par == b.Par);
            Check("max shots", a.MaxShots == b.MaxShots && a.MaxShotsOverride == b.MaxShotsOverride);
            Check("difficulty", a.Difficulty == b.Difficulty);
            Check("boss", a.Boss == b.Boss);
            Check("teaches", (a.Teaches ?? "") == (b.Teaches ?? ""));
            Check("tags", string.Join(",", a.Tags) == string.Join(",", b.Tags));
            Check("hole", Same(a.Hole, b.Hole));
            Check("start", Same(a.Start, b.Start));
            Check("shape", a.Shape.Kind == b.Shape.Kind && Same(a.Shape.Outline, b.Shape.Outline));
            Check("block count", a.Blocks.Count == b.Blocks.Count);
            for (int i = 0; d.Length == 0 && i < a.Blocks.Count; i++)
            {
                BlockDef x = a.Blocks[i], y = b.Blocks[i];
                Check($"block {i}", x.Kind == y.Kind && x.Shape == y.Shape && Same(x.Center, y.Center) && Same(x.Size, y.Size)
                    && Same(x.Corner, y.Corner) && Same(x.Angle, y.Angle) && Same(x.MoveOffset, y.MoveOffset) && Same(x.Period, y.Period)
                    && Same(x.Phase, y.Phase) && Same(x.Spin, y.Spin) && x.Group == y.Group && Same(x.BlinkPeriod, y.BlinkPeriod)
                    && Same(x.BlinkPhase, y.BlinkPhase) && x.Reception == y.Reception);
            }
            Check("zone count", a.Zones.Count == b.Zones.Count);
            for (int i = 0; d.Length == 0 && i < a.Zones.Count; i++)
            {
                ZoneDef x = a.Zones[i], y = b.Zones[i];
                Check($"zone {i}", x.Kind == y.Kind && Same(x.Center, y.Center) && Same(x.Size, y.Size) && Same(x.Angle, y.Angle)
                    && x.Link == y.Link && x.Group == y.Group && Same(x.Turn, y.Turn) && Same(x.Period, y.Period) && Same(x.Phase, y.Phase));
            }
            Check("rail count", a.Rails.Count == b.Rails.Count);
            for (int i = 0; d.Length == 0 && i < a.Rails.Count; i++)
            {
                RailDef x = a.Rails[i], y = b.Rails[i];
                Check($"rail {i}", x.Kind == y.Kind && Same(x.Width, y.Width) && Same(x.Path, y.Path));
            }
            Check("stars", Same(a.Stars, b.Stars));
            Check("bridge count", a.Bridges.Count == b.Bridges.Count);
            for (int i = 0; d.Length == 0 && i < a.Bridges.Count; i++)
                Check($"bridge {i}", Same(a.Bridges[i].x, b.Bridges[i].x) && Same(a.Bridges[i].y, b.Bridges[i].y)
                    && Same(a.Bridges[i].width, b.Bridges[i].width) && Same(a.Bridges[i].height, b.Bridges[i].height));
            return d.Length == 0 ? null : d.ToString();
        }

        // Bit for bit: a negative zero is not a zero here, since it could steer an angle the other way.
        static bool Same(float x, float y) => System.BitConverter.SingleToInt32Bits(x) == System.BitConverter.SingleToInt32Bits(y);
        static bool Same(Vector2 x, Vector2 y) => Same(x.x, y.x) && Same(x.y, y.y);

        static bool Same(IList<Vector2> x, IList<Vector2> y)
        {
            if (x.Count != y.Count) return false;
            for (int i = 0; i < x.Count; i++) if (!Same(x[i], y[i])) return false;
            return true;
        }
    }
}
