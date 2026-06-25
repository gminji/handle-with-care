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
                    new[]{ DayModifier.RushHour, DayModifier.DoubleLoad, DayModifier.HazardPay }, m);
            }
        }

        [Test]
        public void Roll_IsDeterministic()
        {
            for (int day = 0; day <= 30; day++)
                Assert.AreEqual(DailyModifier.Roll(day), DailyModifier.Roll(day));
        }

        [Test]
        public void Roll_ProducesVariety_AndAllThreeAppear()
        {
            var seen = new HashSet<DayModifier>();
            for (int day = 2; day <= 30; day++) seen.Add(DailyModifier.Roll(day));
            // every twist should show up across a month, and certainly more than one kind
            Assert.GreaterOrEqual(seen.Count, 2);
            CollectionAssert.Contains(seen, DayModifier.RushHour);
            CollectionAssert.Contains(seen, DayModifier.DoubleLoad);
            CollectionAssert.Contains(seen, DayModifier.HazardPay);
        }

        [Test]
        public void Effects_MapPerModifier()
        {
            Assert.AreEqual(1f, DailyModifier.FuseMult(DayModifier.None));
            Assert.AreEqual(1.4f, DailyModifier.FuseMult(DayModifier.RushHour));
            Assert.AreEqual(1.2f, DailyModifier.FuseMult(DayModifier.HazardPay));

            Assert.AreEqual(1, DailyModifier.CountBonus(DayModifier.DoubleLoad));
            Assert.AreEqual(0, DailyModifier.CountBonus(DayModifier.RushHour));
            Assert.AreEqual(0, DailyModifier.CountBonus(DayModifier.HazardPay));

            Assert.AreEqual(1.5f, DailyModifier.PayoutMult(DayModifier.HazardPay));
            Assert.AreEqual(1.1f, DailyModifier.PayoutMult(DayModifier.RushHour));
            Assert.AreEqual(1f, DailyModifier.PayoutMult(DayModifier.None));
        }

        [Test]
        public void NameKey_MapsEachModifier()
        {
            Assert.AreEqual("mod.rushhour", DailyModifier.NameKey(DayModifier.RushHour));
            Assert.AreEqual("mod.doubleload", DailyModifier.NameKey(DayModifier.DoubleLoad));
            Assert.AreEqual("mod.hazardpay", DailyModifier.NameKey(DayModifier.HazardPay));
            Assert.AreEqual("mod.none", DailyModifier.NameKey(DayModifier.None));
        }
    }
}
