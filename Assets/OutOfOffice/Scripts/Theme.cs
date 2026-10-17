using UnityEngine;

namespace OutOfOffice
{
    // The office zones. Each has its own floor, walls, light, and one running joke.
    public enum Setting { OpenPlan, Kitchen, Meeting, Manager, HR, Executive, AfterHours }

    // How the walls of a zone look. The collider is the same in every style.
    public enum WallStyle { Partition, Counter, Wood, Cabinet, Painted }

    // One zone's look: floor material, wall style, lighting, UI accent, and its room sign.
    // Mechanic colors (rubber, hazard, boost, portal, gates) stay the same in every zone, so they read the same.
    public class Theme
    {
        public string Key;                // stable id the level catalog refers to (OUT-12); never rename once shipped
        public string Name;
        public string Sign;               // the room's one joke, shown on its sign
        public Setting Setting;
        public WallStyle Walls;
        public Color Sky;                 // camera background
        public Color Ground;              // the plain ground around the tray
        public Color[] WallColors;
        public Color Rod;
        public Color Frame = Color.white; // tints the rim texture
        public Color Shadow;              // soft contact shadows
        public Color Sun, Ambient;
        public float SunIntensity = 0.9f;
        public Color Accent, AccentDark;  // UI color of the zone
        public System.Func<Texture2D> Floor, FrameTexture, RodTexture;
        public float FloorGloss = 0.3f;   // smoothness of the board floor
        public bool DarkFloor;            // the aim guide switches to light dots on dark floors
    }

    public static class Themes
    {
        // Bright, flat, toy-like rooms: a pale two-tone floor, one bold wall color, a white rim, and a saturated
        // background. Every room is light, so the white aim dots and the pieces read the same everywhere.
        // Cozy miniature office: warm neutral floors and walls, one restrained corporate color per zone, and bright
        // accents kept for the things you interact with (chairs, cacti, coffee, the bed).
        public static readonly Theme[] All =
        {
            new Theme
            {
                Key = "open-plan", Name = "OPEN-PLAN OFFICE", Sign = "Everyone is very busy appearing busy.",
                Setting = Setting.OpenPlan, Walls = WallStyle.Partition,
                Sky = Hex(0xC9D3DF), Ground = Hex(0xC9D3DF),
                WallColors = new[] { Hex(0x6F8CB5), Hex(0x7B97BE) },
                Rod = Hex(0xEDEFF2), Frame = Hex(0xF4F2EE),
                Shadow = new Color(0.2f, 0.26f, 0.36f),
                Sun = new Color(1f, 0.97f, 0.92f), Ambient = new Color(0.72f, 0.74f, 0.78f), SunIntensity = 0.9f,
                Accent = Hex(0x4C7BF0), AccentDark = Hex(0x2B4FAE),
                Floor = () => Textures.OfficeFloor(Setting.OpenPlan), FloorGloss = 0.04f, FrameTexture = () => Textures.PaintedFrame, RodTexture = null,
            },
            new Theme
            {
                Key = "kitchen", Name = "OFFICE KITCHEN", Sign = "The coffee machine is the real department head.",
                Setting = Setting.Kitchen, Walls = WallStyle.Counter,
                Sky = Hex(0xE6D8C2), Ground = Hex(0xE6D8C2),
                WallColors = new[] { Hex(0x3E9E8A), Hex(0x48A895) },
                Rod = Hex(0xF5F1E8), Frame = Hex(0xF6F1E8),
                Shadow = new Color(0.36f, 0.28f, 0.18f),
                Sun = new Color(1f, 0.96f, 0.88f), Ambient = new Color(0.74f, 0.72f, 0.68f), SunIntensity = 0.9f,
                Accent = Hex(0x2FA88B), AccentDark = Hex(0x1C7362),
                Floor = () => Textures.OfficeFloor(Setting.Kitchen), FloorGloss = 0.35f, FrameTexture = () => Textures.PaintedFrame, RodTexture = null,
            },
            new Theme
            {
                Key = "meeting-room", Name = "MEETING ROOM", Sign = "A meeting to discuss why there are too many meetings.",
                Setting = Setting.Meeting, Walls = WallStyle.Wood,
                Sky = Hex(0xCFCBE0), Ground = Hex(0xCFCBE0),
                WallColors = new[] { Hex(0xA97852), Hex(0xB88660) },
                Rod = Hex(0xA97852), Frame = Hex(0xF1EEF4),
                Shadow = new Color(0.22f, 0.2f, 0.34f),
                Sun = new Color(1f, 0.97f, 0.92f), Ambient = new Color(0.7f, 0.7f, 0.76f), SunIntensity = 0.9f,
                Accent = Hex(0x7A62E6), AccentDark = Hex(0x4C3BA8),
                Floor = () => Textures.OfficeFloor(Setting.Meeting), FloorGloss = 0.04f, FrameTexture = () => Textures.PaintedFrame, RodTexture = () => Textures.WoodFrame, DarkFloor = true,
            },
            new Theme
            {
                Key = "manager", Name = "MANAGER'S OFFICE", Sign = "Urgent feedback that clarifies nothing.",
                Setting = Setting.Manager, Walls = WallStyle.Wood,
                Sky = Hex(0xE2D2BF), Ground = Hex(0xE2D2BF),
                WallColors = new[] { Hex(0x7E4E32), Hex(0x8A5A3C) },
                Rod = Hex(0x7E4E32), Frame = Hex(0xF2EAE0),
                Shadow = new Color(0.34f, 0.22f, 0.14f),
                Sun = new Color(1f, 0.95f, 0.86f), Ambient = new Color(0.74f, 0.7f, 0.64f), SunIntensity = 0.95f,
                Accent = Hex(0xD9822B), AccentDark = Hex(0x9C5714),
                Floor = () => Textures.OfficeFloor(Setting.Manager), FloorGloss = 0.3f, FrameTexture = () => Textures.PaintedFrame, RodTexture = () => Textures.WoodFrame,
            },
            new Theme
            {
                Key = "hr", Name = "HR & ADMIN", Sign = "Submit the form requesting permission to submit the form.",
                Setting = Setting.HR, Walls = WallStyle.Cabinet,
                Sky = Hex(0xD3DCCB), Ground = Hex(0xD3DCCB),
                WallColors = new[] { Hex(0x8E9DA6), Hex(0x9AA8B0) },
                Rod = Hex(0xB7C1C8), Frame = Hex(0xF0F0EA),
                Shadow = new Color(0.2f, 0.28f, 0.24f),
                Sun = new Color(1f, 0.98f, 0.94f), Ambient = new Color(0.72f, 0.74f, 0.72f), SunIntensity = 0.9f,
                Accent = Hex(0x5E9E4A), AccentDark = Hex(0x3C6E2C),
                Floor = () => Textures.OfficeFloor(Setting.HR), FloorGloss = 0.2f, FrameTexture = () => Textures.PaintedFrame, RodTexture = null,
            },
            new Theme
            {
                Key = "executive", Name = "EXECUTIVE FLOOR", Sign = "\"We are all in this together.\" Printed on a very expensive wall.",
                Setting = Setting.Executive, Walls = WallStyle.Painted,
                Sky = Hex(0xDCD5CB), Ground = Hex(0xDCD5CB),
                WallColors = new[] { Hex(0x2F3A56), Hex(0x384565) },
                Rod = Hex(0xC9A45C), Frame = Hex(0x2F3A56),
                Shadow = new Color(0.24f, 0.2f, 0.26f),
                Sun = new Color(1f, 0.96f, 0.9f), Ambient = new Color(0.7f, 0.68f, 0.7f), SunIntensity = 0.95f,
                Accent = Hex(0xC9A45C), AccentDark = Hex(0x8A6A2C),
                Floor = () => Textures.OfficeFloor(Setting.Executive), FloorGloss = 0.45f, FrameTexture = () => Textures.PaintedFrame, RodTexture = null,
            },
            new Theme
            {
                Key = "after-hours", Name = "AFTER HOURS", Sign = "The building has finally stopped asking questions.",
                Setting = Setting.AfterHours, Walls = WallStyle.Partition,
                Sky = Hex(0x262B45), Ground = Hex(0x262B45),
                WallColors = new[] { Hex(0x56638A), Hex(0x4D5980) },
                Rod = Hex(0x6A779E), Frame = Hex(0x3B4466),
                Shadow = new Color(0.05f, 0.06f, 0.14f),
                Sun = new Color(0.8f, 0.85f, 1f), Ambient = new Color(0.4f, 0.44f, 0.6f), SunIntensity = 0.8f,
                Accent = Hex(0x8C7CF0), AccentDark = Hex(0x5446B8),
                Floor = () => Textures.OfficeFloor(Setting.AfterHours), FloorGloss = 0.04f, FrameTexture = () => Textures.PaintedFrame, RodTexture = null, DarkFloor = true,
            },
            // Rooms 65-100 (OUT-46): three more floors, built from the office's own materials.
            new Theme
            {
                Key = "server-room", Name = "SERVER ROOM", Sign = "It works on my machine. The machine is on fire.",
                Setting = Setting.AfterHours, Walls = WallStyle.Cabinet,
                Sky = Hex(0x15303A), Ground = Hex(0x15303A),
                WallColors = new[] { Hex(0x3E5B66), Hex(0x466572) },
                Rod = Hex(0x7FD6E6), Frame = Hex(0x2A4652),
                Shadow = new Color(0.03f, 0.08f, 0.1f),
                Sun = new Color(0.78f, 0.95f, 1f), Ambient = new Color(0.36f, 0.5f, 0.56f), SunIntensity = 0.85f,
                Accent = Hex(0x2EC4D6), AccentDark = Hex(0x17808E),
                Floor = () => Textures.OfficeFloor(Setting.AfterHours), FloorGloss = 0.2f, FrameTexture = () => Textures.PaintedFrame, RodTexture = null, DarkFloor = true,
            },
            new Theme
            {
                Key = "rooftop", Name = "ROOFTOP TERRACE", Sign = "Team building, outdoors. Attendance is voluntary and mandatory.",
                Setting = Setting.Kitchen, Walls = WallStyle.Counter,
                Sky = Hex(0xBFE3F5), Ground = Hex(0xBFE3F5),
                WallColors = new[] { Hex(0xE07A3F), Hex(0xE88A50) },
                Rod = Hex(0xF7EFE3), Frame = Hex(0xF4ECE0),
                Shadow = new Color(0.26f, 0.3f, 0.4f),
                Sun = new Color(1f, 0.97f, 0.88f), Ambient = new Color(0.76f, 0.8f, 0.86f), SunIntensity = 1f,
                Accent = Hex(0xF08A3C), AccentDark = Hex(0xB0561C),
                Floor = () => Textures.OfficeFloor(Setting.Kitchen), FloorGloss = 0.15f, FrameTexture = () => Textures.PaintedFrame, RodTexture = null,
            },
            new Theme
            {
                Key = "corner-office", Name = "THE CORNER OFFICE", Sign = "The view is incredible. Nobody below this floor has seen it.",
                Setting = Setting.Executive, Walls = WallStyle.Painted,
                Sky = Hex(0x2B1A2E), Ground = Hex(0x2B1A2E),
                WallColors = new[] { Hex(0x5A1E2E), Hex(0x682538) },
                Rod = Hex(0xE2B857), Frame = Hex(0x5A1E2E),
                Shadow = new Color(0.12f, 0.05f, 0.1f),
                Sun = new Color(1f, 0.93f, 0.82f), Ambient = new Color(0.62f, 0.52f, 0.56f), SunIntensity = 0.95f,
                Accent = Hex(0xE2B857), AccentDark = Hex(0x9A7424),
                Floor = () => Textures.OfficeFloor(Setting.Executive), FloorGloss = 0.5f, FrameTexture = () => Textures.PaintedFrame, RodTexture = null,
            },
        };

        // Chapters come from the level catalog (OUT-12): each names its theme by Key and lists its rooms in order.
        // ChapterStart holds the first level index of each chapter, plus the level count at the end.
        public static int[] ChapterStart => Levels.ChapterStart;
        public static int ChapterCount => Levels.Chapters.Count;

        // The theme LevelBuilder uses. The game sets it before it builds a level.
        public static Theme Current = All[0];

        public static int ChapterOf(int levelIndex)
        {
            if (levelIndex < 0) return 0;
            var starts = ChapterStart;
            for (int c = starts.Length - 2; c >= 0; c--)
                if (levelIndex >= starts[c]) return c;
            return 0;
        }

        public static Theme ForChapter(int chapter) =>
            chapter >= 0 && chapter < ChapterCount ? Levels.Chapters[chapter].Theme : All[0];

        public static Theme ForLevel(int levelIndex) => ForChapter(ChapterOf(levelIndex));

        public static Theme ByKey(string key) => System.Array.Find(All, t => t.Key == key);

        public static Color Hex(int rgb) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
    }
}
