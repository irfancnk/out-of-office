using System.IO;
using UnityEditor;
using UnityEngine;

namespace OutOfOffice.EditorTools
{
    // Writes the soundtrack (OUT-17) to Builds/Soundtrack as WAV files for listening and review: each song's full
    // mix played twice through (so the loop seam can be heard), its three stems, and its stingers.
    // Batch: Unity -batchmode -executeMethod OutOfOffice.EditorTools.SoundtrackExport.RunBatch
    public static class SoundtrackExport
    {
        [MenuItem("Out of Office/Export Soundtrack")]
        public static void Export()
        {
            string dir = Path.Combine(Directory.GetCurrentDirectory(), "Builds", "Soundtrack");
            Directory.CreateDirectory(dir);
            var specs = new System.Collections.Generic.List<SongSpec> { Songs.Menu };
            specs.AddRange(Songs.Chapters);
            for (int s = 0; s < specs.Count; s++)
            {
                var st = Composer.Render(specs[s]);
                string name = $"{s:00}_{st.Spec.Id}";
                int n = st.Bed.Length;
                var mix = new float[n * 2];
                for (int i = 0; i < n * 2; i++) mix[i] = st.Bed[i % n] + st.Groove[i % n] + st.Lead[i % n];
                Wav(Path.Combine(dir, name + ".wav"), mix, st.Rate);
                Wav(Path.Combine(dir, name + "_bed.wav"), st.Bed, st.Rate);
                Wav(Path.Combine(dir, name + "_groove.wav"), st.Groove, st.Rate);
                Wav(Path.Combine(dir, name + "_lead.wav"), st.Lead, st.Rate);
                Wav(Path.Combine(dir, name + "_win.wav"), st.Win, st.Rate);
                Wav(Path.Combine(dir, name + "_lose.wav"), st.Lose, st.Rate);
            }
            Sfx.Init();
            foreach (var clip in Sfx.Fanfare) Wav(Path.Combine(dir, "sfx_" + clip.name + ".wav"), Data(clip), clip.frequency);
            foreach (var clip in new[] { Sfx.KaChing, Sfx.GemShimmer, Sfx.Riser, Sfx.Flourish })
                Wav(Path.Combine(dir, "sfx_" + clip.name + ".wav"), Data(clip), clip.frequency);
            Debug.Log("HSAUDIO soundtrack exported to " + dir);
        }

        public static void RunBatch()
        {
            Export();
            EditorApplication.Exit(0);
        }

        static float[] Data(AudioClip clip)
        {
            var d = new float[clip.samples];
            clip.GetData(d, 0);
            return d;
        }

        // 16-bit mono PCM.
        static void Wav(string path, float[] data, int rate)
        {
            using var w = new BinaryWriter(File.Create(path));
            int bytes = data.Length * 2;
            w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            w.Write(36 + bytes);
            w.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            w.Write(16);
            w.Write((short)1);
            w.Write((short)1);
            w.Write(rate);
            w.Write(rate * 2);
            w.Write((short)2);
            w.Write((short)16);
            w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            w.Write(bytes);
            foreach (var v in data) w.Write((short)Mathf.Clamp(Mathf.RoundToInt(v * 32767f), -32768, 32767));
        }
    }
}
