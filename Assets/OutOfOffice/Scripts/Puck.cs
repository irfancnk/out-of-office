using System.Collections.Generic;
using UnityEngine;

namespace OutOfOffice
{
    public enum ShotResult { None, Sunk, Hazard, Fell, Rest }

    public enum Surface { Wood, Ice, Mud, Air }

    // What happened during one physics step. The game turns these into sounds and effects.
    public struct StepInfo
    {
        public ShotResult Result;

        // Strongest new contact this step. ImpactSpeed is the speed along the contact normal,
        // so a fast shallow graze is soft and a head-on hit is hard.
        public bool Impact;
        public float ImpactSpeed, ImpactTangent;
        public Vector2 ImpactPoint, ImpactNormal;
        public BlockTag ImpactBlock;

        // Sustained contact (riding a curve or sliding along a wall): tangential speed at the contact.
        public float RubSpeed;
        public Vector2 RubPoint, RubNormal;
        public BlockTag RubBlock;

        public Surface Surface;
        public bool SurfaceChanged;
        public ZoneInstance Boosted, Teleported, Switched;
        public Vector2 TeleportFrom;
        public bool TookOff, Landed, RimTouch;
        public bool Broke;            // a crystal block shattered (at ImpactPoint)
        public bool Kicked;           // a kicker fired the puck (at ImpactPoint)
        public ZoneInstance Swirled;  // entered a whirlpool this step
        public ZoneInstance Jumped;   // a trampoline launched the puck this step
        public float MagnetPull;      // 0..1, for the hum
        public int StarCollected;     // index into the level's stars, or -1
    }

    // A recent contact, for the developer view.
    public struct ContactMark
    {
        public Vector2 Point, Normal;
        public float Time;
    }

    // The player's puck. Step() holds the shot rules, so the game and the editor checks share them.
    // ExecuteAlways lets the editor checks receive collision callbacks in edit mode.
    [ExecuteAlways]
    public class Puck : MonoBehaviour
    {
        public Rigidbody2D Body { get; private set; }
        public Collider2D Collider { get; private set; }
        public Transform Visual { get; private set; }
        public Transform Spin { get; private set; }
        public Vector2 Hole { get; private set; }
        public LevelInstance Level;

        public bool Airborne { get; private set; }
        public float Lift { get; private set; }  // height above the board, for visuals
        public Surface CurrentSurface { get; private set; }
        public float Elapsed => elapsed;

        [HideInInspector] public float ScaleMul = 1f;
        [HideInInspector] public Vector2 VisualOffset;
        [HideInInspector] public float VisualDepth;  // > 0 sinks the puck below the board surface
        [HideInInspector] public float AimSquash;    // > 0 squashes the puck along AimAxis while the player pulls
        [HideInInspector] public Vector2 AimAxis = Vector2.up;

        // Mascot body language, applied on top of the squash: a tilt, an extra twirl (degrees), and a hop height.
        [HideInInspector] public Quaternion Lean = Quaternion.identity;
        [HideInInspector] public float ExtraSpin, HopLift;

        public static readonly List<ContactMark> RecentContacts = new List<ContactMark>();

        // 1 = full cosmetic motion; the Reduced Motion setting lowers it. Never touches physics.
        public static float MotionScale = 1f;

        readonly Collider2D[] contacts = new Collider2D[8];
        readonly HashSet<Collider2D> touching = new HashSet<Collider2D>();
        readonly HashSet<Collider2D> touchingLong = new HashSet<Collider2D>();
        readonly HashSet<Collider2D> entered = new HashSet<Collider2D>();
        readonly Dictionary<Collider2D, float> enterSpeed = new Dictionary<Collider2D, float>();
        readonly List<Vector2> glanceNormals = new List<Vector2>(8);
        readonly List<float> glanceRoom = new List<float>(8);
        readonly List<Collider2D> current = new List<Collider2D>(8);
        readonly List<Collider2D> touchedThisStep = new List<Collider2D>(8);
        SpriteRenderer blobShadow;
        Vector2 lastVel;
        float elapsed, restTimer, appear = 1f, portalCooldown, stuckTimer;
        Vector2 stuckAnchor;
        float airTime, airDuration, airPeak, airStartLift;
        int portalLock = -1, teleports;
        bool inBoost, inSwirl, onRimEdge;

        // Cosmetic springs: squash along an axis, a vertical squash, and spin. None of these touch the collider.
        Vector2 axis = Vector2.up;
        float squash, squashVel, vSquash, vSquashVel, hitTimer, shownLift, spinAngle, spinVel;

        public void Init(Rigidbody2D body, Collider2D collider, Transform visual, Transform spin, SpriteRenderer shadow, Vector2 hole)
        {
            Body = body;
            Collider = collider;
            Visual = visual;
            Spin = spin;
            blobShadow = shadow;
            Hole = hole;
        }

        // Puts the puck at rest at a position. It stays a physical body, so moving parts can still push it.
        public void PlaceAt(Vector2 pos, bool animate = false)
        {
            if (Airborne) SetBlockCollisions(true);
            Airborne = false;
            Body.simulated = true;
            transform.position = pos;
            Body.position = pos;
            Body.linearVelocity = Vector2.zero;
            Body.linearDamping = Rules.Wood.Drag;
            lastVel = Vector2.zero;
            touching.Clear();
            touchingLong.Clear();
            entered.Clear();
            enterSpeed.Clear();
            elapsed = restTimer = stuckTimer = 0f;
            stuckAnchor = new Vector2(float.MaxValue, float.MaxValue);
            Lift = 0f;
            portalLock = -1;
            teleports = 0;
            inBoost = inSwirl = onRimEdge = false;
            CurrentSurface = Surface.Wood;
            ScaleMul = 1f;
            VisualOffset = Vector2.zero;
            VisualDepth = 0f;
            AimSquash = 0f;
            squash = squashVel = vSquash = vSquashVel = 0f;
            appear = animate ? 0f : 1f;
            Lean = Quaternion.identity;
            ExtraSpin = HopLift = spinAngle = spinVel = 0f;
            GetComponent<Mascot>()?.ResetMotion();
        }

        public void Launch(Vector2 velocity)
        {
            Body.simulated = true;
            Body.position = transform.position;
            Body.linearVelocity = velocity;
            lastVel = velocity;
            elapsed = restTimer = stuckTimer = 0f;
            stuckAnchor = new Vector2(float.MaxValue, float.MaxValue);
            teleports = 0;
            touching.Clear();
            touchingLong.Clear();
            entered.Clear();
            enterSpeed.Clear();
            AimSquash = 0f;
            CurrentSurface = SurfaceAt(Body.position);
            if (velocity.sqrMagnitude > 0.01f)
            {
                axis = velocity.normalized;
                squashVel -= (1f + velocity.magnitude * 0.08f) * MotionScale;  // small snap out of the aim pose
                hitTimer = 0.15f;
            }
        }

        // Starts simulating a resting puck that something else set in motion.
        public void Wake()
        {
            elapsed = restTimer = stuckTimer = 0f;
            stuckAnchor = new Vector2(float.MaxValue, float.MaxValue);
            teleports = 0;
            lastVel = Vector2.zero;  // it was at rest before the push, so the push's full relative speed counts as the hit
            CurrentSurface = SurfaceAt(Body.position);
        }

        // Cosmetic: squash along the hit normal. Strength is the normal impact speed.
        public void Jiggle(Vector2 normal, float strength)
        {
            if (normal.sqrMagnitude > 1e-4f) axis = normal.normalized;
            squashVel += Mathf.Clamp(strength * 0.18f, 0.3f, 3.5f) * MotionScale;
            hitTimer = 0.18f;
        }

        public void JiggleVertical(float strength) => vSquashVel += strength * MotionScale;

        // Cosmetic spin, in degrees per second.
        public void AddSpin(float degreesPerSecond) => spinVel = Mathf.Clamp(spinVel + degreesPerSecond, -1440f, 1440f);

        // Records each new contact with its relative normal speed at first touch, so a spinner that hits a
        // resting puck reads as a hard hit even though the puck itself was not moving.
        void OnCollisionEnter2D(Collision2D collision)
        {
            var other = collision.collider;
            entered.Add(other);
            if (collision.contactCount == 0) return;
            float vn = Mathf.Abs(Vector2.Dot(collision.relativeVelocity, collision.GetContact(0).normal));
            enterSpeed[other] = enterSpeed.TryGetValue(other, out float old) ? Mathf.Max(old, vn) : vn;
        }

        // Velocity of the other surface at a contact point (zero for static walls).
        static Vector2 SurfaceVelocity(Collider2D c, Vector2 point)
        {
            if (c.TryGetComponent(out Mover mover)) return mover.PointVelocity(point);
            var rb = c.attachedRigidbody;
            return rb != null && rb.bodyType == RigidbodyType2D.Dynamic ? rb.GetPointVelocity(point) : Vector2.zero;
        }

        // Contacts are refreshed by the simulation. If the puck was placed somewhere new and stepped before any
        // simulation ran, the list still holds contacts from its old position. A real contact can only have separated
        // as far as the puck moved in one step; anything farther is stale and ignored.
        bool Real(Collider2D c)
        {
            var d = Physics2D.Distance(Collider, c);
            return d.isValid && d.distance < 0.05f + lastVel.magnitude * Rules.Dt * 1.5f;
        }

        static bool IsStationary(Collider2D c)
        {
            var rb = c.attachedRigidbody;
            return rb == null || rb.bodyType == RigidbodyType2D.Static;
        }

        // Call once per physics step while a shot is in flight.
        public StepInfo Step(float dt)
        {
            var info = new StepInfo { StarCollected = -1, Surface = CurrentSurface };
            elapsed += dt;
            portalCooldown -= dt;
            Vector2 pos = Body.position;
            Vector2 vel = Body.linearVelocity;

            // ---- Contacts: new ones are impacts, lasting ones are rubs ----
            int n = Body.GetContacts(contacts);
            current.Clear();
            for (int i = 0; i < n; i++)
            {
                if (!Real(contacts[i])) continue;
                current.Add(contacts[i]);
                if (!touching.Contains(contacts[i])) entered.Add(contacts[i]);
            }

            touchedThisStep.Clear();
            touchedThisStep.AddRange(current);
            foreach (var other in entered)
            {
                if (other == null) continue;
                if (!touchedThisStep.Contains(other)) touchedThisStep.Add(other);
                other.TryGetComponent(out BlockTag tag);
                if (tag != null && tag.Kind == BlockKind.Hazard)
                {
                    info.Result = ShotResult.Hazard;
                    info.ImpactBlock = tag;
                    info.ImpactPoint = other.ClosestPoint(pos);
                    entered.Clear();
                    return info;
                }
                Vector2 point = other.ClosestPoint(pos);
                Vector2 away = pos - point;
                Vector2 normal = away.sqrMagnitude > 1e-6f ? away.normalized : -lastVel.normalized;
                // Relative motion at the contact: a moving obstacle hitting the puck counts like the puck hitting it.
                Vector2 rel = lastVel - SurfaceVelocity(other, point);
                float normalSpeed = Mathf.Max(0f, -Vector2.Dot(rel, normal));
                if (enterSpeed.TryGetValue(other, out float firstTouch)) normalSpeed = Mathf.Max(normalSpeed, firstTouch);
                if (tag != null && tag.Kind == BlockKind.Breakable && normalSpeed >= Rules.BreakSpeed && Level != null && Level.Break(other))
                {
                    // Burst through: undo the rebound and keep part of the incoming velocity.
                    vel = lastVel * Rules.BreakKeep;
                    Body.linearVelocity = vel;
                    info.Broke = true;
                }
                if (tag != null && tag.Kind == BlockKind.Kicker)
                {
                    // Powered bumper: fire straight out along the contact normal, at least at kick speed.
                    vel = normal * Mathf.Max(Rules.KickSpeed, lastVel.magnitude);
                    Body.linearVelocity = vel;
                    lastVel = vel;
                    info.Kicked = true;
                    normalSpeed = Mathf.Max(normalSpeed, 6f);
                }
                if (normalSpeed >= info.ImpactSpeed)
                {
                    info.Impact = true;
                    info.ImpactSpeed = normalSpeed;
                    info.ImpactTangent = (rel - normal * Vector2.Dot(rel, normal)).magnitude;
                    info.ImpactPoint = point;
                    info.ImpactNormal = normal;
                    info.ImpactBlock = tag;
                }
                Mark(point, normal);
            }
            entered.Clear();
            enterSpeed.Clear();

            foreach (var c in current)
            {
                if (!touchingLong.Contains(c) && !touching.Contains(c)) continue;
                Vector2 point = c.ClosestPoint(pos);
                Vector2 away = pos - point;
                if (away.sqrMagnitude < 1e-6f) continue;
                Vector2 normal = away.normalized;
                Vector2 rel = vel - SurfaceVelocity(c, point);
                float tangent = (rel - normal * Vector2.Dot(rel, normal)).magnitude;
                if (tangent > info.RubSpeed)
                {
                    info.RubSpeed = tangent;
                    info.RubPoint = point;
                    info.RubNormal = normal;
                    c.TryGetComponent(out info.RubBlock);
                }
            }
            vel = ApplyGlancing(pos, vel, dt);
            pos = Body.position;
            touchingLong.Clear();
            foreach (var c in current) if (touching.Contains(c)) touchingLong.Add(c);
            touching.Clear();
            foreach (var c in current) touching.Add(c);

            // ---- Airborne flight (ramps) ----
            if (Airborne)
            {
                airTime += dt;
                float k = Mathf.Clamp01(airTime / airDuration);
                Lift = Mathf.Lerp(airStartLift, 0f, k) + 4f * airPeak * k * (1f - k);
                CollectStars(ref info);
                if (airTime < airDuration)
                {
                    lastVel = Body.linearVelocity;
                    return info;
                }
                // Coming down over a pit or onto a desk: glide a little longer, low over it, toward clear floor.
                if (Body.linearVelocity.sqrMagnitude > 1f && LandingBlocked(Body.position, Body.linearVelocity, airTime - airDuration))
                {
                    Lift = 0.12f;
                    lastVel = Body.linearVelocity;
                    return info;
                }
                Land();
                info.Landed = true;
                pos = Body.position;
                // No special landing capture: the grounded hole rule below decides, same as for a rolling puck.
            }

            // ---- Floor zones ----
            if (ApplyZones(dt, ref info)) return info;
            if (info.Result != ShotResult.None) return info;
            CollectStars(ref info);
            pos = Body.position;
            vel = Body.linearVelocity;

            // ---- Surface resistance: drag in Box2D plus a constant rolling decel here ----
            var surf = Tuning(CurrentSurface);
            Body.linearDamping = surf.Drag;
            float speed = vel.magnitude;
            bool grounded = Lift < 0.1f;
            if (grounded && speed > 0f)
            {
                float drop = surf.Decel * dt;
                vel = speed <= drop ? Vector2.zero : vel * (1f - drop / speed);
            }

            // ---- Hole: only the opening matters. No pull from outside the rim. ----
            Vector2 toHole = Hole - pos;
            float dist = toHole.magnitude;
            speed = vel.magnitude;
            if (grounded)
            {
                // Rim event latch: fires once on entry, re-arms only after the puck leaves the outer band.
                if (!onRimEdge && dist < Rules.HoleRadius + Rules.PuckRadius * 0.6f)
                {
                    onRimEdge = true;
                    info.RimTouch = true;
                }
                else if (onRimEdge && dist > Rules.HoleRadius + Rules.PuckRadius) onRimEdge = false;

                // Swept: a fast puck covers about the whole capture zone in one step, so test the closest approach
                // along this step's path, not only where the step ended.
                Vector2 from = pos - vel * dt;
                Vector2 path = pos - from;
                float along = path.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector2.Dot(Hole - from, path) / path.sqrMagnitude) : 1f;
                Vector2 closest = from + path * along;
                if (Captures((Hole - closest).magnitude, speed))
                {
                    if (along < 1f) Body.position = closest;  // the bed scene starts where it actually hit
                    Body.linearVelocity = vel;
                    info.Result = ShotResult.Sunk;
                    return info;
                }
                if (dist < Rules.HoleRadius && dist > 1e-3f)
                {
                    // The center is over the opening: the puck tips inward, so a slow puck drops and a fast one lips out.
                    vel += toHole / dist * (Rules.HoleTilt * dt);
                }
            }

            Body.linearVelocity = vel;
            lastVel = vel;

            // ---- Rest: slow for a short moment, then settle. ----
            if (grounded && vel.magnitude < Rules.RestSpeed && !OverHole(pos)) restTimer += dt;
            else restTimer = 0f;
            // Pinned in place while something keeps pushing (a belt or the wind pressing it into a wall): the speed
            // never drops, but the puck goes nowhere. That counts as resting too, so the next shot is never blocked.
            if ((pos - stuckAnchor).sqrMagnitude > Rules.StuckDistance * Rules.StuckDistance || !grounded || OverHole(pos))
            {
                stuckAnchor = pos;
                stuckTimer = 0f;
            }
            else stuckTimer += dt;
            if (restTimer >= Rules.RestTime || stuckTimer >= Rules.StuckTime || elapsed > Rules.SafetyShotTime)
            {
                Body.linearVelocity = Vector2.zero;
                info.Result = ShotResult.Rest;
            }
            return info;
        }

        bool OverHole(Vector2 pos) => (Hole - pos).magnitude < Rules.HoleRadius;

        // The one capture rule, for rolling and landing pucks alike: the center must be well inside the opening.
        // Over most of the bed any speed counts; toward the edge of the capture region the allowed speed shrinks.
        public static bool Captures(float distToHole, float speed) => speed < CaptureSpeedAt(distToHole);

        public static float CaptureSpeedAt(float distToHole)
        {
            float coreR = Rules.HoleRadius * Rules.CaptureCore, captureR = Rules.HoleRadius * Rules.CaptureEdge;
            if (distToHole >= captureR) return 0f;
            if (distToHole <= coreR) return float.PositiveInfinity;
            float t = (distToHole - coreR) / (captureR - coreR);
            return Rules.CaptureSpeedEdge + (Rules.CaptureSpeedCore - Rules.CaptureSpeedEdge) * (1f - t) * (1f - t);
        }

        // Removes the rebound from glancing contacts on stationary wood (see Rules.GlideRatio). Only the outward
        // normal part of the velocity is removed, so the puck keeps its tangential speed. The small rebound gap is
        // closed too, but never past the real free space measured between the colliders, so the puck can not be
        // pushed into a surface, even with several contacts at once. Moving surfaces always use plain physics.
        Vector2 ApplyGlancing(Vector2 pos, Vector2 vel, float dt)
        {
            Vector2 result = vel, shift = Vector2.zero;
            glanceNormals.Clear();
            glanceRoom.Clear();
            // Includes contacts that already separated during the step (a glancing hit can bounce off within it).
            foreach (var c in touchedThisStep)
            {
                if (c == null || c.usedByEffector || !c.enabled) continue;
                Vector2 away = pos - c.ClosestPoint(pos);
                if (away.sqrMagnitude < 1e-8f) continue;
                Vector2 n = away.normalized;
                var gap = Physics2D.Distance(Collider, c);
                float room = gap.isValid ? Mathf.Max(0f, gap.distance - Rules.ContactRestGap) : 0f;  // free space toward this surface
                glanceNormals.Add(n);
                glanceRoom.Add(room);

                if (!IsStationary(c)) continue;
                c.TryGetComponent(out BlockTag tag);
                if (tag != null && tag.Kind != BlockKind.Wall && tag.Kind != BlockKind.Gate) continue;

                float approach = -Vector2.Dot(lastVel, n);
                if (approach <= 0f) continue;  // already leaving (convex exit): never pull it back
                float tangential = (lastVel + n * approach).magnitude;
                if (approach > Rules.RideNormalMax || approach > Rules.GlideRatio * tangential) continue;
                float outward = Vector2.Dot(result, n);
                if (outward <= 0f) continue;
                result -= n * outward;
                shift -= n * Mathf.Min(outward * dt, room);
            }

            // Several contacts: keep the total shift inside the free space of every nearby surface.
            for (int pass = 0; pass < 2 && shift != Vector2.zero; pass++)
                for (int i = 0; i < glanceNormals.Count; i++)
                {
                    float inward = -Vector2.Dot(shift, glanceNormals[i]);
                    if (inward > glanceRoom[i]) shift += glanceNormals[i] * (inward - glanceRoom[i]);
                }

            if (result != vel) Body.linearVelocity = result;
            if (shift.sqrMagnitude > 1e-12f) Body.position = pos + shift;
            return result;
        }

        static SurfaceTuning Tuning(Surface s) =>
            s == Surface.Ice ? Rules.Ice : s == Surface.Mud ? Rules.Mud : s == Surface.Air ? new SurfaceTuning(0f, 0f) : Rules.Wood;

        public Surface SurfaceAt(Vector2 pos)
        {
            if (Level == null) return Surface.Wood;
            foreach (var z in Level.Zones)
            {
                if (z.Kind == ZoneKind.Ice && z.Contains(pos)) return Surface.Ice;
                if (z.Kind == ZoneKind.Mud && z.Contains(pos)) return Surface.Mud;
            }
            return Surface.Wood;
        }

        void CollectStars(ref StepInfo info)
        {
            if (Level == null) return;
            Vector2 pos = Body.position;
            for (int i = 0; i < Level.Stars.Count; i++)
            {
                var s = Level.Stars[i];
                if (s.Collected || (s.Position - pos).sqrMagnitude > Rules.StarReach * Rules.StarReach) continue;
                s.Collected = true;
                info.StarCollected = i;
                return;
            }
        }

        // Boost pads, ramps, portals, magnets, gaps, and switches. Returns true when the step should end early.
        bool ApplyZones(float dt, ref StepInfo info)
        {
            Lift = 0f;
            var surface = Surface.Wood;
            if (Level == null || Level.Zones.Count == 0)
            {
                SetSurface(surface, ref info);
                return false;
            }

            Vector2 pos = Body.position;
            Vector2 v = Body.linearVelocity;
            bool nowBoost = false, nowSwirl = false;

            foreach (var zone in Level.Zones)
            {
                if (zone.Kind == ZoneKind.Portal && portalLock == zone.Index)
                {
                    if (!zone.Contains(pos)) portalLock = -1;
                    continue;
                }
                if (zone.Kind == ZoneKind.Magnet)
                {
                    v += MagnetPull(zone, pos, dt, ref info);
                    continue;
                }
                if (zone.Kind == ZoneKind.Vortex)
                {
                    if (Level.OnBreak || !SpinPlatform(zone, pos, ref v, dt)) continue;
                    nowSwirl = true;
                    if (!inSwirl) info.Swirled = zone;
                    continue;
                }
                if (!zone.Contains(pos)) continue;

                switch (zone.Kind)
                {
                    case ZoneKind.Boost:
                    {
                        nowBoost = true;
                        Vector2 dir = zone.Direction;
                        v += dir * (Rules.BoostAccel * dt);
                        Vector2 along = dir * Vector2.Dot(v, dir);
                        v = along + (v - along) * Mathf.Max(0f, 1f - Rules.BoostSteer * dt);
                        if (v.magnitude > Rules.BoostMaxSpeed) v = v.normalized * Rules.BoostMaxSpeed;
                        if (!inBoost) info.Boosted = zone;
                        break;
                    }
                    case ZoneKind.Mud:
                        surface = Surface.Mud;
                        break;
                    case ZoneKind.Ice:
                        if (surface == Surface.Wood) surface = Surface.Ice;
                        break;
                    case ZoneKind.Gap:
                        Body.linearVelocity = v;
                        info.Result = ShotResult.Fell;
                        return true;
                    case ZoneKind.Switch:
                        if (Level.PressSwitch(zone)) info.Switched = zone;
                        break;
                    case ZoneKind.Jump:
                    {
                        // Trampoline: bounce up and keep going the same way, at least at jump speed.
                        float speed = v.magnitude;
                        Vector2 dir = speed > 0.1f ? v / speed : zone.Direction;
                        float launch = Mathf.Max(speed, Rules.JumpMinSpeed);
                        v = dir * launch;
                        Body.linearVelocity = v;
                        lastVel = v;
                        TakeOff(launch);
                        info.TookOff = true;
                        info.Jumped = zone;
                        inBoost = false;
                        return true;
                    }
                    case ZoneKind.Wind:
                        if (Level.OnBreak) break;  // switched off for a Coffee Break (OUT-51)
                        v += zone.Direction * (Rules.WindAccel * dt);
                        break;
                    case ZoneKind.Spikes:
                        if (zone.SpikesUp(Level.Time))
                        {
                            Body.linearVelocity = v;
                            info.Result = ShotResult.Hazard;
                            info.ImpactPoint = pos;
                            return true;
                        }
                        break;
                    case ZoneKind.Conveyor:
                    {
                        if (Level.OnBreak) break;
                        // The belt drags the puck toward its own surface velocity, like friction on a moving floor.
                        Vector2 belt = zone.Direction * Rules.ConveyorSpeed;
                        v += (belt - v) * Mathf.Min(1f, Rules.ConveyorGrip * dt);
                        break;
                    }
                    case ZoneKind.Ramp:
                    {
                        float progress = zone.Progress(pos);
                        Lift = Mathf.Max(Lift, Mathf.Clamp01(progress) * Rules.RampHeight);
                        v -= zone.Direction * (Rules.RampGravity * dt);
                        float along = Vector2.Dot(v, zone.Direction);
                        if (progress >= Rules.RampTakeoff && along > 0.5f)
                        {
                            Body.linearVelocity = v;
                            lastVel = v;
                            TakeOff(along);
                            info.TookOff = true;
                            inBoost = false;
                            return true;
                        }
                        break;
                    }
                    case ZoneKind.Portal:
                    {
                        if (portalCooldown > 0f || teleports >= Rules.MaxTeleportsPerShot) break;
                        var target = Level.Zones[zone.Def.Link];
                        info.TeleportFrom = pos;
                        info.Teleported = target;
                        // Momentum is kept; its direction turns by the difference of the two portal facings.
                        v = Quaternion.Euler(0f, 0f, target.Def.Angle - zone.Def.Angle) * v;
                        pos = target.Def.Center;
                        Body.position = pos;
                        transform.position = pos;
                        portalLock = target.Index;
                        portalCooldown = Rules.PortalCooldown;
                        teleports++;
                        break;
                    }
                }
            }

            Body.linearVelocity = v;
            inBoost = nowBoost;
            inSwirl = nowSwirl;
            SetSurface(surface, ref info);
            return false;
        }

        void SetSurface(Surface s, ref StepInfo info)
        {
            if (Airborne) s = Surface.Air;
            if (s != CurrentSurface)
            {
                CurrentSurface = s;
                info.SurfaceChanged = true;
            }
            info.Surface = s;
        }

        // Smooth, bounded pull toward a magnet: grows toward the center, fades inside the core, never spikes.
        Vector2 MagnetPull(ZoneInstance zone, Vector2 pos, float dt, ref StepInfo info)
        {
            if (Airborne) return Vector2.zero;
            Vector2 d = zone.Def.Center - pos;
            float dist = d.magnitude, reach = zone.Def.Size.x * 0.5f;
            if (dist >= reach || dist < 1e-3f) return Vector2.zero;
            float k = 1f - dist / reach;
            float a = Rules.MagnetStrength * Mathf.Lerp(Rules.MagnetEdge, 1f, k);
            info.MagnetPull = Mathf.Max(info.MagnetPull, a / Rules.MagnetStrength);
            return d / dist * (a * dt);
        }

        // The spinning platform (OUT-49): the puck's heading turns the way it spins (hardest at the hub), and it is
        // pushed round and outward, so a fast shot curves and a slow one is whirled off the rim. False when off it.
        static bool SpinPlatform(ZoneInstance zone, Vector2 pos, ref Vector2 v, float dt)
        {
            Vector2 r = pos - zone.Def.Center;
            float dist = r.magnitude, reach = zone.Def.Size.x * 0.5f;
            if (dist >= reach) return false;
            Vector2 outward = dist > 1e-3f ? r / dist : Vector2.right;
            Vector2 round = new Vector2(-outward.y, outward.x) * zone.Def.Turn;
            float turn = Rules.SpinTurn * Mathf.Lerp(1f, Rules.SpinTurnRim, dist / reach) * zone.Def.Turn * dt;
            float c = Mathf.Cos(turn), s = Mathf.Sin(turn);
            v = new Vector2(v.x * c - v.y * s, v.x * s + v.y * c);
            v += (round * Rules.SpinCarry + outward * Rules.SpinFling) * dt;
            return true;
        }

        void TakeOff(float speed)
        {
            Airborne = true;
            airTime = 0f;
            airDuration = Mathf.Clamp(0.3f + speed * 0.035f, 0.35f, 1f);
            airPeak = 0.25f + speed * 0.05f;
            airStartLift = Lift;
            Body.linearDamping = 0f;
            CurrentSurface = Surface.Air;
            SetBlockCollisions(false);
        }

        // Extra glide time when coming down onto a block, and over a pit (long enough to reach the far side).
        const float AirGrace = 0.7f, PitGrace = 3f;

        // Touching down here would drop into a pit (now or on the very next roll forward) or land inside a block.
        bool LandingBlocked(Vector2 pos, Vector2 vel, float overtime)
        {
            if (Level == null) return false;
            Vector2 ahead = pos + vel.normalized * (Rules.PuckRadius + 0.35f);
            if (overtime < PitGrace)
                foreach (var zone in Level.Zones)
                    if (zone.Kind == ZoneKind.Gap && (zone.Contains(pos) || zone.Contains(ahead))) return true;
            if (overtime >= AirGrace) return false;
            foreach (var c in Level.BlockColliders)
            {
                if (c == null || !c.enabled || !c.gameObject.activeInHierarchy || c.usedByEffector) continue;
                if (c.Distance(Collider).distance < -0.02f || c.OverlapPoint(pos)) return true;
            }
            return false;
        }

        void Land()
        {
            Airborne = false;
            Lift = 0f;
            CurrentSurface = SurfaceAt(Body.position);
            Body.linearDamping = Tuning(CurrentSurface).Drag;
            SetBlockCollisions(true);
        }

        void SetBlockCollisions(bool on)
        {
            if (Level == null) return;
            foreach (var c in Level.BlockColliders)
                if (c != null) Physics2D.IgnoreCollision(Collider, c, !on);
        }

        static void Mark(Vector2 point, Vector2 normal)
        {
            RecentContacts.Add(new ContactMark { Point = point, Normal = normal, Time = Time.time });
            if (RecentContacts.Count > 24) RecentContacts.RemoveAt(0);
        }

        void Update()
        {
            if (!Application.isPlaying || Visual == null) return;
            float dt = Mathf.Min(Time.deltaTime, 1f / 30f);
            appear = Mathf.MoveTowards(appear, 1f, Time.deltaTime * 5f);

            // Rest shape: stretched along the velocity, or squashed while the player pulls back.
            Vector2 v = Body.simulated ? Body.linearVelocity : Vector2.zero;
            float speed = v.magnitude;
            float target = 0f;
            Vector2 want = axis;
            if (AimSquash > 0f)
            {
                target = AimSquash;
                want = AimAxis;
            }
            else if (speed > 0.5f && !Airborne)
            {
                // A hint of stretch only: the shell must stay close to the round collider.
                target = -0.04f * Mathf.Clamp01(speed / Rules.MaxSpeed) * MotionScale;
                want = v / speed;
            }
            hitTimer -= dt;
            if (hitTimer <= 0f || AimSquash > 0f)
            {
                if (Vector2.Dot(axis, want) < 0f) want = -want;  // squash is symmetric
                axis = Vector2.Lerp(axis, want, 1f - Mathf.Exp(-12f * dt)).normalized;
            }

            const float stiffness = 520f, damping = 13f;
            squashVel += (-stiffness * (squash - target) - damping * squashVel) * dt;
            // Footprint deformation stays within about 7% of the collider; height can squash more.
            squash = Mathf.Clamp(squash + squashVel * dt, -0.07f, 0.07f);
            if (Mathf.Abs(squash) >= 0.07f) squashVel *= 0.5f;
            vSquashVel += (-stiffness * vSquash - damping * vSquashVel) * dt;
            vSquash = Mathf.Clamp(vSquash + vSquashVel * dt, -0.35f, 0.5f);

            spinVel *= Mathf.Exp(-1.2f * dt);
            spinAngle += spinVel * dt;

            float s = ScaleMul * EaseOutBack(appear);
            float flat = 1f + 0.08f * vSquash;
            float axisDeg = Mathf.Atan2(axis.y, axis.x) * Mathf.Rad2Deg;
            Visual.localRotation = Quaternion.Euler(0f, 0f, axisDeg);
            Visual.localScale = new Vector3(s * (1f - squash) * flat, s * (1f + 0.6f * squash) * flat,
                s * (1f + 0.7f * squash) * (1f - vSquash));
            if (Spin != null) Spin.localRotation = Quaternion.Euler(0f, 0f, -axisDeg) * Lean * Quaternion.Euler(0f, 0f, ExtraSpin);

            // Lift changes at the physics rate; ease it so the puck glides smoothly at 120 Hz.
            shownLift = Lift > shownLift + 0.5f || Lift < 0.001f && shownLift < 0.02f ? Lift : Mathf.Lerp(shownLift, Lift, 1f - Mathf.Exp(-40f * dt));
            Visual.localPosition = new Vector3(VisualOffset.x, VisualOffset.y, VisualDepth - shownLift - HopLift);

            if (blobShadow != null)
            {
                // Contact shadow: tight and dark on the board, wider and fainter in the air, gone in the hole.
                float fade = ScaleMul * Mathf.Clamp01(1f - VisualDepth * 3f) * Mathf.Clamp01(1f - (shownLift + HopLift) * 0.6f);
                blobShadow.color = new Color(0.25f, 0.13f, 0.05f, 0.42f * fade);
                float d = Rules.PuckRadius * 2.6f * (1f + (shownLift + HopLift) * 0.6f);
                blobShadow.transform.localScale = new Vector3(d, d, 1f);
            }
        }

        static float EaseOutBack(float x)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
        }
    }
}
