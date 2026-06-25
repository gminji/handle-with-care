using NUnit.Framework;
using SlopCo.Gameplay;

namespace SlopCo.Tests.EditMode
{
    /// <summary>
    /// Verifies the pure run-scoring (RunGrade) without any NetworkManager/NetworkBehaviour — exactly the
    /// logic that CAN be statically verified in a no-runtime environment (same shape as QuotaSystemTests).
    /// Scores are computed by hand against the named weight/threshold constants.
    /// </summary>
    public class RunGradeTests
    {
        [Test]
        public void Evaluate_LongSurvivalRun_GradesS()
        {
            // 10*120 + 8*15 + 0 + 0 + 150 = 1470 >= ThreshS(1200)
            var v = RunGrade.Evaluate(deliveries: 8, delivered: 900, destroyed: 0,
                                      biggestSmash: 0, bestChain: 0, day: 10, survived: true);
            Assert.AreEqual(1470, v.Score);
            Assert.AreEqual(Grade.S, v.Grade);
        }

        [Test]
        public void Evaluate_FailedShortRun_GradesD()
        {
            // 1*120 = 120 < ThreshC(180)
            var v = RunGrade.Evaluate(0, 0, 0, 0, 0, day: 1, survived: false);
            Assert.AreEqual(120, v.Score);
            Assert.AreEqual(Grade.D, v.Grade);
        }

        [Test]
        public void Evaluate_ExactlyThreshC_GradesC()
        {
            // 1*120 + 4*15 = 180 == ThreshC → C (>=)
            var v = RunGrade.Evaluate(deliveries: 4, delivered: 0, destroyed: 0,
                                      biggestSmash: 0, bestChain: 0, day: 1, survived: false);
            Assert.AreEqual(180, v.Score);
            Assert.AreEqual(Grade.C, v.Grade);
        }

        [Test]
        public void Evaluate_JustBelowThreshC_GradesD()
        {
            // 1*120 + 3*15 = 165 < ThreshC(180) → D
            var v = RunGrade.Evaluate(3, 0, 0, 0, 0, day: 1, survived: false);
            Assert.AreEqual(165, v.Score);
            Assert.AreEqual(Grade.D, v.Grade);
        }

        [Test]
        public void Evaluate_ExactlyThreshB_GradesB()
        {
            // 3*120 + 6*15 = 360 + 90 = 450 == ThreshB → B
            var v = RunGrade.Evaluate(deliveries: 6, delivered: 0, destroyed: 0,
                                      biggestSmash: 0, bestChain: 0, day: 3, survived: false);
            Assert.AreEqual(450, v.Score);
            Assert.AreEqual(Grade.B, v.Grade);
        }

        [Test]
        public void Evaluate_NegativeInputs_ClampToZeroNoThrow()
        {
            var v = RunGrade.Evaluate(-3, -10, -100, -100, -2, day: -5, survived: false);
            Assert.AreEqual(0, v.Score);
            Assert.AreEqual(Grade.D, v.Grade);
        }

        [Test]
        public void Evaluate_SmashContribution_CappedAtSmashCap()
        {
            // Only the smash contributes; 100000/4 is clamped to SmashCap(250).
            var v = RunGrade.Evaluate(0, 0, 0, biggestSmash: 100000, bestChain: 0, day: 0, survived: false);
            Assert.AreEqual(RunGrade.SmashCap, v.Score);
        }

        [Test]
        public void Evaluate_ChainCountsFromTwo()
        {
            // bestChain=1 contributes nothing; bestChain=3 → (3-1)*40 = 80.
            var lo = RunGrade.Evaluate(0, 0, 0, 0, bestChain: 1, day: 0, survived: false);
            var hi = RunGrade.Evaluate(0, 0, 0, 0, bestChain: 3, day: 0, survived: false);
            Assert.AreEqual(0, lo.Score);
            Assert.AreEqual(80, hi.Score);
        }

        [Test]
        public void PickFlavor_ChainBeatsSmash()
        {
            // x4 chain AND a $300 smash → chain wins (highest priority).
            Assert.AreEqual(RunFlavor.Chainmaster,
                RunGrade.PickFlavor(deliveries: 10, destroyed: 500, biggestSmash: 300, bestChain: 4, day: 5, survived: true));
        }

        [Test]
        public void PickFlavor_DemolisherWhenBigSmashNoChain()
        {
            Assert.AreEqual(RunFlavor.Demolisher,
                RunGrade.PickFlavor(deliveries: 1, destroyed: 200, biggestSmash: 200, bestChain: 2, day: 1, survived: false));
        }

        [Test]
        public void PickFlavor_SurvivorWhenLongAndSurvived()
        {
            Assert.AreEqual(RunFlavor.Survivor,
                RunGrade.PickFlavor(deliveries: 1, destroyed: 0, biggestSmash: 0, bestChain: 0, day: 5, survived: true));
        }

        [Test]
        public void PickFlavor_HustlerWhenManyDeliveries()
        {
            Assert.AreEqual(RunFlavor.Hustler,
                RunGrade.PickFlavor(deliveries: 5, destroyed: 0, biggestSmash: 0, bestChain: 0, day: 1, survived: false));
        }

        [Test]
        public void PickFlavor_RookieFallback()
        {
            Assert.AreEqual(RunFlavor.Rookie,
                RunGrade.PickFlavor(deliveries: 1, destroyed: 0, biggestSmash: 0, bestChain: 0, day: 1, survived: false));
        }

        [Test]
        public void Letter_MapsEachGrade()
        {
            Assert.AreEqual("S", RunGrade.Letter(Grade.S));
            Assert.AreEqual("A", RunGrade.Letter(Grade.A));
            Assert.AreEqual("B", RunGrade.Letter(Grade.B));
            Assert.AreEqual("C", RunGrade.Letter(Grade.C));
            Assert.AreEqual("D", RunGrade.Letter(Grade.D));
        }
    }
}
