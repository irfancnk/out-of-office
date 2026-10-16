using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace OutOfOffice
{
    // The reward opening: an "Employee Appreciation" gift box on a 3D stage far from the board, with its own camera
    // and overlay. The box is a real object (walls with thickness, a lined interior, a lid hinged at its back edge).
    // It settles, waits for a tap, gives one small wiggle of anticipation, the latch pops, the lid swings open far
    // enough to show inside, and the employee rises out of the box wearing the actual prize, then pauses for the
    // label and buttons. The reward is granted before the scene starts, so skipping or closing never loses it.
    public class ChestStage : MonoBehaviour
    {
        static readonly Vector3 Origin = new Vector3(0f, 600f, 0f);
        const float RefW = 1080f, RefH = 1920f;

        CosmeticItem item;
        bool duplicate;
        SkinDef look;
        string heading;
        ChestTier tier;
        string note;
        Action<bool> done;
        Action<AudioClip, float> play;

        Camera stageCam, mainCam;
        Transform chest, lid, rays;
        SpriteRenderer glow;
        Puck reveal;
        readonly List<(Transform t, Vector3 v, float life)> sparks = new List<(Transform, Vector3, float)>();
        Canvas canvas;
        readonly List<Canvas> hidden = new List<Canvas>();
        RectTransform ui, prompt, result;
        bool tapped, finished, reduced;
        Transform latch;
        TMP_FontAsset font;
        Material textMat;

        // A stage is up: it owns the screen, so the back button waits for it (OUT-18).
        public static bool Showing => live > 0;
        static int live;

        // tier: which box (OUT-14). note: the line under the item, such as what a duplicate paid in clips.
        public static void Open(CosmeticItem item, bool duplicate, SkinDef look, string heading, Action<AudioClip, float> play, Action<bool> done, bool reducedMotion = false,
            ChestTier tier = ChestTier.DeskDrawer, string note = null)
        {
            live++;
            var go = new GameObject("Gift Stage");
            var stage = go.AddComponent<ChestStage>();
            stage.reduced = reducedMotion;
            stage.item = item;
            stage.duplicate = duplicate;
            stage.look = look;
            stage.heading = heading;
            stage.tier = tier;
            stage.note = note;
            stage.play = play;
            stage.done = done;
        }

        IEnumerator Start()
        {
            transform.position = Origin;
            mainCam = Camera.main;
            if (mainCam != null) mainCam.enabled = false;
            // The stage owns the screen: hide the game's menus until it closes.
            foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.enabled && c.isRootCanvas) { c.enabled = false; hidden.Add(c); }
            Color rarity = Cosmetics.RarityColor(item.Rarity);
            BuildStage(rarity);
            BuildUi(rarity);

            // The box settles onto the stage.
            float settle = reduced ? 0.2f : 0.5f;
            for (float t = 0f; t < settle; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / settle);
                chest.localPosition = new Vector3(0f, 0f, -1.5f * (1f - (reduced ? k : OutBounce(k))) * (reduced ? 0f : 1f));
                Spin(Time.unscaledDeltaTime);
                yield return null;
            }
            chest.localPosition = Vector3.zero;
            Play(Sfx.Pillow, 0.7f);
            Haptics.Play(Haptics.Style.Soft, 0.6f);

            // Wait for a tap (or open by itself after a moment).
            prompt.gameObject.SetActive(true);
            float wait = 0f;
            while (!tapped && wait < 4f)
            {
                wait += Time.unscaledDeltaTime;
                if (!reduced) prompt.localScale = Vector3.one * (1f + 0.05f * Mathf.Sin(wait * 6f));
                Spin(Time.unscaledDeltaTime);
                yield return null;
            }
            prompt.gameObject.SetActive(false);

            // A brief anticipation wiggle, then the latch pops.
            if (!reduced)
            {
                Play(Sfx.Paper, 0.5f);
                for (float t = 0f; t < 0.35f; t += Time.unscaledDeltaTime)
                {
                    float k = t / 0.35f;
                    chest.localRotation = Quaternion.Euler(0f, 0f, 4f * Mathf.Sin(k * Mathf.PI * 4f) * (1f - k));
                    Spin(Time.unscaledDeltaTime);
                    yield return null;
                }
                chest.localRotation = Quaternion.identity;
            }
            Play(Sfx.Chirp, 0.5f);
            Haptics.Play(Haptics.Style.Rigid, 0.6f);
            for (float t = 0f; t < 0.12f; t += Time.unscaledDeltaTime)
            {
                latch.localRotation = Quaternion.Euler(-80f * (t / 0.12f), 0f, 0f);
                yield return null;
            }
            latch.localRotation = Quaternion.Euler(-80f, 0f, 0f);

            // The lid swings open on its hinge, far enough to show the lined interior.
            Play(Sfx.Swish, 0.6f);
            glow.gameObject.SetActive(true);
            float open = reduced ? 0.25f : 0.5f;
            for (float t = 0f; t < open; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / open);
                lid.localRotation = Quaternion.Euler(118f * (reduced ? k : OutBack(k)), 0f, 0f);
                glow.color = new Color(rarity.r, rarity.g, rarity.b, 0.45f * k);
                Spin(Time.unscaledDeltaTime);
                yield return null;
            }
            lid.localRotation = Quaternion.Euler(118f, 0f, 0f);

            // The prize rises out of the box: the employee, wearing exactly what was won.
            Play(Sfx.Reward, 0.8f);
            Haptics.Play(Haptics.Style.Success);
            if (!reduced) for (int i = 0; i < 10; i++) SpawnSpark(rarity);
            var m = reveal.GetComponent<Mascot>();
            m.Pose(Eyes.Happy, Mouth.Open);
            reveal.gameObject.SetActive(true);
            float rise = reduced ? 0.3f : 0.7f;
            for (float t = 0f; t < rise; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / rise);
                float e = reduced ? k : 1f - Mathf.Pow(1f - k, 3f);
                reveal.transform.localPosition = new Vector3(0f, 0f, Mathf.Lerp(0.15f, -1.05f, e));
                reveal.ScaleMul = 1f;
                Spin(Time.unscaledDeltaTime);
                yield return null;
            }
            m.Pose(Eyes.Open, Mouth.Grin);

            // Label and buttons.
            result.gameObject.SetActive(true);
            var group = result.GetComponent<CanvasGroup>();
            for (float t = 0f; t < 0.3f; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / 0.3f);
                group.alpha = k;
                result.localScale = Vector3.one * (reduced ? 1f : Mathf.Lerp(0.92f, 1f, k));
                Spin(Time.unscaledDeltaTime);
                yield return null;
            }
            group.alpha = 1f;
            result.localScale = Vector3.one;
            while (!finished)
            {
                Spin(Time.unscaledDeltaTime);
                if (!reduced) reveal.transform.localPosition = new Vector3(0f, 0f, -1.05f - 0.04f * Mathf.Sin(Time.unscaledTime * 2.5f));
                yield return null;
            }
        }

        void Close(bool equip)
        {
            if (finished) return;
            finished = true;
            live = System.Math.Max(0, live - 1);
            if (mainCam != null) mainCam.enabled = true;
            foreach (var c in hidden) if (c != null) c.enabled = true;
            Destroy(canvas.gameObject);
            Destroy(gameObject);
            done?.Invoke(equip);
        }

        void Play(AudioClip clip, float volume) => play?.Invoke(clip, volume);

        IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                Spin(Time.unscaledDeltaTime);
                yield return null;
            }
        }

        // Background rays turn, sparkles fly and fade.
        void Spin(float dt)
        {
            if (rays != null) rays.localRotation = Quaternion.Euler(0f, 0f, Time.unscaledTime * 25f);
            for (int i = sparks.Count - 1; i >= 0; i--)
            {
                var (t, v, life) = sparks[i];
                life -= dt;
                if (life <= 0f || t == null)
                {
                    if (t != null) Destroy(t.gameObject);
                    sparks.RemoveAt(i);
                    continue;
                }
                v += new Vector3(0f, 0f, 5f) * dt;  // +Z is down
                t.localPosition += v * dt;
                t.localScale = Vector3.one * (0.09f * Mathf.Clamp01(life * 2f));
                sparks[i] = (t, v, life);
            }
        }

        void SpawnSpark(Color rarity)
        {
            var s = LevelBuilder.MakeSprite("Spark", UnityEngine.Random.value < 0.6f ? UiArt.Star : Textures.Glow,
                UnityEngine.Random.value < 0.5f ? Color.white : Color.Lerp(rarity, Color.white, 0.3f), transform, new Vector3(0f, 0f, -0.9f), Vector2.one * 0.22f, 40);
            s.transform.rotation = stageCam.transform.rotation;
            var dir = UnityEngine.Random.insideUnitCircle.normalized;
            var v = new Vector3(dir.x * UnityEngine.Random.Range(1.5f, 4f), dir.y * UnityEngine.Random.Range(0.5f, 2f), -UnityEngine.Random.Range(3f, 6f));
            sparks.Add((s.transform, v, UnityEngine.Random.Range(0.8f, 1.4f)));
        }

        // ---------- Stage ----------

        void BuildStage(Color rarity)
        {
            var camGo = new GameObject("Gift Camera");
            camGo.transform.SetParent(transform, false);
            stageCam = camGo.AddComponent<Camera>();
            stageCam.clearFlags = CameraClearFlags.SolidColor;
            stageCam.backgroundColor = Themes.Hex(0x2B3150);
            stageCam.fieldOfView = 32f;
            stageCam.nearClipPlane = 0.1f;
            stageCam.farClipPlane = 60f;
            stageCam.depth = 20f;
            // Looking down into the box at a steep angle, so the interior is visible once the lid opens.
            camGo.transform.rotation = Quaternion.Euler(-48f, 0f, 0f);
            camGo.transform.position = transform.position + new Vector3(0f, 0.1f, -0.7f) - camGo.transform.forward * 8.6f;

            // A soft spotlight on a quiet backdrop.
            var back = LevelBuilder.MakeSprite("Backdrop", Textures.Glow, new Color(0.55f, 0.6f, 0.9f, 0.35f), transform, Vector3.zero, Vector2.one * 9f, -50);
            back.transform.position = stageCam.transform.position + stageCam.transform.forward * 18f;
            back.transform.rotation = stageCam.transform.rotation;
            rays = back.transform;
            var floor = Look.AddMesh("Stage", Meshes.RoundedPrism(5f, 4f, 2f, 0.02f, 0.01f), Look.Flat(Themes.Hex(0x3A4166), 0.3f), transform, new Vector3(0f, 0f, 0.01f), false);
            floor.receiveShadows = true;
            glow = LevelBuilder.MakeSprite("Glow", Textures.Glow, new Color(rarity.r, rarity.g, rarity.b, 0f), transform, new Vector3(0f, 0.1f, -0.9f), Vector2.one * 3.2f, -10);
            glow.transform.rotation = stageCam.transform.rotation;
            glow.gameObject.SetActive(false);

            // The box: walls with real thickness around a cream-lined interior, a gold ribbon, and a latch.
            chest = new GameObject("Gift Box").transform;
            chest.SetParent(transform, false);
            // Each tier its own box (OUT-14): a wooden desk drawer with brass, a steel-blue supply closet with silver, a black
            // executive safe with gold.
            var navy = Look.Lit(Themes.Hex(tier == ChestTier.DeskDrawer ? 0x9A6238 : tier == ChestTier.SupplyCloset ? 0x2F64B0 : 0x24222E), 0.55f);
            var lining = Look.Flat(Themes.Hex(0xF3E7D2), 0.2f);
            var gold = Look.Lit(tier == ChestTier.SupplyCloset ? Themes.Hex(0xDDE6F4) : tier == ChestTier.DeskDrawer ? Themes.Hex(0xE2B24C) : Palette.Gold, 0.9f);
            const float W = 1.7f, D = 1.2f, H = 0.8f, T = 0.09f;
            Look.AddMesh("Bottom", Meshes.RoundedPrism(W, D, 0.08f, 0.08f, 0.03f), navy, chest, Vector3.zero);
            Look.AddMesh("Front", Meshes.RoundedPrism(W, T, 0.03f, H, 0.02f), navy, chest, new Vector3(0f, -D / 2f + T / 2f, 0f));
            Look.AddMesh("Back", Meshes.RoundedPrism(W, T, 0.03f, H, 0.02f), navy, chest, new Vector3(0f, D / 2f - T / 2f, 0f));
            Look.AddMesh("Left", Meshes.RoundedPrism(T, D - 2f * T, 0.03f, H, 0.02f), navy, chest, new Vector3(-W / 2f + T / 2f, 0f, 0f));
            Look.AddMesh("Right", Meshes.RoundedPrism(T, D - 2f * T, 0.03f, H, 0.02f), navy, chest, new Vector3(W / 2f - T / 2f, 0f, 0f));
            Look.AddMesh("Lining", Meshes.RoundedPrism(W - 2f * T - 0.01f, D - 2f * T - 0.01f, 0.04f, 0.09f, 0.01f), lining, chest, new Vector3(0f, 0f, -0.0f), false);
            foreach (float y in new[] { -1f, 1f })
                Look.AddMesh("Lining Wall", Meshes.RoundedPrism(W - 2f * T, 0.02f, 0.01f, H - 0.02f, 0.005f), lining, chest, new Vector3(0f, y * (D / 2f - T - 0.01f), 0f), false);
            foreach (float x in new[] { -1f, 1f })
                Look.AddMesh("Lining Wall", Meshes.RoundedPrism(0.02f, D - 2f * T, 0.01f, H - 0.02f, 0.005f), lining, chest, new Vector3(x * (W / 2f - T - 0.01f), 0f, 0f), false);
            // Tissue paper puffs inside.
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI * 2f / 6f;
                var puff = Look.AddMesh("Tissue", Meshes.Fluff(0.22f, 0.12f, 0.05f), Look.Lit(Themes.Hex(0xFFF7EE), 0.2f), chest,
                    new Vector3(Mathf.Cos(a) * 0.45f, Mathf.Sin(a) * 0.28f, -0.5f), false);
                puff.transform.localRotation = Quaternion.Euler(0f, 0f, i * 40f);
            }
            Look.AddMesh("Ribbon Front", Meshes.RoundedPrism(0.18f, 0.02f, 0.01f, H, 0.005f), gold, chest, new Vector3(0f, -D / 2f - 0.005f, 0f), false);
            latch = new GameObject("Latch").transform;
            latch.SetParent(chest, false);
            latch.localPosition = new Vector3(0f, -D / 2f - 0.02f, -H);
            Look.AddMesh("Clasp", Meshes.RoundedPrism(0.2f, 0.05f, 0.02f, 0.16f, 0.01f), gold, latch, new Vector3(0f, 0f, 0.14f), false);

            // The lid, on a hinge along the back top edge; a ribbon cross and a bow on top.
            lid = new GameObject("Lid Hinge").transform;
            lid.SetParent(chest, false);
            lid.localPosition = new Vector3(0f, D / 2f + 0.02f, -H);
            Look.AddMesh("Lid", Meshes.RoundedPrism(W + 0.08f, D + 0.08f, 0.08f, 0.14f, 0.04f), navy, lid, new Vector3(0f, -D / 2f - 0.02f, 0f));
            Look.AddMesh("Lid Ribbon", Meshes.RoundedPrism(0.18f, D + 0.1f, 0.02f, 0.145f, 0.01f), gold, lid, new Vector3(0f, -D / 2f - 0.02f, 0f), false);
            Look.AddMesh("Lid Ribbon", Meshes.RoundedPrism(W + 0.1f, 0.18f, 0.02f, 0.146f, 0.01f), gold, lid, new Vector3(0f, -D / 2f - 0.02f, 0f), false);
            foreach (float x in new[] { -1f, 1f })
            {
                var loop = Look.AddMesh("Bow", Meshes.Lathe(Scenery.Sphere(0.1f), 14), gold, lid, new Vector3(x * 0.12f, -D / 2f - 0.02f, -0.14f), false);
                loop.transform.localScale = new Vector3(1.4f, 0.7f, 0.8f);
            }
            Look.AddMesh("Knot", Meshes.Lathe(Scenery.Sphere(0.06f), 12), gold, lid, new Vector3(0f, -D / 2f - 0.02f, -0.14f), false);

            // The employee waits inside, below the rim, and rises out when the lid opens.
            reveal = LevelBuilder.MakeMascot(transform, transform.position, look);
            reveal.transform.localPosition = new Vector3(0f, 0f, 0.15f);
            reveal.GetComponent<Mascot>().ViewCamera = stageCam;
            reveal.gameObject.SetActive(false);
        }

        // ---------- Overlay ----------

        void BuildUi(Color rarity)
        {
            font = Resources.Load<TMP_FontAsset>("Fonts/TitanOne SDF");
            var preset = Resources.Load<Material>("Fonts/TitanOne Bold");
            textMat = preset != null ? new Material(preset) : null;
            if (textMat != null)
            {
                textMat.SetColor(ShaderUtilities.ID_OutlineColor, Themes.Hex(0x1B0E3D));
                textMat.SetColor(ShaderUtilities.ID_UnderlayColor, Themes.Hex(0x1B0E3D));
            }

            var go = new GameObject("Chest UI", typeof(RectTransform));
            canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(RefW, RefH);
            scaler.matchWidthOrHeight = (float)Screen.width / Screen.height > RefW / RefH ? 1f : 0f;
            go.AddComponent<GraphicRaycaster>();
            ui = go.GetComponent<RectTransform>();

            // Tap anywhere to open.
            var catcher = Rect("Tap", ui, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var img = catcher.gameObject.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f);
            catcher.gameObject.AddComponent<Button>().onClick.AddListener(() => tapped = true);

            Text(Rect("Heading", ui, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -230f), new Vector2(1000f, 140f)), heading, 96f, Themes.Hex(0xFFE45C));
            Text(Rect("Tier", ui, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -330f), new Vector2(1000f, 70f)), Chests.Name(tier), 50f, Color.white);
            prompt = Rect("Prompt", ui, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 330f), new Vector2(900f, 110f));
            Text(prompt, "TAP TO OPEN!", 72f, Color.white);
            prompt.gameObject.SetActive(false);

            result = Rect("Result", ui, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 330f), new Vector2(1000f, 520f));
            result.gameObject.AddComponent<CanvasGroup>().alpha = 0f;
            var ribbon = Rect("Rarity", result, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(380f, 84f));
            var rb = ribbon.gameObject.AddComponent<Image>();
            rb.sprite = UiArt.Pill;
            rb.type = Image.Type.Sliced;
            rb.color = rarity;
            Text(ribbon, Cosmetics.RarityName(item.Rarity), 52f, Color.white);
            Text(Rect("Name", result, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -150f), new Vector2(1000f, 120f)), item.Name, 92f, Color.white);
            string sub = note ?? (duplicate ? Quips.RewardDuplicate : "NEW " + Cosmetics.SlotName(item.Slot) + ". " + Quips.Reward());
            Text(Rect("Sub", result, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -235f), new Vector2(1000f, 70f)), sub, 46f, Themes.Hex(0xFFE45C));
            Button(result, "WEAR IT", Palette.UiGreen, new Vector2(-220f, 80f), () => Close(true));
            Button(result, "CLAIM", Palette.UiBlue, new Vector2(220f, 80f), () => Close(false));
            result.gameObject.SetActive(false);

        }

        void Button(RectTransform parent, string label, Color color, Vector2 pos, Action click)
        {
            var r = Rect(label, parent, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), pos, new Vector2(380f, 140f));
            var img = r.gameObject.AddComponent<Image>();
            img.sprite = UiArt.Button;
            img.type = Image.Type.Sliced;
            img.color = color;
            r.gameObject.AddComponent<Button>().onClick.AddListener(() => click());
            Text(Rect("Label", r, Vector2.zero, Vector2.one, new Vector2(0f, 8f), Vector2.zero), label, 64f, Color.white);
        }

        TextMeshProUGUI Text(RectTransform parent, string text, float size, Color color)
        {
            // Text lives on its own child: an object can hold only one graphic (the parent may be an image).
            var r = Rect("Text", parent, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var t = r.gameObject.AddComponent<TextMeshProUGUI>();
            t.font = font;
            if (textMat != null) t.fontSharedMaterial = textMat;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = TextAlignmentOptions.Center;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            t.raycastTarget = false;
            t.characterSpacing = 0.5f;
            // Long item names shrink to fit; white words fade to warm cream at their foot, like the HUD's (OUT-50).
            t.enableAutoSizing = true;
            t.fontSizeMax = size;
            t.fontSizeMin = size * 0.6f;
            if (color == Color.white)
            {
                t.enableVertexGradient = true;
                var cream = Themes.Hex(0xFFF0C8);
                t.colorGradient = new VertexGradient(Color.white, Color.white, cream, cream);
            }
            return t;
        }

        static RectTransform Rect(string name, RectTransform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var r = go.GetComponent<RectTransform>();
            r.SetParent(parent, false);
            r.anchorMin = anchorMin;
            r.anchorMax = anchorMax;
            r.anchoredPosition = pos;
            r.sizeDelta = size;
            return r;
        }

        static float OutBack(float k)
        {
            const float s = 1.70158f;
            k -= 1f;
            return k * k * ((s + 1f) * k + s) + 1f;
        }

        static float OutBounce(float k)
        {
            if (k < 1f / 2.75f) return 7.5625f * k * k;
            if (k < 2f / 2.75f) { k -= 1.5f / 2.75f; return 7.5625f * k * k + 0.75f; }
            if (k < 2.5f / 2.75f) { k -= 2.25f / 2.75f; return 7.5625f * k * k + 0.9375f; }
            k -= 2.625f / 2.75f;
            return 7.5625f * k * k + 0.984375f;
        }
    }
}
