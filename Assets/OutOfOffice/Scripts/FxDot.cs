using System.Collections.Generic;
using UnityEngine;

namespace OutOfOffice
{
    public enum FxMode
    {
        Flat,       // lies on the board
        Confetti,   // faces the camera, falls back to the board and bounces
        Float,      // faces the camera, drifts with no gravity
        Drift,      // faces the camera, keeps its speed and sways gently; fades in and out (ambient particles)
    }

    // A single fading sprite. Instances are pooled: nothing is created or destroyed during play.
    public class FxDot : MonoBehaviour
    {
        static readonly Stack<FxDot> pool = new Stack<FxDot>();
        static Transform poolRoot;
        public static Transform CameraTransform;

        SpriteRenderer sr;
        Vector3 velocity;
        Color color;
        float startSize, endSize, life, age, spin, stretch = 1f, angle;
        FxMode mode;

        // Creates the pool up front, so the first hit does not pay for allocation.
        public static void Prewarm(int count)
        {
            for (int i = 0; i < count; i++) Release(Create());
        }

        public static void Spawn(Vector3 pos, Vector3 velocity, float size, float endSize, Color color, float life,
            FxMode mode, Sprite sprite = null, int order = 6, float stretch = 1f, float angle = 0f)
        {
            var dot = pool.Count > 0 ? pool.Pop() : Create();
            if (dot == null) dot = Create();  // the pool root was destroyed with a scene
            dot.transform.position = pos;
            dot.transform.rotation = Quaternion.identity;
            dot.sr.sprite = sprite != null ? sprite : Shapes.Circle;
            dot.sr.sortingOrder = order;
            dot.velocity = velocity;
            dot.color = color;
            dot.startSize = size;
            dot.endSize = endSize;
            dot.life = life;
            dot.age = 0f;
            dot.mode = mode;
            dot.spin = mode == FxMode.Confetti ? Random.Range(-400f, 400f) : 0f;
            dot.stretch = stretch;
            dot.angle = angle;
            dot.gameObject.SetActive(true);
            dot.Apply(0f);
        }

        static FxDot Create()
        {
            if (poolRoot == null)
            {
                poolRoot = new GameObject("Fx Pool").transform;
                pool.Clear();
            }
            var go = new GameObject("Fx");
            go.transform.SetParent(poolRoot, false);
            var dot = go.AddComponent<FxDot>();
            dot.sr = go.AddComponent<SpriteRenderer>();
            dot.sr.sprite = Shapes.Circle;
            return dot;
        }

        static void Release(FxDot dot)
        {
            dot.gameObject.SetActive(false);
            pool.Push(dot);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            age += dt;
            if (age >= life)
            {
                Release(this);
                return;
            }
            if (mode == FxMode.Drift) velocity.x += Mathf.Sin(age * 2.2f + startSize * 40f) * 0.4f * dt;
            else velocity *= 1f / (1f + 3f * dt);
            if (mode == FxMode.Confetti) velocity.z += 12f * dt;  // gravity pulls toward the board (+Z)
            var p = transform.position + velocity * dt;
            if (mode == FxMode.Confetti && p.z > -0.05f && velocity.z > 0f)
            {
                p.z = -0.05f;
                velocity.z *= -0.4f;
            }
            transform.position = p;
            Apply(age / life);
        }

        void Apply(float k)
        {
            float ease = 1f - (1f - k) * (1f - k);
            float size = Mathf.Lerp(startSize, endSize, ease);
            transform.localScale = new Vector3(size * stretch, size, 1f);
            float alpha = mode == FxMode.Drift ? Mathf.Sin(Mathf.PI * k) : 1f - k * k;
            sr.color = new Color(color.r, color.g, color.b, color.a * alpha);
            if (mode == FxMode.Flat && angle != 0f) transform.rotation = Quaternion.Euler(0f, 0f, angle);
            if (mode != FxMode.Flat && CameraTransform != null)
                transform.rotation = CameraTransform.rotation * Quaternion.Euler(0f, 0f, spin * age);
        }
    }

    public static class Fx
    {
        // Reduced-motion and intensity settings scale how many particles spawn.
        public static float Amount = 1f;

        static int Count(int n) => Mathf.Max(1, Mathf.RoundToInt(n * Amount));

        // A short bright flash at a contact point.
        public static void Flash(Vector2 pos, Color color, float size)
        {
            FxDot.Spawn(new Vector3(pos.x, pos.y, -0.3f), Vector3.zero, size, size * 1.4f, color, 0.14f, FxMode.Float, Textures.Glow, 8);
        }

        // Material-appropriate bits flying off a hit: wood chips, rubber specks, jelly drops.
        public static void Chips(Vector2 point, Vector2 normal, Color color, int count, float speed)
        {
            count = Count(count);
            for (int i = 0; i < count; i++)
            {
                Vector2 dir = (normal + Random.insideUnitCircle * 0.8f).normalized * (speed * Random.Range(0.4f, 1f));
                var v = new Vector3(dir.x, dir.y, -Random.Range(1.5f, 4f));
                FxDot.Spawn(new Vector3(point.x, point.y, -0.25f), v, Random.Range(0.06f, 0.12f), 0f, color,
                    Random.Range(0.3f, 0.5f), FxMode.Confetti, Shapes.Square);
            }
        }

        // A couple of tiny gold flecks at a sliding contact. Called often, so it stays small.
        public static void Flecks(Vector2 point, Vector2 normal)
        {
            for (int i = 0; i < 2; i++)
            {
                Vector2 dir = (normal * 0.6f + Random.insideUnitCircle * 0.6f) * 1.5f;
                FxDot.Spawn(new Vector3(point.x, point.y, -0.2f), new Vector3(dir.x, dir.y, -Random.Range(0.3f, 1f)),
                    Random.Range(0.05f, 0.09f), 0f, new Color(1f, 0.85f, 0.4f, 0.95f), Random.Range(0.25f, 0.4f), FxMode.Float, Textures.Glow, 7);
            }
        }

        // Confetti that pops up off the board and falls back.
        public static void Burst(Vector2 pos, Color color, int count, float speed, Sprite sprite = null)
        {
            count = Count(count);
            for (int i = 0; i < count; i++)
            {
                Vector2 dir = Random.insideUnitCircle.normalized * (speed * Random.Range(0.4f, 1f));
                var v = new Vector3(dir.x, dir.y, -Random.Range(3f, 7f));
                float size = Random.Range(0.14f, 0.3f);
                FxDot.Spawn(new Vector3(pos.x, pos.y, -0.3f), v, size, 0f, color, Random.Range(0.6f, 1f), FxMode.Confetti, sprite);
            }
        }

        // Sparks flying off a hit point, mostly along the surface normal.
        public static void Sparks(Vector2 point, Vector2 normal, Color color, int count, float speed)
        {
            count = Count(count);
            for (int i = 0; i < count; i++)
            {
                Vector2 dir = (normal + Random.insideUnitCircle * 0.9f).normalized * (speed * Random.Range(0.5f, 1f));
                var v = new Vector3(dir.x, dir.y, -Random.Range(1f, 4f));
                FxDot.Spawn(new Vector3(point.x, point.y, -0.25f), v, Random.Range(0.08f, 0.16f), 0f, color,
                    Random.Range(0.25f, 0.45f), FxMode.Confetti);
            }
        }

        // Expanding shockwave ring on the board.
        public static void Ring(Vector2 pos, Color color, float from, float to, float life, float height = -0.03f)
        {
            FxDot.Spawn(new Vector3(pos.x, pos.y, height), Vector3.zero, from, to, color, life, FxMode.Flat, Shapes.Ring, 5);
        }

        // Soft puffs that drift up and fade.
        public static void Puff(Vector2 pos, Color color, int count, float spread)
        {
            count = Count(count);
            for (int i = 0; i < count; i++)
            {
                Vector2 d = Random.insideUnitCircle * spread;
                var v = new Vector3(d.x * 2f, d.y * 2f, -Random.Range(0.8f, 1.8f));
                FxDot.Spawn(new Vector3(pos.x + d.x, pos.y + d.y, -0.2f), v, Random.Range(0.22f, 0.36f), Random.Range(0.5f, 0.8f),
                    color, Random.Range(0.4f, 0.7f), FxMode.Float, Textures.Glow, 7);
            }
        }

        // A flat streak on the board, for trails and speed lines.
        public static void Streak(Vector2 pos, Color color, float size, float life)
        {
            FxDot.Spawn(new Vector3(pos.x, pos.y, -0.012f), Vector3.zero, size, size * 0.3f, color, life, FxMode.Flat, null, 3);
        }
    }
}
