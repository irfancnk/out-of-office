using System.Collections.Generic;
using UnityEngine;

namespace OutOfOffice
{
    // Idle animation for zones: scrolling boost chevrons, mud bubbles, spinning portals.
    public class ZoneVisual : MonoBehaviour
    {
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static MaterialPropertyBlock block;

        public ZoneKind Kind;
        public ZoneInstance Zone;
        public readonly List<SpriteRenderer> Sparkles = new List<SpriteRenderer>();
        float press;
        public readonly List<Renderer> Parts = new List<Renderer>();
        public Transform Spinner;
        public float Length;  // conveyors: travel length of the chevrons
        Color partColor;
        float pulse;

        public void Init(ZoneKind kind, Color color)
        {
            Kind = kind;
            partColor = color;
        }

        public void Pulse() => pulse = 1f;

        readonly Dictionary<Transform, Vector3> baseScales = new Dictionary<Transform, Vector3>();

        float runClock;  // stops while belts, vents and spin platforms are off for a Coffee Break (OUT-51)

        void Update()
        {
            bool resting = Zone != null && Zone.Level != null && Zone.Level.OnBreak;
            if (!resting) runClock += Time.deltaTime;
            float t = Kind == ZoneKind.Conveyor || Kind == ZoneKind.Wind || Kind == ZoneKind.Vortex ? runClock : Time.time;
            pulse = Mathf.MoveTowards(pulse, 0f, Time.deltaTime * 3f);
            switch (Kind)
            {
                case ZoneKind.Jump:
                {
                    float r = Zone != null ? Zone.Def.Size.x * 0.5f : 0.8f;
                    for (int i = 0; i < Sparkles.Count; i++)
                    {
                        float k = Mathf.Repeat(t * 0.9f + i / (float)Sparkles.Count, 1f);
                        Sparkles[i].transform.localScale = Vector3.one * (r * 1.7f * (0.2f + 0.8f * k));
                        Sparkles[i].color = new Color(1f, 1f, 1f, 0.45f * (1f - k));
                    }
                    if (Parts.Count > 0) Parts[0].transform.localPosition = new Vector3(0f, 0f, -0.035f + 0.08f * pulse);
                    break;
                }
                case ZoneKind.Boost:
                case ZoneKind.Ramp:
                case ZoneKind.OneWay:
                    // A light wave runs along the chevrons in the push direction.
                    for (int i = 0; i < Sparkles.Count; i++)
                    {
                        float wave = Mathf.Repeat(t * 1.8f - i / (float)Sparkles.Count, 1f);
                        float k = Mathf.Max(Mathf.Clamp01(1f - wave * 3f), pulse);
                        var sr = Sparkles[i];
                        sr.color = Color.Lerp(partColor * new Color(1f, 1f, 1f, 0.75f), Color.white, k * 0.85f);
                        if (!baseScales.TryGetValue(sr.transform, out var b)) baseScales[sr.transform] = b = sr.transform.localScale;
                        sr.transform.localScale = new Vector3(b.x * (1f + 0.12f * k), b.y * (1f + 0.12f * k), 1f);
                    }
                    break;
                case ZoneKind.Conveyor:
                    // Chevrons travel at the belt speed and wrap around, fading at the ends.
                    for (int i = 0; i < Sparkles.Count; i++)
                    {
                        float spacing = Length / Sparkles.Count;
                        float y = Mathf.Repeat(t * Rules.ConveyorSpeed + i * spacing, Length) - Length / 2f;
                        var sr = Sparkles[i];
                        sr.transform.localPosition = new Vector3(0f, y, -0.07f);
                        float edge = Mathf.Clamp01((Length / 2f - Mathf.Abs(y)) / 0.4f);
                        sr.color = new Color(1f, 1f, 1f, 0.85f * edge);
                    }
                    break;
                case ZoneKind.Mud:
                    for (int i = 0; i < Parts.Count; i++)
                    {
                        float k = Mathf.Repeat(t * 0.7f + i * 0.37f, 1f);
                        float s = Mathf.Sin(k * Mathf.PI) * (1f + pulse);
                        Parts[i].transform.localScale = new Vector3(s, s, s);
                    }
                    break;
                case ZoneKind.Switch:
                {
                    // The button sinks and lights up once pressed; undo raises it again.
                    press = Mathf.MoveTowards(press, Zone != null && Zone.Pressed ? 1f : 0f, Time.deltaTime * 6f);
                    if (Sparkles.Count > 0)
                    {
                        float k = Mathf.Repeat(t * 0.8f, 1f);
                        var ring = Sparkles[0];
                        ring.transform.localScale = Vector3.one * (Zone.Def.Size.x * (1f + 0.35f * k));
                        ring.color = new Color(partColor.r, partColor.g, partColor.b, (1f - press) * 0.6f * (1f - k));
                    }
                    if (Parts.Count > 0)
                    {
                        Parts[0].transform.localPosition = new Vector3(0f, 0f, 0.1f * press);
                        block ??= new MaterialPropertyBlock();
                        block.SetColor(BaseColorId, Color.Lerp(partColor, Color.white, 0.35f * press + 0.25f * pulse));
                        Parts[0].SetPropertyBlock(block);
                    }
                    break;
                }
                case ZoneKind.Ice:
                    // Glints flash briefly, one at a time, and grow as they flash.
                    for (int i = 0; i < Sparkles.Count; i++)
                    {
                        float k = Mathf.Max(0f, Mathf.Sin(t * 1.1f + i * 2.3f));
                        k = k * k * k * k;
                        Sparkles[i].color = new Color(1f, 1f, 1f, k);
                        Sparkles[i].transform.localScale = Vector3.one * (0.15f + 0.25f * k);
                    }
                    break;
                case ZoneKind.Magnet:
                {
                    if (Sparkles.Count == 0 || Zone == null) break;
                    float reach = Zone.Def.Size.x * 0.5f;
                    // A ring shrinks inward on a beat, and aroma wisps drift in from the edge, faster as they near the machine.
                    float k = Mathf.Repeat(t * 0.7f, 1f);
                    Sparkles[0].transform.localScale = Vector3.one * (Zone.Def.Size.x * (1f - 0.6f * k * k));
                    Sparkles[0].color = new Color(1f, 0.8f, 0.55f, 0.55f * Mathf.Sin(k * Mathf.PI) + 0.25f * pulse);
                    int n = Sparkles.Count - 1;
                    for (int i = 0; i < n; i++)
                    {
                        float p = Mathf.Repeat(t * 0.55f + i * 0.618f, 1f);
                        float rad = Mathf.Lerp(reach * 0.98f, 0.5f, p * p);
                        float a = i * 2.39996f + p * 0.6f;
                        var sr = Sparkles[i + 1];
                        sr.transform.localPosition = new Vector3(Mathf.Cos(a) * rad, Mathf.Sin(a) * rad, -0.01f);
                        sr.transform.localScale = Vector3.one * (0.14f + 0.12f * (1f - p));
                        sr.color = new Color(1f, 0.9f, 0.75f, 0.85f * Mathf.Sin(p * Mathf.PI));
                    }
                    break;
                }
                case ZoneKind.Wind:
                    // Gusts stream along the push, each at its own pace, fading in and out.
                    for (int i = 0; i < Sparkles.Count; i++)
                    {
                        var sr = Sparkles[i];
                        float speed = 5f + (i * 0.37f % 1f) * 3f;
                        float y = Mathf.Repeat(t * speed + i * 1.7f, Length) - Length / 2f + 0.35f;
                        var p = sr.transform.localPosition;
                        sr.transform.localPosition = new Vector3(p.x, y, p.z);
                        float edge = Mathf.Clamp01((Length / 2f - Mathf.Abs(y - 0.35f)) / 0.6f);
                        sr.color = new Color(0.36f, 0.7f, 1f, 0.85f * edge);
                    }
                    break;
                case ZoneKind.Vortex:
                {
                    // The deck turns at the platform's real rate; the rim glows when the employee lands on it.
                    float turn = Zone != null ? Zone.Def.Turn : 1f;
                    if (Spinner != null) Spinner.localRotation = Quaternion.Euler(0f, 0f, t * Rules.SpinRate * Mathf.Rad2Deg * turn);
                    if (Sparkles.Count > 0) Sparkles[0].color = new Color(partColor.r, partColor.g, partColor.b, 0.12f + 0.08f * Mathf.Sin(t * 4f) + 0.7f * pulse);
                    break;
                }
                case ZoneKind.Spikes:
                {
                    if (Zone == null || Zone.Level == null || Spinner == null) break;
                    float time = Zone.Level.Time;
                    float up = Zone.SpikeHeight(time);
                    float warn = Zone.UntilRise(time);
                    float shake = warn > 0f && warn < Rules.SpikeWarn ? 1f - warn / Rules.SpikeWarn : 0f;
                    // Down: hidden in the plate. Warning: they peek and tremble. Up: fully out.
                    float rise = Mathf.Max(up, shake * 0.25f);
                    float jitter = shake > 0f ? Mathf.Sin(t * 70f) * 0.015f * shake : 0f;
                    Spinner.localPosition = new Vector3(jitter, 0f, 0.26f * (1f - rise));
                    Spinner.gameObject.SetActive(rise > 0.02f);
                    if (Sparkles.Count > 0) Sparkles[0].color = new Color(1f, 0.25f, 0.25f, 0.45f * Mathf.Max(up, shake * (0.5f + 0.5f * Mathf.Sin(t * 30f))));
                    break;
                }
                case ZoneKind.Portal:
                    if (Spinner != null) Spinner.localRotation = Quaternion.Euler(0f, 0f, t * 140f);
                    float scale = 1f + 0.08f * Mathf.Sin(t * 5f) + 0.3f * pulse;
                    if (Spinner != null) Spinner.localScale = new Vector3(scale, scale, 1f);
                    break;
            }
        }
    }
}
