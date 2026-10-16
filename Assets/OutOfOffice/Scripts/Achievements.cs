using System;
using System.Collections.Generic;

namespace OutOfOffice
{
    // Achievements (OUT-9): 52 goals across the whole game, each worth gems once claimed. Most read the save as it
    // is (rooms, stars, chests, the wardrobe); the rest count events through Progress stats. They live on the device
    // for now; Google Play Games and Game Center can mirror the same keys once those accounts exist.
    public class AchievementDef
    {
        public string Key, Name, Text;
        public int Target, Gems;
        public Func<Progress, int> Value;
    }

    public static class Achievements
    {
        // Stat keys the game counts.
        public const string HoleInOne = "holeinone", ParFinish = "par", Shots = "shots", BoostersUsed = "boosters", QuestsClaimed = "quests",
            DaysPlayed = "days", BestStreak = "beststreak", Pokes = "pokes", Pinball = "pinball", BossClean = "bossclean", ClipBuys = "clipbuys";

        static AchievementDef A(string key, string name, string text, int target, int gems, Func<Progress, int> value) =>
            new AchievementDef { Key = key, Name = name, Text = text, Target = target, Gems = gems, Value = value };

        // Rooms beaten: with a best score, which also counts the last room (the unlocked count stops at the last room).
        static int Rooms(Progress p)
        {
            int n = 0;
            foreach (var l in Levels.All) if (p.Best(l.Id) > 0) n++;
            return n;
        }

        static int Mastered(Progress p)
        {
            int n = 0;
            for (int c = 0; c + 1 < Themes.ChapterStart.Length; c++) if (p.ChapterMastered(c)) n++;
            return n;
        }

        static int Owned(Progress p, Func<CosmeticItem, bool> which)
        {
            int n = 0;
            foreach (var i in Cosmetics.All) if (which(i) && p.Owns(i)) n++;
            return n;
        }

        static int Met(Progress p)
        {
            int n = 0;
            foreach (var g in Gadgets.All) if (p.SeenGadget(g.Key)) n++;
            return n;
        }

        public static readonly AchievementDef[] All =
        {
            A("rooms1", "FIRST DAY", "Make it to bed in the first room.", 1, 10, Rooms),
            A("rooms10", "PROBATION OVER", "Beat 10 rooms.", 10, 20, Rooms),
            A("rooms25", "TEAM PLAYER", "Beat 25 rooms.", 25, 30, Rooms),
            A("rooms50", "MIDDLE MANAGEMENT", "Beat 50 rooms.", 50, 50, Rooms),
            A("rooms75", "SENIOR NAPPER", "Beat 75 rooms.", 75, 70, Rooms),
            A("rooms100", "RETIRED", "Beat all 100 rooms.", 100, 150, Rooms),

            A("stars50", "GOLD STARS", "Earn 50 stars.", 50, 20, p => p.TotalStars),
            A("stars100", "STAR EMPLOYEE", "Earn 100 stars.", 100, 30, p => p.TotalStars),
            A("stars150", "OVERACHIEVER", "Earn 150 stars.", 150, 40, p => p.TotalStars),
            A("stars200", "TEACHER'S PET", "Earn 200 stars.", 200, 60, p => p.TotalStars),
            A("stars250", "PERFORMANCE REVIEW: EXCEEDS", "Earn 250 stars.", 250, 80, p => p.TotalStars),
            A("stars300", "PERFECT RECORD", "Earn all 300 stars.", 300, 200, p => p.TotalStars),

            A("master1", "FLOOR PLAN", "Earn every star on a floor.", 1, 30, Mastered),
            A("master3", "THREE FLOORS DOWN", "Earn every star on 3 floors.", 3, 50, Mastered),
            A("master5", "HALF THE BUILDING", "Earn every star on 5 floors.", 5, 80, Mastered),
            A("master10", "OWNS THE BUILDING", "Earn every star on all 10 floors.", 10, 200, Mastered),

            A("hio1", "HOLE IN ONE", "Reach the bed in a single shot.", 1, 15, p => p.Stat(HoleInOne)),
            A("hio10", "SHOW-OFF", "10 holes in one.", 10, 30, p => p.Stat(HoleInOne)),
            A("hio50", "TRICK SHOT ARTIST", "50 holes in one.", 50, 60, p => p.Stat(HoleInOne)),
            A("hio100", "ONE AND DONE", "100 holes in one.", 100, 120, p => p.Stat(HoleInOne)),

            A("par10", "ON SCHEDULE", "Finish 10 rooms at par or better.", 10, 15, p => p.Stat(ParFinish)),
            A("par50", "EFFICIENT", "Finish 50 rooms at par or better.", 50, 40, p => p.Stat(ParFinish)),
            A("par100", "LEAN AND MEAN", "Finish 100 rooms at par or better.", 100, 80, p => p.Stat(ParFinish)),

            A("coffee25", "CAFFEINATED", "Collect 25 coffees.", 25, 15, p => p.Stamps),
            A("coffee100", "DOUBLE SHOT", "Collect 100 coffees.", 100, 40, p => p.Stamps),
            A("coffee250", "BARISTA'S FAVOURITE", "Collect 250 coffees.", 250, 80, p => p.Stamps),

            A("chest1", "UNBOXING", "Open your first chest.", 1, 10, p => p.ChestsOpened),
            A("chest10", "SUPPLY RUN", "Open 10 chests.", 10, 25, p => p.ChestsOpened),
            A("chest50", "HOARDER", "Open 50 chests.", 50, 60, p => p.ChestsOpened),

            A("wardrobe10", "DRESS CODE", "Own 10 wardrobe items.", 10, 15, p => Owned(p, i => i.Slot != Slot.Employee)),
            A("wardrobe25", "FASHION WEEK", "Own 25 wardrobe items.", 25, 40, p => Owned(p, i => i.Slot != Slot.Employee)),
            A("wardrobe50", "WALK-IN CLOSET", "Own 50 wardrobe items.", 50, 80, p => Owned(p, i => i.Slot != Slot.Employee)),

            A("staff10", "HIRING SPREE", "Have 10 people on staff.", 10, 25, p => Owned(p, i => i.Slot == Slot.Employee)),
            A("staff25", "HR DEPARTMENT", "Have 25 people on staff.", 25, 50, p => Owned(p, i => i.Slot == Slot.Employee)),
            A("clevel", "THE C-SUITE", "Hire someone from the C-suite.", 1, 40, p => Owned(p, i => i.Slot == Slot.Employee && i.Dept == Department.CLevel)),
            A("boss", "MEET THE BOSS", "Hire one of the bosses.", 1, 80, p => Owned(p, i => i.Slot == Slot.Employee && i.Dept == Department.Boss)),

            A("booster1", "WORK SMARTER", "Use a booster.", 1, 10, p => p.Stat(BoostersUsed)),
            A("booster25", "POWER USER", "Use 25 boosters.", 25, 40, p => p.Stat(BoostersUsed)),

            A("streak5", "ON A ROLL", "Win 5 rooms in a row without failing.", 5, 25, p => p.Stat(BestStreak)),
            A("streak15", "UNSTOPPABLE", "Win 15 rooms in a row without failing.", 15, 60, p => p.Stat(BestStreak)),

            A("tasks10", "TO-DO LIST", "Claim 10 daily tasks.", 10, 20, p => p.Stat(QuestsClaimed)),
            A("tasks50", "INBOX ZERO", "Claim 50 daily tasks.", 50, 60, p => p.Stat(QuestsClaimed)),

            A("days3", "REGULAR", "Play on 3 different days.", 3, 15, p => p.Stat(DaysPlayed)),
            A("days7", "FULL WEEK", "Play on 7 different days.", 7, 30, p => p.Stat(DaysPlayed)),
            A("days30", "EMPLOYEE OF THE MONTH", "Play on 30 different days.", 30, 100, p => p.Stat(DaysPlayed)),

            A("shots500", "BUSY BEE", "Take 500 shots.", 500, 20, p => p.Stat(Shots)),
            A("shots2000", "WORKAHOLIC", "Take 2,000 shots.", 2000, 50, p => p.Stat(Shots)),

            A("guide", "FIELD RESEARCH", "Meet every gadget in the office.", Gadgets.All.Length, 40, Met),
            A("clips", "BARGAIN HUNTER", "Buy something in the Clip Shop.", 1, 15, p => p.Stat(ClipBuys)),
            A("boardroom", "CLEAN DESK POLICY", "Beat a room on the top floor without an undo.", 1, 30, p => p.Stat(BossClean)),
            A("pinball", "PINBALL WIZARD", "Bounce your way to bed in one wild shot.", 1, 20, p => p.Stat(Pinball)),
            A("poke", "PLEASE STOP", "Poke your employee 50 times.", 50, 10, p => p.Stat(Pokes)),
        };

        public static AchievementDef Find(string key) => Array.Find(All, a => a.Key == key);

        public static int ValueOf(AchievementDef a, Progress p) => Math.Min(a.Target, Math.Max(0, a.Value(p)));
        public static bool Done(AchievementDef a, Progress p) => a.Value(p) >= a.Target;

        // Done and not yet claimed: what the badge counts.
        public static int Ready(Progress p)
        {
            int n = 0;
            foreach (var a in All) if (Done(a, p) && !p.AchievementClaimed(a.Key)) n++;
            return n;
        }

        // Newly done since the last check (each announced once).
        public static List<AchievementDef> Fresh(Progress p)
        {
            var list = new List<AchievementDef>();
            foreach (var a in All)
                if (Done(a, p) && !p.AchievementAnnounced(a.Key)) list.Add(a);
            return list;
        }
    }
}
