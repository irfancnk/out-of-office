using System;

namespace OutOfOffice
{
    // Energy (OUT-48): what a play costs. The bar holds 30 and refills a point every 48 minutes, so a full bar comes
    // back once a day: six plays. A play starts when a room starts with none running, and lasts until it fails: every
    // win carries it on into the next room for free. Running out of shots, retrying, restarting, or leaving a room after
    // a shot ends it, which is what makes an undo or a continue worth paying for: they keep the play going. The first
    // rooms are free while the game is learned, and the Office Pass makes energy unlimited.
    public class Energy
    {
        public const int Max = 30, PlayCost = 5;
        public const int FirstRoom = Ads.FirstRoom;  // plays cost energy once this many rooms are open
        public const int Ceiling = 99;               // rewards may fill past the bar, up to this
        public static readonly TimeSpan Regen = TimeSpan.FromMinutes(48);

        const string PointsKey = "buca.energy", AnchorKey = "buca.energy.at", RunKey = "buca.run", AttemptKey = "buca.run.attempt";

        readonly IPrefs prefs;
        readonly Func<DateTime> utcNow;
        readonly Func<bool> unlimited;

        public Energy(IPrefs prefs, Func<DateTime> utcNow, Func<bool> unlimited)
        {
            this.prefs = prefs;
            this.utcNow = utcNow;
            this.unlimited = unlimited;
        }

        public bool Unlimited => unlimited();

        // Points now, the refill since the last change counted in. A new install starts full.
        public int Points
        {
            get
            {
                Settle();
                return Stored;
            }
        }

        public bool Full => Points >= Max;

        // How long until the next point, a whole play, and a full bar. Zero when already there.
        public TimeSpan UntilNext => Until(Math.Min(Max, Points + 1));
        public TimeSpan UntilPlay => Until(PlayCost);
        public TimeSpan UntilFull => Until(Max);

        TimeSpan Until(int target)
        {
            Settle();
            int stored = Stored;
            if (stored >= target) return TimeSpan.Zero;
            var left = Anchor + TimeSpan.FromTicks(Regen.Ticks * (target - stored)) - utcNow();
            return left > TimeSpan.Zero ? left : TimeSpan.Zero;
        }

        int Stored => prefs.GetInt(PointsKey, Max);

        DateTime Anchor
        {
            get
            {
                long.TryParse(prefs.GetString(AnchorKey, "0"), out long ticks);
                return new DateTime(Math.Max(0L, ticks), DateTimeKind.Utc);
            }
            set => prefs.SetString(AnchorKey, value.Ticks.ToString());
        }

        // Counts the points refilled since the anchor, keeping the part of a point already waited for.
        void Settle()
        {
            int stored = Stored;
            if (stored >= Max) return;
            var now = utcNow();
            var anchor = Anchor;
            if (anchor > now || anchor.Ticks == 0)
            {
                // A clock wound back past the anchor (or no anchor yet): start the wait again from now.
                Anchor = now;
                prefs.Save();
                return;
            }
            long gained = (now - anchor).Ticks / Regen.Ticks;
            if (gained <= 0) return;
            stored = (int)Math.Min(Max, stored + gained);
            prefs.SetInt(PointsKey, stored);
            Anchor = anchor + TimeSpan.FromTicks(Regen.Ticks * gained);
            prefs.Save();
        }

        public bool Spend(int amount, string sink)
        {
            if (amount <= 0) return true;
            Settle();
            int stored = Stored;
            if (stored < amount) return false;
            if (stored >= Max && stored - amount < Max) Anchor = utcNow();  // the refill starts once the bar is below full
            prefs.SetInt(PointsKey, stored - amount);
            prefs.Save();
            Analytics.Event("energy_spend", ("amount", amount), ("sink", sink), ("left", stored - amount));
            return true;
        }

        // Rewards (an ad, gems) add on top, past a full bar if need be.
        public void Add(int amount, string source)
        {
            if (amount <= 0) return;
            Settle();
            int next = Math.Min(Ceiling, Stored + amount);
            prefs.SetInt(PointsKey, next);  // below full, the wait already under way for the next point carries on
            prefs.Save();
            Analytics.Event("energy_earn", ("amount", amount), ("source", source), ("points", next));
        }

        // ---------- Plays ----------

        // A play is running: the next room starts free.
        public bool InPlay => prefs.GetInt(RunKey, 0) == 1;

        // A room of the running play has had its first shot and isn't finished: leaving it now ends the play.
        public bool AttemptOpen => prefs.GetInt(AttemptKey, 0) == 1;

        // Whether starting a room now is free: no charge here, a play already running, or unlimited energy.
        public bool StartsFree(bool counted) => !counted || Unlimited || InPlay;

        // leaving: the room being left has had a shot, so its play ends first and this start needs a new one.
        public bool CanStart(bool counted, bool leaving = false) => !counted || Unlimited || (InPlay && !leaving) || Points >= PlayCost;

        // Starts a room. Costs a play unless one is running (or the room is free). False when there's not enough energy.
        public bool Start(bool counted, string level)
        {
            if (StartsFree(counted)) return true;
            if (!Spend(PlayCost, "play")) return false;
            prefs.SetInt(RunKey, 1);
            prefs.SetInt(AttemptKey, 0);
            prefs.Save();
            Analytics.Event("play_start", ("level", level), ("points", Stored));
            return true;
        }

        // The first shot in a room of the running play.
        public void MarkAttempt()
        {
            if (!InPlay || AttemptOpen) return;
            prefs.SetInt(AttemptKey, 1);
            prefs.Save();
        }

        // A room won: the play carries on.
        public void FinishAttempt()
        {
            if (!AttemptOpen) return;
            prefs.SetInt(AttemptKey, 0);
            prefs.Save();
        }

        // Out of shots, a retry, or a room left after a shot: the play is over.
        public void EndPlay(string why, string level)
        {
            if (!InPlay && !AttemptOpen) return;
            prefs.SetInt(RunKey, 0);
            prefs.SetInt(AttemptKey, 0);
            prefs.Save();
            Analytics.Event("play_end", ("why", why), ("level", level));
        }

        // A continue after running out of shots: the same play, picked up where it failed.
        public void Revive()
        {
            prefs.SetInt(RunKey, 1);
            prefs.SetInt(AttemptKey, 1);
            prefs.Save();
        }

        // "47:59" under an hour, "3h 59m" above.
        public static string Clock(TimeSpan t)
        {
            if (t <= TimeSpan.Zero) return "0:00";
            if (t.TotalHours >= 1) return $"{(int)t.TotalHours}h {t.Minutes:00}m";
            return $"{t.Minutes}:{t.Seconds:00}";
        }
    }
}
