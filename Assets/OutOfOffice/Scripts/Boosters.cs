using System;
using UnityEngine;

namespace OutOfOffice
{
    public enum BoosterKind { Laser, Overtime, CoffeeBreak, DoOver, MagnetBed }

    // Boosters (OUT-13): five office tools used during a try, each unlocked at a room with three to start. More cost
    // gems. They help a try, never the record: a Do-Over never makes a hole-in-one, and the level checker still solves
    // every room without any. The two that touch physics are deterministic: Coffee Break holds the movers' clock still,
    // and Magnet Bed's pull is one fixed function of position (BedPull), the same in the game and in its check.
    public class BoosterDef
    {
        public BoosterKind Kind;
        public string Key, Name, Text, Icon;
        public int UnlockRoom;  // the room (index) where it is introduced
    }

    public static class Boosters
    {
        public const int GemCost = 20, StartGift = 3, OvertimeShots = 3;
        public const int BoxEach = 2, BoxGems = 150;  // the shop's booster box: two of each unlocked one, at a discount
        public const float LaserReach = 1.6f;
        // Magnet Bed (OUT-51): well above floor friction across its whole reach, hardest at the bed.
        public const float MagnetRadius = 2.8f, MagnetPull = 16f, MagnetEdge = 0.45f;

        public static readonly BoosterDef[] All =
        {
            new BoosterDef { Kind = BoosterKind.Laser, Key = "laser", Name = "LASER POINTER", Icon = "laser", UnlockRoom = 7,
                Text = "Your aim line reaches 60% further for the rest of this try." },
            new BoosterDef { Kind = BoosterKind.Overtime, Key = "overtime", Name = "OVERTIME", Icon = "alarm", UnlockRoom = 11,
                Text = "+3 shots on this try. Nobody has to know." },
            new BoosterDef { Kind = BoosterKind.CoffeeBreak, Key = "coffeebreak", Name = "COFFEE BREAK", Icon = "coffee", UnlockRoom = 15,
                Text = "Everyone's on break: desks and turnstiles stop, pop-up walls and staples stay down, belts and vents switch off." },
            new BoosterDef { Kind = BoosterKind.DoOver, Key = "doover", Name = "DO-OVER", Icon = "mulligan", UnlockRoom = 21,
                Text = "Takes back your last shot, and it doesn't count. A do-over never makes a hole-in-one." },
            new BoosterDef { Kind = BoosterKind.MagnetBed, Key = "magnetbed", Name = "MAGNET BED", Icon = "magnet", UnlockRoom = 29,
                Text = "The bed pulls you in when you roll close, for the rest of this try." },
        };

        // Whether a Coffee Break changes anything in a room (OUT-51): something that moves, rises or blows. Elsewhere it
        // isn't offered, so it is never spent for nothing.
        public static bool BreakMatters(LevelDef d) =>
            d.Blocks.Exists(b => b.Moves || b.Kind == BlockKind.PopWall && b.BlinkPeriod > 0f) ||
            d.Zones.Exists(z => z.Kind == ZoneKind.Conveyor || z.Kind == ZoneKind.Wind || z.Kind == ZoneKind.Vortex ||
                                z.Kind == ZoneKind.Spikes && z.Period > 0f);

        public static BoosterDef Find(BoosterKind kind) => Array.Find(All, b => b.Kind == kind);
        public static BoosterDef Find(string key) => Array.Find(All, b => b.Key == key);

        // Magnet Bed: the speed change toward the bed for one step, strongest close in and nothing beyond the radius.
        public static Vector2 BedPull(Vector2 puck, Vector2 bed, float dt)
        {
            Vector2 to = bed - puck;
            float d = to.magnitude;
            if (d >= MagnetRadius || d < 1e-4f) return Vector2.zero;
            float k = 1f - d / MagnetRadius;
            return to / d * (MagnetPull * Mathf.Lerp(MagnetEdge, 1f, k) * dt);
        }
    }
}
