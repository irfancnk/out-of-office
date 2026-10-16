using Unity.Profiling;
using UnityEngine;

namespace OutOfOffice
{
    // Frame timing on development builds for phones (OUT-36): every hitch is logged with the screen it happened on
    // and what ran that frame, plus a summary every ten seconds. Read it with: adb logcat -s Unity | grep HSPERF
    [DefaultExecutionOrder(-1000)]
    public class FrameWatch : MonoBehaviour
    {
        const float HitchMs = 40f, SlowMs = 25f, SummarySeconds = 10f;

        static FrameWatch instance;
        static string notes;
        System.Func<string> context;
        ProfilerRecorder allocated;
        int collections, frames, slow, hitches, summaryCollections;
        float total, worst, summaryAt;

        public static void Attach(GameObject host, System.Func<string> context)
        {
            if (Application.isEditor || !Debug.isDebugBuild || instance != null) return;
            instance = host.AddComponent<FrameWatch>();
            instance.context = context;
        }

        public static bool Active => instance != null;

        // Marks work done this frame, so a hitch can name its cause.
        public static void Note(string what)
        {
            if (instance == null || (notes != null && notes.Length > 120)) return;
            notes = notes == null ? what : notes + " " + what;
        }

        void OnEnable()
        {
            allocated = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            collections = System.GC.CollectionCount(0);
            summaryAt = Time.realtimeSinceStartup + SummarySeconds;
        }

        void OnDisable() => allocated.Dispose();

        // Runs first in each frame, so the time since the last frame covers the work noted during it.
        void Update()
        {
            float ms = Time.unscaledDeltaTime * 1000f;
            int gc = System.GC.CollectionCount(0);
            if (Time.frameCount > 60 && ms < 1000f)
            {
                frames++;
                total += ms;
                worst = Mathf.Max(worst, ms);
                if (ms > SlowMs) slow++;
                if (ms > HitchMs)
                {
                    hitches++;
                    long kb = allocated.Valid ? allocated.LastValue / 1024 : -1;
                    Debug.Log($"HSPERF hitch {ms:0}ms on={context?.Invoke()} gc={gc - collections} alloc={kb}KB {notes}");
                }
            }
            if (gc != collections) summaryCollections += gc - collections;
            collections = gc;
            notes = null;

            if (Time.realtimeSinceStartup < summaryAt) return;
            summaryAt = Time.realtimeSinceStartup + SummarySeconds;
            if (frames > 0)
                Debug.Log($"HSPERF summary frames={frames} avg={total / frames:0.0}ms worst={worst:0}ms over{SlowMs:0}ms={slow} hitches={hitches} gcs={summaryCollections} on={context?.Invoke()} target={Application.targetFrameRate}");
            frames = slow = hitches = summaryCollections = 0;
            total = worst = 0f;
        }
    }
}
