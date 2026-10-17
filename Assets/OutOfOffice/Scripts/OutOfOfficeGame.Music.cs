using System.Collections;
using UnityEngine;

namespace OutOfOffice
{
    // Adaptive music and the sound of big moments (OUT-17). Every frame the game tells the music player which song
    // fits (the menu theme over menus, the chapter's song in a room) and the mood of play, and the stems follow:
    // aiming thins the groove, rolling fills it, the tune leans in near the bed, a riser builds on the approach.
    public partial class OutOfOfficeGame
    {
        bool shotHadRiser;   // the approach riser plays once per shot
        State musicState;    // last frame's state, to notice a new shot

        // How close to the bed the music starts to lean in, and where the riser fires (board units).
        public const float NearHole = 2.6f, RiserDistance = 2.2f;

        void TickMusic()
        {
            var music = sound.Music;
            if (state == State.Moving && musicState != State.Moving) shotHadRiser = false;
            musicState = state;

            bool menu = state == State.Title || hud.CoversBoard;
            var song = menu ? Songs.Menu : SongForRoom(levelIndex);
            bool nearBed = ApproachingBed(out float distance);
            var mood = ChestStage.Showing ? MusicMood.Chest : menu ? MusicMood.Menu : MoodFor(state, nearBed);

            if (state == State.Moving && !shotHadRiser && mood == MusicMood.Near && distance < RiserDistance)
            {
                shotHadRiser = true;
                sound.Play(Sfx.Riser, 0.45f, 1f, 0.7f, 0.5f);
            }
            music.Set(song, mood);
            if (menu && level != null) music.Prepare(SongForRoom(levelIndex));  // the room's song is ready when Play is pressed
        }

        static SongSpec SongForRoom(int index) => Songs.ForChapter(Themes.ChapterOf(Mathf.Max(0, index)));

        // The mood of play for a state. Pure, for the checks.
        static MusicMood MoodFor(State s, bool nearBed)
        {
            switch (s)
            {
                case State.Aiming: return MusicMood.Aim;
                case State.Moving: return nearBed ? MusicMood.Near : MusicMood.Roll;
                case State.Capturing:
                case State.Complete: return MusicMood.Win;
                case State.Failed: return MusicMood.Fail;
                case State.Paused: return MusicMood.Pause;
                case State.Title: return MusicMood.Menu;
                default: return MusicMood.Idle;  // ready, penalty
            }
        }

        // Rolling toward the bed and close to it: not just passing by on the way somewhere else.
        bool ApproachingBed(out float distance)
        {
            distance = float.MaxValue;
            if (level == null || state != State.Moving) return false;
            Vector2 pos = level.Puck.Body.position, vel = level.Puck.Body.linearVelocity;
            Vector2 to = Def.Hole - pos;
            distance = to.magnitude;
            return distance < NearHole && vel.sqrMagnitude > 0.25f && Vector2.Dot(vel, to) > 0f;
        }

        // A hole in one gets a glissando the moment the blanket is up; every win ends on a stinger in the song's key.
        void PlayHoleInOne() => sound.Stinger(Sfx.Flourish, 0.6f, 0.35f);

        void PlayCompleteStinger() => sound.Stinger(sound.Music.WinStinger ?? Sfx.Complete, 0.75f, 0.25f);

        void PlayLoseStinger() => sound.Stinger(sound.Music.LoseStinger, 0.6f, 0.35f);

        // A chest's sounds: the rarity fanfare goes over the music, everything else is an ordinary effect.
        void PlayChestSound(AudioClip clip, float volume)
        {
            if (System.Array.IndexOf(Sfx.Fanfare, clip) >= 0) sound.Stinger(clip, volume, 0.2f);
            else sound.Play(clip, volume, 1f, 0.9f);
        }

        // The store and rewards: a register for a purchase, a shimmer and coin ticks for gems flying in.
        void PlayPurchase() => sound.Stinger(Sfx.KaChing, 0.7f, 0.5f);

        void OnCelebrated(string icon)
        {
            if (icon != "gem" && icon != "clip") return;
            sound.Play(Sfx.GemShimmer, 0.5f, 1f, 0.8f);
            hud.StartCoroutine(CoinTicks(icon == "gem" ? 8 : 5));  // on the HUD: a room loading must not cut them off
        }

        IEnumerator CoinTicks(int count)
        {
            yield return new WaitForSecondsRealtime(0.35f);
            for (int i = 0; i < count; i++)
            {
                var clip = Sfx.CoinTicks[Mathf.Min(i * Sfx.CoinTicks.Length / count, Sfx.CoinTicks.Length - 1)];
                sound.Play(clip, 0.35f, 1f + 0.02f * i, 0.4f, 0f, 2);
                yield return new WaitForSecondsRealtime(0.055f);
            }
        }
    }
}
