using UnityEngine;

namespace OutOfOffice
{
    // Runtime data for a floor zone. Zones are static, so the containment test is pure geometry.
    public class ZoneInstance
    {
        public readonly ZoneDef Def;
        public readonly int Index;
        public ZoneVisual Visual;
        public bool Pressed;  // switches only
        public LevelInstance Level;

        readonly Vector2 dir, right;

        public ZoneInstance(ZoneDef def, int index)
        {
            Def = def;
            Index = index;
            dir = def.Direction;
            right = new Vector2(dir.y, -dir.x);
        }

        public ZoneKind Kind => Def.Kind;
        public Vector2 Direction => dir;

        public bool Contains(Vector2 p)
        {
            Vector2 d = p - Def.Center;
            if (Def.Kind == ZoneKind.Portal || Def.Kind == ZoneKind.Switch) return d.sqrMagnitude < Sq(Def.Size.x * 0.5f - 0.1f);
            if (Def.Kind == ZoneKind.Magnet || Def.Kind == ZoneKind.Vortex || Def.Kind == ZoneKind.Jump) return d.sqrMagnitude < Sq(Def.Size.x * 0.5f);
            return Mathf.Abs(Vector2.Dot(d, right)) <= Def.Size.x * 0.5f && Mathf.Abs(Vector2.Dot(d, dir)) <= Def.Size.y * 0.5f;
        }

        // Timed staples stay down through a Coffee Break (OUT-51); a strip with no period is always up and stays so.
        bool Resting => Level != null && Level.OnBreak && Def.Period > 0f;

        // Pop-up spikes: 1 fully up, 0 fully down, from the level clock. Always up without a period.
        public float SpikeHeight(float time)
        {
            if (Def.Period <= 0f) return 1f;
            if (Resting) return 0f;
            float k = Mathf.Repeat(time / Def.Period + Def.Phase, 1f) * Def.Period;  // seconds into the cycle
            float up = Def.Period * Rules.SpikeDuty;
            const float rise = 0.12f;
            if (k < up) return Mathf.Clamp01(k / rise) * Mathf.Clamp01((up - k) / rise + 1f);
            return 0f;
        }

        public bool SpikesUp(float time) => Def.Period <= 0f || !Resting && Mathf.Repeat(time / Def.Period + Def.Phase, 1f) < Rules.SpikeDuty;

        // Seconds until the spikes rise next (0 while up, or while resting).
        public float UntilRise(float time)
        {
            if (Def.Period <= 0f || Resting || SpikesUp(time)) return 0f;
            return (1f - Mathf.Repeat(time / Def.Period + Def.Phase, 1f)) * Def.Period;
        }

        // 0 at the back edge, 1 at the front edge (along Direction).
        public float Progress(Vector2 p) => Vector2.Dot(p - Def.Center, dir) / Def.Size.y + 0.5f;

        public Rect Bounds
        {
            get
            {
                float a = Def.Angle * Mathf.Deg2Rad;
                float c = Mathf.Abs(Mathf.Cos(a)), s = Mathf.Abs(Mathf.Sin(a));
                var ext = new Vector2(Def.Size.x * c + Def.Size.y * s, Def.Size.x * s + Def.Size.y * c) * 0.5f;
                return new Rect(Def.Center - ext, ext * 2f);
            }
        }

        static float Sq(float x) => x * x;
    }
}
