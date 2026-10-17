using System.Collections.Generic;
using UnityEngine;

namespace OutOfOffice
{
    // The mix buses (OUT-17). Effects, UI and the employees' voices follow the sound slider, each with its own trim;
    // music has its own slider, ducks under stingers and voices, and has its own player (MusicPlayer).
    public enum Bus { Sfx, Ui, Voice }

    // Plays one-shots through a small voice pool with per-clip cooldowns and instance limits, drives the continuous
    // loops (sliding, rubbing, charging, magnet hum) every frame, and runs the adaptive music.
    public class GameAudio : MonoBehaviour
    {
        const int VoiceCount = 14;

        class Voice
        {
            public AudioSource Source;
            public AudioClip Clip;
            public float Priority;
            public float EndTime;
        }

        class LoopChannel
        {
            public AudioSource Source;
            public float Volume, TargetVolume, Pitch = 1f, TargetPitch = 1f;
            public float Response;  // seconds to settle toward the target
        }

        readonly List<Voice> voices = new List<Voice>();
        readonly Dictionary<AudioClip, float> lastPlay = new Dictionary<AudioClip, float>();
        readonly Dictionary<AudioClip[], int> lastVariant = new Dictionary<AudioClip[], int>();
        readonly Dictionary<AudioClip, Bus> buses = new Dictionary<AudioClip, Bus>();
        LoopChannel slideWood, slideIce, rub, charge, magnet;
        LoopChannel[] loops;
        bool sfxOn = true;
        public MusicPlayer Music { get; private set; }

        // Bus trims: UI clicks sit under the game.
        static float Trim(Bus bus) => bus == Bus.Ui ? 0.85f : 1f;

        public static GameAudio Create()
        {
            Sfx.Init();
            var go = new GameObject("Audio");
            var a = go.AddComponent<GameAudio>();
            for (int i = 0; i < VoiceCount; i++)
            {
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                a.voices.Add(new Voice { Source = src });
            }
            a.slideWood = a.MakeLoop(Sfx.SlideWoodLoop, 0.08f);
            a.slideIce = a.MakeLoop(Sfx.SlideIceLoop, 0.08f);
            a.rub = a.MakeLoop(Sfx.RubLoop, 0.05f);
            a.charge = a.MakeLoop(Sfx.ChargeLoop, 0.06f);
            a.magnet = a.MakeLoop(Sfx.MagnetLoop, 0.12f);
            a.loops = new[] { a.slideWood, a.slideIce, a.rub, a.charge, a.magnet };
            foreach (var c in new[] { Sfx.Select, Sfx.Cancel, Sfx.UiTap, Sfx.UiBoing }) a.buses[c] = Bus.Ui;
            foreach (var c in Sfx.Taps) a.buses[c] = Bus.Ui;
            foreach (var c in Sfx.CoinTicks) a.buses[c] = Bus.Ui;
            foreach (var c in new[] { Sfx.Squeak, Sfx.Whee, Sfx.Yay, Sfx.Giggle, Sfx.Boing, Sfx.Yawn, Sfx.Oops }) a.buses[c] = Bus.Voice;
            a.Music = new MusicPlayer(go.transform) { KeepMenuLoaded = Quality.Detected != Tier.Low };
            a.Music.Prepare(Songs.Menu);  // the title's song renders while the boot screen is up
            return a;
        }

        LoopChannel MakeLoop(AudioClip clip, float response)
        {
            var src = gameObject.AddComponent<AudioSource>();
            src.clip = clip;
            src.loop = true;
            src.volume = 0f;
            src.playOnAwake = false;
            src.Play();
            return new LoopChannel { Source = src, Response = response };
        }

        public void SetEnabled(bool sfx, bool musicEnabled)
        {
            sfxOn = sfx;
            Music.Enabled = musicEnabled;
        }

        // Volume levels from the settings sliders (OUT-16), on top of the on/off switches.
        public float SfxVolume = 1f;
        public float MusicVolume { get => Music.Volume; set => Music.Volume = value; }

        public Bus BusOf(AudioClip clip) => clip != null && buses.TryGetValue(clip, out var b) ? b : Bus.Sfx;

        // Plays a one-shot. Skips it if the same clip played within `cooldown` or already has `maxInstances` playing.
        // When all voices are busy, the lowest-priority voice that is closest to finishing gets replaced.
        public void Play(AudioClip clip, float volume, float pitch = 1f, float priority = 0.5f, float cooldown = 0.03f, int maxInstances = 3)
        {
            if (!sfxOn || clip == null || volume <= 0.01f) return;
            float now = Time.unscaledTime;
            if (lastPlay.TryGetValue(clip, out float last) && now - last < cooldown) return;
            int playing = 0;
            Voice free = null, steal = null;
            foreach (var v in voices)
            {
                bool busy = v.Source.isPlaying && now < v.EndTime;
                if (!busy)
                {
                    free ??= v;
                    continue;
                }
                if (v.Clip == clip) playing++;
                if (steal == null || v.Priority < steal.Priority || v.Priority == steal.Priority && v.EndTime < steal.EndTime) steal = v;
            }
            if (playing >= maxInstances) return;
            var pick = free ?? steal;
            if (free == null && steal.Priority > priority) return;
            lastPlay[clip] = now;
            pick.Clip = clip;
            pick.Priority = priority;
            pick.EndTime = now + clip.length / Mathf.Max(0.1f, pitch);
            pick.Source.Stop();
            pick.Source.clip = clip;
            pick.Source.pitch = pitch;
            var bus = BusOf(clip);
            pick.Source.volume = Mathf.Clamp01(volume) * SfxVolume * Trim(bus);
            pick.Source.Play();
            if (bus == Bus.Voice) Music.Duck(0.8f, clip.length * 0.8f);  // a voice line pulls the music back a little
        }

        // Plays one of a set of takes, never the same one twice running, a few percent louder or softer each time.
        public void Play(AudioClip[] variants, float volume, float pitch = 1f, float priority = 0.5f, float cooldown = 0.03f)
        {
            if (variants == null || variants.Length == 0) return;
            int pick = PickVariant(variants.Length, lastVariant.TryGetValue(variants, out int last) ? last : -1, Random.value);
            lastVariant[variants] = pick;
            Play(variants[pick], volume * Random.Range(0.92f, 1f), pitch, priority, cooldown);
        }

        // Which take to play: uniform over the others, so a take never repeats. Pure, for the checks.
        public static int PickVariant(int count, int last, float roll)
        {
            if (count <= 1) return 0;
            roll = Mathf.Clamp01(roll);
            if (last < 0 || last >= count) return Mathf.Min(count - 1, (int)(roll * count));
            int k = Mathf.Min(count - 2, (int)(roll * (count - 1)));
            return k >= last ? k + 1 : k;
        }

        // A musical moment over the music (a stinger, a fanfare): the music ducks under it for its length.
        public void Stinger(AudioClip clip, float volume, float duckTo = 0.3f)
        {
            if (clip == null) return;
            if (sfxOn) Music.Duck(duckTo, clip.length * 0.85f);
            Play(clip, volume, 1f, 1f, 0f);
        }

        // Sliding follows speed and surface. Silent in the air or at rest.
        public void SetSlide(Surface surface, float speed)
        {
            float k = Mathf.Clamp01(speed / 18f);
            float level = k <= 0.02f ? 0f : Mathf.Pow(k, 0.7f);
            slideWood.TargetVolume = surface == Surface.Wood ? level * 0.32f : surface == Surface.Mud ? level * 0.2f : 0f;
            slideIce.TargetVolume = surface == Surface.Ice ? level * 0.28f : 0f;
            slideWood.TargetPitch = 0.75f + 0.5f * k;
            slideIce.TargetPitch = 0.85f + 0.4f * k;
        }

        // Sustained contact along a wall or curve: a soft rolling rub driven by tangential speed.
        public void SetRub(float tangentialSpeed)
        {
            float k = Mathf.Clamp01((tangentialSpeed - Rules.RubMinSpeed) / 14f);
            rub.TargetVolume = k * 0.55f;
            rub.TargetPitch = 0.8f + 0.6f * k;
        }

        public void SetCharge(bool active, float power)
        {
            charge.TargetVolume = active ? 0.03f + 0.07f * power : 0f;
            charge.TargetPitch = 0.8f + 0.5f * power;
        }

        public void SetMagnet(float pull)
        {
            magnet.TargetVolume = Mathf.Clamp01(pull) * 0.4f;
            magnet.TargetPitch = 0.9f + 0.3f * pull;
        }

        public void StopContinuous()
        {
            SetSlide(Surface.Air, 0f);
            SetRub(0f);
            SetCharge(false, 0f);
            SetMagnet(0f);
        }

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            foreach (var ch in loops)
            {
                float a = 1f - Mathf.Exp(-dt / Mathf.Max(0.01f, ch.Response));
                ch.Volume = Mathf.Lerp(ch.Volume, sfxOn ? ch.TargetVolume : 0f, a);
                ch.Pitch = Mathf.Lerp(ch.Pitch, ch.TargetPitch, a);
                ch.Source.volume = ch.Volume * SfxVolume;
                ch.Source.pitch = ch.Pitch;
            }
            Music.Tick(Mathf.Min(dt, 0.1f));
        }

        // Fade everything when the app loses focus; resume when it returns.
        void OnApplicationFocus(bool focus) => AudioListener.pause = !focus;
        void OnApplicationPause(bool paused) => AudioListener.pause = paused;
    }
}
