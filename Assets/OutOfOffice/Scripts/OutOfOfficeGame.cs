using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace OutOfOffice
{
    // Out of Office game flow: shots and par, undo and restart, input, camera, lighting, and the mapping from
    // physics events (Puck.Step) to sound, effects, and haptics. Boots itself in any scene.
    public class OutOfOfficeGame : MonoBehaviour
    {
        enum State { Title, Ready, Aiming, Moving, Capturing, Penalty, Complete, Paused }

        const int AimDotCount = 24, RingDotCount = 28;
        const float CameraTilt = 34f, CameraFov = 36f;

        static bool booted;

        Camera cam;
        Light sun;
        Mascot mascot;
        float lastSqueak;
        Hud hud;
        GameAudio sound;
        Progress progress;
        DebugView debugView;
        LevelInstance level;
        int levelIndex;   // -1 is the developer lab
        State state, stateBeforePause;
        float levelTime;
        int shots, starCombo;
        int undos;  // undos this attempt: undo gives the shot back, so par can be reached with it (OUT-13)
        Vector2 restPos;
        readonly Stack<LevelSnapshot> history = new Stack<LevelSnapshot>();

        Vector2 dragStart, dragNow, aimDir = Vector2.up;
        Vector2 releaseOffset;
        SpriteRenderer pathEnd, bounceMark;
        float power;
        Slingshot sling;
        // How far the drawing is pulled back at full power, in board units.
        const float PullBack = 0.6f;
        int aimTick;
        readonly List<Vector2> preview = new List<Vector2>();
        AimPreview.Result previewResult;
        readonly List<SpriteRenderer> aimDots = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> ringDots = new List<SpriteRenderer>();
        SpriteRenderer aimRing;

        float lastImpact, fleckTimer, trailTimer;
        object levelFx = new object();  // owner of level-bound tweens; replaced on undo, restart, and load
        Vector3 cameraRest, shakePos, shakeVel;
        Vector3Int fittedFor;

        LevelDef Def => levelIndex < 0 ? Levels.Lab : Levels.All[levelIndex];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => booted = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (booted) return;
            booted = true;
            new GameObject("Out of Office").AddComponent<OutOfOfficeGame>();
        }

        void Awake()
        {
            // Rendering runs at the display rate (120 Hz on ProMotion). Physics stays on a fixed 60 Hz step,
            // and rigidbodies interpolate between steps, so shots behave the same at any frame rate.
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = Screen.currentResolution.refreshRateRatio.value >= 90.0 ? 120 : 60;
            Screen.orientation = ScreenOrientation.Portrait;
            Time.fixedDeltaTime = Rules.Dt;
            Physics2D.simulationMode = SimulationMode2D.FixedUpdate;

            progress = new Progress(new UnityPrefs());
            SetUpCamera();
            SetUpLighting();

            sound = GameAudio.Create();
            sound.SetEnabled(progress.Sfx, progress.Music);
            Haptics.Prepare();
            FxDot.Prewarm(240);
            ApplyMotionSetting(progress.ReducedMotion);
            debugView = gameObject.AddComponent<DebugView>();
            debugView.PreviewPath = preview;

            hud = Hud.Create();
            WireHud();
            BuildAimVisuals();

            LoadLevel(progress.CurrentIndex);
            ShowTitle();
            IconStudio.RenderHero(Cosmetics.Compose(progress.Outfit));
            IconStudio.RenderItems(progress.Outfit);
        }

        void ShowTitle()
        {
            state = State.Title;
            int index = Mathf.Max(0, progress.CurrentIndex);
            hud.ShowTitle(progress.TotalStars, index + 1, Themes.ForLevel(index));
            hud.SetQuestBadge(progress.ChestsWaiting);
        }

        // Rebuilds Puffy's look in place after an outfit change, and re-renders the title portrait.
        void ApplyOutfit()
        {
            var look = Cosmetics.Compose(progress.Outfit);
            if (level != null)
            {
                for (int c = level.Puck.Spin.childCount - 1; c >= 0; c--)
                {
                    var old = level.Puck.Spin.GetChild(c);
                    old.gameObject.SetActive(false);
                    old.SetParent(null);
                    Destroy(old.gameObject);
                }
                LevelBuilder.BuildPuckLook(level.Puck.Spin, look);
                mascot.SetSkin(look);
                mascot.OnJoy(0.8f);
                mascot.Hop();
            }
            IconStudio.RenderHero(look);
        }

        // Rolls a chest, saves the prize, and plays the opening scene. The prize may be worn right away.
        void OpenChest(string heading)
        {
            var item = progress.OpenChest(new System.Random(), out bool duplicate);
            ChestStage.Open(item, duplicate, Cosmetics.Compose(progress.Outfit.With(item)), heading, (clip, volume) => sound.Play(clip, volume, 1f, 0.9f), equip =>
            {
                if (equip)
                {
                    progress.Equip(item);
                    ApplyOutfit();
                }
                hud.RefreshQuests();
                hud.RefreshChapterChest();
                hud.SetQuestBadge(progress.ChestsWaiting);
            }, progress.ReducedMotion);
            hud.SetQuestBadge(progress.ChestsWaiting);
        }

        // Counts toward today's quests and cheers when one completes.
        void TrackQuest(QuestKind kind, int amount = 1)
        {
            if (progress.Track(kind, amount) <= 0) return;
            hud.Banner("TASK DONE!", Themes.Hex(0xFFE45C), 0.2f);
            sound.Play(Sfx.Reward, 0.6f, 1.1f, 0.8f);
        }

        void ApplyMotionSetting(bool reduced)
        {
            Fx.Amount = reduced ? 0.5f : 1f;
            Puck.MotionScale = reduced ? 0.35f : 1f;
        }

        void WireHud()
        {
            hud.UnlockedCount = () => progress.Unlocked;
            hud.CurrentLevel = () => Mathf.Max(0, progress.CurrentIndex);
            hud.BestStars = id => progress.Best(id);
            hud.CurrentOutfit = () => progress.Outfit;
            hud.OwnsItem = item => progress.Owns(item);
            hud.EquipItem = item =>
            {
                if (!progress.Owns(item)) return;
                Tap();
                progress.Equip(item);
                ApplyOutfit();
            };
            hud.TodayQuests = () => progress.TodayQuests;
            hud.QuestProgressOf = i => progress.QuestProgress(i);
            hud.QuestClaimedOf = i => progress.QuestClaimed(i);
            hud.DailyChestReady = () => progress.DailyChestReady;
            hud.UntilQuestReset = () => progress.UntilReset;
            hud.ChestsWaiting = () => progress.ChestsWaiting;
            hud.ClaimQuest = i => { if (progress.ClaimQuest(i)) OpenChest("TASK COMPLETE!"); };
            hud.OpenDailyChest = () => { if (progress.ClaimDailyChest()) OpenChest("EMPLOYEE APPRECIATION"); };
            hud.ChapterChestClaimed = c => progress.ChapterChestClaimed(c);
            hud.ClaimChapterChest = c => { if (progress.ClaimChapterChest(c)) OpenChest("CHAPTER MASTERED!"); };
            hud.UpdateSettings(progress.Sfx, progress.Music, progress.ReducedMotion);

            hud.Play = () => { Tap(); hud.HideTitle(); state = State.Ready; hud.SetHint(shots == 0); quipAt = Time.unscaledTime + 0.4f; };
            hud.Pause = PauseGame;
            hud.Resume = ResumeGame;
            hud.Restart = () => { sound.Play(Sfx.Restart, 0.7f); StartLevel(levelIndex); };
            hud.Undo = UndoShot;
            hud.Next = () =>
            {
                // After the last room there is nothing to start: go home rather than replay it forever.
                if (IsFinalLevel) { GoHome(); return; }
                Tap();
                StartLevel(levelIndex < 0 ? -1 : levelIndex + 1);
            };
            hud.Replay = () => { sound.Play(Sfx.Restart, 0.7f); StartLevel(levelIndex); };
            hud.Home = GoHome;
            hud.SelectLevel = i => { Tap(); StartLevel(i); };
            hud.SetSfx = on => { progress.Sfx = on; sound.SetEnabled(on, progress.Music); Tap(); hud.UpdateSettings(progress.Sfx, progress.Music, progress.ReducedMotion); };
            hud.SetMusic = on => { progress.Music = on; sound.SetEnabled(progress.Sfx, on); Tap(); hud.UpdateSettings(progress.Sfx, progress.Music, progress.ReducedMotion); };
            hud.SetReducedMotion = on =>
            {
                progress.ReducedMotion = on;
                ApplyMotionSetting(on);
                Tap();
                hud.UpdateSettings(progress.Sfx, progress.Music, progress.ReducedMotion);
            };
            hud.OpenLab = () => { Tap(); StartLevel(-1); };
            hud.ToggleDebug = () => { DebugView.Enabled = !DebugView.Enabled; Tap(); };
        }

        void Tap() => sound.Play(Sfx.UiTap, 0.5f, Random.Range(0.95f, 1.05f));

        void SetUpCamera()
        {
            foreach (var c in Camera.allCameras) c.gameObject.SetActive(false);
            var camGo = new GameObject("Out of Office Camera") { tag = "MainCamera" };
            cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = CameraFov;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Palette.SkyBottom;
            cam.nearClipPlane = 0.5f;
            cam.farClipPlane = 250f;
            camGo.AddComponent<AudioListener>();
            FxDot.CameraTransform = camGo.transform;
        }

        void SetUpLighting()
        {
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) l.gameObject.SetActive(false);
            sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.55f;
            sun.transform.rotation = Quaternion.LookRotation(new Vector3(0.4f, -0.5f, 1f), Vector3.up);
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.fog = false;
            ApplyTheme(Themes.All[0]);
        }

        // Sky color and lighting of a chapter.
        void ApplyTheme(Theme theme)
        {
            // The aim guide's palette follows the floor, decided once per room: navy dots with a white halo on pale
            // floors, ivory dots with a navy rim on dark carpet. Contrast never depends on color alone.
            var dotSprite = theme.DarkFloor ? Shapes.OutlinedDot : Shapes.DarkDot;
            foreach (var d in aimDots) d.sprite = dotSprite;
            foreach (var d in ringDots) d.sprite = dotSprite;
            cam.backgroundColor = theme.Ground;
            sun.color = theme.Sun;
            sun.intensity = theme.SunIntensity;
            var ambient = new SphericalHarmonicsL2();
            ambient.AddAmbientLight(theme.Ambient);
            RenderSettings.ambientLight = theme.Ambient;
            RenderSettings.ambientProbe = ambient;
        }

        void BuildAimVisuals()
        {
            sling = new Slingshot(transform);
            pathEnd = LevelBuilder.MakeSprite("Path End", Shapes.OutlinedRing, Color.white, transform, Vector3.zero, Vector2.one * 0.42f, 4);
            bounceMark = LevelBuilder.MakeSprite("Bounce Mark", Shapes.OutlinedDot, new Color(1f, 0.8f, 0.3f), transform, Vector3.zero, Vector2.one * 0.26f, 5);
            pathEnd.enabled = bounceMark.enabled = false;
            for (int i = 0; i < AimDotCount; i++)
            {
                var dot = LevelBuilder.MakeSprite("Aim Dot", Shapes.OutlinedDot, Color.white, transform, Vector3.zero, Vector2.one * 0.16f, 4);
                dot.enabled = false;
                aimDots.Add(dot);
            }
            aimRing = LevelBuilder.MakeSprite("Aim Ring", Shapes.OutlinedRing, Color.white, transform, Vector3.zero, Vector2.one, 4);
            aimRing.enabled = false;
            for (int i = 0; i < RingDotCount; i++)
            {
                var dot = LevelBuilder.MakeSprite("Power Dot", Shapes.OutlinedDot, Color.white, transform, Vector3.zero, Vector2.one * 0.11f, 5);
                dot.enabled = false;
                ringDots.Add(dot);
            }
        }

        // ---------- Level flow ----------

        float quipAt = -1f;
        int openedLevel = int.MinValue;  // the room that has had its opener; restarts and replays don't repeat it
        const float OpenerSeconds = 1f;

        void StartLevel(int index)
        {
            LoadLevel(index);
            quipAt = Time.unscaledTime + 1.6f;  // after the camera has shown the hole and come back to Puffy
            hud.HideAll();
            hud.HideTitle();
            Time.timeScale = 1f;
            state = State.Ready;
        }

        void LoadLevel(int index)
        {
            StopAllCoroutines();
            KillLevelFx();
            celebrating = false;
            if (mascot != null) mascot.Tumble = false;
            sling?.Hide();
            Time.timeScale = 1f;
            if (level != null)
            {
                Meshes.ReleaseLevel(level.Root);
                Destroy(level.Root);
            }
            levelIndex = index;
            if (index >= 0) progress.CurrentIndex = index;

            var theme = Themes.ForLevel(index);
            ApplyTheme(theme);
            level = LevelBuilder.Build(Def, transform, true, Cosmetics.Compose(progress.Outfit), theme);
            level.GateMoved = g =>
            {
                sound.Play(Sfx.GateMove, 0.6f, 1f, 0.6f);
                Fx.Puff(g.Def.Center, Color.white, 6, 0.6f);
            };
            levelTime = 0f;
            level.TickMovers(0f, true);
            mascot = level.Puck.GetComponent<Mascot>();
            mascot.Sound = (clip, volume) => sound.Play(clip, volume, Random.Range(0.96f, 1.04f), 0.7f);
            shots = 0;
            undos = 0;
            history.Clear();
            restPos = Def.Start;
            level.Puck.PlaceAt(restPos, true);
            sound.StopContinuous();
            fittedFor = default;
            shakePos = shakeVel = Vector3.zero;
            debugView.Level = level;
            debugView.ClearShapes();

            hud.SetLevel(index < 0 ? 0 : index + 1, Def.Name, Def.Hint, theme);
            hud.SetShots(0, Def.Par);
            hud.SetStars(0, level.Stars.Count, false);
            hud.SetUndoAvailable(false);
            hud.SetHint(true);
            state = State.Ready;
        }

        // Cancels everything that belongs to the current shot's presentation: pickup tweens and flying stars.
        // Their callbacks must never touch a restored or rebuilt level.
        void KillLevelFx()
        {
            releaseOffset = Vector2.zero;
            Tweens.Kill(levelFx);
            levelFx = new object();
            hud?.CancelFlyingStars();
        }

        // Home always leaves the level: it rebuilds it fresh behind the title, so no shot, outcome, or animation
        // can continue while the title is open. Play then starts that level from the beginning.
        void GoHome()
        {
            Tap();
            LoadLevel(levelIndex < 0 ? Mathf.Max(0, progress.CurrentIndex) : levelIndex);
            ShowTitle();
        }

        bool IsFinalLevel => levelIndex == Levels.All.Length - 1;

        int StarsFor(int shotCount) =>
            1 + (shotCount <= Def.Par ? 1 : 0) + (level.StarsCollected == level.Stars.Count ? 1 : 0);

        // ---------- Input ----------

        // Frame pacing: a steady 60 feels smoother than a 120 that keeps missing frames. If the phone can not hold
        // ProMotion rates for a couple of seconds of play, settle on 60 for the rest of the session.
        float paceAverage = 1f / 120f, paceSlow;
        bool paceLocked;

        void PaceFrames()
        {
            if (paceLocked || Application.targetFrameRate <= 60) return;
            float dt = Time.unscaledDeltaTime;
            if (dt > 0.1f) return;  // a load or a pause, not a pacing problem
            paceAverage = Mathf.Lerp(paceAverage, dt, 0.05f);
            paceSlow = paceAverage > 1f / 100f ? paceSlow + dt : 0f;
            if (paceSlow > 2f)
            {
                Application.targetFrameRate = 60;
                paceLocked = true;
            }
        }

        void Update()
        {
            PaceFrames();
            UpdateQuips();
            if (state == State.Ready || state == State.Aiming) HandleInput();
            UpdateAim();
            UpdateTrail();
            hud.SetDebug(DebugView.Enabled ? debugView.Stats : null);
        }

        void HandleInput()
        {
            var pointer = Pointer.current;
            if (pointer == null) return;
            Vector2 screen = pointer.position.ReadValue();

            if (state == State.Ready)
            {
                if (pointer.press.wasPressedThisFrame && !hud.Blocks(screen))
                {
                    state = State.Aiming;
                    hud.HideSpeech();  // nothing sits over the board while aiming
                    mascot.Wake();
                    dragStart = ScreenToBoard(screen);
                    dragNow = dragStart;
                    power = 0f;
                    aimTick = 0;
                    sound.Play(Sfx.Select, 0.4f, Random.Range(0.97f, 1.03f));
                    Haptics.Play(Haptics.Style.Light, 0.3f);
                }
                return;
            }

            // A cancelled touch (system gesture, call, etc.) never fires a shot.
            var touch = Touchscreen.current;
            if (touch != null && touch.primaryTouch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Canceled)
            {
                CancelAim(false);
                return;
            }

            if (pointer.press.isPressed)
            {
                // Slingshot: pull away from the shot direction. Pulling back into the dead zone cancels.
                dragNow = ScreenToBoard(screen);
                Vector2 pull = dragStart - dragNow;
                power = Mathf.Clamp01(pull.magnitude / Rules.MaxDrag);
                if (pull.sqrMagnitude > 1e-4f) aimDir = pull.normalized;
                previewResult = power >= Rules.MinPower
                    ? AimPreview.Compute(level, restPos, aimDir * LaunchSpeed(power), preview)
                    : default;
                if (power < Rules.MinPower) preview.Clear();
                sound.SetCharge(power >= Rules.MinPower, power);

                int tick = Mathf.FloorToInt(power * 5f);
                if (tick > aimTick)
                {
                    Haptics.Play(Haptics.Style.Light, 0.2f + 0.12f * tick);
                }
                aimTick = tick;
                return;
            }

            if (power >= Rules.MinPower) Launch();
            else CancelAim(true);
        }

        static float LaunchSpeed(float p) => Rules.MaxSpeed * Mathf.Pow(p, Rules.PowerCurve);

        void CancelAim(bool audible)
        {
            if (state != State.Aiming) return;
            state = State.Ready;
            power = 0f;
            preview.Clear();
            sling.Release();
            sound.SetCharge(false, 0f);
            if (audible) sound.Play(Sfx.Cancel, 0.4f);
        }

        void OnApplicationFocus(bool focus)
        {
            if (focus) return;
            CancelAim(false);
            if (state == State.Ready || state == State.Moving) PauseGame();
        }

        // A backgrounded app can be killed by the OS with no further callback, so this is the last safe moment to save.
        void OnApplicationPause(bool paused)
        {
            if (paused) progress?.Flush();
        }

        void OnApplicationQuit() => progress?.Flush();

        void Launch()
        {
            history.Push(level.Capture(restPos, shots, levelTime));
            shots++;
            TrackQuest(QuestKind.Shots);
            hud.HideSpeech();
            starCombo = 0;
            hud.SetShots(shots, Def.Par);
            hud.SetHint(false);
            hud.SetUndoAvailable(true);

            var puck = level.Puck;
            float speed = LaunchSpeed(power);
            // Puffy is drawn squashed back into the pull; the drawing catches up with the body within a few frames.
            releaseOffset = puck.VisualOffset;
            puck.Launch(aimDir * speed);
            sling.Release();
            sound.SetCharge(false, 0f);
            int strength = power < 0.35f ? 0 : power < 0.7f ? 1 : 2;
            sound.Play(Sfx.Launch[strength], 0.22f + 0.28f * power, Random.Range(0.97f, 1.03f), 0.8f);
            Haptics.Play(Haptics.Style.Soft, 0.3f + 0.5f * power);
            mascot.AimDir = aimDir;
            mascot.OnLaunch(power);
            // A soft ring of air where Puffy left, and a little puff behind it.
            Fx.Ring(restPos, new Color(1f, 1f, 1f, 0.7f), 0.4f, 1.4f + power, 0.28f);
            Fx.Puff(restPos - aimDir * 0.35f, Color.white, 2 + Mathf.RoundToInt(3f * power), 0.15f);
            if (power > 0.9f)
            {
                // A full pull: a bigger blast, sparks, a camera kick, and a heavy thump.
                Fx.Ring(restPos, new Color(1f, 0.85f, 0.4f, 0.8f), 0.6f, 3.2f, 0.35f);
                Fx.Sparks(restPos, aimDir, new Color(1f, 0.85f, 0.4f), 10, 7f);
                if (!progress.ReducedMotion) Kick(-aimDir, 2.2f);
                Haptics.Play(Haptics.Style.Heavy, 1f);
            }
            preview.Clear();
            state = State.Moving;
        }

        // ---------- Simulation ----------

        void FixedUpdate()
        {
            if (level == null || state == State.Paused) return;
            levelTime += Time.fixedDeltaTime;
            level.TickMovers(levelTime, false);

            // A moving part can push a resting puck; then it simulates like any shot, without counting one.
            if ((state == State.Ready || state == State.Aiming) && level.Puck.Body.linearVelocity.sqrMagnitude > 0.09f)
            {
                CancelAim(false);
                level.Puck.Wake();
                state = State.Moving;
            }

            // Pop-up spikes rising under a resting Puffy send it back, like rolling onto them.
            if ((state == State.Ready || state == State.Aiming) && history.Count > 0 && SpikesUnder(level.Puck.Body.position))
            {
                CancelAim(false);
                StartCoroutine(PenaltyRoutine(false, level.Puck.Body.position + Vector2.down * 0.01f));
                return;
            }

            if (state != State.Moving) return;
            var info = level.Puck.Step(Time.fixedDeltaTime);
            PlayEvents(info);

            switch (info.Result)
            {
                case ShotResult.Rest:
                    if (CratesMoving()) break;  // wait for crates to settle too
                    restPos = level.Puck.Body.position;
                    sound.StopContinuous();
                    state = State.Ready;
                    break;
                case ShotResult.Sunk:
                    StartCoroutine(CaptureRoutine());
                    break;
                case ShotResult.Hazard:
                case ShotResult.Fell:
                    StartCoroutine(PenaltyRoutine(info.Result == ShotResult.Fell, info.ImpactPoint));
                    break;
            }
        }

        bool CratesMoving()
        {
            foreach (var c in level.Crates)
                if (c.Body.linearVelocity.sqrMagnitude > 0.0025f) return true;
            return false;
        }

        void PlayEvents(StepInfo info)
        {
            var puck = level.Puck;
            Vector2 pos = puck.Body.position;
            Vector2 vel = puck.Body.linearVelocity;
            bool reduced = progress.ReducedMotion;

            sound.SetSlide(puck.Airborne ? Surface.Air : info.Surface, vel.magnitude);
            sound.SetMagnet(info.MagnetPull);

            // Impacts: loudness and feedback follow the normal speed, so grazes stay gentle.
            if (info.Impact && info.ImpactSpeed >= Rules.ImpactMinSpeed && Time.time - lastImpact > Rules.ImpactCooldown)
            {
                lastImpact = Time.time;
                float vn = info.ImpactSpeed;
                float k = Mathf.Clamp01((vn - Rules.ImpactMinSpeed) / 12f);
                var kind = info.ImpactBlock != null ? info.ImpactBlock.Kind : BlockKind.Wall;
                float pitch = Random.Range(0.95f, 1.05f) * Mathf.Lerp(1.08f, 0.9f, k);
                float volume = 0.12f + 0.88f * Mathf.Pow(k, 0.8f);
                Color chip;
                switch (kind)
                {
                    case BlockKind.Bouncer: sound.Play(Sfx.Rubber, volume, pitch, 0.7f); chip = Palette.RubberCap; break;
                    case BlockKind.Kicker: sound.Play(Sfx.Rubber, 0.9f, pitch * 1.25f, 0.8f); chip = Palette.Gold; break;
                    case BlockKind.Sticky: sound.Play(Sfx.Soft, volume, pitch, 0.6f); chip = Palette.Sticky; break;
                    case BlockKind.Crate: sound.Play(Sfx.Crate, volume, pitch, 0.6f); chip = new Color(0.85f, 0.62f, 0.38f); break;
                    case BlockKind.Breakable: sound.Play(Sfx.RimTouch, 0.3f + volume * 0.5f, pitch * 0.8f, 0.6f); chip = Themes.Hex(0xE6DCFF); break;
                    default: sound.Play(Sfx.Wood, volume, pitch, 0.6f); chip = new Color(0.95f, 0.8f, 0.58f); break;
                }
                Fx.Flash(info.ImpactPoint, new Color(1f, 0.97f, 0.85f, 0.5f + 0.4f * k), 0.35f + 0.6f * k);
                if (k > 0.15f) Fx.Chips(info.ImpactPoint, info.ImpactNormal, chip, 2 + Mathf.RoundToInt(5f * k), 2f + 4f * k);
                if (info.ImpactBlock != null) info.ImpactBlock.Wobble(0.4f + 3f * k);
                if (kind == BlockKind.Bouncer || kind == BlockKind.Kicker) TrackQuest(QuestKind.BumperHits);
                puck.Jiggle(info.ImpactNormal, vn);
                puck.AddSpin(SpinSign(info.ImpactNormal, vel) * info.ImpactTangent * 12f);
                mascot.OnImpact(info.ImpactNormal, vn);
                if (vn > 9f)
                {
                    sound.Play(Sfx.Boing, 0.5f, Random.Range(0.95f, 1.05f), 0.8f);
                    Fx.Burst(pos, Palette.Gold, 4, 2f, Textures.Star);
                }
                else if (vn > 5f && Time.time - lastSqueak > 0.5f)
                {
                    lastSqueak = Time.time;
                    sound.Play(Sfx.Squeak, 0.2f + 0.02f * vn, Random.Range(0.92f, 1.1f), 0.6f);
                }
                if (vn > 9f) Haptics.Play(Haptics.Style.Medium, 0.5f + 0.5f * k);
                else if (vn > 3f) Haptics.Play(Haptics.Style.Light, 0.3f + 0.5f * k);
                if (!reduced && vn > Rules.ShakeImpactSpeed) Kick(-info.ImpactNormal, 0.5f * k);
            }

            // Sustained contact along a wall or curve: a soft rub and a few gold flecks, never repeated bursts.
            if (info.RubSpeed > Rules.RubMinSpeed)
            {
                sound.SetRub(info.RubSpeed);
                if (info.RubSpeed > 3f) mascot.OnRide(info.RubNormal, info.RubSpeed);
                fleckTimer -= Time.fixedDeltaTime;
                if (fleckTimer <= 0f && info.RubSpeed > 3f)
                {
                    fleckTimer = 0.07f;
                    Fx.Flecks(info.RubPoint, info.RubNormal);
                }
            }
            else sound.SetRub(0f);

            if (info.SurfaceChanged && info.Surface == Surface.Ice)
            {
                sound.Play(Sfx.IceEnter, 0.4f + vel.magnitude / 40f);
                mascot.OnSurprise(0.6f);
                Fx.Ring(pos, new Color(0.85f, 0.95f, 1f, 0.8f), 0.4f, 1.6f, 0.3f);
            }
            if (info.SurfaceChanged && info.Surface == Surface.Mud)
            {
                sound.Play(Sfx.MudEnter, 0.6f);
                Fx.Puff(pos, Color.Lerp(Palette.Mud, Color.white, 0.4f), 5, 0.3f);
            }
            if (info.Boosted != null)
            {
                info.Boosted.Visual.Pulse();
                sound.Play(Sfx.Boost, 0.6f);
                Fx.Ring(pos, Palette.Boost, 0.5f, 2.2f, 0.35f);
                Haptics.Play(Haptics.Style.Light, 0.7f);
            }
            if (info.Teleported != null)
            {
                mascot.ResetTravel();
                trailTimer = 0f;
                Color c = Palette.Portal[(info.Teleported.Index / 2) % Palette.Portal.Length];
                sound.Play(Sfx.PortalIn, 0.7f);
                sound.Play(Sfx.PortalOut, 0.7f, 1f, 0.5f, 0f);
                Fx.Ring(info.TeleportFrom, c, 1.4f, 0.2f, 0.3f);
                Fx.Ring(pos, c, 0.3f, 2f, 0.35f);
                Fx.Burst(pos, c, 6, 3f, Textures.Glow);
                info.Teleported.Visual.Pulse();
                level.Zones[info.Teleported.Def.Link].Visual.Pulse();
                puck.JiggleVertical(3f);
                mascot.OnDizzyShort();
                Haptics.Play(Haptics.Style.Rigid, 0.7f);
            }
            if (info.Switched != null)
            {
                info.Switched.Visual.Pulse();
                sound.Play(Sfx.Switch, 0.8f, 1f, 0.8f);
                mascot.OnJoy(0.5f);
                Fx.Ring(info.Switched.Def.Center, Palette.GateColors[Mathf.Max(0, info.Switched.Def.Group) % Palette.GateColors.Length], 0.5f, 2f, 0.4f);
                Haptics.Play(Haptics.Style.Rigid);
            }
            if (info.Jumped != null)
            {
                sound.Play(Sfx.Boing, 0.6f, Random.Range(1.05f, 1.2f), 0.8f);
                info.Jumped.Visual?.Pulse();
                Fx.Ring(info.Jumped.Def.Center, new Color(0.8f, 0.7f, 1f, 0.9f), 0.5f, 2.2f, 0.35f);
                mascot.Express(Eyes.Happy, Mouth.Open, 0.7f, 2f);
            }
            if (info.TookOff)
            {
                sound.Play(Sfx.RampLaunch, 0.7f);
                mascot.Express(Eyes.Wide, Mouth.Open, 0.5f, 1.5f);
                sound.Play(Sfx.Whee, 0.3f, Random.Range(1f, 1.1f), 0.6f);
                Fx.Puff(pos, Color.white, 5, 0.3f);
                puck.JiggleVertical(-3f);
                Haptics.Play(Haptics.Style.Light);
            }
            if (info.Landed)
            {
                sound.Play(Sfx.Land, 0.9f, Random.Range(0.95f, 1.05f), 0.8f);
                mascot.OnJoy(0.5f);
                puck.JiggleVertical(5f);
                Fx.Ring(pos, new Color(1f, 1f, 1f, 0.8f), 0.5f, 2f, 0.35f);
                Fx.Puff(pos, new Color(1f, 0.95f, 0.85f), 6, 0.35f);
                if (!reduced) Kick(Vector2.down, 0.8f);
                Haptics.Play(Haptics.Style.Heavy, 0.6f);
            }
            if (info.RimTouch && info.Result != ShotResult.Sunk)
            {
                sound.Play(Sfx.RimTouch, 0.25f + Mathf.Clamp01(vel.magnitude / 15f) * 0.5f, Random.Range(0.95f, 1.08f));
                mascot.OnSurprise(0.45f);
            }

            if (info.Kicked)
            {
                // Kicker: a bright pop, a gold ring, and Puffy shot off with a "whee".
                sound.Play(Sfx.Boost, 0.7f, Random.Range(1.05f, 1.15f), 0.8f);
                sound.Play(Sfx.Whee, 0.3f, Random.Range(1.05f, 1.15f), 0.6f);
                if (info.ImpactBlock != null) info.ImpactBlock.Flash();
                Fx.Ring(info.ImpactPoint, new Color(1f, 0.8f, 0.3f, 0.95f), 0.4f, 2.6f, 0.3f);
                Fx.Burst(info.ImpactPoint, Palette.Kicker, 6, 4f, Textures.Glow);
                mascot.Express(Eyes.Wide, Mouth.Open, 0.6f, 3f);
                if (!reduced) Kick(-info.ImpactNormal, 0.35f);
                Haptics.Play(Haptics.Style.Heavy, 0.7f);
            }
            if (info.Swirled != null)
            {
                sound.Play(Sfx.MudEnter, 0.45f, 1.4f, 0.6f);
                Fx.Ring(pos, new Color(0.8f, 0.95f, 1f, 0.8f), 0.4f, 1.8f, 0.35f);
                mascot.OnDizzyShort();
            }

            if (info.Broke)
            {
                // Crystal shatters: a bright crack, a shower of shards, and a delighted face.
                sound.Play(Sfx.Crack, 0.85f, Random.Range(0.95f, 1.05f), 0.9f);
                Fx.Chips(info.ImpactPoint, -info.ImpactNormal, Themes.Hex(0xE6DCFF), 14, 7f);
                Fx.Chips(info.ImpactPoint, info.ImpactNormal, Color.white, 8, 5f);
                Fx.Burst(info.ImpactPoint, Themes.Hex(0xC9B6FF), 8, 4f, Textures.Glow);
                Fx.Ring(info.ImpactPoint, Color.white, 0.4f, 2.4f, 0.3f);
                if (!reduced) Kick(-info.ImpactNormal, 0.5f);
                Haptics.Play(Haptics.Style.Heavy, 0.8f);
                mascot.Express(Eyes.Happy, Mouth.Open, 0.7f, 3.5f);
            }

            if (info.StarCollected >= 0) CollectStar(info.StarCollected);
        }

        static float SpinSign(Vector2 normal, Vector2 vel) => Mathf.Sign(normal.x * vel.y - normal.y * vel.x);

        void CollectStar(int index)
        {
            var star = level.Stars[index];
            sound.Play(Sfx.StarNotes[Mathf.Min(starCombo, Sfx.StarNotes.Length - 1)], 0.7f, 1f, 0.9f, 0f);
            starCombo++;
            TrackQuest(QuestKind.CollectStars);
            mascot.Express(Eyes.Happy, Mouth.Grin, 0.6f, 2f);
            Haptics.Play(Haptics.Style.Light, 0.6f);
            // One clean grab: the cup hops up, tips toward the camera like a sip, and shrinks away in a puff of steam.
            var visual = star.Visual.transform;
            var bob = visual.Find("Bob");
            if (bob != null && bob.TryGetComponent(out Wiggle bobWiggle)) bobWiggle.enabled = false;
            Fx.Ring(star.Position, new Color(1f, 0.75f, 0.55f, 0.85f), 0.35f, 1.8f, 0.3f);
            Fx.Puff(star.Position, Color.white, 4, 0.2f);
            Tweens.Run(0.3f, k =>
            {
                if (bob == null) return;
                bob.localPosition = new Vector3(0f, 0f, -0.25f - 0.7f * Mathf.Sin(k * Mathf.PI * 0.5f));
                bob.localRotation = Quaternion.Euler(-35f * k, 0f, 0f);
                bob.localScale = Vector3.one * (k < 0.35f ? 1f + 0.5f * (k / 0.35f) : 1.5f * (1f - (k - 0.35f) / 0.65f));
            }, Tweens.OutCubic, 0f, () =>
            {
                if (bob != null)
                {
                    bob.localScale = Vector3.one;
                    bob.localRotation = Quaternion.identity;
                    bob.localPosition = new Vector3(0f, 0f, -0.25f);
                }
                visual.localScale = Vector3.one;
                star.Visual.SetActive(false);
            }, levelFx);
            Vector3 sp = cam.WorldToScreenPoint(new Vector3(star.Position.x, star.Position.y, -0.6f));
            int collected = level.StarsCollected, total = level.Stars.Count;
            hud.FlyStar(sp, () => hud.SetStars(collected, total, true));
        }

        // ---------- Outcomes ----------

        // Reaching the bed: the employee is guided onto the mattress, flops down on the back with the head on the
        // pillow, the blanket unfolds up to the chin, and a couple of sleepy Zs drift up. Then the results.
        IEnumerator CaptureRoutine()
        {
            state = State.Capturing;
            sound.StopContinuous();
            var puck = level.Puck;
            puck.Body.simulated = false;
            int earned = StarsFor(shots);
            bool reduced = progress.ReducedMotion;
            celebrating = true;
            mascot.Tumble = false;
            mascot.Express(Eyes.Wide, Mouth.O, 0.3f, 4f);
            Haptics.Play(Haptics.Style.Soft, 0.6f);

            var bed = level.Root.transform.Find("Bed");
            var blanket = bed != null ? bed.Find("Blanket") : null;
            Vector2 anchor = puck.Hole + new Vector2(0f, 0.18f);  // head on the pillow, feet toward the foot end
            Vector2 from = puck.transform.position;

            // Onto the bed with a little hop.
            float slide = reduced ? 0.18f : 0.3f;
            for (float t = 0f; t < slide; t += Time.deltaTime)
            {
                float k = t / slide;
                float e = 1f - (1f - k) * (1f - k);
                puck.transform.position = Vector2.Lerp(from, anchor, e);
                puck.HopLift = reduced ? 0f : 0.35f * 4f * k * (1f - k);
                yield return null;
            }
            puck.transform.position = anchor;
            puck.HopLift = 0f;
            puck.VisualDepth = 0.06f;  // sink into the mattress
            mascot.Sleep(true);
            sound.Play(Sfx.Pillow, 0.8f, 1f, 0.9f);
            puck.JiggleVertical(reduced ? 1f : 4f);
            Fx.Puff(anchor, Color.white, reduced ? 2 : 5, 0.25f);
            yield return new WaitForSeconds(reduced ? 0.15f : 0.35f);

            // The blanket unfolds over the body, up to the chin, leaving the face visible.
            sound.Play(Sfx.Swish, 0.6f, 1f, 0.8f);
            if (blanket != null)
            {
                Vector3 start = blanket.localScale, end = new Vector3(1f, 0.74f, 1f);
                float unfold = reduced ? 0.25f : 0.55f;
                for (float t = 0f; t < unfold; t += Time.deltaTime)
                {
                    float k = t / unfold;
                    float e = reduced ? k : Tweens.OutBack(k);
                    blanket.localScale = new Vector3(1f, Mathf.LerpUnclamped(start.y, end.y, e), Mathf.Lerp(start.z, end.z, Mathf.Min(1f, k * 1.6f)));
                    yield return null;
                }
                blanket.localScale = end;
                blanket.gameObject.AddComponent<Breathe>();
            }
            Haptics.Play(Haptics.Style.Success);
            sound.Play(Sfx.Reward, 0.55f, 1f, 1f);
            hud.Say(Quips.Rested(), 2.2f);
            var head = mascot.transform.position + new Vector3(0f, 0.38f, -0.5f);
            for (int i = 0; i < (reduced ? 1 : 2); i++)
            {
                FxDot.Spawn(head + new Vector3(0.15f * i, 0f, -0.1f * i), new Vector3(0.15f, 0.35f, -0.4f), 0.16f, 0.26f,
                    new Color(0.55f, 0.6f, 0.95f, 0.9f), 1.4f, FxMode.Drift, MascotArt.Z, 23);
                yield return new WaitForSeconds(0.25f);
            }

            if (levelIndex >= 0)
            {
                progress.Record(levelIndex, earned, shots);
                Analytics.Event("level_complete", ("level", Def.Id), ("index", levelIndex), ("shots", shots), ("par", Def.Par),
                    ("stars", earned), ("undos", undos), ("par_with_undo", undos > 0 && shots <= Def.Par), ("seconds", Mathf.RoundToInt(levelTime)));
                TrackQuest(QuestKind.FinishLevels);
                if (shots <= Def.Par) TrackQuest(QuestKind.ParFinishes);
                if (shots == 1) TrackQuest(QuestKind.HoleInOne);
            }
            yield return new WaitForSeconds(reduced ? 0.3f : 0.6f);
            state = State.Complete;
            sound.Play(Sfx.Complete, 0.6f, 1f, 1f);
            hud.ShowComplete(levelIndex + 1, earned, shots, Def.Par, level.Stars.Count > 0 && level.StarsCollected == level.Stars.Count, IsFinalLevel);
        }

        // The blanket rises and falls with slow sleepy breaths.
        class Breathe : MonoBehaviour
        {
            Vector3 rest;
            void Start() => rest = transform.localScale;
            void Update() => transform.localScale = new Vector3(rest.x, rest.y, rest.z * (1f + 0.04f * Mathf.Sin(Time.time * 1.6f)));
        }

        bool celebrating;

        // Hazards and gaps send the puck back to where the shot started. The shot still counts.
        // Falling keeps the momentum (Puffy tumbles over the edge and drops); spikes knock Puffy back with a hop
        // and a puff of smoke. Then Puffy pops back in at the last resting place.
        IEnumerator PenaltyRoutine(bool fell, Vector2 hitPoint)
        {
            state = State.Penalty;
            sound.StopContinuous();
            var puck = level.Puck;
            Vector2 pos = puck.Body.position;
            Vector2 vel = puck.Body.linearVelocity;
            puck.Body.simulated = false;
            bool reduced = progress.ReducedMotion;
            mascot.Tumble = true;
            sound.Play(Sfx.Oops, 0.4f, 1f, 0.8f);
            if (fell)
            {
                mascot.Express(Eyes.Wide, Mouth.O, 1.2f, 4f);
                sound.Play(Sfx.Fall, 0.7f, 1f, 0.9f);
                Fx.Puff(pos, new Color(1f, 0.95f, 0.85f), 6, 0.35f);
                for (float t = 0f; t < 0.55f; t += Time.deltaTime)
                {
                    float k = t / 0.55f;
                    vel *= Mathf.Exp(-1.2f * Time.deltaTime);
                    pos += vel * Time.deltaTime;
                    puck.transform.position = pos;
                    puck.VisualDepth = 0.5f * 14f * t * t;
                    puck.ScaleMul = 1f - 0.3f * k;
                    yield return null;
                }
            }
            else
            {
                mascot.Express(Eyes.Dizzy, Mouth.Wavy, 1.2f, 4f);
                sound.Play(Sfx.Hazard, 0.75f, 1f, 0.9f);
                Fx.Burst(hitPoint, Palette.Hazard, 10, 4f);
                Fx.Ring(hitPoint, Palette.Hazard, 0.4f, 2.4f, 0.35f);
                if (!reduced) Kick(Random.insideUnitCircle.normalized, 1.2f);
                Vector2 away = pos - hitPoint;
                away = away.sqrMagnitude > 1e-4f ? away.normalized : (vel.sqrMagnitude > 1e-4f ? -vel.normalized : Vector2.down);
                Vector2 start = pos;
                mascot.Twirl(540f, 0.45f);
                // Knock back with a little hop.
                for (float t = 0f; t < 0.34f; t += Time.deltaTime)
                {
                    float k = t / 0.34f;
                    puck.transform.position = start + away * (0.7f * (1f - (1f - k) * (1f - k)));
                    puck.VisualDepth = -0.55f * 4f * k * (1f - k);
                    yield return null;
                }
                puck.VisualDepth = 0f;
                puck.JiggleVertical(5f);
                // Poof.
                Fx.Puff(puck.transform.position, new Color(0.95f, 0.95f, 1f), 10, 0.5f);
                sound.Play(Sfx.Pop, 0.5f, 0.8f, 0.6f);
                for (float t = 0f; t < 0.16f; t += Time.deltaTime)
                {
                    puck.ScaleMul = 1f - t / 0.16f;
                    yield return null;
                }
            }
            Haptics.Play(Haptics.Style.Heavy, 0.7f);
            puck.ScaleMul = 0f;
            yield return new WaitForSeconds(0.2f);

            var snap = history.Peek();
            KillLevelFx();
            level.Restore(snap);
            levelTime = snap.Time;
            restPos = snap.Puck;
            mascot.Tumble = false;
            puck.PlaceAt(restPos, true);  // pops back in
            sound.Play(Sfx.Pop, 0.55f, 1.15f, 0.6f);
            Fx.Ring(restPos, Color.white, 0.3f, 1.6f, 0.3f);
            Fx.Burst(restPos, Palette.Gold, 5, 2.5f, Textures.Star);
            mascot.Express(Eyes.Wide, Mouth.O, 0.5f, 3f);
            hud.SetStars(level.StarsCollected, level.Stars.Count, false);
            hud.Banner(fell ? "OOPS!" : "OUCH!", new Color(1f, 0.85f, 0.8f), 0.3f);
            hud.Say(Quips.Fail(), 1.4f);
            state = State.Ready;
        }

        void UndoShot()
        {
            if (mascot != null) mascot.Tumble = false;
            if (state != State.Ready && state != State.Aiming && state != State.Moving && state != State.Penalty) return;
            if (history.Count == 0)
            {
                sound.Play(Sfx.Cancel, 0.4f);
                return;
            }
            StopAllCoroutines();
            KillLevelFx();
            var snap = history.Pop();
            level.Restore(snap);
            levelTime = snap.Time;
            shots = snap.Shots;
            undos++;
            restPos = snap.Puck;
            level.Puck.PlaceAt(restPos, true);
            sound.StopContinuous();
            sound.Play(Sfx.Undo, 0.6f);
            preview.Clear();
            hud.SetShots(shots, Def.Par);
            hud.SetStars(level.StarsCollected, level.Stars.Count, false);
            hud.SetUndoAvailable(history.Count > 0);
            state = State.Ready;
        }

        void PauseGame()
        {
            if (state == State.Paused || state == State.Title || state == State.Complete) return;
            CancelAim(false);
            stateBeforePause = state;
            state = State.Paused;
            Time.timeScale = 0f;
            sound.StopContinuous();
            Tap();
            hud.ShowPause();
        }

        void ResumeGame()
        {
            hud.HideAll();
            Time.timeScale = 1f;
            state = stateBeforePause == State.Aiming ? State.Ready : stateBeforePause;
            Tap();
        }

        bool SpikesUnder(Vector2 p)
        {
            foreach (var z in level.Zones)
                if (z.Kind == ZoneKind.Spikes && z.Contains(p) && z.SpikesUp(level.Time)) return true;
            return false;
        }

        // Puffy speaks at three moments only, so bubbles never sit over the board while the player plays (OUT-22):
        // a short opener when a room is first started, a line on a failure, and one in bed.
        void UpdateQuips()
        {
            if (level == null || state == State.Title) return;
            if (quipAt > 0f && Time.unscaledTime > quipAt && state == State.Ready)
            {
                quipAt = -1f;
                if (levelIndex >= 0 && levelIndex != openedLevel && shots == 0)
                {
                    openedLevel = levelIndex;
                    hud.Say(Quips.Opener(Def.Id), OpenerSeconds);
                }
            }
            var head = level.Puck.Visual.position + new Vector3(0f, 0f, -Mascot.BodyHalfHeight * 2f - 0.25f);
            Vector3 sp = cam.WorldToScreenPoint(head);
            hud.SetSpeechAnchor(sp, sp.z > 0f && state != State.Paused && state != State.Complete);
        }

        // ---------- Aim and trail visuals ----------

        void UpdateAim()
        {
            bool aiming = state == State.Aiming && power >= Rules.MinPower;
            var puck = level.Puck;

            if (state == State.Moving && releaseOffset != Vector2.zero)
            {
                releaseOffset *= Mathf.Exp(-30f * Time.deltaTime);
                if (releaseOffset.sqrMagnitude < 1e-6f) releaseOffset = Vector2.zero;
                puck.VisualOffset = releaseOffset;
            }
            else if (state != State.Moving) releaseOffset = Vector2.zero;

            // Power dots around the rest position (the band and the dots carry the power; the big ring is retired).
            aimRing.enabled = false;
            float ringR = 0.62f + 0.12f * power;
            Color gold = Color.Lerp(new Color(1f, 1f, 1f), new Color(1f, 0.78f, 0.25f), power);
            if (aiming)
            {
                aimRing.transform.position = new Vector3(restPos.x, restPos.y, -0.015f);
                aimRing.transform.localScale = Vector3.one * (ringR * 2f);
                aimRing.color = new Color(gold.r, gold.g, gold.b, 0.75f);
            }
            int lit = Mathf.RoundToInt(power * RingDotCount);
            for (int i = 0; i < ringDots.Count; i++)
            {
                var d = ringDots[i];
                d.enabled = aiming;
                if (!aiming) continue;
                float a = Mathf.PI / 2f - i * Mathf.PI * 2f / RingDotCount;
                d.transform.position = new Vector3(restPos.x + Mathf.Cos(a) * (ringR + 0.16f), restPos.y + Mathf.Sin(a) * (ringR + 0.16f), -0.016f);
                d.color = i < lit ? new Color(1f, 0.8f, 0.3f, 1f) : new Color(1f, 1f, 1f, 0.45f);
            }

            // Dotted line along the predicted path: it starts at the character, evenly spaced, longer with power,
            // shrinking and fading toward its end (sooner where the prediction is uncertain).
            int used = 0;
            if (aiming && preview.Count >= 2)
            {
                float total = 0f;
                for (int i = 0; i + 1 < preview.Count; i++) total += (preview[i + 1] - preview[i]).magnitude;
                float shown = Mathf.Min(total, Mathf.Lerp(2.6f, Rules.PreviewLength, power));
                float walked = 0f;
                for (int seg = 0; seg + 1 < preview.Count && used < aimDots.Count; seg++)
                {
                    Vector2 a = preview[seg], b = preview[seg + 1];
                    float len = (b - a).magnitude;
                    float first = seg == 0 ? Rules.PuckRadius + 0.3f : Mathf.Repeat(-walked, Rules.PreviewDot);
                    for (float d = first; d < len && walked + d <= shown && used < aimDots.Count; d += Rules.PreviewDot)
                    {
                        float along = (walked + d) / Mathf.Max(0.01f, shown);
                        float fade = previewResult.Uncertain ? 1f - along * along : 1f - Mathf.Pow(along, 3f) * 0.7f;
                        var dot = aimDots[used++];
                        Vector2 p = Vector2.Lerp(a, b, d / len);
                        dot.enabled = true;
                        dot.transform.position = new Vector3(p.x, p.y, -0.03f);
                        dot.transform.localScale = Vector3.one * Mathf.Lerp(0.27f, 0.15f, along);
                        dot.color = new Color(1f, 1f, 1f, Mathf.Clamp01(fade));
                    }
                    walked += len;
                }
            }
            for (int i = used; i < aimDots.Count; i++) aimDots[i].enabled = false;

            // A gold dot where the shot bounces, when the line reaches that far.
            pathEnd.enabled = false;
            bounceMark.enabled = aiming && previewResult.Bounced && preview.Count >= 3
                && previewResult.FirstLength <= Mathf.Lerp(2.6f, Rules.PreviewLength, power);
            if (bounceMark.enabled)
                bounceMark.transform.position = new Vector3(previewResult.BouncePoint.x, previewResult.BouncePoint.y, -0.035f);

            mascot.Mode = state == State.Aiming ? MascotMode.Aiming
                : (state == State.Capturing || state == State.Complete) && celebrating ? MascotMode.Idle
                : state == State.Moving || state == State.Capturing || state == State.Penalty ? MascotMode.Moving
                : MascotMode.Idle;
            if (state == State.Aiming)
            {
                mascot.AimDir = aimDir;
                mascot.AimPower = power;
                // Squashed back into the pull like a jelly, trembling a little at full power. Cosmetic only: the shot
                // starts from the rest position.
                float tremble = power > 0.75f && !progress.ReducedMotion ? Mathf.Sin(Time.time * 55f) * 0.02f * (power - 0.75f) * 4f : 0f;
                Vector2 pulled = -aimDir * (Mathf.Sqrt(power) * PullBack);
                puck.VisualOffset = pulled + new Vector2(-aimDir.y, aimDir.x) * tremble;
                puck.AimSquash = power * 0.08f;
                puck.AimAxis = aimDir;
                sling.Aim(restPos, aimDir, restPos + pulled - aimDir * (Rules.PuckRadius + 0.05f), power, Time.deltaTime);
            }
            else if (state == State.Ready)
            {
                puck.VisualOffset = Vector2.zero;
                puck.AimSquash = 0f;
            }
            if (state != State.Aiming) sling.Tick(Time.deltaTime);
        }

        // Wind around the rolling mascot: side streaks and soft air puffs behind it; pooled, never ahead of it.
        void UpdateTrail()
        {
            if (state != State.Moving || progress.ReducedMotion) { trailTimer = 0f; return; }
            var puck = level.Puck;
            Vector2 velocity = puck.Body.linearVelocity;
            float speed = velocity.magnitude;
            if (speed < Rules.TrailMinSpeed) { trailTimer = 0f; return; }
            trailTimer -= Time.deltaTime;
            if (trailTimer > 0f) return;
            trailTimer = Mathf.Lerp(0.075f, 0.035f, Mathf.Clamp01(speed / Rules.MaxSpeed));
            Vector2 direction = velocity / speed;
            Vector2 side = new Vector2(-direction.y, direction.x);
            Vector2 center = (Vector2)puck.transform.position - direction * (Rules.PuckRadius + 0.08f);
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            float strength = Mathf.Clamp01((speed - Rules.TrailMinSpeed) / 12f);
            float height = -puck.Lift - Mascot.BodyHalfHeight;
            // Wind: thin streaks that peel off both sides of the fur and slide back, like air rushing past.
            for (int i = -1; i <= 1; i += 2)
            {
                float jitter = Random.Range(-0.06f, 0.06f);
                Vector2 p = (Vector2)puck.transform.position + side * ((0.36f + jitter) * i) - direction * Random.Range(0.05f, 0.2f);
                FxDot.Spawn(new Vector3(p.x, p.y, height + Random.Range(-0.1f, 0.12f)), -(Vector3)direction * (1.2f + speed * 0.12f) + (Vector3)side * (0.25f * i),
                    0.05f + strength * 0.02f, 0.02f, new Color(1f, 1f, 1f, 0.35f + strength * 0.35f),
                    0.24f, FxMode.Flat, Textures.Glow, 7, 7f + 5f * strength, angle);
            }
            // A soft puff of air left behind on the board.
            if (Random.value < 0.35f + 0.4f * strength)
            {
                Vector2 p = center + side * Random.Range(-0.12f, 0.12f);
                FxDot.Spawn(new Vector3(p.x, p.y, -puck.Lift - 0.03f), -(Vector3)direction * 0.5f,
                    0.18f, 0.42f + 0.2f * strength, new Color(1f, 1f, 1f, 0.12f + 0.12f * strength), 0.35f, FxMode.Flat, Textures.Glow, 3);
            }
        }

        // ---------- Camera ----------

        // Pushes the camera with a spring, so the rare shakes are smooth and settle quickly.
        void Kick(Vector2 dir, float strength) => shakeVel += new Vector3(dir.x, dir.y, 0f) * strength;

        // ---- Follow camera ----
        // The board fills the screen width. A board taller than the view scrolls: the camera follows Puffy with a
        // little look-ahead, frames the way to the hole while Puffy rests, holds still while aiming (so the drag
        // maps to the board exactly), and watches the hole during the finish. A new level opens on the hole and
        // pans down to Puffy.
        float camDistance = 20f, camY, camYVel, camX, camXVel, camXMin, camXMax, camYMin, camYMax, introTime;
        bool camFits;
        Vector3? cameraOverride;  // test hook

        void LateUpdate()
        {
            var key = new Vector3Int(Screen.width, Screen.height, levelIndex + 2);
            if (key != fittedFor)
            {
                bool newLevel = fittedFor.z != key.z;
                fittedFor = key;
                FitCamera(newLevel);
            }
            float dt = Mathf.Min(Time.unscaledDeltaTime, 1f / 30f);
            shakeVel += (-260f * shakePos - 18f * shakeVel) * dt;
            shakePos += shakeVel * dt;
            UpdateCamera(dt);
            cam.transform.position = (cameraOverride ?? cameraRest) + shakePos;
            UpdateHoleMarker();
        }

        void UpdateCamera(float dt)
        {
            if (level == null) return;
            var puck = level.Puck;
            Vector2 p = puck.transform.position;
            Vector2 desired = new Vector2(camX, camY);
            float smooth = 0.42f;
            introTime -= dt;
            switch (state)
            {
                case State.Aiming:
                    camYVel = camXVel = 0f;
                    break;
                case State.Moving:
                case State.Penalty:
                {
                    Vector2 v = puck.Body.simulated ? puck.Body.linearVelocity : Vector2.zero;
                    desired = p + Vector2.ClampMagnitude(v * 0.28f, 2.5f);
                    smooth = 0.22f;
                    break;
                }
                case State.Capturing:
                case State.Complete:
                    desired = puck.Hole;
                    smooth = 0.3f;
                    break;
                case State.Title:
                    desired = level.Bounds.center;
                    break;
                default:
                    desired = introTime > 0.35f ? (Vector2)puck.Hole : p + Vector2.ClampMagnitude((puck.Hole - p) * 0.3f, 2.2f);
                    smooth = introTime > -0.6f ? 0.55f : 0.4f;
                    break;
            }
            desired.x = camXMin >= camXMax ? (camXMin + camXMax) * 0.5f : Mathf.Clamp(desired.x, camXMin, camXMax);
            desired.y = camFits ? (camYMin + camYMax) * 0.5f : Mathf.Clamp(desired.y, camYMin, camYMax);
            if (state != State.Aiming)
            {
                camY = Mathf.SmoothDamp(camY, desired.y, ref camYVel, smooth, Mathf.Infinity, dt);
                camX = Mathf.SmoothDamp(camX, desired.x, ref camXVel, smooth, Mathf.Infinity, dt);
            }
            cameraRest = new Vector3(camX, camY, 0f) - cam.transform.forward * camDistance;
        }

        // The view shows at most MaxViewWidth of floor across, so big maps scroll both ways to follow the shot.
        const float MaxViewWidth = 9.6f;

        // Distance so the board's width (up to MaxViewWidth) fills the screen, and how far the view may travel.
        void FitCamera(bool newLevel)
        {
            cam.transform.rotation = Quaternion.Euler(-CameraTilt, 0f, 0f);
            Vector3 forward = cam.transform.forward;
            Rect b = level.Bounds;
            // Fit the playable floor (not the rim and scenery) to the screen width: the rim may run off the edges.
            Rect floor = Geometry.Bounds(level.Def.Shape.Outline);
            float half = Mathf.Min(floor.width + 0.5f, MaxViewWidth) * 0.5f;
            float lo = 4f, hi = 150f;
            for (int i = 0; i < 32; i++)
            {
                float d = (lo + hi) * 0.5f;
                cam.transform.position = new Vector3(0f, 0f, 0f) - forward * d;
                float row = GroundAt(new Vector2(0.5f, 0.18f)).y;
                Vector3 left = cam.WorldToViewportPoint(new Vector3(-half, row, -Rules.WallHeight));
                Vector3 right = cam.WorldToViewportPoint(new Vector3(half, row, -Rules.WallHeight));
                if (left.x >= 0.005f && right.x <= 0.995f) hi = d; else lo = d;
            }
            camDistance = hi;
            camXMin = floor.xMin - 0.25f + half;
            camXMax = floor.xMax + 0.25f - half;

            // View edges relative to the view center, at this distance. The HUD bars may cover a strip of board.
            cam.transform.position = Vector3.zero - forward * camDistance;
            float bottom = GroundAt(new Vector2(0.5f, hud.BottomPixels / Screen.height * 0.6f)).y;
            float top = GroundAt(new Vector2(0.5f, 1f - hud.TopPixels / Screen.height * 0.75f)).y;
            const float margin = 0.25f;
            camYMin = b.yMin - margin - bottom;
            camYMax = b.yMax + margin - top;
            camFits = camYMin >= camYMax;
            if (newLevel)
            {
                Vector2 hole = level.Puck.Hole;
                camY = camFits ? (camYMin + camYMax) * 0.5f : Mathf.Clamp(hole.y, camYMin, camYMax);
                camX = camXMin >= camXMax ? (camXMin + camXMax) * 0.5f : Mathf.Clamp(hole.x, camXMin, camXMax);
                camYVel = camXVel = 0f;
                introTime = camFits && camXMin >= camXMax ? 0f : 1.1f;
            }
            UpdateCamera(0f);
        }

        // Where a viewport point lands on the board plane (z = 0).
        Vector3 GroundAt(Vector2 viewport)
        {
            Ray ray = cam.ViewportPointToRay(new Vector3(viewport.x, viewport.y, 0f));
            if (Mathf.Abs(ray.direction.z) < 1e-5f) return ray.origin;
            return ray.origin + ray.direction * (-ray.origin.z / ray.direction.z);
        }

        // A marker at the screen edge points at the bed while it is out of view.
        void UpdateHoleMarker()
        {
            if (level == null || state == State.Title || state == State.Complete || state == State.Capturing)
            {
                hud.SetHoleMarker(false, Vector2.zero, Vector2.zero);
                return;
            }
            Vector3 v = cam.WorldToViewportPoint(level.Puck.Hole);
            float top = 1f - hud.TopPixels / Screen.height, bottom = hud.BottomPixels / Screen.height;
            bool off = v.z < 0f || v.y > top - 0.02f || v.y < bottom + 0.02f || v.x < 0.02f || v.x > 0.98f;
            Vector2 screen = new Vector2(v.x * Screen.width, v.y * Screen.height);
            Vector2 center = new Vector2(Screen.width * 0.5f, (top + bottom) * 0.5f * Screen.height);
            hud.SetHoleMarker(off, screen, screen - center);
        }

        Vector2 ScreenToBoard(Vector2 screen)
        {
            Ray ray = cam.ScreenPointToRay(new Vector3(screen.x, screen.y, 0f));
            if (Mathf.Abs(ray.direction.z) < 1e-5f) return Vector2.zero;
            float t = -ray.origin.z / ray.direction.z;
            Vector3 p = ray.origin + ray.direction * t;
            return new Vector2(p.x, p.y);
        }
    }
}
