using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace OutOfOffice.EditorTools
{
    // The level data's own checks (OUT-12), part of Run All Checks:
    //   - every shipped room id is still in the catalog (saves key on them)
    //   - every room file parses, is named after its id, and is in the catalog exactly once (no stray rooms in the build)
    //   - every room survives a write and a read bit for bit, so saving in the editor never changes a room
    //   - the game's loaded catalog is the one on disk
    //   - no shipped room has a lint error
    public static class LevelDataChecks
    {
        public static int Run()
        {
            int failures = 0;
            void Report(string name, bool ok, string detail)
            {
                if (!ok) failures++;
                Debug.Log($"HSDATA {name} {(ok ? "PASS" : "FAIL")} {detail}");
            }

            var catalog = LevelFiles.ReadCatalog();
            var listed = new List<string>();
            foreach (var ch in catalog.Chapters) listed.AddRange(ch.LevelIds);
            var listedSet = new HashSet<string>(listed);

            var shipped = LevelFiles.ShippedIds();
            var lost = shipped.FindAll(id => !listedSet.Contains(id));
            Report("shipped-ids", shipped.Count > 0 && lost.Count == 0,
                lost.Count == 0 ? $"{shipped.Count} shipped ids all in the catalog" : "MISSING " + string.Join(", ", lost));

            var files = LevelFiles.RoomFileIds();
            var stray = files.FindAll(id => !listedSet.Contains(id));
            var missing = listed.FindAll(id => !files.Contains(id));
            Report("files", stray.Count == 0 && missing.Count == 0 && listed.Count == listedSet.Count,
                $"{files.Count} room files, {listed.Count} listed" + (stray.Count > 0 ? " NOT IN CATALOG " + string.Join(", ", stray) : "")
                + (missing.Count > 0 ? " NO FILE " + string.Join(", ", missing) : "") + (listed.Count != listedSet.Count ? " LISTED TWICE" : ""));

            int parsed = 0, same = 0, misnamed = 0;
            var broken = new List<string>();
            foreach (var id in files)
            {
                try
                {
                    string text = File.ReadAllText(LevelFiles.RoomPath(id));
                    var def = LevelData.ParseLevel(text);
                    parsed++;
                    if (def.Id != id) { misnamed++; broken.Add($"{id}.json says id '{def.Id}'"); }
                    string diff = LevelFiles.Diff(def, LevelData.ParseLevel(LevelData.ToJson(def)));
                    if (diff == null) same++;
                    else broken.Add($"{id} round trip changes {diff}");
                }
                catch (System.Exception e)
                {
                    broken.Add($"{id}: {e.Message}");
                }
            }
            Report("round-trip", parsed == files.Count && same == files.Count && misnamed == 0,
                $"{same}/{files.Count} rooms read back bit for bit" + (broken.Count > 0 ? " " + string.Join("; ", broken) : ""));

            Levels.Reload();
            bool loaded = Levels.All.Length == listed.Count;
            for (int i = 0; loaded && i < listed.Count; i++) loaded = Levels.All[i].Id == listed[i];
            Report("game-catalog", loaded, $"the game loads {Levels.All.Length} rooms in {Levels.Chapters.Count} chapters");

            int errors = 0, warnings = 0;
            var notes = new List<string>();
            foreach (var def in Levels.All)
                foreach (var p in LevelLint.Check(def))
                {
                    if (p.Error) { errors++; notes.Add($"{def.Id}: {p.Text}"); }
                    else warnings++;
                }
            Report("lint", errors == 0, $"{errors} errors, {warnings} warnings" + (notes.Count > 0 ? " " + string.Join("; ", notes) : ""));

            // A room the editor builds from scratch, saved and read back, is the same room: the designer's whole loop.
            var fresh = LevelTemplates.Make("blank", "lint-fresh", "Fresh Room");
            Report("template", LevelFiles.Diff(fresh, LevelData.Clone(fresh)) == null && !LevelLint.HasErrors(LevelLint.Check(fresh)),
                "a new room from the blank template saves, reloads and lints clean");

            Debug.Log($"HSDATA done, failures: {failures}");
            return failures;
        }
    }
}
