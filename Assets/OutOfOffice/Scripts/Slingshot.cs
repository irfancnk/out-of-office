using UnityEngine;

namespace OutOfOffice
{
    // The slingshot that pops up while aiming: two wooden posts either side of the employee, with rubber bands to a
    // leather pouch behind them. The pull stretches the bands; on release they snap forward, wobble, and the posts
    // sink back into the floor. Visual only: the shot always starts from the rest spot.
    public class Slingshot
    {
        const float Spread = 0.6f, PostHeight = 0.52f, BandHeight = 0.44f, PouchHeight = 0.3f, Lean = 12f;

        readonly Transform root, pouch;
        readonly Transform[] posts = new Transform[2], bands = new Transform[2];
        Vector2 center, axis = Vector2.up, pouchPos, releaseFrom;
        float shown, releaseTime = -1f;
        bool active;

        public Slingshot(Transform parent)
        {
            root = new GameObject("Slingshot").transform;
            root.SetParent(parent, false);
            var wood = Look.Lit(Themes.Hex(0xC07F43), 0.35f);
            var woodDark = Look.Lit(Themes.Hex(0x8E5729), 0.35f);
            var rubber = Look.Lit(Themes.Hex(0x5B2A20), 0.45f);
            for (int i = 0; i < 2; i++)
            {
                var post = new GameObject("Post").transform;
                post.SetParent(root, false);
                Look.AddMesh("Foot", Meshes.SoftBox(new Vector3(0.24f, 0.24f, 0.07f), 0.03f), woodDark, post, new Vector3(0f, 0f, -0.035f), false);
                Look.AddMesh("Wood", Meshes.SoftBox(new Vector3(0.15f, 0.15f, PostHeight), 0.055f), wood, post, new Vector3(0f, 0f, -PostHeight / 2f));
                Look.AddMesh("Wrap", Meshes.SoftBox(new Vector3(0.18f, 0.18f, 0.09f), 0.04f), rubber, post, new Vector3(0f, 0f, -BandHeight), false);
                posts[i] = post;
                bands[i] = Look.AddMesh("Band", Meshes.SoftBox(Vector3.one, 0.45f, 3), rubber, root, Vector3.zero, false).transform;
            }
            pouch = Look.AddMesh("Pouch", Meshes.SoftBox(new Vector3(0.4f, 0.13f, 0.17f), 0.06f), rubber, root, Vector3.zero, false).transform;
            root.gameObject.SetActive(false);
        }

        public bool Active => active;

        // While aiming: the rest spot, the shot direction, where the pouch is pulled to, and the power.
        public void Aim(Vector2 rest, Vector2 dir, Vector2 pouchAt, float power, float dt)
        {
            if (!active || releaseTime >= 0f)
            {
                active = true;
                releaseTime = -1f;
                if (!root.gameObject.activeSelf) { shown = 0f; axis = dir; }
                root.gameObject.SetActive(true);
            }
            center = rest;
            float turn = Vector2.SignedAngle(axis, dir);
            axis = Quaternion.Euler(0f, 0f, turn * (1f - Mathf.Exp(-22f * dt))) * axis;
            pouchPos = pouchAt;
            shown = Mathf.MoveTowards(shown, 1f, dt / 0.18f);
            Layout(power);
        }

        // The bands snap the pouch forward through the posts.
        public void Release()
        {
            if (!active) return;
            releaseTime = 0f;
            releaseFrom = pouchPos;
        }

        public void Hide()
        {
            active = false;
            releaseTime = -1f;
            root.gameObject.SetActive(false);
        }

        // After release (or a cancel): the snap-back wobble, then the posts sink away.
        public void Tick(float dt)
        {
            if (!active || releaseTime < 0f) return;
            releaseTime += dt;
            Vector2 rest = center + axis * 0.08f;
            float k = Mathf.Exp(-7f * releaseTime) * Mathf.Cos(30f * releaseTime);
            pouchPos = rest + (releaseFrom - rest) * k;
            if (releaseTime > 0.4f) shown = Mathf.MoveTowards(shown, 0f, dt / 0.22f);
            Layout(0f);
            if (releaseTime > 0.4f && shown <= 0f) Hide();
        }

        void Layout(float power)
        {
            Vector2 side = new Vector2(-axis.y, axis.x);
            float s = Mathf.Max(0.001f, shown >= 1f ? 1f : Tweens.OutBack(shown));
            float angle = Mathf.Atan2(side.y, side.x) * Mathf.Rad2Deg;
            var pouch3 = new Vector3(pouchPos.x, pouchPos.y, -PouchHeight * s);
            for (int i = 0; i < 2; i++)
            {
                float sign = i == 0 ? -1f : 1f;
                Vector2 p = center + side * (sign * Spread) + axis * 0.08f;
                var post = posts[i];
                post.position = new Vector3(p.x, p.y, 0f);
                // Local X along the fork's spread, leaning outward like a Y; it grows up out of the floor.
                post.rotation = Quaternion.Euler(0f, 0f, angle) * Quaternion.Euler(0f, -sign * Lean, 0f);
                post.localScale = new Vector3(1f, 1f, s);
                Vector3 top = post.TransformPoint(new Vector3(0f, 0f, -BandHeight));
                Vector3 end = pouch3 + (Vector3)(side * (sign * 0.17f));
                Vector3 d = end - top;
                float len = Mathf.Max(0.01f, d.magnitude);
                // Thinner the further it stretches, like real rubber.
                float thick = Mathf.Lerp(0.085f, 0.05f, Mathf.Clamp01((len - 0.4f) / 1.2f)) * Mathf.Min(1f, s * 1.5f);
                var band = bands[i];
                band.position = (top + end) * 0.5f;
                band.rotation = Quaternion.LookRotation(d / len, Vector3.back);
                band.localScale = new Vector3(thick, thick, len + thick);
            }
            pouch.position = pouch3;
            pouch.rotation = Quaternion.Euler(0f, 0f, angle);
            pouch.localScale = Vector3.one * Mathf.Min(1f, s) * (1f + 0.15f * power);
        }
    }
}
