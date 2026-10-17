using System.Collections.Generic;
using UnityEngine;

namespace OutOfOffice
{
    // Signs that a booster is on (OUT-51): a coffee cup bobbing over everything a Coffee Break has stopped, and a gold
    // ring round the bed for a Magnet Bed, with sparks drawn into it. Cosmetic only; it lives under the level root.
    public class BoosterFx : MonoBehaviour
    {
        readonly List<SpriteRenderer> cups = new List<SpriteRenderer>();
        readonly List<SpriteRenderer> sparks = new List<SpriteRenderer>();
        SpriteRenderer ring;
        float radius, born, cupScale;

        // A cup over each moving desk, turnstile, pop wall, timed staple strip, belt, vent and spin platform.
        public static BoosterFx BreakSigns(LevelInstance level)
        {
            var fx = Make("Coffee Break Signs", level.Root.transform);
            var spots = new List<Vector3>();
            foreach (var m in level.Movers) spots.Add(m.transform.position);
            foreach (var w in level.PopWalls) if (w.Def.BlinkPeriod > 0f) spots.Add(w.Def.Center);
            foreach (var z in level.Zones)
                if (z.Kind == ZoneKind.Conveyor || z.Kind == ZoneKind.Wind || z.Kind == ZoneKind.Vortex || z.Kind == ZoneKind.Spikes && z.Def.Period > 0f)
                    spots.Add(z.Def.Center);
            var cup = UiArt.Icon3D("coffee");
            foreach (var p in spots)
            {
                var sr = Sprite(fx.transform, "Cup", cup, new Vector3(p.x, p.y + 0.2f, -1.1f), 0.75f, 30);
                fx.cupScale = sr.transform.localScale.x;
                fx.cups.Add(sr);
            }
            return fx;
        }

        // The bed's pull, shown: a gold ring at its reach and sparks streaming in.
        public static BoosterFx BedRing(LevelInstance level, Vector2 bed, float reach)
        {
            var fx = Make("Magnet Bed Ring", level.Root.transform);
            fx.transform.position = new Vector3(bed.x, bed.y, 0f);
            fx.radius = reach;
            fx.ring = Sprite(fx.transform, "Ring", Shapes.Ring, new Vector3(0f, 0f, -0.02f), reach * 2f, 6);
            Sprite(fx.transform, "Glow", Textures.Glow, new Vector3(0f, 0f, -0.01f), reach * 2.2f, 5).color = new Color(1f, 0.82f, 0.3f, 0.18f);
            for (int i = 0; i < 14; i++) fx.sparks.Add(Sprite(fx.transform, "Spark", Textures.Glow, Vector3.zero, 0.2f, 7));
            return fx;
        }

        static BoosterFx Make(string name, Transform parent)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var fx = go.AddComponent<BoosterFx>();
            fx.born = Time.time;
            return fx;
        }

        static SpriteRenderer Sprite(Transform parent, string name, Sprite sprite, Vector3 localPos, float size, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            float s = sprite != null ? size / Mathf.Max(sprite.bounds.size.x, 0.01f) : size;
            go.transform.localScale = new Vector3(s, s, 1f);
            return sr;
        }

        void Update()
        {
            float t = Time.time - born;
            float pop = Mathf.Clamp01(t / 0.35f);
            float popScale = 1f + 0.25f * Mathf.Sin(pop * Mathf.PI);  // a little overshoot as they appear
            for (int i = 0; i < cups.Count; i++)
            {
                var c = cups[i];
                var p = c.transform.localPosition;
                c.transform.localPosition = new Vector3(p.x, p.y, -1.1f - 0.08f * Mathf.Sin(t * 2.4f + i));
                c.color = new Color(1f, 1f, 1f, pop * 0.95f);
                c.transform.localScale = Vector3.one * (cupScale * Mathf.Lerp(0.4f, 1f, pop) * popScale);
                c.transform.localRotation = Quaternion.Euler(0f, 0f, 8f * Mathf.Sin(t * 1.7f + i * 1.3f));
            }
            if (ring != null)
            {
                float k = Mathf.Repeat(t * 0.8f, 1f);
                ring.color = new Color(1f, 0.84f, 0.32f, (0.35f + 0.3f * Mathf.Sin(t * 5f)) * pop);
                for (int i = 0; i < sparks.Count; i++)
                {
                    float s = Mathf.Repeat(t * 0.6f + i * 0.618f, 1f);
                    float r = Mathf.Lerp(radius, 0.3f, s * s);
                    float a = i * 2.39996f + s * 1.2f;
                    sparks[i].transform.localPosition = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, -0.03f);
                    sparks[i].color = new Color(1f, 0.9f, 0.5f, 0.9f * Mathf.Sin(s * Mathf.PI) * pop);
                }
                ring.transform.localScale = Vector3.one * (radius * 2f / Mathf.Max(0.01f, ring.sprite.bounds.size.x)) * (1f - 0.04f * k);
            }
        }
    }
}
