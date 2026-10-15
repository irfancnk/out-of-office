using System.Collections.Generic;
using UnityEngine;

namespace OutOfOffice
{
    // 2D polygon helpers for map shapes. Polygons are counter-clockwise lists of points.
    public static class Geometry
    {
        public static float SignedArea(IList<Vector2> p)
        {
            float a = 0f;
            for (int i = 0; i < p.Count; i++)
            {
                Vector2 u = p[i], v = p[(i + 1) % p.Count];
                a += u.x * v.y - v.x * u.y;
            }
            return a * 0.5f;
        }

        public static List<Vector2> CounterClockwise(IList<Vector2> p)
        {
            var list = new List<Vector2>(p);
            if (SignedArea(list) < 0f) list.Reverse();
            return list;
        }

        // Rounds every corner turning more than minTurnDeg with an arc of the given radius. The radius shrinks on short edges.
        // Larger arcs get more segments, so big round ends stay smooth.
        public static List<Vector2> Fillet(IList<Vector2> pts, float radius, float minTurnDeg = 0.6f)
        {
            var result = new List<Vector2>();
            int n = pts.Count;
            for (int i = 0; i < n; i++)
            {
                Vector2 prev = pts[(i + n - 1) % n], cur = pts[i], next = pts[(i + 1) % n];
                Vector2 d1 = (prev - cur).normalized, d2 = (next - cur).normalized;
                float angle = Vector2.Angle(d1, d2) * Mathf.Deg2Rad;
                if (radius <= 0f || angle > Mathf.PI - minTurnDeg * Mathf.Deg2Rad || angle < 0.01f)
                {
                    result.Add(cur);
                    continue;
                }
                float half = angle * 0.5f;
                float maxTangent = 0.5f * Mathf.Min((prev - cur).magnitude, (next - cur).magnitude);
                float r = Mathf.Min(radius, maxTangent * Mathf.Tan(half));
                float t = r / Mathf.Tan(half);
                Vector2 p1 = cur + d1 * t, p2 = cur + d2 * t;
                Vector2 center = cur + (d1 + d2).normalized * (r / Mathf.Sin(half));
                float a1 = Mathf.Atan2(p1.y - center.y, p1.x - center.x);
                float a2 = Mathf.Atan2(p2.y - center.y, p2.x - center.x);
                float delta = Mathf.DeltaAngle(a1 * Mathf.Rad2Deg, a2 * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                int segments = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(delta) * r * 8f), 4, 48);
                for (int s = 0; s <= segments; s++)
                {
                    float a = a1 + delta * s / segments;
                    result.Add(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
                }
            }
            return RemoveDuplicates(result);
        }

        public static List<Vector2> Star(Vector2 center, float outer, float inner, int points)
        {
            var list = new List<Vector2>();
            for (int i = 0; i < points * 2; i++)
            {
                float a = Mathf.PI / 2f + i * Mathf.PI / points;
                list.Add(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (i % 2 == 0 ? outer : inner));
            }
            return list;
        }

        // Rounds the inner corners of an open path (the two ends stay put). Used for bent rods.
        public static List<Vector2> FilletOpen(IList<Vector2> pts, float radius)
        {
            if (pts.Count < 3 || radius <= 0f) return new List<Vector2>(pts);
            var result = new List<Vector2> { pts[0] };
            for (int i = 1; i < pts.Count - 1; i++)
            {
                Vector2 prev = pts[i - 1], cur = pts[i], next = pts[i + 1];
                Vector2 d1 = (prev - cur).normalized, d2 = (next - cur).normalized;
                float angle = Vector2.Angle(d1, d2) * Mathf.Deg2Rad;
                if (angle > Mathf.PI - 0.01f) { result.Add(cur); continue; }
                float half = angle * 0.5f;
                float limit1 = (prev - cur).magnitude * (i == 1 ? 1f : 0.5f);
                float limit2 = (next - cur).magnitude * (i == pts.Count - 2 ? 1f : 0.5f);
                float r = Mathf.Min(radius, Mathf.Min(limit1, limit2) * Mathf.Tan(half));
                float t = r / Mathf.Tan(half);
                Vector2 p1 = cur + d1 * t, p2 = cur + d2 * t;
                Vector2 center = cur + (d1 + d2).normalized * (r / Mathf.Sin(half));
                float a1 = Mathf.Atan2(p1.y - center.y, p1.x - center.x);
                float a2 = Mathf.Atan2(p2.y - center.y, p2.x - center.x);
                float delta = Mathf.DeltaAngle(a1 * Mathf.Rad2Deg, a2 * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                int segments = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(delta) * r * 8f), 4, 48);
                for (int s = 0; s <= segments; s++)
                {
                    float a = a1 + delta * s / segments;
                    result.Add(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r);
                }
            }
            result.Add(pts[pts.Count - 1]);
            return RemoveDuplicatesOpen(result);
        }

        // Points along a circular arc, angles in degrees (counter-clockwise from +X).
        public static List<Vector2> Arc(Vector2 center, float radius, float fromDeg, float toDeg)
        {
            float sweep = (toDeg - fromDeg) * Mathf.Deg2Rad;
            int segments = Mathf.Clamp(Mathf.CeilToInt(Mathf.Abs(sweep) * radius * 8f), 6, 96);
            var list = new List<Vector2>();
            for (int i = 0; i <= segments; i++)
            {
                float a = fromDeg * Mathf.Deg2Rad + sweep * i / segments;
                list.Add(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
            }
            return list;
        }

        // Unit tangent and left normal at each point of an open path.
        public static void PathFrames(IList<Vector2> path, out Vector2[] tangents, out Vector2[] normals)
        {
            int n = path.Count;
            tangents = new Vector2[n];
            normals = new Vector2[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 t = (path[Mathf.Min(i + 1, n - 1)] - path[Mathf.Max(i - 1, 0)]).normalized;
                tangents[i] = t;
                normals[i] = new Vector2(-t.y, t.x);
            }
        }

        // Outline of a rod of the given width along an open path, with round ends. Counter-clockwise.
        public static List<Vector2> Capsule(IList<Vector2> path, float halfWidth)
        {
            PathFrames(path, out var tan, out var nrm);
            int n = path.Count;
            var outline = new List<Vector2>();
            for (int i = 0; i < n; i++) outline.Add(path[i] - nrm[i] * halfWidth);
            for (int k = 1; k < 8; k++)
            {
                float a = Mathf.PI * k / 8f;
                outline.Add(path[n - 1] + (-nrm[n - 1] * Mathf.Cos(a) + tan[n - 1] * Mathf.Sin(a)) * halfWidth);
            }
            for (int i = n - 1; i >= 0; i--) outline.Add(path[i] + nrm[i] * halfWidth);
            for (int k = 1; k < 8; k++)
            {
                float a = Mathf.PI * k / 8f;
                outline.Add(path[0] + (nrm[0] * Mathf.Cos(a) - tan[0] * Mathf.Sin(a)) * halfWidth);
            }
            return CounterClockwise(outline);
        }

        public static List<Vector2> Circle(Vector2 center, float radius, int segments)
        {
            var list = new List<Vector2>(segments);
            for (int i = 0; i < segments; i++)
            {
                float a = 2f * Mathf.PI * i / segments;
                list.Add(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
            }
            return list;
        }

        // Outward unit normal and miter length at each vertex of a counter-clockwise polygon.
        public static void Miters(IList<Vector2> p, out Vector2[] normals, out float[] miter)
        {
            int n = p.Count;
            normals = new Vector2[n];
            miter = new float[n];
            for (int i = 0; i < n; i++)
            {
                Vector2 e1 = (p[i] - p[(i + n - 1) % n]).normalized;
                Vector2 e2 = (p[(i + 1) % n] - p[i]).normalized;
                Vector2 n1 = new Vector2(e1.y, -e1.x), n2 = new Vector2(e2.y, -e2.x);
                Vector2 nn = (n1 + n2).normalized;
                if (nn.sqrMagnitude < 1e-6f) nn = n1;
                normals[i] = nn;
                miter[i] = 1f / Mathf.Max(0.35f, Vector2.Dot(nn, n1));
            }
        }

        public static Rect Bounds(IList<Vector2> p)
        {
            Vector2 min = p[0], max = p[0];
            foreach (var v in p)
            {
                min = Vector2.Min(min, v);
                max = Vector2.Max(max, v);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        public static float DistanceToRect(Vector2 p, Rect r)
        {
            float dx = Mathf.Max(r.xMin - p.x, 0f, p.x - r.xMax);
            float dy = Mathf.Max(r.yMin - p.y, 0f, p.y - r.yMax);
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        // Ear-clipping triangulation of a counter-clockwise polygon with an optional hole.
        // Returns the vertex list it used; triangle indices go into tris.
        public static List<Vector2> Triangulate(IList<Vector2> outer, IList<Vector2> hole, List<int> tris)
        {
            var verts = new List<Vector2>(CounterClockwise(outer));
            var ring = new List<int>();
            for (int i = 0; i < verts.Count; i++) ring.Add(i);

            if (hole != null && hole.Count >= 3)
            {
                var h = CounterClockwise(hole);
                h.Reverse();  // holes run clockwise
                int offset = verts.Count;
                verts.AddRange(h);

                int m = 0;
                for (int i = 1; i < h.Count; i++) if (h[i].x > h[m].x) m = i;
                Vector2 mp = h[m];

                // Bridge to the nearest outer vertex that the hole vertex can see.
                var candidates = new List<int>();
                for (int i = 0; i < offset; i++) candidates.Add(i);
                candidates.Sort((a, b) => (verts[a] - mp).sqrMagnitude.CompareTo((verts[b] - mp).sqrMagnitude));
                int bridge = candidates[0];
                foreach (int c in candidates)
                {
                    if (Visible(verts, offset, h.Count, offset + m, c))
                    {
                        bridge = c;
                        break;
                    }
                }

                int at = ring.IndexOf(bridge);
                var merged = new List<int>();
                for (int i = 0; i <= at; i++) merged.Add(ring[i]);
                for (int k = 0; k <= h.Count; k++) merged.Add(offset + (m + k) % h.Count);
                merged.Add(bridge);
                for (int i = at + 1; i < ring.Count; i++) merged.Add(ring[i]);
                ring = merged;
            }

            int guard = ring.Count * ring.Count + 10;
            while (ring.Count > 3 && guard-- > 0)
            {
                bool clipped = false;
                int n = ring.Count;
                for (int i = 0; i < n; i++)
                {
                    int ia = ring[(i + n - 1) % n], ib = ring[i], ic = ring[(i + 1) % n];
                    Vector2 a = verts[ia], b = verts[ib], c = verts[ic];
                    if (Cross(b - a, c - b) <= 1e-7f) continue;  // reflex or flat
                    bool empty = true;
                    for (int j = 0; j < n && empty; j++)
                    {
                        Vector2 p = verts[ring[j]];
                        if (p == a || p == b || p == c) continue;
                        if (Cross(ring, verts, j) > 0f) continue;  // only reflex vertices can sit inside an ear
                        if (InTriangle(p, a, b, c)) empty = false;
                    }
                    if (!empty) continue;
                    tris.Add(ia);
                    tris.Add(ib);
                    tris.Add(ic);
                    ring.RemoveAt(i);
                    clipped = true;
                    break;
                }
                if (!clipped)
                {
                    // Degenerate leftovers (collinear runs): drop the flattest vertex and continue.
                    int flat = 0;
                    float best = float.MaxValue;
                    for (int i = 0; i < n; i++)
                    {
                        float cr = Mathf.Abs(Cross(ring, verts, i));
                        if (cr < best) { best = cr; flat = i; }
                    }
                    ring.RemoveAt(flat);
                }
            }
            if (ring.Count == 3)
            {
                tris.Add(ring[0]);
                tris.Add(ring[1]);
                tris.Add(ring[2]);
            }
            return verts;
        }

        static bool Visible(List<Vector2> verts, int outerCount, int holeCount, int from, int to)
        {
            Vector2 a = verts[from], b = verts[to];
            for (int i = 0; i < outerCount; i++)
                if (SegmentsCross(a, b, verts[i], verts[(i + 1) % outerCount])) return false;
            for (int i = 0; i < holeCount; i++)
                if (SegmentsCross(a, b, verts[outerCount + i], verts[outerCount + (i + 1) % holeCount])) return false;
            return true;
        }

        static bool SegmentsCross(Vector2 p1, Vector2 p2, Vector2 q1, Vector2 q2)
        {
            if (p1 == q1 || p1 == q2 || p2 == q1 || p2 == q2) return false;
            float d1 = Cross(p2 - p1, q1 - p1), d2 = Cross(p2 - p1, q2 - p1);
            float d3 = Cross(q2 - q1, p1 - q1), d4 = Cross(q2 - q1, p2 - q1);
            return d1 * d2 < 0f && d3 * d4 < 0f;
        }

        static float Cross(List<int> ring, List<Vector2> verts, int i)
        {
            int n = ring.Count;
            Vector2 a = verts[ring[(i + n - 1) % n]], b = verts[ring[i]], c = verts[ring[(i + 1) % n]];
            return Cross(b - a, c - b);
        }

        static float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;

        static bool InTriangle(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(b - a, p - a), d2 = Cross(c - b, p - b), d3 = Cross(a - c, p - c);
            return d1 >= -1e-6f && d2 >= -1e-6f && d3 >= -1e-6f;
        }

        static List<Vector2> RemoveDuplicatesOpen(List<Vector2> p)
        {
            var result = new List<Vector2>();
            foreach (var v in p)
                if (result.Count == 0 || (result[result.Count - 1] - v).sqrMagnitude > 1e-6f) result.Add(v);
            return result;
        }

        static List<Vector2> RemoveDuplicates(List<Vector2> p)
        {
            var result = new List<Vector2>();
            foreach (var v in p)
                if (result.Count == 0 || (result[result.Count - 1] - v).sqrMagnitude > 1e-6f) result.Add(v);
            if (result.Count > 1 && (result[0] - result[result.Count - 1]).sqrMagnitude < 1e-6f) result.RemoveAt(result.Count - 1);
            return result;
        }
    }
}
