using System.Collections.Generic;
using UnityEngine;

namespace OutOfOffice
{
    public enum Eyes { Open, Happy, Squeeze, Dizzy, Wide, Sleepy, Closed, Star, Tired }
    public enum Mouth { Smile, Open, O, Wavy, Grin, Flat }

    // What the game is doing with the employee right now. The base expression and body language follow from it.
    public enum MascotMode { Idle, Aiming, Moving, Gone }

    // The office employee that replaces the puck. Cosmetic only: the collider stays the puck's circle.
    // Every part hangs off one "Roll" pivot at the ball's center. Standing still, the employee stands upright; while
    // rolling, the parts tuck into a cannonball and the whole rig turns with the distance travelled. The face is
    // printed on the head's camera-facing side, in the head's frame, so nothing slides across it.
    public class Mascot : MonoBehaviour
    {
        public const float BodyRadius = 0.37f;      // face layout units (eye spacing etc. are authored for this)
        public const float BodyHalfHeight = 0.34f;  // height of the rolling ball's center above the board
        public const float HeadRadius = 0.26f;
        const float FaceScale = 0.95f;              // face size relative to the layout units
        const float FaceLift = 0.012f;

        // ---- Inputs from the game, set every frame ----
        public MascotMode Mode;
        public Camera ViewCamera;  // the camera the face turns to; the main camera when empty
        public Vector2 AimDir;
        public float AimPower;
        public bool Tumble;  // rolls with its movement even while the game moves it by hand (falls, hole swirl)
        public float Tremble;  // 0..1: a nervous jitter (near the boss, caffeine buzz). The game eases it in and out.

        Puck puck;
        Transform face, eyeL, eyeR, mouthT, cheekL, cheekR, browL, browR, nose;
        SpriteRenderer eyeLR, eyeRR, mouthR, cheekLR, cheekRR, browLR, browRR, noseR;
        Quaternion accessoryRestRotation = Quaternion.identity;
        float sleepBlend;
        readonly List<SpriteRenderer> dizzyStars = new List<SpriteRenderer>();
        Transform accessory, roll;
        Quaternion rollRotation = Quaternion.identity;
        Vector2 previousPosition;
        bool hasPreviousPosition;
        Vector3 accessoryRest;

        // Board up is -Z. A full circumference produces exactly one visual revolution.
        public static Quaternion RollDelta(Vector2 displacement)
        {
            float distance = displacement.magnitude;
            if (distance < 1e-6f) return Quaternion.identity;
            Vector3 axis = Vector3.Cross(Vector3.back, (Vector3)(displacement / distance));
            return Quaternion.AngleAxis(distance / Rules.PuckRadius * Mathf.Rad2Deg, axis);
        }

        public void ResetTravel() => hasPreviousPosition = false;

        public void ResetMotion()
        {
            ResetTravel();
            rollRotation = Quaternion.identity;
            lean = leanVel = leanTarget = sway = swayVel = prevVel = Vector2.zero;
            tuck = 0f;
            asleep = false;
            twirl = 0f;
            twirlTime = hopTime = -1f;
            tempTime = dizzyTime = rideTime = sleepBlend = 0f;
            lastRideAt = -1f;
            later.Clear();
            if (roll != null) roll.localRotation = Quaternion.identity;
        }
        Camera cam;

        // ---- Rig parts and poses ----
        Transform head, torso, legL, legR, tie;
        readonly Dictionary<Transform, (Vector3 pos, Quaternion rot, Vector3 scale)> rest = new Dictionary<Transform, (Vector3, Quaternion, Vector3)>();
        float tuck, tieSwing, tieSwingVel, glassesTilt, glassesVel;
        bool asleep;

        // Lies down on the bed with the face turned to the camera (true), or gets back up (false).
        public void Sleep(bool on)
        {
            asleep = on;
            if (on)
            {
                Mode = MascotMode.Idle;
                Pose(Eyes.Closed, Mouth.Smile);
            }
            else posed = false;
        }

        public bool Asleep => asleep;

        Eyes baseEyes = Eyes.Open, tempEyes;
        Mouth baseMouth = Mouth.Smile, tempMouth;
        float tempTime, tempPriority, dizzyTime;

        Vector2 look, lookTarget, lean, leanVel, leanTarget;
        Vector2 prevVel, sway, swayVel;
        float blinkTimer = 2f, blink, idleTime, momentTimer = 3f, hopTime = -1f, twirl, twirlFrom, twirlTime = -1f, sleepZ;
        float rideTime, lastRideAt = -1f;
        bool sleeping;

        // Riding is reported per physics step; it counts as ongoing until a step passes without it.
        bool riding => Time.time - lastRideAt < 0.07f;

        public void Init(Puck p, SkinDef skin)
        {
            puck = p;
            face = new GameObject("Face").transform;
            face.SetParent(transform, false);
            cheekL = Part("Cheek L", MascotArt.Cheek, 20, CheekPos(-1f), 0.14f, out cheekLR);
            cheekR = Part("Cheek R", MascotArt.Cheek, 20, CheekPos(1f), 0.14f, out cheekRR);
            nose = Part("Nose", MascotArt.Nose, 20, NosePos, 0.05f, out noseR);
            eyeL = Part("Eye L", MascotArt.EyeOpen, 21, EyePos(-1f), 0.2f, out eyeLR);
            eyeR = Part("Eye R", MascotArt.EyeOpen, 21, EyePos(1f), 0.2f, out eyeRR);
            browL = Part("Brow L", MascotArt.Brow, 21, BrowPos(-1f), 0.12f, out browLR);
            browR = Part("Brow R", MascotArt.Brow, 21, BrowPos(1f), 0.12f, out browRR);
            mouthT = Part("Mouth", MascotArt.MouthSmile, 21, MouthPos, 0.1f, out mouthR);
            faceItem = Part("Face Item", MascotArt.Mustache, 22, Vector2.zero, 0.2f, out faceItemR);
            faceItemR.enabled = false;
            for (int i = 0; i < 3; i++)
            {
                Part("Dizzy Star", UiArt.Star, 22, Vector2.zero, 0.07f, out var s);
                s.enabled = false;
                dizzyStars.Add(s);
            }
            SetSkin(skin);
        }

        // Face layout, in face units (about the body radius).
        public static Vector2 EyePos(float side) => new Vector2(side * 0.118f, 0.01f);
        public static Vector2 BrowPos(float side) => new Vector2(side * 0.12f, 0.122f);
        static Vector2 CheekPos(float side) => new Vector2(side * 0.19f, -0.065f);
        public static readonly Vector2 MouthPos = new Vector2(0f, -0.1f);
        static readonly Vector2 NosePos = new Vector2(0f, -0.045f);

        Transform Part(string name, Sprite sprite, int order, Vector2 pos, float size, out SpriteRenderer sr)
        {
            var t = new GameObject(name).transform;
            t.SetParent(face, false);
            t.localPosition = pos;
            sr = t.gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            float s = size / (sprite.rect.width / sprite.pixelsPerUnit);
            t.localScale = new Vector3(s, s, 1f);
            t.gameObject.AddComponent<PartScale>().Base = t.localScale;
            return t;
        }

        // Remembers a face part's rest scale.
        class PartScale : MonoBehaviour { public Vector3 Base; }

        static Vector3 BaseScale(Transform t) => t.GetComponent<PartScale>().Base;

        // Face sticker placement: where on the face, how big, and drawn above or under the eyes.
        void SetFaceItem(FaceItem item)
        {
            faceItemKind = item;
            faceItemR.enabled = item != FaceItem.None;
            if (item == FaceItem.None) return;
            Sprite sprite; float size; int order;
            switch (item)
            {
                case FaceItem.Sunglasses: sprite = MascotArt.Sunglasses; size = 0.52f; order = 22; faceItemPos = new Vector2(0f, 0.01f); break;
                case FaceItem.StarShades: sprite = MascotArt.StarShades; size = 0.52f; order = 22; faceItemPos = new Vector2(0f, 0.01f); break;
                case FaceItem.Glasses: sprite = MascotArt.Glasses; size = 0.52f; order = 22; faceItemPos = new Vector2(0f, 0.01f); break;
                case FaceItem.BowTie: sprite = MascotArt.BowTie; size = 0.17f; order = 22; faceItemPos = new Vector2(0f, -0.2f); break;
                case FaceItem.Freckles: sprite = MascotArt.Freckles; size = 0.5f; order = 20; faceItemPos = new Vector2(0f, -0.045f); break;
                default: sprite = MascotArt.Mustache; size = 0.18f; order = 22; faceItemPos = new Vector2(0f, -0.072f); break;
            }
            faceItemR.sprite = sprite;
            faceItemR.sortingOrder = order;
            float s = size / (sprite.rect.width / sprite.pixelsPerUnit);
            faceItem.GetComponent<PartScale>().Base = new Vector3(s, s, 1f);
        }

        Transform armL, armR;
        float armFlap;

        // The pose: each part blends from its standing rest toward a tucked cannonball, plus the arm gestures.
        // Arms: a lazy sway at rest, wound back while aiming, flailing while flying, up for joy.
        void AnimateArms(float dt, float speed)
        {
            if (armL == null || armR == null) return;
            Eyes e = tempTime > 0f ? tempEyes : baseEyes;
            float t = Time.time;
            float target, wobble;
            if (asleep) { target = 0f; wobble = 0f; }
            else if (e == Eyes.Happy || e == Eyes.Star) { target = 75f; wobble = 18f * Mathf.Sin(t * 14f); }
            else if (Mode == MascotMode.Aiming) { target = -8f - 14f * AimPower; wobble = 3f * Mathf.Sin(t * 30f) * AimPower; }
            else if (Tumble) { target = 70f; wobble = 35f * Mathf.Sin(t * 22f); }
            else { target = 6f; wobble = 5f * Mathf.Sin(t * 2.2f); }
            armFlap = Mathf.Lerp(armFlap, target, 1f - Mathf.Exp(-12f * dt));
            float k = tuck * tuck * (3f - 2f * tuck);

            // Tucked offsets: the head drops to the ball's middle, the body curls under it, limbs hug in.
            Blend(head, new Vector3(0f, -0.02f, 0.13f), Quaternion.Euler(-20f, 0f, 0f), 1f, k);
            Blend(torso, new Vector3(0f, 0.08f, -0.06f), Quaternion.Euler(-35f, 0f, 0f), 0.85f, k);
            Blend(legL, new Vector3(0.01f, -0.12f, -0.13f), Quaternion.Euler(-85f, 0f, 0f), 0.9f, k);
            Blend(legR, new Vector3(-0.01f, -0.12f, -0.13f), Quaternion.Euler(-85f, 0f, 0f), 0.9f, k);
            Blend(armL, new Vector3(0.03f, -0.1f, 0.02f), Quaternion.Euler(-70f, 0f, -25f), 1f, k);
            Blend(armR, new Vector3(-0.03f, -0.1f, 0.02f), Quaternion.Euler(-70f, 0f, 25f), 1f, k);
            float flap = (armFlap + wobble) * (1f - k);
            armL.localRotation *= Quaternion.Euler(0f, -flap, 0f);
            armR.localRotation *= Quaternion.Euler(0f, armFlap * (1f - k) - wobble * 0.8f * (1f - k), 0f);
            // The tie flutters out from the shirt, never into it.
            if (tie != null && rest.TryGetValue(tie, out var tr))
                tie.localRotation = tr.rot * Quaternion.Euler(Mathf.Abs(tieSwing) * 0.8f, 0f, tieSwing * 0.25f);

            // Lying in bed, the head turns on the pillow to face the camera, so the relieved face stays in view.
            sleepBlend = Mathf.MoveTowards(sleepBlend, asleep ? 1f : 0f, dt * 3f);
            if (sleepBlend > 0f && head != null && roll != null && cam != null && rest.TryGetValue(head, out var hr))
            {
                Vector3 toCam = Quaternion.Inverse(roll.rotation) * -cam.transform.forward;
                var turn = Quaternion.FromToRotation(hr.rot * People.FaceDir, toCam);
                head.localRotation = Quaternion.Slerp(Quaternion.identity, turn, sleepBlend * 0.85f) * head.localRotation;
            }
        }

        // Sets a part to its rest pose moved by a tucked offset, blended by k (0 standing, 1 tucked).
        void Blend(Transform part, Vector3 offset, Quaternion turn, float scale, float k)
        {
            if (part == null || !rest.TryGetValue(part, out var r)) return;
            part.localPosition = r.pos + offset * k;
            part.localRotation = Quaternion.Slerp(r.rot, turn * r.rot, k);
            part.localScale = r.scale * Mathf.Lerp(1f, scale, k);
        }

        Transform faceItem;
        SpriteRenderer faceItemR;
        FaceItem faceItemKind;
        Vector2 faceItemPos;

        public void SetSkin(SkinDef skin)
        {
            SetFaceItem(skin.Face);
            // Blush, nose, and brows come from the person's own coloring, so every skin tone reads naturally.
            var blush = Color.Lerp(skin.SkinTone, new Color(1f, 0.42f, 0.45f), 0.6f);
            cheekLR.color = cheekRR.color = new Color(blush.r, blush.g, blush.b, 0.5f);
            var shade = skin.SkinTone * 0.72f;
            noseR.color = new Color(shade.r, shade.g, shade.b, 0.6f);
            var brow = Color.Lerp(skin.HairColor, Color.black, 0.3f);
            browLR.color = browRR.color = new Color(brow.r, brow.g, brow.b, 1f);
            roll = puck.Spin != null ? puck.Spin.Find("Roll") : null;
            Transform Part(string path) => roll != null ? roll.Find(path) : null;
            armL = Part("Arm L");
            armR = Part("Arm R");
            legL = Part("Leg L");
            legR = Part("Leg R");
            head = Part("Head");
            torso = Part("Torso");
            tie = Part("Torso/Tie");
            accessory = Part("Head/Accessory");
            if (accessory != null)
            {
                accessoryRest = accessory.localPosition;
                accessoryRestRotation = accessory.localRotation;
            }
            rest.Clear();
            foreach (var t in new[] { armL, armR, legL, legR, head, torso, tie })
                if (t != null) rest[t] = (t.localPosition, t.localRotation, t.localScale);
            ResetMotion();
        }

        // ---------- Reactions (called by the game) ----------

        // A temporary face that overrides the base one. Higher priority wins while both run.
        public void Express(Eyes e, Mouth m, float seconds, float priority = 1f)
        {
            if (tempTime > 0f && priority < tempPriority) return;
            tempEyes = e;
            tempMouth = m;
            tempTime = seconds;
            tempPriority = priority;
        }

        // A fixed face for portraits: no blinking, no idle moments.
        public void Pose(Eyes e, Mouth m)
        {
            posed = true;
            baseEyes = e;
            baseMouth = m;
            blinkTimer = float.MaxValue;
        }

        bool posed;

        public void Wake()
        {
            idleTime = 0f;
            momentTimer = Random.Range(3f, 5f);
            if (sleeping) Express(Eyes.Wide, Mouth.O, 0.4f, 2f);
            sleeping = false;
        }

        public void OnLaunch(float power)
        {
            Wake();
            leanVel -= AimDir * (40f + 60f * power) * Puck.MotionScale;  // the top lags behind the push
            Express(power > 0.6f ? Eyes.Happy : Eyes.Open, Mouth.Open, 0.5f);
        }

        // Normal speed of a hit decides how it feels: a bump, a squeeze, or a cartoon daze. Never painful.
        public void OnImpact(Vector2 normal, float vn)
        {
            if (vn > 9f)
            {
                dizzyTime = 1.4f;
                Express(Eyes.Dizzy, Mouth.Wavy, 1.4f, 3f);
                puck.JiggleVertical(5f);
                leanVel += normal * 120f * Puck.MotionScale;
            }
            else if (vn > 3f)
            {
                // Briefly offended.
                Express(Eyes.Squeeze, Mouth.Flat, 0.4f, 2f);
                leanVel += normal * (30f + 8f * vn) * Puck.MotionScale;
            }
            else leanVel += normal * 15f * Puck.MotionScale;
            glassesVel += Mathf.Clamp(vn, 0f, 12f) * 9f * (Random.value < 0.5f ? -1f : 1f) * Puck.MotionScale;
        }

        public void OnRide(Vector2 wallNormal, float speed)
        {
            lastRideAt = Time.time;
            if (rideTime < 0.25f && rideTime + Time.fixedDeltaTime >= 0.25f) Sound?.Invoke(Sfx.Whee, 0.3f);
            rideTime += Time.fixedDeltaTime;
            leanTarget = -wallNormal * Mathf.Clamp(speed * 2.2f, 8f, 20f);  // lean into the curve
        }

        public void OnRideEnd()
        {
            // A long ride ends with a happy twirl.
            if (rideTime > 0.45f)
            {
                Twirl(360f * Mathf.Sign(Random.value - 0.5f), 0.5f);
                Express(Eyes.Happy, Mouth.Grin, 0.6f, 1.5f);
            }
            lastRideAt = -1f;
            rideTime = 0f;
        }

        public void OnSurprise(float seconds = 0.6f) => Express(Eyes.Wide, Mouth.O, seconds, 2f);
        public void OnJoy(float seconds = 0.5f) => Express(Eyes.Happy, Mouth.Grin, seconds, 1.5f);
        public void OnDizzyShort() { dizzyTime = 0.6f; Express(Eyes.Dizzy, Mouth.Wavy, 0.6f, 2.5f); }

        public void Twirl(float degrees, float seconds)
        {
            if (Puck.MotionScale < 1f) degrees *= 0.25f;
            twirlFrom = twirl;
            twirlTarget = twirl + degrees;
            twirlTime = 0f;
            twirlDuration = seconds;
        }

        float twirlTarget, twirlDuration;

        public void Hop(float height = 0.22f)
        {
            if (hopTime >= 0f) return;
            hopTime = 0f;
            hopHeight = height * Mathf.Max(0.3f, Puck.MotionScale);
            puck.JiggleVertical(-3f);
        }

        float hopHeight;

        // ---------- Animation ----------

        void LateUpdate() => TickPresentation(Time.deltaTime);

        public void TickPresentation(float deltaTime)
        {
            if (puck == null || puck.Visual == null) return;
            if (ViewCamera != null) cam = ViewCamera;
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            float dt = Mathf.Clamp(deltaTime, 0f, 1f / 30f);
            Vector2 vel = puck.Body != null && puck.Body.simulated ? puck.Body.linearVelocity : Vector2.zero;
            float speed = vel.magnitude;

            if (!posed) Behave(dt, vel, speed);
            AnimateBody(dt, vel);
            AnimateRoll(deltaTime);
            AnimateArms(dt, speed);
            PlaceFace(dt);
            DrawFace(dt);
        }

        void Behave(float dt, Vector2 vel, float speed)
        {
            tempTime -= dt;
            dizzyTime -= dt;
            switch (Mode)
            {
                case MascotMode.Aiming:
                    idleTime = 0f;
                    sleeping = false;
                    lookTarget = AimDir * 0.9f;
                    baseEyes = Eyes.Open;  // determined (squints with power)
                    baseMouth = AimPower > 0.55f ? Mouth.Flat : Mouth.Smile;
                    leanTarget = -AimDir * (4f + 10f * AimPower);   // winding up away from the shot
                    break;
                case MascotMode.Moving:
                    idleTime = 0f;
                    lookTarget = speed > 0.5f ? vel / speed : lookTarget * 0.9f;
                    // Alarmed at speed, mildly concerned otherwise.
                    if (puck.Airborne || speed > 9f) { baseEyes = Eyes.Wide; baseMouth = Mouth.O; }
                    else if (riding) { baseEyes = Eyes.Happy; baseMouth = Mouth.Open; }
                    else if (speed > 3f) { baseEyes = Eyes.Open; baseMouth = Mouth.Flat; }
                    else { baseEyes = Eyes.Tired; baseMouth = Mouth.Smile; }
                    if (!riding) leanTarget = speed > 1f ? vel / speed * Mathf.Min(10f, speed * 0.8f) : Vector2.zero;
                    break;
                case MascotMode.Idle:
                    IdleMoments(dt);
                    break;
            }
            if (rideTime > 0f && !riding) OnRideEnd();
        }

        // Little things the employee does while waiting (calmly exhausted): look around, hop, wiggle, twirl, yawn, then doze off.
        void IdleMoments(float dt)
        {
            idleTime += dt;
            leanTarget = Vector2.zero;
            if (sleeping)
            {
                baseEyes = Eyes.Sleepy;
                baseMouth = Mouth.Flat;
                sleepZ -= dt;
                if (sleepZ <= 0f && Puck.MotionScale >= 1f)
                {
                    sleepZ = 1.3f;
                    Vector3 top = face.position + cam.transform.up * 0.22f + cam.transform.right * 0.15f;
                    FxDot.Spawn(top, (cam.transform.up * 0.35f + cam.transform.right * 0.15f), 0.14f, 0.24f, new Color(0.55f, 0.6f, 0.95f, 0.9f),
                        1.6f, FxMode.Drift, MascotArt.Z, 23);
                }
                return;
            }
            baseEyes = Eyes.Tired;
            baseMouth = Mouth.Smile;
            if (idleTime > 22f)
            {
                sleeping = true;
                return;
            }
            momentTimer -= dt;
            if (momentTimer > 0f) return;
            momentTimer = Random.Range(3f, 6f);
            if (idleTime > 14f)
            {
                Express(Eyes.Closed, Mouth.O, 1.2f, 1f);
                Sound?.Invoke(Sfx.Yawn, 0.35f);
                return;
            }
            switch (Random.Range(0, 5))
            {
                case 0:
                    lookTarget = new Vector2(Random.value < 0.5f ? -1f : 1f, Random.Range(-0.3f, 0.6f));
                    break;
                case 1:
                    Hop();
                    Express(Eyes.Happy, Mouth.Open, 0.45f);
                    break;
                case 2:
                    Twirl(Random.value < 0.5f ? -360f : 360f, 0.6f);
                    Express(Eyes.Happy, Mouth.Grin, 0.6f);
                    Sound?.Invoke(Sfx.Giggle, 0.25f);
                    break;
                case 3:
                    lookTarget = Vector2.zero;
                    Express(Eyes.Happy, Mouth.Smile, 0.8f);  // a content little smile at the player
                    break;
                default:
                    Twirl(25f, 0.15f);
                    StartCoroutineSafe(0.15f, () => Twirl(-25f, 0.2f));
                    break;
            }
        }

        // Plays a mascot sound through the game's audio, if it is connected.
        public System.Action<AudioClip, float> Sound;

        readonly List<(float time, System.Action action)> later = new List<(float, System.Action)>();

        void StartCoroutineSafe(float delay, System.Action action) => later.Add((delay, action));

        void AnimateBody(float dt, Vector2 vel)
        {
            for (int i = later.Count - 1; i >= 0; i--)
            {
                var (time, action) = later[i];
                time -= dt;
                if (time <= 0f) { later.RemoveAt(i); action(); }
                else later[i] = (time, action);
            }

            // Lean: a springy tilt (degrees as a 2D vector on the board).
            if (!riding && Mode != MascotMode.Moving && Mode != MascotMode.Aiming) leanTarget = Vector2.zero;
            float stiffness = 140f, damping = 11f;
            leanVel += ((leanTarget * Puck.MotionScale - lean) * stiffness - leanVel * damping) * dt;
            lean += leanVel * dt;
            lean = Vector2.ClampMagnitude(lean, 30f);
            // Never tip far toward the camera: the head top (and its accessory) would cover the face.
            lean.y = Mathf.Max(lean.y, -5f);

            // Twirl around the up axis.
            if (twirlTime >= 0f)
            {
                twirlTime += dt;
                float k = Mathf.Clamp01(twirlTime / twirlDuration);
                float e = 1f - Mathf.Pow(1f - k, 3f);
                twirl = Mathf.LerpUnclamped(twirlFrom, twirlTarget, e);
                if (k >= 1f) { twirlTime = -1f; twirl = Mathf.Repeat(twirl, 360f); }
            }

            // Hop.
            float hop = 0f;
            if (hopTime >= 0f)
            {
                hopTime += dt;
                const float duration = 0.36f;
                float k = hopTime / duration;
                hop = 4f * hopHeight * k * (1f - k);
                if (k >= 1f)
                {
                    hopTime = -1f;
                    hop = 0f;
                    puck.JiggleVertical(4f);
                }
            }

            // Nervous jitter: a quick, uneven side-to-side shiver on top of the lean (none with reduced motion).
            Vector2 shiver = Vector2.zero;
            if (Tremble > 0.01f && Puck.MotionScale >= 1f)
            {
                float t = Time.time;
                shiver = new Vector2(Mathf.Sin(t * 61f) + 0.5f * Mathf.Sin(t * 97f), 0.4f * Mathf.Sin(t * 73f)) * (3.5f * Tremble);
            }
            Vector3 tilted = new Vector3(Mathf.Sin((lean.x + shiver.x) * Mathf.Deg2Rad), Mathf.Sin((lean.y + shiver.y) * Mathf.Deg2Rad), -1f);
            puck.Lean = Quaternion.FromToRotation(Vector3.back, tilted.normalized);
            puck.ExtraSpin = twirl;
            puck.HopLift = hop;

            // The ribbon flexes locally at its knot; it never changes its attachment position.
            Vector2 accel = dt > 0f ? (vel - prevVel) / dt : Vector2.zero;
            prevVel = vel;
            Vector2 swayTarget = Vector2.ClampMagnitude(-accel * 0.07f, 7f) * Puck.MotionScale;
            swayVel += ((swayTarget - sway) * 140f - swayVel * 22f) * dt;
            sway = Vector2.ClampMagnitude(sway + swayVel * dt, 8f);
            if (accessory != null)
            {
                accessory.localPosition = accessoryRest;
                accessory.localRotation = accessoryRestRotation * Quaternion.Euler(sway.y, -sway.x, 0f);
            }

            // The tie swings back on a soft spring when the employee accelerates, and settles.
            float tieTarget = Mathf.Clamp(accel.magnitude * 0.8f, 0f, 35f) * Puck.MotionScale;
            tieSwingVel += ((tieTarget - tieSwing) * 160f - tieSwingVel * 12f) * dt;
            tieSwing = Mathf.Clamp(tieSwing + tieSwingVel * dt, -40f, 40f);
            // Glasses wobble a little after a knock.
            glassesVel += (-glassesTilt * 300f - glassesVel * 9f) * dt;
            glassesTilt += glassesVel * dt;
        }

        void AnimateRoll(float dt)
        {
            if (roll == null) return;
            Vector2 position = transform.position; // interpolated pose, matching what the player sees
            Vector2 delta = position - previousPosition;
            bool rolling = !asleep && (Tumble || Mode == MascotMode.Moving && puck.Body.simulated && !puck.Airborne);
            float speed = dt > 0f ? delta.magnitude / dt : 0f;
            // Tuck into a ball to roll; stand back up once the roll has nearly stopped.
            float tuckTarget = asleep ? 0f : rolling && (speed > 1.2f || Tumble) || puck.Airborne ? 1f : 0f;
            tuck = Mathf.MoveTowards(tuck, tuckTarget, dt * (tuckTarget > tuck ? 9f : 4f));
            if (hasPreviousPosition && dt > 0f && !posed && rolling && delta.sqrMagnitude < 4f)
                rollRotation = RollDelta(delta) * rollRotation;
            else
            {
                // Lying on the bed: on the back, head toward the pillow (+Y). Otherwise stand upright.
                var upright = asleep ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.identity;
                rollRotation = Quaternion.Slerp(rollRotation, upright, 1f - Mathf.Exp(-(asleep ? 7f : 9f) * Mathf.Max(0f, dt)));
            }
            previousPosition = position;
            hasPreviousPosition = true;
            roll.localRotation = rollRotation;
            // A slow breath while resting, so the employee always feels alive (slower when asleep).
            float breath = Mode == MascotMode.Idle || Mode == MascotMode.Aiming ? 0.016f * Mathf.Sin(Time.time * (asleep ? 1.5f : 2.4f)) : 0f;
            roll.localScale = new Vector3(1f - breath * 0.4f, 1f - breath * 0.4f, 1f + breath);
        }

        // The face is printed on the head at People.FaceDir, tangent to the skull, in the head's frame: it turns with
        // the head while rolling and can never drift onto the hair. (Asleep, the head itself turns to the camera.)
        void PlaceFace(float dt)
        {
            if (roll == null || head == null) return;
            look = Vector2.Lerp(look, Vector2.ClampMagnitude(lookTarget, 1f), 1f - Mathf.Exp(-10f * dt));
            Vector3 scale = head.lossyScale;
            float sx = Mathf.Max(0.001f, (scale.x + scale.y) * 0.5f);
            face.position = head.TransformPoint(People.FaceDir * (HeadRadius + FaceLift));
            face.rotation = head.rotation * People.FaceFrame;
            face.localScale = Vector3.one * (sx * FaceScale);
            face.gameObject.SetActive(Mode != MascotMode.Gone && sx > 0.05f);
        }

        void DrawFace(float dt)
        {
            Eyes e = tempTime > 0f ? tempEyes : baseEyes;
            Mouth m = tempTime > 0f ? tempMouth : baseMouth;
            if (tempTime <= 0f) tempPriority = 0f;

            // Blink now and then while the eyes are open.
            blinkTimer -= dt;
            if (blinkTimer <= 0f)
            {
                blinkTimer = Random.Range(2.2f, 4.5f);
                blink = 0.13f;
            }
            blink -= dt;
            bool blinking = blink > 0f && (e == Eyes.Open || e == Eyes.Wide || e == Eyes.Tired);

            Sprite eyeSprite;
            float eyeScale = 1f, eyeY = 1f;
            switch (e)
            {
                case Eyes.Happy: eyeSprite = MascotArt.EyeHappy; break;
                case Eyes.Squeeze: eyeSprite = MascotArt.EyeSqueeze; break;
                case Eyes.Dizzy: eyeSprite = MascotArt.EyeDizzy; break;
                case Eyes.Closed: eyeSprite = MascotArt.EyeClosed; break;
                case Eyes.Star: eyeSprite = MascotArt.EyeStar; eyeScale = 1.1f + 0.08f * Mathf.Sin(Time.time * 9f); break;
                case Eyes.Sleepy: eyeSprite = MascotArt.EyeSleepy; break;
                case Eyes.Tired: eyeSprite = MascotArt.EyeTired; break;
                case Eyes.Wide: eyeSprite = MascotArt.EyeOpen; eyeScale = 1.22f; break;
                default: eyeSprite = MascotArt.EyeOpen; break;
            }
            if (blinking) { eyeSprite = MascotArt.EyeClosed; eyeScale = 1f; }
            if (Mode == MascotMode.Aiming && e == Eyes.Open) eyeY = 1f - 0.25f * AimPower;  // a determined squint

            eyeLR.sprite = eyeRR.sprite = eyeSprite;
            eyeLR.flipX = false;
            eyeRR.flipX = e == Eyes.Squeeze || e == Eyes.Tired;  // > <, and mirrored tired lids
            Vector2 lookShift = look * 0.012f;
            float spinEyes = e == Eyes.Dizzy ? Time.time * 540f : 0f;
            SetPart(eyeL, EyePos(-1f) + lookShift, eyeScale, eyeY, spinEyes);
            SetPart(eyeR, EyePos(1f) + lookShift, eyeScale, eyeY, -spinEyes);

            // Brows carry most of the acting. Positive tilt lifts the inner ends (tired, worried); negative pulls them
            // down (determined). Offended raises one brow.
            float browY = 0.006f, tilt = 0f, liftR = 0f, tiltR = 0f;
            switch (e)
            {
                case Eyes.Tired: browY = -0.004f; tilt = 10f; break;
                case Eyes.Happy: browY = 0.028f; tilt = 3f; break;
                case Eyes.Wide: browY = 0.038f; tilt = 7f; break;
                case Eyes.Squeeze: browY = -0.006f; tilt = -15f; liftR = 0.036f; tiltR = -9f; break;
                case Eyes.Dizzy: browY = 0.02f; tilt = 14f; break;
                case Eyes.Closed: case Eyes.Sleepy: browY = -0.012f; tilt = 5f; break;
                case Eyes.Star: browY = 0.036f; break;
            }
            if (Mode == MascotMode.Aiming && e == Eyes.Open) { tilt = -5f - 13f * AimPower; browY = -0.012f * AimPower; }
            SetPart(browL, BrowPos(-1f) + lookShift * 0.9f + new Vector2(0f, browY), 1f, 1f, tilt);
            SetPart(browR, BrowPos(1f) + lookShift * 0.9f + new Vector2(0f, browY + liftR), 1f, 1f, -tilt + tiltR);
            SetPart(nose, NosePos + lookShift * 0.8f, 1f, 1f, 0f);

            Sprite mouthSprite;
            float mouthScale = 1f;
            switch (m)
            {
                case Mouth.Open: mouthSprite = MascotArt.MouthOpen; break;
                case Mouth.O: mouthSprite = MascotArt.MouthO; mouthScale = 0.8f; break;
                case Mouth.Wavy: mouthSprite = MascotArt.MouthWavy; break;
                case Mouth.Grin: mouthSprite = MascotArt.MouthGrin; break;
                case Mouth.Flat: mouthSprite = MascotArt.MouthFlat; mouthScale = 0.7f; break;
                default: mouthSprite = MascotArt.MouthSmile; break;
            }
            mouthR.sprite = mouthSprite;
            SetPart(mouthT, MouthPos + lookShift * 0.8f, mouthScale, 1f, 0f);
            SetPart(cheekL, CheekPos(-1f) + lookShift * 0.6f, 1f, 1f, 0f);
            SetPart(cheekR, CheekPos(1f) + lookShift * 0.6f, 1f, 1f, 0f);
            if (faceItemKind != FaceItem.None)
            {
                // Glasses sit a little crooked (it has been a long week) and wobble after knocks.
                bool specs = faceItemKind == FaceItem.Glasses || faceItemKind == FaceItem.Sunglasses || faceItemKind == FaceItem.StarShades;
                SetPart(faceItem, faceItemPos + lookShift * 0.8f, 1f, 1f, specs ? -4f + Mathf.Clamp(glassesTilt, -10f, 10f) : 0f);
            }

            // Cartoon stars circle the head while dazed.
            bool dizzy = dizzyTime > 0f;
            for (int i = 0; i < dizzyStars.Count; i++)
            {
                var s = dizzyStars[i];
                s.enabled = dizzy;
                if (!dizzy) continue;
                float a = Time.time * 5f + i * Mathf.PI * 2f / 3f;
                s.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.2f, 0.2f + Mathf.Sin(a) * 0.05f, -0.01f);
                s.sortingOrder = Mathf.Sin(a) < 0f ? 22 : 19;
            }
        }

        static void SetPart(Transform t, Vector2 pos, float scale, float yScale, float angle)
        {
            // Printed flat on the front of the head cube.
            t.localPosition = new Vector3(pos.x, pos.y, 0f);
            var b = BaseScale(t);
            t.localScale = new Vector3(b.x * scale, b.y * scale * yScale, 1f);
            t.localRotation = Quaternion.Euler(0f, 0f, angle);
        }
    }

    // Procedural face sprites, drawn with signed distances at 128 px. White is not used for strokes, so faces read on
    // every body color.
    public static class MascotArt
    {
        static readonly Color Ink = new Color(0.2f, 0.12f, 0.11f);
        static readonly Color MouthIn = new Color(0.5f, 0.17f, 0.2f);
        static readonly Color Tongue = new Color(0.96f, 0.5f, 0.52f);
        static readonly Color IrisGlow = new Color(0.45f, 0.29f, 0.2f);
        static readonly Color IrisLight = new Color(0.66f, 0.46f, 0.32f);
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();
        const int S = 128;

        // A big, warm brown eye: near-black at the top, a chestnut glow at the bottom, one big catch light and a small one.
        public static Sprite EyeOpen => Get("eyeOpen", (x, y) =>
        {
            float d = Ellipse(x, y, 64, 64, 34, 48);
            Color c = Color.Lerp(IrisGlow, Ink, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((y - 20f) / 52f)));
            float crescent = Mathf.Max(Ellipse(x, y, 64, 40, 24, 16), -Ellipse(x, y, 64, 52, 28, 16));
            c = Color.Lerp(c, IrisLight, Cover(crescent - 2f) * 0.55f);
            c = Color.Lerp(c, Color.white, Cover(Ellipse(x, y, 50, 84, 12, 14)));
            c = Color.Lerp(c, Color.white, Cover(Circle(x, y, 78, 44, 6f)) * 0.9f);
            // A soft light rim, so the eye still reads on deep skin tones.
            return WithRim(c, d, 4f);
        });

        // Calmly exhausted: the same eye under a heavy, slightly drooping lid, with a faint shadow underneath.
        public static Sprite EyeTired => Get("eyeTired", (x, y) =>
        {
            float d = Ellipse(x, y, 64, 66, 34, 46);
            float lidLine = 74f + (x - 64f) * 0.16f;  // droops toward the outer corner (the right eye is mirrored)
            Color c = Color.Lerp(IrisGlow, Ink, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((y - 24f) / 44f)));
            c = Color.Lerp(c, Color.white, Cover(Ellipse(x, y, 52, 60, 10, 11)));
            c = Color.Lerp(c, Color.white, Cover(Circle(x, y, 77, 40, 5f)) * 0.9f);
            float shape = Mathf.Max(d, y - lidLine);
            float lid = Mathf.Max(Mathf.Abs(y - lidLine) - 5.5f, Mathf.Abs(x - 64f) - 40f);
            if (lid < shape) { var ink = Ink; ink.a = Cover(lid); return ink; }
            if (shape > 0f)
            {
                // The under-eye bag: a soft mauve arc below the eye.
                float bag = Mathf.Max(Mathf.Abs(Circle(x, y, 64, 44, 38)) - 3f, y - 22f);
                return new Color(0.55f, 0.32f, 0.38f, Cover(bag) * 0.35f);
            }
            return WithRim(c, shape, 4f);
        });

        // An eyebrow: a short, slightly arched stroke with round ends. White, tinted by the hair color.
        public static Sprite Brow => Get("brow", (x, y) =>
        {
            float best = 99f;
            for (int i = 0; i < 12; i++)
            {
                float a0 = Mathf.Lerp(0.32f, 0.68f, i / 12f) * Mathf.PI, a1 = Mathf.Lerp(0.32f, 0.68f, (i + 1) / 12f) * Mathf.PI;
                best = Mathf.Min(best, Segment(x, y, 64 + Mathf.Cos(a0) * 84f, Mathf.Sin(a0) * 84f - 18f, 64 + Mathf.Cos(a1) * 84f, Mathf.Sin(a1) * 84f - 18f));
            }
            return new Color(1f, 1f, 1f, Cover(best - 9f));
        });

        // A small soft nose: a rounded shadow, tinted by the skin tone.
        public static Sprite Nose => Get("nose", (x, y) =>
        {
            float d = Ellipse(x, y, 64, 62, 34, 26);
            return new Color(1f, 1f, 1f, Mathf.Clamp01(-d / 14f));
        });

        // An urgent "!" in a red bubble, for the pacing manager.
        public static Sprite Alert => Get("alert", (x, y) =>
        {
            float bubble = Circle(x, y, 64, 70, 50);
            float tail = Mathf.Max(Mathf.Max(-(y - 10f), y - 34f), Mathf.Abs(x - 44f) - (y - 10f) * 0.5f);
            float shape = Mathf.Min(bubble, tail);
            float bang = Mathf.Min(RoundRect(x, y, 64, 82, 8, 24, 7), Circle(x, y, 64, 44, 8));
            Color c = Color.Lerp(new Color(0.85f, 0.18f, 0.24f), Color.white, Cover(bang));
            c = Color.Lerp(Ink, c, Cover(shape + 5f));
            c.a = Cover(shape);
            return c;
        });

        // ---- Face stickers (wardrobe) ----

        // Dark shades with a white glint, spanning both eyes.
        public static Sprite Sunglasses => Get("sunglasses", (x, y) =>
        {
            float l = RoundRect(x, y, 36, 66, 23, 17, 9), r = RoundRect(x, y, 92, 66, 23, 17, 9);
            float bridge = Segment(x, y, 56, 72, 72, 72) - 3.5f;
            float d = Mathf.Min(Mathf.Min(l, r), bridge);
            Color c = Color.Lerp(new Color(0.08f, 0.07f, 0.16f), new Color(0.3f, 0.26f, 0.5f), Mathf.Clamp01((70 - y) / 30f));
            float glint = Mathf.Min(Segment(x, y, 22, 74, 34, 58), Segment(x, y, 78, 74, 90, 58)) - 3f;
            if (Mathf.Min(l, r) < 0f) c = Color.Lerp(c, Color.white, Cover(glint) * 0.8f);
            c.a = Cover(d);
            return c;
        });

        // Pink star-shaped party shades.
        public static Sprite StarShades => Get("starShades", (x, y) =>
        {
            float l = StarShape(x - 36, y - 66, 26f), r = StarShape(x - 92, y - 66, 26f);
            float bridge = Segment(x, y, 56, 70, 72, 70) - 3.5f;
            float d = Mathf.Min(Mathf.Min(l, r), bridge);
            Color c = Color.Lerp(new Color(1f, 0.35f, 0.65f), new Color(1f, 0.75f, 0.3f), Mathf.Clamp01((80 - y) / 40f));
            c = Color.Lerp(new Color(0.45f, 0.08f, 0.3f), c, Cover(Mathf.Min(l, r) + 4f));
            c = Color.Lerp(c, Color.white, Cover(Mathf.Min(Circle(x, y, 28, 74, 4f), Circle(x, y, 84, 74, 4f))));
            c.a = Cover(d);
            return c;
        });

        // Round nerd glasses: thin dark frames with a faint lens shine.
        public static Sprite Glasses => Get("glasses", (x, y) =>
        {
            // Slightly squarish frames, thin and dark, with a faint lens shine.
            float l = RoundRect(x, y, 35, 64, 23, 20, 12), r = RoundRect(x, y, 93, 64, 23, 20, 12);
            float ring = Mathf.Min(Mathf.Abs(l) - 3f, Mathf.Abs(r) - 3f);
            float bridge = Segment(x, y, 58, 70, 70, 70) - 2.6f;
            float d = Mathf.Min(ring, bridge);
            var frame = new Color(0.24f, 0.16f, 0.13f);
            if (d < 0.5f) { frame.a = Cover(d); return frame; }
            float shine = Mathf.Min(Segment(x, y, 22, 70, 32, 80), Segment(x, y, 80, 70, 90, 80)) - 2.5f;
            return new Color(1f, 1f, 1f, Cover(Mathf.Min(l, r)) * 0.12f + Cover(shine) * 0.35f);
        });

        // A curly mustache.
        public static Sprite Mustache => Get("mustache", (x, y) =>
        {
            float l = Ellipse(x, y, 46, 62, 22, 13), r = Ellipse(x, y, 82, 62, 22, 13);
            float curlL = Mathf.Abs(Circle(x, y, 22, 70, 9)) - 4f, curlR = Mathf.Abs(Circle(x, y, 106, 70, 9)) - 4f;
            if (y > 70) { curlL = Mathf.Max(curlL, x - 22); curlR = Mathf.Max(curlR, 106 - x); }
            float d = Mathf.Min(Mathf.Min(l, r), Mathf.Min(curlL, curlR));
            Color c = Color.Lerp(new Color(0.24f, 0.14f, 0.08f), new Color(0.42f, 0.27f, 0.15f), Mathf.Clamp01((y - 50) / 20f));
            c.a = Cover(d);
            return c;
        });

        // A red bow tie with white polka dots.
        public static Sprite BowTie => Get("bowTie", (x, y) =>
        {
            float dx = Mathf.Abs(x - 64), dy = Mathf.Abs(y - 64);
            float wing = Mathf.Max(dx - 50f, dy - (8f + dx * 0.55f));
            float knot = RoundRect(x, y, 64, 64, 11, 12, 5);
            float d = Mathf.Min(wing, knot);
            Color c = new Color(0.92f, 0.22f, 0.3f);
            float dots = Mathf.Min(Mathf.Min(Circle(x, y, 32, 70, 4), Circle(x, y, 96, 58, 4)), Mathf.Min(Circle(x, y, 40, 54, 3.5f), Circle(x, y, 88, 74, 3.5f)));
            c = Color.Lerp(c, Color.white, Cover(dots) * 0.9f);
            c = Color.Lerp(c, new Color(0.7f, 0.12f, 0.2f), Cover(knot));
            c.a = Cover(d);
            return c;
        });

        // Freckles across both cheeks.
        public static Sprite Freckles => Get("freckles", (x, y) =>
        {
            float best = 99f;
            foreach (float side in new[] { -1f, 1f })
            {
                float cx = 64 + side * 42f;
                best = Mathf.Min(best, Circle(x, y, cx - 7f * side, 70, 3.2f));
                best = Mathf.Min(best, Circle(x, y, cx + 5f * side, 72, 2.8f));
                best = Mathf.Min(best, Circle(x, y, cx - 1f * side, 63, 3f));
            }
            return new Color(0.72f, 0.42f, 0.22f, Cover(best) * 0.85f);
        });

        static float RoundRect(float x, float y, float cx, float cy, float hx, float hy, float r)
        {
            float qx = Mathf.Abs(x - cx) - hx + r, qy = Mathf.Abs(y - cy) - hy + r;
            return Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f)) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        static float StarShape(float dx, float dy, float r)
        {
            float a = Mathf.Atan2(dx, dy), d = Mathf.Sqrt(dx * dx + dy * dy);
            float k = Mathf.Cos(5f * a) * 0.5f + 0.5f;
            return d - Mathf.Lerp(r * 0.55f, r, Mathf.Pow(k, 1.4f));
        }

        // Star eyes for big wins: a golden star with an orange edge and a sparkle.
        public static Sprite EyeStar => Get("eyeStar", (x, y) =>
        {
            float dx = x - 64, dy = y - 60;
            float a = Mathf.Atan2(dx, dy), r = Mathf.Sqrt(dx * dx + dy * dy);
            float k = Mathf.Cos(5f * a) * 0.5f + 0.5f;
            float d = r - Mathf.Lerp(26f, 52f, Mathf.Pow(k, 1.6f));
            Color c = Color.Lerp(new Color(1f, 0.62f, 0.15f), new Color(1f, 0.86f, 0.3f), Cover(d + 7f));
            c = Color.Lerp(c, Color.white, Cover(Circle(x, y, 52, 74, 7f)));
            return WithRim(c, d, 4f);
        });

        static Color WithRim(Color c, float d, float width)
        {
            float inside = Cover(d);
            float rim = Cover(d - width) * (1f - inside);
            var outc = Color.Lerp(new Color(1f, 1f, 1f, 0f), c, inside);
            outc.a = Mathf.Max(inside, rim * 0.85f);
            if (inside < 1f) outc = new Color(Mathf.Lerp(1f, c.r, inside), Mathf.Lerp(1f, c.g, inside), Mathf.Lerp(1f, c.b, inside), outc.a);
            return outc;
        }

        public static Sprite EyeSleepy => Get("eyeSleepy", (x, y) =>
        {
            float d = Mathf.Max(Ellipse(x, y, 64, 64, 36, 54), y - 66f);
            var c = Ink;
            c.a = Cover(d);
            return c;
        });

        public static Sprite EyeHappy => Get("eyeHappy", (x, y) => Stroke(Mathf.Abs(Circle(x, y, 64, 44, 36)) - 9f, y < 44f ? 99f : 0f));
        public static Sprite EyeClosed => Get("eyeClosed", (x, y) => Stroke(Mathf.Abs(Circle(x, y, 64, 84, 34)) - 8f, y > 84f ? 99f : 0f));
        public static Sprite EyeSqueeze => Get("eyeSqueeze", (x, y) =>
            Stroke(Mathf.Min(Segment(x, y, 36, 94, 88, 64), Segment(x, y, 88, 64, 36, 34)) - 10f, 0f));

        public static Sprite EyeDizzy => Get("eyeDizzy", (x, y) =>
        {
            // An Archimedean spiral.
            float dx = x - 64, dy = y - 64;
            float r = Mathf.Sqrt(dx * dx + dy * dy), a = Mathf.Atan2(dy, dx);
            const float pitch = 15f;
            float k = (r - a / (Mathf.PI * 2f) * pitch) / pitch;
            float d = (Mathf.Abs(k - Mathf.Round(k)) * pitch) - 4.5f;
            if (r > 50f) d = Mathf.Max(d, r - 50f);
            return Stroke(d, 0f);
        });

        // A small, polite smile: one gentle curve with round ends.
        public static Sprite MouthSmile => Get("mouthSmile", (x, y) =>
        {
            float best = 99f;
            const int steps = 16;
            for (int i = 0; i < steps; i++)
            {
                float a0 = Mathf.Lerp(1.22f, 1.78f, i / (float)steps) * Mathf.PI, a1 = Mathf.Lerp(1.22f, 1.78f, (i + 1) / (float)steps) * Mathf.PI;
                best = Mathf.Min(best, Segment(x, y, 64 + Mathf.Cos(a0) * 58f, 112 + Mathf.Sin(a0) * 58f, 64 + Mathf.Cos(a1) * 58f, 112 + Mathf.Sin(a1) * 58f));
            }
            return Stroke(best - 7.5f, 0f);
        });
        public static Sprite MouthFlat => Get("mouthFlat", (x, y) => Stroke(Segment(x, y, 36, 64, 92, 64) - 8f, 0f));
        public static Sprite MouthWavy => Get("mouthWavy", (x, y) =>
        {
            float best = 99f;
            for (int i = 0; i < 24; i++)
            {
                float t0 = i / 24f, t1 = (i + 1) / 24f;
                float x0 = 20 + 88 * t0, x1 = 20 + 88 * t1;
                float y0 = 64 + 10 * Mathf.Sin(t0 * Mathf.PI * 4f), y1 = 64 + 10 * Mathf.Sin(t1 * Mathf.PI * 4f);
                best = Mathf.Min(best, Segment(x, y, x0, y0, x1, y1));
            }
            return Stroke(best - 6.5f, 0f);
        });

        // A "D" mouth: flat top, round bottom, with a tongue.
        public static Sprite MouthOpen => Get("mouthOpen", (x, y) => DMouth(x, y, 46f, 44f));
        public static Sprite MouthGrin => Get("mouthGrin", (x, y) => DMouth(x, y, 50f, 26f));

        public static Sprite MouthO => Get("mouthO", (x, y) =>
        {
            float d = Ellipse(x, y, 64, 62, 26, 32);
            Color c = Color.Lerp(MouthIn, Tongue, Cover(Circle(x, y, 64, 42, 16)));
            c = Color.Lerp(Ink, c, Cover(d + 5f));
            c.a = Cover(d);
            return c;
        });

        // Soft blush, white so the skin color can tint it.
        public static Sprite Cheek => Get("cheek", (x, y) =>
        {
            float d = Ellipse(x, y, 64, 64, 56, 38);
            float a = Mathf.Clamp01(-d / 30f);
            return new Color(1f, 1f, 1f, a * a * (3f - 2f * a));
        });

        public static Sprite Z => Get("z", (x, y) =>
        {
            float d = Mathf.Min(Mathf.Min(Segment(x, y, 30, 100, 98, 100), Segment(x, y, 98, 100, 30, 28)), Segment(x, y, 30, 28, 98, 28)) - 10f;
            return new Color(1f, 1f, 1f, Cover(d));
        });

        static Color DMouth(float x, float y, float halfWidth, float depth)
        {
            // Rounded rectangle top, half-ellipse bottom.
            float top = 82f;
            float d = y > top ? 99f : Ellipse(x, y, 64, top, halfWidth, depth);
            d = Mathf.Max(d, y - top);
            d = Mathf.Min(d, Mathf.Max(Mathf.Abs(x - 64) - halfWidth, Mathf.Abs(y - top) - 6f));
            Color c = Color.Lerp(MouthIn, Tongue, Cover(Ellipse(x, y, 64, top - depth + 6, halfWidth * 0.55f, depth * 0.45f)));
            c = Color.Lerp(Ink, c, Cover(d + 6f));
            c.a = Cover(d);
            return c;
        }

        static Color Stroke(float d, float mask)
        {
            var c = Ink;
            c.a = Cover(Mathf.Max(d, mask > 0f ? 99f : -99f));
            return c;
        }

        static Sprite Get(string key, System.Func<float, float, Color> shade)
        {
            if (cache.TryGetValue(key, out var s) && s != null) return s;
            // Drawn in 128 px coordinates but sampled at Res, so faces stay crisp on close-ups and the app icon.
            const int Res = 192;
            const float step = S / (float)Res;
            aa = 1f / step;
            var px = new Color[Res * Res];
            for (int y = 0; y < Res; y++)
            for (int x = 0; x < Res; x++)
                px[y * Res + x] = shade((x + 0.5f) * step, (y + 0.5f) * step);
            aa = 1f;
            var tex = new Texture2D(Res, Res, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear };
            tex.SetPixels(px);
            tex.Apply(true, true);
            return cache[key] = Sprite.Create(tex, new Rect(0, 0, Res, Res), new Vector2(0.5f, 0.5f), Res);
        }

        static float aa = 1f;  // output pixels per drawing pixel while a sprite is being drawn
        static float Cover(float d) => Mathf.Clamp01(0.5f - d * aa);
        static float Circle(float x, float y, float cx, float cy, float r) => Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r;

        static float Ellipse(float x, float y, float cx, float cy, float rx, float ry)
        {
            float qx = (x - cx) / rx, qy = (y - cy) / ry;
            return (Mathf.Sqrt(qx * qx + qy * qy) - 1f) * Mathf.Min(rx, ry);
        }

        static float Segment(float x, float y, float ax, float ay, float bx, float by)
        {
            float ex = bx - ax, ey = by - ay;
            float t = Mathf.Clamp01(((x - ax) * ex + (y - ay) * ey) / (ex * ex + ey * ey));
            float dx = x - ax - ex * t, dy = y - ay - ey * t;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }
    }
}
