using System;
using UnityEngine;

namespace OutOfOffice
{
    // Procedural sound set, so the game ships with no audio files. Generated once at startup. The music is the
    // Composer's (OUT-17), rendered off the main thread per chapter.
    // Loops are built to repeat seamlessly; one-shots fade out to avoid clicks.
    public static class Sfx
    {
        const int Rate = 44100;
        const float TwoPi = Mathf.PI * 2f;

        public static AudioClip Select, Cancel, UiTap, UiBoing, Undo, Restart;
        public static AudioClip[] Launch;       // weak, medium, strong
        public static AudioClip[] Wood, Rubber; // variants
        public static AudioClip Soft, Crate, RimTouch, IceEnter, MudEnter;
        public static AudioClip HoleDrop, HoleLand, Reward, Complete;
        public static AudioClip[] StarNotes;    // ascending scale for consecutive pickups
        public static AudioClip Switch, GateMove, PortalIn, PortalOut, RampLaunch, Land, Hazard, Fall, Boost;
        // The mascot's little voice: tiny pitched chirps, never words.
        public static AudioClip Squeak, Whee, Yay, Giggle, Boing, Yawn, Oops, Crack, Pop;
        // Office palette: a pillow landing, a blanket swish, a paper rustle, a printer chirp.
        public static AudioClip Pillow, Swish, Paper, Chirp, Ring;
        public static AudioClip SlideWoodLoop, SlideIceLoop, RubLoop, ChargeLoop, MagnetLoop;
        // OUT-17: variations of the busiest sounds, so no two knocks in a row are the same recording.
        public static AudioClip[] Crates, Softs, Lands, Taps, Metal, Glass;
        // OUT-17: moments. A fanfare per chest rarity, the store, and the shot's big finishes.
        public static AudioClip[] Fanfare;      // by Rarity: common, rare, epic, legendary
        public static AudioClip KaChing, GemShimmer, Riser, Flourish;
        public static AudioClip[] CoinTicks;    // rising ticks for currency flying into the counter

        static System.Random rng;

        public static void Init()
        {
            if (Select != null) return;
            rng = new System.Random(11);

            Select = Make("select", 0.09f, t => Pluck(t, 880f, 40f) * 0.35f);
            Cancel = Make("cancel", 0.16f, t => Mathf.Sin(TwoPi * Sweep(t, 520f, 300f, 0.16f)) * Env(t, 0.005f, 18f) * 0.28f);
            // UI tap: a soft keyboard click.
            UiTap = Make("tap", 0.05f, t => Bandnoise(0.6f) * Mathf.Exp(-t * 260f) * 0.35f + Mathf.Sin(TwoPi * 2400f * t) * Mathf.Exp(-t * 320f) * 0.06f);
            // Button press (OUT-40): a short, soft rubbery boing, quiet enough to hear on every tap.
            UiBoing = Make("uiboing", 0.2f, t =>
                Voice(t, (500f + 280f * Mathf.Exp(-t * 32f)) * (1f + 0.05f * Mathf.Sin(TwoPi * 15f * t) * Mathf.Exp(-t * 14f))) * Swell(t, 0.004f, 0.2f) * Mathf.Exp(-t * 10f) * 0.24f);
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
            Wood = new AudioClip[4];
            float[] woodBase = { 380f, 430f, 470f, 410f };
            for (int i = 0; i < Wood.Length; i++)
            {
                float f = woodBase[i];
                Wood[i] = Make("wood" + i, 0.16f, t =>
                    (Mathf.Sin(TwoPi * f * t) * Mathf.Exp(-t * 45f) + 0.5f * Mathf.Sin(TwoPi * f * 2.3f * t) * Mathf.Exp(-t * 70f)
                     + 0.25f * Mathf.Sin(TwoPi * f * 4.1f * t) * Mathf.Exp(-t * 110f)) * 0.45f
                    + Noise() * Mathf.Exp(-t * 500f) * 0.3f);
            }

            // Rubber: a soft round thump with a little wobble.
            Rubber = new AudioClip[4];
            for (int i = 0; i < Rubber.Length; i++)
            {
                float f = 170f + 18f * i;
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
            // The reception desk phone (OUT-38): two short, polite trills.
            Ring = Make("ring", 0.62f, t =>
            {
                float burst = t < 0.24f ? Swell(t, 0.01f, 0.24f) : t > 0.34f ? Swell(t - 0.34f, 0.01f, 0.24f) : 0f;
                return (Mathf.Sin(TwoPi * 1320f * t) + 0.6f * Mathf.Sin(TwoPi * 1650f * t)) * (0.6f + 0.4f * Mathf.Sign(Mathf.Sin(TwoPi * 22f * t))) * burst * 0.12f;
            });

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
            MakeVariants();
            MakeMoments();
        }

        // Three takes of each busy sound: the same gesture at slightly different sizes, picked in turn by GameAudio.
        static void MakeVariants()
        {
            Crates = new AudioClip[3];
            Softs = new AudioClip[3];
            Lands = new AudioClip[3];
            Taps = new AudioClip[3];
            Metal = new AudioClip[3];
            Glass = new AudioClip[3];
            for (int i = 0; i < 3; i++)
            {
                float k = 1f + 0.06f * (i - 1);
                Crates[i] = i == 1 ? Crate : Make("crate" + i, 0.25f, t =>
                    (Mathf.Sin(TwoPi * 210f * k * t) * Mathf.Exp(-t * 28f) + 0.6f * Mathf.Sin(TwoPi * 520f * k * t) * Mathf.Exp(-t * 50f)) * 0.5f
                    + Noise() * Mathf.Exp(-t * 300f) * 0.35f);
                Softs[i] = i == 1 ? Soft : Make("soft" + i, 0.25f, t => (Lowpass(Noise(), 0.06f * k) * 2.5f + Mathf.Sin(TwoPi * 95f * k * t)) * Env(t, 0.003f, 18f) * 0.45f);
                Lands[i] = i == 1 ? Land : Make("land" + i, 0.3f, t =>
                    Mathf.Sin(TwoPi * Sweep(t, 150f * k, 85f * k, 0.12f)) * Env(t, 0.001f, 16f) * 0.75f + Lowpass(Noise(), 0.15f) * Env(t, 0.001f, 30f) * 0.6f);
                Taps[i] = i == 1 ? UiTap : Make("tap" + i, 0.05f, t => Bandnoise(0.6f) * Mathf.Exp(-t * 260f) * 0.35f + Mathf.Sin(TwoPi * 2400f * k * t) * Mathf.Exp(-t * 320f) * 0.06f);
                // A metal gate or rail: a clank of inharmonic partials over a short click.
                float m = 520f + 45f * i;
                Metal[i] = Make("metal" + i, 0.45f, t =>
                    (Mathf.Sin(TwoPi * m * t) * Mathf.Exp(-t * 18f) + 0.55f * Mathf.Sin(TwoPi * m * 2.76f * t) * Mathf.Exp(-t * 26f)
                     + 0.3f * Mathf.Sin(TwoPi * m * 5.4f * t) * Mathf.Exp(-t * 40f) + 0.15f * Mathf.Sin(TwoPi * m * 8.93f * t) * Mathf.Exp(-t * 60f)) * 0.3f
                    + Noise() * Mathf.Exp(-t * 450f) * 0.3f);
                // Glass and crystal: high, clear partials that ring a moment.
                float g = 1900f + 160f * i;
                Glass[i] = Make("glass" + i, 0.5f, t =>
                    (Mathf.Sin(TwoPi * g * t) * Mathf.Exp(-t * 10f) + 0.5f * Mathf.Sin(TwoPi * g * 2.32f * t) * Mathf.Exp(-t * 14f)
                     + 0.25f * Mathf.Sin(TwoPi * g * 4.25f * t) * Mathf.Exp(-t * 22f)) * 0.16f
                    + Noise() * Mathf.Exp(-t * 600f) * 0.15f);
            }
        }

        static void MakeMoments()
        {
            // Chest fanfares rise with rarity: a chime, a run, a cascade, then brass and a drum roll for a legend.
            float[] cMaj = { 523.25f, 659.25f, 783.99f, 1046.5f, 1318.5f, 1568f, 2093f, 2637f };
            Fanfare = new AudioClip[4];
            Fanfare[0] = Make("fanfare_common", 0.8f, t => Arpeggio(t, new[] { cMaj[0], cMaj[1], cMaj[2] }, 0.07f, 6f) * 0.3f);
            Fanfare[1] = Make("fanfare_rare", 1.2f, t =>
                Arpeggio(t, new[] { cMaj[0], cMaj[1], cMaj[2], cMaj[3] }, 0.07f, 5f) * 0.3f + Chord(t, new[] { cMaj[0], cMaj[1], cMaj[2] }, 0.3f) * 0.12f
                + Bell(Mathf.Max(0f, t - 0.38f), 2093f) * 0.1f);
            Fanfare[2] = Make("fanfare_epic", 1.8f, t =>
                Arpeggio(t, cMaj, 0.055f, 4.5f) * 0.26f + Chord(t, new[] { 261.63f, 329.63f, 392f, 523.25f }, 0.42f) * 0.2f
                + Sparkle(t - 0.5f, 6, 2400f) * 0.08f);
            Fanfare[3] = Make("fanfare_legendary", 2.8f, t =>
            {
                // "Da-da-da DAAA" on G G G C, a third on top of the long note, timpani under the pickups.
                float brass = Brass(t, 0f, 0.12f, 392f) + Brass(t, 0.15f, 0.12f, 392f) + Brass(t, 0.3f, 0.12f, 392f)
                    + Brass(t, 0.45f, 1.5f, 523.25f) + 0.7f * Brass(t, 0.45f, 1.5f, 659.25f) + 0.5f * Brass(t, 0.45f, 1.5f, 392f);
                float roll = t < 0.5f ? Lowpass(Noise(), 0.03f) * 2f * (0.6f + 0.4f * Mathf.Sin(TwoPi * 22f * t)) * (t / 0.5f) : 0f;
                float boom = t > 0.45f ? Mathf.Sin(TwoPi * 98f * (t - 0.45f)) * Mathf.Exp(-(t - 0.45f) * 5f) * 0.6f : 0f;
                return brass * 0.16f + roll * 0.3f + boom * 0.4f + Sparkle(t - 0.6f, 10, 2600f) * 0.08f;
            });

            // The store: a register "ka" and a bright "ching", with a few coins settling.
            KaChing = Make("kaching", 0.8f, t =>
            {
                float ka = Noise() * Mathf.Exp(-t * 180f) * 0.35f + Mathf.Sin(TwoPi * 180f * t) * Mathf.Exp(-t * 40f) * 0.3f;
                float lt = t - 0.06f;
                float ching = lt > 0f ? (Mathf.Sin(TwoPi * 2637f * lt) + 0.6f * Mathf.Sin(TwoPi * 3951f * lt) + 0.3f * Mathf.Sin(TwoPi * 5327f * lt))
                    * Mathf.Min(1f, lt * 400f) * Mathf.Exp(-lt * 5f) * 0.14f : 0f;
                float coins = 0f;
                for (int c = 0; c < 4; c++)
                {
                    float ct = t - 0.16f - c * 0.07f;
                    if (ct > 0f) coins += Mathf.Sin(TwoPi * (3200f + 380f * c) * ct) * Mathf.Exp(-ct * 70f) * 0.07f;
                }
                return ka + ching + coins;
            });
            CoinTicks = new AudioClip[4];
            for (int i = 0; i < CoinTicks.Length; i++)
            {
                float f = 2600f * Mathf.Pow(1.12f, i);
                CoinTicks[i] = Make("coin" + i, 0.07f, t => (Mathf.Sin(TwoPi * f * t) + 0.3f * Mathf.Sin(TwoPi * f * 2f * t)) * Env(t, 0.001f, 60f) * 0.18f);
            }
            GemShimmer = Make("gems", 1f, t => Sparkle(t, 7, 2300f) * 0.12f + Chord(t, new[] { 1174.7f, 1568f, 1864.7f }, 0.02f) * 0.06f);

            // Rolling up to the bed: air rising in pitch and level, cut off by the landing.
            Riser = Make("riser", 1.1f, t =>
            {
                float k = Mathf.Clamp01(t / 1.1f);
                return (Bandnoise(0.05f + 0.4f * k) * 0.6f + Mathf.Sin(TwoPi * Sweep(t, 300f, 1300f, 1.1f)) * 0.12f) * k * k * 0.5f;
            });
            // A hole in one: a glissando up two octaves of the scale and a bright chord.
            float[] gliss = { 523.25f, 587.33f, 659.25f, 698.46f, 783.99f, 880f, 987.77f, 1046.5f, 1174.7f, 1318.5f, 1396.9f, 1568f, 1760f, 1975.5f, 2093f };
            Flourish = Make("flourish", 1.5f, t =>
                Arpeggio(t, gliss, 0.032f, 7f) * 0.2f + Chord(t, new[] { 1046.5f, 1318.5f, 1568f }, 0.5f) * 0.14f + Bell(Mathf.Max(0f, t - 0.52f), 2093f) * 0.12f);
        }

        // A brassy note: a few harmonics with a soft attack and a little vibrato, inside a longer clip.
        static float Brass(float t, float start, float length, float f)
        {
            float lt = t - start;
            if (lt <= 0f || lt >= length + 0.15f) return 0f;
            float env = Mathf.Min(1f, lt / 0.035f) * (lt < length ? 1f - 0.25f * lt / Mathf.Max(0.2f, length) : Mathf.Max(0f, 1f - (lt - length) / 0.15f));
            float vib = 1f + 0.006f * Mathf.Clamp01(lt * 3f - 0.3f) * Mathf.Sin(TwoPi * 5.5f * lt);
            float sum = 0f;
            for (int h = 1; h <= 6; h++) sum += Mathf.Sin(TwoPi * f * h * vib * lt) / h * Mathf.Exp(-h * 0.15f);
            return sum * env;
        }

        // Scattered bells up high: `count` of them over half a second, at fixed pseudo-random pitches.
        static float Sparkle(float t, int count, float baseHz)
        {
            if (t <= 0f) return 0f;
            float sum = 0f;
            for (int i = 0; i < count; i++)
            {
                float at = i * 0.5f / count;
                float f = baseHz * (1f + 0.37f * ((i * 7) % 5) / 4f);
                if (t > at) sum += Bell(t - at, f) * (1f - 0.06f * i);
            }
            return sum;
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
