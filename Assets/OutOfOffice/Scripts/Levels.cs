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
    // Wind: a fan blows along its direction. Vortex: a spinning platform turns the shot its way and flings the puck off its rim.
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
    // The path is built from a source the level data keeps (OUT-12): either corner points bent with a radius, or an arc.
    // Mirror flips the built path left to right, exactly as a remixed room does.
    public class RailDef
    {
        public BlockKind Kind;
        public List<Vector2> Path;
        public float Width = 0.5f;
        public List<Vector2> Points;   // corner points, when the rail is not an arc
        public float Bend;             // corner radius for Points
        public bool IsArc;
        public Vector2 ArcCenter;
        public float ArcRadius, ArcFrom, ArcTo;  // degrees, counter-clockwise from +X
        public bool Mirror;

        public static RailDef Through(BlockKind kind, float bend, List<Vector2> points) =>
            new RailDef { Kind = kind, Bend = bend, Points = points }.Rebuild();

        public static RailDef Around(BlockKind kind, Vector2 center, float radius, float fromDeg, float toDeg) =>
            new RailDef { Kind = kind, IsArc = true, ArcCenter = center, ArcRadius = radius, ArcFrom = fromDeg, ArcTo = toDeg }.Rebuild();

        public RailDef Rebuild()
        {
            Path = IsArc ? Geometry.Arc(ArcCenter, ArcRadius, ArcFrom, ArcTo) : Geometry.FilletOpen(Points, Bend);
            if (Mirror)
                for (int i = 0; i < Path.Count; i++) Path[i] = new Vector2(-Path[i].x, Path[i].y);
            return this;
        }

        public RailDef Clone() => new RailDef
        {
            Kind = Kind, Width = Width, Points = Points == null ? null : new List<Vector2>(Points), Bend = Bend, IsArc = IsArc,
            ArcCenter = ArcCenter, ArcRadius = ArcRadius, ArcFrom = ArcFrom, ArcTo = ArcTo, Mirror = Mirror,
        }.Rebuild();
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
    // Every outline remembers its recipe (OUT-12): a kind with its numbers, then a shift, then a left-right flip. The
    // level data stores the recipe, so a room rebuilt from data has exactly the outline it was authored with.
    public class ShapeDef
    {
        public const string RectKind = "rect", StadiumKind = "stadium", ChamferedKind = "chamfered", CircleKind = "circle",
            FigureEightKind = "figure8", PolygonKind = "polygon", OutlineKind = "outline";
        public static readonly string[] Kinds = { RectKind, StadiumKind, ChamferedKind, CircleKind, FigureEightKind, PolygonKind, OutlineKind };

        public readonly List<Vector2> Outline;
        public string Kind { get; private set; } = OutlineKind;
        public float[] Args { get; private set; } = new float[0];
        public Vector2 Offset { get; private set; }
        public bool Flipped { get; private set; }

        ShapeDef(List<Vector2> outline) => Outline = Geometry.CounterClockwise(outline);

        ShapeDef Recipe(string kind, params float[] args)
        {
            Kind = kind;
            Args = args;
            return this;
        }

        public static ShapeDef Rect(float w, float h, float corner = 0.7f) =>
            Corners(corner, -w / 2f, -h / 2f, w / 2f, -h / 2f, w / 2f, h / 2f, -w / 2f, h / 2f).Recipe(RectKind, w, h, corner);

        // Rectangle with fully round ends.
        public static ShapeDef Stadium(float w, float h) => Rect(w, h, Mathf.Min(w, h) / 2f).Recipe(StadiumKind, w, h);

        // Rectangle with its corners cut at 45 degrees (then lightly rounded): a frosty octagon.
        public static ShapeDef Chamfered(float w, float h, float cut, float corner = 0.5f) =>
            Corners(corner, -w / 2f + cut, -h / 2f, w / 2f - cut, -h / 2f, w / 2f, -h / 2f + cut, w / 2f, h / 2f - cut,
                w / 2f - cut, h / 2f, -w / 2f + cut, h / 2f, -w / 2f, h / 2f - cut, -w / 2f, -h / 2f + cut).Recipe(ChamferedKind, w, h, cut, corner);

        public static ShapeDef Circle(float radius) => new ShapeDef(Geometry.Circle(Vector2.zero, radius, 96)).Recipe(CircleKind, radius);

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
            return new ShapeDef(Geometry.Fillet(Geometry.CounterClockwise(pts), 0.8f, 20f)).Recipe(FigureEightKind, radius, halfSpacing, waist);
        }

        // Corner points as x, y pairs. Every corner gets rounded with the given radius.
        public static ShapeDef Polygon(float corner, params float[] xy)
        {
            var args = new float[xy.Length + 1];
            args[0] = corner;
            System.Array.Copy(xy, 0, args, 1, xy.Length);
            return Corners(corner, xy).Recipe(PolygonKind, args);
        }

        // The exact points, unrounded: for outlines no recipe describes.
        public static ShapeDef Points(params float[] xy)
        {
            var pts = new List<Vector2>();
            for (int i = 0; i + 1 < xy.Length; i += 2) pts.Add(new Vector2(xy[i], xy[i + 1]));
            return new ShapeDef(pts).Recipe(OutlineKind, xy);
        }

        static ShapeDef Corners(float corner, params float[] xy)
        {
            var pts = new List<Vector2>();
            for (int i = 0; i + 1 < xy.Length; i += 2) pts.Add(new Vector2(xy[i], xy[i + 1]));
            return new ShapeDef(Geometry.Fillet(Geometry.CounterClockwise(pts), corner));
        }

        // Rebuilds an outline from its recipe. Unknown kinds or short argument lists fall back to the default board.
        public static ShapeDef Make(string kind, float[] a, Vector2 offset, bool flipped)
        {
            a = a ?? new float[0];
            float A(int i, float fallback) => i < a.Length ? a[i] : fallback;
            ShapeDef s;
            switch (kind)
            {
                case RectKind: s = Rect(A(0, 9f), A(1, 16f), A(2, 0.7f)); break;
                case StadiumKind: s = Stadium(A(0, 9f), A(1, 16f)); break;
                case ChamferedKind: s = Chamfered(A(0, 9f), A(1, 16f), A(2, 1.6f), A(3, 0.5f)); break;
                case CircleKind: s = Circle(A(0, 7.2f)); break;
                case FigureEightKind: s = FigureEight(A(0, 6f), A(1, 7.4f), A(2, 3.2f)); break;
                case PolygonKind when a.Length >= 7: s = Polygon(a[0], Tail(a)); break;
                case OutlineKind when a.Length >= 6: s = Points(a); break;
                default: s = Rect(9f, 16f); break;
            }
            if (offset != Vector2.zero) s = s.Shift(offset.x, offset.y);
            return flipped ? s.Mirrored() : s;
        }

        static float[] Tail(float[] a)
        {
            var t = new float[a.Length - 1];
            System.Array.Copy(a, 1, t, 0, t.Length);
            return t;
        }

        // The same outline flipped left to right (OUT-46).
        public ShapeDef Mirrored()
        {
            var pts = new List<Vector2>();
            foreach (var p in Outline) pts.Add(new Vector2(-p.x, p.y));
            return new ShapeDef(pts) { Kind = Kind, Args = Args, Offset = Offset, Flipped = !Flipped };
        }

        // The same outline moved by an offset (taller boards keep their top at y = 8 and grow downward).
        public ShapeDef Shift(float dx, float dy)
        {
            var pts = new List<Vector2>();
            foreach (var p in Outline) pts.Add(p + new Vector2(dx, dy));
            // The recipe shifts before it flips, so a shift after a flip is stored flipped back.
            var d = Flipped ? new Vector2(-dx, dy) : new Vector2(dx, dy);
            return new ShapeDef(pts) { Kind = Kind, Args = Args, Offset = Offset + d, Flipped = Flipped };
        }
    }

    // How hard a room plays, marked in the level data (OUT-12) for the saga map and the difficulty curve.
    public enum Difficulty { Normal, Hard, SuperHard }

    public class LevelDef
    {
        public string Id;               // stable save key; never rename once shipped (the shipped-ids check enforces it)
        public string Name;
        public Vector2 Hole;
        public int Par = 1;             // shots for the mastery star
        public string Hint = "";
        public Difficulty Difficulty;
        public bool Boss;               // the chapter's closing room
        public string Teaches = "";     // the gadget key this room introduces, if it is a teaching room
        public readonly List<string> Tags = new List<string>();  // designer labels, e.g. "remix:rubber"
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

        // A room flipped left to right (OUT-46): the same physics mirrored, so it plays exactly as well as the original,
        // in a new place with a new name. Zone directions, spins and swirls flip with it; the receptionist stays behind.
        public static LevelDef Mirror(LevelDef src, string id, string name, string hint)
        {
            var m = new LevelDef(id, name, Flip(src.Hole.x), src.Hole.y) { Par = src.Par, Hint = hint, Start = new Vector2(Flip(src.Start.x), src.Start.y), Shape = src.Shape.Mirrored() };
            m.maxShots = src.maxShots;
            foreach (var b in src.Blocks)
            {
                if (b.Reception != 0) continue;
                m.Blocks.Add(new BlockDef(b.Kind, new Vector2(Flip(b.Center.x), b.Center.y), b.Size)
                {
                    Shape = b.Shape, Corner = b.Corner, Angle = Flip(b.Angle), MoveOffset = new Vector2(Flip(b.MoveOffset.x), b.MoveOffset.y), Period = b.Period,
                    Phase = b.Phase, Spin = Flip(b.Spin), Group = b.Group, BlinkPeriod = b.BlinkPeriod, BlinkPhase = b.BlinkPhase,
                });
            }
            foreach (var z in src.Zones)
                m.Zones.Add(new ZoneDef
                {
                    Kind = z.Kind, Center = new Vector2(Flip(z.Center.x), z.Center.y), Size = z.Size, Angle = Flip(z.Angle), Link = z.Link, Group = z.Group,
                    Turn = Flip(z.Turn), Period = z.Period, Phase = z.Phase,
                });
            foreach (var r in src.Rails)
            {
                var flipped = r.Clone();
                flipped.Mirror = !r.Mirror;
                m.Rails.Add(flipped.Rebuild());
            }
            foreach (var s in src.Stars) m.Stars.Add(new Vector2(Flip(s.x), s.y));
            foreach (var br in src.Bridges) m.Bridges.Add(new Rect(Flip(br.xMax), br.yMin, br.width, br.height));
            return m;
        }

        // Negates without making a negative zero, which the level data would not keep (OUT-12).
        static float Flip(float v) => v == 0f ? 0f : -v;

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
        public int MaxShotsOverride => maxShots;  // 0 when the level uses par plus the slack
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
            Rails.Add(RailDef.Through(kind, bend, pts));
            return this;
        }

        // A curved rod along a circle. Angles in degrees, counter-clockwise from +X.
        public LevelDef Arc(BlockKind kind, float cx, float cy, float radius, float fromDeg, float toDeg)
        {
            Rails.Add(RailDef.Around(kind, new Vector2(cx, cy), radius, fromDeg, toDeg));
            return this;
        }

        public LevelDef Boost(float x, float y, float w, float length, float angle = 0f) => AddZone(ZoneKind.Boost, x, y, w, length, angle);
        public LevelDef Mud(float x, float y, float w, float h) => AddZone(ZoneKind.Mud, x, y, w, h, 0f);
        public LevelDef Conveyor(float x, float y, float w, float length, float angle = 0f) => AddZone(ZoneKind.Conveyor, x, y, w, length, angle);
        public LevelDef Ice(float x, float y, float w, float h) => AddZone(ZoneKind.Ice, x, y, w, h, 0f);
        public LevelDef Gap(float x, float y, float w, float h) => AddZone(ZoneKind.Gap, x, y, w, h, 0f);
        public LevelDef Ramp(float x, float y, float w, float length, float angle = 0f) => AddZone(ZoneKind.Ramp, x, y, w, length, angle);
        public LevelDef Wind(float x, float y, float w, float length, float angle = 0f) => AddZone(ZoneKind.Wind, x, y, w, length, angle);

        // A spinning platform of the given reach, turning counter-clockwise or clockwise.
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

    // One chapter of the saga (OUT-12): a theme and its rooms, in order. Lives in Resources/Levels/catalog.json.
    public class ChapterDef
    {
        public string Key;      // stable id, e.g. "open-plan"
        public string ThemeKey;
        public Theme Theme;
        public readonly List<string> LevelIds = new List<string>();
    }

    // The default field is 9 x 16: x in [-4.5, 4.5], y in [-8, 8]. The puck starts at (0, -6) unless a level sets Start.
    // The game's rooms are data (OUT-12): Resources/Levels/catalog.json orders the chapters and their rooms, and each
    // room is Resources/Levels/Rooms/<id>.json. Build and test them in Out of Office > Level Editor; Out of Office >
    // Check Levels proves every one can be finished and suggests par.
    public static class Levels
    {
        public const string CatalogPath = "Levels/catalog";
        public const string RoomsPath = "Levels/Rooms/";

        static LevelDef[] all;
        static List<ChapterDef> chapters;
        static int[] chapterStart;
        static Dictionary<string, int> index;

        // Every room in play order.
        public static LevelDef[] All { get { Load(); return all; } }
        public static List<ChapterDef> Chapters { get { Load(); return chapters; } }
        // First level index of each chapter, plus the level count at the end.
        public static int[] ChapterStart { get { Load(); return chapterStart; } }

        // Drops the loaded rooms so the next use reads the data again (the level editor after a save, Play mode entry).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reload() => all = null;

        static void Load()
        {
            if (all != null) return;
            var catalog = Resources.Load<TextAsset>(CatalogPath);
            var loaded = catalog == null ? new LevelCatalog() : LevelData.ParseCatalog(catalog.text, LoadRoom);
            Use(loaded);
        }

        static LevelDef LoadRoom(string id)
        {
            var file = Resources.Load<TextAsset>(RoomsPath + id);
            if (file == null)
            {
                Debug.LogError($"Out of Office: room '{id}' is in the catalog but Resources/{RoomsPath}{id}.json is missing.");
                return null;
            }
            return LevelData.ParseLevel(file.text);
        }

        // Puts a catalog in play: the shipped one, or (later) one delivered by live ops.
        public static void Use(LevelCatalog catalog)
        {
            chapters = catalog.Chapters;
            var list = new List<LevelDef>();
            chapterStart = new int[chapters.Count + 1];
            for (int c = 0; c < chapters.Count; c++)
            {
                chapterStart[c] = list.Count;
                foreach (var id in chapters[c].LevelIds)
                    if (catalog.Rooms.TryGetValue(id, out var def) && def != null) list.Add(def);
            }
            chapterStart[chapters.Count] = list.Count;
            index = new Dictionary<string, int>();
            for (int i = 0; i < list.Count; i++) index[list[i].Id] = i;
            all = list.ToArray();
        }

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

        // The orientation room (OUT-52): a new player's first room, before the hundred and not one of them. Three shots
        // teach the game: straight up to a coffee, through the gap at the end of the desk for the next, then into bed.
        public static readonly LevelDef Tutorial =
            new LevelDef("orientation", "Orientation Day", -2.2f, 5.2f).WithHint("DRAG BACK, AIM, RELEASE").WithPar(3)
                .StartAt(0f, -5.6f)
                .Wall(-1.1f, 1.4f, 6.8f, 0.6f)                  // a long desk, open at the right-hand end
                .Post(BlockKind.Bouncer, 3.3f, 5.4f, 0.45f)     // a chair in the far corner to bounce off
                .Star(0f, -2.4f).Star(3.2f, 1.4f).Star(0.6f, 4.4f);

        // The game's rooms: the first 64, then the three new floors (OUT-46), rooms 65 to 100.
        public static int IndexOf(string id)
        {
            Load();
            return id != null && index.TryGetValue(id, out int i) ? i : -1;
        }
    }
}
