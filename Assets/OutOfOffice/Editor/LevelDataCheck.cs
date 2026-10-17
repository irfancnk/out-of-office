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

            Line("format", FormatRules(out string formatNote), formatNote);
            Line("broken-file", BrokenFileIsSkipped(out string brokenNote), brokenNote);
            // The editor's Check button: one room on its own, with a solution whose replayed path ends in the bed.
            var bank = LevelCheck.CheckOne(Levels.All[Levels.IndexOf("bank")]);
            var lastShot = bank.Solution.Count > 0 ? bank.Solution[bank.Solution.Count - 1].Path : null;
            bool traced = bank.Solved && bank.Solution.Count == bank.MinShots && lastShot != null && lastShot.Count > 1
                          && (lastShot[lastShot.Count - 1] - Levels.All[Levels.IndexOf("bank")].Hole).magnitude < Rules.HoleRadius + 0.2f;
            Line("solver-trace", traced, $"bank: {bank.Solution.Count} shot solution, min {bank.MinShots}, path ends {(lastShot == null || lastShot.Count == 0 ? "nowhere" : LevelFiles.Describe(lastShot[lastShot.Count - 1]))}");
            var editor = LevelEditorWindow.SelfTest();
            Line("editor", editor.Count == 0, editor.Count == 0 ? "edit, copy, paste, undo to the start, redo, duplicate, mirror, new room" : string.Join("; ", editor));
            Levels.Reload();

            report.AppendLine($"HSLEVELDATA done, failures: {bad}");
            Debug.Log(report.ToString());
            return bad;
        }

        // The reader refuses what it should, and keeps what it must.
        static bool FormatRules(out string note)
        {
            var fails = new List<string>();
            void Rejects(string what, string json)
            {
                try { LevelData.FromJson(json); fails.Add("accepted " + what); }
                catch (LevelData.FormatException) { }
            }
            const string ok = "{ \"id\": \"t\", \"name\": \"T\", \"par\": 1, \"hole\": [0, 5] }";
            Rejects("a typo'd field", ok.Replace("\"par\"", "\"pra\""));
            Rejects("a missing bed", "{ \"id\": \"t\", \"name\": \"T\" }");
            Rejects("an unknown block kind", ok.Replace("}", ", \"blocks\": [{ \"kind\": \"Trampoline\", \"at\": [0, 0], \"size\": [1, 1] }] }"));
            Rejects("a number as a kind", ok.Replace("}", ", \"blocks\": [{ \"kind\": \"3\", \"at\": [0, 0], \"size\": [1, 1] }] }"));
            Rejects("a point with three numbers", ok.Replace("[0, 5]", "[0, 5, 1]"));
            Rejects("broken JSON", ok.Replace("}", ""));
            Rejects("a 2-corner polygon", ok.Replace("}", ", \"shape\": { \"kind\": \"Polygon\", \"points\": [[0, 0], [1, 1]] } }"));

            // Text and numbers that must survive a save untouched.
            var def = new LevelDef("t", "Quotes \"and\" \\ back, slash / tab\t", 0.1f, 1f / 3f) { Hint = "ÜNICODE ☕" }.WithPar(2);
            def.Stars.Add(new Vector2(float.Epsilon, -123456.7f));
            def.Blocks.Add(new BlockDef(BlockKind.Wall, new Vector2(1e-7f, 3.4028235e38f), new Vector2(0.3f, 0.6f)) { Angle = -0f });
            var back = LevelData.FromJson(LevelData.ToJson(def));
            var diff = LevelDiff.Compare(def, back);
            if (diff.Count > 0) fails.Add("lost on a save: " + diff[0]);
            if (LevelData.ToJson(back) != LevelData.ToJson(def)) fails.Add("saving twice changes the text");
            note = fails.Count == 0 ? "rejects 7 bad files, keeps odd text and extreme numbers exactly" : string.Join("; ", fails);
            return fails.Count == 0;
        }

        // One broken file must not take the game down: that room is left out and reported, and the rest load.
        static bool BrokenFileIsSkipped(out string note)
        {
            var source = Levels.Source;
            int count = Levels.All.Length;
            string victim = Levels.All[1].Id;
            try
            {
                Levels.Source = name => name == victim ? "{ \"id\": \"" + victim + "\", \"hole\": [0, 5], \"oops\" " : source(name);
                Levels.Reload(false);
                bool ok = Levels.All.Length == count - 1 && Levels.Errors.Count == 1 && Levels.IndexOf(victim) < 0 && Levels.All[0].Id != victim;
                Levels.Source = name => null;  // a build with no level files at all still has a room to show
                Levels.Reload(false);
                ok &= Levels.All.Length == 1 && Levels.All[0] == Levels.Lab && Levels.Tutorial != null && Levels.ChapterStart.Length == 2;
                note = ok ? $"a corrupt {victim}.json leaves {count - 1} rooms and one error; no files at all falls back to the lab" : "a broken file broke more than its own room";
                return ok;
            }
            finally
            {
                Levels.Source = source;
                Levels.Reload();
            }
        }

        static int FirstDifferentLine(string a, string b)
        {
            string[] x = a.Split('\n'), y = b.Split('\n');
            for (int i = 0; i < Mathf.Min(x.Length, y.Length); i++) if (x[i] != y[i]) return i + 1;
            return Mathf.Min(x.Length, y.Length) + 1;
        }
    }
}
