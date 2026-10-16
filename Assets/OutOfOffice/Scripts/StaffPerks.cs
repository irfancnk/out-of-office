namespace OutOfOffice
{
    // What wearing an employee does (OUT-45): every department brings a perk, from an intern's extra undo up to the
    // C-suite's golden parachute, and each boss has a special of their own. Perks only ever make a room easier or
    // pay a little more, so every room stays solvable without them (LevelCheck plays with none).
    public class Perk
    {
        public string Name, Description;
        public int FreeUndos;       // extra free undos each try
        public int ExtraShots;      // added to every room's shot limit
        public int StarGems;        // gems for every new star earned
        public float GemDiscount;   // off continues and paid undos, 0..1
        public float AimReach = 1f; // how far the aim guide reaches
        public bool GoldTrail;      // a golden sparkle trail while rolling

        public static readonly Perk None = new Perk { Name = "", Description = "" };
    }

    public static class StaffPerks
    {
        static readonly Perk Interns = new Perk { Name = "EAGER BEAVER", Description = "+1 free undo every try", FreeUndos = 1 };
        static readonly Perk Employees = new Perk { Name = "OVERTIME", Description = "+1 shot in every room", ExtraShots = 1 };
        static readonly Perk Engineering = new Perk { Name = "DEBUG MODE", Description = "The aim guide reaches 40% further", AimReach = 1.4f };
        static readonly Perk HR = new Perk { Name = "WELLNESS DAY", Description = "+2 free undos every try", FreeUndos = 2 };
        static readonly Perk Sales = new Perk { Name = "COMMISSION", Description = "+5 gems for every new star", StarGems = 5 };
        static readonly Perk Finance = new Perk { Name = "BUDGET CUTS", Description = "Continues and undos cost 30% fewer gems", GemDiscount = 0.3f };
        static readonly Perk Management = new Perk { Name = "DELEGATION", Description = "+2 shots in every room", ExtraShots = 2 };
        static readonly Perk CLevel = new Perk { Name = "GOLDEN PARACHUTE", Description = "+2 shots, +1 free undo, 20% off gem costs", ExtraShots = 2, FreeUndos = 1, GemDiscount = 0.2f };

        // The bosses each have their own special, and all of them roll on a trail of gold.
        static readonly System.Collections.Generic.Dictionary<string, Perk> Specials = new System.Collections.Generic.Dictionary<string, Perk>
        {
            { "e.boss", new Perk { Name = "CORNER OFFICE", Description = "+3 shots, +2 free undos, +10 gems a star", ExtraShots = 3, FreeUndos = 2, StarGems = 10, GoldTrail = true } },
            { "e.chair", new Perk { Name = "BOARD VETO", Description = "+2 shots, +3 free undos, 30% off gems", ExtraShots = 2, FreeUndos = 3, GemDiscount = 0.3f, GoldTrail = true } },
            { "e.investor", new Perk { Name = "SEED FUNDING", Description = "+20 gems for every new star, +1 shot", StarGems = 20, ExtraShots = 1, GoldTrail = true } },
            { "e.heir", new Perk { Name = "NEPOTISM", Description = "+4 free undos, +2 shots, a longer aim", FreeUndos = 4, ExtraShots = 2, AimReach = 1.25f, GoldTrail = true } },
        };

        public static Perk For(CosmeticItem employee)
        {
            if (employee == null || employee.Slot != Slot.Employee) return Perk.None;
            if (Specials.TryGetValue(employee.Id, out var special)) return special;
            switch (employee.Dept)
            {
                case Department.Interns: return Interns;
                case Department.Employees: return Employees;
                case Department.Engineering: return Engineering;
                case Department.HR: return HR;
                case Department.Sales: return Sales;
                case Department.Finance: return Finance;
                case Department.Management: return Management;
                case Department.CLevel: return CLevel;
                default: return CLevel;
            }
        }

        public static Perk For(string employeeId) => For(Cosmetics.Find(employeeId));
    }
}
