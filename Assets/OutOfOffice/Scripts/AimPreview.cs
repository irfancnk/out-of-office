using System.Collections.Generic;
using UnityEngine;

namespace OutOfOffice
{
    // Predicts the start of a shot with the same collision shapes, surface resistance, and materials as gameplay:
    // a straight segment, then at most one bounce off a static obstacle.
    // The prediction stops early where it cannot be trusted: moving parts, crates, and zones that change the path.
    public static class AimPreview
    {
        public struct Result
        {
            public bool Bounced;
            public Vector2 BouncePoint;    // puck center at the first contact
            public float FirstLength;      // length of the first segment
            public bool Uncertain;         // the path ends at something the preview cannot follow
        }

        static readonly RaycastHit2D[] hits = new RaycastHit2D[8];

        // Distance the puck travels on wood from a given speed, stepping exactly like the game's fixed step.
        public static float GlideDistance(float speed, float limit)
        {
            float d = 0f, dt = Rules.Dt;
            var s = Rules.Wood;
            while (speed > Rules.RestSpeed && d < limit)
            {
                // Game order per fixed step: Puck.Step applies the rolling decel, then Box2D applies drag and moves.
                speed = Mathf.Max(0f, speed - s.Decel * dt);
                speed /= 1f + dt * s.Drag;
                d += speed * dt;
            }
            return d;
        }

        // Speed left after gliding a distance on wood.
        public static float SpeedAfter(float speed, float distance)
        {
            float d = 0f, dt = Rules.Dt;
            var s = Rules.Wood;
            while (speed > 0f && d < distance)
            {
                speed = Mathf.Max(0f, speed - s.Decel * dt);
                speed /= 1f + dt * s.Drag;
                d += speed * dt;
            }
            return speed;
        }

        public static Result Compute(LevelInstance level, Vector2 origin, Vector2 velocity, List<Vector2> path)
        {
            path.Clear();
            var result = new Result();
            float speed = velocity.magnitude;
            if (speed < 0.01f) return result;
            Vector2 dir = velocity / speed;
            float budget = Mathf.Min(Rules.PreviewLength, GlideDistance(speed, Rules.PreviewLength));

            path.Add(origin);
            float first = Cast(level, origin, dir, budget, out var hit, out bool blocked);
            first = ClipAtZone(level, origin, dir, first, ref result);
            Vector2 p1 = origin + dir * first;
            path.Add(p1);
            result.FirstLength = first;
            if (result.Uncertain || hit.collider == null || first < budget * 0.999f && hit.distance > first + 1e-3f) return result;
            if (blocked)
            {
                result.Uncertain = true;
                return result;
            }

            // One bounce using the obstacle's restitution; shallow contacts slide instead of bouncing.
            float vAtHit = SpeedAfter(speed, first);
            Vector2 v = dir * vAtHit;
            Vector2 n = hit.normal;
            float vn = Vector2.Dot(v, n);
            if (vn >= 0f) return result;
            float e = hit.collider.sharedMaterial != null ? hit.collider.sharedMaterial.bounciness : 0f;
            // Same contact rules as the simulation: glancing wood contacts slide, others rebound by restitution.
            hit.collider.TryGetComponent(out BlockTag tag);
            if (tag != null && tag.Kind == BlockKind.Kicker)
            {
                // A kicker fires the puck off at its own speed: show the way in, not a guessed way out.
                result.Uncertain = true;
                return result;
            }
            bool wood = tag == null || tag.Kind == BlockKind.Wall || tag.Kind == BlockKind.Gate;
            float tangential = (v - vn * n).magnitude;
            bool glancing = wood && -vn <= Rules.RideNormalMax && -vn <= Rules.GlideRatio * tangential;
            // Right at the glide/bounce threshold a tiny speed or angle change flips the outcome: do not guess.
            if (wood && -vn <= Rules.RideNormalMax * 1.1f && Mathf.Abs(-vn / Mathf.Max(tangential, 1e-3f) - Rules.GlideRatio) < 0.1f)
            {
                result.Uncertain = true;
                return result;
            }
            Vector2 after = !glancing && -vn > Rules.BounceThreshold ? v - (1f + e) * vn * n : v - vn * n;
            // Contact friction (Box2D mixes frictions as sqrt(puck * obstacle); the puck's is 1) slows the tangential part.
            float mu = hit.collider.sharedMaterial != null ? Mathf.Sqrt(hit.collider.sharedMaterial.friction) : 0f;
            Vector2 tangent = v - vn * n;
            float tSpeed = tangent.magnitude;
            if (tSpeed > 1e-4f)
            {
                float impulse = glancing || -vn <= Rules.BounceThreshold ? -vn : -(1f + e) * vn;
                after -= tangent / tSpeed * Mathf.Min(tSpeed, mu * impulse);
            }
            float afterSpeed = after.magnitude;
            if (afterSpeed < 0.3f) return result;

            result.Bounced = true;
            result.BouncePoint = p1;
            Vector2 dir2 = after / afterSpeed;
            float budget2 = Mathf.Min(Rules.PreviewLength - first, GlideDistance(afterSpeed, Rules.PreviewLength)) * 0.7f;
            if (budget2 < 0.05f) return result;
            float second = Cast(level, p1 + n * 0.01f, dir2, budget2, out _, out _);
            second = ClipAtZone(level, p1, dir2, second, ref result);
            path.Add(p1 + dir2 * second);
            return result;
        }

        // Sweeps the puck's circle along a direction. Returns the free distance before contact.
        static float Cast(LevelInstance level, Vector2 from, Vector2 dir, float max, out RaycastHit2D hit, out bool blocked)
        {
            hit = default;
            blocked = false;
            var filter = new ContactFilter2D { useTriggers = false };
            int n = Physics2D.CircleCast(from, Rules.PuckRadius * 0.999f, dir, filter, hits, max);
            float best = max;
            for (int i = 0; i < n; i++)
            {
                var h = hits[i];
                if (h.collider == level.Puck.Collider) continue;
                if (h.collider.usedByEffector) continue;          // one-way ramp edges
                if (h.distance <= 1e-4f)
                {
                    // Already touching: only blocks the preview when aiming into it.
                    if (Vector2.Dot(dir, h.normal) < -0.1f && best > 0f)
                    {
                        best = 0f;
                        hit = h;
                    }
                    continue;
                }
                if (h.distance < best)
                {
                    best = h.distance;
                    hit = h;
                    // Moving parts and crates make anything past the contact a guess.
                    var rb = h.collider.attachedRigidbody;
                    blocked = rb != null && rb.bodyType != RigidbodyType2D.Static;
                }
            }
            return best;
        }

        // Stops the preview where it enters a zone that changes the motion (pads, ramps, portals, magnets, gaps, ice, mud).
        static float ClipAtZone(LevelInstance level, Vector2 from, Vector2 dir, float length, ref Result result)
        {
            const float step = 0.12f;
            for (float d = step; d < length; d += step)
            {
                Vector2 p = from + dir * d;
                foreach (var z in level.Zones)
                {
                    if (z.Kind == ZoneKind.Switch) continue;
                    if (z.Kind == ZoneKind.Magnet ? (p - z.Def.Center).magnitude < z.Def.Size.x * 0.5f : z.Contains(p))
                    {
                        result.Uncertain = true;
                        return d;
                    }
                }
            }
            return length;
        }
    }
}
