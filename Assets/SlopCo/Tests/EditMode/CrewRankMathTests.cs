using NUnit.Framework;
using SlopCo.Core;

namespace SlopCo.Tests.EditMode
{
    /// <summary>
    /// Pins the pure crew-rank tier math (CrewRankMath) — thresholds + tier boundaries + XP-to-next.
    /// Same shape as RunGradeTests / DailyModifierTests.
    /// </summary>
    public class CrewRankMathTests
    {
        [TestCase(0, 0)]
        [TestCase(1199, 0)]
        [TestCase(1200, 1)]
        [TestCase(3499, 1)]
        [TestCase(3500, 2)]
        [TestCase(7500, 3)]
        [TestCase(15000, 4)]
        [TestCase(999999, 4)]
        public void TierForXp_AtAndAroundBoundaries(int xp, int expectedTier)
        {
            Assert.AreEqual(expectedTier, CrewRankMath.TierForXp(xp));
        }

        [Test]
        public void TierForXp_IsMonotonic()
        {
            int prev = 0;
            for (int xp = 0; xp <= 20000; xp += 250)
            {
                int t = CrewRankMath.TierForXp(xp);
                Assert.GreaterOrEqual(t, prev);
                prev = t;
            }
        }

        [Test]
        public void XpToNext_DecreasesWithinTier_AndZeroAtMax()
        {
            Assert.AreEqual(1200, CrewRankMath.XpToNext(0));     // Intern → Driver
            Assert.AreEqual(1, CrewRankMath.XpToNext(1199));     // one off Driver
            Assert.AreEqual(2300, CrewRankMath.XpToNext(1200));  // Driver → Hauler (3500-1200)
            Assert.AreEqual(0, CrewRankMath.XpToNext(15000));    // already Slop Legend
            Assert.AreEqual(0, CrewRankMath.XpToNext(99999));    // beyond top
        }
    }
}
