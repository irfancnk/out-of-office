using System;
using System.Collections.Generic;

namespace OutOfOffice
{
    // The procedural soundtrack (OUT-17). Every song is written as data (key, mode, tempo, chords, groove,
    // instruments) and rendered at run time into three stems of the same length that loop seamlessly:
    //   Bed     pad and bass: the harmony, always on
    //   Groove  drums and chord comping: thins out while aiming, fills in while rolling
    //   Lead    melody and sparkles: the tune
    // plus a win and a lose stinger in the song's own key. Rendering is pure C# with no Unity calls, so it runs on a
    // worker thread and the game never stalls for music. Everything is owned by the game: no licensing, no install size.
    public enum Instrument { Kalimba, Marimba, Glock, EPiano, Ocarina, Pluck, MusicBox, Vibes }
    public enum BassStyle { Bounce, Walk, Sustain, Synco }
    public enum Comping { Offbeat, Arp, Stabs, None }

    public class SongSpec
    {
        public string Id, Title;
        public float Bpm = 104f;
        public int Root = 60;                          // MIDI note of the tonic (60 = C4)
        public int[] Mode = Composer.Major;
        public int[] Chords;                           // scale degree (0-based) per bar; 8 entries repeat for 16 bars
        public bool Sevenths;                          // jazzier voicings
        public Instrument Lead = Instrument.Kalimba, Counter = Instrument.Glock, Comp = Instrument.Pluck;
        public BassStyle Bass = BassStyle.Bounce;
        public Comping Comping = Comping.Offbeat;
        public float Swing;                            // 0 straight, 0.33 a light shuffle
        public string Kick = "x.......x.......", Snare = "....x.......x...", Hat = "x.x.x.x.x.x.x.x.", Tick = "";
        public float Pad = 0.5f;                       // pad level in the bed
        public string[] Melody;                        // written melody (one token string per bar), or null to compose one
        public int Seed = 1;
        public int Bars = 16;
    }

    public class SongStems
    {
        public SongSpec Spec;
        public int Rate;
        public float[] Bed, Groove, Lead, Win, Lose;
        public float Seconds => Bed.Length / (float)Rate;
    }

    public static class Composer
    {
        public const int Rate = 22050;  // music is soft and mid-heavy: half the effects' rate halves its memory

        public static readonly int[] Major = { 0, 2, 4, 5, 7, 9, 11 };
        public static readonly int[] Mixolydian = { 0, 2, 4, 5, 7, 9, 10 };
        public static readonly int[] Dorian = { 0, 2, 3, 5, 7, 9, 10 };
        public static readonly int[] Minor = { 0, 2, 3, 5, 7, 8, 10 };
        public static readonly int[] Lydian = { 0, 2, 4, 6, 7, 9, 11 };

        // ---------- instruments ----------

        struct Voice
        {
            public (float ratio, float amp, float decay)[] Partials;
            public float Attack, Release, Ring;  // Ring: how long a note sounds past its written length (plucked voices)
            public bool Sustained;               // held for its written length (ocarina) instead of ringing out
            public float Vibrato;
        }

        static Voice VoiceOf(Instrument i)
        {
            switch (i)
            {
                case Instrument.Kalimba: return new Voice { Partials = new[] { (1f, 1f, 3.2f), (2f, 0.3f, 9f), (5.4f, 0.12f, 30f) }, Attack = 0.003f, Release = 0.05f, Ring = 1.4f };
                case Instrument.Marimba: return new Voice { Partials = new[] { (1f, 1f, 5f), (4f, 0.35f, 14f), (9.9f, 0.08f, 40f) }, Attack = 0.002f, Release = 0.04f, Ring = 0.9f };
                case Instrument.Glock: return new Voice { Partials = new[] { (1f, 1f, 2.5f), (2.76f, 0.32f, 6f), (5.4f, 0.14f, 12f) }, Attack = 0.001f, Release = 0.05f, Ring = 1.8f };
                case Instrument.EPiano: return new Voice { Partials = new[] { (1f, 1f, 1.6f), (2f, 0.25f, 3f), (3f, 0.1f, 5f), (7f, 0.05f, 14f) }, Attack = 0.004f, Release = 0.08f, Ring = 1.6f };
                case Instrument.Ocarina: return new Voice { Partials = new[] { (1f, 1f, 0.5f), (2f, 0.12f, 0.8f), (3f, 0.04f, 1.2f) }, Attack = 0.03f, Release = 0.08f, Sustained = true, Vibrato = 0.012f };
                case Instrument.MusicBox: return new Voice { Partials = new[] { (1f, 1f, 3f), (3.01f, 0.25f, 8f), (6.2f, 0.1f, 20f) }, Attack = 0.001f, Release = 0.05f, Ring = 1.5f };
                case Instrument.Vibes: return new Voice { Partials = new[] { (1f, 1f, 1.2f), (4f, 0.18f, 6f), (10f, 0.04f, 20f) }, Attack = 0.003f, Release = 0.1f, Ring = 2f, Vibrato = 0.006f };
                default: return new Voice { Partials = new[] { (1f, 1f, 9f), (2f, 0.4f, 14f), (3f, 0.15f, 20f) }, Attack = 0.002f, Release = 0.03f, Ring = 0.5f };  // Pluck
            }
        }

        static readonly Voice PadVoice = new Voice { Partials = new[] { (1f, 1f, 0.25f), (1.004f, 0.7f, 0.25f), (2f, 0.18f, 0.5f), (3f, 0.05f, 0.8f) }, Attack = 0.35f, Release = 0.35f, Sustained = true, Vibrato = 0.002f };
        static readonly Voice BassVoice = new Voice { Partials = new[] { (1f, 1f, 2.8f), (2f, 0.45f, 4f), (3f, 0.15f, 6f) }, Attack = 0.006f, Release = 0.04f, Ring = 0f };

        // ---------- render ----------

        public static SongStems Render(SongSpec s)
        {
            var song = new Song(s);
            song.Write();
            var stems = new SongStems { Spec = s, Rate = Rate, Bed = song.Bed, Groove = song.Groove, Lead = song.Lead };
            Normalize(stems);
            stems.Win = song.WinStinger();
            stems.Lose = song.LoseStinger();
            return stems;
        }

        // One gain for all three stems, so their balance survives: the full mix to a common loudness (-18 dBFS RMS,
        // low enough that every song reaches it with peak headroom to spare). Songs therefore sit at one level.
        public const float TargetRms = 0.125f;

        static void Normalize(SongStems st)
        {
            int n = st.Bed.Length;
            double sum = 0;
            float peak = 0f;
            for (int i = 0; i < n; i++)
            {
                float m = st.Bed[i] + st.Groove[i] + st.Lead[i];
                sum += m * m;
                peak = Math.Max(peak, Math.Abs(m));
            }
            float rms = (float)Math.Sqrt(sum / Math.Max(1, n));
            float gain = rms > 1e-6f ? TargetRms / rms : 1f;
            if (peak * gain > 0.95f) gain = 0.95f / peak;
            foreach (var a in new[] { st.Bed, st.Groove, st.Lead })
                for (int i = 0; i < n; i++) a[i] *= gain;
        }

        public static float Rms(float[] a)
        {
            double sum = 0;
            foreach (var v in a) sum += v * v;
            return (float)Math.Sqrt(sum / Math.Max(1, a.Length));
        }

        static float Hz(float midi) => 440f * (float)Math.Pow(2.0, (midi - 69) / 12.0);

        static readonly float[] sine = BuildSine();

        static float[] BuildSine()
        {
            var t = new float[4097];
            for (int i = 0; i <= 4096; i++) t[i] = (float)Math.Sin(2.0 * Math.PI * i / 4096.0);
            return t;
        }

        static float FastSin(double cycles)
        {
            double x = (cycles - Math.Floor(cycles)) * 4096.0;
            int i = (int)x;
            if (i >= 4096) return sine[0];  // a hair under a whole cycle rounds up to it
            return sine[i] + (sine[i + 1] - sine[i]) * (float)(x - i);
        }

        // Adds one note into `data`, wrapping past the end so loops ring across their seam.
        static void Note(float[] data, float start, float freq, float length, float gain, Voice v, bool wrap = true)
        {
            int n = data.Length;
            float sounding = v.Sustained ? length : Math.Max(length, v.Ring);
            int s0 = (int)Math.Round(start * Rate), len = (int)Math.Round((sounding + v.Release) * Rate);
            int attackN = Math.Max(1, (int)(v.Attack * Rate)), releaseN = Math.Max(1, (int)(v.Release * Rate));
            int holdN = len - releaseN;
            foreach (var (ratio, amp, decay) in v.Partials)
            {
                if (freq * ratio > Rate * 0.45f) continue;  // above Nyquist it would fold back as a sour tone
                double phase = 0;
                double step = freq * ratio / Rate;
                float env = amp * gain, k = (float)Math.Exp(-decay / Rate);
                for (int i = 0; i < len; i++)
                {
                    int at = s0 + i;
                    if (at >= n) { if (!wrap) break; at %= n; }
                    if (at < 0) { env *= k; continue; }
                    float shape = Math.Min(1f, i / (float)attackN);
                    if (i > holdN) shape *= Math.Max(0f, (len - i) / (float)releaseN);
                    double vib = v.Vibrato > 0f ? 1.0 + v.Vibrato * Math.Min(1.0, i / (0.25 * Rate)) * FastSin(5.5 * i / Rate) : 1.0;
                    phase += step * vib;
                    data[at] += FastSin(phase) * env * shape;
                    env *= k;
                }
            }
        }

        // ---------- the song ----------

        class Song
        {
            readonly SongSpec s;
            readonly Random rng;
            readonly float beat, bar;
            readonly int n;
            public readonly float[] Bed, Groove, Lead;
            readonly Voice lead, counter, comp;

            public Song(SongSpec spec)
            {
                s = spec;
                rng = new Random(spec.Seed);
                beat = 60f / s.Bpm;
                bar = beat * 4f;
                n = (int)Math.Round(Rate * bar * s.Bars);
                Bed = new float[n];
                Groove = new float[n];
                Lead = new float[n];
                lead = VoiceOf(s.Lead);
                counter = VoiceOf(s.Counter);
                comp = VoiceOf(s.Comp);
            }

            // MIDI note of a scale degree (any integer: 7 is the tonic an octave up, -1 the leading tone below).
            int Deg(int degree)
            {
                int oct = (int)Math.Floor(degree / 7.0);
                return s.Root + 12 * oct + s.Mode[degree - oct * 7];
            }

            int ChordAt(int barIndex) => s.Chords[barIndex % s.Chords.Length];

            // Swing pushes the off-eighths late, by `Swing` of an eighth.
            float Time(int barIndex, float beats)
            {
                float frac = beats * 2f - (float)Math.Floor(beats * 2f);
                bool off = Math.Abs(frac) < 1e-4f && ((int)Math.Round(beats * 2f) % 2 == 1);
                return barIndex * bar + beats * beat + (off ? s.Swing * beat * 0.5f : 0f);
            }

            public void Write()
            {
                for (int b = 0; b < s.Bars; b++)
                {
                    WritePad(b);
                    WriteBass(b);
                    WriteComp(b);
                    WriteDrums(b);
                }
                if (s.Melody != null) WriteWrittenMelody();
                else WriteComposedMelody();
                WriteSparkles();
            }

            int[] Triad(int barIndex)
            {
                int d = ChordAt(barIndex);
                return s.Sevenths ? new[] { d, d + 2, d + 4, d + 6 } : new[] { d, d + 2, d + 4 };
            }

            void WritePad(int b)
            {
                if (s.Pad <= 0f) return;
                foreach (var d in Triad(b))
                    Note(Bed, b * bar, Hz(Deg(d)), bar, 0.05f * s.Pad, PadVoice);
            }

            void WriteBass(int b)
            {
                int d = ChordAt(b);
                float root = Hz(Deg(d) - 24), fifth = Hz(Deg(d + 4) - 24), oct = Hz(Deg(d) - 12);
                switch (s.Bass)
                {
                    case BassStyle.Bounce:
                        Note(Bed, Time(b, 0f), root, 1.3f, 0.2f, BassVoice);
                        Note(Bed, Time(b, 1.5f), root, 0.45f, 0.13f, BassVoice);
                        Note(Bed, Time(b, 2f), fifth, 0.9f, 0.16f, BassVoice);
                        Note(Bed, Time(b, 3f), oct, 0.9f, 0.13f, BassVoice);
                        break;
                    case BassStyle.Walk:
                    {
                        int next = ChordAt(b + 1);
                        float approach = Hz(Deg(next) - 24 - 1);  // a half step under the next root
                        Note(Bed, Time(b, 0f), root, beat, 0.2f, BassVoice);
                        Note(Bed, Time(b, 1f), Hz(Deg(d + 2) - 24), beat, 0.15f, BassVoice);
                        Note(Bed, Time(b, 2f), fifth, beat, 0.16f, BassVoice);
                        Note(Bed, Time(b, 3f), approach, beat, 0.14f, BassVoice);
                        break;
                    }
                    case BassStyle.Sustain:
                        Note(Bed, Time(b, 0f), root, beat * 2f, 0.22f, BassVoice);
                        Note(Bed, Time(b, 2f), root, beat * 2f, 0.16f, BassVoice);
                        break;
                    default:  // Synco
                        Note(Bed, Time(b, 0f), root, 0.6f, 0.2f, BassVoice);
                        Note(Bed, Time(b, 0.75f), root, 0.4f, 0.14f, BassVoice);
                        Note(Bed, Time(b, 2.5f), fifth, 0.6f, 0.15f, BassVoice);
                        Note(Bed, Time(b, 3.5f), oct, 0.4f, 0.12f, BassVoice);
                        break;
                }
            }

            void WriteComp(int b)
            {
                var chord = Triad(b);
                switch (s.Comping)
                {
                    case Comping.Offbeat:
                        for (int k = 0; k < 4; k++)
                            for (int v = 0; v < chord.Length; v++)
                                Note(Groove, Time(b, k + 0.5f) + v * 0.012f, Hz(Deg(chord[v])), 0.5f, 0.045f, comp);
                        break;
                    case Comping.Arp:
                    {
                        int[] order = { 0, 1, 2, 1, 2, 3, 2, 1 };
                        for (int k = 0; k < 8; k++)
                        {
                            int idx = order[k] % (chord.Length + 1);
                            int d = idx < chord.Length ? chord[idx] : chord[0] + 7;
                            Note(Groove, Time(b, k * 0.5f), Hz(Deg(d)), 0.4f, 0.04f, comp);
                        }
                        break;
                    }
                    case Comping.Stabs:
                        foreach (var at in new[] { 0f, 1.5f, 3f })
                            for (int v = 0; v < chord.Length; v++)
                                Note(Groove, Time(b, at) + v * 0.008f, Hz(Deg(chord[v])), 0.35f, 0.04f, comp);
                        break;
                }
            }

            void WriteDrums(int b)
            {
                bool fill = b % 8 == 7;  // a little snare run into each new section
                for (int step = 0; step < 16; step++)
                {
                    float at = Time(b, step * 0.25f);
                    if (Hit(s.Kick, step)) Kick(at);
                    if (Hit(s.Snare, step) && !(fill && step >= 12)) Snap(at, 0.11f);
                    if (Hit(s.Hat, step)) Shaker(at, step % 4 == 2 ? 0.045f : 0.028f);
                    if (Hit(s.Tick, step)) Tick(at);
                }
                if (fill)
                    for (int k = 0; k < 4; k++) Snap(Time(b, 3f + k * 0.25f), 0.05f + 0.025f * k);
            }

            static bool Hit(string pattern, int step) => !string.IsNullOrEmpty(pattern) && step < pattern.Length && pattern[step] != '.';

            void Drum(float start, float seconds, float gain, Func<float, float> wave)
            {
                int s0 = (int)Math.Round(start * Rate), len = (int)Math.Round(seconds * Rate);
                for (int i = 0; i < len; i++)
                    Groove[(s0 + i) % n] += wave(i / (float)Rate) * gain * Math.Min(1f, (len - i) / 200f);
            }

            float Noise() => (float)rng.NextDouble() * 2f - 1f;

            void Kick(float at)
            {
                double ph = 0;
                Drum(at, 0.25f, 0.3f, t =>
                {
                    float f = 55f + 95f * (float)Math.Exp(-t * 28f);
                    ph += f / Rate;
                    return FastSin(ph) * (float)Math.Exp(-t * 14f);
                });
            }

            void Snap(float at, float gain)
            {
                float bl = 0f, bh = 0f;
                Drum(at, 0.18f, gain, t =>
                {
                    float x = Noise();
                    bl += (x - bl) * 0.6f;
                    bh += (bl - bh) * 0.16f;
                    return ((bl - bh) * 1.6f + 0.3f * FastSin(1750.0 * t)) * (float)Math.Exp(-t * 30f);
                });
            }

            void Shaker(float at, float gain)
            {
                float lp = 0f;
                Drum(at, 0.07f, gain, t =>
                {
                    float x = Noise();
                    lp += (x - lp) * 0.45f;
                    return (x - lp) * (float)Math.Exp(-t * 55f);
                });
            }

            // A woodblock, like a typewriter key: the office in the rhythm section.
            void Tick(float at) =>
                Drum(at, 0.08f, 0.06f, t => (FastSin(1250.0 * t) + 0.5f * FastSin(3400.0 * t)) * (float)Math.Exp(-t * 70f));

            // ---------- melody ----------

            void WriteWrittenMelody()
            {
                for (int b = 0; b < s.Bars; b++)
                {
                    float at = 0f;
                    foreach (var token in s.Melody[b % s.Melody.Length].Split(' '))
                    {
                        var parts = token.Split(':');
                        int eighths = int.Parse(parts[1]);
                        if (parts[0] != "R")
                            Note(Lead, Time(b, at * 0.5f), Hz(NoteName(parts[0])), eighths * beat * 0.5f * 0.95f, 0.15f, lead);
                        at += eighths;
                    }
                }
            }

            // Rhythm cells, in eighths, each filling one bar.
            static readonly int[][] Cells =
            {
                new[] { 2, 2, 2, 2 }, new[] { 3, 1, 2, 2 }, new[] { 2, 1, 1, 2, 2 }, new[] { 2, 2, 1, 1, 2 },
                new[] { 1, 1, 2, 1, 1, 2 }, new[] { 3, 3, 2 }, new[] { 2, 2, 4 }, new[] { 4, 2, 2 }, new[] { 2, 1, 1, 4 },
            };

            struct MotifNote
            {
                public int Eighths;
                public int ChordTone;  // which chord tone (0 root, 1 third, 2 fifth), or -1 for a rest
                public int Step;       // a scale step off that chord tone: passing and neighbour notes
            }

            // A melody that sounds written: a two-bar motif, repeated over the changing chords (so it follows the
            // harmony), varied at its tail, answered by a contrasting idea, and closed by cadences on bars 8 and 16.
            // Strong beats land on chord tones; weak beats may step off them.
            void WriteComposedMelody()
            {
                var a1 = MakeBar(false);
                var a2 = MakeBar(false);
                var c1 = MakeBar(false);
                var half = Cadence(false);
                var full = Cadence(true);
                List<MotifNote>[] plan =
                {
                    a1, a2, a1, half,
                    a1, Vary(a2), c1, Cadence(false),
                    c1, Vary(c1), a1, Vary(a2),
                    a1, a2, Vary(c1), full,
                };
                int prev = 9;  // degree of the last note: starts near the middle of the lead range
                for (int b = 0; b < s.Bars; b++)
                {
                    var chord = ChordAt(b);
                    float at = 0f;
                    foreach (var m in plan[b % plan.Length])
                    {
                        if (m.ChordTone >= 0)
                        {
                            int baseDeg = chord + 2 * m.ChordTone + m.Step;
                            int deg = NearestOctave(baseDeg, prev, 7, 15);
                            prev = deg;
                            float len = m.Eighths * beat * 0.5f * (lead.Sustained ? 0.92f : 1f);
                            Note(Lead, Time(b, at * 0.5f), Hz(Deg(deg)), len, 0.15f, lead);
                        }
                        at += m.Eighths;
                    }
                }
            }

            // The octave of `degree` closest to the previous note, kept inside [low, high].
            static int NearestOctave(int degree, int prev, int low, int high)
            {
                int best = degree, bestD = int.MaxValue;
                for (int o = -21; o <= 21; o += 7)
                {
                    int d = degree + o;
                    if (d < low || d > high) continue;
                    int dist = Math.Abs(d - prev);
                    if (dist < bestD) { bestD = dist; best = d; }
                }
                return best;
            }

            List<MotifNote> MakeBar(bool allowRest)
            {
                var cell = Cells[rng.Next(Cells.Length)];
                var bar = new List<MotifNote>();
                int pos = 0, tone = rng.Next(3);
                for (int i = 0; i < cell.Length; i++)
                {
                    bool strong = pos % 4 == 0;
                    int step = strong ? 0 : rng.Next(3) - 1;  // weak beats: a step up, down, or the chord tone
                    if (!strong && rng.NextDouble() < 0.4) tone = Math.Max(0, Math.Min(2, tone + (rng.Next(2) * 2 - 1)));
                    bool rest = allowRest && i == cell.Length - 1 && rng.NextDouble() < 0.3;
                    bar.Add(new MotifNote { Eighths = cell[i], ChordTone = rest ? -1 : tone, Step = step });
                    pos += cell[i];
                }
                return bar;
            }

            List<MotifNote> Vary(List<MotifNote> bar)
            {
                var v = new List<MotifNote>(bar);
                var last = v[v.Count - 1];
                last.ChordTone = (last.ChordTone + 1 + rng.Next(2)) % 3;
                last.Step = 0;
                v[v.Count - 1] = last;
                return v;
            }

            // Bar 8 rests on the fifth or third (a breath); bar 16 lands home on the root, so the loop resolves.
            List<MotifNote> Cadence(bool final)
            {
                return final
                    ? new List<MotifNote> { new MotifNote { Eighths = 2, ChordTone = 2 }, new MotifNote { Eighths = 2, ChordTone = 1 }, new MotifNote { Eighths = 4, ChordTone = 0 } }
                    : new List<MotifNote> { new MotifNote { Eighths = 3, ChordTone = 1 }, new MotifNote { Eighths = 1, ChordTone = 1, Step = 1 }, new MotifNote { Eighths = 2, ChordTone = 2 }, new MotifNote { Eighths = 2, ChordTone = -1 } };
            }

            // The counter-voice answers at the end of each phrase: three rising chord tones up high.
            void WriteSparkles()
            {
                for (int b = 3; b < s.Bars; b += 4)
                {
                    var chord = Triad(b);
                    for (int i = 0; i < 3; i++)
                        Note(Lead, Time(b, 2f + i * 0.5f), Hz(Deg(chord[i] + 14)), 1f, 0.05f, counter);
                }
            }

            // ---------- stingers ----------

            // Win: the tonic chord rolled up in the lead voice over a held pad, topped with a sparkle. In the song's key,
            // so it lands on the music instead of over it.
            public float[] WinStinger()
            {
                var data = new float[(int)(Rate * 2.2f)];
                int[] up = { 0, 2, 4, 7, 9, 11, 14 };
                for (int i = 0; i < up.Length; i++)
                    Note(data, 0.02f + i * 0.075f, Hz(Deg(up[i] + 7)), 0.6f, 0.17f, lead, false);
                foreach (var d in new[] { 0, 2, 4 })
                    Note(data, 0.45f, Hz(Deg(d)), 1.2f, 0.07f, PadVoice, false);
                Note(data, 0.5f, Hz(Deg(-7)), 1.3f, 0.22f, BassVoice, false);
                Note(data, 0.62f, Hz(Deg(21)), 1.2f, 0.06f, VoiceOf(Instrument.Glock), false);
                Note(data, 0.70f, Hz(Deg(23)), 1.2f, 0.05f, VoiceOf(Instrument.Glock), false);
                return Finish(data);
            }

            // Lose: a gentle "aw" falling by half steps to a low thud: sympathetic, never mocking.
            public float[] LoseStinger()
            {
                var data = new float[(int)(Rate * 1.8f)];
                var ocarina = VoiceOf(Instrument.Ocarina);
                int top = Deg(4) + 12;
                for (int i = 0; i < 3; i++)
                    Note(data, i * 0.26f, Hz(top - i), 0.22f, 0.15f, ocarina, false);
                var last = ocarina;
                last.Vibrato = 0.03f;
                Note(data, 0.8f, Hz(top - 3), 0.7f, 0.15f, last, false);
                Note(data, 0.8f, Hz(Deg(0) - 24), 0.9f, 0.2f, BassVoice, false);
                return Finish(data);
            }

            static float[] Finish(float[] data)
            {
                float peak = 0f;
                foreach (var v in data) peak = Math.Max(peak, Math.Abs(v));
                float gain = peak > 0f ? 0.8f / peak : 1f;
                int fade = Rate / 50;
                for (int i = 0; i < data.Length; i++)
                    data[i] *= gain * Math.Min(1f, (data.Length - i) / (float)fade);
                return data;
            }
        }

        public static int NoteName(string name)
        {
            int[] steps = { 9, 11, 0, 2, 4, 5, 7 };  // A B C D E F G
            int note = steps[name[0] - 'A'];
            int i = 1;
            if (name[i] == '#') { note++; i++; }
            else if (name[i] == 'b') { note--; i++; }
            int octave = int.Parse(name.Substring(i));
            return 12 * (octave + 1) + note;
        }
    }
}
