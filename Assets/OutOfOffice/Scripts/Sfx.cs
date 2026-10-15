using System;
using UnityEngine;

namespace OutOfOffice
{
    // Procedural sound set, so the game ships with no audio files. Generated once at startup.
    // Loops are built to repeat seamlessly; one-shots fade out to avoid clicks.
    public static class Sfx
    {
        const int Rate = 44100;
        const float TwoPi = Mathf.PI * 2f;

        public static AudioClip Select, Cancel, UiTap, Undo, Restart;
        public static AudioClip[] Launch;       // weak, medium, strong
        public static AudioClip[] Wood, Rubber; // variants
        public static AudioClip Soft, Crate, RimTouch, IceEnter, MudEnter;
        public static AudioClip HoleDrop, HoleLand, Reward, Complete;
        public static AudioClip[] StarNotes;    // ascending scale for consecutive pickups
        public static AudioClip Switch, GateMove, PortalIn, PortalOut, RampLaunch, Land, Hazard, Fall, Boost;
        // The mascot's little voice: tiny pitched chirps, never words.
        public static AudioClip Squeak, Whee, Yay, Giggle, Boing, Yawn, Oops, Crack, Pop;
        // Office palette: a pillow landing, a blanket swish, a paper rustle, a printer chirp.
        public static AudioClip Pillow, Swish, Paper, Chirp;
        public static AudioClip SlideWoodLoop, SlideIceLoop, RubLoop, ChargeLoop, MagnetLoop, MusicLoop;

        static System.Random rng;

        public static void Init()
        {
            if (Select != null) return;
            rng = new System.Random(11);

            Select = Make("select", 0.09f, t => Pluck(t, 880f, 40f) * 0.35f);
            Cancel = Make("cancel", 0.16f, t => Mathf.Sin(TwoPi * Sweep(t, 520f, 300f, 0.16f)) * Env(t, 0.005f, 18f) * 0.28f);
            // UI tap: a soft keyboard click.
            UiTap = Make("tap", 0.05f, t => Bandnoise(0.6f) * Mathf.Exp(-t * 260f) * 0.35f + Mathf.Sin(TwoPi * 2400f * t) * Mathf.Exp(-t * 320f) * 0.06f);
            Undo = Make("undo", 0.26f, t => Bandnoise(0.3f) * Mathf.Sin(Mathf.PI * t / 0.26f) * 0.45f + Mathf.Sin(TwoPi * Sweep(t, 300f, 600f, 0.26f)) * Env(t, 0.02f, 9f) * 0.12f);
            Restart = Make("restart", 0.3f, t => Bandnoise(0.25f) * Mathf.Sin(Mathf.PI * t / 0.3f) * 0.4f + Mathf.Sin(TwoPi * Sweep(t, 700f, 250f, 0.3f)) * Env(t, 0.01f, 8f) * 0.15f);

            // Launch: a soft, round "fwoop", like a cushion being let go. Low and short, so it never grates.
            Launch = new AudioClip[3];
            for (int i = 0; i < 3; i++)
            {
                float k = (i + 1) / 3f;
                float length = 0.18f + 0.06f * k;
                Launch[i] = Make("launch" + i, length, t =>
                    Mathf.Sin(TwoPi * Sweep(t, 150f + 30f * k, 330f + 90f * k, length)) * Swell(t, 0.02f, length) * 0.42f
                    + Lowpass(Noise(), 0.05f) * Swell(t, 0.03f, length) * (0.25f + 0.25f * k));
            }

            // Wood knocks: a few damped partials and a short click. Variants differ in pitch.
            Wood = new AudioClip[3];
            float[] woodBase = { 380f, 430f, 470f };
            for (int i = 0; i < 3; i++)
            {
                float f = woodBase[i];
                Wood[i] = Make("wood" + i, 0.16f, t =>
                    (Mathf.Sin(TwoPi * f * t) * Mathf.Exp(-t * 45f) + 0.5f * Mathf.Sin(TwoPi * f * 2.3f * t) * Mathf.Exp(-t * 70f)
                     + 0.25f * Mathf.Sin(TwoPi * f * 4.1f * t) * Mathf.Exp(-t * 110f)) * 0.45f
                    + Noise() * Mathf.Exp(-t * 500f) * 0.3f);
            }

            // Rubber: a soft round thump with a little wobble.
            Rubber = new AudioClip[2];
            for (int i = 0; i < 2; i++)
            {
                float f = 170f + 25f * i;
                Rubber[i] = Make("rubber" + i, 0.3f, t =>
                    Mathf.Sin(TwoPi * Sweep(t, f * 1.6f, f, 0.12f)) * (1f + 0.25f * Mathf.Sin(TwoPi * 22f * t)) * Env(t, 0.002f, 14f) * 0.6f);
            }

            Soft = Make("soft", 0.25f, t => (Lowpass(Noise(), 0.06f) * 2.5f + Mathf.Sin(TwoPi * 95f * t)) * Env(t, 0.003f, 18f) * 0.45f);
            Crate = Make("crate", 0.25f, t =>
                (Mathf.Sin(TwoPi * 210f * t) * Mathf.Exp(-t * 28f) + 0.6f * Mathf.Sin(TwoPi * 520f * t) * Mathf.Exp(-t * 50f)) * 0.5f
                + Noise() * Mathf.Exp(-t * 300f) * 0.35f);
            RimTouch = Make("rim", 0.3f, t =>
                (Mathf.Sin(TwoPi * 2350f * t) * Mathf.Exp(-t * 25f) + 0.6f * Mathf.Sin(TwoPi * 3720f * t) * Mathf.Exp(-t * 35f)) * 0.18f);
            IceEnter = Make("ice", 0.35f, t => (Mathf.Sin(TwoPi * 1760f * t) + 0.5f * Mathf.Sin(TwoPi * 2640f * t)) * Env(t, 0.003f, 12f) * 0.12f);
            MudEnter = Make("mud", 0.28f, t => (Lowpass(Noise(), 0.05f) * 3f + Mathf.Sin(TwoPi * Sweep(t, 160f, 80f, 0.28f))) * Env(t, 0.01f, 11f) * 0.4f);

            // Hole capture: a soft descending tone, a rounded landing, then a little musical reward.
            HoleDrop = Make("drop", 0.4f, t =>
                Mathf.Sin(TwoPi * Sweep(t, 620f, 210f, 0.4f)) * Env(t, 0.01f, 6f) * 0.3f + Bandnoise(0.2f) * Env(t, 0.005f, 14f) * 0.1f);
            HoleLand = Make("land_hole", 0.45f, t =>
                (Mathf.Sin(TwoPi * 105f * t) * Mathf.Exp(-t * 11f) + 0.6f * Mathf.Sin(TwoPi * 68f * t) * Mathf.Exp(-t * 8f)
                 + 0.3f * Mathf.Sin(TwoPi * 310f * t) * Mathf.Exp(-t * 30f)) * 0.6f);
            float[] reward = { 1046.5f, 1318.5f, 1568f, 2093f };
            Reward = Make("reward", 0.75f, t => Arpeggio(t, reward, 0.075f, 6f) * 0.3f);
            float[] complete = { 523.25f, 659.25f, 783.99f, 1046.5f, 1318.5f };
            Complete = Make("complete", 1.4f, t => Arpeggio(t, complete, 0.09f, 3f) * 0.28f + Chord(t, complete, 0.45f) * 0.12f);

            // Coffee pickup: a ceramic clink plus a soft sip, rising a step per cup so a run sounds pleased with itself.
            float[] scale = { 1046.5f, 1174.7f, 1318.5f, 1568f, 1760f, 2093f, 2349.3f, 2637f };
            StarNotes = new AudioClip[scale.Length];
            for (int i = 0; i < scale.Length; i++)
            {
                float f = scale[i];
                StarNotes[i] = Make("coffee" + i, 0.4f, t =>
                    (Mathf.Sin(TwoPi * f * t) + 0.4f * Mathf.Sin(TwoPi * f * 2.71f * t) * Mathf.Exp(-t * 18f)) * Env(t, 0.001f, 9f) * 0.22f
                    + Lowpass(Noise(), 0.12f) * Swell(Mathf.Max(0f, t - 0.06f), 0.04f, 0.18f) * 0.35f);
            }
            Pillow = Make("pillow", 0.32f, t => (Lowpass(Noise(), 0.04f) * 2.4f + Mathf.Sin(TwoPi * Sweep(t, 120f, 70f, 0.2f)) * 0.6f) * Env(t, 0.006f, 12f) * 0.5f);
            Swish = Make("swish", 0.6f, t => Bandnoise(0.12f + 0.25f * Mathf.Clamp01(t / 0.6f)) * Swell(t, 0.25f, 0.6f) * 0.45f);
            Paper = Make("paper", 0.25f, t => (Noise() - Lowpass(Noise(), 0.3f)) * Swell(t, 0.02f, 0.25f) * 0.25f);
            Chirp = Make("chirp", 0.22f, t => (Note(t, 0f, 0.09f, 1500f, 1560f) + Note(t, 0.1f, 0.1f, 1980f, 2000f)) * 1.2f);

            Switch = Make("switch", 0.4f, t =>
                Noise() * Mathf.Exp(-t * 400f) * 0.3f + Bell(Mathf.Max(0f, t - 0.04f), 880f) * 0.2f + Bell(Mathf.Max(0f, t - 0.12f), 1318.5f) * 0.22f);
            GateMove = Make("gate", 0.6f, t =>
                (Lowpass(Noise(), 0.1f) * 1.6f + Mathf.Sin(TwoPi * Sweep(t, 120f, 70f, 0.6f)) * 0.5f) * Mathf.Sin(Mathf.PI * t / 0.6f) * 0.35f);
            PortalIn = Make("portal_in", 0.35f, t =>
                Mathf.Sin(TwoPi * Sweep(t, 900f, 260f, 0.35f)) * Env(t, 0.01f, 7f) * 0.22f + Bandnoise(0.35f) * Env(t, 0.01f, 9f) * 0.15f);
            PortalOut = Make("portal_out", 0.35f, t =>
                Mathf.Sin(TwoPi * Sweep(t, 260f, 980f, 0.35f)) * Env(t, 0.01f, 7f) * 0.22f + Bandnoise(0.35f) * Env(t, 0.01f, 9f) * 0.15f);
            RampLaunch = Make("ramp", 0.35f, t => Mathf.Sin(TwoPi * Sweep(t, 260f, 760f, 0.35f)) * Env(t, 0.01f, 8f) * 0.28f + Bandnoise(0.3f) * Env(t, 0.01f, 10f) * 0.18f);
            Land = Make("land", 0.3f, t =>
                Mathf.Sin(TwoPi * Sweep(t, 150f, 85f, 0.12f)) * Env(t, 0.001f, 16f) * 0.75f + Lowpass(Noise(), 0.15f) * Env(t, 0.001f, 30f) * 0.6f);
            Hazard = Make("hazard", 0.45f, t =>
                Mathf.Sin(TwoPi * Sweep(t, 420f, 160f, 0.45f)) * Env(t, 0.003f, 6f) * 0.3f + Noise() * Mathf.Exp(-t * 200f) * 0.25f);
            Fall = Make("fall", 0.7f, t => Mathf.Sin(TwoPi * Sweep(t, 1100f, 300f, 0.7f)) * Env(t, 0.02f, 3.5f) * 0.18f);
            Boost = Make("boost", 0.45f, t =>
                Mathf.Sin(TwoPi * Sweep(t, 250f, 1100f, 0.45f)) * Mathf.Exp(-t * 4f) * 0.2f + Bandnoise(0.4f) * Mathf.Exp(-t * 10f) * 0.12f);

            // Mascot voice: a soft, round "oo" tone (an ocarina more than a buzzer) with smooth swells and gentle
            // pitch glides. Quiet and short, so it never nags.
            Squeak = Make("squeak", 0.2f, t =>
                Voice(t, Glide(t, 0.2f, 900f, 1240f, 1020f)) * Swell(t, 0.02f, 0.2f) * 0.2f);
            Whee = Make("whee", 0.6f, t =>
                Voice(t, Mathf.Lerp(560f, 1120f, Mathf.SmoothStep(0f, 1f, t / 0.5f)) * Vibrato(t, 5.5f, 0.025f, 0.15f)) * Swell(t, 0.05f, 0.6f) * 0.18f);
            Yay = Make("yay", 0.6f, t =>
                Note(t, 0f, 0.15f, 760f, 800f) * 0.9f + Note(t, 0.16f, 0.44f, 1010f, 1060f, true));
            Giggle = Make("giggle", 0.48f, t =>
            {
                float[] f = { 1175f, 1047f, 1175f, 1319f };
                float sum = 0f;
                for (int i = 0; i < f.Length; i++) sum += Note(t, i * 0.1f, 0.085f, f[i] * 0.94f, f[i]) * (1f - i * 0.12f);
                return sum * 0.8f;
            });
            Boing = Make("boing", 0.55f, t =>
                Voice(t, (430f + 170f * Mathf.Exp(-t * 14f)) * (1f + 0.07f * Mathf.Sin(TwoPi * 8f * t) * Mathf.Exp(-t * 5f))) * Swell(t, 0.01f, 0.55f) * Mathf.Exp(-t * 2.5f) * 0.22f);
            Yawn = Make("yawn", 1f, t =>
                (Voice(t, Glide(t, 1f, 640f, 700f, 420f)) * 0.8f + Lowpass(Noise(), 0.08f) * 0.6f) * Swell(t, 0.25f, 1f) * 0.13f);
            Oops = Make("oops", 0.42f, t =>
                Note(t, 0f, 0.14f, 880f, 900f) + Note(t, 0.17f, 0.25f, 700f, 660f) * 0.9f);
            Crack = Make("crack", 0.5f, t =>
                Noise() * Mathf.Exp(-t * 60f) * 0.5f
                + (Mathf.Sin(TwoPi * 2630f * t) * Mathf.Exp(-t * 18f) + 0.7f * Mathf.Sin(TwoPi * 3950f * t) * Mathf.Exp(-t * 24f)
                   + 0.5f * Mathf.Sin(TwoPi * 5170f * t) * Mathf.Exp(-t * 30f)) * 0.14f);
            Pop = Make("pop", 0.12f, t => Mathf.Sin(TwoPi * Sweep(t, 500f, 1400f, 0.05f)) * Env(t, 0.001f, 40f) * 0.35f);

            // Seamless loops. Their volume and pitch are driven every frame by speed and contact.
            SlideWoodLoop = Loop("slide_wood", 2f, t => Lowpass(Noise(), 0.07f) * (0.8f + 0.2f * Mathf.Sin(TwoPi * 13f * t)) * 1.6f);
            SlideIceLoop = Loop("slide_ice", 2f, t => (Noise() - Lowpass(Noise(), 0.2f)) * 0.35f + Mathf.Sin(TwoPi * 3520f * t) * 0.02f);
            RubLoop = Loop("rub", 2f, t => Lowpass(Noise(), 0.04f) * (0.7f + 0.3f * Mathf.Sin(TwoPi * 31f * t)) * 2.2f);
            // Aiming: a faint, soft stretching sound (filtered air), with no tone to hum.
            ChargeLoop = Loop("charge", 1f, t => Lowpass(Noise(), 0.03f) * (0.8f + 0.2f * Mathf.Sin(TwoPi * 3f * t)) * 1.2f);
            MagnetLoop = Loop("magnet", 1f, t => (Mathf.Sin(TwoPi * 110f * t) + 0.4f * Mathf.Sin(TwoPi * 220f * t)) * (0.7f + 0.3f * Mathf.Sin(TwoPi * 8f * t)) * 0.25f);
            MusicLoop = MakeMusic();
        }

        // Background music: a bouncy 16-bar loop at 104 bpm. Kalimba melody, soft bass, ukulele-like offbeat plucks,
        // a light kick/snap/shaker groove, and a few glockenspiel sparkles. Notes that ring past the end wrap to the
        // start, so the loop is seamless.
        static AudioClip MakeMusic()
        {
            const float bpm = 104f;
            float beat = 60f / bpm;
            const int bars = 16;
            int n = Mathf.RoundToInt(Rate * bars * 4 * beat);
            var data = new float[n];
            var noise = new System.Random(5);

            // Chord voicings (MIDI) and bass roots per bar: C Am F G | C Am F G | F G Em Am | F G C C
            int[] C = { 60, 64, 67 }, Am = { 57, 60, 64 }, F = { 57, 60, 65 }, G = { 55, 59, 62 }, Em = { 55, 59, 64 };
            int[][] chords = { C, Am, F, G, C, Am, F, G, F, G, Em, Am, F, G, C, C };
            int[] roots = { 48, 45, 41, 43, 48, 45, 41, 43, 41, 43, 40, 45, 41, 43, 48, 48 };
            string[] melody =
            {
                "E5:2 G5:2 A5:1 G5:1 E5:2", "C5:2 E5:2 D5:1 C5:1 A4:2", "C5:2 D5:2 E5:1 F5:1 A5:2", "G5:3 F5:1 E5:2 D5:2",
                "E5:2 G5:2 C6:1 B5:1 G5:2", "A5:2 G5:1 E5:1 C5:2 E5:2", "F5:1 E5:1 D5:1 C5:1 A4:2 C5:2", "D5:3 B4:1 G4:2 R:2",
                "A5:3 G5:1 F5:2 A5:2", "G5:3 F5:1 E5:2 D5:2", "E5:2 G5:2 B5:2 G5:2", "A5:4 E5:2 R:2",
                "F5:2 A5:2 C6:2 A5:2", "G5:2 B5:2 D6:2 B5:1 A5:1", "C6:4 G5:2 E5:2", "C5:3 D5:1 E5:2 G4:2",
            };

            for (int bar = 0; bar < bars; bar++)
            {
                float t0 = bar * 4 * beat;
                // Melody, in eighths.
                float at = 0f;
                foreach (var token in melody[bar].Split(' '))
                {
                    var parts = token.Split(':');
                    int eighths = int.Parse(parts[1]);
                    if (parts[0] != "R")
                        Partials(data, t0 + at * beat * 0.5f, Hz(Midi(parts[0])), 1.4f, 0.15f, 0.003f,
                            (1f, 1f, 3.2f), (2f, 0.3f, 9f), (5.4f, 0.12f, 30f));
                    at += eighths;
                }
                // Bass: root, a little bounce, fifth, octave.
                float root = Hz(roots[bar]);
                Partials(data, t0, root, 1.3f, 0.2f, 0.006f, (1f, 1f, 2.8f), (2f, 0.45f, 4f), (3f, 0.15f, 6f));
                Partials(data, t0 + 1.5f * beat, root, 0.45f, 0.13f, 0.006f, (1f, 1f, 5f), (2f, 0.45f, 7f));
                Partials(data, t0 + 2f * beat, root * 1.5f, 0.9f, 0.16f, 0.006f, (1f, 1f, 3.5f), (2f, 0.4f, 5f));
                Partials(data, t0 + 3f * beat, root * 2f, 0.9f, 0.13f, 0.006f, (1f, 1f, 3.5f), (2f, 0.35f, 5f));
                // Offbeat plucks of the chord, slightly strummed.
                for (int k = 0; k < 4; k++)
                {
                    float on = t0 + (k + 0.5f) * beat;
                    for (int v = 0; v < 3; v++)
                        Partials(data, on + v * 0.012f, Hz(chords[bar][v]), 0.5f, 0.045f, 0.002f, (1f, 1f, 9f), (2f, 0.4f, 14f), (3f, 0.15f, 20f));
                }
                // Groove: kick on 1 and 3, snap on 2 and 4, shaker on eighths.
                for (int k = 0; k < 4; k++)
                {
                    float on = t0 + k * beat;
                    if (k % 2 == 0) Drum(data, on, 0.25f, 0.3f, tt => Mathf.Sin(TwoPi * Sweep(tt, 150f, 55f, 0.08f)) * Mathf.Exp(-tt * 14f));
                    else
                    {
                        float bl = 0f, bh = 0f;
                        Drum(data, on, 0.18f, 0.11f, tt =>
                        {
                            float x = (float)noise.NextDouble() * 2f - 1f;
                            bl += (x - bl) * 0.45f;
                            bh += (bl - bh) * 0.12f;
                            return ((bl - bh) * 1.6f + 0.3f * Mathf.Sin(TwoPi * 1750f * tt)) * Mathf.Exp(-tt * 30f);
                        });
                    }
                    for (int h = 0; h < 2; h++)
                    {
                        float lpf = 0f;
                        Drum(data, on + h * beat * 0.5f, 0.07f, h == 1 ? 0.045f : 0.028f, tt =>
                        {
                            float x = (float)noise.NextDouble() * 2f - 1f;
                            lpf += (x - lpf) * 0.3f;
                            return (x - lpf) * Mathf.Exp(-tt * 55f);
                        });
                    }
                }
            }
            // Glockenspiel answers where the melody rests.
            foreach (var (bar, notes) in new[] { (7, new[] { 79, 83, 86 }), (11, new[] { 76, 81, 84 }), (15, new[] { 79, 84, 88 }) })
                for (int i = 0; i < notes.Length; i++)
                    Partials(data, bar * 4 * beat + (2f + i * 0.5f) * beat, Hz(notes[i]), 1f, 0.05f, 0.002f, (1f, 1f, 4f), (2.76f, 0.3f, 9f));

            float peak = 0f;
            foreach (var v in data) peak = Mathf.Max(peak, Mathf.Abs(v));
            float gain = peak > 0f ? 0.8f / peak : 1f;
            for (int i = 0; i < n; i++) data[i] *= gain;
            var clip = AudioClip.Create("music", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static readonly float[] sine = BuildSine();

        static float[] BuildSine()
        {
            var table = new float[4097];
            for (int i = 0; i <= 4096; i++) table[i] = Mathf.Sin(TwoPi * i / 4096f);
            return table;
        }

        static float FastSin(double cycles)
        {
            double f = cycles - System.Math.Floor(cycles);
            float x = (float)(f * 4096.0);
            int i = (int)x;
            return sine[i] + (sine[i + 1] - sine[i]) * (x - i);
        }

        static float Hz(int midi) => 440f * Mathf.Pow(2f, (midi - 69) / 12f);

        static int Midi(string name)
        {
            int[] steps = { 9, 11, 0, 2, 4, 5, 7 };  // A B C D E F G
            int note = steps[name[0] - 'A'];
            int i = 1;
            if (name[i] == '#') { note++; i++; }
            else if (name[i] == 'b') { note--; i++; }
            int octave = int.Parse(name.Substring(i));
            return 12 * (octave + 1) + note;
        }

        // Adds a note made of decaying partials (frequency ratio, amplitude, decay per second), wrapping past the end.
        static void Partials(float[] data, float start, float f, float seconds, float gain, float attack, params (float ratio, float amp, float decay)[] partials)
        {
            int s0 = Mathf.RoundToInt(start * Rate), len = Mathf.RoundToInt(seconds * Rate), n = data.Length;
            int attackN = Mathf.Max(1, Mathf.RoundToInt(attack * Rate)), fadeN = Rate / 50;
            foreach (var (ratio, amp, decay) in partials)
            {
                double step = f * ratio / Rate;
                float env = amp * gain, k = Mathf.Exp(-decay / Rate);
                for (int i = 0; i < len; i++)
                {
                    float shape = Mathf.Min(1f, i / (float)attackN) * Mathf.Min(1f, (len - i) / (float)fadeN);
                    data[(s0 + i) % n] += FastSin(step * i) * env * shape;
                    env *= k;
                }
            }
        }

        static void Drum(float[] data, float start, float seconds, float gain, Func<float, float> wave)
        {
            int s0 = Mathf.RoundToInt(start * Rate), len = Mathf.RoundToInt(seconds * Rate);
            for (int i = 0; i < len; i++) data[(s0 + i) % data.Length] += wave(i / (float)Rate) * gain * Mathf.Min(1f, (len - i) / 200f);
        }

        // ---------- building blocks ----------

        static float Noise() => (float)rng.NextDouble() * 2f - 1f;

        static float lp;
        static float Lowpass(float x, float k) => lp += (x - lp) * k;

        static float bpLow, bpHigh;
        static float Bandnoise(float k)
        {
            float x = Noise();
            bpLow += (x - bpLow) * k;
            bpHigh += (bpLow - bpHigh) * k * 0.5f;
            return (bpLow - bpHigh) * 2f;
        }

        static float Env(float t, float attack, float decay) => Mathf.Min(1f, t / attack) * Mathf.Exp(-t * decay);
        static float Pluck(float t, float f, float decay) => Mathf.Sin(TwoPi * f * t) * Env(t, 0.002f, decay);

        // Phase of a linear frequency sweep, in cycles.
        static float Sweep(float t, float from, float to, float over)
        {
            float k = Mathf.Clamp01(t / over);
            return from * t + (to - from) * k * t * 0.5f;
        }

        static float Bell(float t, float f) =>
            (Mathf.Sin(TwoPi * f * t) + 0.35f * Mathf.Sin(TwoPi * f * 2.76f * t) * Mathf.Exp(-t * 6f)) * Env(t, 0.002f, 5f);

        static float Arpeggio(float t, float[] notes, float step, float decay)
        {
            float sum = 0f;
            for (int i = 0; i < notes.Length; i++)
            {
                float lt = t - i * step;
                if (lt > 0f) sum += Bell(lt, notes[i]) * Mathf.Exp(-lt * (decay - 5f + 1f)) * 0.8f;
            }
            return sum;
        }

        static float Chord(float t, float[] notes, float delay)
        {
            float lt = t - delay;
            if (lt <= 0f) return 0f;
            float sum = 0f;
            foreach (var f in notes) sum += Mathf.Sin(TwoPi * f * 0.5f * lt);
            return sum / notes.Length * Mathf.Min(1f, lt * 20f) * Mathf.Exp(-lt * 2.2f);
        }

        static float voicePhase;

        // A soft vowel-like tone: mostly the fundamental, a little warmth on top. The phase follows the changing pitch.
        static float Voice(float t, float freq)
        {
            if (t <= 0f) voicePhase = 0f;
            voicePhase += freq / Rate;
            float p = TwoPi * voicePhase;
            return (Mathf.Sin(p) + 0.18f * Mathf.Sin(2f * p) + 0.05f * Mathf.Sin(3f * p)) / 1.15f;
        }

        // Smooth swell: a cosine rise over `attack`, then a cosine fall to silence at `length`.
        static float Swell(float t, float attack, float length)
        {
            if (t <= 0f || t >= length) return 0f;
            if (t < attack) return 0.5f - 0.5f * Mathf.Cos(Mathf.PI * t / attack);
            return 0.5f + 0.5f * Mathf.Cos(Mathf.PI * (t - attack) / (length - attack));
        }

        // Pitch that rises from `from` to `peak` at the middle, then settles to `to`.
        static float Glide(float t, float length, float from, float peak, float to)
        {
            float k = Mathf.Clamp01(t / length);
            return k < 0.5f ? Mathf.Lerp(from, peak, Mathf.SmoothStep(0f, 1f, k * 2f)) : Mathf.Lerp(peak, to, Mathf.SmoothStep(0f, 1f, k * 2f - 1f));
        }

        static float Vibrato(float t, float rate, float depth, float delay) =>
            1f + depth * Mathf.Clamp01((t - delay) * 5f) * Mathf.Sin(TwoPi * rate * t);

        // One sung syllable inside a longer clip: its own phase, a small scoop in pitch, a smooth swell.
        static float Note(float t, float start, float length, float from, float to, bool vibrato = false)
        {
            float lt = t - start;
            if (lt <= 0f || lt >= length) return 0f;
            // Phase in closed form: the pitch slides exponentially from `from` to `to`, plus an optional vibrato.
            float tau = length * 0.15f;
            float cycles = to * lt + (from - to) * tau * (1f - Mathf.Exp(-lt / tau));
            if (vibrato) cycles += to * 0.02f * (1f - Mathf.Cos(TwoPi * 6f * lt)) / (TwoPi * 6f);
            float p = TwoPi * cycles;
            float v = (Mathf.Sin(p) + 0.18f * Mathf.Sin(2f * p) + 0.05f * Mathf.Sin(3f * p)) / 1.15f;
            return v * Swell(lt, Mathf.Min(0.025f, length * 0.25f), length) * 0.2f;
        }

        static AudioClip Make(string name, float seconds, Func<float, float> wave)
        {
            int n = Mathf.CeilToInt(Rate * seconds);
            var data = new float[n];
            lp = bpLow = bpHigh = 0f;
            for (int i = 0; i < n; i++)
            {
                float fadeOut = Mathf.Clamp01((n - i) / (Rate * 0.012f));
                data[i] = Mathf.Clamp(wave(i / (float)Rate) * fadeOut, -1f, 1f);
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // Crossfades the tail into the head, so the loop point is inaudible.
        static AudioClip Loop(string name, float seconds, Func<float, float> wave)
        {
            int n = Mathf.CeilToInt(Rate * seconds), fade = Rate / 10;
            var raw = new float[n + fade];
            lp = bpLow = bpHigh = 0f;
            for (int i = 0; i < raw.Length; i++) raw[i] = wave(i / (float)Rate);
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float v = raw[i];
                if (i < fade)
                {
                    float k = i / (float)fade;
                    v = raw[i] * k + raw[n + i] * (1f - k);
                }
                data[i] = Mathf.Clamp(v, -1f, 1f);
            }
            var clip = AudioClip.Create(name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
