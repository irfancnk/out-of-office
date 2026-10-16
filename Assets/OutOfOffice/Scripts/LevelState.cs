using System.Collections.Generic;
using UnityEngine;

namespace OutOfOffice
{
    public class StarPickup
    {
        public Vector2 Position;
        public bool Collected;
        public GameObject Visual;
    }

    public class GateInstance
    {
        public BlockDef Def;
        public Collider2D Collider;
        public Transform Visual;
        public bool Open;
    }

    public class CrateInstance
    {
        public Rigidbody2D Body;
        public Vector2 StartPosition;
        public float StartRotation;
    }

    // A wall that rises and sinks on the level clock. Down, its collider is off and it can be crossed.
    public class PopWallInstance
    {
        public BlockDef Def;
        public Collider2D Collider;
        public Transform Visual;

        public bool Up(float time) => Def.BlinkPeriod <= 0f || Mathf.Repeat(time / Def.BlinkPeriod + Def.BlinkPhase, 1f) < 0.5f;

        // 1 up, 0 down, with a short slide between, for the visual.
        public float Height(float time)
        {
            if (Def.BlinkPeriod <= 0f) return 1f;
            float k = Mathf.Repeat(time / Def.BlinkPeriod + Def.BlinkPhase, 1f) * Def.BlinkPeriod, half = Def.BlinkPeriod * 0.5f;
            const float slide = 0.15f;
            return k < half ? Mathf.Clamp01(k / slide) * Mathf.Clamp01((half - k) / slide + 1f) : 0f;
        }

        // Seconds until it rises next (0 while up).
        public float UntilRise(float time) =>
            Def.BlinkPeriod <= 0f || Up(time) ? 0f : (1f - Mathf.Repeat(time / Def.BlinkPeriod + Def.BlinkPhase, 1f)) * Def.BlinkPeriod;
    }

    public class BreakableInstance
    {
        public Collider2D Collider;
        public GameObject Visual;
        public bool Broken;
    }

    // Everything a shot can change. Undo restores one of these; restart rebuilds the level.
    public class LevelSnapshot
    {
        public Vector2 Puck;
        public int Shots;
        public float Time;
        public bool[] Switches, Stars, Broken;
        public Vector2[] CratePositions;
        public float[] CrateRotations;
    }

    public class LevelInstance
    {
        public LevelDef Def;
        public GameObject Root;
        public Puck Puck;
        public Rect Bounds;  // outer edge of the rim
        public readonly List<Mover> Movers = new List<Mover>();
        public readonly List<ZoneInstance> Zones = new List<ZoneInstance>();
        public readonly List<Collider2D> BlockColliders = new List<Collider2D>();  // everything a jump flies over
        public readonly List<StarPickup> Stars = new List<StarPickup>();
        public readonly List<GateInstance> Gates = new List<GateInstance>();
        public readonly List<CrateInstance> Crates = new List<CrateInstance>();
        public readonly List<BreakableInstance> Breakables = new List<BreakableInstance>();
        public readonly List<PopWallInstance> PopWalls = new List<PopWallInstance>();

        // Shatters a crystal block once. Returns false if the collider is not a breakable or it already broke.
        public bool Break(Collider2D collider)
        {
            foreach (var b in Breakables)
            {
                if (b.Collider != collider || b.Broken) continue;
                b.Broken = true;
                b.Collider.enabled = false;
                if (b.Visual != null) b.Visual.SetActive(false);
                return true;
            }
            return false;
        }

        public System.Action<GateInstance> GateMoved;  // cosmetic hook for sound and animation

        public float Time;  // level clock of the last tick: drives movers and pop-up spikes

        public void TickMovers(float time, bool snap) => TickMovers(time, snap, time);

        // moverTime: the clock for moving desks and turnstiles. A Coffee Break (OUT-13) holds it still while pop walls and
        // pop-up spikes keep their beat on `time`.
        public void TickMovers(float time, bool snap, float moverTime)
        {
            Time = time;
            foreach (var w in PopWalls) w.Collider.enabled = w.Up(time);
            foreach (var m in Movers) m.Tick(moverTime, snap);
        }

        // Presses a switch once and opens its gates. Returns false if it was already pressed.
        public bool PressSwitch(ZoneInstance zone)
        {
            if (zone.Pressed) return false;
            zone.Pressed = true;
            foreach (var g in Gates)
                if (g.Def.Group == zone.Def.Group) SetGate(g, true);
            return true;
        }

        void SetGate(GateInstance g, bool open)
        {
            if (g.Open == open) return;
            g.Open = open;
            g.Collider.enabled = !open;
            GateMoved?.Invoke(g);
        }

        public int StarsCollected
        {
            get
            {
                int n = 0;
                foreach (var s in Stars) if (s.Collected) n++;
                return n;
            }
        }

        public LevelSnapshot Capture(Vector2 puck, int shots, float time)
        {
            var snap = new LevelSnapshot
            {
                Puck = puck,
                Shots = shots,
                Time = time,
                Switches = new bool[Zones.Count],
                Stars = new bool[Stars.Count],
                CratePositions = new Vector2[Crates.Count],
                CrateRotations = new float[Crates.Count],
                Broken = new bool[Breakables.Count],
            };
            for (int i = 0; i < Breakables.Count; i++) snap.Broken[i] = Breakables[i].Broken;
            for (int i = 0; i < Zones.Count; i++) snap.Switches[i] = Zones[i].Pressed;
            for (int i = 0; i < Stars.Count; i++) snap.Stars[i] = Stars[i].Collected;
            for (int i = 0; i < Crates.Count; i++)
            {
                snap.CratePositions[i] = Crates[i].Body.position;
                snap.CrateRotations[i] = Crates[i].Body.rotation;
            }
            return snap;
        }

        // Restores switches, gates, stars, crates, and moving parts. The caller places the puck.
        public void Restore(LevelSnapshot snap)
        {
            for (int i = 0; i < Zones.Count; i++) Zones[i].Pressed = snap.Switches[i];
            foreach (var g in Gates)
            {
                bool open = false;
                foreach (var z in Zones)
                    if (z.Kind == ZoneKind.Switch && z.Pressed && z.Def.Group == g.Def.Group) open = true;
                SetGate(g, open);
            }
            for (int i = 0; i < Stars.Count; i++)
            {
                Stars[i].Collected = snap.Stars[i];
                if (Stars[i].Visual == null) continue;
                Stars[i].Visual.transform.localScale = Vector3.one;  // a pickup animation may have been cut short
                var bob = Stars[i].Visual.transform.Find("Bob");
                if (bob != null)
                {
                    bob.localScale = Vector3.one;
                    bob.localRotation = Quaternion.identity;
                    if (bob.TryGetComponent(out Wiggle w)) w.enabled = true;
                }
                Stars[i].Visual.SetActive(!snap.Stars[i]);
            }
            for (int i = 0; i < Breakables.Count; i++)
            {
                var br = Breakables[i];
                br.Broken = snap.Broken[i];
                br.Collider.enabled = !br.Broken;
                if (br.Visual != null) br.Visual.SetActive(!br.Broken);
            }
            for (int i = 0; i < Crates.Count; i++)
            {
                var b = Crates[i].Body;
                b.position = snap.CratePositions[i];
                b.rotation = snap.CrateRotations[i];
                b.linearVelocity = Vector2.zero;
                b.angularVelocity = 0f;
                b.transform.SetPositionAndRotation(snap.CratePositions[i], Quaternion.Euler(0f, 0f, snap.CrateRotations[i]));
            }
            TickMovers(snap.Time, true);
            Physics2D.SyncTransforms();
        }
    }
}
