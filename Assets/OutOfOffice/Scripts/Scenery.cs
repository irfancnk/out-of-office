using System.Collections.Generic;
using UnityEngine;

namespace OutOfOffice
{
    // The world around the board: a plain pastel ground in the chapter color with a soft light pool under the
    // tray. Hyper-casual: nothing competes with the board. Also holds the shared lathe profiles.
    public static class Scenery
    {
        public static void Build(LevelInstance inst, Theme theme, List<Vector2> outline, List<Vector2> outer)
        {
            var root = new GameObject("Scenery").transform;
            root.SetParent(inst.Root.transform, false);
            Rect b = inst.Bounds;
            Look.AddMesh("Ground", Meshes.RoundedPrism(220f, 220f, 1f, 0.01f, 0f, true, 2), Look.Flat(theme.Ground, 0.05f),
                root, new Vector3(b.center.x, b.center.y, Rules.BaseDepth + 0.01f), false).receiveShadows = true;
            var pool = LevelBuilder.MakeSprite("Light Pool", Textures.Glow, new Color(1f, 1f, 1f, 0.35f), root,
                new Vector3(b.center.x, b.center.y, Rules.BaseDepth), new Vector2(b.width * 2.4f, b.height * 1.8f), -5);
            pool.receiveShadows = false;
        }

        public static List<Vector2> Sphere(float r)
        {
            var p = new List<Vector2>();
            for (int i = 0; i <= 12; i++)
            {
                float a = Mathf.PI * i / 12f;
                p.Add(new Vector2(Mathf.Sin(a) * r, r - Mathf.Cos(a) * r));
            }
            return p;
        }

        public static List<Vector2> Cylinder(float r, float h)
        {
            float bev = Mathf.Min(r * 0.3f, h * 0.3f, 0.03f);
            return new List<Vector2> { new Vector2(r, 0f), new Vector2(r, h - bev), new Vector2(r - bev, h), new Vector2(0f, h) };
        }

        // A ring lying flat, with a round cross-section.
        public static List<Vector2> Torus(float r, float thickness)
        {
            var p = new List<Vector2>();
            for (int i = 0; i <= 12; i++)
            {
                float a = -Mathf.PI / 2f + Mathf.PI * 2f * i / 12f;
                p.Add(new Vector2(r + Mathf.Cos(a) * thickness, thickness + Mathf.Sin(a) * thickness));
            }
            return p;
        }

    }
}
