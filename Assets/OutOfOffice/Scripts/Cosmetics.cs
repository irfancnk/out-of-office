using System.Collections.Generic;
using UnityEngine;

namespace OutOfOffice
{
    public enum Rarity { Common, Rare, Epic, Legendary }
    public enum Slot { Employee, Color, Hat, Face }

    // Little things worn on the face, drawn as stickers on Puffy's face.
    public enum FaceItem { None, Freckles, Mustache, Glasses, BowTie, Sunglasses, StarShades }

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

        public bool FromChest => StarsNeeded < 0;
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
        public static readonly Outfit Default = new Outfit { Employee = "e.ava", Color = "c.bluebell", Hat = "h.tuft", Face = "f.glasses" };

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

            // The staff. Same body, same physics; different people having the same week.
            Employee("ava", "AVA", 0, 0xF5D2BC, 0x5B3A29, HairStyle.Bob);
            Employee("leo", "LEO", 0, 0xE0B08A, 0x2B2420, HairStyle.Short);
            Employee("mia", "MIA", 8, 0x8D5A3B, 0x1E1714, HairStyle.Bun);
            Employee("sam", "SAM", 16, 0xC98E64, 0xB4552A, HairStyle.Curly);
            // The extraordinary ones (OUT-30): bold hair, tattoos, earrings. Some unlock with stars, some only from chests.
            Employee("jax", "JAX", 24, 0xF2CDB4, 0x8A3FE0, HairStyle.Mohawk, PersonLook.Tattoos | PersonLook.Earrings);
            Employee("nova", "NOVA", 32, 0xD9A27A, 0xFF5FA8, HairStyle.Long, PersonLook.Earrings);
            Employee("kai", "KAI", 40, 0xB5795A, 0x23C4B4, HairStyle.Spiky, PersonLook.Tattoos);
            Employee("otto", "OTTO", 48, 0xF0C7A8, 0xD9D9E3, HairStyle.Buzz, PersonLook.Beard);
            Employee("luna", "LUNA", -1, 0xF7DCCB, 0x4E7BFF, HairStyle.Long, PersonLook.Earrings, Rarity.Rare);
            Employee("ivy", "IVY", -1, 0xE8B894, 0x5CC46A, HairStyle.Ponytail, PersonLook.Tattoos, Rarity.Rare);
            Employee("zara", "ZARA", -1, 0x6E4430, 0x7A3FD0, HairStyle.Afro, PersonLook.Earrings, Rarity.Epic);
            Employee("rex", "REX", -1, 0xD49A72, 0xF4F1E6, HairStyle.Spiky, PersonLook.Tattoos | PersonLook.Earrings | PersonLook.Beard, Rarity.Legendary);

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
        }

        // stars < 0: chest only, with the given rarity.
        static void Employee(string id, string name, int stars, int skin, int hair, HairStyle style, PersonLook extras = PersonLook.None, Rarity? rarity = null) =>
            All.Add(new CosmeticItem { Id = "e." + id, Name = name, Slot = Slot.Employee, Rarity = rarity ?? (stars > 0 ? Rarity.Rare : Rarity.Common), StarsNeeded = stars,
                SkinTone = Themes.Hex(skin), HairColor = Themes.Hex(hair), Hair = style, Extras = extras });

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
