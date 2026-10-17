using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace OutOfOffice.EditorTools
{
    // The adaptive music in the running game (OUT-17): boots Play mode, waits for the first song, opens a room in the
    // second chapter and checks that its song takes over with all three stems playing in lockstep, and that the
    // switch never costs a frame (the render runs on a worker thread).
    // Unity -batchmode -executeMethod OutOfOffice.EditorTools.AudioSmoke.RunBatch
    [InitializeOnLoad]
    public static class AudioSmoke
    {
        const string Key = "OOO_AUDIO_SMOKE";
        static double phaseStart;
        static int phase, failures;
        static float worstFrame;

        static AudioSmoke()
        {
            if (!SessionState.GetBool(Key, false)) return;
            EditorApplication.update -= Step;
            EditorApplication.update += Step;
        }

        public static void RunBatch()
        {
            SessionState.SetBool(Key, true);
            EditorSceneManagerOpen();
            EditorApplication.update -= Step;
            EditorApplication.update += Step;    // with domain reload off (this project) the callback survives Play mode;
            EditorApplication.isPlaying = true;  // with it on, the static constructor registers it again
        }

        static void EditorSceneManagerOpen()
        {
            var scene = "Assets/Scenes/SampleScene.unity";
            if (System.IO.File.Exists(scene)) UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scene);
        }

        static void Check(bool ok, string name)
        {
            Debug.Log("HSAUDIO smoke " + name + (ok ? " PASS" : " FAIL"));
            if (!ok) failures++;
        }

        static void Finish()
        {
            Debug.Log($"HSAUDIO smoke done, failures: {failures}");
            SessionState.SetBool(Key, false);
            EditorApplication.update -= Step;
            EditorApplication.isPlaying = false;
            EditorApplication.Exit(failures == 0 ? 0 : 7);
        }

        static void Next()
        {
            phase++;
            phaseStart = EditorApplication.timeSinceStartup;
        }

        static double Elapsed => EditorApplication.timeSinceStartup - phaseStart;

        static void Step()
        {
            if (!EditorApplication.isPlaying) { if (phase == 0) phaseStart = EditorApplication.timeSinceStartup; return; }
            var audio = UnityEngine.Object.FindAnyObjectByType<GameAudio>();
            var game = UnityEngine.Object.FindAnyObjectByType<OutOfOfficeGame>();
            try
            {
                switch (phase)
                {
                    case 0:  // boot: the first song (the menu theme, or the orientation room's) starts on its own
                        if (audio != null && audio.Music != null && audio.Music.Playing != null)
                        {
                            Check(true, "first song plays: " + audio.Music.Playing);
                            CheckStems(audio, "first song");
                            Next();
                        }
                        else if (Elapsed > 40) { Check(false, "first song plays"); Finish(); }
                        break;
                    case 1:  // open room 12, the first of chapter 2
                        Invoke(game, "LoadLevel", Themes.ChapterStart[1]);
                        var hud = Field(game, "hud");
                        hud.GetType().GetMethod("HideTitle").Invoke(hud, null);
                        hud.GetType().GetMethod("HideAll").Invoke(hud, null);
                        var state = game.GetType().GetField("state", BindingFlags.NonPublic | BindingFlags.Instance);
                        state.SetValue(game, Enum.Parse(state.FieldType, "Ready"));
                        worstFrame = 0f;
                        Next();
                        break;
                    case 2:  // its song takes over without a hitch
                        worstFrame = Mathf.Max(worstFrame, Time.unscaledDeltaTime);
                        string want = Songs.ForChapter(1).Id;
                        if (audio.Music.Playing == want && Elapsed > MusicPlayer.Crossfade + 0.5)
                        {
                            Check(true, "chapter 2 song takes over");
                            CheckStems(audio, "chapter 2 song");
                            Check(worstFrame < 0.25f, $"no hitch while switching songs (worst frame {worstFrame * 1000f:0} ms)");
                            Check(audio.Music.LoadedSongs <= 2, $"old songs are released ({audio.Music.LoadedSongs} loaded)");
                            Check(audio.Music.WinStinger != null && audio.Music.WinStinger.name.StartsWith(want), "win stinger is the chapter's");
                            Finish();
                        }
                        else if (Elapsed > 30) { Check(false, "chapter 2 song takes over (playing " + audio.Music.Playing + ")"); Finish(); }
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                failures++;
                Finish();
            }
        }

        // All three stems of the live deck are playing the same song at the same sample.
        static void CheckStems(GameAudio audio, string what)
        {
            AudioSource[] live = null;
            foreach (Transform child in audio.transform)
            {
                var stems = child.GetComponents<AudioSource>();
                if (stems.Length == 3 && stems[0].clip != null && stems[0].clip.name.StartsWith(audio.Music.Playing + ".")) live = stems;
            }
            Check(live != null, what + " has its stems on a deck");
            if (live == null) return;
            bool playing = live[0].isPlaying && live[1].isPlaying && live[2].isPlaying;
            int t0 = live[0].timeSamples;
            bool locked = Mathf.Abs(live[1].timeSamples - t0) <= 1 && Mathf.Abs(live[2].timeSamples - t0) <= 1;
            Check(playing, what + " stems are playing");
            Check(locked, $"{what} stems are in lockstep ({live[0].timeSamples}, {live[1].timeSamples}, {live[2].timeSamples})");
        }

        static object Field(object o, string name) =>
            o.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public).GetValue(o);

        static void Invoke(object o, string name, params object[] args) =>
            o.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public).Invoke(o, args);
    }
}
