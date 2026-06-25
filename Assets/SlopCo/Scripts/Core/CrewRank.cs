using UnityEngine;

namespace SlopCo.Core
{
    /// <summary>
    /// Persistent crew progression (local PlayerPrefs) — the cheapest meta-carrot: a rank number + title that
    /// climbs across runs without gating any content. Banks each finished run's chaos score into a cumulative
    /// XP total; <see cref="CrewRankMath"/> turns that into a tier. Same PlayerPrefs idiom as
    /// <see cref="BestRecords"/> / <see cref="SettingsManager"/>; no netcode (a co-op peer accrues their own).
    /// </summary>
    public static class CrewRank
    {
        private const string KXp = "slop.crew.xp";

        /// <summary>Cumulative chaos score banked across all finished runs.</summary>
        public static int Xp => PlayerPrefs.GetInt(KXp, 0);

        /// <summary>Current tier (0 = Intern … 4 = Slop Legend).</summary>
        public static int Tier => CrewRankMath.TierForXp(Xp);

        /// <summary>Bank a finished run's score. Returns true when the tier increased (drives the rank-up
        /// flourish). Negative scores are clamped so a rough run never subtracts progress.</summary>
        public static bool AddRun(int score)
        {
            int before = CrewRankMath.TierForXp(Xp);
            int next = Xp + Mathf.Max(0, score);
            PlayerPrefs.SetInt(KXp, next);
            PlayerPrefs.Save();
            return CrewRankMath.TierForXp(next) > before;
        }
    }
}
