using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace OutOfOffice.EditorTools
{
    // The soundtrack and the sound set (OUT-17): every song renders off the main thread, its stems line up and loop
    // without a click, the songs sit at one loudness, renders are repeatable, and they fit the memory and time budgets.
    public static class AudioChecks
    {
        const float MaxSongMb = 12f;        // all three stems of one song, as 32-bit samples
        const float MaxRenderSeconds = 2f;  // one song on one editor core; a phone's worker thread has seconds of slack

        // Unity -batchmode -executeMethod OutOfOffice.EditorTools.AudioChecks.RunBatch
        public static void RunBatch() => UnityEditor.EditorApplication.Exit(Run() == 0 ? 0 : 6);

        public static int Run()
        {
            int failures = 0;
            void Check(bool ok, string name)
            {
                Debug.Log("HSAUDIO " + name + (ok ? " PASS" : " FAIL"));
                if (!ok) failures++;
            }

            var specs = new List<SongSpec> { Songs.Menu };
            specs.AddRange(Songs.Chapters);
            Check(specs.Select(s => s.Id).Distinct().Count() == specs.Count, "song ids unique");
            Check(Songs.Chapters.Length >= 7, "at least 7 chapter songs");
            int chapters = ChapterCount();
            var chapterSongs = Enumerable.Range(0, chapters).Select(c => Songs.ForChapter(c).Id).ToList();
            Check(chapterSongs.Distinct().Count() == Math.Min(chapters, Songs.Chapters.Length), "each chapter has its own song");

            // Everything renders on worker threads at once: any Unity call inside the composer would throw here.
            var watch = Stopwatch.StartNew();
            var tasks = specs.Select(s => Task.Run(() =>
            {
                var w = Stopwatch.StartNew();
                var st = Composer.Render(s);
                return (st, seconds: w.Elapsed.TotalSeconds);
            })).ToArray();
            bool threaded = true;
            try { Task.WaitAll(tasks); }
            catch (Exception e) { Debug.LogException(e); threaded = false; }
            Check(threaded, "songs render on worker threads");
            if (!threaded) return failures;
            Debug.Log($"HSAUDIO rendered {specs.Count} songs in parallel in {watch.Elapsed.TotalSeconds:0.00} s");

            var mixRms = new List<float>();
            foreach (var t in tasks)
            {
                var (st, seconds) = t.Result;
                string id = st.Spec.Id;
                int expected = (int)Math.Round(Composer.Rate * 60.0 / st.Spec.Bpm * 4 * st.Spec.Bars);
                Check(st.Bed.Length == expected && st.Groove.Length == expected && st.Lead.Length == expected, id + " stems are bar-exact and equal");
                Check(Finite(st.Bed) && Finite(st.Groove) && Finite(st.Lead) && Finite(st.Win) && Finite(st.Lose), id + " no NaN or infinity");

                var mix = new float[expected];
                for (int i = 0; i < expected; i++) mix[i] = st.Bed[i] + st.Groove[i] + st.Lead[i];
                Check(Peak(mix) <= 0.951f, id + " mix never clips");
                mixRms.Add(Composer.Rms(mix));
                Check(Composer.Rms(st.Bed) > 0.01f && Composer.Rms(st.Groove) > 0.005f && Composer.Rms(st.Lead) > 0.01f, id + " every stem plays");

                // The seam: the step from the last sample to the first is no bigger than the steps inside the song.
                Check(SeamOk(mix), id + " loops without a click");
                Check(SeamOk(st.Bed) && SeamOk(st.Groove) && SeamOk(st.Lead), id + " each stem loops without a click");

                Check(st.Win.Length > Composer.Rate && Math.Abs(Peak(st.Win) - 0.8f) < 0.01f && Math.Abs(st.Win[st.Win.Length - 1]) < 0.01f, id + " win stinger");
                Check(st.Lose.Length > Composer.Rate && Math.Abs(Peak(st.Lose) - 0.8f) < 0.01f && Math.Abs(st.Lose[st.Lose.Length - 1]) < 0.01f, id + " lose stinger");

                float mb = 3f * expected * 4f / (1024f * 1024f);
                Check(mb <= MaxSongMb, $"{id} fits the memory budget ({mb:0.0} MB)");
                Check(seconds <= MaxRenderSeconds, $"{id} renders in time ({seconds:0.00} s)");
            }

            // One loudness for every song (within 1.5 dB of the target), so a new chapter never jumps out.
            float lo = mixRms.Min(), hi = mixRms.Max();
            Check(Db(hi / Composer.TargetRms) < 1.5f && Db(Composer.TargetRms / lo) < 1.5f, $"songs share a loudness ({Db(lo):0.0} to {Db(hi):0.0} dBFS RMS)");

            // Repeatable: the same spec gives the same samples, so every player hears the same song.
            var again = Composer.Render(Songs.Chapters[1]);
            var first = tasks[2].Result.st;
            Check(again.Lead.SequenceEqual(first.Lead) && again.Groove.SequenceEqual(first.Groove), "renders are deterministic");

            // Composed melodies: no two chapters share a tune.
            var composed = tasks.Select(t => t.Result.st).Where(s => s.Spec.Melody == null).ToList();
            bool distinct = true;
            for (int i = 0; i < composed.Count; i++)
                for (int j = i + 1; j < composed.Count; j++)
                    distinct &= !(composed[i].Lead.Length == composed[j].Lead.Length && composed[i].Lead.SequenceEqual(composed[j].Lead));
            Check(composed.Count >= 5 && distinct, "chapters have their own composed tunes");

            SfxChecks(Check);
            return failures;
        }

        static void SfxChecks(Action<bool, string> Check)
        {
            Sfx.Init();
            var sets = new Dictionary<string, AudioClip[]>
            {
                { "wood", Sfx.Wood }, { "rubber", Sfx.Rubber }, { "crates", Sfx.Crates }, { "softs", Sfx.Softs }, { "lands", Sfx.Lands },
                { "taps", Sfx.Taps }, { "metal", Sfx.Metal }, { "glass", Sfx.Glass }, { "coin ticks", Sfx.CoinTicks },
            };
            foreach (var kv in sets)
                Check(kv.Value != null && kv.Value.Length >= 3 && kv.Value.All(c => c != null) && kv.Value.Distinct().Count() == kv.Value.Length, kv.Key + " has 3+ distinct takes");

            Check(Sfx.Fanfare != null && Sfx.Fanfare.Length == Enum.GetValues(typeof(Rarity)).Length, "a fanfare per rarity");
            bool growing = true;
            for (int i = 1; i < Sfx.Fanfare.Length; i++) growing &= Sfx.Fanfare[i].length > Sfx.Fanfare[i - 1].length;
            Check(growing, "rarer fanfares are longer");
            foreach (var c in Sfx.Fanfare.Concat(new[] { Sfx.KaChing, Sfx.GemShimmer, Sfx.Riser, Sfx.Flourish }))
            {
                var data = new float[c.samples];
                c.GetData(data, 0);
                Check(Finite(data) && Peak(data) <= 1f && Peak(data) > 0.05f, c.name + " is audible and never clips");
            }

            // Variant rotation: never the same take twice running, and every take gets played.
            var rng = new System.Random(3);
            foreach (int count in new[] { 2, 3, 4 })
            {
                int last = -1;
                bool repeats = false;
                var seen = new HashSet<int>();
                for (int i = 0; i < 2000; i++)
                {
                    int pick = GameAudio.PickVariant(count, last, (float)rng.NextDouble());
                    repeats |= pick == last || pick < 0 || pick >= count;
                    seen.Add(pick);
                    last = pick;
                }
                Check(!repeats && seen.Count == count, $"variants of {count} rotate without repeats");
            }
            Check(GameAudio.PickVariant(3, 2, 1f) < 3 && GameAudio.PickVariant(3, -1, 1f) == 2 && GameAudio.PickVariant(1, 0, 0.5f) == 0, "variant pick edges");
        }

        static int ChapterCount() => Themes.All.Length;

        static bool Finite(float[] a)
        {
            foreach (var v in a) if (float.IsNaN(v) || float.IsInfinity(v)) return false;
            return true;
        }

        static float Peak(float[] a)
        {
            float p = 0f;
            foreach (var v in a) p = Math.Max(p, Math.Abs(v));
            return p;
        }

        static bool SeamOk(float[] a)
        {
            float maxStep = 0f;
            for (int i = 1; i < a.Length; i++) maxStep = Math.Max(maxStep, Math.Abs(a[i] - a[i - 1]));
            return Math.Abs(a[0] - a[a.Length - 1]) <= maxStep + 1e-6f;
        }

        static float Db(float ratio) => 20f * Mathf.Log10(Mathf.Max(1e-9f, ratio));
    }
}
