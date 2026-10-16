using UnityEngine;

namespace OutOfOffice
{
    // The face of a coworker rig (no Mascot: coworkers do not roll). Printed tangent to the head at People.FaceDir,
    // exactly like the employee's, so it sits on the face and never on the hair. An urgent "!" bubble pops up beside
    // the head now and then.
    public class CoworkerFace : MonoBehaviour
    {
        const float Lift = 0.012f, Scale = 0.95f;
        const float NoticeDistance = 1.7f;
        Transform head, face, alert;

        // Every coworker in the scene, and where the employee is (set by the game each frame; null outside a level).
        public static readonly System.Collections.Generic.List<CoworkerFace> Active = new System.Collections.Generic.List<CoworkerFace>();
        public static Vector3? Player;

        void OnEnable() => Active.Add(this);
        void OnDisable() => Active.Remove(this);

        void Start() => Build();

        void Build()
        {
            if (face != null) return;
            head = transform.Find("Roll/Head");
            face = new GameObject("Face").transform;
            face.SetParent(head != null ? head : transform, false);
            var brow = new Color(0.32f, 0.33f, 0.37f);
            Part(MascotArt.EyeOpen, Mascot.EyePos(-1f), 0.19f, 21, 0f, Color.white);
            Part(MascotArt.EyeOpen, Mascot.EyePos(1f), 0.19f, 21, 0f, Color.white).flipX = true;  // lashes flick outward on both eyes
            // Stern brows, pulled down at the middle: this is urgent, apparently.
            Part(MascotArt.Brow, Mascot.BrowPos(-1f) + new Vector2(0f, -0.01f), 0.13f, 21, -16f, brow);
            Part(MascotArt.Brow, Mascot.BrowPos(1f) + new Vector2(0f, -0.01f), 0.13f, 21, 16f, brow);
            Part(MascotArt.Nose, new Vector2(0f, -0.045f), 0.05f, 20, 0f, new Color(0.62f, 0.45f, 0.36f, 0.6f));
            Part(MascotArt.MouthFlat, Mascot.MouthPos, 0.075f, 21, 0f, Color.white);
            Part(MascotArt.Cheek, new Vector2(-0.19f, -0.065f), 0.13f, 20, 0f, new Color(0.95f, 0.55f, 0.52f, 0.35f));
            Part(MascotArt.Cheek, new Vector2(0.19f, -0.065f), 0.13f, 20, 0f, new Color(0.95f, 0.55f, 0.52f, 0.35f));
            alert = new GameObject("Alert").transform;
            alert.SetParent(transform, false);
            var sr = alert.gameObject.AddComponent<SpriteRenderer>();
            sr.sprite = MascotArt.Alert;
            sr.sortingOrder = 23;
            alert.gameObject.SetActive(false);
        }

        SpriteRenderer Part(Sprite sprite, Vector2 pos, float size, int order, float angle, Color color)
        {
            var t = new GameObject("Part").transform;
            t.SetParent(face, false);
            // Printed flat on the front of the head cube.
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

        void LateUpdate() => Tick(null);

        public void Tick(Camera view)
        {
            Build();
            if (head == null) return;
            var cam = view != null ? view : Camera.main;
            face.localPosition = People.FaceDir * (Mascot.HeadRadius + Lift);
            face.localRotation = People.FaceFrame;
            face.localScale = Vector3.one * Scale;
            if (cam == null) return;
            // The "!" pops in for a moment every couple of seconds, up and to the side of the head, facing the camera.
            float k = Mathf.Repeat(Time.time * 0.5f + transform.position.x, 1f);
            float pop = k < 0.4f ? Mathf.Sin(k / 0.4f * Mathf.PI) : 0f;
            // They notice the employee walking past: the "!" stays up while they are close (OUT-26).
            if (Player.HasValue && ((Vector2)(Player.Value - head.position)).magnitude < NoticeDistance * transform.lossyScale.x) pop = 1f;
            alert.gameObject.SetActive(pop > 0.02f);
            if (!alert.gameObject.activeSelf) return;
            alert.position = head.position + cam.transform.up * 0.36f * transform.lossyScale.x + cam.transform.right * 0.26f * transform.lossyScale.x;
            alert.rotation = cam.transform.rotation;
            alert.localScale = Vector3.one * (0.2f + 0.12f * pop);
        }
    }
}
