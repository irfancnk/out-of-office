using System;
using UnityEngine;

namespace OutOfOffice
{
    public enum QuestKind { FinishLevels, CollectStars, ParFinishes, HoleInOne, BumperHits, Shots }

    public class QuestDef
    {
        public QuestKind Kind;
        public int Target;
        public string Text;
    }

    // Three daily quests, picked from the local date, so everyone gets a fresh set at midnight with no server.
    public static class Quests
    {
        public const int PerDay = 3;

        static readonly QuestDef[] Pool =
        {
            new QuestDef { Kind = QuestKind.FinishLevels, Target = 3, Text = "Finish 3 rooms" },
            new QuestDef { Kind = QuestKind.CollectStars, Target = 8, Text = "Grab 8 coffees" },
            new QuestDef { Kind = QuestKind.ParFinishes, Target = 2, Text = "Beat par in 2 rooms" },
            new QuestDef { Kind = QuestKind.HoleInOne, Target = 1, Text = "Score a hole-in-one" },
            new QuestDef { Kind = QuestKind.BumperHits, Target = 10, Text = "Bounce off 10 chairs" },
            new QuestDef { Kind = QuestKind.Shots, Target = 25, Text = "Take 25 shots" },
        };

        // Today's date as yyyymmdd (local time).
        public static int Today => DayOf(DateTime.Now);

        public static int DayOf(DateTime t) => t.Year * 10000 + t.Month * 100 + t.Day;

        // A fixed, different trio for each day.
        public static QuestDef[] For(int day)
        {
            var rng = new System.Random(day * 7919 + 17);
            var picks = new QuestDef[PerDay];
            var used = new bool[Pool.Length];
            for (int i = 0; i < PerDay; i++)
            {
                int k;
                do k = rng.Next(Pool.Length); while (used[k]);
                used[k] = true;
                picks[i] = Pool[k];
            }
            return picks;
        }

        public static TimeSpan UntilReset()
        {
            var now = DateTime.Now;
            return now.Date.AddDays(1) - now;
        }
    }
}
