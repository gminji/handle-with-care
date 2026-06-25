namespace SlopCo.Core
{
    /// <summary>
    /// Pure crew-rank tier math — NO UnityEngine dependency, so it is EditMode-testable like <c>RunGrade</c>
    /// / <c>QuotaMath</c>. Cumulative run score → a tier (Intern → Driver → Hauler → Veteran Hauler →
    /// Slop Legend). Thresholds are the single source of truth, pinned by tests. The PlayerPrefs-backed
    /// accumulation lives in <see cref="CrewRank"/>.
    /// </summary>
    public static class CrewRankMath
    {
        /// <summary>Cumulative-XP entry point for each tier (index 0 = Intern). Tuned so the first rank
        /// arrives in a run or two and Slop Legend takes a real grind.</summary>
        public static readonly int[] Thresholds = { 0, 1200, 3500, 7500, 15000 };

        /// <summary>Highest tier whose threshold the XP has reached (clamped to the top tier).</summary>
        public static int TierForXp(int xp)
        {
            int t = 0;
            for (int i = 0; i < Thresholds.Length; i++)
                if (xp >= Thresholds[i]) t = i;
            return t;
        }

        /// <summary>XP remaining to the next tier, or 0 when already at the top tier.</summary>
        public static int XpToNext(int xp)
        {
            int t = TierForXp(xp);
            return t >= Thresholds.Length - 1 ? 0 : Thresholds[t + 1] - xp;
        }
    }
}
