using System.Collections.Generic;
using UnityEngine;

namespace OutOfOffice
{
    public enum Rarity { Common, Rare, Epic, Legendary }
    public enum Slot { Employee, Color, Hat, Face }

    // Little things worn on the face, drawn as stickers on Puffy's face.
    public enum FaceItem { None, Freckles, Mustache, Glasses, BowTie, Sunglasses, StarShades, Monocle, HeartShades, Aviators, RoundSpecs, Handlebar, ClownNose, BandAid, VrGoggles }

    // Where an employee works (OUT-43), in the order the wardrobe lists them: from the interns up to the boss.
    public enum Department { Interns, Employees, Engineering, HR, Sales, Finance, Management, CLevel, Boss }

    // One wardrobe item. Items with StarsNeeded > 0 (or the starter set) unlock with stars; the rest come from chests.
    public class CosmeticItem
    {
        public string Id, Name;
        public Slot Slot;
        public Rarity Rarity;
        public int StarsNeeded = -1;     // -1: chest only
        public Color Body, Cheek, Extra; // colors: body and blush; Extra tints hats when the color is worn
        public Accessory Hat;
        public Color? HatTint;           // hats with their own colors; otherwise the body color's Extra
        public FaceItem Face;
        public Color SkinTone, HairColor;    // employees
        public HairStyle Hair;
        public PersonLook Extras;            // employees: tattoos, earrings, beard
        public Department Dept;              // employees: their department and job title (OUT-43)
        public string Title = "";
        public FaceDef Features;             // employees: their own face (OUT-42)
        public Color Jacket;                 // employees: blazer, cardigan, vest or lanyard colour

        public int GemCost;                  // > 0: hired with gems only (OUT-45), never from chests
        public bool ForGems => GemCost > 0;
        public bool FromChest => StarsNeeded < 0 && GemCost <= 0;
    }

    // What Puffy is wearing: one item per slot.
    // Every slot lookup goes through Get and With, so a new slot can't be missed by one of the callers.
    public struct Outfit
    {
        public string Employee, Color, Hat, Face;

        public string Get(Slot slot) =>
            slot == Slot.Employee ? Employee : slot == Slot.Color ? Color : slot == Slot.Hat ? Hat : Face;

        // A copy with one slot replaced.
        public Outfit With(Slot slot, string id)
        {
            var o = this;
            if (slot == Slot.Employee) o.Employee = id;
            else if (slot == Slot.Color) o.Color = id;
            else if (slot == Slot.Hat) o.Hat = id;
            else o.Face = id;
            return o;
        }

        public Outfit With(CosmeticItem item) => With(item.Slot, item.Id);
    }

    // The wardrobe catalog and the chest odds.
    public static class Cosmetics
    {
        public static readonly List<CosmeticItem> All = new List<CosmeticItem>();
        // Everyone starts as an intern (OUT-45) and works their way up.
        public static readonly Outfit Default = new Outfit { Employee = "e.pip", Color = "c.bluebell", Hat = "h.tuft", Face = "f.glasses" };

        // Tie names for the original twelve colors, by skin id (ids stay, so saves keep working).
        static readonly Dictionary<string, string> TieNames = new Dictionary<string, string>
        {
            { "puffy", "MUSTARD" }, { "peachy", "SALMON" }, { "minty", "MINT" }, { "bluebell", "CORPORATE BLUE" },
            { "grape", "PLUM" }, { "snowy", "CLEAN SLATE" }, { "tangerine", "QUARTER-END" }, { "cocoa", "DOUBLE ESPRESSO" },
            { "bubblegum", "CASUAL FRIDAY" }, { "lime", "SPREADSHEET" }, { "midnight", "NIGHT SHIFT" }, { "angel", "GOLDEN HANDSHAKE" },
        };

        static Cosmetics()
        {
            // The original twelve looks: each brings its color and its hat, unlocked by total stars as before.
            foreach (var s in Skins.All)
            {
                All.Add(new CosmeticItem { Id = "c." + s.Id, Name = TieNames.TryGetValue(s.Id, out var tie) ? tie : s.Name, Slot = Slot.Color, Rarity = RarityForStars(s.StarsNeeded), StarsNeeded = s.StarsNeeded,
                    Body = s.Body, Cheek = s.Cheek, Extra = s.Extra });
                All.Add(new CosmeticItem { Id = "h." + HatId(s.Accessory), Name = HatName(s.Accessory), Slot = Slot.Hat, Rarity = RarityForStars(s.StarsNeeded),
                    StarsNeeded = s.StarsNeeded, Hat = s.Accessory });
            }
            All.Add(new CosmeticItem { Id = "f.none", Name = "NOTHING", Slot = Slot.Face, Rarity = Rarity.Common, StarsNeeded = 0, Face = FaceItem.None });

            // The staff (OUT-43), by department: same body, same physics, different people having the same week. Each
            // has a face of their own (OUT-42). The first twelve keep their ids, unlocks and looks so saves keep working.
            var E = Department.Employees;
            Staff("ava", "AVA", E, "JUNIOR ASSOCIATE", 0, 0xF5D2BC, 0x5B3A29, HairStyle.Bob, F(EyeShape.Round, Iris.Brown, BrowStyle.Arched, NoseStyle.Soft, MouthStyle.Smile, FaceMarks.Rosy));
            Staff("leo", "LEO", E, "OFFICE ASSISTANT", 0, 0xE0B08A, 0x2B2420, HairStyle.Short, F(EyeShape.Almond, Iris.Hazel, BrowStyle.Thick, NoseStyle.Button, MouthStyle.Grin, FaceMarks.Dimples));
            Staff("mia", "MIA", Department.HR, "HR COORDINATOR", 8, 0x8D5A3B, 0x1E1714, HairStyle.Bun, F(EyeShape.Big, Iris.Dark, BrowStyle.Thin, NoseStyle.Soft, MouthStyle.Lipstick, FaceMarks.Lashes | FaceMarks.Mole), jacket: 0xF2A7B8);
            Staff("sam", "SAM", Department.Sales, "SALES REP", 16, 0xC98E64, 0xB4552A, HairStyle.Curly, F(EyeShape.Round, Iris.Green, BrowStyle.Arched, NoseStyle.Button, MouthStyle.Grin, FaceMarks.Freckles));
            // The extraordinary ones (OUT-30): bold hair, tattoos, earrings. Some unlock with stars, some only from chests.
            Staff("jax", "JAX", Department.Engineering, "DEVOPS ENGINEER", 24, 0xF2CDB4, 0x8A3FE0, HairStyle.Mohawk, F(EyeShape.Narrow, Iris.Grey, BrowStyle.Straight, NoseStyle.Pointy, MouthStyle.Smirk, FaceMarks.EyeBags), PersonLook.Tattoos | PersonLook.Earrings);
            Staff("nova", "NOVA", Department.Sales, "SOCIAL MEDIA LEAD", 32, 0xD9A27A, 0xFF5FA8, HairStyle.Long, F(EyeShape.Cat, Iris.Amber, BrowStyle.Thin, NoseStyle.Button, MouthStyle.Smirk, FaceMarks.Lashes), PersonLook.Earrings);
            Staff("kai", "KAI", Department.Engineering, "FRONTEND DEV", 40, 0xB5795A, 0x23C4B4, HairStyle.Spiky, F(EyeShape.Big, Iris.Blue, BrowStyle.Straight, NoseStyle.Soft, MouthStyle.Smile, FaceMarks.EyeBags | FaceMarks.Freckles), PersonLook.Tattoos);
            Staff("otto", "OTTO", Department.Finance, "SENIOR ACCOUNTANT", 48, 0xF0C7A8, 0xD9D9E3, HairStyle.Buzz, F(EyeShape.Droopy, Iris.Grey, BrowStyle.Bushy, NoseStyle.Round, MouthStyle.Polite, FaceMarks.Wrinkles), PersonLook.Beard);
            Staff("luna", "LUNA", Department.Engineering, "PRODUCT DESIGNER", -1, 0xF7DCCB, 0x4E7BFF, HairStyle.Long, F(EyeShape.Almond, Iris.Blue, BrowStyle.Arched, NoseStyle.Button, MouthStyle.Smile, FaceMarks.Lashes | FaceMarks.Freckles), PersonLook.Earrings, rarity: Rarity.Rare);
            Staff("ivy", "IVY", Department.HR, "WELLNESS OFFICER", -1, 0xE8B894, 0x5CC46A, HairStyle.Ponytail, F(EyeShape.Round, Iris.Green, BrowStyle.Thin, NoseStyle.Soft, MouthStyle.Grin, FaceMarks.Rosy | FaceMarks.Dimples), PersonLook.Tattoos, jacket: 0x9EDDC4, rarity: Rarity.Rare);
            Staff("zara", "ZARA", Department.Management, "MARKETING DIRECTOR", -1, 0x6E4430, 0x7A3FD0, HairStyle.Afro, F(EyeShape.Cat, Iris.Amber, BrowStyle.Thick, NoseStyle.Soft, MouthStyle.Lipstick, FaceMarks.Lashes | FaceMarks.Mole), PersonLook.Earrings, jacket: 0xC2367A, rarity: Rarity.Epic);
            Staff("rex", "REX", Department.CLevel, "CTO", -1, 0xD49A72, 0xF4F1E6, HairStyle.Spiky, F(EyeShape.Narrow, Iris.Blue, BrowStyle.Bushy, NoseStyle.Pointy, MouthStyle.Smirk, FaceMarks.Wrinkles), PersonLook.Tattoos | PersonLook.Earrings | PersonLook.Beard, rarity: Rarity.Legendary, gems: 600);

            // Interns: the fresh faces, from chests.
            var I = Department.Interns;
            Staff("pip", "PIP", I, "SUMMER INTERN", 0, 0xF6D7C3, 0xF2C94C, HairStyle.Pigtails, F(EyeShape.Big, Iris.Blue, BrowStyle.Thin, NoseStyle.Button, MouthStyle.Grin, FaceMarks.Freckles | FaceMarks.Rosy), jacket: 0xFF8A3D);
            Staff("theo", "THEO", I, "COFFEE INTERN", -1, 0xE6B897, 0x6B4226, HairStyle.Short, F(EyeShape.Round, Iris.Brown, BrowStyle.Arched, NoseStyle.Soft, MouthStyle.Polite, FaceMarks.EyeBags | FaceMarks.Dimples));
            Staff("bea", "BEA", I, "UNPAID INTERN", -1, 0xF3CDB0, 0xA0522D, HairStyle.Ponytail, F(EyeShape.Narrow, Iris.Hazel, BrowStyle.Straight, NoseStyle.Button, MouthStyle.Smile, FaceMarks.Freckles));
            Staff("milo", "MILO", I, "INTERN, AGAIN", -1, 0x7A4B33, 0x1A1412, HairStyle.Curly, F(EyeShape.Big, Iris.Dark, BrowStyle.Thick, NoseStyle.Round, MouthStyle.Grin));
            // More of the rank and file.
            Staff("rosa", "ROSA", E, "FRONT DESK", -1, 0xD9A07A, 0x1C1A1A, HairStyle.Bob, F(EyeShape.Almond, Iris.Dark, BrowStyle.Arched, NoseStyle.Soft, MouthStyle.Lipstick, FaceMarks.Mole | FaceMarks.Lashes), PersonLook.Earrings);
            Staff("dev", "DEV", E, "DATA ENTRY", -1, 0xA8714F, 0x141010, HairStyle.Short, F(EyeShape.Droopy, Iris.Dark, BrowStyle.Thick, NoseStyle.Long, MouthStyle.Polite, FaceMarks.EyeBags));
            Staff("gus", "GUS", E, "MAILROOM", -1, 0xF1C9A9, 0xC2502A, HairStyle.Buzz, F(EyeShape.Round, Iris.Green, BrowStyle.Bushy, NoseStyle.Round, MouthStyle.Grin, FaceMarks.Freckles | FaceMarks.Stubble));
            // Engineering: lanyards, badges, and eye bags from the last release.
            var Eng = Department.Engineering;
            Staff("neo", "NEO", Eng, "BACKEND DEV", 84, 0xE8BC98, 0x111111, HairStyle.Slick, F(EyeShape.Narrow, Iris.Dark, BrowStyle.Straight, NoseStyle.Pointy, MouthStyle.Smirk, FaceMarks.Stubble | FaceMarks.EyeBags), rarity: Rarity.Epic);
            Staff("ada", "ADA", Eng, "DATA SCIENTIST", -1, 0xF0CDB4, 0xE9E9F0, HairStyle.Bun, F(EyeShape.Almond, Iris.Grey, BrowStyle.Thin, NoseStyle.Soft, MouthStyle.Smile, FaceMarks.Wrinkles | FaceMarks.Lashes), PersonLook.Earrings, rarity: Rarity.Epic);
            Staff("byte", "BYTE", Eng, "QA TESTER", -1, 0xDDB08C, 0x4CD964, HairStyle.Spiky, F(EyeShape.Big, Iris.Amber, BrowStyle.Straight, NoseStyle.Button, MouthStyle.Grin, FaceMarks.Freckles), rarity: Rarity.Rare);
            Staff("ravi", "RAVI", Eng, "SECURITY ENGINEER", -1, 0x9E6A48, 0x161212, HairStyle.Short, F(EyeShape.Narrow, Iris.Dark, BrowStyle.Thick, NoseStyle.Long, MouthStyle.Polite, FaceMarks.Stubble), PersonLook.Beard, rarity: Rarity.Rare);
            // HR: cardigans and a firm, warm smile.
            Staff("gwen", "GWEN", Department.HR, "HEAD OF PEOPLE", 96, 0xF4D2C0, 0xCFCFD8, HairStyle.Bob, F(EyeShape.Droopy, Iris.Blue, BrowStyle.Arched, NoseStyle.Soft, MouthStyle.Smile, FaceMarks.Wrinkles | FaceMarks.Rosy), jacket: 0xB98ED8, rarity: Rarity.Epic);
            Staff("kim", "KIM", Department.HR, "RECRUITER", -1, 0xEBC7A6, 0x1B1B22, HairStyle.Long, F(EyeShape.Almond, Iris.Dark, BrowStyle.Thin, NoseStyle.Button, MouthStyle.Grin, FaceMarks.Lashes | FaceMarks.Dimples), PersonLook.Earrings, jacket: 0xFFC7A1, rarity: Rarity.Rare);
            // Sales and marketing: big smiles, bigger promises.
            Staff("chad", "CHAD", Department.Sales, "SALES CLOSER", 60, 0xF0C29C, 0xE8C25A, HairStyle.Slick, F(EyeShape.Round, Iris.Blue, BrowStyle.Thick, NoseStyle.Pointy, MouthStyle.Grin, FaceMarks.Dimples), rarity: Rarity.Rare);
            Staff("lola", "LOLA", Department.Sales, "BRAND MANAGER", -1, 0xF5D0B8, 0xD6402B, HairStyle.Long, F(EyeShape.Cat, Iris.Green, BrowStyle.Thin, NoseStyle.Button, MouthStyle.Lipstick, FaceMarks.Lashes | FaceMarks.Freckles), PersonLook.Hoops, rarity: Rarity.Rare);
            Staff("hugo", "HUGO", Department.Sales, "GROWTH HACKER", -1, 0xC98F69, 0xFF7A1A, HairStyle.Mohawk, F(EyeShape.Big, Iris.Hazel, BrowStyle.Straight, NoseStyle.Soft, MouthStyle.Smirk, FaceMarks.Stubble), PersonLook.Tattoos, rarity: Rarity.Rare);
            // Finance: sweater vests and a healthy fear of rounding errors.
            Staff("penny", "PENNY", Department.Finance, "PAYROLL", 72, 0xF7D8C5, 0x8B3A2E, HairStyle.Bun, F(EyeShape.Round, Iris.Hazel, BrowStyle.Thin, NoseStyle.Button, MouthStyle.Polite, FaceMarks.Mole | FaceMarks.Lashes), jacket: 0x8E5A9E, rarity: Rarity.Rare);
            Staff("bob", "BOB", Department.Finance, "AUDITOR", -1, 0xEDC3A1, 0x6B5444, HairStyle.Combover, F(EyeShape.Droopy, Iris.Brown, BrowStyle.Bushy, NoseStyle.Long, MouthStyle.Polite, FaceMarks.EyeBags | FaceMarks.Wrinkles), rarity: Rarity.Rare);
            Staff("yuki", "YUKI", Department.Finance, "BUDGET ANALYST", -1, 0xF2D3B8, 0x161620, HairStyle.Bob, F(EyeShape.Almond, Iris.Dark, BrowStyle.Straight, NoseStyle.Button, MouthStyle.Smile, FaceMarks.Rosy), jacket: 0x3E8E7E, rarity: Rarity.Rare);
            // Management: blazers, and a calendar full of syncs.
            var Mgmt = Department.Management;
            Staff("gary", "GARY", Mgmt, "MIDDLE MANAGER", -1, 0xE9BE9C, 0x7A6252, HairStyle.Combover, F(EyeShape.Droopy, Iris.Grey, BrowStyle.Bushy, NoseStyle.Round, MouthStyle.Polite, FaceMarks.EyeBags | FaceMarks.Stubble), jacket: 0x6E7480, rarity: Rarity.Epic);
            Staff("dana", "DANA", Mgmt, "PROJECT MANAGER", 108, 0xF1CBB0, 0x5A3825, HairStyle.Ponytail, F(EyeShape.Round, Iris.Blue, BrowStyle.Straight, NoseStyle.Soft, MouthStyle.Smile, FaceMarks.EyeBags), rarity: Rarity.Epic);
            Staff("omar", "OMAR", Mgmt, "TEAM LEAD", 120, 0x6B4330, 0x15100E, HairStyle.Short, F(EyeShape.Almond, Iris.Dark, BrowStyle.Thick, NoseStyle.Long, MouthStyle.Grin), PersonLook.Beard, jacket: 0x1F7A80, rarity: Rarity.Epic);
            // The C-suite: dark blazers, pocket squares and very calm faces. Hired with gems.
            var C = Department.CLevel;
            Staff("vera", "VERA", C, "CFO", -1, 0xF3D1BE, 0xDADAE6, HairStyle.Bob, F(EyeShape.Cat, Iris.Grey, BrowStyle.Thin, NoseStyle.Pointy, MouthStyle.Lipstick, FaceMarks.Lashes | FaceMarks.Mole), PersonLook.Earrings, rarity: Rarity.Legendary, gems: 700);
            Staff("max", "MAX", C, "COO", -1, 0xE0AE8A, 0x4A3A30, HairStyle.Bald, F(EyeShape.Narrow, Iris.Blue, BrowStyle.Thick, NoseStyle.Round, MouthStyle.Smirk, FaceMarks.Stubble | FaceMarks.Wrinkles), jacket: 0x2E3442, rarity: Rarity.Legendary, gems: 800);
            Staff("elle", "ELLE", C, "CMO", -1, 0xF7DCCB, 0xF4E2A8, HairStyle.Long, F(EyeShape.Big, Iris.Green, BrowStyle.Arched, NoseStyle.Button, MouthStyle.Lipstick, FaceMarks.Lashes | FaceMarks.Rosy), PersonLook.Earrings, jacket: 0xD7263D, rarity: Rarity.Legendary, gems: 650);
            // The bosses: hired with gems only, each with a special of their own (StaffPerks).
            var Boss = Department.Boss;
            Staff("chair", "MADAM CHAIR", Boss, "CHAIRWOMAN", -1, 0x8A5A3C, 0xCDCDD8, HairStyle.Curly, F(EyeShape.Almond, Iris.Amber, BrowStyle.Arched, NoseStyle.Soft, MouthStyle.Lipstick, FaceMarks.Wrinkles | FaceMarks.Lashes), PersonLook.Hoops, jacket: 0x3B1E5A, rarity: Rarity.Legendary, gems: 1500);
            Staff("boss", "THE BOSS", Boss, "CEO & FOUNDER", -1, 0xEAB894, 0xF2F2F2, HairStyle.Slick, F(EyeShape.Narrow, Iris.Amber, BrowStyle.Bushy, NoseStyle.Long, MouthStyle.Smirk, FaceMarks.Wrinkles | FaceMarks.Stubble), rarity: Rarity.Legendary, gems: 2500);

            // More hires (OUT-45): new faces in every department, more star goals, and two more bosses to hire.
            Staff("zoe", "ZOE", I, "TIKTOK INTERN", -1, 0xF2C2A0, 0x7B3FE0, HairStyle.Ponytail, F(EyeShape.Cat, Iris.Violet, BrowStyle.Thin, NoseStyle.Button, MouthStyle.Grin, FaceMarks.Freckles | FaceMarks.Lashes), PersonLook.Earrings);
            Staff("finn", "FINN", I, "IT INTERN", -1, 0xEFD0B8, 0xD98A3C, HairStyle.Spiky, F(EyeShape.Round, Iris.Hazel, BrowStyle.Straight, NoseStyle.Soft, MouthStyle.Smirk, FaceMarks.Freckles | FaceMarks.EyeBags));
            Staff("hank", "HANK", E, "NIGHT GUARD", 135, 0x8E5B3E, 0x2A2420, HairStyle.Buzz, F(EyeShape.Narrow, Iris.Brown, BrowStyle.Bushy, NoseStyle.Round, MouthStyle.Polite, FaceMarks.Stubble | FaceMarks.EyeBags), PersonLook.Beard, rarity: Rarity.Epic);
            Staff("olga", "OLGA", E, "CLEANING CREW", 150, 0xF3D3BD, 0xB7B7C2, HairStyle.Bun, F(EyeShape.Droopy, Iris.Green, BrowStyle.Thick, NoseStyle.Pointy, MouthStyle.Smile, FaceMarks.Wrinkles | FaceMarks.Rosy), rarity: Rarity.Epic);
            Staff("ozzy", "OZZY", Eng, "SYSADMIN", -1, 0xE9C2A2, 0x3A2A22, HairStyle.Long, F(EyeShape.Droopy, Iris.Grey, BrowStyle.Thick, NoseStyle.Long, MouthStyle.Smirk, FaceMarks.Stubble | FaceMarks.EyeBags), PersonLook.Beard | PersonLook.Tattoos, rarity: Rarity.Epic);
            Staff("mei", "MEI", Eng, "AI ENGINEER", 165, 0xF4D6BF, 0x1A1A24, HairStyle.Pigtails, F(EyeShape.Big, Iris.Violet, BrowStyle.Straight, NoseStyle.Button, MouthStyle.Smile, FaceMarks.Lashes | FaceMarks.Rosy), rarity: Rarity.Epic);
            Staff("pam", "PAM", Department.HR, "PARTY PLANNER", -1, 0xD8A07C, 0xFF6FA0, HairStyle.Curly, F(EyeShape.Big, Iris.Amber, BrowStyle.Arched, NoseStyle.Round, MouthStyle.Grin, FaceMarks.Dimples | FaceMarks.Rosy), jacket: 0xFFD166, rarity: Rarity.Rare);
            Staff("ian", "IAN", Department.Finance, "TAX WIZARD", -1, 0xEBC4A4, 0xE6E6EE, HairStyle.Combover, F(EyeShape.Narrow, Iris.Green, BrowStyle.Bushy, NoseStyle.Pointy, MouthStyle.Smirk, FaceMarks.Wrinkles | FaceMarks.Mole), jacket: 0x5A4A8A, rarity: Rarity.Epic);
            Staff("duke", "DUKE", Mgmt, "VP OF SALES", 180, 0xC98B66, 0x1E1612, HairStyle.Slick, F(EyeShape.Almond, Iris.Hazel, BrowStyle.Thick, NoseStyle.Pointy, MouthStyle.Grin, FaceMarks.Dimples | FaceMarks.Stubble), jacket: 0x8C1F2E, rarity: Rarity.Legendary);
            Staff("fran", "FRAN", Mgmt, "HEAD OF OPS", -1, 0xF1CFB6, 0x8C5A2B, HairStyle.Bob, F(EyeShape.Cat, Iris.Brown, BrowStyle.Straight, NoseStyle.Soft, MouthStyle.Polite, FaceMarks.Lashes | FaceMarks.EyeBags), jacket: 0x2F5E4E, rarity: Rarity.Epic);
            Staff("sol", "SOL", C, "CHIEF VIBES OFFICER", -1, 0xB97A55, 0x2EC4B6, HairStyle.Afro, F(EyeShape.Big, Iris.Amber, BrowStyle.Thin, NoseStyle.Soft, MouthStyle.Grin, FaceMarks.Rosy | FaceMarks.Freckles), PersonLook.Earrings, jacket: 0xFF8C42, rarity: Rarity.Legendary, gems: 600);
            Staff("aria", "ARIA", C, "CHIEF OF STAFF", -1, 0xF6D9C8, 0x2B1D16, HairStyle.Ponytail, F(EyeShape.Almond, Iris.Grey, BrowStyle.Arched, NoseStyle.Pointy, MouthStyle.Lipstick, FaceMarks.Mole | FaceMarks.Lashes), jacket: 0x1F2F5A, rarity: Rarity.Legendary, gems: 650);
            Staff("investor", "THE INVESTOR", Boss, "ANGEL INVESTOR", -1, 0xE7B48E, 0xC0C0C8, HairStyle.Slick, F(EyeShape.Round, Iris.Blue, BrowStyle.Thick, NoseStyle.Round, MouthStyle.Smirk, FaceMarks.Wrinkles | FaceMarks.Dimples), jacket: 0x0F3D2E, rarity: Rarity.Legendary, gems: 1800);
            Staff("heir", "THE HEIR", Boss, "THE BOSS'S KID", -1, 0xF5D6C2, 0xF2D37A, HairStyle.Curly, F(EyeShape.Big, Iris.Blue, BrowStyle.Arched, NoseStyle.Button, MouthStyle.Smirk, FaceMarks.Freckles | FaceMarks.Rosy | FaceMarks.Lashes), jacket: 0x5B2C83, rarity: Rarity.Legendary, gems: 1200);

            // Chest-only colors.
            Color("cherry", "PERFORMANCE REVIEW", Rarity.Common, 0xE84A5F, 0xFF3D5E, 0x5CC46A);
            Color("ocean", "OCEAN VIEW OFFICE", Rarity.Common, 0x3FA9E0, 0xFF8FB8, 0xFFD45C);
            Color("mango", "MANGO SMOOTHIE", Rarity.Common, 0xFFB300, 0xFF7A7A, 0x4CB86A);
            Color("lilac", "LILAC LANYARD", Rarity.Common, 0xA78BD6, 0xFF8FC0, 0x7E57C2);
            Color("mintchoco", "MINT CHOCO", Rarity.Rare, 0x5FC7A0, 0xFF9AA2, 0x6D4C41);
            Color("lava", "HOT TAKE", Rarity.Rare, 0xFF6A3D, 0xFFD54F, 0x3E2723);
            Color("galaxy", "BIG PICTURE", Rarity.Epic, 0x3F2E7A, 0xFF8FD8, 0x7CF5FF);
            Color("gold", "PROMOTED", Rarity.Legendary, 0xE8B530, 0xFF9A6A, 0xFFFFFF);

            // Chest-only hats.
            Hat("cap", "TEAM BUILDING CAP", Rarity.Common, Accessory.Cap, 0x3D7BF2);
            Hat("bunny", "EASTER OFFSITE", Rarity.Rare, Accessory.BunnyEars, 0xFFF4F8);
            Hat("headphones", "DO NOT DISTURB", Rarity.Rare, Accessory.Headphones, 0xFF5E7E);
            Hat("chef", "MICROWAVE CHEF", Rarity.Rare, Accessory.ChefHat, 0xFFFFFF);
            Hat("tophat", "BOARD MEMBER", Rarity.Epic, Accessory.TopHat, 0x2B2440);
            Hat("wizard", "IT WIZARD", Rarity.Legendary, Accessory.WizardHat, 0x5B3FE0);

            // Chest-only face stickers.
            All.Add(new CosmeticItem { Id = "f.glasses", Name = "READING GLASSES", Slot = Slot.Face, Rarity = Rarity.Common, StarsNeeded = 0, Face = FaceItem.Glasses });
            Face("freckles", "FRECKLES", Rarity.Common, FaceItem.Freckles);
            Face("mustache", "MIDDLE MANAGEMENT", Rarity.Common, FaceItem.Mustache);
            Face("bowtie", "BUSINESS ON TOP", Rarity.Rare, FaceItem.BowTie);
            Face("sunglasses", "WORK FROM BEACH", Rarity.Epic, FaceItem.Sunglasses);
            Face("starshades", "EMPLOYEE OF THE MONTH", Rarity.Legendary, FaceItem.StarShades);
            // More face items (OUT-44).
            Face("bandaid", "PAPER CUT", Rarity.Common, FaceItem.BandAid);
            Face("clownnose", "MANDATORY FUN", Rarity.Common, FaceItem.ClownNose);
            Face("heartshades", "WORK CRUSH", Rarity.Rare, FaceItem.HeartShades);
            Face("roundspecs", "THOUGHT LEADER", Rarity.Rare, FaceItem.RoundSpecs);
            Face("aviators", "FREQUENT FLYER", Rarity.Epic, FaceItem.Aviators);
            Face("handlebar", "VINTAGE CEO", Rarity.Epic, FaceItem.Handlebar);
            Face("monocle", "OLD MONEY", Rarity.Legendary, FaceItem.Monocle);
            Face("vrgoggles", "METAVERSE MEETING", Rarity.Legendary, FaceItem.VrGoggles);

            // The wardrobe lists the staff by department, interns first; keep them in that order here too, so their icons
            // render top down.
            var staff = All.FindAll(i => i.Slot == Slot.Employee);
            var ordered = new List<CosmeticItem>(staff.Count);
            foreach (Department d in System.Enum.GetValues(typeof(Department))) ordered.AddRange(staff.FindAll(s => s.Dept == d));
            for (int i = 0, k = 0; i < All.Count; i++)
                if (All[i].Slot == Slot.Employee) All[i] = ordered[k++];
        }

        // An employee. stars < 0: chest only, with the given rarity. Each department dresses its people the same way
        // (OUT-43); jacket picks their own colour of it, or the department's.
        static void Staff(string id, string name, Department dept, string title, int stars, int skin, int hair, HairStyle style, FaceDef face,
            PersonLook extras = PersonLook.None, int jacket = -1, Rarity? rarity = null, int gems = 0) =>
            All.Add(new CosmeticItem { Id = "e." + id, Name = name, Slot = Slot.Employee, Rarity = rarity ?? (stars > 0 ? Rarity.Rare : Rarity.Common), StarsNeeded = gems > 0 ? -1 : stars, GemCost = gems,
                SkinTone = Themes.Hex(skin), HairColor = Themes.Hex(hair), Hair = style, Extras = extras | Clothes(dept), Dept = dept, Title = title, Features = face,
                Jacket = Themes.Hex(jacket >= 0 ? jacket : DepartmentColour(dept)) });

        static FaceDef F(EyeShape eyes, Iris iris, BrowStyle brows, NoseStyle nose, MouthStyle mouth, FaceMarks marks = FaceMarks.None) =>
            new FaceDef(eyes, iris, brows, nose, mouth, marks);

        static PersonLook Clothes(Department d) =>
            d == Department.Engineering || d == Department.Interns ? PersonLook.Lanyard
            : d == Department.HR ? PersonLook.Cardigan
            : d == Department.Finance ? PersonLook.Vest
            : d == Department.Management ? PersonLook.Blazer
            : d == Department.CLevel ? PersonLook.Blazer | PersonLook.PocketSquare
            : d == Department.Boss ? PersonLook.Blazer | PersonLook.PocketSquare | PersonLook.GoldWatch | PersonLook.Mug
            : PersonLook.None;

        static int DepartmentColour(Department d) =>
            d == Department.Engineering ? 0x2F6FE0 : d == Department.Interns ? 0xFF8A3D : d == Department.HR ? 0xF2A7B8 : d == Department.Finance ? 0x6B7A3A
            : d == Department.Management ? 0x3A4A6B : d == Department.CLevel ? 0x22222E : d == Department.Boss ? 0x141418 : 0xFFFFFF;

        public static string DepartmentName(Department d) =>
            d == Department.Sales ? "SALES & MARKETING" : d == Department.CLevel ? "C-LEVEL" : d == Department.Boss ? "THE BOSS" : d == Department.HR ? "HR" : d.ToString().ToUpperInvariant();

        static void Color(string id, string name, Rarity r, int body, int cheek, int extra) =>
            All.Add(new CosmeticItem { Id = "c." + id, Name = name, Slot = Slot.Color, Rarity = r, Body = Themes.Hex(body), Cheek = Themes.Hex(cheek), Extra = Themes.Hex(extra) });

        static void Hat(string id, string name, Rarity r, Accessory hat, int tint) =>
            All.Add(new CosmeticItem { Id = "h." + id, Name = name, Slot = Slot.Hat, Rarity = r, Hat = hat, HatTint = Themes.Hex(tint) });

        static void Face(string id, string name, Rarity r, FaceItem face) =>
            All.Add(new CosmeticItem { Id = "f." + id, Name = name, Slot = Slot.Face, Rarity = r, Face = face });

        static Rarity RarityForStars(int stars) => stars >= 48 ? Rarity.Legendary : stars >= 28 ? Rarity.Epic : stars >= 10 ? Rarity.Rare : Rarity.Common;

        public static string HatId(Accessory a) => a.ToString().ToLowerInvariant();

        static string HatName(Accessory a)
        {
            switch (a)
            {
                case Accessory.Tuft: return "NO HAT";
                case Accessory.Bow: return "HAIR BOW";
                case Accessory.Sprout: return "DESK PLANT";
                case Accessory.Antenna: return "SIGNAL BOOSTER";
                case Accessory.CatEars: return "CAT EARS";
                case Accessory.Beanie: return "WORK FROM BED";
                case Accessory.PartyHat: return "OFFICE PARTY";
                case Accessory.Crown: return "SPREADSHEET ROYALTY";
                case Accessory.Horns: return "DEVIL'S ADVOCATE";
                case Accessory.Flower: return "FLOWER CLIP";
                case Accessory.Halo: return "MODEL EMPLOYEE";
                case Accessory.Propeller: return "IDEAS HAT";
                default: return a.ToString().ToUpperInvariant();
            }
        }

        public static CosmeticItem Find(string id)
        {
            foreach (var i in All) if (i.Id == id) return i;
            return null;
        }

        public static List<CosmeticItem> InSlot(Slot slot)
        {
            var list = new List<CosmeticItem>();
            foreach (var i in All) if (i.Slot == slot) list.Add(i);
            return list;
        }

        public static Color RarityColor(Rarity r) =>
            r == Rarity.Legendary ? Themes.Hex(0xFFB020) : r == Rarity.Epic ? Themes.Hex(0xB15CFF) : r == Rarity.Rare ? Themes.Hex(0x3FA2FF) : Themes.Hex(0x8BC34A);

        public static string RarityName(Rarity r) => r.ToString().ToUpperInvariant();

        public static string SlotName(Slot s) => s == Slot.Employee ? "EMPLOYEE" : s == Slot.Color ? "TIE" : s == Slot.Hat ? "HEADWEAR" : "FACE";

        // The look the game builds: colors from the color item, the hat (tinted by its own color or the body's
        // accent), and the face sticker.
        public static SkinDef Compose(Outfit o)
        {
            var person = Find(o.Employee) ?? Find(Default.Employee);
            var color = Find(o.Color) ?? Find(Default.Color);
            var hat = Find(o.Hat) ?? Find(Default.Hat);
            var face = Find(o.Face) ?? Find(Default.Face);
            return new SkinDef
            {
                Id = person.Id + "+" + color.Id + "+" + hat.Id + "+" + face.Id,
                Name = color.Name,
                Body = color.Body,
                Cheek = color.Cheek,
                Extra = hat.HatTint ?? color.Extra,
                Accessory = hat.Hat,
                Face = face.Face,
                SkinTone = person.SkinTone,
                HairColor = person.HairColor,
                Hair = person.Hair,
                Extras = person.Extras,
                Jacket = person.Jacket,
                Features = person.Features,
            };
        }

        // Chest roll: pick a rarity by weight among those with items left, then an item of that rarity.
        // When nothing is left, a duplicate of an owned chest item comes out instead.
        public static CosmeticItem Roll(System.Func<CosmeticItem, bool> owned, System.Random rng)
        {
            var pool = new List<CosmeticItem>();
            foreach (var i in All) if (i.FromChest && !owned(i)) pool.Add(i);
            bool duplicate = pool.Count == 0;
            if (duplicate) foreach (var i in All) if (i.FromChest) pool.Add(i);
            int[] weights = { 55, 30, 12, 3 };
            int total = 0;
            for (int r = 0; r < 4; r++) if (pool.Exists(i => (int)i.Rarity == r)) total += weights[r];
            int pick = rng.Next(total);
            Rarity chosen = Rarity.Common;
            for (int r = 0; r < 4; r++)
            {
                if (!pool.Exists(i => (int)i.Rarity == r)) continue;
                if (pick < weights[r]) { chosen = (Rarity)r; break; }
                pick -= weights[r];
            }
            var tier = pool.FindAll(i => i.Rarity == chosen);
            return tier[rng.Next(tier.Count)];
        }
    }
}
