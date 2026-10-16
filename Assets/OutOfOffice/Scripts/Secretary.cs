using System.Collections.Generic;
using UnityEngine;

namespace OutOfOffice
{
    // The receptionist (OUT-38): red hair up in a high bun, a cream blouse with a bow, a pencil skirt, gold hoops and red
    // lipstick, unimpressed by everything. She sits at her desk in one room of each chapter and, every few seconds,
    // does one thing at random: chews gum and blows a bubble that pops, files her nails, takes a call ("He's in a
    // meeting."), or scrunches a memo and flicks it into her bin. When the employee rolls past she rolls her eyes and
    // looks away. Reduced Motion keeps her still except for blinking. Nothing here touches physics.
    public class Secretary : MonoBehaviour
    {
        public static readonly SkinDef Look = new SkinDef
        {
            Id = "receptionist", Name = "RECEPTIONIST", Body = Themes.Hex(0x23A595), Cheek = Themes.Hex(0xF29A9A), Extra = Color.white,
            SkinTone = Themes.Hex(0xF3CBAA), HairColor = Themes.Hex(0xD9472B), Hair = HairStyle.Bun,
            Extras = PersonLook.Earrings | PersonLook.Hoops | PersonLook.Blouse | PersonLook.Skirt, Shirt = Themes.Hex(0xFFF1DC),
        };

        // Set by the game.
        public static System.Func<bool> MayTalk;                  // the player isn't aiming and nobody else is talking (OUT-22)
        public static System.Action<Secretary, string> Say;
        public static System.Action<AudioClip, float> Sound;
        public static bool Calm;                                  // Reduced Motion
        public static readonly List<Secretary> Active = new List<Secretary>();

        public const string PhoneLine = "He's in a meeting.";
        const float TalkGap = 18f, NoticeDistance = 2.6f, NoticeSpeed = 0.8f;
        static float lastTalk = -99f;

        public enum Act { None, Gum, Nails, Phone, Paper, EyeRoll }

        Transform head, armL, armR, legL, legR, face, handset, handsetHome, bin, ballT, bubbleT;
        Vector3 handsetPos;
        Quaternion handsetRot;
        SpriteRenderer eyeL, eyeR, browL, browR, lips;
        int side;
        Act act = Act.None;
        float actT, actLength, nextAct, nextBlink, blinkUntil, noticeCooldown, rollUntil, rollTurn;
        bool spoke, popped, thrown;
        Vector3 lastPlayer;
        bool hasLast;

        public Vector3 HeadTop => head != null ? head.position + new Vector3(0f, 0f, -0.55f) : transform.position;

        public void Init(Transform phoneHandset, Transform wasteBin, int cornerSide)
        {
            handset = phoneHandset;
            bin = wasteBin;
            side = cornerSide;
            head = transform.Find("Roll/Head");
            armL = transform.Find("Roll/Arm L");
            armR = transform.Find("Roll/Arm R");
            legL = transform.Find("Roll/Leg L");
            legR = transform.Find("Roll/Leg R");
            // Sat down: legs forward under the desk, forearms resting on it.
            foreach (var leg in new[] { legL, legR }) if (leg != null) leg.localRotation = Quaternion.Euler(78f, 0f, 0f);
            RestArms();
        }

        void OnEnable() => Active.Add(this);
        void OnDisable() => Active.Remove(this);

        void Start()
        {
            BuildFace();
            nextAct = Time.time + Random.Range(1.5f, 4f);
            nextBlink = Time.time + Random.Range(1f, 3f);
        }

        // ---------- Face ----------

        // Printed on the front of the head like every face in the office: half-lidded, one brow up, red lips.
        public void BuildFace()
        {
            if (face != null || head == null) return;
            face = new GameObject("Face").transform;
            face.SetParent(head, false);
            face.localPosition = People.FaceDir * (Mascot.HeadRadius + 0.012f);
            face.localRotation = People.FaceFrame;
            face.localScale = Vector3.one * 0.95f;
            eyeL = Part(MascotArt.EyeTired, Mascot.EyePos(-1f), 0.19f, 21, 0f, Color.white);
            eyeR = Part(MascotArt.EyeTired, Mascot.EyePos(1f), 0.19f, 21, 0f, Color.white);
            eyeR.flipX = true;
            var brow = Color.Lerp(Look.HairColor, Color.black, 0.35f);
            browL = Part(MascotArt.Brow, Mascot.BrowPos(-1f) + new Vector2(0f, 0.035f), 0.13f, 21, 12f, brow);   // the raised one
            browR = Part(MascotArt.Brow, Mascot.BrowPos(1f) + new Vector2(0f, -0.005f), 0.13f, 21, 4f, brow);
            Part(MascotArt.Nose, new Vector2(0f, -0.045f), 0.05f, 20, 0f, new Color(0.7f, 0.46f, 0.38f, 0.55f));
            lips = Part(MascotArt.Lips, Mascot.MouthPos + new Vector2(0.01f, 0f), 0.11f, 21, 0f, Color.white);
            Part(MascotArt.Cheek, new Vector2(-0.19f, -0.065f), 0.13f, 20, 0f, new Color(0.95f, 0.5f, 0.5f, 0.4f));
            Part(MascotArt.Cheek, new Vector2(0.19f, -0.065f), 0.13f, 20, 0f, new Color(0.95f, 0.5f, 0.5f, 0.4f));
        }

        SpriteRenderer Part(Sprite sprite, Vector2 pos, float size, int order, float angle, Color color)
        {
            var t = new GameObject("Part").transform;
            t.SetParent(face, false);
            t.localPosition = new Vector3(pos.x, pos.y, 0f);
            t.localRotation = Quaternion.Euler(0f, 0f, angle);
            var sr = t.gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            sr.color = color;
            float s = size / (sprite.rect.width / sprite.pixelsPerUnit);
            t.localScale = new Vector3(s, s, 1f);
            return sr;
        }

        // ---------- The idle loop ----------

        void Update()
        {
            if (face == null) BuildFace();
            float now = Time.time, dt = Time.deltaTime;
            Blink(now);
            if (Calm)
            {
                if (act != Act.None) Stop();
                return;
            }
            Notice(now, dt);
            if (act == Act.None)
            {
                if (now >= nextAct) Begin(Pick());
            }
            else
            {
                actT += dt;
                Pose(act, actT);
                if (actT >= actLength) Stop();
            }
            Head(now);
        }

        Act Pick()
        {
            // The phone only rings now and then: the line is the joke, and it must not wear out.
            int roll = Random.Range(0, 7);
            return roll < 2 ? Act.Gum : roll < 4 ? Act.Nails : roll < 5 && Time.time - lastTalk > TalkGap ? Act.Phone : Act.Paper;
        }

        void Begin(Act a)
        {
            act = a;
            actT = 0f;
            spoke = popped = thrown = false;
            actLength = LengthOf(a);
            if (a == Act.Phone) Sound?.Invoke(Sfx.Ring, 0.35f);
            if (a == Act.Paper) Sound?.Invoke(Sfx.Paper, 0.45f);
        }

        static float LengthOf(Act a) => a == Act.Gum ? 2.4f : a == Act.Nails ? 2.8f : a == Act.Phone ? 3f : a == Act.Paper ? 2.1f : 1.3f;

        void Stop()
        {
            Pose(act, actLength);  // the end pose of each act puts everything back
            act = Act.None;
            nextAct = Time.time + Random.Range(2.5f, 5.5f);
            RestArms();
        }

        // Every act as a pose at time t, so the editor's review shots can freeze any moment of it.
        public void Pose(Act a, float t)
        {
            actLength = LengthOf(a);
            switch (a)
            {
                case Act.Gum: Gum(t); break;
                case Act.Nails: Nails(t); break;
                case Act.Phone: Phone(t); break;
                case Act.Paper: Paper(t); break;
                case Act.EyeRoll:
                    SetEyes(MascotArt.EyeRoll);
                    if (head != null) head.localRotation = Quaternion.Euler(0f, 0f, side * 24f);
                    break;
            }
        }

        // Chews, blows a pink bubble, and it pops.
        void Gum(float t)
        {
            float chew = t < 0.8f || t > 2f ? Mathf.Sin(t * 22f) : 0f;
            if (lips != null) lips.transform.localScale = Vector3.Scale(BaseLipScale(), new Vector3(1f - 0.06f * chew, 1f + 0.12f * chew, 1f));
            float grow = Mathf.Clamp01((t - 0.8f) / 1f);
            bool showing = t >= 0.8f && t < 1.85f;
            if (showing)
            {
                var b = Bubble();
                b.gameObject.SetActive(true);
                float r = Mathf.Lerp(0.02f, 0.12f, grow * grow) * (1f + 0.04f * Mathf.Sin(t * 18f));
                b.localScale = Vector3.one * r * 2f;
                b.localPosition = new Vector3(Mascot.MouthPos.x, Mascot.MouthPos.y - 0.01f, -r * 0.9f);
            }
            else if (bubbleT != null) bubbleT.gameObject.SetActive(false);
            if (t >= 1.85f && !popped)
            {
                popped = true;
                Sound?.Invoke(Sfx.Pop, 0.5f);
                if (bubbleT != null)
                    for (int i = 0; i < 5; i++)  // pink flecks where it was
                        FxDot.Spawn(bubbleT.position, Random.onUnitSphere * 0.6f, 0.07f, 0.02f, new Color(1f, 0.55f, 0.78f), 0.35f, FxMode.Float, Textures.Glow, 24);
            }
            if (t >= actLength && lips != null) lips.transform.localScale = BaseLipScale();
        }

        // Files her nails: one hand up, the file going back and forth over it.
        void Nails(float t)
        {
            float raise = Mathf.Clamp01(t / 0.3f) * Mathf.Clamp01((actLength - t) / 0.3f);
            float rub = Mathf.Sin(t * 26f) * 10f * raise;
            if (armL != null) armL.localRotation = Quaternion.Slerp(ArmRest(-1f), ArmIn(-1f, 40f, 108f), raise);
            if (armR != null) armR.localRotation = Quaternion.Slerp(ArmRest(1f), ArmIn(1f, 30f + rub, 100f), raise);
            File().gameObject.SetActive(raise > 0.2f);
        }

        // Picks up the phone and tells them he's in a meeting. Only when the player isn't aiming (OUT-22).
        void Phone(float t)
        {
            if (handset == null || armR == null) return;
            float up = Mathf.Clamp01((t - 0.5f) / 0.35f) * Mathf.Clamp01((actLength - t) / 0.35f);
            armR.localRotation = Quaternion.Slerp(ArmRest(1f), Quaternion.Euler(0f, 150f, 0f), up);
            if (up > 0.5f && handset.parent != armR)
            {
                handsetHome = handset.parent;
                handsetPos = handset.localPosition;
                handsetRot = handset.localRotation;
                handset.SetParent(armR, false);
                handset.localPosition = new Vector3(0f, -0.02f, 0.19f);
                handset.localRotation = Quaternion.Euler(0f, 90f, 90f);
            }
            else if (up <= 0.5f && handset.parent == armR) PutDownHandset();
            if (!spoke && t > 1.05f && t < 1.6f)
            {
                spoke = true;
                if (MayTalk == null || MayTalk())
                {
                    lastTalk = Time.time;
                    Say?.Invoke(this, PhoneLine);
                }
            }
        }

        void PutDownHandset()
        {
            if (handset == null || handsetHome == null || handset.parent != armR) return;
            handset.SetParent(handsetHome, false);
            handset.localPosition = handsetPos;
            handset.localRotation = handsetRot;
        }

        // Scrunches a memo into a ball and flicks it in an arc into her bin.
        void Paper(float t)
        {
            var arm = side < 0 ? armL : armR;
            var other = side < 0 ? armR : armL;
            float s = side < 0 ? -1f : 1f;
            float scrunch = Mathf.Clamp01(t / 0.25f) * (t < 0.75f ? 1f : 0f);
            float squeeze = Mathf.Sin(t * 30f) * 6f * scrunch;
            if (other != null) other.localRotation = Quaternion.Slerp(ArmRest(-s), ArmIn(-s, 42f + squeeze, 88f), scrunch);
            float flick = Mathf.Clamp01((t - 0.75f) / 0.18f) * Mathf.Clamp01((actLength - t) / 0.4f);
            if (arm != null)
                arm.localRotation = t < 0.75f ? Quaternion.Slerp(ArmRest(s), ArmIn(s, 42f - squeeze, 88f), scrunch)
                    : Quaternion.Slerp(ArmIn(s, 42f, 88f), ArmIn(s, -55f, 60f), flick);
            var ball = Ball();
            Vector3 hand = HandPoint(side < 0 ? armL : armR);
            if (t < 0.85f)
            {
                ball.gameObject.SetActive(t > 0.15f);
                ball.position = hand;
                ball.localScale = Vector3.one * Mathf.Lerp(0.16f, 0.1f, Mathf.Clamp01(t / 0.6f));
            }
            else
            {
                float k = Mathf.Clamp01((t - 0.85f) / 0.6f);
                ball.localScale = Vector3.one * 0.1f;
                Vector3 target = bin != null ? bin.position + new Vector3(0f, 0f, -0.2f) : hand;
                var p = Vector3.Lerp(hand, target, k);
                p.z -= Mathf.Sin(k * Mathf.PI) * 0.55f;  // up and over (-Z is up)
                ball.position = p;
                ball.gameObject.SetActive(k < 1f);
                if (k >= 1f && !thrown)
                {
                    thrown = true;
                    Sound?.Invoke(Sfx.Soft, 0.4f);
                    if (bin != null && !Calm) Tweens.Run(0.35f, q => bin.localScale = Vector3.one * (1f + 0.12f * Mathf.Sin(q * Mathf.PI * 3f) * (1f - q)), Tweens.Linear, 0f, () => bin.localScale = Vector3.one, bin);
                }
            }
        }

        // ---------- Noticing the employee ----------

        // The employee rolls past: a long-suffering eye roll, and she turns her head away.
        void Notice(float now, float dt)
        {
            noticeCooldown -= dt;
            if (!CoworkerFace.Player.HasValue || head == null) { hasLast = false; return; }
            var p = CoworkerFace.Player.Value;
            float speed = hasLast && dt > 0f ? ((Vector2)(p - lastPlayer)).magnitude / dt : 0f;
            lastPlayer = p;
            hasLast = true;
            if (noticeCooldown > 0f || speed < NoticeSpeed) return;
            if (((Vector2)(p - head.position)).magnitude > NoticeDistance) return;
            noticeCooldown = 6f;
            rollUntil = now + 1.3f;
            rollTurn = p.x > head.position.x ? -1f : 1f;  // away from the employee
        }

        void Head(float now)
        {
            if (head == null) return;
            float left = rollUntil - now;
            float turn = 0f;
            if (left > 0f)
            {
                float k = 1f - left / 1.3f;
                turn = rollTurn * 28f * Mathf.Sin(Mathf.Clamp01(k * 1.4f) * Mathf.PI * 0.5f) * Mathf.Clamp01(left / 0.3f);
                if (now >= blinkUntil) SetEyes(k < 0.6f ? MascotArt.EyeRoll : MascotArt.EyeTired);
            }
            else if (act == Act.Nails) turn = side * -8f;  // looking down at her hands
            head.localRotation = Quaternion.Euler(0f, 0f, turn);
        }

        void Blink(float now)
        {
            if (eyeL == null) return;
            if (now >= nextBlink)
            {
                nextBlink = now + Random.Range(2.2f, 4.8f);
                blinkUntil = now + 0.13f;
            }
            if (now < blinkUntil) SetEyes(MascotArt.EyeClosed);
            else if (now >= rollUntil) SetEyes(MascotArt.EyeTired);
        }

        void SetEyes(Sprite s)
        {
            if (eyeL == null || eyeL.sprite == s) return;
            eyeL.sprite = eyeR.sprite = s;
        }

        // ---------- Props and arms ----------

        Vector3 baseLip;
        Vector3 BaseLipScale()
        {
            if (baseLip == Vector3.zero && lips != null) baseLip = lips.transform.localScale;
            return baseLip;
        }

        Transform Bubble()
        {
            if (bubbleT != null) return bubbleT;
            var b = OutOfOffice.Look.AddMesh("Gum Bubble", Meshes.Blob(Vector3.one, 2f), OutOfOffice.Look.Person(Themes.Hex(0xFF8CC8), 0.75f), face, Vector3.zero, false);
            bubbleT = b.transform;
            bubbleT.gameObject.SetActive(false);
            return bubbleT;
        }

        Transform File()
        {
            var hand = armR;
            var existing = hand != null ? hand.Find("Nail File") : null;
            if (existing != null) return existing;
            var f = OutOfOffice.Look.AddMesh("Nail File", Meshes.SoftBox(new Vector3(0.025f, 0.012f, 0.16f), 0.005f), OutOfOffice.Look.Person(Themes.Hex(0xE85A9A), 0.5f), hand,
                new Vector3(-0.03f, -0.02f, 0.22f), false);
            f.transform.localRotation = Quaternion.Euler(0f, -35f, 0f);
            f.gameObject.SetActive(false);
            return f.transform;
        }

        Transform Ball()
        {
            if (ballT != null) return ballT;
            var b = OutOfOffice.Look.AddMesh("Paper Ball", Meshes.Blob(Vector3.one, 1.7f), OutOfOffice.Look.Person(Themes.Hex(0xF8F6F0), 0.2f), transform, Vector3.zero, false);
            ballT = b.transform;
            ballT.gameObject.SetActive(false);
            return ballT;
        }

        static Vector3 HandPoint(Transform arm) => arm != null ? arm.TransformPoint(new Vector3(0f, -0.004f, 0.17f)) : Vector3.zero;

        // Forearms resting forward on the desk.
        static Quaternion ArmRest(float s) => Quaternion.AngleAxis(-s * 18f, Vector3.forward) * Quaternion.AngleAxis(62f, Vector3.right);

        // An arm raised forward by `lift` degrees and swung `inward` degrees toward the middle of the body.
        static Quaternion ArmIn(float s, float inward, float lift) => Quaternion.AngleAxis(-s * inward, Vector3.forward) * Quaternion.AngleAxis(lift, Vector3.right);

        void RestArms()
        {
            if (armL != null) armL.localRotation = ArmRest(-1f);
            if (armR != null) armR.localRotation = ArmRest(1f);
            PutDownHandset();
            if (armR != null) { var f = armR.Find("Nail File"); if (f != null) f.gameObject.SetActive(false); }
            if (ballT != null) ballT.gameObject.SetActive(false);
            if (bubbleT != null) bubbleT.gameObject.SetActive(false);
        }

        void OnDestroy() => PutDownHandset();
    }
}
