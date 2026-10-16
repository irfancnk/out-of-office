using System.Runtime.InteropServices;
using UnityEngine;

namespace OutOfOffice
{
    // Taptic feedback: the iPhone's Taptic Engine, and Android's own haptic feedback (OUT-18), which follows the phone's
    // touch-feedback setting and needs no vibrate permission. Settings scale it down or turn it off (OUT-16). Does
    // nothing in the editor.
    public static class Haptics
    {
        public enum Style { Light = 0, Medium = 1, Heavy = 2, Soft = 3, Rigid = 4, Success = 5, Error = 6 }

        // 0 off, 1 light, 2 full (the default). Set from the settings card.
        public static int Level = 2;

        static float last;

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern void _OutOfOfficeHaptic(int style, float intensity);

        [DllImport("__Internal")]
        static extern void _OutOfOfficeHapticPrepare();
#endif

        public static void Prepare()
        {
#if UNITY_IOS && !UNITY_EDITOR
            _OutOfOfficeHapticPrepare();
#endif
        }

        public static void Play(Style style, float intensity = 1f)
        {
            if (Level <= 0) return;
            // Taps closer than 40 ms blur together, so skip them.
            if (Time.unscaledTime - last < 0.04f) return;
            last = Time.unscaledTime;
            if (Level == 1)
            {
                // Light: everything a step softer, the heavy thumps down to medium.
                intensity *= 0.55f;
                if (style == Style.Heavy || style == Style.Error) style = Style.Medium;
            }
#if UNITY_IOS && !UNITY_EDITOR
            _OutOfOfficeHaptic((int)style, Mathf.Clamp01(intensity));
#elif UNITY_ANDROID && !UNITY_EDITOR
            AndroidPlay(AndroidEffect(style, intensity));
#endif
        }

        // Android's HapticFeedbackConstants for each style: the phone maker tunes these to its own motor.
        static int AndroidEffect(Style style, float intensity)
        {
            const int LongPress = 0, VirtualKey = 1, KeyboardTap = 3, ClockTick = 4, ContextClick = 6, Confirm = 16, Reject = 17;
            bool modern = AndroidSdk >= 30;  // CONFIRM and REJECT arrived in Android 11
            if (intensity < 0.3f) return ClockTick;
            switch (style)
            {
                case Style.Heavy: return LongPress;
                case Style.Medium: return VirtualKey;
                case Style.Rigid: return ContextClick;
                case Style.Success: return modern ? Confirm : VirtualKey;
                case Style.Error: return modern ? Reject : LongPress;
                case Style.Soft: return intensity > 0.65f ? VirtualKey : KeyboardTap;
                default: return intensity > 0.75f ? VirtualKey : KeyboardTap;
            }
        }

        static int sdk = -1;

        static int AndroidSdk
        {
            get
            {
#if UNITY_ANDROID && !UNITY_EDITOR
                if (sdk < 0)
                {
                    try { using (var version = new AndroidJavaClass("android.os.Build$VERSION")) sdk = version.GetStatic<int>("SDK_INT"); }
                    catch (System.Exception) { sdk = 26; }
                }
#endif
                return sdk;
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        static AndroidJavaObject activity, decorView;

        // performHapticFeedback must run on the UI thread, on a view of the window.
        static void AndroidPlay(int effect)
        {
            try
            {
                if (activity == null)
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                        activity = player.GetStatic<AndroidJavaObject>("currentActivity");
                activity.Call("runOnUiThread", new AndroidJavaRunnable(() =>
                {
                    try
                    {
                        if (decorView == null) decorView = activity.Call<AndroidJavaObject>("getWindow").Call<AndroidJavaObject>("getDecorView");
                        decorView.Call<bool>("performHapticFeedback", effect);
                    }
                    catch (System.Exception) { }
                }));
            }
            catch (System.Exception) { }  // haptics are a nicety: never let them break a frame
        }
#endif
    }
}
