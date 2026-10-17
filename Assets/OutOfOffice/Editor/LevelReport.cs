using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace OutOfOffice.EditorTools
{
    // Batch tools for the level files (OUT-12).
    //   Difficulty report: LevelCheck on every room, one CSV row each (fewest shots, one-shot win rate, hazard rate,
    //     coffees reached, suggested par) next to the design fields, for tuning the difficulty curve in a sheet.
    //     Batch: Unity -batchmode -executeMethod OutOfOffice.EditorTools.LevelReport.WriteBatch [-reportPath file.csv]
    //   Re-save all: rewrites every file in canonical form (after a hand edit, or a format change).
    public static class LevelReport
    {
        public const string DefaultPath = "Logs/level-difficulty.csv";

        [MenuItem("Out of Office/Levels/Difficulty Report (CSV)…")]
        public static void WriteWithDialog()
        {
            string path = EditorUtility.SaveFilePanel("Difficulty report", "Logs", "level-difficulty", "csv");
            if (string.IsNullOrEmpty(path)) return;
            Write(path);
            EditorUtility.RevealInFinder(path);
        }

        public static void WriteBatch()
        {
            string path = DefaultPath;
            var args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++) if (args[i] == "-reportPath") path = args[i + 1];
            int bad = 1;
            try { bad = Write(path); }
            catch (System.Exception e) { Debug.LogException(e); }
            EditorApplication.Exit(bad == 0 ? 0 : 3);
        }

        // Returns how many rooms LevelCheck failed.
        public static int Write(string path)
        {
            Levels.Reload();
            var results = new List<LevelCheck.Result>();
            int bad;
            try
            {
                EditorUtility.DisplayProgressBar("Difficulty report", $"LevelCheck on {Levels.All.Length} rooms…", 0.5f);
                bad = LevelCheck.Run(Levels.All, results);
            }
            finally { EditorUtility.ClearProgressBar(); }
            File.WriteAllText(path, Csv(results), new UTF8Encoding(false));
            Debug.Log($"HSREPORT wrote {results.Count} rooms to {path}, {bad} failing");
            return bad;
        }

        public static string Csv(List<LevelCheck.Result> results)
        {
            var sb = new StringBuilder("room,chapter,theme,id,name,par,shot_limit,difficulty,boss,teaches,tags,verdict,min_shots,suggested_par,one_shot_win_pct,hazard_pct,coffees_reached,coffees\n");
            var byId = new Dictionary<string, LevelCheck.Result>();
            foreach (var r in results) byId[r.Id] = r;
            for (int i = 0; i < Levels.All.Length; i++)
            {
                var def = Levels.All[i];
                byId.TryGetValue(def.Id, out var r);
                var chapter = def.Chapter >= 0 && def.Chapter < Levels.Chapters.Length ? Levels.Chapters[def.Chapter] : null;
                string verdict = r == null ? "" : r.Ok ? "OK" : r.Solved ? "BAD" : "UNSOLVED";
                string suggested = r == null || !r.Solved ? "" : $"{r.MinShots}-{r.MinShots + 1}";
                sb.Append(string.Join(",", new[]
                {
                    (i + 1).ToString(), (def.Chapter + 1).ToString(), Cell(chapter?.Theme ?? ""), def.Id, Cell(def.Name), def.Par.ToString(),
                    def.MaxShots.ToString(), def.Difficulty.ToString(), def.IsBoss ? "1" : "0", def.Teaches, Cell(string.Join(" ", def.Tags)), verdict,
                    r == null ? "" : r.MinShots.ToString(), suggested,
                    r == null ? "" : r.OneShotWin.ToString("0.0", CultureInfo.InvariantCulture),
                    r == null ? "" : r.HazardRate.ToString("0.0", CultureInfo.InvariantCulture),
                    r == null ? "" : r.StarsReached.ToString(), def.Stars.Count.ToString(),
                })).Append('\n');
            }
            return sb.ToString();
        }

        static string Cell(string s) => s.IndexOfAny(new[] { ',', '"', '\n' }) < 0 ? s : "\"" + s.Replace("\"", "\"\"") + "\"";

        [MenuItem("Out of Office/Levels/Re-save All Level Files")]
        public static void ResaveAll()
        {
            int changed = 0;
            foreach (string id in LevelFiles.AllIds())
            {
                string text = LevelFiles.Read(LevelFiles.PathOf(id));
                try { if (LevelFiles.Save(LevelData.FromJson(text))) changed++; }
                catch (LevelData.FormatException e) { Debug.LogError($"{id}.json: {e.Message}"); }
            }
            if (LevelFiles.SaveManifest(LevelFiles.ReadManifest())) changed++;
            LevelFiles.Refresh();
            Debug.Log($"Out of Office: re-saved the level files, {changed} changed");
        }
    }
}
