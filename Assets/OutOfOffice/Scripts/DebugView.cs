using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace OutOfOffice
{
    // Developer overlay: collider outlines (from the real collider shapes), recent contact normals,
    // puck velocity, and simulation timing. Toggled from the pause menu's developer section.
    public class DebugView : MonoBehaviour
    {
        public static bool Enabled;
        public LevelInstance Level;
        public List<Vector2> PreviewPath;

        Material lineMat;
        readonly Dictionary<Collider2D, Mesh> shapes = new Dictionary<Collider2D, Mesh>();
        int fixedSteps, stepsShown;
        float fpsTimer, fps, frames;

        public string Stats => $"{fps:0} FPS  {stepsShown} STEPS/FRAME  dt {Rules.Dt * 1000f:0.0} ms" +
                               (Level?.Puck != null ? $"  v {Level.Puck.Body.linearVelocity.magnitude:0.00}" : "");

        void OnEnable() => RenderPipelineManager.endCameraRendering += Draw;
        void OnDisable() => RenderPipelineManager.endCameraRendering -= Draw;

        void FixedUpdate() => fixedSteps++;

        void Update()
        {
            stepsShown = fixedSteps;
            fixedSteps = 0;
            frames++;
            fpsTimer += Time.unscaledDeltaTime;
            if (fpsTimer >= 0.5f)
            {
                fps = frames / fpsTimer;
                frames = 0;
                fpsTimer = 0f;
            }
        }

        public void ClearShapes() => shapes.Clear();

        void Draw(ScriptableRenderContext ctx, Camera cam)
        {
            if (!Enabled || Level == null || Level.Root == null || cam != Camera.main) return;
            if (lineMat == null)
            {
                lineMat = new Material(Shader.Find("Hidden/Internal-Colored")) { hideFlags = HideFlags.HideAndDontSave };
                lineMat.SetInt("_ZTest", (int)CompareFunction.Always);
            }
            lineMat.SetPass(0);
            GL.PushMatrix();
            GL.LoadProjectionMatrix(cam.projectionMatrix);
            GL.modelview = cam.worldToCameraMatrix;
            GL.Begin(GL.LINES);

            // Collider outlines, lifted slightly so they read above the board.
            GL.Color(new Color(0f, 1f, 0.4f, 0.9f));
            foreach (var c in Level.Root.GetComponentsInChildren<Collider2D>())
            {
                if (!c.enabled) continue;
                bool moving = c.attachedRigidbody != null && c.attachedRigidbody.bodyType != RigidbodyType2D.Static;
                if (moving || !shapes.TryGetValue(c, out var mesh) || mesh == null)
                {
                    mesh = c.CreateMesh(false, false);
                    if (!moving) shapes[c] = mesh;
                }
                if (mesh == null) continue;
                var v = mesh.vertices;
                var t = mesh.triangles;
                for (int i = 0; i + 2 < t.Length; i += 3)
                {
                    Line(v[t[i]], v[t[i + 1]]);
                    Line(v[t[i + 1]], v[t[i + 2]]);
                    Line(v[t[i + 2]], v[t[i]]);
                }
                if (moving) Destroy(mesh);
            }

            // Recent contact normals, fading over 1.5 seconds.
            foreach (var m in Puck.RecentContacts)
            {
                float age = Time.time - m.Time;
                if (age > 1.5f) continue;
                GL.Color(new Color(1f, 0.2f, 0.2f, 1f - age / 1.5f));
                Line(m.Point, m.Point + m.Normal * 0.6f);
            }

            // Puck velocity (scaled) and the aim preview.
            var puck = Level.Puck;
            if (puck != null)
            {
                GL.Color(Color.cyan);
                Vector2 p = puck.Body.position;
                Line(p, p + puck.Body.linearVelocity * 0.15f);
            }
            if (PreviewPath != null)
            {
                GL.Color(Color.yellow);
                for (int i = 0; i + 1 < PreviewPath.Count; i++) Line(PreviewPath[i], PreviewPath[i + 1]);
            }
            GL.End();
            GL.PopMatrix();
        }

        static void Line(Vector3 a, Vector3 b)
        {
            a.z = b.z = -0.7f;
            GL.Vertex(a);
            GL.Vertex(b);
        }
    }
}
