using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace OutOfOffice
{
    // What the music is doing (OUT-17). The game sets one every frame; the player eases the stems toward its mix.
    public enum MusicMood { Menu, Idle, Aim, Roll, Near, Win, Fail, Pause, Chest }

    // Adaptive music (OUT-17): two decks that crossfade between songs, each with the song's three stems started on the
    // same DSP sample so they stay locked together. The mood sets each stem's level (aiming thins the groove and
    // the tune, rolling brings everything in, near the bed the tune leans forward), a duck dips the music under
    // stingers and fanfares, and the pause snapshot muffles it through a low-pass filter.
    // Songs render on a worker thread; until a new one is ready the current one keeps playing.
    public class MusicPlayer
    {
        public const float BaseVolume = 0.18f;     // the music bus at full slider: songs at -18 dBFS RMS sit under the effects
        public const float Crossfade = 1.6f;

        class Deck
        {
            public GameObject Go;
            public AudioSource[] Stems = new AudioSource[3];
            public AudioLowPassFilter Filter;
            public string SongId;
            public float Fade, FadeTarget;
        }

        class Rendered
        {
            public SongSpec Spec;
            public AudioClip[] Stems;   // bed, groove, lead
            public AudioClip Win, Lose;
        }

        readonly Deck[] decks = new Deck[2];
        int live;  // the deck playing the wanted song
        readonly Dictionary<string, Rendered> songs = new Dictionary<string, Rendered>();
        readonly Dictionary<string, Task<SongStems>> rendering = new Dictionary<string, Task<SongStems>>();
        readonly float[] layer = { 0f, 0f, 0f }, layerTarget = { 1f, 1f, 1f };
        SongSpec wanted;
        string prefetched;  // rendered ahead for the next screen (the room behind the menu)
        readonly List<string> sweep = new List<string>();
        float duck = 1f, duckUntil, duckDepth = 1f, cutoff = 22000f, cutoffTarget = 22000f;
        public bool KeepMenuLoaded = true;  // off on low-memory devices: the menu song is rendered again on return

        public bool Enabled = true;
        public float Volume = 1f;  // the settings slider
        public MusicMood Mood { get; private set; } = MusicMood.Menu;
        public string Playing => decks[live].SongId;
        public bool Ready(SongSpec spec) => spec != null && songs.ContainsKey(spec.Id);

        public MusicPlayer(Transform parent)
        {
            for (int d = 0; d < 2; d++)
            {
                var deck = new Deck { Go = new GameObject("Music " + (d == 0 ? "A" : "B")) };
                deck.Go.transform.SetParent(parent, false);
                for (int i = 0; i < 3; i++)
                {
                    var src = deck.Go.AddComponent<AudioSource>();
                    src.playOnAwake = false;
                    src.loop = true;
                    src.volume = 0f;
                    src.priority = 0;  // music is never the voice that gets dropped
                    src.ignoreListenerPause = false;
                    deck.Stems[i] = src;
                }
                deck.Filter = deck.Go.AddComponent<AudioLowPassFilter>();
                deck.Filter.cutoffFrequency = 22000f;
                deck.Filter.enabled = false;
                decks[d] = deck;
            }
        }

        // The song to play and how it should sound right now.
        public void Set(SongSpec spec, MusicMood mood)
        {
            wanted = spec;
            Mood = mood;
            switch (mood)
            {
                case MusicMood.Aim: Layers(1f, 0.35f, 0.5f); break;   // concentration: the pulse steps back
                case MusicMood.Roll: Layers(1f, 1f, 1f); break;
                case MusicMood.Near: Layers(1f, 1f, 1.15f); break;    // the tune leans in as the bed comes close
                case MusicMood.Win: Layers(1f, 0.7f, 0.9f); break;
                case MusicMood.Fail: Layers(0.9f, 0.25f, 0f); break;  // just the harmony, kindly
                case MusicMood.Chest: Layers(1f, 0.6f, 0.4f); break;
                case MusicMood.Idle: Layers(1f, 0.75f, 1f); break;
                default: Layers(1f, 1f, 1f); break;                     // menu, pause
            }
            if (mood != MusicMood.Menu && mood != MusicMood.Chest) prefetched = null;  // in a room: nothing to get ready
            cutoffTarget = mood == MusicMood.Pause ? 900f : mood == MusicMood.Chest ? 2500f : 22000f;
        }

        void Layers(float bed, float groove, float lead)
        {
            layerTarget[0] = bed;
            layerTarget[1] = groove;
            layerTarget[2] = lead;
        }

        // Dips the music to `depth` for `seconds`, then lets it back up: stingers and fanfares sit on top.
        public void Duck(float depth, float seconds)
        {
            duckDepth = Mathf.Min(depth, Time.unscaledTime < duckUntil ? duckDepth : 1f);
            duckUntil = Mathf.Max(duckUntil, Time.unscaledTime + seconds);
        }

        public AudioClip WinStinger => Current?.Win;
        public AudioClip LoseStinger => Current?.Lose;
        Rendered Current => decks[live].SongId != null && songs.TryGetValue(decks[live].SongId, out var r) ? r : null;

        // Starts rendering a song ahead of time (the next chapter, the menu at boot).
        public void Prepare(SongSpec spec)
        {
            if (spec == null) return;
            if (spec != Songs.Menu) prefetched = spec.Id;
            if (songs.ContainsKey(spec.Id) || rendering.ContainsKey(spec.Id) || uploading?.Spec.Id == spec.Id) return;
            rendering[spec.Id] = Task.Run(() => Composer.Render(spec));
        }

        public void Tick(float dt)
        {
            CollectRenders();
            if (wanted != null)
            {
                if (decks[live].SongId != wanted.Id)
                {
                    if (songs.ContainsKey(wanted.Id)) Switch(wanted.Id);
                    else Prepare(wanted);
                }
            }

            // Duck: fast down (60 ms), slow back up (0.6 s).
            float duckGoal = Time.unscaledTime < duckUntil ? duckDepth : 1f;
            duck = Mathf.MoveTowards(duck, duckGoal, dt * (duckGoal < duck ? 12f : 1.2f));
            if (Time.unscaledTime >= duckUntil) duckDepth = 1f;

            for (int i = 0; i < 3; i++) layer[i] = Mathf.MoveTowards(layer[i], layerTarget[i], dt * 1.4f);
            cutoff = Mathf.Lerp(cutoff, cutoffTarget, 1f - Mathf.Exp(-dt / 0.15f));

            float bus = (Enabled ? BaseVolume : 0f) * Volume * duck;
            for (int d = 0; d < 2; d++)
            {
                var deck = decks[d];
                deck.Fade = Mathf.MoveTowards(deck.Fade, deck.FadeTarget, dt / Crossfade);
                for (int i = 0; i < 3; i++) deck.Stems[i].volume = bus * deck.Fade * Mathf.Min(1.2f, layer[i]);
                bool filtered = cutoff < 20000f;
                if (deck.Filter.enabled != filtered) deck.Filter.enabled = filtered;
                if (filtered) deck.Filter.cutoffFrequency = cutoff;
                if (deck.Fade <= 0f && deck.FadeTarget <= 0f && deck.SongId != null)
                {
                    foreach (var s in deck.Stems) { s.Stop(); s.clip = null; }
                    string old = deck.SongId;
                    deck.SongId = null;
                    Evict(old);
                }
            }

            // Songs that were rendered ahead and are no longer wanted (the player went elsewhere) are released too.
            if (songs.Count > 2)
            {
                sweep.Clear();
                sweep.AddRange(songs.Keys);
                foreach (var id in sweep) Evict(id);
            }
        }

        // A finished render becomes clips one per frame: copying a whole song into Unity at once could cost a frame.
        SongStems uploading;
        readonly List<AudioClip> uploaded = new List<AudioClip>();

        void CollectRenders()
        {
            if (uploading == null)
            {
                if (rendering.Count == 0) return;
                string done = null;
                foreach (var kv in rendering)
                    if (kv.Value.IsCompleted) { done = kv.Key; break; }
                if (done == null) return;
                var task = rendering[done];
                rendering.Remove(done);
                if (task.IsFaulted) { Debug.LogException(task.Exception); return; }
                uploading = task.Result;
                uploaded.Clear();
                return;
            }
            var st = uploading;
            string id = st.Spec.Id;
            switch (uploaded.Count)
            {
                case 0: uploaded.Add(Clip(id + ".bed", st.Bed, st.Rate)); return;
                case 1: uploaded.Add(Clip(id + ".groove", st.Groove, st.Rate)); return;
                case 2: uploaded.Add(Clip(id + ".lead", st.Lead, st.Rate)); return;
                case 3: uploaded.Add(Clip(id + ".win", st.Win, st.Rate)); return;
            }
            songs[id] = new Rendered
            {
                Spec = st.Spec,
                Stems = new[] { uploaded[0], uploaded[1], uploaded[2] },
                Win = uploaded[3],
                Lose = Clip(id + ".lose", st.Lose, st.Rate),
            };
            uploading = null;
            uploaded.Clear();
        }

        static AudioClip Clip(string name, float[] data, int rate)
        {
            var clip = AudioClip.Create(name, data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // Fades the live deck out and the other one in on the new song, its stems all starting on one DSP tick.
        void Switch(string id)
        {
            var r = songs[id];
            var old = decks[live];
            old.FadeTarget = 0f;
            live = 1 - live;
            var deck = decks[live];
            if (deck.SongId != null && deck.SongId != id)
            {
                foreach (var s in deck.Stems) s.Stop();
                string prev = deck.SongId;
                deck.SongId = null;
                Evict(prev);
            }
            bool resume = deck.SongId == id;  // switching back mid-fade: carry on where it is
            deck.SongId = id;
            deck.FadeTarget = 1f;
            if (resume) return;
            deck.Fade = 0f;
            double at = AudioSettings.dspTime + 0.08;
            for (int i = 0; i < 3; i++)
            {
                deck.Stems[i].clip = r.Stems[i];
                deck.Stems[i].timeSamples = 0;
                deck.Stems[i].PlayScheduled(at);
            }
        }

        // Frees a song nobody is playing, except the menu song when there is memory to keep it warm.
        void Evict(string id)
        {
            if (id == null || id == wanted?.Id || id == prefetched || id == decks[0].SongId || id == decks[1].SongId) return;
            if (KeepMenuLoaded && id == Songs.Menu.Id) return;
            if (!songs.TryGetValue(id, out var r)) return;
            foreach (var c in r.Stems) Object.Destroy(c);
            Object.Destroy(r.Win);
            Object.Destroy(r.Lose);
            songs.Remove(id);
        }

        public int LoadedSongs => songs.Count;
    }
}
