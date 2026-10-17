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
    public class RailDef
    {
        public BlockKind Kind;
        public List<Vector2> Path;
        public float Width = 0.5f;

        // The recipe Path is made from, which level files store (OUT-12): either corner points bent with Bend, or an
        // arc (center x, y, radius, from and to degrees). Flipped mirrors the result left to right (OUT-46).
        public List<Vector2> Points;
        public float Bend;
        public float[] ArcArgs;
        public bool Flipped;

        public bool IsArc => ArcArgs != null;

        public static RailDef Bent(BlockKind kind, float bend, List<Vector2> points, bool flipped = false, float width = 0.5f) =>
            new RailDef { Kind = kind, Bend = bend, Points = points, Flipped = flipped, Width = width }.Rebuild();

        public static RailDef Curve(BlockKind kind, float cx, float cy, float radius, float fromDeg, float toDeg, bool flipped = false, float width = 0.5f) =>
            new RailDef { Kind = kind, ArcArgs = new[] { cx, cy, radius, fromDeg, toDeg }, Flipped = flipped, Width = width }.Rebuild();

        // Remakes Path from the recipe, after an edit or a load.
        public RailDef Rebuild()
        {
            Path = IsArc ? Geometry.Arc(new Vector2(ArcArgs[0], ArcArgs[1]), ArcArgs[2], ArcArgs[3], ArcArgs[4]) : Geometry.FilletOpen(Points, Bend);
            if (Flipped) for (int i = 0; i < Path.Count; i++) Path[i] = new Vector2(-Path[i].x, Path[i].y);
            return this;
        }

        // The same rail flipped left to right.
        public RailDef Mirrored()
        {
            var m = new RailDef { Kind = Kind, Width = Width, Points = Points, Bend = Bend, ArcArgs = ArcArgs, Flipped = !Flipped, Path = new List<Vector2>(Path.Count) };
            foreach (var p in Path) m.Path.Add(new Vector2(-p.x, p.y));
            return m;
        }
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

    // How a board outline is made. Level files store the recipe, not the hundreds of filleted points (OUT-12).
    public enum ShapeKind { Rect, Chamfered, Circle, FigureEight, Polygon }

    // The playable floor outline. Outer walls follow it.
    public class ShapeDef
    {
        public readonly List<Vector2> Outline;

        // The recipe: Make(Kind, Args, Flipped, Offset) rebuilds exactly this outline. Rect: w, h, corner.
        // Chamfered: w, h, cut, corner. Circle: radius. FigureEight: radius, half spacing, waist. Polygon: corner, x0, y0, x1, y1...
        public readonly ShapeKind Kind;
        public readonly float[] Args;
        public readonly bool Flipped;     // mirrored left to right (OUT-46)
        public readonly Vector2 Offset;   // moved after any flip (taller boards keep their top at y = 8)

        ShapeDef(List<Vector2> outline, ShapeKind kind, float[] args, bool flipped, Vector2 offset)
        {
            Outline = Geometry.CounterClockwise(outline);
            Kind = kind;
            Args = args;
            Flipped = flipped;
            Offset = offset;
        }

        public static ShapeDef Make(ShapeKind kind, float[] args, bool flipped = false, Vector2 offset = default)
        {
            ShapeDef s;
            switch (kind)
            {
                case ShapeKind.Rect: s = Rect(args[0], args[1], args[2]); break;
                case ShapeKind.Chamfered: s = Chamfered(args[0], args[1], args[2], args[3]); break;
                case ShapeKind.Circle: s = Circle(args[0]); break;
                case ShapeKind.FigureEight: s = FigureEight(args[0], args[1], args[2]); break;
                default:
                    var xy = new float[args.Length - 1];
                    System.Array.Copy(args, 1, xy, 0, xy.Length);
                    s = Polygon(args[0], xy);
                    break;
            }
            if (flipped) s = s.Mirrored();
            if (offset.x != 0f || offset.y != 0f) s = s.Shift(offset.x, offset.y);
            return s;
        }

        public static ShapeDef Rect(float w, float h, float corner = 0.7f) =>
            new ShapeDef(Rounded(corner, -w / 2f, -h / 2f, w / 2f, -h / 2f, w / 2f, h / 2f, -w / 2f, h / 2f), ShapeKind.Rect, new[] { w, h, corner }, false, Vector2.zero);

        // Rectangle with fully round ends.
        public static ShapeDef Stadium(float w, float h) => Rect(w, h, Mathf.Min(w, h) / 2f);

        // Rectangle with its corners cut at 45 degrees (then lightly rounded): a frosty octagon.
        public static ShapeDef Chamfered(float w, float h, float cut, float corner = 0.5f) =>
            new ShapeDef(Rounded(corner, -w / 2f + cut, -h / 2f, w / 2f - cut, -h / 2f, w / 2f, -h / 2f + cut, w / 2f, h / 2f - cut,
                w / 2f - cut, h / 2f, -w / 2f + cut, h / 2f, -w / 2f, h / 2f - cut, -w / 2f, -h / 2f + cut), ShapeKind.Chamfered, new[] { w, h, cut, corner }, false, Vector2.zero);

        public static ShapeDef Circle(float radius) => new ShapeDef(Geometry.Circle(Vector2.zero, radius, 96), ShapeKind.Circle, new[] { radius }, false, Vector2.zero);

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
            return new ShapeDef(Geometry.Fillet(Geometry.CounterClockwise(pts), 0.8f, 20f), ShapeKind.FigureEight, new[] { radius, halfSpacing, waist }, false, Vector2.zero);
        }

        // The same outline flipped left to right (OUT-46).
        public ShapeDef Mirrored()
        {
            var pts = new List<Vector2>();
            foreach (var p in Outline) pts.Add(new Vector2(-p.x, p.y));
            return new ShapeDef(pts, Kind, Args, !Flipped, new Vector2(-Offset.x, Offset.y));
        }

        // The same outline moved by an offset (taller boards keep their top at y = 8 and grow downward).
        public ShapeDef Shift(float dx, float dy)
        {
            var pts = new List<Vector2>();
            foreach (var p in Outline) pts.Add(p + new Vector2(dx, dy));
            return new ShapeDef(pts, Kind, Args, Flipped, Offset + new Vector2(dx, dy));
        }

        // Corner points as x, y pairs. Every corner gets rounded with the given radius.
        public static ShapeDef Polygon(float corner, params float[] xy)
        {
            var args = new float[xy.Length + 1];
            args[0] = corner;
            System.Array.Copy(xy, 0, args, 1, xy.Length);
            return new ShapeDef(Rounded(corner, xy), ShapeKind.Polygon, args, false, Vector2.zero);
        }

        static List<Vector2> Rounded(float corner, params float[] xy)
        {
            var pts = new List<Vector2>();
            for (int i = 0; i + 1 < xy.Length; i += 2) pts.Add(new Vector2(xy[i], xy[i + 1]));
            return Geometry.Fillet(Geometry.CounterClockwise(pts), corner);
        }
    }

    public class LevelDef
    {
        public readonly string Id;      // stable save key; never rename once shipped
        public string Name;
        public Vector2 Hole;
        public int Par = 1;             // shots for the mastery star
        public string Hint = "";
        public Vector2 Start = new Vector2(0f, -6f);
        public ShapeDef Shape = ShapeDef.Rect(9f, 16f);
        public readonly List<BlockDef> Blocks = new List<BlockDef>();
        public readonly List<ZoneDef> Zones = new List<ZoneDef>();
        public readonly List<RailDef> Rails = new List<RailDef>();
        public readonly List<Vector2> Stars = new List<Vector2>();
        public readonly List<Rect> Bridges = new List<Rect>();  // plank decks over water: decoration only

        // Design data (OUT-12), for the editor, reports and later the saga map. None of it changes how a room plays.
        public int Difficulty;          // 1 (gentle) to 5 (brutal); 0 = not rated yet
        public bool IsBoss;
        public string Teaches = "";     // the gadget key (Gadgets.All) this room introduces, if any
        public readonly List<string> Tags = new List<string>();
        public int Chapter = -1;        // set by the level list from chapters.json; -1 for rooms outside the chapters

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
            var m = new LevelDef(id, name, -src.Hole.x, src.Hole.y) { Par = src.Par, Hint = hint, Start = new Vector2(-src.Start.x, src.Start.y), Shape = src.Shape.Mirrored() };
            m.maxShots = src.maxShots;
            foreach (var b in src.Blocks)
            {
                if (b.Reception != 0) continue;
                m.Blocks.Add(new BlockDef(b.Kind, new Vector2(-b.Center.x, b.Center.y), b.Size)
                {
                    Shape = b.Shape, Corner = b.Corner, Angle = -b.Angle, MoveOffset = new Vector2(-b.MoveOffset.x, b.MoveOffset.y), Period = b.Period,
                    Phase = b.Phase, Spin = -b.Spin, Group = b.Group, BlinkPeriod = b.BlinkPeriod, BlinkPhase = b.BlinkPhase,
                });
            }
            foreach (var z in src.Zones)
                m.Zones.Add(new ZoneDef
                {
                    Kind = z.Kind, Center = new Vector2(-z.Center.x, z.Center.y), Size = z.Size, Angle = -z.Angle, Link = z.Link, Group = z.Group,
                    Turn = -z.Turn, Period = z.Period, Phase = z.Phase,
                });
            foreach (var r in src.Rails) m.Rails.Add(r.Mirrored());
            foreach (var s in src.Stars) m.Stars.Add(new Vector2(-s.x, s.y));
            foreach (var br in src.Bridges) m.Bridges.Add(new Rect(-br.xMax, br.yMin, br.width, br.height));
            return m;
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
        public int MaxShotsOverride => maxShots;  // 0 when the level uses par + ShotSlack
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
            Rails.Add(RailDef.Bent(kind, bend, pts));
            return this;
        }

        // A curved rod along a circle. Angles in degrees, counter-clockwise from +X.
        public LevelDef Arc(BlockKind kind, float cx, float cy, float radius, float fromDeg, float toDeg)
        {
            Rails.Add(RailDef.Curve(kind, cx, cy, radius, fromDeg, toDeg));
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

    // A chapter of rooms, from chapters.json (OUT-12). Chapter i uses Themes.All[i].
    public class ChapterDef
    {
        public string Theme;
        public int First, End;   // level indices [First, End)
        public int Count => End - First;
    }

    // The game's rooms, loaded from the level files in Resources/Levels (OUT-12). The default field is 9 x 16:
    // x in [-4.5, 4.5], y in [-8, 8]. The puck starts at (0, -6) unless a level sets Start.
    // Edit rooms with Out of Office > Level Editor, and run Out of Office > Check Levels after an edit: it proves every
    // level can be finished and suggests par.
    public static class Levels
    {
        public const string Folder = "Levels";            // under Resources
        public const string ManifestName = "chapters";

        // Where level files come from. Resources by default; remote content (OUT-6) can swap in its own reader before
        // the first room is built. Takes a file name without extension, returns its text or null.
        public static System.Func<string, string> Source = ReadResource;

        static string ReadResource(string name)
        {
#if UNITY_EDITOR
            // Straight from disk in the editor, so a file the level editor just saved is read even before it imports.
            string path = $"Assets/OutOfOffice/Resources/{Folder}/{name}.json";
            if (System.IO.File.Exists(path)) return System.IO.File.ReadAllText(path);
#endif
            var asset = Resources.Load<TextAsset>(Folder + "/" + name);
            return asset == null ? null : asset.text;
        }

        public static LevelDef[] All { get; private set; }
        public static LevelDef Tutorial { get; private set; }
        public static ChapterDef[] Chapters { get; private set; }
        public static int[] ChapterStart { get; private set; }   // first level of each chapter, then the level count

        // Problems found while loading. A broken file never stops the game: the room is left out and logged, and
        // LevelDataCheck fails the build, so it can't ship.
        public static readonly List<string> Errors = new List<string>();

        static Levels() => Reload();

        // Reads every level file again (the level editor calls this after a save).
        public static void Reload()
        {
            Errors.Clear();
            var all = new List<LevelDef>();
            var chapters = new List<ChapterDef>();
            LevelData.Manifest manifest = null;
            string text = Source(ManifestName);
            if (text == null) Errors.Add($"Resources/{Folder}/{ManifestName}.json is missing");
            else
            {
                try { manifest = LevelData.ManifestFromJson(text); }
                catch (LevelData.FormatException e) { Errors.Add(e.Message); }
            }
            var seen = new HashSet<string>();
            if (manifest != null)
            {
                foreach (var entry in manifest.Chapters)
                {
                    var chapter = new ChapterDef { Theme = entry.Theme, First = all.Count };
                    foreach (string id in entry.Levels)
                    {
                        if (!seen.Add(id)) { Errors.Add($"{id} is listed twice in {ManifestName}.json"); continue; }
                        var def = Load(id);
                        if (def == null) continue;
                        def.Chapter = chapters.Count;
                        all.Add(def);
                    }
                    chapter.End = all.Count;
                    chapters.Add(chapter);
                }
                if (!string.IsNullOrEmpty(manifest.Tutorial)) Tutorial = Load(manifest.Tutorial);
            }
            // The game always has a first room, even with no level files, so a bad content push can't brick it.
            if (all.Count == 0) all.Add(Lab);
            if (chapters.Count == 0) chapters.Add(new ChapterDef { Theme = "", First = 0, End = all.Count });
            if (Tutorial == null) Tutorial = all[0];
            foreach (var e in Errors) Debug.LogError("Out of Office levels: " + e);

            All = all.ToArray();
            Chapters = chapters.ToArray();
            var starts = new int[Chapters.Length + 1];
            for (int c = 0; c < Chapters.Length; c++) starts[c] = Chapters[c].First;
            starts[Chapters.Length] = All.Length;
            ChapterStart = starts;
        }

        // One level file, or null (with the reason in Errors).
        public static LevelDef Load(string id)
        {
            string text = Source(id);
            if (text == null) { Errors.Add($"{id}: no file Resources/{Folder}/{id}.json"); return null; }
            try
            {
                var def = LevelData.FromJson(text);
                if (def.Id != id) { Errors.Add($"{id}.json holds the level \"{def.Id}\"; the file must be named after its id"); return null; }
                return def;
            }
            catch (LevelData.FormatException e)
            {
                Errors.Add($"{id}.json: {e.Message}");
                return null;
            }
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


        public static int IndexOf(string id)
        {
            for (int i = 0; i < All.Length; i++) if (All[i].Id == id) return i;
            return -1;
        }
    }
}
