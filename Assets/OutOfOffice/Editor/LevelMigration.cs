using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace OutOfOffice.EditorTools
{
    // The one-off move of the rooms from C# to level files (OUT-12).
    //   Export: writes every LegacyLevels room and chapters.json, with the same ids.
    //   Verify: proves the files load into exactly the same boards (field by field, outline and rail points to the
    //           bit), and that LevelCheck gives the same verdict, line for line, from code and from data.
    // Batch: Unity -batchmode -executeMethod OutOfOffice.EditorTools.LevelMigration.ExportAndVerifyBatch
    public static class LevelMigration
    {
        [MenuItem("Out of Office/Levels/Migration/Export C# Levels")]
        public static void Export()
        {
            var manifest = new LevelData.Manifest { Tutorial = LegacyLevels.Tutorial.Id };
            var seen = new HashSet<string>();
            void Teach(LevelDef def)
            {
                // The gadget a room introduces: the first one not seen in any earlier room.
                foreach (var g in Gadgets.In(def))
                    if (seen.Add(g.Key) && string.IsNullOrEmpty(def.Teaches)) def.Teaches = g.Key;
            }
            Teach(LegacyLevels.Tutorial);
            LevelFiles.Save(LegacyLevels.Tutorial);
            var starts = LegacyLevels.ChapterStart;
            for (int c = 0; c + 1 < starts.Length; c++)
            {
                var entry = new LevelData.ChapterEntry { Theme = Themes.All[c].Name };
                for (int i = starts[c]; i < starts[c + 1]; i++)
                {
                    var def = LegacyLevels.All[i];
                    Teach(def);
                    LevelFiles.Save(def);
                    entry.Levels.Add(def.Id);
                }
                manifest.Chapters.Add(entry);
            }
            LevelFiles.SaveManifest(manifest);
            LevelFiles.Refresh();
            Debug.Log($"HSMIGRATE exported {LegacyLevels.All.Length} rooms and the tutorial to {LevelFiles.Dir}");
        }

        [MenuItem("Out of Office/Levels/Migration/Verify Data Against C#")]
        public static int Verify()
        {
            Levels.Reload();
            var report = new StringBuilder("OUT OF OFFICE LEVEL MIGRATION\n");
            int bad = 0;
            void Line(bool ok, string what) { if (!ok) bad++; report.AppendLine($"HSMIGRATE {(ok ? "PASS" : "FAIL")} {what}"); }

            Line(Levels.Errors.Count == 0, $"load errors {Levels.Errors.Count} {string.Join("; ", Levels.Errors)}");
            Line(Levels.All.Length == LegacyLevels.All.Length, $"room count data {Levels.All.Length} vs code {LegacyLevels.All.Length}");
            Line(string.Join(",", Levels.ChapterStart) == string.Join(",", LegacyLevels.ChapterStart),
                $"chapter starts data {string.Join(",", Levels.ChapterStart)} vs code {string.Join(",", LegacyLevels.ChapterStart)}");

            int same = 0;
            for (int i = 0; i < Mathf.Min(Levels.All.Length, LegacyLevels.All.Length); i++)
            {
                var diff = LevelDiff.Compare(LegacyLevels.All[i], Levels.All[i]);
                if (diff.Count == 0) same++;
                else Line(false, $"{i + 1} {LegacyLevels.All[i].Id}: {string.Join("; ", diff)}");
            }
            Line(same == LegacyLevels.All.Length, $"identical boards {same}/{LegacyLevels.All.Length}");
            var tut = LevelDiff.Compare(LegacyLevels.Tutorial, Levels.Tutorial);
            Line(tut.Count == 0, $"tutorial {string.Join("; ", tut)}");

            // Same LevelCheck verdict, sweep for sweep: identical boards must play identically.
            var fromCode = new List<LevelCheck.Result>();
            var fromData = new List<LevelCheck.Result>();
            LevelCheck.Run(LegacyLevels.All, fromCode);
            LevelCheck.Run(Levels.All, fromData);
            int agree = 0;
            for (int i = 0; i < Mathf.Min(fromCode.Count, fromData.Count); i++)
            {
                if (fromCode[i].Line == fromData[i].Line) agree++;
                else Line(false, $"LevelCheck {fromCode[i].Id}: code \"{fromCode[i].Line}\" vs data \"{fromData[i].Line}\"");
            }
            Line(agree == LegacyLevels.All.Length && fromCode.Count == fromData.Count, $"LevelCheck results identical {agree}/{LegacyLevels.All.Length}");

            report.AppendLine($"HSMIGRATE done, failures: {bad}");
            Debug.Log(report.ToString());
            return bad;
        }

        public static void ExportAndVerifyBatch()
        {
            int bad;
            try
            {
                Export();
                bad = Verify();
            }
            catch (System.Exception e)
            {
                Debug.LogException(e);
                bad = 1;
            }
            EditorApplication.Exit(bad == 0 ? 0 : 5);
        }
    }
}
