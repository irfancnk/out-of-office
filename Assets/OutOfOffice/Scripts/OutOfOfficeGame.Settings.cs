using UnityEngine;

namespace OutOfOffice
{
    // Settings and device quality (OUT-16, OUT-19): volumes, haptics strength, the graphics tier, battery saver, the
    // phone's heat, and the frame rate those allow.
    public partial class OutOfOfficeGame
    {
        int maxFps = 60;          // 120 where the screen and the tier allow it; frame pacing may settle it on 60
        float nextDevicePoll;

        void SetUpQuality()
        {
            Quality.Start(progress.GraphicsPick);
            Haptics.Level = progress.HapticsLevel;
            ApplyQuality();
            Analytics.Event("quality", ("tier", Quality.Current.ToString()), ("detected", Quality.Detected.ToString()), ("pick", progress.GraphicsPick),
                ("gpu", SystemInfo.graphicsDeviceName), ("memory", SystemInfo.systemMemorySize), ("cores", SystemInfo.processorCount),
                ("refresh", (float)Screen.currentResolution.refreshRateRatio.value));
        }

        // The tier's frame rate ceiling and effect density. The render settings themselves are in Quality.
        void ApplyQuality()
        {
            bool fastScreen = Screen.currentResolution.refreshRateRatio.value >= 90.0;
            maxFps = fastScreen && Quality.Now.HighRefresh && !paceLocked ? 120 : 60;
            if (sound != null) ApplyMotionSetting(progress.ReducedMotion);
        }

        void WireSettings()
        {
            hud.SetSfxVolume = (v, done) =>
            {
                sound.SfxVolume = v;
                progress.SfxVolume = v;
                if (v > 0.01f && !progress.Sfx) { progress.Sfx = true; sound.SetEnabled(true, progress.Music); hud.UpdateSettings(progress.Sfx, progress.Music, progress.ReducedMotion); }
                if (done) { progress.SaveSettings(); Tap(); }
            };
            hud.SetMusicVolume = (v, done) =>
            {
                sound.MusicVolume = v;
                progress.MusicVolume = v;
                if (v > 0.01f && !progress.Music) { progress.Music = true; sound.SetEnabled(progress.Sfx, true); hud.UpdateSettings(progress.Sfx, progress.Music, progress.ReducedMotion); }
                if (done) progress.SaveSettings();
            };
            hud.SetHaptics = level =>
            {
                progress.HapticsLevel = level;
                Haptics.Level = level;
                Tap();
                Haptics.Play(Haptics.Style.Medium);  // feel the new setting
                RefreshSettings();
            };
            hud.SetGraphics = pick =>
            {
                progress.GraphicsPick = pick;
                Quality.Choose(pick);
                ApplyQuality();
                Tap();
                Analytics.Event("graphics_pick", ("pick", pick), ("tier", Quality.Current.ToString()));
                RefreshSettings();
            };
            hud.SetBatterySaver = on =>
            {
                progress.BatterySaver = on;
                Tap();
                RefreshSettings();
            };
        }

        void RefreshSettings()
        {
            hud.UpdateSettings(progress.Sfx, progress.Music, progress.ReducedMotion);
            string note = progress.GraphicsPick < 0
                ? $"Auto picked {Quality.Detected.ToString().ToUpperInvariant()} for this phone."
                : $"On its own this phone would pick {Quality.Detected.ToString().ToUpperInvariant()}.";
            if (Quality.Heat > 0) note += " Cooling down: a step lower for now.";
            hud.UpdateMoreSettings(progress.SfxVolume, progress.MusicVolume, progress.HapticsLevel, progress.GraphicsPick, progress.BatterySaver, note);
        }

        // Every frame: the phone's heat now and then, and the frame rate the moment calls for. Menus drop to 30 fps
        // with battery saver (the player's, or the phone's own power saving) and when the phone runs hot.
        void TickDevice()
        {
            if (Time.unscaledTime >= nextDevicePoll)
            {
                nextDevicePoll = Time.unscaledTime + 10f;
                DeviceState.Poll();
                if (Quality.SetHeat(DeviceState.ThermalStatus))
                {
                    ApplyQuality();
                    if (hud.PanelOpen) RefreshSettings();
                }
            }
            int want = Quality.Heat > 0 ? Mathf.Min(maxFps, 60) : maxFps;
            bool menus = state == State.Title || state == State.Paused || state == State.Complete || state == State.Failed || hud.CoversBoard;
            if (menus && (progress.BatterySaver || DeviceState.PowerSave || Quality.Heat >= 2)) want = 30;
            if (Application.targetFrameRate != want) Application.targetFrameRate = want;
        }
    }
}
