using UnityEngine;

namespace OutOfOffice
{
    // Swings and/or spins a kinematic block. The owner calls Tick, so the level check can drive it too.
    public class Mover : MonoBehaviour
    {
        Rigidbody2D body;
        BlockDef def;

        public void Init(Rigidbody2D rb, BlockDef block)
        {
            body = rb;
            def = block;
        }

        Vector2 position, velocity;

        // Velocity of a point on this block, from its motion definition (Box2D clears a kinematic body's
        // velocity after each step, so it can not be read back afterwards).
        public Vector2 PointVelocity(Vector2 point)
        {
            Vector2 r = point - position;
            float w = def.Spin * Mathf.Deg2Rad;
            return velocity + new Vector2(-r.y, r.x) * w;
        }

        public void Tick(float time, bool snap)
        {
            Vector2 p = def.Center;
            velocity = Vector2.zero;
            if (def.Period > 0f)
            {
                float w = Mathf.PI * 2f / def.Period;
                p += def.MoveOffset * Mathf.Sin(time * w + def.Phase);
                velocity = def.MoveOffset * (Mathf.Cos(time * w + def.Phase) * w);
            }
            position = p;
            float angle = def.Angle + def.Spin * time;
            if (snap)
            {
                body.position = p;
                body.rotation = angle;
                transform.SetPositionAndRotation(p, Quaternion.Euler(0f, 0f, angle));
            }
            else
            {
                body.MovePositionAndRotation(p, angle);
            }
        }
    }
}
