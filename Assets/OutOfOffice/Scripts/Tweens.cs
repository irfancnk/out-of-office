using System;
using System.Collections.Generic;
using UnityEngine;

namespace OutOfOffice
{
    // Minimal tween runner on unscaled time. One shared component drives every tween.
    public class Tweens : MonoBehaviour
    {
        class Tween
        {
            public float Time, Duration, Delay;
            public Action<float> Step;
            public Func<float, float> Ease;
            public Action Done;
            public object Owner;
        }

        static Tweens runner;
        readonly List<Tween> active = new List<Tween>();

        public static void Run(float duration, Action<float> step, Func<float, float> ease = null, float delay = 0f,
            Action done = null, object owner = null)
        {
            if (runner == null) runner = new GameObject("Tweens").AddComponent<Tweens>();
            runner.active.Add(new Tween
            {
                Duration = Mathf.Max(0.0001f, duration), Delay = delay, Step = step, Ease = ease ?? OutCubic, Done = done, Owner = owner,
            });
            if (delay <= 0f) step(0f);
        }

        public static bool Running(object owner)
        {
            if (runner == null || owner == null) return false;
            foreach (var t in runner.active) if (t.Owner == owner) return true;
            return false;
        }

        // Jumps every running tween to its end (editor review shots, where Update does not run).
        public static void FinishAll()
        {
            if (runner == null) return;
            for (int guard = 0; guard < 8 && runner.active.Count > 0; guard++)
            {
                var all = new List<Tween>(runner.active);
                runner.active.Clear();
                foreach (var t in all)
                {
                    t.Step(t.Ease(1f));
                    t.Done?.Invoke();
                }
            }
        }

        // Stops every tween started with this owner, without calling Done.
        public static void Kill(object owner)
        {
            if (runner == null || owner == null) return;
            runner.active.RemoveAll(t => t.Owner == owner);
        }

        void Update()
        {
            float dt = UnityEngine.Time.unscaledDeltaTime;
            for (int i = active.Count - 1; i >= 0; i--)
            {
                if (i >= active.Count) continue;
                var t = active[i];
                if (t.Delay > 0f)
                {
                    t.Delay -= dt;
                    if (t.Delay > 0f) continue;
                }
                t.Time += dt;
                float k = Mathf.Clamp01(t.Time / t.Duration);
                try
                {
                    t.Step(t.Ease(k));
                    if (k >= 1f)
                    {
                        active.Remove(t);
                        t.Done?.Invoke();
                    }
                }
                catch (System.Exception e) when (e is MissingReferenceException || e is NullReferenceException)
                {
                    // Its object was destroyed (a level or panel rebuilt mid-tween): drop the tween quietly.
                    active.Remove(t);
                }
            }
        }

        public static float Linear(float x) => x;
        public static float OutCubic(float x) => 1f - Mathf.Pow(1f - x, 3f);
        public static float InOutSine(float x) => -(Mathf.Cos(Mathf.PI * x) - 1f) / 2f;

        public static float OutBack(float x)
        {
            const float c1 = 1.9f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }

        public static float OutElastic(float x)
        {
            if (x <= 0f || x >= 1f) return x;
            return Mathf.Pow(2f, -10f * x) * Mathf.Sin((x * 10f - 0.75f) * (2f * Mathf.PI / 3f)) + 1f;
        }
    }
}
