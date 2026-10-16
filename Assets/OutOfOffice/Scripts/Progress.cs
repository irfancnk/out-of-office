using System;
using System.Collections.Generic;
using UnityEngine;

namespace OutOfOffice
{
    // Storage behind progress, so the migration can be tested without touching real PlayerPrefs.
    public interface IPrefs
    {
        bool HasKey(string key);
        int GetInt(string key, int fallback);
        void SetInt(string key, int value);
        string GetString(string key, string fallback);
        void SetString(string key, string value);
        void Save();
    }

    public class UnityPrefs : IPrefs
    {
        public bool HasKey(string key) => PlayerPrefs.HasKey(key);
        public int GetInt(string key, int fallback) => PlayerPrefs.GetInt(key, fallback);
        public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);
        public string GetString(string key, string fallback) => PlayerPrefs.GetString(key, fallback);
        public void SetString(string key, string value) => PlayerPrefs.SetString(key, value);
        public void Save() => PlayerPrefs.Save();
    }

    public class MemoryPrefs : IPrefs
    {
        public readonly Dictionary<string, object> Values = new Dictionary<string, object>();
        public bool HasKey(string key) => Values.ContainsKey(key);
        public int GetInt(string key, int fallback) => Values.TryGetValue(key, out var v) && v is int i ? i : fallback;
        public void SetInt(string key, int value) => Values[key] = value;
        public string GetString(string key, string fallback) => Values.TryGetValue(key, out var v) && v is string s ? s : fallback;
        public void SetString(string key, string value) => Values[key] = value;
        public int Saves;
        public void Save() => Saves++;
    }

    // Player progress and preferences. Keys keep the original "buca." prefix so nothing existing breaks.
    // Version 1 (Buca) stored the current level index and stars per level index.
    // Version 2 (HoleShot, later Puffy Putt, now Out of Office) stores everything by stable level id. Version 1 keys are read once and left in place.
    //
    // Save keys. Never rename one: players would lose what it holds.
    //   buca.save                  save format version
    //   buca.level, buca.stars.N   version 1 only (current level index, stars by level index); read by Migrate
    //   buca.unlocked              how many levels are open
    //   buca.current               id of the level Play starts
    //   buca.best.<id>             best stars on a level
    //   buca.shots.<id>            fewest shots on a level
    //   buca.skin                  pre-wardrobe skin id; read when no outfit is saved yet
    //   buca.outfit.<slot>         equipped item per slot (employee, color, hat, face)
    //   buca.own.<item>            1 once a chest has given that item
    //   buca.chests.opened         chests opened so far (input for the chest pity timer, OUT-14)
    //   buca.quest.day             the day today's quest progress belongs to (yyyymmdd)
    //   buca.quest.pN, .cN         progress and claimed flag of today's quest N
    //   buca.daily.chest           the day the free daily chest was last claimed (yyyymmdd)
    //   buca.clock.seen            latest UTC time seen, in ticks; the clock never reads earlier than this
    //   buca.clock.rollbacks       how many launches found the device clock wound back
    //   buca.sfx, buca.music, buca.reduce   settings
    public class Progress
    {
        public const int Version = 2;

        // Level order of the Buca release, and where each of those levels lives now.
        static readonly string[] V1Names =
            { "Warm up", "Go around", "Pink hurts", "Speed pad", "Soft landing", "Timing", "Spinner", "Corner", "Portal", "Gate", "Jump", "Boing", "Pinball", "Track" };
        static readonly string[] V1ToId =
            { "first", "bank", "easy", "speedpad", "soft", "timing", "spinner", "corner", "portal", "door", "ramp", "rubber", "pinball", "track" };

        readonly IPrefs prefs;
        readonly IClock clock;
        long seenTicks;

        public Progress(IPrefs prefs, IClock clock = null)
        {
            this.prefs = prefs;
            this.clock = clock ?? new SystemClock();
            long.TryParse(prefs.GetString("buca.clock.seen", "0"), out seenTicks);
            savedTicks = seenTicks;
            Migrate();
        }

        // Writes everything to disk. The game calls it when the app is paused or quits, because the OS may kill a
        // backgrounded app without warning, and quest progress isn't saved on every shot.
        public void Flush()
        {
            if (seenTicks != savedTicks) SaveSeen();
            prefs.Save();
        }

        // ---------- Time ----------

        // Local time for daily resets, guarded against winding the device clock back to claim the same day's
        // rewards again: the clock never reads earlier than the latest time it has shown. UTC is compared, so
        // flying across time zones is not a rollback. Moving the clock forward only borrows future days, since
        // those days are then already claimed when they really come. Server time (OUT-5) replaces this.
        public DateTime Now
        {
            get
            {
                var utc = clock.UtcNow;
                long behind = seenTicks - utc.Ticks;
                if (behind > TrustLimit.Ticks)
                {
                    // Far behind: the earlier reading was a wrong clock (a dead battery, a bad manual setting), not
                    // this one. Trust this one, and close out the days claimed in that false future.
                    seenTicks = utc.Ticks;
                    SaveSeen();
                    CloseFutureDays(Quests.DayOf(clock.ToLocal(utc)));
                    Analytics.Event("clock_reanchor", ("behind_days", (int)TimeSpan.FromTicks(behind).TotalDays));
                }
                else if (behind > 0)
                {
                    // Counted once per launch, not on every read while the clock stays behind.
                    if (behind > RollbackSlack.Ticks && !heldBack)
                    {
                        heldBack = true;
                        prefs.SetInt("buca.clock.rollbacks", ClockRollbacks + 1);
                        Analytics.Event("clock_rollback", ("behind_minutes", (int)TimeSpan.FromTicks(behind).TotalMinutes));
                    }
                    utc = new DateTime(seenTicks, DateTimeKind.Utc);
                }
                else
                {
                    heldBack = false;
                    seenTicks = utc.Ticks;
                    if (seenTicks - savedTicks > TimeSpan.TicksPerMinute) SaveSeen();  // the HUD reads the time often
                }
                return clock.ToLocal(utc);
            }
        }

        static readonly TimeSpan RollbackSlack = TimeSpan.FromMinutes(5);  // network time corrections are not tampering
        static readonly TimeSpan TrustLimit = TimeSpan.FromDays(30);

        bool heldBack;
        long savedTicks;

        void SaveSeen()
        {
            savedTicks = seenTicks;
            prefs.SetString("buca.clock.seen", seenTicks.ToString());
        }

        // Daily state saved under a day later than today becomes today's, already used up, so going back to the
        // real date gives nothing extra and tomorrow works as normal.
        void CloseFutureDays(int today)
        {
            if (prefs.GetInt("buca.daily.chest", 0) > today) prefs.SetInt("buca.daily.chest", today);
            if (prefs.GetInt("buca.quest.day", 0) > today)
            {
                prefs.SetInt("buca.quest.day", today);
                var quests = Quests.For(today);
                for (int i = 0; i < Quests.PerDay; i++)
                {
                    prefs.SetInt("buca.quest.p" + i, quests[i].Target);
                    prefs.SetInt("buca.quest.c" + i, 1);
                }
            }
            prefs.Save();
        }

        public int Today => Quests.DayOf(Now);
        public int ClockRollbacks => prefs.GetInt("buca.clock.rollbacks", 0);

        public TimeSpan UntilReset
        {
            get
            {
                var now = Now;
                return now.Date.AddDays(1) - now;
            }
        }

        void Migrate()
        {
            int version = prefs.GetInt("buca.save", prefs.HasKey("buca.level") ? 1 : Version);
            if (version >= Version) return;

            // Stars earned in Buca carry over to the same level under its new id.
            int unlocked = 1;
            for (int i = 0; i < V1Names.Length; i++)
            {
                int stars = prefs.GetInt("buca.stars." + i, 0);
                int index = Levels.IndexOf(V1ToId[i]);
                if (stars <= 0 || index < 0) continue;
                string key = "buca.best." + V1ToId[i];
                prefs.SetInt(key, Mathf.Max(prefs.GetInt(key, 0), Mathf.Clamp(stars, 1, 3)));
                unlocked = Mathf.Max(unlocked, index + 2);
            }

            // Every level the player had reached stays open.
            int reached = Mathf.Clamp(prefs.GetInt("buca.level", 0), 0, V1Names.Length - 1);
            for (int i = 0; i <= reached; i++)
                unlocked = Mathf.Max(unlocked, Levels.IndexOf(V1ToId[i]) + 1);
            prefs.SetInt("buca.unlocked", Mathf.Clamp(Mathf.Max(unlocked, prefs.GetInt("buca.unlocked", 1)), 1, Levels.All.Length));
            prefs.SetString("buca.current", V1ToId[reached]);
            prefs.SetInt("buca.save", Version);
            prefs.Save();
        }

        public int Unlocked => Mathf.Clamp(prefs.GetInt("buca.unlocked", 1), 1, Levels.All.Length);

        public int CurrentIndex
        {
            get
            {
                int i = Levels.IndexOf(prefs.GetString("buca.current", Levels.All[0].Id));
                return Mathf.Clamp(i < 0 ? 0 : i, 0, Unlocked - 1);
            }
            set
            {
                prefs.SetString("buca.current", Levels.All[Mathf.Clamp(value, 0, Levels.All.Length - 1)].Id);
                prefs.Save();
            }
        }

        public int Best(string id) => prefs.GetInt("buca.best." + id, 0);
        public int BestShots(string id) => prefs.GetInt("buca.shots." + id, 0);

        public int TotalStars
        {
            get
            {
                int sum = 0;
                foreach (var l in Levels.All) sum += Best(l.Id);
                return sum;
            }
        }

        // Saves a finished level and opens the next one.
        public void Record(int index, int stars, int shots)
        {
            string id = Levels.All[index].Id;
            prefs.SetInt("buca.best." + id, Mathf.Max(Best(id), stars));
            int bestShots = BestShots(id);
            if (bestShots == 0 || shots < bestShots) prefs.SetInt("buca.shots." + id, shots);
            prefs.SetInt("buca.unlocked", Mathf.Clamp(Mathf.Max(Unlocked, index + 2), 1, Levels.All.Length));
            prefs.Save();
        }

        public int Skin
        {
            get => Skins.IndexOf(prefs.GetString("buca.skin", Skins.All[0].Id));
            set { prefs.SetString("buca.skin", Skins.All[value].Id); prefs.Save(); }
        }

        public bool SkinUnlocked(int i) => TotalStars >= Skins.All[i].StarsNeeded;

        // ---------- Wardrobe ----------

        // Star items unlock with total stars; chest items once a chest gives them.
        public bool Owns(CosmeticItem item)
        {
            if (item == null) return false;
            return item.FromChest ? prefs.GetInt("buca.own." + item.Id, 0) == 1 : TotalStars >= Mathf.Max(0, item.StarsNeeded);
        }

        public void Grant(CosmeticItem item)
        {
            prefs.SetInt("buca.own." + item.Id, 1);
            prefs.Save();
        }

        // What Puffy wears. An older save that only knew a skin wears that skin's color and hat.
        public Outfit Outfit
        {
            get
            {
                if (!prefs.HasKey("buca.outfit.color"))
                {
                    var o = Cosmetics.Default;
                    if (prefs.HasKey("buca.skin"))
                    {
                        var skin = Skins.All[Skins.IndexOf(prefs.GetString("buca.skin", Skins.All[0].Id))];
                        o.Color = "c." + skin.Id;
                        o.Hat = "h." + Cosmetics.HatId(skin.Accessory);
                    }
                    return o;
                }
                return new Outfit
                {
                    Employee = prefs.GetString("buca.outfit.employee", Cosmetics.Default.Employee),
                    Color = prefs.GetString("buca.outfit.color", Cosmetics.Default.Color),
                    Hat = prefs.GetString("buca.outfit.hat", Cosmetics.Default.Hat),
                    Face = prefs.GetString("buca.outfit.face", Cosmetics.Default.Face),
                };
            }
            set
            {
                prefs.SetString("buca.outfit.employee", value.Employee ?? Cosmetics.Default.Employee);
                prefs.SetString("buca.outfit.color", value.Color);
                prefs.SetString("buca.outfit.hat", value.Hat);
                prefs.SetString("buca.outfit.face", value.Face);
                prefs.Save();
            }
        }

        public void Equip(CosmeticItem item)
        {
            Outfit = Outfit.With(item);
        }

        // Rolls a chest and keeps what it gives. Returns the item and whether it was already owned.
        public CosmeticItem OpenChest(System.Random rng, out bool duplicate)
        {
            var item = Cosmetics.Roll(Owns, rng);
            duplicate = Owns(item);
            Grant(item);
            prefs.SetInt("buca.chests.opened", prefs.GetInt("buca.chests.opened", 0) + 1);
            prefs.Save();
            return item;
        }

        // ---------- Daily quests ----------

        void EnsureDay()
        {
            // Days only move forward. A saved day later than today (a clock wound back before this guard existed)
            // keeps its progress instead of handing out a fresh set.
            int today = Today;
            if (prefs.GetInt("buca.quest.day", 0) >= today) return;
            prefs.SetInt("buca.quest.day", today);
            for (int i = 0; i < Quests.PerDay; i++)
            {
                prefs.SetInt("buca.quest.p" + i, 0);
                prefs.SetInt("buca.quest.c" + i, 0);
            }
            prefs.Save();
        }

        public QuestDef[] TodayQuests
        {
            get { EnsureDay(); return Quests.For(prefs.GetInt("buca.quest.day", Today)); }
        }

        public int QuestProgress(int i) { EnsureDay(); return prefs.GetInt("buca.quest.p" + i, 0); }
        public bool QuestClaimed(int i) { EnsureDay(); return prefs.GetInt("buca.quest.c" + i, 0) == 1; }
        public bool QuestDone(int i) => QuestProgress(i) >= TodayQuests[i].Target;

        // Counts progress toward today's quests. Returns how many quests became complete with this event.
        public int Track(QuestKind kind, int amount = 1)
        {
            var quests = TodayQuests;
            int completed = 0;
            for (int i = 0; i < quests.Length; i++)
            {
                if (quests[i].Kind != kind) continue;
                int before = QuestProgress(i);
                if (before >= quests[i].Target) continue;
                int after = Mathf.Min(quests[i].Target, before + amount);
                prefs.SetInt("buca.quest.p" + i, after);
                if (after >= quests[i].Target) completed++;
            }
            if (completed > 0) prefs.Save();
            return completed;
        }

        public bool ClaimQuest(int i)
        {
            if (!QuestDone(i) || QuestClaimed(i)) return false;
            prefs.SetInt("buca.quest.c" + i, 1);
            prefs.Save();
            return true;
        }

        public bool DailyChestReady => prefs.GetInt("buca.daily.chest", 0) < Today;

        public bool ClaimDailyChest()
        {
            if (!DailyChestReady) return false;
            prefs.SetInt("buca.daily.chest", Today);
            prefs.Save();
            return true;
        }

        // Chests waiting on the quest screen: the free daily one plus completed, unclaimed quests.
        public int ChestsWaiting
        {
            get
            {
                int n = DailyChestReady ? 1 : 0;
                for (int i = 0; i < Quests.PerDay; i++) if (QuestDone(i) && !QuestClaimed(i)) n++;
                return n;
            }
        }

        public bool Sfx { get => prefs.GetInt("buca.sfx", 1) == 1; set { prefs.SetInt("buca.sfx", value ? 1 : 0); prefs.Save(); } }
        public bool Music { get => prefs.GetInt("buca.music", 1) == 1; set { prefs.SetInt("buca.music", value ? 1 : 0); prefs.Save(); } }
        public bool ReducedMotion { get => prefs.GetInt("buca.reduce", 0) == 1; set { prefs.SetInt("buca.reduce", value ? 1 : 0); prefs.Save(); } }
    }
}
