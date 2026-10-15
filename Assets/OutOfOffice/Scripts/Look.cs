using System.Collections.Generic;
using UnityEngine;

namespace OutOfOffice
{
    // Materials for the 3D pieces. The base material lives in Resources, so builds keep the URP Lit shader.
    public static class Look
    {
        static Material baseLit, vertexColor;
        static readonly Dictionary<(Color, float, Texture), Material> cache = new Dictionary<(Color, float, Texture), Material>();
        static readonly Dictionary<Color, Material> unlitCache = new Dictionary<Color, Material>();

        // Cartoon shading (Resources/OutOfOfficeToon.shader) with an ink outline; falls back to URP Lit if unsupported.
        public static Material Lit(Color color, float smoothness = 0.5f, Texture texture = null)
        {
            var key = (color, smoothness, texture);
            if (cache.TryGetValue(key, out var m) && m != null) return m;
            if (ToonShader != null)
            {
                m = new Material(ToonShader) { name = "OutOfOffice Toon" };
                m.SetColor("_BaseColor", color);
                m.SetFloat("_Smoothness", smoothness);
                if (texture != null) m.SetTexture("_BaseMap", texture);
                return cache[key] = m;
            }
            m = new Material(BaseLit) { name = "OutOfOffice Lit" };
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", 0f);
            if (texture != null) m.SetTexture("_BaseMap", texture);
            return cache[key] = m;
        }

        static Material baseGlow;
        static readonly Dictionary<(Color, float, Texture, float), Material> softCache = new Dictionary<(Color, float, Texture, float), Material>();

        // Lit, plus a little self-glow in its own color, so shadowed sides stay bright and candy-like (the mascot).
        public static Material Soft(Color color, float smoothness, Texture texture, float glow)
        {
            var key = (color, smoothness, texture, glow);
            if (softCache.TryGetValue(key, out var m) && m != null) return m;
            if (ToonShader != null)
            {
                m = new Material(Lit(color, smoothness, texture)) { name = "OutOfOffice Toon Glow" };
                m.SetFloat("_Glow", glow * 0.6f);
                return softCache[key] = m;
            }
            if (baseGlow == null) baseGlow = Resources.Load<Material>("OutOfOfficeLitGlow");
            if (baseGlow == null) return Lit(color, smoothness, texture);
            m = new Material(baseGlow) { name = "OutOfOffice Soft" };
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", 0f);
            m.SetColor("_EmissionColor", color * glow);
            if (texture != null) m.SetTexture("_BaseMap", texture);
            return softCache[key] = m;
        }

        static readonly Dictionary<(Color, float, Texture), Material> flatCache = new Dictionary<(Color, float, Texture), Material>();

        // Toon shading without the ink outline: floors, hole insides, and anything seen from inside.
        public static Material Flat(Color color, float smoothness = 0.5f, Texture texture = null)
        {
            var key = (color, smoothness, texture);
            if (flatCache.TryGetValue(key, out var m) && m != null) return m;
            m = Lit(color, smoothness, texture);
            if (ToonShader != null)
            {
                m = new Material(m) { name = "OutOfOffice Toon Flat" };
                m.SetFloat("_Outline", 0f);
            }
            return flatCache[key] = m;
        }

        static readonly Dictionary<(Color, float), Material> characterCache = new Dictionary<(Color, float), Material>();

        // Puffy's body: glossy candy toon with a thicker outline and a lighter muzzle where the face sits.
        public static Material Character(Color color, float patch = 0.85f)
        {
            var key = (color, patch);
            if (characterCache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Lit(color, 0.7f)) { name = "OutOfOffice Character" };
            if (ToonShader != null)
            {
                m.SetFloat("_Outline", 0.032f);
                m.SetColor("_PatchColor", Color.Lerp(color, Color.white, 0.42f));
                m.SetFloat("_Patch", patch);
                m.SetFloat("_Glow", 0.08f);
                m.SetColor("_ShadeColor", new Color(0.92f, 0.8f, 0.8f));
                m.SetFloat("_ShadeSoftness", 0.45f);
                m.SetFloat("_ShadowWeight", 0.25f);
            }
            return characterCache[key] = m;
        }

        static readonly Dictionary<(Color, float, float), Material> personCache = new Dictionary<(Color, float, float), Material>();

        // People (the employee and coworkers): matte, soft-edged shading and a thin outline in a darker shade of the
        // part's own color, so small characters read cleanly without a heavy black halo.
        public static Material Person(Color color, float smoothness = 0.2f, float outline = 0.011f)
        {
            var key = (color, smoothness, outline);
            if (personCache.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Lit(color, smoothness)) { name = "OutOfOffice Person" };
            if (ToonShader != null)
            {
                m.SetFloat("_Outline", outline);
                m.SetFloat("_InkMix", 0.7f);
                // The sun comes from behind the board; faces look up at the camera and need a front key light.
                m.SetFloat("_ViewLight", 1.1f);
                m.SetFloat("_Glow", 0.12f);
                m.SetColor("_ShadeColor", new Color(0.84f, 0.8f, 0.88f));
                m.SetFloat("_ShadeSoftness", 0.4f);
                m.SetFloat("_ShadowWeight", 0.3f);
            }
            return personCache[key] = m;
        }

        // Painted toy plastic: soft color with a glossy highlight.
        public static Material Toy(Color color) => Lit(color, 0.72f);

        // Unlit, alpha-blended, multiplied by vertex color. Used for soft shadows and glows on the board.
        public static Material Unlit(Color color)
        {
            if (unlitCache.TryGetValue(color, out var m) && m != null) return m;
            if (vertexColor == null)
            {
                // The default sprite material is unlit, alpha blended, and uses vertex colors.
                var probe = new GameObject("probe");
                vertexColor = probe.AddComponent<SpriteRenderer>().sharedMaterial;
                if (Application.isPlaying) Object.Destroy(probe);
                else Object.DestroyImmediate(probe);
            }
            m = new Material(vertexColor) { name = "OutOfOffice Unlit", color = color };
            return unlitCache[color] = m;
        }

        static Shader toonShader;
        static bool toonChecked;

        public static Shader ToonShader
        {
            get
            {
                if (!toonChecked)
                {
                    toonChecked = true;
                    toonShader = Resources.Load<Shader>("OutOfOfficeToon");
                    if (toonShader != null && !toonShader.isSupported) toonShader = null;
                }
                return toonShader;
            }
        }

        // The cartoon outline pushes each vertex out along a smoothed normal (stored in UV channel 3), so meshes
        // with hard edges still get one closed outline. Computed once per mesh.
        public static void EnsureSmoothNormals(Mesh mesh)
        {
            if (mesh == null || mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord3) || !mesh.isReadable) return;
            var verts = mesh.vertices;
            var normals = mesh.normals;
            if (normals == null || normals.Length != verts.Length) return;
            var sums = new Dictionary<Vector3Int, Vector3>();
            Vector3Int Key(Vector3 v) => new Vector3Int(Mathf.RoundToInt(v.x * 2000f), Mathf.RoundToInt(v.y * 2000f), Mathf.RoundToInt(v.z * 2000f));
            for (int i = 0; i < verts.Length; i++)
            {
                var k = Key(verts[i]);
                sums[k] = (sums.TryGetValue(k, out var acc) ? acc : Vector3.zero) + normals[i];
            }
            var smooth = new List<Vector3>(verts.Length);
            for (int i = 0; i < verts.Length; i++)
            {
                var n = sums[Key(verts[i])];
                smooth.Add(n.sqrMagnitude > 1e-8f ? n.normalized : normals[i]);
            }
            mesh.SetUVs(3, smooth);
        }

        // Merges plain decorative pieces that share a material into one mesh per parent, so a level draws a few
        // hundred fewer objects. Only leaves with nothing but a MeshFilter and MeshRenderer are merged, and only
        // under parents without scripts (scripts may hold references to their children). The merged mesh stays a
        // child of the same parent, so a parent's animation (wobble, spin, pop-up) still moves every piece.
        public static int CombineStatic(Transform root, Transform skip = null)
        {
            int removed = 0;
            var stack = new Stack<Transform>();
            stack.Push(root);
            var groups = new Dictionary<(Material, UnityEngine.Rendering.ShadowCastingMode), List<MeshFilter>>();
            while (stack.Count > 0)
            {
                var parent = stack.Pop();
                if (parent == null || parent == skip) continue;
                if (parent.GetComponent<MonoBehaviour>() != null)
                {
                    for (int i = 0; i < parent.childCount; i++) stack.Push(parent.GetChild(i));
                    continue;
                }
                groups.Clear();
                for (int i = 0; i < parent.childCount; i++)
                {
                    var c = parent.GetChild(i);
                    if (c.childCount > 0 || !c.gameObject.activeSelf || c.GetComponents<Component>().Length != 3) continue;
                    if (!c.TryGetComponent(out MeshFilter mf) || !c.TryGetComponent(out MeshRenderer mr) || mf.sharedMesh == null || mr.sharedMaterial == null) continue;
                    var key = (mr.sharedMaterial, mr.shadowCastingMode);
                    if (!groups.TryGetValue(key, out var list)) groups[key] = list = new List<MeshFilter>();
                    list.Add(mf);
                }
                foreach (var kv in groups)
                {
                    var list = kv.Value;
                    if (list.Count < 3) continue;
                    var parts = new CombineInstance[list.Count];
                    int verts = 0;
                    var toParent = parent.worldToLocalMatrix;
                    for (int i = 0; i < list.Count; i++)
                    {
                        parts[i] = new CombineInstance { mesh = list[i].sharedMesh, transform = toParent * list[i].transform.localToWorldMatrix };
                        verts += list[i].sharedMesh.vertexCount;
                    }
                    var mesh = new Mesh { name = "Combined " + parent.name };
                    if (verts > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                    mesh.CombineMeshes(parts, true, true);
                    var go = new GameObject("Combined");
                    go.transform.SetParent(parent, false);
                    go.AddComponent<MeshFilter>().sharedMesh = mesh;
                    var mr = go.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = kv.Key.Item1;
                    mr.shadowCastingMode = kv.Key.Item2;
                    mr.receiveShadows = true;
                    // Level-owned meshes would be orphaned with their objects: free them too.
                    var owned = new HashSet<Mesh>();
                    foreach (var mf in list)
                        if (mf.sharedMesh != null && !Meshes.IsShared(mf.sharedMesh)) owned.Add(mf.sharedMesh);
                    foreach (var mf in list)
                    {
                        mf.gameObject.SetActive(false);
                        if (Application.isPlaying) Object.Destroy(mf.gameObject);
                        else Object.DestroyImmediate(mf.gameObject);
                    }
                    foreach (var m in owned)
                    {
                        if (Application.isPlaying) Object.Destroy(m);
                        else Object.DestroyImmediate(m);
                    }
                    removed += list.Count - 1;
                }
                // Visit what is left (merged pieces are already gone).
                for (int i = 0; i < parent.childCount; i++)
                {
                    var c = parent.GetChild(i);
                    if (c.gameObject.activeSelf || c.childCount > 0) stack.Push(c);
                }
            }
            return removed;
        }

        static Material BaseLit
        {
            get
            {
                if (baseLit == null) baseLit = Resources.Load<Material>("OutOfOfficeLit");
                if (baseLit == null) baseLit = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                return baseLit;
            }
        }

        public static MeshRenderer AddMesh(string name, Mesh mesh, Material material, Transform parent, Vector3 localPos,
            bool castShadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            if (material != null && ToonShader != null && material.shader == ToonShader) EnsureSmoothNormals(mesh);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = true;
            return mr;
        }

        // Soft dark contact shadow lying on the board.
        public static MeshRenderer AddShadow(string name, Mesh mesh, Transform parent, Vector3 localPos)
        {
            var c = Themes.Current.Shadow;
            var mr = AddMesh(name, mesh, Unlit(new Color(c.r, c.g, c.b, 1f)), parent, localPos, false);
            mr.receiveShadows = false;
            return mr;
        }

        // A vertical gradient, used for the background.
        public static Sprite Gradient(Color top, Color bottom)
        {
            const int h = 128;
            var tex = new Texture2D(2, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var px = new Color[2 * h];
            for (int y = 0; y < h; y++)
            {
                var c = Color.Lerp(bottom, top, y / (h - 1f));
                px[y * 2] = c;
                px[y * 2 + 1] = c;
            }
            tex.SetPixels(px);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, 2, h), new Vector2(0.5f, 0.5f), h);
        }
    }
}
