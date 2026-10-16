using System.Collections.Generic;
using UnityEngine;

namespace OutOfOffice
{
    // Wall: wood. Hazard: sends the puck back. Bouncer: rubber bumper. Sticky: soft jelly.
    // Gate: wall that a matching switch retracts. Crate: movable toy crate.
    // Breakable: crystal block that shatters when hit hard enough; the puck carries on through.
    // Kicker: a powered bumper that fires the puck away at speed.
    // PopWall: a wall that rises out of the floor and sinks back on a timer.
    public enum BlockKind { Wall, Hazard, Bouncer, Sticky, Gate, Crate, Breakable, Kicker, PopWall }

    public enum BlockShape { Box, Post }

    // Floor areas that change how the puck moves. Only Magnet has a collider (its center post).
    // Conveyor: a moving belt that carries the puck along its direction.
    // Wind: a fan blows along its direction. Vortex: a whirlpool swirls the puck around and pulls it inward.
    // Spikes: floor spikes that pop up and sink on a timer; rolling over them while up sends the puck back.
    // Jump: a trampoline that bounces the puck into the air. OneWay: a gate it can pass only along its arrow.
    public enum ZoneKind { Boost, Mud, Ramp, Portal, Ice, Magnet, Gap, Switch, Conveyor, Wind, Vortex, Spikes, Jump, OneWay }

    public class BlockDef
    {
        public BlockKind Kind;
        public BlockShape Shape;
        public Vector2 Center;
        public Vector2 Size;        // Post: Size.x is the diameter
        public float Corner = -1f;  // corner radius; -1 picks a default from the size
        public float Angle;         // degrees, counter-clockwise
        public Vector2 MoveOffset;  // block swings between Center - MoveOffset and Center + MoveOffset
        public float Period;        // seconds per full swing; 0 = no swing
        public float Phase;
        public float Spin;          // degrees per second; 0 = no spin
        public int Group = -1;      // gates: which switch opens them
        public float BlinkPeriod, BlinkPhase;  // pop walls: seconds per up/down cycle, offset as a fraction of it
        public int Reception;       // the receptionist's desk (OUT-38): -1 in the top left corner, +1 in the top right; 0 otherwise

        public bool Moves => Period > 0f || Spin != 0f;

        // The visual and the collider use this same radius, so what you see is what you hit.
        public float CornerRadius => Shape == BlockShape.Post ? Size.x * 0.5f
            : Corner >= 0f ? Mathf.Min(Corner, Mathf.Min(Size.x, Size.y) * 0.5f - 0.01f)
            : Mathf.Min(0.35f, Mathf.Min(Size.x, Size.y) * 0.45f);

        public BlockDef(BlockKind kind, Vector2 center, Vector2 size)
        {
            Kind = kind;
            Center = center;
            Size = size;
        }
    }

    // A rod along a smooth path: bent corners and arcs instead of separate blocks touching.
    public class RailDef
    {
        public BlockKind Kind;
        public List<Vector2> Path;
        public float Width = 0.5f;
    }

    public class ZoneDef
    {
        public ZoneKind Kind;
        public Vector2 Center;
        public Vector2 Size;   // width across, length along the direction. Portal, Magnet, Switch: Size.x is the diameter.
        public float Angle;    // degrees; 0 points up (+Y), 90 points left. Portals: the facing used to turn the exit velocity.
        public int Link = -1;  // portals: index of the paired portal in LevelDef.Zones
        public int Group = -1; // switches: which gates they open
        public float Turn = 1f;  // vortex: +1 counter-clockwise, -1 clockwise
        public float Period, Phase;  // spikes: seconds per up/down cycle, and offset as a fraction of it

        public Vector2 Direction => new Vector2(-Mathf.Sin(Angle * Mathf.Deg2Rad), Mathf.Cos(Angle * Mathf.Deg2Rad));
    }

    // The playable floor outline. Outer walls follow it.
    public class ShapeDef
    {
        public readonly List<Vector2> Outline;

        ShapeDef(List<Vector2> outline) => Outline = Geometry.CounterClockwise(outline);

        public static ShapeDef Rect(float w, float h, float corner = 0.7f) =>
            Polygon(corner, -w / 2f, -h / 2f, w / 2f, -h / 2f, w / 2f, h / 2f, -w / 2f, h / 2f);

        // Rectangle with fully round ends.
        public static ShapeDef Stadium(float w, float h) => Rect(w, h, Mathf.Min(w, h) / 2f);

        // Rectangle with its corners cut at 45 degrees (then lightly rounded): a frosty octagon.
        public static ShapeDef Chamfered(float w, float h, float cut, float corner = 0.5f) =>
            Polygon(corner, -w / 2f + cut, -h / 2f, w / 2f - cut, -h / 2f, w / 2f, -h / 2f + cut, w / 2f, h / 2f - cut,
                w / 2f - cut, h / 2f, -w / 2f + cut, h / 2f, -w / 2f, h / 2f - cut, -w / 2f, -h / 2f + cut);

        public static ShapeDef Circle(float radius) => new ShapeDef(Geometry.Circle(Vector2.zero, radius, 96));

        // Two round rooms, one above the other, joined by a straight bridge `waist` wide: a big figure 8.
        public static ShapeDef FigureEight(float radius, float halfSpacing, float waist)
        {
            float a = Mathf.Acos(waist * 0.5f / radius);
            var pts = new List<Vector2>();
            var low = new Vector2(0f, -halfSpacing);
            var high = new Vector2(0f, halfSpacing);
            const int steps = 80;
            for (int i = 0; i <= steps; i++)
            {
                float t = Mathf.Lerp(Mathf.PI - a, 2f * Mathf.PI + a, i / (float)steps);
                pts.Add(low + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * radius);
            }
            for (int i = 0; i <= steps; i++)
            {
                float t = Mathf.Lerp(-a, Mathf.PI + a, i / (float)steps);
                pts.Add(high + new Vector2(Mathf.Cos(t), Mathf.Sin(t)) * radius);
            }
            return new ShapeDef(Geometry.Fillet(Geometry.CounterClockwise(pts), 0.8f, 20f));
        }

        // The same outline moved by an offset (taller boards keep their top at y = 8 and grow downward).
        public ShapeDef Shift(float dx, float dy)
        {
            var pts = new List<Vector2>();
            foreach (var p in Outline) pts.Add(p + new Vector2(dx, dy));
            return new ShapeDef(pts);
        }

        // Corner points as x, y pairs. Every corner gets rounded with the given radius.
        public static ShapeDef Polygon(float corner, params float[] xy)
        {
            var pts = new List<Vector2>();
            for (int i = 0; i + 1 < xy.Length; i += 2) pts.Add(new Vector2(xy[i], xy[i + 1]));
            return new ShapeDef(Geometry.Fillet(Geometry.CounterClockwise(pts), corner));
        }
    }

    public class LevelDef
    {
        public readonly string Id;      // stable save key; never rename once shipped
        public readonly string Name;
        public readonly Vector2 Hole;
        public int Par = 1;             // shots for the mastery star
        public string Hint = "";
        public Vector2 Start = new Vector2(0f, -6f);
        public ShapeDef Shape = ShapeDef.Rect(9f, 16f);
        public readonly List<BlockDef> Blocks = new List<BlockDef>();
        public readonly List<ZoneDef> Zones = new List<ZoneDef>();
        public readonly List<RailDef> Rails = new List<RailDef>();
        public readonly List<Vector2> Stars = new List<Vector2>();
        public readonly List<Rect> Bridges = new List<Rect>();  // plank decks over water: decoration only

        public LevelDef(string id, string name, float holeX, float holeY)
        {
            Id = id;
            Name = name;
            Hole = new Vector2(holeX, holeY);
        }

        public LevelDef WithShape(ShapeDef shape) { Shape = shape; return this; }
        public LevelDef StartAt(float x, float y) { Start = new Vector2(x, y); return this; }

        // A taller board: the 9 x 16 field grows downward by `extra`, so the camera scrolls to follow the shot.
        public LevelDef Taller(float extra, ShapeDef shape = null)
        {
            Shape = (shape ?? ShapeDef.Rect(9f, 16f + extra)).Shift(0f, -extra / 2f);
            return this;
        }
        public LevelDef WithPar(int par) { Par = par; return this; }

        // Shots before the level fails (OUT-27): par plus some slack, unless a level sets its own. LevelCheck proves
        // every limit leaves at least two spare shots over the fewest the level can be finished in.
        public const int ShotSlack = 3;
        int maxShots;
        public int MaxShots => maxShots > 0 ? maxShots : Par + ShotSlack;
        public LevelDef WithMaxShots(int shots) { maxShots = shots; return this; }
        public LevelDef WithHint(string hint) { Hint = hint; return this; }
        public LevelDef Star(float x, float y) { Stars.Add(new Vector2(x, y)); return this; }

        // A plank bridge across the floor, `width` wide and `length` long (along y), with water beside it.
        public LevelDef Bridge(float x, float y, float width, float length)
        {
            Bridges.Add(new Rect(x - width / 2f, y - length / 2f, width, length));
            return this;
        }

        public LevelDef Wall(float x, float y, float w, float h) => Add(BlockKind.Wall, x, y, w, h);
        public LevelDef Hazard(float x, float y, float w, float h) => Add(BlockKind.Hazard, x, y, w, h);
        public LevelDef Bouncer(float x, float y, float w, float h) => Add(BlockKind.Bouncer, x, y, w, h);
        public LevelDef Sticky(float x, float y, float w, float h) => Add(BlockKind.Sticky, x, y, w, h);
        public LevelDef Crate(float x, float y, float size) => Add(BlockKind.Crate, x, y, size, size);
        public LevelDef Breakable(float x, float y, float w, float h) => Add(BlockKind.Breakable, x, y, w, h);

        // A round, spiky hazard ball.
        public LevelDef SpikeBall(float x, float y, float radius) => Post(BlockKind.Hazard, x, y, radius);

        public LevelDef Gate(int group, float x, float y, float w, float h)
        {
            Add(BlockKind.Gate, x, y, w, h);
            LastBlock.Group = group;
            return this;
        }

        // A round post: a true circle collider.
        public LevelDef Post(BlockKind kind, float x, float y, float radius)
        {
            Add(kind, x, y, radius * 2f, radius * 2f);
            LastBlock.Shape = BlockShape.Post;
            return this;
        }

        // Modifiers for the most recently added block.
        public LevelDef Rounded(float radius) { LastBlock.Corner = radius; return this; }
        public LevelDef Rotated(float degrees) { LastBlock.Angle = degrees; return this; }

        public LevelDef Moving(float dx, float dy, float period, float phase = 0f)
        {
            var b = LastBlock;
            b.MoveOffset = new Vector2(dx, dy);
            b.Period = period;
            b.Phase = phase;
            return this;
        }

        public LevelDef Spinning(float degreesPerSecond) { LastBlock.Spin = degreesPerSecond; return this; }

        // A rod through the given x, y points. Corners bend with the given radius.
        public LevelDef Rail(BlockKind kind, float bend, params float[] xy)
        {
            var pts = new List<Vector2>();
            for (int i = 0; i + 1 < xy.Length; i += 2) pts.Add(new Vector2(xy[i], xy[i + 1]));
            Rails.Add(new RailDef { Kind = kind, Path = Geometry.FilletOpen(pts, bend) });
            return this;
        }

        // A curved rod along a circle. Angles in degrees, counter-clockwise from +X.
        public LevelDef Arc(BlockKind kind, float cx, float cy, float radius, float fromDeg, float toDeg)
        {
            Rails.Add(new RailDef { Kind = kind, Path = Geometry.Arc(new Vector2(cx, cy), radius, fromDeg, toDeg) });
            return this;
        }

        public LevelDef Boost(float x, float y, float w, float length, float angle = 0f) => AddZone(ZoneKind.Boost, x, y, w, length, angle);
        public LevelDef Mud(float x, float y, float w, float h) => AddZone(ZoneKind.Mud, x, y, w, h, 0f);
        public LevelDef Conveyor(float x, float y, float w, float length, float angle = 0f) => AddZone(ZoneKind.Conveyor, x, y, w, length, angle);
        public LevelDef Ice(float x, float y, float w, float h) => AddZone(ZoneKind.Ice, x, y, w, h, 0f);
        public LevelDef Gap(float x, float y, float w, float h) => AddZone(ZoneKind.Gap, x, y, w, h, 0f);
        public LevelDef Ramp(float x, float y, float w, float length, float angle = 0f) => AddZone(ZoneKind.Ramp, x, y, w, length, angle);
        public LevelDef Wind(float x, float y, float w, float length, float angle = 0f) => AddZone(ZoneKind.Wind, x, y, w, length, angle);

        // A whirlpool of the given reach. Clockwise or counter-clockwise swirl.
        public LevelDef Vortex(float x, float y, float reach, bool clockwise = false)
        {
            AddZone(ZoneKind.Vortex, x, y, reach * 2f, reach * 2f, 0f);
            Zones[Zones.Count - 1].Turn = clockwise ? -1f : 1f;
            return this;
        }

        // Pop-up floor spikes: up for half of each period, down for the other half.
        public LevelDef Spikes(float x, float y, float w, float h, float period, float phase = 0f)
        {
            AddZone(ZoneKind.Spikes, x, y, w, h, 0f);
            Zones[Zones.Count - 1].Period = period;
            Zones[Zones.Count - 1].Phase = phase;
            return this;
        }

        // A trampoline of the given radius.
        public LevelDef Jump(float x, float y, float radius) => AddZone(ZoneKind.Jump, x, y, radius * 2f, radius * 2f, 0f);

        // A one-way gate across `width`, passable only along `angle` (0 = up).
        public LevelDef OneWay(float x, float y, float width, float angle = 0f) => AddZone(ZoneKind.OneWay, x, y, width, 0.7f, angle);

        // A wall that is up for half of each period and down (passable) for the other half.
        public LevelDef PopWall(float x, float y, float w, float h, float period, float phase = 0f)
        {
            Add(BlockKind.PopWall, x, y, w, h);
            LastBlock.BlinkPeriod = period;
            LastBlock.BlinkPhase = phase;
            return this;
        }

        // A powered bumper that kicks the puck away.
        public LevelDef Kicker(float x, float y, float radius) => Post(BlockKind.Kicker, x, y, radius);

        // A magnet post with a round pull region of the given radius.
        public LevelDef Magnet(float x, float y, float reach) => AddZone(ZoneKind.Magnet, x, y, reach * 2f, reach * 2f, 0f);

        // A floor button that retracts every gate with the same group.
        public LevelDef Switch(int group, float x, float y)
        {
            AddZone(ZoneKind.Switch, x, y, 1.1f, 1.1f, 0f);
            Zones[Zones.Count - 1].Group = group;
            return this;
        }

        // Two linked portals. Entering one exits the other; the velocity turns by the difference of their facings.
        public LevelDef Portal(float ax, float ay, float bx, float by, float angleA = 0f, float angleB = 0f)
        {
            int a = Zones.Count;
            AddZone(ZoneKind.Portal, ax, ay, 1.1f, 1.1f, angleA);
            AddZone(ZoneKind.Portal, bx, by, 1.1f, 1.1f, angleB);
            Zones[a].Link = a + 1;
            Zones[a + 1].Link = a;
            return this;
        }

        // The receptionist's corner (OUT-38): her desk, an ordinary wall flush in a top corner of the board, with her
        // sat behind it where no shot can reach. Decorative: it only takes floor no route needs, which LevelCheck proves.
        // Call it after the board's shape is set.
        public static readonly Vector2 ReceptionSize = new Vector2(1.9f, 1.45f);

        public LevelDef Reception(bool right)
        {
            Rect floor = Geometry.Bounds(Shape.Outline);
            float x = right ? floor.xMax - ReceptionSize.x / 2f : floor.xMin + ReceptionSize.x / 2f;
            return Reception(x, floor.yMax - ReceptionSize.y / 2f, right ? 1 : -1);
        }

        // A reception desk centered anywhere (review shots); side picks which way she faces the room.
        public LevelDef Reception(float x, float y, int side)
        {
            Add(BlockKind.Wall, x, y, ReceptionSize.x, ReceptionSize.y);
            LastBlock.Reception = side < 0 ? -1 : 1;
            LastBlock.Corner = 0.2f;
            return this;
        }

        BlockDef LastBlock => Blocks[Blocks.Count - 1];

        LevelDef Add(BlockKind kind, float x, float y, float w, float h)
        {
            Blocks.Add(new BlockDef(kind, new Vector2(x, y), new Vector2(w, h)));
            return this;
        }

        LevelDef AddZone(ZoneKind kind, float x, float y, float w, float h, float angle)
        {
            Zones.Add(new ZoneDef { Kind = kind, Center = new Vector2(x, y), Size = new Vector2(w, h), Angle = angle });
            return this;
        }
    }

    // The default field is 9 x 16: x in [-4.5, 4.5], y in [-8, 8]. The puck starts at (0, -6) unless a level sets Start.
    // Run Out of Office > Check Levels after an edit: it proves every level can be finished and suggests par.
    public static class Levels
    {
        public static readonly LevelDef[] All =
        {
            // ---------- Chapter 1, Open-Plan Office (levels 1-11): aim, bank off walls, ride the desks ----------
            new LevelDef("first", "Day One, Already Tired", 0f, 4.5f).WithHint("DRAG BACK, AIM, RELEASE")
                .Wall(-2.95f, 0.8f, 3.1f, 0.6f)
                .Wall(2.95f, 0.8f, 3.1f, 0.6f)
                .Post(BlockKind.Bouncer, -2.9f, 4.6f, 0.5f)
                .Post(BlockKind.Bouncer, 2.9f, 4.6f, 0.5f)
                .Post(BlockKind.Bouncer, -3.2f, -3.8f, 0.45f).Post(BlockKind.Bouncer, 3.2f, -3.8f, 0.45f)  // OUT-25: two chairs to bank off from the start
                .Star(0f, -2.4f).Star(0f, 0.8f).Star(-2.5f, 3.1f),  // two on the way, one for a bank toward the chair

            new LevelDef("easy", "Avoid Eye Contact", 0f, 5.3f).WithHint("GO AROUND THE DESK").WithPar(2)
                .Wall(0f, 1.2f, 5.4f, 2.6f).Rounded(0.6f)
                .SpikeBall(3.6f, 1.2f, 0.4f).Moving(0f, 1.7f, 3f)
                .Wall(-2.9f, -3.8f, 2.2f, 0.6f).Post(BlockKind.Bouncer, 3f, -3.4f, 0.45f)  // OUT-25: a side desk and a chair by the start
                .Star(-3.5f, -1.4f).Star(-3.5f, 2.8f).Star(-1.4f, 4.4f).Reception(false),

            new LevelDef("bank", "Bounce Off the Walls", 2.8f, 5.8f).WithHint("BOUNCE OFF THE WALL").WithPar(2)
                .StartAt(-2.6f, -6.2f)
                .Wall(2.1f, -1.6f, 4.8f, 0.6f)
                .Wall(-1.1f, 2.4f, 6.8f, 0.6f)
                .Post(BlockKind.Bouncer, 0.4f, 6.4f, 0.5f)
                .Star(-2.6f, -3.4f).Star(0.9f, 0.4f).Star(3.5f, 3.6f),

            new LevelDef("ring", "Nobody Owns This Desk", 0f, 5.4f).WithHint("GO ROUND THE BIG DESK").WithPar(2)
                .WithShape(ShapeDef.Circle(7.2f))
                .StartAt(0f, -5.4f)
                .Post(BlockKind.Wall, 0f, 0f, 3.3f)
                .Post(BlockKind.Bouncer, -5.3f, 0.4f, 0.5f)
                .SpikeBall(5.25f, 0f, 0.4f).Moving(0f, 1.3f, 3f)
                .Star(-4.6f, -3.9f).Star(-4.4f, 4.2f).Star(4.6f, 3.9f),

            new LevelDef("graze", "Cutting Corners, Legally", 2.7f, 6.2f).WithHint("GRAZE THE ROUND CORNERS").WithPar(2)
                .StartAt(-2.8f, -6.2f)
                .Wall(1.7f, -1.6f, 5.6f, 2.4f).Rounded(1.1f)
                .Wall(-1.7f, 3.4f, 5.6f, 2.4f).Rounded(1.1f)
                .SpikeBall(3f, 0.9f, 0.4f)
                .Star(-2.9f, -2f).Star(0f, 0.9f).Star(2.9f, 3.4f),

            new LevelDef("curve", "Circling Back Forever", -3.4f, 2.1f).WithHint("RIDE THE CURVE")
                .StartAt(2.6f, -6f)
                .Arc(BlockKind.Wall, 0f, 3.4f, 4.1f, -2f, 182f)
                .Rail(BlockKind.Wall, 0f, 0f, -8.1f, 0f, 0.6f)
                .SpikeBall(2.4f, -1.8f, 0.4f).Moving(1.3f, 0f, 2.6f)
                .Star(3f, 1.4f).Star(0f, 6.85f).Star(-2.5f, 5.9f),

            new LevelDef("kicker", "The Boss Will See You Now", -2.8f, 6.2f).WithHint("RED CHAIRS KICK YOU AWAY").WithPar(2)
                .Wall(-0.9f, 3.2f, 7.2f, 0.6f)
                .Kicker(3.4f, 7f, 0.45f)
                .Kicker(-3.4f, -1.2f, 0.5f)
                .Post(BlockKind.Bouncer, 0.8f, 0.6f, 0.5f)
                .SpikeBall(-0.6f, 5.8f, 0.4f)
                .Post(BlockKind.Bouncer, 2.8f, -3.6f, 0.45f)  // OUT-25: a chair to warm up on
                .Star(3.6f, 1.4f).Star(3.6f, 4.6f).Star(0.8f, 6.6f),

            new LevelDef("figure8", "This Meeting Has No End", 0f, 12.2f).WithHint("CROSS THE BRIDGE IN THE MIDDLE").WithPar(3)
                .WithShape(ShapeDef.FigureEight(6f, 7.4f, 3.2f))
                .StartAt(0f, -12.2f)
                .Bridge(0f, 0f, 3.2f, 3.4f)
                .Post(BlockKind.Wall, 0f, -7.4f, 2.5f)
                .Post(BlockKind.Wall, 0f, 7.4f, 2.5f)
                .Post(BlockKind.Bouncer, -4.3f, -7.4f, 0.5f)
                .Post(BlockKind.Bouncer, 4.3f, 7.4f, 0.5f)
                .SpikeBall(0f, 0f, 0.36f).Moving(0.95f, 0f, 2.6f)
                .Star(4.2f, -4.6f).Star(0f, 2.6f).Star(-4.2f, 9.8f),

            new LevelDef("boing", "Launched Into Management", 0f, 5f).WithHint("BOUNCE MATS LAUNCH YOU OVER").WithPar(2)
                .Wall(0f, 1f, 9f, 0.6f)
                .Jump(0f, -2f, 0.8f)
                .Post(BlockKind.Bouncer, -3.4f, 6.6f, 0.5f)
                .Post(BlockKind.Bouncer, 3.4f, 6.6f, 0.5f)
                .SpikeBall(-3f, -2f, 0.4f).Moving(0.8f, 0f, 2.4f)
                .Star(0f, -3.6f).Star(0f, 2.6f).Star(2.6f, 4.2f),

            new LevelDef("rows", "Open Plan, Closed Hearts", 0f, 6.4f).WithHint("SLALOM THROUGH THE DESKS").WithPar(3)
                .Wall(-1.2f, -2.6f, 6.6f, 0.6f)
                .Wall(1.2f, 0.6f, 6.6f, 0.6f)
                .Wall(-1.2f, 3.8f, 6.6f, 0.6f)
                .SpikeBall(0f, -1f, 0.36f).Moving(2.4f, 0f, 3f)
                .Star(3.4f, -2.6f).Star(-3.4f, 0.6f).Star(3.4f, 3.8f),

            new LevelDef("cubicles", "Cubicle Farm Animal", 2.8f, 6.2f).WithHint("BANK OFF THE PARTITIONS").WithPar(2)
                .StartAt(-2.8f, -6.4f)
                .Wall(-2.3f, -2.2f, 4.4f, 0.4f)
                .Wall(1.2f, -0.4f, 0.4f, 3.6f)
                .Wall(2.3f, 2.6f, 4.4f, 0.4f)
                .Wall(-1.2f, 4.4f, 0.4f, 3.2f)
                .Post(BlockKind.Bouncer, -3f, 1.2f, 0.45f)
                .Post(BlockKind.Bouncer, 3f, -4f, 0.45f)
                .Star(0f, -3.6f).Star(-0.2f, 1.4f).Star(1.2f, 5.6f),

            // ---------- Chapter 2, Office Kitchen (levels 12-20): chairs, vents, switches, spills ----------
            new LevelDef("rubber", "Musical Chairs, No Music", -3f, 6.3f).WithHint("CHAIRS BOUNCE YOU AROUND").WithPar(2)
                .Rail(BlockKind.Wall, 0.3f, -4.6f, 4f, -0.9f, 4f, -0.9f, 5.6f)
                .Post(BlockKind.Bouncer, 3.3f, 6.6f, 0.6f)
                .Post(BlockKind.Bouncer, 1.3f, 2f, 0.55f)
                .Post(BlockKind.Bouncer, -2.9f, 0.8f, 0.5f)
                .Hazard(2.6f, -2f, 3.8f, 0.5f)
                .Post(BlockKind.Bouncer, -3f, -3.9f, 0.45f)  // OUT-25: one more chair at the start
                .Star(0f, -0.8f).Star(3.6f, 3.8f).Star(1.2f, 6.8f),

            new LevelDef("breeze", "Can You Turn Off the AC?", -2.8f, 6.2f).WithHint("THE VENT PUSHES YOU SIDEWAYS").WithPar(2)
                .Taller(6f)
                .StartAt(3f, -12.8f)
                .Wall(-1.4f, -10.6f, 6.2f, 0.6f)
                .Kicker(-3.6f, -12.8f, 0.45f)
                .Wind(0f, 0.6f, 3.2f, 9f, -90f)
                .Wall(1.3f, 3.8f, 6.4f, 0.6f)
                .SpikeBall(3.5f, 6.3f, 0.4f)
                .Post(BlockKind.Bouncer, -3.3f, -3.4f, 0.5f)
                .Star(3.4f, -9f).Star(-0.8f, 0.6f).Star(-3.4f, 4.6f),

            new LevelDef("oneway", "Career Path: One Way", 3f, 5.6f).WithHint("GREEN GATES: ONE WAY ONLY").WithPar(2)
                .Wall(0f, 1f, 3f, 0.6f)
                .OneWay(-3f, 1f, 3f, 0f)
                .OneWay(3f, 1f, 3f, 180f)
                .Post(BlockKind.Bouncer, 0f, 4.4f, 0.55f)
                .SpikeBall(3f, 3.2f, 0.4f).Moving(1f, 0f, 2.6f)
                .Wall(2.9f, -3.4f, 2.4f, 0.6f).Post(BlockKind.Bouncer, -3.1f, -3.8f, 0.45f)  // OUT-25: a side desk and a chair
                .Star(-3f, -1f).Star(-3f, 4f).Star(1.4f, 6.6f).Reception(false),

            new LevelDef("switch", "Badge In, Lose Your Soul", 0f, 6.2f).WithHint("PRESS THE BADGE READER FIRST").WithPar(3)
                .Rail(BlockKind.Wall, 0f, -2.2f, 8.1f, -2.2f, 3.6f)
                .Rail(BlockKind.Wall, 0f, 2.2f, 8.1f, 2.2f, 3.6f)
                .Gate(0, 0f, 3.6f, 3.9f, 0.5f)
                .Switch(0, 3.4f, 6.6f)
                .Post(BlockKind.Bouncer, 0f, -0.8f, 0.6f)
                .SpikeBall(-3.3f, 1.2f, 0.45f).Moving(0f, 1.8f, 3.2f)
                .Ice(-2.8f, -3.6f, 2.4f, 1.6f).Post(BlockKind.Bouncer, 3f, -3.4f, 0.45f)  // OUT-25: a wet patch and a chair by the start
                .Star(3.4f, 3f).Star(-3.35f, 6.4f).Star(0f, 2.2f),

            new LevelDef("spinner", "Death by Spreadsheet", 0f, 6.4f).WithHint("TIME IT WITH THE SPINNERS").WithPar(2)
                .WithShape(ShapeDef.Stadium(9f, 16f))
                .StartAt(0f, -6f)
                .Wall(-3.45f, 0f, 2.1f, 0.6f)
                .Wall(3.45f, 0f, 2.1f, 0.6f)
                .Bouncer(0f, 0f, 4.2f, 0.55f).Spinning(70f)
                .Hazard(0f, 4f, 3f, 0.45f).Spinning(-50f)
                .Star(0f, -2.6f).Star(-3.2f, 3.6f).Star(-1.8f, 5.9f),

            new LevelDef("popwalls", "Standing Desks Hate You", 0f, 6.3f).WithHint("DESKS POP UP AND DOWN").WithPar(2)
                .PopWall(0f, -1.5f, 9f, 0.5f, 2.4f, 0f)
                .PopWall(0f, 1.5f, 9f, 0.5f, 2.4f, 0.33f)
                .PopWall(0f, 4.2f, 9f, 0.5f, 2.4f, 0.66f)
                .Post(BlockKind.Bouncer, -3.1f, -4f, 0.45f).Post(BlockKind.Bouncer, 3.1f, -4f, 0.45f)  // OUT-25: chairs waiting for the desks
                .Star(-3f, 0f).Star(3f, 2.9f).Star(-3.1f, 5.6f),

            new LevelDef("hairpin", "The Long Walk to HR", 4.25f, -10.8f).WithHint("UP, AROUND, AND BACK DOWN").WithPar(4)
                .WithShape(ShapeDef.Polygon(1.2f, -7f, -13f, -1.5f, -13f, -1.5f, 6f, 1.5f, 6f, 1.5f, -13f, 7f, -13f, 7f, 13f, -7f, 13f))
                .StartAt(-4.25f, -11f)
                .Wall(-5.6f, -5f, 2.8f, 0.6f)
                .Wall(-2.9f, 0.5f, 2.8f, 0.6f)
                .Post(BlockKind.Bouncer, 0f, 9.6f, 0.55f)
                .SpikeBall(4.25f, 1f, 0.4f).Moving(1.6f, 0f, 3f)
                .Boost(4.25f, -4.5f, 2f, 3f, 180f)
                .Post(BlockKind.Bouncer, -6.1f, -8.4f, 0.45f)  // OUT-25: a chair in the long corridor
                .Star(-4.25f, -2.2f).Star(0f, 11.2f).Star(4.25f, 4.6f),

            new LevelDef("microwave", "Who Microwaved Fish?", 0f, 6.2f).WithHint("MIND THE WET FLOOR").WithPar(2)
                .Ice(0f, -1.6f, 9f, 1.8f)
                .Wall(0f, 1.2f, 4.2f, 1.4f)
                .Magnet(3.3f, -3.4f, 1.8f)
                .SpikeBall(0f, 3.9f, 0.4f).Moving(2.4f, 0f, 2.8f)
                .Star(-3.4f, 1.2f).Star(3.4f, 1.2f).Star(0f, -1.6f),

            new LevelDef("fridge", "Not Your Yogurt", 0f, 6.6f).WithHint("DOWN THE GALLEY, ROUND THE SPILL").WithPar(2)
                .Wall(-3.25f, 0.2f, 2.5f, 9.4f)
                .Wall(3.25f, 0.2f, 2.5f, 9.4f)
                .Vortex(0f, 0.4f, 1.4f)
                .Post(BlockKind.Bouncer, 0f, 5.2f, 0.45f)
                .Star(0f, -2.8f).Star(-1.3f, 2.6f).Star(1.3f, 2.6f),

            // ---------- Chapter 3, Meeting Room (levels 21-29): mail chutes, jumps, hallways, timing ----------
            new LevelDef("portal", "Lost in Internal Mail", 1.6f, 5.4f).WithHint("IN ONE CHUTE, OUT THE OTHER").WithPar(2)
                .WithShape(ShapeDef.Rect(9f, 16f, 1.4f))
                .Rail(BlockKind.Wall, 0f, -4.6f, 3f, 4.6f, 3f)
                .Portal(3.3f, -1.2f, -3.3f, 5.4f, 0f, -90f)
                .Hazard(3.3f, -3.4f, 2.2f, 0.45f).Moving(1f, 0f, 2.6f)
                .Post(BlockKind.Bouncer, 3.6f, 6.9f, 0.45f)
                .Star(1f, -4.6f).Star(3.3f, -2.4f).Star(-1f, 5.4f),

            new LevelDef("ramp", "Leap of Faith, No Raise", -2.4f, 5.6f).WithHint("JUMP IT, OR TAKE THE BRIDGE").WithPar(2)
                .StartAt(-2.4f, -6.5f)
                .Ramp(-2.4f, -2.4f, 3f, 2.4f)
                .Gap(-0.95f, 0.9f, 7.3f, 1.8f)
                .Wall(1.6f, 4.2f, 0.6f, 3.2f)
                .Star(-2.4f, 0.9f).Star(3.6f, 0.9f).Star(-0.5f, 3.8f),  // one over the jump, one on the bridge, one behind the wall

            new LevelDef("popspikes", "Stapled to the Floor", 0f, 6.4f).WithHint("CROSS WHEN STAPLES ARE DOWN").WithPar(2)
                .Spikes(0f, -1.8f, 9f, 1.1f, 2.4f, 0f)
                .Spikes(0f, 1.2f, 9f, 1.1f, 2.4f, 0.33f)
                .Spikes(0f, 4f, 9f, 1.1f, 2.4f, 0.66f)
                .Star(-3.2f, -0.3f).Star(3.5f, 2.6f).Star(-2.8f, 5.5f),

            new LevelDef("magnet", "Caffeine Is a Leash", 2.8f, 6f).WithHint("THE COFFEE MACHINE PULLS YOU").WithPar(2)
                .WithShape(ShapeDef.Rect(9f, 16f, 1.4f))
                .StartAt(2.8f, -6.2f)
                .Wall(-1.3f, -0.8f, 6.4f, 0.6f)
                .Wall(1.3f, 3.2f, 6.4f, 0.6f)
                .Magnet(-3.2f, 1.2f, 2.4f)
                .SpikeBall(0.6f, 6.2f, 0.4f)
                .Ice(-2.4f, -3.6f, 3.2f, 2f).Post(BlockKind.Bouncer, 0.4f, -4.3f, 0.42f)  // OUT-25: someone spilled on the way to the machine
                .Star(3.2f, -0.8f).Star(0f, 1.2f).Star(-3.4f, 4.6f),

            new LevelDef("ice", "Slip, Sue, Get Fired", 2.8f, 6.3f).WithHint("WET FLOOR: GO GENTLE").WithPar(2)
                .StartAt(-2.8f, -6.5f)
                .Ice(0f, 0.3f, 9f, 8.6f)
                .SpikeBall(-1.8f, -1.2f, 0.45f)
                .SpikeBall(1.6f, 0.8f, 0.45f)
                .SpikeBall(-0.6f, 3f, 0.45f)
                .Sticky(2.8f, 7.55f, 2.4f, 0.4f)
                .Wall(-2.5f, 5.6f, 4f, 0.5f)
                .Star(0.3f, -0.9f).Star(-3.4f, 2f).Star(2.8f, 3.8f),

            new LevelDef("fan", "The AC Has a Grudge", 3f, 6.2f).WithHint("RIDE THE AIR CON").WithPar(2)
                .StartAt(-3f, -6.5f)
                .Rail(BlockKind.Wall, 0f, -1.5f, -8.1f, -1.5f, 4.2f)
                .Rail(BlockKind.Wall, 0f, 1.5f, -4.4f, 1.5f, 8.1f)
                .Wind(0f, 0f, 2.5f, 8f, 180f)
                .Wind(3f, -1f, 2.5f, 6f, 0f)
                .Star(-3f, 2f).Star(0f, 6.4f).Star(0f, -6.2f),

            new LevelDef("crossroads", "Every Hallway Leads Here", 9.3f, 0f).WithHint("PICK THE RIGHT HALLWAY").WithPar(2)
                .WithShape(ShapeDef.Polygon(1f, -2.5f, -11f, 2.5f, -11f, 2.5f, -2.5f, 11f, -2.5f, 11f, 2.5f, 2.5f, 2.5f,
                    2.5f, 11f, -2.5f, 11f, -2.5f, 2.5f, -11f, 2.5f, -11f, -2.5f, -2.5f, -2.5f))
                .StartAt(0f, -9.3f)
                .Kicker(0f, 0f, 0.6f)
                .Post(BlockKind.Bouncer, 0f, 9.4f, 0.55f)
                .Post(BlockKind.Bouncer, -9.4f, 0f, 0.55f)
                .SpikeBall(5.6f, 0f, 0.4f).Moving(0f, 1.6f, 2.8f)
                .Post(BlockKind.Bouncer, -1.6f, -6.2f, 0.4f).Post(BlockKind.Bouncer, 1.6f, -4.4f, 0.4f)  // OUT-25: chairs zigzag down the south hallway
                .Star(-7.6f, 0f).Star(0f, 7.6f).Star(3.6f, -1.4f),

            new LevelDef("boardroom", "Bored Room", 0f, 6.4f).WithHint("BANK OFF THE CHAIRS").WithPar(2)
                .StartAt(0f, -6.2f)
                .Wall(0f, 0.8f, 3f, 6f).Rounded(1.2f)
                .Post(BlockKind.Bouncer, -2.6f, -1.4f, 0.45f)
                .Post(BlockKind.Bouncer, -2.6f, 3f, 0.45f)
                .Post(BlockKind.Bouncer, 2.6f, -1.4f, 0.45f)
                .Post(BlockKind.Bouncer, 2.6f, 3f, 0.45f)
                .Star(-3.7f, 0.8f).Star(3.7f, 0.8f).Star(0f, -2.8f).Reception(true),

            new LevelDef("standup", "Stand Up, Shut Up", 0f, 6f).WithHint("WAIT FOR THE DESKS TO SIT").WithPar(2)
                .PopWall(-2.25f, -1.5f, 4.5f, 0.5f, 2.2f, 0f)
                .PopWall(2.25f, -1.5f, 4.5f, 0.5f, 2.2f, 0.5f)
                .PopWall(-2.25f, 2f, 4.5f, 0.5f, 2.2f, 0.5f)
                .PopWall(2.25f, 2f, 4.5f, 0.5f, 2.2f, 0f)
                .Post(BlockKind.Bouncer, -3.1f, -4f, 0.45f).Post(BlockKind.Bouncer, 3.1f, -4f, 0.45f)  // OUT-25: chairs for the people still sitting
                .Star(-3f, 0.25f).Star(3f, 0.25f).Star(0f, 4.2f),

            // ---------- Chapter 4, Manager's Office (levels 30-37): gaps, cacti, speed strips, pacing managers ----------
            new LevelDef("hopscotch", "Mind the Budget Gap", 0f, 6.2f).WithHint("BOUNCE OVER THE GAPS").WithPar(2)
                .Gap(0f, -1f, 9.2f, 1.6f)
                .Gap(0f, 3f, 9.2f, 1.6f)
                .Jump(-2f, -3.2f, 0.75f)
                .Jump(2f, 1f, 0.75f)
                .Star(-1.6f, -1f).Star(0f, 1f).Star(2f, 3f),

            new LevelDef("thorns", "Only the Cacti Survive", 2.8f, 5.8f).WithHint("CACTI SEND YOU BACK").WithPar(3)
                .Wall(-1.5f, -2.5f, 6f, 0.6f)
                .Wall(1.5f, 1.5f, 6f, 0.6f)
                .SpikeBall(3f, -2.5f, 0.35f)
                .SpikeBall(0f, -0.5f, 0.45f).Moving(2.6f, 0f, 3f)
                .SpikeBall(-3f, 1.5f, 0.35f)
                .SpikeBall(0.2f, 4.6f, 0.45f).Moving(0f, 1.2f, 2.4f)
                .Star(2.1f, -2.5f).Star(-2.1f, 1.5f).Star(0.6f, 3.2f),

            new LevelDef("maze", "Lost in Bureaucracy", 2.6f, 6.4f).WithHint("FIND THE BADGE READER FIRST").WithPar(4)
                .Taller(6f)
                .StartAt(0f, -13f)
                .Spikes(0f, -11.2f, 9f, 0.9f, 2.2f, 0f)
                .Wall(-2.7f, -8.6f, 3.6f, 0.6f)
                .Wall(2.7f, -8.6f, 3.6f, 0.6f)
                .Wall(-1.2f, -2.6f, 6.6f, 0.6f)
                .Wall(1.2f, 0.8f, 6.6f, 0.6f)
                .Gate(0, -3.3f, 4.2f, 2.4f, 0.6f)
                .Wall(1.2f, 4.2f, 6.6f, 0.6f)
                .Switch(0, 3.4f, 2.5f)
                .Kicker(-3.4f, 2.5f, 0.45f)
                .SpikeBall(0.4f, -1f, 0.4f).Moving(2.2f, 0f, 2.8f)
                .Star(0f, -9.6f).Star(-0.4f, 2.5f).Star(-1f, 6.4f),

            new LevelDef("crate", "Boxes Full of Regret", -2.6f, 6.4f).WithHint("PUSH THE BOXES ASIDE").WithPar(4)
                .Rail(BlockKind.Wall, 0f, -4.6f, 0f, -1.45f, 0f)
                .Rail(BlockKind.Wall, 0f, 1.45f, 0f, 4.6f, 0f)
                .Crate(0f, 0f, 1.3f)
                .Rail(BlockKind.Wall, 0f, -4.6f, 4f, 1.7f, 4f)
                .Crate(3.2f, 4f, 1.4f)
                .Crate(-2.8f, -3.4f, 1f).Post(BlockKind.Bouncer, 3f, -3.6f, 0.45f)  // OUT-25: one more box nobody will ever open
                .Star(1.4f, -0.2f).Star(2.6f, 2f).Star(-0.4f, 5.6f),  // beside the middle box: hug it on the way up

            new LevelDef("speedpad", "Fast Track to Burnout", 0f, 6.2f).WithHint("SPEED STRIPS BEAT THE RUG").WithPar(2)
                .StartAt(0f, -6.5f)
                .Boost(0f, -3f, 2f, 2.6f)
                .Boost(-3.3f, -1.4f, 1.6f, 2f, 20f)
                .Mud(0f, 1.6f, 9f, 3f)
                .SpikeBall(-1.7f, 5f, 0.4f)
                .SpikeBall(1.7f, 5f, 0.4f)
                .Star(0f, -1.2f).Star(-3f, 2.2f).Star(3.1f, 4.6f),  // the side strip, and one daringly close to a stapler

            new LevelDef("whirlpool", "Spiralling Since Monday", 0f, 6.2f).WithHint("SPILLS SPIN YOU ROUND").WithPar(2)
                .Vortex(0f, 1f, 2.6f)
                .Wall(-3.2f, 4.4f, 2.6f, 0.6f)
                .Wall(3.2f, 4.4f, 2.6f, 0.6f)
                .SpikeBall(-3.5f, 1f, 0.4f)
                .SpikeBall(3.5f, 1f, 0.4f)
                .SpikeBall(-3.2f, -3.6f, 0.35f).Post(BlockKind.Bouncer, 3.2f, -3.8f, 0.45f)  // OUT-25: a cactus and a chair by the start
                .Star(-1.6f, -1f).Star(0f, 1f).Star(1.2f, 3.6f),

            new LevelDef("feedback", "Dodge the Feedback", 0f, 6.4f).WithHint("DODGE THE PACING MANAGER").WithPar(2)
                .SpikeBall(0f, 0f, 0.42f).Moving(3.2f, 0f, 2.4f)
                .Wall(0f, 3.6f, 3.6f, 0.6f)
                .Kicker(-3.6f, 3.6f, 0.45f)
                .Kicker(3.6f, 3.6f, 0.45f)
                .Post(BlockKind.Bouncer, -3f, -3.4f, 0.45f).Post(BlockKind.Bouncer, 3f, -3.4f, 0.45f)  // OUT-25: the audience for your feedback
                .Star(-3.6f, 0f).Star(3.6f, 0f).Star(0f, 5f).Reception(false),

            new LevelDef("window", "View of Another Office", 0f, 5.2f).WithHint("ROUND THE DESK, UP THE FAST LANE").WithPar(2)
                .WithShape(ShapeDef.Circle(6.8f))
                .StartAt(0f, -5f)
                .Post(BlockKind.Wall, 0f, 0f, 1.7f)
                .Arc(BlockKind.Wall, 0f, 0f, 3.4f, 20f, 160f)
                .Boost(-4.6f, 0f, 1.6f, 2.4f)
                .Post(BlockKind.Bouncer, 4.4f, 1.6f, 0.45f)
                .Star(-4.6f, 2.4f).Star(4.6f, -0.6f).Star(0f, -2.6f),

            // ---------- Chapter 5, HR & Admin (levels 38-45): spill leaps, beanbags, filing boxes, paperwork ----------
            new LevelDef("lagoon", "Not My Spill", 0f, 6.2f).WithHint("BOUNCE OVER THE SPILL").WithPar(2)
                .Vortex(0f, 2f, 2.4f)
                .Jump(0f, -2.6f, 0.8f)
                .SpikeBall(-3.5f, 2f, 0.4f)
                .SpikeBall(3.5f, 2f, 0.4f)
                .Star(-2.6f, -1.4f).Star(0f, 2f).Star(2.6f, 5.2f),

            new LevelDef("corner", "Corner Office? Not You", 3.2f, 5.5f).WithHint("BOUNCE ROUND THE CORNER").WithPar(2)
                .WithShape(ShapeDef.Polygon(1f, -4.5f, -8f, 0f, -8f, 0f, 3f, 4.5f, 3f, 4.5f, 8f, -4.5f, 8f))
                .StartAt(-2.25f, -6.5f)
                .Boost(-2.25f, -2.4f, 2f, 2.4f)
                .Post(BlockKind.Bouncer, -3.6f, 7.1f, 0.55f)
                .SpikeBall(1.4f, 5.5f, 0.4f).Moving(0f, 1.3f, 2.4f)
                .Star(-2.25f, 1.5f).Star(-2.6f, 6.2f).Star(0.6f, 4f),

            new LevelDef("timing", "The Printer Wants Blood", 0f, 6.6f).WithHint("WAIT FOR A GAP").WithPar(2)
                .WithShape(ShapeDef.Rect(9f, 16f, 1.6f))
                .Hazard(0f, -2f, 2.6f, 0.45f).Moving(3f, 0f, 3f)
                .Hazard(0f, 1.2f, 2.2f, 0.45f).Moving(3.1f, 0f, 2.4f, 1.2f)
                .Hazard(0f, 4.2f, 2.6f, 0.45f).Moving(3f, 0f, 3.4f, 0.6f)
                .Post(BlockKind.Bouncer, -3.2f, -4.2f, 0.45f)  // OUT-25: a chair in the queue
                .Star(0f, -0.4f).Star(2f, 2.7f).Star(-2.6f, 5.8f),

            new LevelDef("turnstile", "Security Theatre", 3f, 6f).WithHint("SPIN, THEN SLIP THROUGH").WithPar(2)
                .Bouncer(0f, 0.4f, 3.6f, 0.5f).Spinning(80f)
                .Wall(0f, 3f, 3.2f, 0.6f)
                .OneWay(-3.05f, 3f, 2.9f, 0f)
                .OneWay(3.05f, 3f, 2.9f, 180f)
                .Kicker(-3.4f, 6.5f, 0.45f)
                .Sticky(-3.3f, -3.2f, 1.2f, 1.2f).Rounded(0.6f).Post(BlockKind.Bouncer, 3.1f, -3.8f, 0.45f)  // OUT-25: a beanbag and a chair in reception
                .Star(-3f, 1f).Star(-3f, 5f).Star(0f, 6.4f),

            new LevelDef("soft", "Nap Pods for Managers", 0f, 5.6f).WithHint("BEANBAGS SOAK UP SPEED").WithPar(2)
                .WithShape(ShapeDef.Rect(9f, 16f, 1.6f))
                .Arc(BlockKind.Sticky, 0f, 5.6f, 1.6f, 15f, 165f)
                .Hazard(-2.75f, 5.6f, 2.5f, 0.45f)
                .Hazard(2.75f, 5.6f, 2.5f, 0.45f)
                .Wall(0f, 1.5f, 3f, 0.5f)
                .Sticky(-4.2f, 3f, 0.6f, 3f)
                .Sticky(4.2f, 3f, 0.6f, 3f)
                .Sticky(-3.3f, -3.4f, 1.2f, 1.2f).Rounded(0.6f).Sticky(3.3f, -3.4f, 1.2f, 1.2f).Rounded(0.6f)  // OUT-25: more beanbags, of course
                .Star(0f, -0.5f).Star(-3f, 3f).Star(-1.2f, 4.3f),

            new LevelDef("tidepool", "Twice the Mess, No Raise", -3f, 6.8f).WithHint("TWO SPILLS, TWO SPINS").WithPar(2)
                .StartAt(2.6f, -6.5f)
                .Vortex(-2f, -1f, 2f, true)
                .Vortex(2f, 2.4f, 2f)
                .Conveyor(0f, 5f, 2f, 9f, 90f)
                .SpikeBall(0f, -3.6f, 0.4f).Moving(2f, 0f, 3f)
                .Star(3.3f, -3.8f).Star(2f, 2.4f).Star(0f, 5f),

            new LevelDef("filing", "File It Under Never", -2.6f, 6.4f).WithHint("BADGE IN, THEN SHIFT THE BOX").WithPar(2)
                .Wall(-2.4f, -2f, 4.2f, 0.6f)
                .Wall(2.4f, 1f, 4.2f, 0.6f)
                .Gate(0, -2.4f, 4f, 4.2f, 0.6f)
                .Switch(0, 3.4f, -3.4f)
                .Wall(-0.1f, 6.05f, 0.4f, 4.1f)
                .Crate(2.4f, 4.6f, 1f)
                .Star(3.4f, -0.5f).Star(-3.4f, 1f).Star(2f, 6.4f),

            new LevelDef("paperwork", "Shred the Evidence", 0f, 6f).WithHint("SMASH THROUGH THE FOLDERS").WithPar(2)
                .Breakable(-3f, 0f, 3f, 0.6f)
                .Breakable(0f, 0f, 3f, 0.6f)
                .Breakable(3f, 0f, 3f, 0.6f)
                .Breakable(-1.2f, 3.4f, 6.6f, 0.6f)
                .Wall(3.3f, 3.4f, 2.4f, 0.6f)
                .Breakable(-3f, -3.6f, 2f, 0.6f).Post(BlockKind.Bouncer, 3.1f, -3.8f, 0.45f)  // OUT-25: a first pile of folders to smash
                .Star(-3.6f, 1.7f).Star(3.6f, 1.7f).Star(-3.4f, 5.2f).Reception(true),  // the last one is behind the folders

            // ---------- Chapter 6, Executive Floor (levels 46-54): mail belts, sliding doors, air con, elevators ----------
            new LevelDef("conveyor", "Conveyor Belt of Life", -3f, 6.3f).WithHint("THE BELTS CARRY YOU").WithPar(3)
                .Taller(6f)
                .StartAt(-2.6f, -13f)
                .Conveyor(0f, -11f, 2f, 9f, -90f)
                .SpikeBall(2.8f, -9f, 0.4f).Moving(1.2f, 0f, 2.6f)
                .Conveyor(0f, -1f, 2.2f, 9f, -90f)
                .Conveyor(0f, 3f, 2.2f, 9f, 90f)
                .Wall(1.2f, 5f, 6.6f, 0.5f)
                .Star(-3.5f, -9.6f).Star(-1f, 3f).Star(-3.2f, 4.4f),

            new LevelDef("tidegate", "Escalated to Nobody", -3f, 6.2f).WithHint("RIDE THE BELT, MIND THE DESK").WithPar(2)
                .Conveyor(0f, 0f, 2.2f, 9f, 90f)
                .PopWall(0f, 3f, 9f, 0.5f, 2.6f, 0f)
                .Post(BlockKind.Bouncer, 2.8f, 5.6f, 0.5f)
                .Star(2.5f, 0f).Star(-3.5f, 1.6f).Star(0f, 5f),

            new LevelDef("icebreak", "Cut Through the Red Tape", 0f, 6f).WithHint("HIT FOLDERS HARD TO BREAK THEM").WithPar(2)
                .Wall(0f, 0.5f, 5f, 0.6f)
                .Breakable(0f, 4.2f, 3.4f, 0.6f)
                .Breakable(-2f, 5.8f, 0.6f, 3.6f)
                .Breakable(2f, 5.8f, 0.6f, 3.6f)
                .Ice(0f, 2.3f, 9f, 2f)
                .Boost(3.2f, -4f, 1.4f, 2.4f)  // OUT-25: a speed strip to hit the folders hard
                .Star(-3.4f, -0.5f).Star(0f, 3f).Star(-3.4f, 6.8f),

            new LevelDef("avalanche", "Shiny Floor, Dull Soul", 0f, 6f).WithHint("SLIDE PAST THE STANDING DESKS").WithPar(2)
                .Taller(6f)
                .StartAt(0f, -13f)
                .Ice(0f, -8f, 9f, 6f)
                .PopWall(-2.5f, -6f, 4f, 0.5f, 2.2f, 0f)
                .PopWall(2.5f, -3f, 4f, 0.5f, 2.2f, 0.5f)
                .Wall(0f, 0.5f, 5f, 0.6f)
                .Spikes(0f, 3f, 9f, 0.9f, 2f, 0.25f)
                .Star(3f, -8f).Star(-3.2f, -1.4f).Star(0f, 4.6f),

            new LevelDef("door", "Doors Close on Dreams", 2.5f, 6f).WithHint("SLIP THROUGH THE DOORS").WithPar(3)
                .WithShape(ShapeDef.Chamfered(9f, 16f, 1.6f))
                .Wall(-3f, 0f, 3.2f, 0.5f)
                .Wall(3f, 0f, 3.2f, 0.5f)
                .Wall(0f, -0.47f, 2.9f, 0.42f).Moving(1.6f, 0f, 3f)          // door panel on its own track, in front of the wall line
                .Wall(-3.4f, 3.6f, 2.2f, 0.5f)
                .Wall(2.1f, 3.6f, 4.8f, 0.5f)
                .Wall(-1.3f, 3.13f, 2.1f, 0.42f).Moving(1f, 0f, 2.2f, 1f)    // second door, also on its own track
                .Post(BlockKind.Bouncer, -3.1f, -4f, 0.45f)  // OUT-25: a chair waiting for the doors
                .Star(0f, -2f).Star(-1.3f, 1.8f).Star(-1.3f, 5f),

            new LevelDef("blizzard", "Executive Wind Chill", 2.6f, 6.4f).WithHint("MIND THE AIR CON: AIM UPWIND").WithPar(2)
                .Ice(0f, 1f, 9f, 7f)
                .Wind(0f, 1f, 3f, 9f, 90f)
                .Breakable(2.2f, 4.8f, 4.6f, 0.6f)
                .Wall(-2.4f, 4.8f, 4.2f, 0.6f)
                .Star(3.6f, -1f).Star(2.8f, 1f).Star(2.2f, 3.6f),

            new LevelDef("curveswitch", "Badge, Curve, Despair", -3.4f, 1.3f).WithHint("CURVE INTO THE BADGE READER").WithPar(2)
                .StartAt(2.6f, -6f)
                .Arc(BlockKind.Wall, 0f, 3.4f, 4.1f, -2f, 182f)
                .Rail(BlockKind.Wall, 0f, 0f, -8.1f, 0f, 0.6f)
                .Switch(0, 0f, 6.85f)
                .Gate(0, -2.45f, 2.75f, 4.4f, 0.5f)
                .SpikeBall(2.4f, -2.5f, 0.4f).Moving(1.3f, 0f, 2.2f)
                .Star(2.6f, 1f).Star(-2.6f, 5.9f).Star(-2.4f, -3f),

            new LevelDef("elevator", "Elevator Pitch Gone Wrong", 0f, 6f).WithHint("TAKE THE ELEVATOR UP").WithPar(2)
                .Wall(0f, 0.8f, 9f, 0.6f)
                .Portal(3.2f, -3f, -3.2f, 3.6f, 0f, 0f)
                .SpikeBall(0f, 3.4f, 0.4f).Moving(2.4f, 0f, 2.8f)
                .Ice(0f, -3f, 9f, 2f)
                .Star(-3.2f, -3f).Star(3.2f, 4.2f).Star(-1.7f, 5.4f).Reception(true),

            new LevelDef("green", "The CEO Is Golfing Again", 0f, 4.6f).WithHint("JUMP THE WATER FEATURE").WithPar(2)
                .WithShape(ShapeDef.Circle(7f))
                .StartAt(0f, -5.2f)
                .Ramp(0f, -2.6f, 2.6f, 2.2f)
                .Gap(0f, 0.6f, 14f, 1.6f)
                .Post(BlockKind.Bouncer, -3f, 3.6f, 0.45f)
                .Post(BlockKind.Bouncer, 3f, 3.6f, 0.45f)
                .Star(0f, 0.6f).Star(-4.4f, -1.2f).Star(4.4f, -1.2f),

            // ---------- Chapter 7, After Hours (levels 55-64): every gadget at once ----------
            new LevelDef("rampbumper", "Quarterly Review Panic", -3f, 6.2f).WithHint("JUMP, THEN BANK OFF THE CHAIR").WithPar(2)
                .WithShape(ShapeDef.Chamfered(9f, 16f, 1.6f))
                .StartAt(0f, -6.5f)
                .Ramp(0f, -2.4f, 3.5f, 2.4f)
                .Gap(0f, 0.9f, 9.2f, 1.8f)
                .Post(BlockKind.Bouncer, 3f, 5.6f, 0.65f)
                .Wall(-1.5f, 3.8f, 3f, 0.5f)
                .SpikeBall(0.6f, 6.6f, 0.4f).Moving(0f, 0.8f, 2f)
                .Star(0f, -4.2f).Star(0f, 1f).Star(3f, 3.2f),

            new LevelDef("skijump", "Synergy, Whatever That Is", 0f, 6.6f).WithHint("JUMP, THEN BOUNCE AGAIN").WithPar(2)
                .StartAt(0f, -6.5f)
                .Ramp(0f, -2.4f, 3.5f, 2.4f)
                .Gap(0f, 0.9f, 9.2f, 1.8f)
                .Jump(-2.8f, 3.4f, 0.7f)
                .Spikes(0f, 5f, 9f, 0.8f, 2.4f, 0f)
                .Star(0f, 0.9f).Star(-2.8f, 3.4f).Star(2.6f, 4.1f),

            new LevelDef("spikeway", "The Share Price Drop", 2.25f, -5.6f).WithHint("UP, OVER, AND DOWN").WithPar(3)
                .StartAt(-2.25f, -6.5f)
                .Rail(BlockKind.Wall, 0f, 0f, -8.1f, 0f, 4.5f)
                .Spikes(-2.25f, -2f, 4.3f, 0.9f, 2f, 0f)
                .Spikes(-2.25f, 2f, 4.3f, 0.9f, 2f, 0.5f)
                .Kicker(0f, 6.6f, 0.45f)
                .Spikes(2.25f, 0f, 4.3f, 0.9f, 1.6f, 0.25f)
                .Wind(2.25f, -1f, 4f, 5f, 180f)
                .Star(-2.25f, 0f).Star(-1.4f, 6.8f).Star(2.25f, 4.5f),

            new LevelDef("pinball", "All-Hands, No Brains", -2.6f, 6.3f).WithHint("CHAIRS EVERYWHERE").WithPar(2)
                .WithShape(ShapeDef.Chamfered(9f, 16f, 1.6f))
                .Hazard(0f, -1.6f, 2.4f, 0.45f).Moving(2.2f, 0f, 2.8f)
                .Post(BlockKind.Bouncer, -1.8f, 1.8f, 0.55f)
                .Post(BlockKind.Bouncer, 1.8f, 1.8f, 0.55f)
                .Post(BlockKind.Bouncer, 0f, 3.6f, 0.55f)
                .Post(BlockKind.Bouncer, 3.2f, 5.2f, 0.5f)
                .Rail(BlockKind.Wall, 0.3f, -4.6f, 4.6f, -1.2f, 4.6f, -1.2f, 6f)
                .Post(BlockKind.Bouncer, -3f, -4f, 0.45f).Post(BlockKind.Bouncer, 3f, -4f, 0.45f)  // OUT-25: two more flippers
                .Star(3.2f, 0.2f).Star(0f, 5.6f).Star(-0.4f, 7.2f),

            new LevelDef("track", "Per Company Policy", 2.75f, 5.2f).WithHint("THE LONG WAY ROUND").WithPar(2)
                .WithShape(ShapeDef.Polygon(2f, -4.5f, -7f, 4.5f, -7f, 4.5f, 7f, 1f, 7f, 1f, -1.5f, -1f, -1.5f, -1f, 7f, -4.5f, 7f))
                .StartAt(-2.75f, 5f)
                .Ice(-2.75f, 1.5f, 3.5f, 4f)
                .Boost(-2.75f, -5.2f, 2.4f, 2.2f, -90f)
                .SpikeBall(0f, -4.2f, 0.4f).Moving(0f, 1.3f, 2.4f)
                .Boost(2.75f, -3.5f, 2.6f, 2.4f)
                .Mud(2.75f, 2.4f, 3.5f, 1.6f)
                .Star(-2.75f, -1f).Star(0f, -6.4f).Star(2.75f, 0.5f),

            new LevelDef("summit", "Almost Out of Office", 3.2f, 6.3f).WithHint("EVERY GADGET YOU KNOW").WithPar(2)
                .Taller(6f)
                .StartAt(0f, -13f)
                .Jump(0f, -10f, 0.8f)
                .Wall(0f, -7.5f, 9f, 0.6f)
                .PopWall(0f, -2f, 9f, 0.5f, 2.4f, 0f)
                .Kicker(-3.3f, 0.5f, 0.5f)
                .Vortex(2f, 2.8f, 1.8f)
                .Wall(-1.5f, 4.6f, 6f, 0.6f)
                .Star(0f, -8.6f).Star(-3f, -0.8f).Star(2f, 2.8f),

            new LevelDef("nightshift", "Unpaid Overtime", 2.8f, 6f).WithHint("THE BELT TAKES YOU LEFT").WithPar(2)
                .Conveyor(0f, -1.5f, 2.4f, 9f, 90f)
                .OneWay(-3f, 1.8f, 3f, 0f)
                .Wall(1.5f, 1.8f, 6f, 0.6f)
                .SpikeBall(2.8f, 3.8f, 0.4f).Moving(1f, 0f, 2.6f)
                .Star(-2f, -1.5f).Star(-3f, 3.4f).Star(0.6f, 4.6f).Reception(false),

            new LevelDef("cleaner", "The Cleaner Saw Nothing", 0f, 6.2f).WithHint("WET FLOOR, THEN STAPLES").WithPar(2)
                .Ice(0f, 0f, 9f, 6f)
                .Spikes(0f, 3.6f, 9f, 0.9f, 2.2f, 0f)
                .Post(BlockKind.Bouncer, -2.6f, 0f, 0.45f)
                .Post(BlockKind.Bouncer, 2.6f, 0f, 0.45f)
                .Star(-3.6f, 1.6f).Star(3.6f, 1.6f).Star(-2.8f, 5.4f),

            new LevelDef("deadline", "Three Deadlines, One Life", 0f, 6.6f).WithHint("THREE DEADLINES. GO!").WithPar(2)
                .Hazard(0f, -2f, 2.4f, 0.45f).Moving(3f, 0f, 2.2f)
                .Hazard(0f, 1f, 2.4f, 0.45f).Moving(3f, 0f, 1.8f, 0.5f)
                .Hazard(0f, 4f, 2.4f, 0.45f).Moving(3f, 0f, 2f, 0.25f)
                .Ice(-2.8f, -4f, 2.6f, 2f)  // OUT-25: the cleaner was here first
                .Star(-3.6f, -0.5f).Star(3.6f, 2.5f).Star(-2.4f, 5.7f),

            new LevelDef("finale", "Last One, Turn Off Lights", 0f, 6.5f).WithHint("EVERYTHING AT ONCE!").WithPar(3)
                .Taller(8f, ShapeDef.Chamfered(9f, 24f, 1.6f))
                .StartAt(0f, -14.6f)
                .Vortex(0f, -11.6f, 2.2f)
                .Kicker(-3.2f, -9.2f, 0.45f)
                .Kicker(3.2f, -9.2f, 0.45f)
                .Rail(BlockKind.Wall, 0f, -1.9f, 8.1f, -1.9f, 5f)
                .Rail(BlockKind.Wall, 0f, 1.9f, 8.1f, 1.9f, 5f)
                .Gate(0, 0f, 5f, 3.3f, 0.5f)
                .Switch(0, -3.4f, 6.4f)
                .Spikes(0f, 2.2f, 9f, 0.9f, 2.2f, 0f)
                .Wind(0f, -0.6f, 2.2f, 9f, -90f)
                .Kicker(-3f, -3.5f, 0.5f)
                .Kicker(3f, -3.5f, 0.5f)
                .Star(0f, -11.6f).Star(-3.4f, 4f).Star(3.4f, 6.4f),
        };

        // Developer test board for tuning collisions. Not part of progression.
        public static readonly LevelDef Lab =
            new LevelDef("lab", "Physics Lab", 3f, 6.6f)
                .StartAt(0f, -6f)
                .Wall(-3.2f, -3.5f, 2f, 0.3f)                                  // thin straight wall, high-speed test
                .Wall(2.6f, -2.4f, 2.2f, 2.2f).Rounded(0.8f)                   // rounded block corner
                .Post(BlockKind.Bouncer, -2.6f, 0.5f, 0.6f)                    // convex rubber bumper
                .Arc(BlockKind.Wall, 0.6f, 4.2f, 2.6f, 120f, 240f)            // concave guide
                .Rail(BlockKind.Wall, 0f, 2.3f, 1.2f, 2.3f, 4.4f)              // narrow channel, left side
                .Rail(BlockKind.Wall, 0f, 3.7f, 1.2f, 3.7f, 4.4f)              // narrow channel, right side
                .Rail(BlockKind.Wall, 0f, -4.6f, 7.6f, -2.6f, 7.6f)            // smooth join: two collinear rods meeting
                .Rail(BlockKind.Wall, 0f, -2.6f, 7.6f, -0.6f, 7.6f);

        public static int IndexOf(string id)
        {
            for (int i = 0; i < All.Length; i++) if (All[i].Id == id) return i;
            return -1;
        }
    }
}
