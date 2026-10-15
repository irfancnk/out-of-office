using System.Collections.Generic;
using UnityEngine;

namespace OutOfOffice
{
    // Procedural meshes for the 2.5D look. The board lies in the XY plane and "up" is -Z (toward the camera).
    public static class Meshes
    {
        static readonly Vector3 Up = new Vector3(0f, 0f, -1f);
        static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();
        static readonly HashSet<Mesh> shared = new HashSet<Mesh>();

        // Marks a mesh as shared across levels, so level teardown keeps it.
        public static bool IsShared(Mesh mesh) => shared.Contains(mesh);

        public static Mesh Keep(Mesh mesh)
        {
            shared.Add(mesh);
            return mesh;
        }

        // Destroys the meshes a level built for itself (floor, rim, rods, shadows, extrusions), keeping cached ones.
        // Call before destroying the level root, so repeated restarts do not pile up native mesh memory.
        public static void ReleaseLevel(GameObject root)
        {
            if (root == null) return;
            var owned = new HashSet<Mesh>();
            foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var m = mf.sharedMesh;
                if (m != null && !shared.Contains(m)) owned.Add(m);
            }
            foreach (var m in owned)
            {
                if (Application.isPlaying) Object.Destroy(m);
                else Object.DestroyImmediate(m);
            }
        }

        // A rounded rectangle extruded from z = 0 up to z = -height, with a rounded bevel on the top edge.
        // Use w == h == 2 * radius for a disc.
        public static Mesh RoundedPrism(float w, float h, float radius, float height, float bevel,
            bool topCap = true, int cornerSegments = 6)
        {
            string key = $"prism {w:0.###} {h:0.###} {radius:0.###} {height:0.###} {bevel:0.###} {topCap} {cornerSegments}";
            if (cache.TryGetValue(key, out var cached)) return cached;

            radius = Mathf.Min(radius, Mathf.Min(w, h) * 0.5f);
            bevel = Mathf.Min(bevel, radius * 0.9f, height);
            var outline = new List<Vector2>();
            var outNormals = new List<Vector2>();
            Vector2 c = new Vector2(w * 0.5f - radius, h * 0.5f - radius);
            Vector2[] centers = { new Vector2(c.x, c.y), new Vector2(-c.x, c.y), new Vector2(-c.x, -c.y), new Vector2(c.x, -c.y) };
            for (int k = 0; k < 4; k++)
            for (int i = 0; i <= cornerSegments; i++)
            {
                float a = (k * 90f + 90f * i / cornerSegments) * Mathf.Deg2Rad;
                var n = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                outline.Add(centers[k] + n * radius);
                outNormals.Add(n);
            }

            // Profile rings from the base to the top of the bevel.
            var ringOffset = new List<float> { 0f, 0f };
            var ringZ = new List<float> { 0f, -(height - bevel) };
            var ringPhi = new List<float> { 0f, 0f };
            const int bevelSteps = 4;
            if (bevel > 0f)
            {
                for (int i = 1; i <= bevelSteps; i++)
                {
                    float phi = 0.5f * Mathf.PI * i / bevelSteps;
                    ringOffset.Add(bevel * (1f - Mathf.Cos(phi)));
                    ringZ.Add(-(height - bevel) - bevel * Mathf.Sin(phi));
                    ringPhi.Add(phi);
                }
            }

            var b = new Builder();
            int n0 = outline.Count;
            int[] ringStart = new int[ringZ.Count];
            for (int r = 0; r < ringZ.Count; r++)
            {
                ringStart[r] = b.Verts.Count;
                for (int i = 0; i < n0; i++)
                {
                    Vector2 p = outline[i] - outNormals[i] * ringOffset[r];
                    Vector3 side = new Vector3(outNormals[i].x, outNormals[i].y, 0f);
                    Vector3 normal = (side * Mathf.Cos(ringPhi[r]) + Up * Mathf.Sin(ringPhi[r])).normalized;
                    b.Add(new Vector3(p.x, p.y, ringZ[r]), normal);
                }
            }
            for (int r = 0; r + 1 < ringZ.Count; r++)
            for (int i = 0; i < n0; i++)
            {
                int j = (i + 1) % n0;
                int a0 = ringStart[r] + i, a1 = ringStart[r] + j, b0 = ringStart[r + 1] + i, b1 = ringStart[r + 1] + j;
                Vector3 facing = b.Normals[a0] + b.Normals[b1];
                b.Tri(a0, a1, b1, facing);
                b.Tri(a0, b1, b0, facing);
            }

            if (topCap)
            {
                int last = ringStart[ringZ.Count - 1];
                float topZ = ringZ[ringZ.Count - 1];
                int center = b.Add(new Vector3(0f, 0f, topZ), Up);
                int capStart = b.Verts.Count;
                for (int i = 0; i < n0; i++) b.Add(b.Verts[last + i], Up);
                for (int i = 0; i < n0; i++) b.Tri(center, capStart + i, capStart + (i + 1) % n0, Up);
            }

            return cache[key] = Keep(b.Build(key));
        }

        // The board surface at z = 0: the map outline with a round hole cut out.
        public static Mesh Floor(IList<Vector2> outline, Vector2 hole, float radius)
        {
            var tris = new List<int>();
            var verts = Geometry.Triangulate(outline, radius > 0f ? Geometry.Circle(hole, radius, 64) : null, tris);
            var b = new Builder();
            foreach (var v in verts) b.Add(new Vector3(v.x, v.y, 0f), Up);
            for (int i = 0; i + 2 < tris.Count; i += 3) b.Tri(tris[i], tris[i + 1], tris[i + 2], Up);
            return b.Build("floor");
        }

        // Outer walls that follow the map outline: inner face, rounded top, and the base below the board.
        public static Mesh Rim(IList<Vector2> outline, float thickness, float height, float bevel, float baseDepth)
        {
            Geometry.Miters(outline, out var normals, out var miter);
            int n = outline.Count;

            // Profile across the rim: (offset from the inner edge, z, side weight, up weight).
            var profile = new List<Vector4> { new Vector4(0f, 0f, -1f, 0f), new Vector4(0f, -(height - bevel), -1f, 0f) };
            const int steps = 4;
            for (int i = 1; i <= steps; i++)
            {
                float phi = 0.5f * Mathf.PI * i / steps;
                profile.Add(new Vector4(bevel * (1f - Mathf.Cos(phi)), -(height - bevel) - bevel * Mathf.Sin(phi), -Mathf.Cos(phi), Mathf.Sin(phi)));
            }
            for (int i = 0; i <= steps; i++)
            {
                float th = 0.5f * Mathf.PI * i / steps;
                profile.Add(new Vector4(thickness - bevel + bevel * Mathf.Sin(th), -(height - bevel) - bevel * Mathf.Cos(th), Mathf.Sin(th), Mathf.Cos(th)));
            }
            profile.Add(new Vector4(thickness, baseDepth, 1f, 0f));

            var b = new Builder();
            for (int r = 0; r < profile.Count; r++)
            {
                var pr = profile[r];
                for (int i = 0; i < n; i++)
                {
                    Vector2 p = outline[i] + normals[i] * (pr.x * miter[i]);
                    Vector3 side = new Vector3(normals[i].x, normals[i].y, 0f);
                    b.Add(new Vector3(p.x, p.y, pr.y), (side * pr.z + Up * pr.w).normalized);
                }
            }
            for (int r = 0; r + 1 < profile.Count; r++)
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                int a0 = r * n + i, a1 = r * n + j, b0 = (r + 1) * n + i, b1 = (r + 1) * n + j;
                Vector3 facing = b.Normals[a0] + b.Normals[b1];
                b.Tri(a0, a1, b1, facing);
                b.Tri(a0, b1, b0, facing);
            }
            return b.Build("rim");
        }

        // A flat polygon extruded from z = 0 up to z = -height.
        public static Mesh Extrude(IList<Vector2> polygon, float height)
        {
            var tris = new List<int>();
            var verts = Geometry.Triangulate(polygon, null, tris);
            var b = new Builder();
            Vector3 top = new Vector3(0f, 0f, -height);
            foreach (var v in verts) b.Add((Vector3)v + top, Up);
            for (int i = 0; i + 2 < tris.Count; i += 3) b.Tri(tris[i], tris[i + 1], tris[i + 2], Up);
            int n = verts.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 p = verts[i], q = verts[(i + 1) % n];
                Vector2 e = (q - p).normalized;
                var side = new Vector3(e.y, -e.x, 0f);
                int a0 = b.Add(p, side), a1 = b.Add(q, side), b0 = b.Add((Vector3)p + top, side), b1 = b.Add((Vector3)q + top, side);
                b.Tri(a0, a1, b1, side);
                b.Tri(a0, b1, b0, side);
            }
            return b.Build("extrude");
        }

        // A ramp: w wide, `length` long along +Y, rising from 0 at the back to `height` at the front.
        public static Mesh Wedge(float w, float length, float height)
        {
            string key = $"wedge {w:0.###} {length:0.###} {height:0.###}";
            if (cache.TryGetValue(key, out var cached)) return cached;

            float x = w / 2f, y = length / 2f;
            Vector3 bl = new Vector3(-x, -y, 0f), br = new Vector3(x, -y, 0f);
            Vector3 fl = new Vector3(-x, y, 0f), fr = new Vector3(x, y, 0f);
            Vector3 tl = new Vector3(-x, y, -height), tr = new Vector3(x, y, -height);
            var b = new Builder();
            Vector3 slope = Vector3.Cross(tr - br, bl - br).normalized;
            if (slope.z > 0f) slope = -slope;
            Quad(b, bl, br, tr, tl, slope);
            Quad(b, fl, fr, tr, tl, Vector3.up);
            Tri(b, bl, fl, tl, Vector3.left);
            Tri(b, br, fr, tr, Vector3.right);
            return cache[key] = Keep(b.Build(key));
        }

        static void Quad(Builder b, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 n)
        {
            int a = b.Add(p0, n), c = b.Add(p1, n), d = b.Add(p2, n), e = b.Add(p3, n);
            b.Tri(a, c, d, n);
            b.Tri(a, d, e, n);
        }

        static void Tri(Builder b, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 n)
        {
            b.Tri(b.Add(p0, n), b.Add(p1, n), b.Add(p2, n), n);
        }

        // Rotates a profile of (radius, height) points around the Z axis. Height grows toward the camera (-Z).
        public static Mesh Lathe(IList<Vector2> profile, int segments = 32)
        {
            string key = "lathe " + segments;
            foreach (var p in profile) key += $" {p.x:0.###},{p.y:0.###}";
            if (cache.TryGetValue(key, out var cached)) return cached;

            int m = profile.Count;
            var pn = new Vector2[m];
            for (int j = 0; j < m; j++)
            {
                Vector2 a = profile[Mathf.Max(j - 1, 0)], b = profile[Mathf.Min(j + 1, m - 1)];
                Vector2 t = (b - a).normalized;
                pn[j] = new Vector2(t.y, -t.x);
            }
            var b2 = new Builder();
            for (int i = 0; i <= segments; i++)
            {
                float ang = 2f * Mathf.PI * i / segments;
                float c = Mathf.Cos(ang), sn = Mathf.Sin(ang);
                for (int j = 0; j < m; j++)
                    b2.Add(new Vector3(c * profile[j].x, sn * profile[j].x, -profile[j].y), new Vector3(c * pn[j].x, sn * pn[j].x, -pn[j].y).normalized);
            }
            for (int i = 0; i < segments; i++)
            for (int j = 0; j + 1 < m; j++)
            {
                int a0 = i * m + j, a1 = i * m + j + 1, c0 = (i + 1) * m + j, c1 = (i + 1) * m + j + 1;
                Vector3 facing = b2.Normals[a0] + b2.Normals[c1];
                b2.Tri(a0, c0, c1, facing);
                b2.Tri(a0, c1, a1, facing);
            }
            return cache[key] = Keep(b2.Build(key));
        }

        // A rod along an open path: flat-ish top with rounded edges, and round ends. Sits on the board.
        public static Mesh Rod(IList<Vector2> path, float width, float height, float bevel)
        {
            Geometry.PathFrames(path, out var tan, out var nrm);
            float hw = width * 0.5f;
            bevel = Mathf.Min(bevel, hw * 0.95f, height);

            // Half profile from the base to the top center: (s, h, normal side, normal up).
            var half = new List<Vector4> { new Vector4(hw, 0f, 1f, 0f), new Vector4(hw, height - bevel, 1f, 0f) };
            const int steps = 5;
            for (int i = 1; i <= steps; i++)
            {
                float phi = 0.5f * Mathf.PI * i / steps;
                half.Add(new Vector4(hw - bevel + bevel * Mathf.Cos(phi), height - bevel + bevel * Mathf.Sin(phi), Mathf.Cos(phi), Mathf.Sin(phi)));
            }
            half.Add(new Vector4(0f, height, 0f, 1f));

            // Full cross-section: left side up, across the top, right side down.
            var full = new List<Vector4>();
            for (int i = 0; i < half.Count; i++) full.Add(new Vector4(-half[i].x, half[i].y, -half[i].z, half[i].w));
            for (int i = half.Count - 2; i >= 0; i--) full.Add(half[i]);

            var b = new Builder { UvScale = 0.35f };
            int m = full.Count, n = path.Count;
            for (int i = 0; i < n; i++)
            for (int j = 0; j < m; j++)
            {
                var pr = full[j];
                Vector2 p = path[i] + nrm[i] * -pr.x;
                Vector3 side = new Vector3(nrm[i].x, nrm[i].y, 0f) * -pr.z;
                b.Add(new Vector3(p.x, p.y, -pr.y), (side + Up * pr.w).normalized);
            }
            for (int i = 0; i + 1 < n; i++)
            for (int j = 0; j + 1 < m; j++)
            {
                int a0 = i * m + j, a1 = i * m + j + 1, c0 = (i + 1) * m + j, c1 = (i + 1) * m + j + 1;
                Vector3 facing = b.Normals[a0] + b.Normals[c1];
                b.Tri(a0, a1, c1, facing);
                b.Tri(a0, c1, c0, facing);
            }

            // Round end caps: the half profile swept around each end point.
            for (int end = 0; end < 2; end++)
            {
                int idx = end == 0 ? 0 : n - 1;
                Vector2 e = path[idx], t = end == 0 ? -tan[idx] : tan[idx], nn = nrm[idx];
                const int capSteps = 10;
                int start = b.Verts.Count;
                for (int k = 0; k <= capSteps; k++)
                {
                    float th = Mathf.PI * k / capSteps;
                    Vector2 d = nn * Mathf.Cos(th) + t * Mathf.Sin(th);
                    for (int j = 0; j < half.Count; j++)
                    {
                        var pr = half[j];
                        Vector2 p = e + d * pr.x;
                        b.Add(new Vector3(p.x, p.y, -pr.y), (new Vector3(d.x, d.y, 0f) * pr.z + Up * pr.w).normalized);
                    }
                }
                int hm = half.Count;
                for (int k = 0; k < capSteps; k++)
                for (int j = 0; j + 1 < hm; j++)
                {
                    int a0 = start + k * hm + j, a1 = a0 + 1, c0 = a0 + hm, c1 = c0 + 1;
                    Vector3 facing = b.Normals[a0] + b.Normals[c1];
                    b.Tri(a0, a1, c1, facing);
                    b.Tri(a0, c1, c0, facing);
                }
            }
            return b.Build("rod");
        }

        // Soft contact shadow: the outline filled at `alpha`, fading to 0 over `spread` outside it.
        // A negative spread fades inward instead and leaves the middle empty (used for the rim's inner shade).
        public static Mesh SoftShadow(IList<Vector2> outline, float spread, float alpha)
        {
            var poly = Geometry.CounterClockwise(outline);
            Geometry.Miters(poly, out var normals, out var miter);
            int n = poly.Count;
            var b = new Builder();
            var solid = new Color(1f, 1f, 1f, alpha);
            var clear = new Color(1f, 1f, 1f, 0f);
            if (spread > 0f)
            {
                var tris = new List<int>();
                var verts = Geometry.Triangulate(poly, null, tris);
                foreach (var v in verts) b.Add(v, Up, solid);
                for (int i = 0; i + 2 < tris.Count; i += 3) b.Tri(tris[i], tris[i + 1], tris[i + 2], Up);
            }
            int ring = b.Verts.Count;
            for (int i = 0; i < n; i++)
            {
                // Where the offset folds over itself (tight corners turning away from it), keep the fade short.
                Vector2 e1 = poly[i] - poly[(i + n - 1) % n], e2 = poly[(i + 1) % n] - poly[i];
                bool folds = (e1.x * e2.y - e1.y * e2.x) * spread < 0f;
                float length = folds ? Mathf.Sign(spread) * Mathf.Min(Mathf.Abs(spread), 0.22f) : spread * Mathf.Min(miter[i], 2f);
                b.Add(poly[i], Up, solid);
                b.Add(poly[i] + normals[i] * length, Up, clear);
            }
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                int a0 = ring + i * 2, a1 = a0 + 1, c0 = ring + j * 2, c1 = c0 + 1;
                b.Tri(a0, a1, c1, Up);
                b.Tri(a0, c1, c0, Up);
            }
            return b.Build("shadow");
        }

        // Inside wall and floor of the hole, from z = 0 down to z = depth.
        public static Mesh HoleTube(float radius, float depth)
        {
            string key = $"hole {radius:0.###} {depth:0.###}";
            if (cache.TryGetValue(key, out var cached)) return cached;

            var b = new Builder();
            const int steps = 48;
            for (int i = 0; i <= steps; i++)
            {
                float a = 2f * Mathf.PI * i / steps;
                var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                b.Add(d * radius, -d);
                b.Add(d * radius + new Vector3(0f, 0f, depth), -d);
            }
            for (int i = 0; i < steps; i++)
            {
                int a0 = i * 2, a1 = i * 2 + 1, b0 = i * 2 + 2, b1 = i * 2 + 3;
                Vector3 facing = b.Normals[a0];
                b.Tri(a0, b0, b1, facing);
                b.Tri(a0, b1, a1, facing);
            }
            int center = b.Add(new Vector3(0f, 0f, depth), Up);
            int ring = b.Verts.Count;
            for (int i = 0; i < steps; i++)
            {
                float a = 2f * Mathf.PI * i / steps;
                b.Add(new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, depth), Up);
            }
            for (int i = 0; i < steps; i++) b.Tri(center, ring + i, ring + (i + 1) % steps, Up);
            return cache[key] = Keep(b.Build(key));
        }

        // A soft, fluffy blob: an ellipsoid (radius r, half height h) resting on the board, with rounded clumps of fur
        // in its silhouette. The bottom sits at z = 0 and the top at z = -2h.
        // `pear` narrows the top and widens the bottom (0 = an even ellipsoid), for a soft mochi shape.
        public static Mesh Fluff(float r, float h, float clump = 0.06f, float pear = 0f)
        {
            string key = $"fluff {r:0.###} {h:0.###} {clump:0.###} {pear:0.###}";
            if (cache.TryGetValue(key, out var cached)) return cached;
            const int lon = 40, lat = 22;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            for (int i = 0; i <= lat; i++)
            {
                float th = Mathf.PI * i / lat;
                for (int j = 0; j <= lon; j++)
                {
                    float ph = 2f * Mathf.PI * j / lon;
                    var d = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Sin(th) * Mathf.Sin(ph), Mathf.Cos(th));
                    // Clumps: smooth bumps that never dip below the base surface, fading at the poles.
                    float f = 0.5f + 0.5f * (Mathf.Sin(9f * ph + 1.7f * th) * Mathf.Sin(7f * th) * 0.6f + Mathf.Sin(14f * ph - 0.6f) * Mathf.Sin(11f * th + 0.3f) * 0.4f);
                    float bump = 1f + clump * Mathf.Pow(Mathf.Clamp01(f), 1.6f) * Mathf.Sin(th);
                    float z = -(h + d.z * h * Mathf.Lerp(1f, bump, 0.5f));
                    if (z > -0.005f) z = -0.005f;  // flat bottom, so it sits on the board
                    float widen = 1f - pear * d.z;  // d.z = +1 at the top, -1 at the bottom
                    verts.Add(new Vector3(d.x * r * bump * widen, d.y * r * bump * widen, z));
                    uvs.Add(new Vector2(j / (float)lon * 5f, i / (float)lat * 3f));
                }
            }
            int row = lon + 1;
            for (int i = 0; i < lat; i++)
            for (int j = 0; j < lon; j++)
            {
                int a0 = i * row + j, a1 = a0 + 1, b0 = a0 + row, b1 = b0 + 1;
                // Winding: outward faces toward the camera side (-Z is up).
                tris.Add(a0); tris.Add(b1); tris.Add(b0);
                tris.Add(a0); tris.Add(a1); tris.Add(b1);
            }
            var mesh = new Mesh { name = key };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            // Smooth the seam and the poles, which RecalculateNormals sees as separate vertices.
            var n = mesh.normals;
            for (int i = 0; i <= lat; i++)
            {
                var avg = (n[i * row] + n[i * row + lon]).normalized;
                n[i * row] = n[i * row + lon] = avg;
            }
            // Make sure faces and normals point outward (away from the center).
            Vector3 center = new Vector3(0f, 0f, -h);
            if (Vector3.Dot(n[(lat / 2) * row], verts[(lat / 2) * row] - center) < 0f)
            {
                for (int k = 0; k < n.Length; k++) n[k] = -n[k];
                tris.Reverse();
                mesh.SetTriangles(tris, 0);
            }
            for (int j = 0; j <= lon; j++)
            {
                n[j] = Vector3.back;               // top pole faces up (-Z)
                n[lat * row + j] = Vector3.forward;
            }
            mesh.normals = n;
            mesh.RecalculateBounds();
            return cache[key] = Keep(mesh);
        }

        // A smooth tapered tube along a 3D path (hair curls, stems). Not cached: callers keep the result.
        public static Mesh Tube(IList<Vector3> path, float startRadius, float endRadius, int sides = 10)
        {
            string key = $"tube {path.Count} {startRadius:0.###} {endRadius:0.###} {sides}";
            foreach (var p in path) key += $" {p.x:0.###},{p.y:0.###},{p.z:0.###}";
            if (cache.TryGetValue(key, out var cached)) return cached;
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var tris = new List<int>();
            int n = path.Count;
            // Parallel-transported frames: the ring never flips or twists along the path.
            Vector3 nrm = Vector3.zero;
            for (int i = 0; i < n; i++)
            {
                Vector3 t = (path[Mathf.Min(i + 1, n - 1)] - path[Mathf.Max(i - 1, 0)]).normalized;
                if (i == 0) nrm = Vector3.Cross(t, Mathf.Abs(t.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                else nrm = (nrm - t * Vector3.Dot(nrm, t)).normalized;
                Vector3 bin = Vector3.Cross(t, nrm);
                float r = Mathf.Lerp(startRadius, endRadius, i / (n - 1f));
                for (int k = 0; k <= sides; k++)
                {
                    float a = Mathf.PI * 2f * k / sides;
                    Vector3 dir = nrm * Mathf.Cos(a) + bin * Mathf.Sin(a);
                    verts.Add(path[i] + dir * r);
                    norms.Add(dir);
                }
            }
            int row = sides + 1;
            for (int i = 0; i + 1 < n; i++)
            for (int k = 0; k < sides; k++)
            {
                int a0 = i * row + k, a1 = a0 + 1, b0 = a0 + row, b1 = b0 + 1;
                tris.Add(a0); tris.Add(b0); tris.Add(a1);
                tris.Add(a1); tris.Add(b0); tris.Add(b1);
            }
            var mesh = new Mesh { name = key };
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetTriangles(tris, 0);
            // Make faces point outward whatever the path handedness.
            mesh.RecalculateNormals();
            var rn = mesh.normals;
            if (Vector3.Dot(rn[row / 2], norms[row / 2]) < 0f)
            {
                tris.Reverse();
                mesh.SetTriangles(tris, 0);
            }
            mesh.SetNormals(norms);
            mesh.RecalculateBounds();
            return cache[key] = Keep(mesh);
        }

        // A closed surface around `center` whose distance along each unit direction is radius(dir): hair, heads,
        // cushions. The poles lie on the X axis (or `axis` times X). Normals come from the surface itself, so there is no seam.
        public static Mesh Radial(string key, System.Func<Vector3, float> radius, Vector3 center, int lon = 48, int lat = 32, Quaternion? axis = null)
        {
            key = "radial " + key;
            if (cache.TryGetValue(key, out var cached)) return cached;
            var frame = axis ?? Quaternion.identity;
            Vector3 Dir(float a, float b) => frame * new Vector3(Mathf.Cos(b), Mathf.Sin(b) * Mathf.Cos(a), Mathf.Sin(b) * Mathf.Sin(a));
            Vector3 Point(float a, float b) { var d = Dir(a, b); return center + d * radius(d); }
            var bld = new Builder();
            const float eps = 0.002f;
            for (int j = 0; j <= lat; j++)
            for (int i = 0; i <= lon; i++)
            {
                float a = 2f * Mathf.PI * i / lon, b = Mathf.PI * j / lat;
                Vector3 p = Point(a, b);
                float bb = Mathf.Clamp(b, 0.01f, Mathf.PI - 0.01f);
                Vector3 n = Vector3.Cross(Point(a + eps, bb) - Point(a - eps, bb), Point(a, bb + eps) - Point(a, bb - eps));
                // Divide by hand: Vector3.Normalize zeroes vectors shorter than 1e-5, and these are about 1e-6.
                n = n.sqrMagnitude < 1e-18f || j == 0 || j == lat ? Dir(a, b) : n / n.magnitude;
                if (Vector3.Dot(n, p - center) < 0f) n = -n;
                bld.Add(p, n);
            }
            int row = lon + 1;
            for (int j = 0; j < lat; j++)
            for (int i = 0; i < lon; i++)
            {
                int a0 = j * row + i, a1 = a0 + 1, b0 = a0 + row, b1 = b0 + 1;
                Vector3 outward = bld.Verts[a0] + bld.Verts[a1] + bld.Verts[b0] + bld.Verts[b1] - 4f * center;
                bld.Tri(a0, a1, b1, outward);
                bld.Tri(a0, b1, b0, outward);
            }
            return cache[key] = Keep(bld.Build(key));
        }

        // A flat ring lying on the board.
        public static Mesh Ring(float inner, float outer)
        {
            string key = $"ring {inner:0.###} {outer:0.###}";
            if (cache.TryGetValue(key, out var cached)) return cached;

            var b = new Builder();
            const int steps = 48;
            for (int i = 0; i < steps; i++)
            {
                float a = 2f * Mathf.PI * i / steps;
                var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                b.Add(d * inner, Up);
                b.Add(d * outer, Up);
            }
            for (int i = 0; i < steps; i++)
            {
                int j = (i + 1) % steps;
                b.Tri(i * 2, i * 2 + 1, j * 2 + 1, Up);
                b.Tri(i * 2, j * 2 + 1, j * 2, Up);
            }
            return cache[key] = Keep(b.Build(key));
        }

        // A box of the given full size, centered on the origin, with every edge and corner rounded by `radius`:
        // the soft-block look shared by the people, hats, and props.
        public static Mesh SoftBox(Vector3 size, float radius, int arc = 4)
        {
            string key = $"softbox {size.x:0.####} {size.y:0.####} {size.z:0.####} {radius:0.####} {arc}";
            if (cache.TryGetValue(key, out var cached)) return cached;
            Vector3 half = size * 0.5f;
            radius = Mathf.Clamp(radius, 0.0005f, Mathf.Min(half.x, Mathf.Min(half.y, half.z)));
            Vector3 inner = half - Vector3.one * radius;

            // Grid positions along one axis, on the bounding box: dense over both rounded ends, sparse in the flat middle.
            List<float> Axis(float inn)
            {
                var list = new List<float>();
                void Push(float v) { if (list.Count == 0 || v - list[list.Count - 1] > 1e-5f) list.Add(v); }
                for (int i = 0; i <= arc; i++) Push(-inn - radius * Mathf.Cos(0.5f * Mathf.PI * i / arc));
                int mid = Mathf.Max(1, Mathf.CeilToInt(2f * inn / 0.2f));
                for (int i = 1; i < mid; i++) Push(Mathf.Lerp(-inn, inn, i / (float)mid));
                for (int i = 0; i <= arc; i++) Push(inn + radius * Mathf.Sin(0.5f * Mathf.PI * i / arc));
                return list;
            }
            var axes = new[] { Axis(inner.x), Axis(inner.y), Axis(inner.z) };

            var b = new Builder();
            for (int n = 0; n < 3; n++)
            foreach (float sign in new[] { -1f, 1f })
            {
                int ua = (n + 1) % 3, va = (n + 2) % 3;
                var us = axes[ua];
                var vs = axes[va];
                int start = b.Verts.Count;
                for (int j = 0; j < vs.Count; j++)
                for (int i = 0; i < us.Count; i++)
                {
                    var p = Vector3.zero;
                    p[n] = sign * half[n];
                    p[ua] = us[i];
                    p[va] = vs[j];
                    var q = new Vector3(Mathf.Clamp(p.x, -inner.x, inner.x), Mathf.Clamp(p.y, -inner.y, inner.y), Mathf.Clamp(p.z, -inner.z, inner.z));
                    Vector3 d = p - q;
                    Vector3 normal = d.sqrMagnitude > 1e-12f ? d / d.magnitude : Vector3.zero;
                    if (normal == Vector3.zero) normal[n] = sign;
                    b.Add(q + normal * radius, normal);
                }
                var facing = Vector3.zero;
                facing[n] = sign;
                for (int j = 0; j + 1 < vs.Count; j++)
                for (int i = 0; i + 1 < us.Count; i++)
                {
                    int a0 = start + j * us.Count + i, a1 = a0 + 1, b0 = a0 + us.Count, b1 = b0 + 1;
                    b.Tri(a0, a1, b1, facing);
                    b.Tri(a0, b1, b0, facing);
                }
            }
            return cache[key] = Keep(b.Build(key));
        }

        class Builder
        {
            public readonly List<Vector3> Verts = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            readonly List<Vector2> uvs = new List<Vector2>();
            readonly List<Color> colors = new List<Color>();
            readonly List<int> tris = new List<int>();

            // Planar UVs picked by the face direction, in world units times this scale.
            public float UvScale = 0.15f;

            public int Add(Vector3 v, Vector3 n) => Add(v, n, Color.white);

            public int Add(Vector3 v, Vector3 n, Color c)
            {
                Verts.Add(v);
                Normals.Add(n);
                colors.Add(c);
                Vector2 uv = Mathf.Abs(n.z) > 0.6f ? new Vector2(v.x, v.y)
                    : Mathf.Abs(n.x) > Mathf.Abs(n.y) ? new Vector2(v.y, -v.z) : new Vector2(v.x, -v.z);
                uvs.Add(uv * UvScale);
                return Verts.Count - 1;
            }

            // Adds a triangle and fixes its winding so it faces along `facing`.
            public void Tri(int a, int b, int c, Vector3 facing)
            {
                Vector3 n = Vector3.Cross(Verts[b] - Verts[a], Verts[c] - Verts[a]);
                if (n.sqrMagnitude < 1e-12f) return;
                if (Vector3.Dot(n, facing) < 0f) (b, c) = (c, b);
                tris.Add(a);
                tris.Add(b);
                tris.Add(c);
            }

            public Mesh Build(string name)
            {
                var mesh = new Mesh { name = name };
                if (Verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(Verts);
                mesh.SetNormals(Normals);
                mesh.SetUVs(0, uvs);
                mesh.SetColors(colors);
                mesh.SetTriangles(tris, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
