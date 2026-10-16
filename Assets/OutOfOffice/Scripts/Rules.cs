using UnityEngine;

namespace OutOfOffice
{
    // How a surface or obstacle behaves. Physics only: sound and effects read the kind, not these numbers.
    public struct SurfaceTuning
    {
        public float Drag;   // speed-proportional resistance (1/s)
        public float Decel;  // constant rolling resistance (units/s^2); lets the puck settle in finite time

        public SurfaceTuning(float drag, float decel)
        {
            Drag = drag;
            Decel = decel;
        }
    }

    // Central tuning for physics, input, capture, and feedback. The game and the editor checks share these.
    public static class Rules
    {
        public const float Dt = 1f / 60f;           // fixed simulation step; rendering interpolates between steps

        public const float WallThickness = 0.7f;    // outer rim around the map shape (visual only; physics uses the outline)

        public const float PuckRadius = 0.35f;
        public const float HoleRadius = 0.6f;

        // 3D look only. Physics stays flat in the XY plane.
        public const float WallHeight = 0.6f;
        public const float HazardHeight = 0.45f;
        public const float BouncerHeight = 0.5f;
        public const float PuckHeight = 0.28f;
        public const float HoleDepth = 1f;
        public const float StickyHeight = 0.5f;
        public const float BaseDepth = 0.9f;

        // ---------- Launch ----------
        public const float MaxSpeed = 28f;          // launch speed at full drag
        public const float PowerCurve = 1.6f;       // >1 gives finer control on soft shots
        public const float MaxDrag = 3f;            // board units of pull for full power
        public const float MinPower = 0.08f;        // pulling back inside this dead zone cancels the shot

        // ---------- Surfaces ----------
        public static readonly SurfaceTuning Wood = new SurfaceTuning(0.55f, 1.8f);
        public static readonly SurfaceTuning Ice = new SurfaceTuning(0.12f, 0.35f);
        public static readonly SurfaceTuning Mud = new SurfaceTuning(4f, 6f);

        // ---------- Obstacle materials (restitution, contact friction) ----------
        // The puck's own material has bounciness 0 and friction 1, so these values decide each contact:
        // Box2D takes the larger restitution and sqrt(friction1 * friction2).
        public const float WoodBounce = 0.62f, WoodFriction = 0.001f;       // walls and rim: modest rebound
        public const float RubberBounce = 0.92f, RubberFriction = 0.01f;    // bumpers: lively, never above 1
        public const float SoftBounce = 0.15f, SoftFriction = 0.06f;        // jelly: soaks up energy
        public const float CrateBounce = 0.25f, CrateFriction = 0.02f;
        public const float CrystalBounce = 0.45f, CrystalFriction = 0.005f;  // breakable blocks below the break speed
        public const float BounceThreshold = 1.2f;  // normal speeds below this do not bounce (Box2D setting)

        // Glancing contacts on wood: when the normal speed is small and shallow compared with the tangential speed,
        // the contact does not rebound. Grazes keep their momentum and concave guides carry the puck smoothly.
        // Rubber, soft, and crate contacts always use their restitution.
        public const float GlideRatio = 0.42f;      // normal / tangential speed below which a contact counts as glancing
        public const float RideNormalMax = 4.5f;    // ...and only while the normal speed is below this
        // Separation (Physics2D.Distance) that Box2D itself settles a resting contact at: its linear slop, measured
        // as -0.005. The glancing rule may close a rebound gap down to this, never deeper, so it never forces penetration.
        public const float ContactRestGap = -0.005f;

        // ---------- Rest ----------
        public const float RestSpeed = 0.08f;       // below this speed the puck counts as settling
        public const float RestTime = 0.12f;        // ...for this long, then it is at rest
        public const float SafetyShotTime = 15f;    // last-resort guard only; normal shots settle long before
        public const float StuckDistance = 0.06f;   // moving less than this...
        public const float StuckTime = 0.5f;        // ...for this long counts as resting, even while pushed

        // ---------- Hole capture ----------
        // The puck only feels the hole once its center is over the opening (no pull from outside the rim).
        public const float HoleTilt = 9f;           // inward acceleration at the rim edge while the center is over the opening
        // Landing on most of the bed counts at any speed: a full-power shot through the middle is a win, not a
        // bounce (OUT-23). Outside that core the allowed speed falls off quadratically, so fast grazes still lip out.
        public const float CaptureCore = 0.5f;        // fraction of the hole radius: inside it, any speed drops in
        public const float CaptureSpeedCore = 32f;    // allowed speed at the core's edge (above any launch or boost speed)
        public const float CaptureSpeedEdge = 4.5f;   // max speed that still drops in at the capture edge
        public const float CaptureEdge = 0.82f;     // fraction of the hole radius the center must reach to drop

        // ---------- Zones ----------
        public const float BoostAccel = 70f;        // boost pad push along its arrow
        public const float BoostSteer = 6f;         // how fast a boost pad turns the puck toward its arrow
        public const float BoostMaxSpeed = 30f;
        public const float RampHeight = 0.45f;
        public const float RampGravity = 9f;        // pulls the puck back down the slope
        public const float RampTakeoff = 0.85f;     // fraction of the ramp length where the puck leaves the ground
        // The coffee machine pulls across its whole ring, always well above floor friction, and hardest at the machine
        // (OUT-49): a slow roll anywhere in the ring is drawn in, and passing shots slingshot round it.
        public const float MagnetStrength = 18f;    // pull at the machine (units/s^2)
        public const float MagnetEdge = 0.4f;       // share of that pull left at the edge of the ring
        public const int MaxTeleportsPerShot = 6;
        public const float ConveyorSpeed = 4.5f;    // belt surface speed
        public const float ConveyorGrip = 3f;       // how fast the belt brings the puck to its speed (1/s)

        // ---------- Breakable blocks ----------
        public const float WindAccel = 15f;         // fan push along its direction (units/s^2)
        // The spinning platform (ZoneKind.Vortex, OUT-49) turns a shot's heading the way it spins, carries the puck
        // round and flings it off the rim: nothing ever parks on it.
        public const float SpinTurn = 2.2f;         // how fast it turns the puck's heading at the hub (rad/s)
        public const float SpinTurnRim = 0.4f;      // share of that turn left at the rim
        public const float SpinCarry = 6f;          // push along the spin (units/s^2)
        public const float SpinFling = 7f;          // push outward, off the rim
        public const float SpinRate = 2.2f;         // how fast the platform itself turns (rad/s), for the look
        public const float KickSpeed = 14f;         // a kicker fires the puck away at least this fast
        public const float JumpMinSpeed = 7f;       // a trampoline launches at least this fast
        public const float SpikeDuty = 0.5f;        // share of each period that pop-up spikes stay up
        public const float SpikeWarn = 0.45f;       // seconds of warning wobble before they rise
        public const float BreakSpeed = 5f;         // normal impact speed that shatters a crystal block
        public const float BreakKeep = 0.6f;        // share of the puck's velocity kept when it bursts through
        public const float PortalCooldown = 0.2f;

        // ---------- Movers ----------
        public const float SpinnerSpeed = 70f;      // default degrees per second for spinners

        // ---------- Aim preview ----------
        public const float PreviewLength = 6.5f;    // world units of dashed preview, including one bounce
        public const float PreviewDot = 0.36f;

        // ---------- Feedback (cosmetic only) ----------
        public const float ImpactMinSpeed = 0.6f;   // normal speed below this is a graze: rub audio, no impact
        public const float ImpactCooldown = 0.05f;
        public const float RubMinSpeed = 1.2f;      // tangential speed for the sustained-contact sound
        public const float ShakeImpactSpeed = 12f;  // only impacts harder than this shake the camera
        public const float TrailMinSpeed = 3f;
        public const float StarReach = 0.95f;       // a star is collected when Puffy's center passes this close

        // ---------- Crates ----------
        public const float CrateMass = 2.5f;
        public const float CrateDrag = 2.6f;
        public const float CrateAngularDrag = 4f;
    }

    // Toy-box palette: warm wood with soft pastel paint.
    public static class Palette
    {
        public static readonly Color SkyTop = Hex(0xFFE9D6);
        public static readonly Color SkyBottom = Hex(0xFFD9C7);
        public static readonly Color Field = Hex(0xFFFFFF);       // tints the wood floor texture
        public static readonly Color Frame = Hex(0xFFFFFF);       // tints the wood rim texture
        public static readonly Color Mat = Hex(0xFFFFFF);         // tints the play mat
        public static readonly Color Wall = Hex(0x7DB8F7);        // sky blue paint
        public static readonly Color Hazard = Hex(0xFF7A7A);      // coral red
        public static readonly Color Bouncer = Hex(0x63DCA0);     // mint
        public static readonly Color Sticky = Hex(0xC7A1F5);      // lilac jelly
        public static readonly Color Boost = Hex(0xFFB547);
        public static readonly Color BoostBase = Hex(0xFFE27A);
        public static readonly Color Mud = Hex(0xD6A24A);         // a thick shag rug
        public static readonly Color Ramp = Hex(0xFFD45C);
        public static readonly Color RampArrow = Hex(0x5AA2F2);
        public static readonly Color PortalInner = Hex(0x3A2E6E);
        public static readonly Color[] Portal = { Hex(0x4FD8FF), Hex(0xB57BFF) };
        public static readonly Color HoleInside = Hex(0x2A1E16);
        public static readonly Color HoleRim = Hex(0xFFC83D);     // gold
        public static readonly Color StartMark = Hex(0xE9C79C);
        public static readonly Color Puck = Hex(0x26232B);        // glossy black
        public static readonly Color PuckTop = Hex(0xFFD23C);     // yellow ring
        public static readonly Color Gold = Hex(0xFFC83D);
        public static readonly Color Tree = Hex(0x7BCF6E);
        public static readonly Color Trunk = Hex(0xB07B4F);
        public static readonly Color[] ToyColors = { Hex(0xFF8A80), Hex(0x7DB8F7), Hex(0xFFD45C), Hex(0x8CE0B0), Hex(0xC7A1F5), Hex(0xFFB38A) };
        public static readonly Color Text = Hex(0x5A3A22);        // dark wood brown for outlines
        public static readonly Color UiWood = Hex(0xF2B36B);
        public static readonly Color UiBlue = Hex(0x4C9BF5);
        public static readonly Color UiRed = Hex(0xFF5F57);
        public static readonly Color UiGreen = Hex(0x4CD964);
        public static readonly Color UiCream = Hex(0xFFF4E3);
        public static readonly Color UiPurple = Hex(0x8E6CF0);

        // Lacquered toy blocks cycle through these; rods are natural lacquered wood; bumpers are soft blue rubber.
        public static readonly Color[] WallColors = { Hex(0xFFC64D), Hex(0xFF8A5C), Hex(0x8E7CF2), Hex(0x4FC3B4) };
        public static readonly Color Rod = Hex(0xFFE2B8);
        public static readonly Color Rubber = Hex(0x4C8DF6);
        public static readonly Color RubberCap = Hex(0xBFD8FF);
        public static readonly Color Ice = Hex(0xCDEBFF);
        public static readonly Color Kicker = Hex(0xFF5E57);
        public static readonly Color PopWall = Hex(0x26C6DA);
        public static readonly Color Blanket = Hex(0xF08A6C);
        public static readonly Color Trampoline = Hex(0x7C4DFF);
        public static readonly Color OneWay = Hex(0x43D17A);
        public static readonly Color Wind = Hex(0x8FD8FF);
        public static readonly Color Water = Hex(0x8A5A3B);       // spilled coffee
        public static readonly Color Spin = Hex(0x1FB2C2);        // spinning platform stripes
        public static readonly Color SpinLight = Hex(0xFFF4DE);   // ...and the deck between them
        public static readonly Color Gap = Hex(0x2B1A10);
        public static readonly Color[] GateColors = { Hex(0x35C2F2), Hex(0xFF9F1C), Hex(0xFF5E9A) };
        public static readonly Color Magnet = Hex(0xC07A45);      // the coffee machine's pull
        public static readonly Color Metal = Hex(0xD9DEE6);
        public static readonly Color Crate = Hex(0xC79A68);  // cardboard

        public static Color For(BlockKind kind)
        {
            switch (kind)
            {
                case BlockKind.Hazard: return Hazard;
                case BlockKind.Bouncer: return Rubber;
                case BlockKind.Sticky: return Sticky;
                case BlockKind.Gate: return GateColors[0];
                case BlockKind.Crate: return Hex(0xD9A066);
                case BlockKind.Kicker: return Kicker;
                case BlockKind.PopWall: return PopWall;
                default: return Wall;
            }
        }

        static Color Hex(int rgb) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
    }
}
