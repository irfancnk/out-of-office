using System.Runtime.InteropServices;
using UnityEngine;

namespace OutOfOffice
{
    // iPhone taptic feedback. Does nothing in the editor and on other platforms.
    public static class Haptics
    {
        public enum Style { Light = 0, Medium = 1, Heavy = 2, Soft = 3, Rigid = 4, Success = 5, Error = 6 }

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
            // Taps closer than 40 ms blur together, so skip them.
            if (Time.unscaledTime - last < 0.04f) return;
            last = Time.unscaledTime;
#if UNITY_IOS && !UNITY_EDITOR
            _OutOfOfficeHaptic((int)style, Mathf.Clamp01(intensity));
#endif
        }
    }
}
