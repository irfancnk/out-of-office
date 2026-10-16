using UnityEngine;

namespace OutOfOffice
{
    // Achievements (OUT-9): the counters the game keeps for them, a toast for each one earned, and claiming the gems.
    public partial class OutOfOfficeGame
    {
        const string Streak = "streak";  // wins in a row without a fail; its best is Achievements.BestStreak

        void WireAchievements()
        {
            hud.AchievementValue = a => Achievements.ValueOf(a, progress);
            hud.AchievementDone = a => Achievements.Done(a, progress);
            hud.AchievementClaimed = a => progress.AchievementClaimed(a.Key);
            hud.ClaimAchievement = a =>
            {
                if (!progress.ClaimAchievement(a.Key)) return;
                sound.Play(Sfx.Reward, 0.6f, 1.1f, 0.8f);
                Haptics.Play(Haptics.Style.Success);
                hud.RefreshAchievements();
                RefreshBadges();
                hud.Celebrate("trophy", $"+{a.Gems} GEMS", Palette.Gold);
            };
        }

        // Counts an event toward achievements, then announces anything it completed.
        void CountStat(string key, int n = 1)
        {
            progress.BumpStat(key, n);
            CheckAchievements();
        }

        // A toast for each newly done achievement, and the badges.
        void CheckAchievements()
        {
            foreach (var a in Achievements.Fresh(progress))
            {
                progress.AnnounceAchievement(a.Key);
                hud.Toast("trophy", "ACHIEVEMENT!", a.Name);
                Analytics.Event("achievement", ("achievement", a.Key));
            }
            RefreshBadges();
        }

        // The tasks tab counts chests waiting and achievements to claim; the achievements button its own.
        void RefreshBadges()
        {
            int ready = Achievements.Ready(progress);
            hud.SetQuestBadge(progress.ChestsWaiting + ready);
            hud.SetAchievementsBadge(ready);
        }

        // A room beaten: streaks, par, holes in one, a clean top floor.
        void CountWinStats()
        {
            if (levelIndex < 0) return;
            int streak = progress.Stat(Streak) + 1;
            progress.SetStat(Streak, streak);
            progress.StatAtLeast(Achievements.BestStreak, streak);
            if (shots <= Def.Par) progress.BumpStat(Achievements.ParFinish);
            if (shots == 1 && doOvers == 0) progress.BumpStat(Achievements.HoleInOne);
            if (Themes.ChapterOf(levelIndex) == Themes.ChapterStart.Length - 2 && undos == 0) progress.BumpStat(Achievements.BossClean);
            CheckAchievements();
        }

        void BreakStreak() => progress.SetStat(Streak, 0);
    }
}
