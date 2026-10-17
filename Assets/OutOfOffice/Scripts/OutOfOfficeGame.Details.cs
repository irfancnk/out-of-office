using UnityEngine;

namespace OutOfOffice
{
    // Small details (OUT-55): the bed perks up as the employee rolls close, with sleepy Z's drifting off it, and a desk
    // slammed hard sheds a few papers. All cosmetic; Reduced Motion keeps them quiet.
    public partial class OutOfOfficeGame
    {
        Transform restSign;        // the "Z" over the bed's pillow
        LevelInstance restLevel;   // the room restSign belongs to
        float restNear, nextSleepyZ;

        void TickDetails()
        {
            if (level == null) return;
            if (restLevel != level)
            {
                restLevel = level;
                restSign = null;
                foreach (var sr in level.Root.GetComponentsInChildren<SpriteRenderer>(true))
                    if (sr.name == "Rest") { restSign = sr.transform; break; }
            }
            if (restSign == null) return;
            float near = 0f;
            if (state == State.Moving)
            {
                float d = ((Vector2)level.Puck.transform.position - Def.Hole).magnitude;
                near = Mathf.Clamp01(1f - (d - 0.8f) / 2.4f);
            }
            restNear = Mathf.MoveTowards(restNear, near, Time.deltaTime * 3f);
            restSign.localScale = Vector3.one * (0.26f * (1f + 0.9f * restNear));
            if (restNear > 0.35f && Time.time > nextSleepyZ && Fx.Amount > 0.6f)
            {
                nextSleepyZ = Time.time + 0.3f;
                var p = restSign.position;
                FxDot.Spawn(p + new Vector3(Random.Range(-0.1f, 0.1f), 0f, -0.05f), new Vector3(Random.Range(0.1f, 0.3f), 0.25f, -0.5f),
                    0.14f, 0.24f, new Color(0.45f, 0.5f, 0.9f, 0.85f), 1.1f, FxMode.Drift, MascotArt.Z, 23);
            }
        }

        // A hard knock on a desk: a few sheets of paper flutter up off it and fall back.
        void ShedPapers(Vector2 point, Vector2 normal, float k)
        {
            int n = Mathf.RoundToInt(Mathf.Lerp(1f, 3f, k) * Fx.Amount);
            for (int i = 0; i < n; i++)
            {
                Vector2 d = (-normal * 0.4f + Random.insideUnitCircle * 0.8f) * Random.Range(0.8f, 1.6f);
                FxDot.Spawn(new Vector3(point.x - normal.x * 0.3f, point.y - normal.y * 0.3f, -0.6f), new Vector3(d.x, d.y, -Random.Range(2.5f, 4.5f)),
                    Random.Range(0.2f, 0.26f), 0.18f, new Color(0.98f, 0.97f, 0.93f, 1f), Random.Range(0.9f, 1.3f), FxMode.Confetti, Shapes.Square, 7, 0.75f);
            }
        }
    }
}
