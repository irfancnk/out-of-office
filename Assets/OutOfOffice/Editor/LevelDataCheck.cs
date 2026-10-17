using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace OutOfOffice.EditorTools
{
    // Guards the level files (OUT-12). Part of Run All Checks:
    //   load        every file listed in chapters.json loads, with no format errors
    //   round-trip  each file is in canonical form (load and save gives the same text), and save then load gives
    //               the same board, so nothing a designer sets is ever lost on a save
    //   shipped-ids no id in ShippedLevelIds.txt has been removed or renamed: saves key on them
    //   files       every level file is in a chapter (or is the tutorial), and file names match ids
    //   lint        no room breaks a LevelLint rule
    public static class LevelDataCheck
    {
        [MenuItem("Out of Office/Levels/Check Level Files")]
        static void RunFromMenu() => Run();

        public static int Run()
        {
            Levels.Reload();
            var report = new StringBuilder("OUT OF OFFICE LEVEL DATA CHECK\n");
            int bad = 0;
            void Line(string name, bool ok, string detail)
            {
                if (!ok) bad++;
                report.AppendLine($"HSLEVELDATA {name} {(ok ? "PASS" : "FAIL")} {detail}");
            }

            Line("load", Levels.Errors.Count == 0, Levels.Errors.Count == 0 ? $"{Levels.All.Length} rooms in {Levels.Chapters.Length} chapters" : string.Join("; ", Levels.Errors));

            var rooms = new List<LevelDef>(Levels.All) { Levels.Tutorial };
            var notCanonical = new List<string>();
            var lossy = new List<string>();
            foreach (var def in rooms)
            {
                string text = LevelFiles.Read(LevelFiles.PathOf(def.Id));
                if (text == null) continue;
                string again = LevelData.ToJson(LevelData.FromJson(text));
                if (again != text) notCanonical.Add($"{def.Id} (line {FirstDifferentLine(text, again)})");
                var diff = LevelDiff.Compare(def, LevelData.FromJson(LevelData.ToJson(def)));
                if (diff.Count > 0) lossy.Add($"{def.Id}: {diff[0]}");
            }
            Line("round-trip", notCanonical.Count == 0 && lossy.Count == 0,
                notCanonical.Count + lossy.Count == 0 ? $"{rooms.Count} files canonical and lossless"
                    : $"not canonical (open and save in the Level Editor, or Levels > Re-save All): {string.Join(", ", notCanonical)}  lossy: {string.Join(", ", lossy)}");
            string manifestText = LevelFiles.Read(LevelFiles.ManifestPath);
            Line("manifest-canonical", manifestText != null && manifestText == LevelData.ManifestToJson(LevelData.ManifestFromJson(manifestText)), LevelFiles.ManifestPath);

            var ids = new HashSet<string>();
            foreach (var def in rooms) ids.Add(def.Id);
            var shipped = LevelFiles.ShippedIds();
            var gone = shipped.FindAll(id => !ids.Contains(id));
            Line("shipped-ids", shipped.Count > 0 && gone.Count == 0,
                gone.Count == 0 ? $"all {shipped.Count} shipped ids present" : $"MISSING {string.Join(", ", gone)}: players' saves key on these ids; restore them");

            var files = LevelFiles.AllIds();
            var orphans = files.FindAll(id => !ids.Contains(id));
            var badNames = files.FindAll(id => !LevelFiles.ValidId(id));
            Line("files", orphans.Count == 0 && badNames.Count == 0,
                $"{files.Count} level files" + (orphans.Count > 0 ? $"  NOT IN ANY CHAPTER {string.Join(", ", orphans)}" : "")
                + (badNames.Count > 0 ? $"  BAD NAMES {string.Join(", ", badNames)}" : ""));

            int errors = 0, warnings = 0;
            var lint = new StringBuilder();
            foreach (var def in rooms)
                foreach (var p in LevelLint.Check(def))
                {
                    if (p.Error) { errors++; lint.Append($"  {def.Id}: {p.Text}"); }
                    else warnings++;
                }
            Line("lint", errors == 0, $"{errors} errors, {warnings} warnings{lint}");

            report.AppendLine($"HSLEVELDATA done, failures: {bad}");
            Debug.Log(report.ToString());
            return bad;
        }

        static int FirstDifferentLine(string a, string b)
        {
            string[] x = a.Split('\n'), y = b.Split('\n');
            for (int i = 0; i < Mathf.Min(x.Length, y.Length); i++) if (x[i] != y[i]) return i + 1;
            return Mathf.Min(x.Length, y.Length) + 1;
        }
    }
}
