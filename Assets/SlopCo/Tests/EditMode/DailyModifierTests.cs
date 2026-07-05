using System.Collections.Generic;
using NUnit.Framework;
using SlopCo.Gameplay;

namespace SlopCo.Tests.EditMode
{
    /// <summary>
    /// Pins the pure daily-modifier policy (DailyModifier) — deterministic roll + per-modifier effects.
    /// Same shape as RunGradeTests / MusicBusTests.
    /// </summary>
    public class DailyModifierTests
    {
        [TestCase(0)]
        [TestCase(1)]
        public void Roll_EarlyDays_AreClean(int day)
        {
            Assert.AreEqual(DayModifier.None, DailyModifier.Roll(day));
        }

        [Test]
        public void Roll_Day2Plus_IsAlwaysAnActiveModifier()
        {
            for (int day = 2; day <= 60; day++)
            {
                var m = DailyModifier.Roll(day);
                Assert.AreNotEqual(DayModifier.None, m, "day " + day + " should have a modifier");
                CollectionAssert.Contains(
                    new[]{ DayModifier.RushHour, DayModifier.DoubleLoad, DayModifier.HazardPay,
                           DayModifier.DemolitionDay, DayModifier.ScenicRoute, DayModifier.PaydayRun,
                           DayModifier.GreasyDay, DayModifier.RubberDay }, m);
            }
        }

        [Test]
        public void Roll_IsDeterministic()
        {
            for (int day = 0; day <= 30; day++)
                Assert.AreEqual(DailyModifier.Roll(day), DailyModifier.Roll(day));
        }

        [Test]
        public void Roll_ProducesVariety_AndAllEightAppear()
        {
            var seen = new HashSet<DayModifier>();
            // generous span — the lowbias32 hash is well-distributed, so all eight surface well within this range
            for (int day = 2; day <= 800; day++) seen.Add(DailyModifier.Roll(day));
            Assert.AreEqual(8, seen.Count, "all eight active modifiers should appear across the span");
            CollectionAssert.Contains(seen, DayModifier.RushHour);
            CollectionAssert.Contains(seen, DayModifier.DoubleLoad);
            CollectionAssert.Contains(seen, DayModifier.HazardPay);
            CollectionAssert.Contains(seen, DayModifier.DemolitionDay);
            CollectionAssert.Contains(seen, DayModifier.ScenicRoute);
            CollectionAssert.Contains(seen, DayModifier.PaydayRun);
            CollectionAssert.Contains(seen, DayModifier.GreasyDay);
            CollectionAssert.Contains(seen, DayModifier.RubberDay);
            CollectionAssert.DoesNotContain(seen, DayModifier.None); // None is reserved for day 1 only
        }

        [Test]
        public void Effects_MapPerModifier()
        {
            Assert.AreEqual(1f, DailyModifier.FuseMult(DayModifier.None));
            Assert.AreEqual(1.4f, DailyModifier.FuseMult(DayModifier.RushHour));
            Assert.AreEqual(1.2f, DailyModifier.FuseMult(DayModifier.HazardPay));
            Assert.AreEqual(0.7f, DailyModifier.FuseMult(DayModifier.ScenicRoute)); // longer fuse (the only <1)

            Assert.AreEqual(1, DailyModifier.CountBonus(DayModifier.DoubleLoad));
            Assert.AreEqual(1, DailyModifier.CountBonus(DayModifier.PaydayRun));
            Assert.AreEqual(0, DailyModifier.CountBonus(DayModifier.RushHour));
            Assert.AreEqual(0, DailyModifier.CountBonus(DayModifier.HazardPay));
            Assert.AreEqual(0, DailyModifier.CountBonus(DayModifier.DemolitionDay));

            Assert.AreEqual(1.5f, DailyModifier.PayoutMult(DayModifier.HazardPay));
            Assert.AreEqual(2f, DailyModifier.PayoutMult(DayModifier.PaydayRun));
            Assert.AreEqual(1.25f, DailyModifier.PayoutMult(DayModifier.DemolitionDay));
            Assert.AreEqual(1.1f, DailyModifier.PayoutMult(DayModifier.RushHour));
            Assert.AreEqual(0.8f, DailyModifier.PayoutMult(DayModifier.ScenicRoute));
            Assert.AreEqual(1f, DailyModifier.PayoutMult(DayModifier.None));

            // BlastMult — only Demolition Day supersizes the boom; everything else is neutral.
            Assert.AreEqual(1.6f, DailyModifier.BlastMult(DayModifier.DemolitionDay));
            Assert.AreEqual(1f, DailyModifier.BlastMult(DayModifier.None));
            Assert.AreEqual(1f, DailyModifier.BlastMult(DayModifier.RushHour));
            Assert.AreEqual(1f, DailyModifier.BlastMult(DayModifier.PaydayRun));

            // FrictionMult — only Greasy Day makes cargo slippery; everything else keeps full grip.
            Assert.AreEqual(0.05f, DailyModifier.FrictionMult(DayModifier.GreasyDay));
            Assert.AreEqual(1f, DailyModifier.FrictionMult(DayModifier.None));
            Assert.AreEqual(1f, DailyModifier.FrictionMult(DayModifier.RubberDay));
            Assert.AreEqual(1f, DailyModifier.FrictionMult(DayModifier.RushHour));

            // Bounciness — only Rubber Day makes cargo springy; everything else is inert.
            Assert.AreEqual(0.85f, DailyModifier.Bounciness(DayModifier.RubberDay));
            Assert.AreEqual(0f, DailyModifier.Bounciness(DayModifier.None));
            Assert.AreEqual(0f, DailyModifier.Bounciness(DayModifier.GreasyDay));
            Assert.AreEqual(0f, DailyModifier.Bounciness(DayModifier.DemolitionDay));

            // The two new physics-surface days are scalar-neutral (they change feel, not economy/timing).
            Assert.AreEqual(1f, DailyModifier.FuseMult(DayModifier.GreasyDay));
            Assert.AreEqual(1f, DailyModifier.FuseMult(DayModifier.RubberDay));
            Assert.AreEqual(0, DailyModifier.CountBonus(DayModifier.GreasyDay));
            Assert.AreEqual(0, DailyModifier.CountBonus(DayModifier.RubberDay));
            Assert.AreEqual(1f, DailyModifier.PayoutMult(DayModifier.GreasyDay));
            Assert.AreEqual(1f, DailyModifier.PayoutMult(DayModifier.RubberDay));
            Assert.AreEqual(1f, DailyModifier.BlastMult(DayModifier.GreasyDay));
            Assert.AreEqual(1f, DailyModifier.BlastMult(DayModifier.RubberDay));
        }

        [Test]
        public void NameKey_MapsEachModifier()
        {
            Assert.AreEqual("mod.rushhour", DailyModifier.NameKey(DayModifier.RushHour));
            Assert.AreEqual("mod.doubleload", DailyModifier.NameKey(DayModifier.DoubleLoad));
            Assert.AreEqual("mod.hazardpay", DailyModifier.NameKey(DayModifier.HazardPay));
            Assert.AreEqual("mod.demolition", DailyModifier.NameKey(DayModifier.DemolitionDay));
            Assert.AreEqual("mod.scenicroute", DailyModifier.NameKey(DayModifier.ScenicRoute));
            Assert.AreEqual("mod.payday", DailyModifier.NameKey(DayModifier.PaydayRun));
            Assert.AreEqual("mod.greasy", DailyModifier.NameKey(DayModifier.GreasyDay));
            Assert.AreEqual("mod.rubber", DailyModifier.NameKey(DayModifier.RubberDay));
            Assert.AreEqual("mod.none", DailyModifier.NameKey(DayModifier.None));
        }
    }
}
