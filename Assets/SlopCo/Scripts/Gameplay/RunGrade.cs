using System;

namespace SlopCo.Gameplay
{
    /// <summary>End-of-run letter grade. Lower value = better, so <see cref="Core.BestRecords"/> stores
    /// the best grade as the minimum (mirrors its "raise to a new best" idiom, inverted).</summary>
    public enum Grade : byte { S = 0, A = 1, B = 2, C = 3, D = 4 }

    /// <summary>Headline angle: whichever stat stood out most this run, so <c>ResultsUI</c> can pick the
    /// matching localized punchline ("x4 chain!" / "Demolished $1,240!").</summary>
    public enum RunFlavor : byte { Rookie = 0, Survivor = 1, Demolisher = 2, Chainmaster = 3, Hustler = 4 }

    /// <summary>One scored verdict: grade + raw score + headline angle. Readonly struct = zero alloc.</summary>
    public readonly struct RunVerdict
    {
        public readonly Grade Grade;
        public readonly int Score;
        public readonly RunFlavor Flavor;
        public RunVerdict(Grade g, int s, RunFlavor f) { Grade = g; Score = s; Flavor = f; }
    }

    /// <summary>
    /// Pure run scoring — NO UnityEngine / NGO dependency, so it is unit-testable in EditMode without a
    /// NetworkManager (exactly like <see cref="QuotaMath"/> / <c>CargoMath</c>). Turns the already-tallied
    /// <c>RunStats</c> + survived day into a single shareable "CHAOS RATING" the end-of-run card shows.
    /// All thresholds/weights live here as the single source of truth, pinned by tests.
    /// </summary>
    public static class RunGrade
    {
        // ── Score weights ──
        public const int PerDay        = 120;  // survived days = the score's spine
        public const int PerDelivery   = 15;   // throughput
        public const int PerChainStep  = 40;   // best chain (clip highlight)
        public const int SmashDivisor  = 4;    // biggest single smash / 4 (comedy highlight)
        public const int SmashCap      = 250;  // ceiling on the smash contribution (no runaway)
        public const int SurvivedBonus = 150;  // finished the day without being FIRED

        // ── Grade thresholds (score >= threshold → that grade) ──
        public const int ThreshS = 1200;
        public const int ThreshA = 800;
        public const int ThreshB = 450;
        public const int ThreshC = 180;
        // below ThreshC = D

        /// <summary>This run's stats → grade/score/headline angle. Inputs are already-tallied values
        /// (RunStats + replicated RoundNumber); negatives are clamped so the math never throws.</summary>
        public static RunVerdict Evaluate(
            int deliveries, int delivered, int destroyed,
            int biggestSmash, int bestChain, int day, bool survived)
        {
            deliveries   = Math.Max(0, deliveries);
            biggestSmash = Math.Max(0, biggestSmash);
            bestChain    = Math.Max(0, bestChain);
            day          = Math.Max(0, day);

            int smashPts = Math.Min(SmashCap, biggestSmash / SmashDivisor);
            int chainPts = Math.Max(0, bestChain - 1) * PerChainStep; // chains only count from 2
            int score = day * PerDay
                      + deliveries * PerDelivery
                      + chainPts
                      + smashPts
                      + (survived ? SurvivedBonus : 0);

            Grade tier = score >= ThreshS ? Grade.S
                       : score >= ThreshA ? Grade.A
                       : score >= ThreshB ? Grade.B
                       : score >= ThreshC ? Grade.C
                       :                     Grade.D;

            return new RunVerdict(tier, score,
                PickFlavor(deliveries, destroyed, biggestSmash, bestChain, day, survived));
        }

        /// <summary>Headline angle: the most brag/laugh-worthy stat wins (chain > big smash > long
        /// survival > throughput > rookie fallback).</summary>
        public static RunFlavor PickFlavor(
            int deliveries, int destroyed, int biggestSmash, int bestChain, int day, bool survived)
        {
            if (bestChain >= 3)       return RunFlavor.Chainmaster; // x3+ chain = the best clip
            if (biggestSmash >= 150)  return RunFlavor.Demolisher;  // a big single smash = comedy
            if (survived && day >= 3) return RunFlavor.Survivor;    // lasted a while
            if (deliveries >= 4)      return RunFlavor.Hustler;     // diligent hauling
            return RunFlavor.Rookie;                                // short / failed run
        }

        /// <summary>Display letter (no localization — grade letters are universal).</summary>
        public static string Letter(Grade t) => t switch
        {
            Grade.S => "S", Grade.A => "A", Grade.B => "B",
            Grade.C => "C", _ => "D",
        };
    }
}
