using UnityEngine;

namespace OutOfOffice
{
    // Headwear on top of the employee's head. Purely cosmetic. (Tuft is "no hat": the hair shows.)
    public enum Accessory { Tuft, Bow, Sprout, Antenna, CatEars, Beanie, PartyHat, Crown, Horns, Flower, Halo, Propeller, Cap, BunnyEars, Headphones, ChefHat, TopHat, WizardHat }

    public enum HairStyle { Bob, Short, Bun, Curly, Mohawk, Long, Afro, Spiky, Ponytail, Buzz }

    // Extra details on a person (OUT-30). Cosmetic only.
    [System.Flags] public enum PersonLook { None = 0, Tattoos = 1, Earrings = 2, Beard = 4 }

    // A look for the employee. Every look uses the same round collider and physics.
    // Body is the tie color; the shirt stays office white.
    public class SkinDef
    {
        public string Id, Name;
        public Color Body, Cheek, Extra;   // Body: tie. Extra colors the headwear
        public Color SkinTone = new Color(0.97f, 0.82f, 0.71f);
        public Color HairColor = new Color(0.36f, 0.23f, 0.16f);
        public HairStyle Hair = HairStyle.Bob;
        public PersonLook Extras;
        public Accessory Accessory;
        public FaceItem Face;
        public int StarsNeeded;
    }

    public static class Skins
    {
        public static readonly SkinDef[] All =
        {
            new SkinDef { Id = "puffy", Name = "PUFFY", Body = Hex(0xFFD45C), Cheek = Hex(0xFF9AA2), Extra = Hex(0xFFB43C), Accessory = Accessory.Tuft, StarsNeeded = 0 },
            new SkinDef { Id = "peachy", Name = "PEACHY", Body = Hex(0xFFB5A7), Cheek = Hex(0xFF7F8E), Extra = Hex(0xFF5E7E), Accessory = Accessory.Bow, StarsNeeded = 3 },
            new SkinDef { Id = "minty", Name = "MINTY", Body = Hex(0x8FE3C0), Cheek = Hex(0xFF9AA2), Extra = Hex(0x4CB86A), Accessory = Accessory.Sprout, StarsNeeded = 6 },
            new SkinDef { Id = "bluebell", Name = "BLUEBELL", Body = Hex(0x8CC8FF), Cheek = Hex(0xFF9AB8), Extra = Hex(0xFFD45C), Accessory = Accessory.Antenna, StarsNeeded = 10 },
            new SkinDef { Id = "grape", Name = "GRAPE", Body = Hex(0xC4A6F5), Cheek = Hex(0xFF8FC0), Extra = Hex(0xA584E6), Accessory = Accessory.CatEars, StarsNeeded = 14 },
            new SkinDef { Id = "snowy", Name = "SNOWY", Body = Hex(0xF7F7FB), Cheek = Hex(0xFFA0B4), Extra = Hex(0xFF6B6B), Accessory = Accessory.Beanie, StarsNeeded = 18 },
            new SkinDef { Id = "tangerine", Name = "TANGY", Body = Hex(0xFFA552), Cheek = Hex(0xFF7A7A), Extra = Hex(0x5CC46A), Accessory = Accessory.Flower, StarsNeeded = 23 },
            new SkinDef { Id = "cocoa", Name = "COCOA", Body = Hex(0xC08A62), Cheek = Hex(0xFF9AA2), Extra = Hex(0x5AB2FF), Accessory = Accessory.PartyHat, StarsNeeded = 28 },
            new SkinDef { Id = "bubblegum", Name = "BUBBLES", Body = Hex(0xFF8FC8), Cheek = Hex(0xFF5E9A), Extra = Hex(0xFFD24C), Accessory = Accessory.Crown, StarsNeeded = 34 },
            new SkinDef { Id = "lime", Name = "LIMEY", Body = Hex(0xC6EC6A), Cheek = Hex(0xFF9A9A), Extra = Hex(0x3D9BF5), Accessory = Accessory.Propeller, StarsNeeded = 40 },
            new SkinDef { Id = "midnight", Name = "MIDNIGHT", Body = Hex(0x5662B8), Cheek = Hex(0xFF8FC0), Extra = Hex(0xFFD45C), Accessory = Accessory.Horns, StarsNeeded = 48 },
            new SkinDef { Id = "angel", Name = "ANGEL", Body = Hex(0xFFF3D6), Cheek = Hex(0xFFA6B8), Extra = Hex(0xFFD24C), Accessory = Accessory.Halo, StarsNeeded = 58 },
        };

        public static int IndexOf(string id)
        {
            for (int i = 0; i < All.Length; i++) if (All[i].Id == id) return i;
            return 0;
        }

        static Color Hex(int rgb) => new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f);
    }
}
