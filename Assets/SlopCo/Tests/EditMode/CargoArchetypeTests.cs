using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using SlopCo.Cargo;

namespace SlopCo.Tests.EditMode
{
    /// <summary>
    /// Pins the pure cargo-archetype policy (CargoArchetypeTable) — deterministic roll + per-archetype effects.
    /// Same shape as DailyModifierTests / RunGradeTests. Guards the backward-compat contract that Standard AND
    /// Slippery never override mass (the bomb prefab ships TwoPerson; forcing OneHand would regress co-op).
    /// </summary>
    public class CargoArchetypeTests
    {
        [TestCase(0)]
        [TestCase(1)]
        public void Roll_EarlyDays_AllStandard(int day)
        {
            for (int i = 0; i < 6; i++)
                Assert.AreEqual(CargoArchetype.Standard, CargoArchetypeTable.Roll(day, i), "day " + day + " index " + i);
        }

        [Test]
        public void Roll_IsDeterministic()
        {
            for (int day = 0; day <= 30; day++)
                for (int i = 0; i < 6; i++)
                    Assert.AreEqual(CargoArchetypeTable.Roll(day, i), CargoArchetypeTable.Roll(day, i));
        }

        [Test]
        public void Roll_ProducesAllFour_AcrossSpan()
        {
            var seen = new HashSet<CargoArchetype>();
            for (int day = 2; day <= 800; day++)
                for (int i = 0; i < 6; i++)
                    seen.Add(CargoArchetypeTable.Roll(day, i));
            Assert.AreEqual(4, seen.Count, "all four archetypes should appear across the span");
        }

        [Test]
        public void Roll_EscalatesWithDay()
        {
            // "Hardness" = share of non-Standard archetypes. Later days should be harder on average.
            float early = HardShare(2, 3);
            float late = HardShare(12, 15);
            Assert.Less(early, late, "later days should roll harder archetypes more often (" + early + " vs " + late + ")");
        }

        private static float HardShare(int dayLo, int dayHi)
        {
            int hard = 0, total = 0;
            for (int day = dayLo; day <= dayHi; day++)
                for (int i = 0; i < 200; i++) // index sweep gives a stable sample per day
                {
                    total++;
                    if (CargoArchetypeTable.Roll(day, i) != CargoArchetype.Standard) hard++;
                }
            return (float)hard / total;
        }

        [Test]
        public void FuseMult_MapsPerArchetype()
        {
            Assert.AreEqual(1.6f, CargoArchetypeTable.FuseMult(CargoArchetype.Volatile));
            Assert.AreEqual(1f, CargoArchetypeTable.FuseMult(CargoArchetype.Standard));
            Assert.AreEqual(1f, CargoArchetypeTable.FuseMult(CargoArchetype.Slippery));
            Assert.AreEqual(1f, CargoArchetypeTable.FuseMult(CargoArchetype.Heavy));
        }

        [Test]
        public void FrictionMult_MapsPerArchetype()
        {
            Assert.AreEqual(0.10f, CargoArchetypeTable.FrictionMult(CargoArchetype.Slippery));
            Assert.AreEqual(1f, CargoArchetypeTable.FrictionMult(CargoArchetype.Standard));
            Assert.AreEqual(1f, CargoArchetypeTable.FrictionMult(CargoArchetype.Volatile));
            Assert.AreEqual(1f, CargoArchetypeTable.FrictionMult(CargoArchetype.Heavy));
        }

        [Test]
        public void MassOverride_OnlyVolatileAndHeavy()
        {
            // Backward-compat contract: Standard AND Slippery must NOT touch mass (bomb prefab ships TwoPerson).
            Assert.IsFalse(CargoArchetypeTable.MassOverride(CargoArchetype.Standard).HasValue);
            Assert.IsFalse(CargoArchetypeTable.MassOverride(CargoArchetype.Slippery).HasValue);
            Assert.AreEqual(CargoMassClass.OneHand, CargoArchetypeTable.MassOverride(CargoArchetype.Volatile));
            Assert.AreEqual(CargoMassClass.TwoPerson, CargoArchetypeTable.MassOverride(CargoArchetype.Heavy));
        }

        [Test]
        public void ScaleMult_HeavyIsLarger()
        {
            Assert.AreEqual(1.25f, CargoArchetypeTable.ScaleMult(CargoArchetype.Heavy));
            Assert.AreEqual(1f, CargoArchetypeTable.ScaleMult(CargoArchetype.Standard));
            Assert.AreEqual(1f, CargoArchetypeTable.ScaleMult(CargoArchetype.Volatile));
            Assert.AreEqual(1f, CargoArchetypeTable.ScaleMult(CargoArchetype.Slippery));
        }

        [Test]
        public void TintColor_StandardIsTransparent()
        {
            Assert.Less(CargoArchetypeTable.TintColor(CargoArchetype.Standard).a, 0.01f);
            Assert.Greater(CargoArchetypeTable.TintColor(CargoArchetype.Volatile).a, 0.99f);
            Assert.Greater(CargoArchetypeTable.TintColor(CargoArchetype.Slippery).a, 0.99f);
            Assert.Greater(CargoArchetypeTable.TintColor(CargoArchetype.Heavy).a, 0.99f);
        }

        [Test]
        public void NameKey_MapsEach()
        {
            Assert.AreEqual("cargo.standard", CargoArchetypeTable.NameKey(CargoArchetype.Standard));
            Assert.AreEqual("cargo.volatile", CargoArchetypeTable.NameKey(CargoArchetype.Volatile));
            Assert.AreEqual("cargo.slippery", CargoArchetypeTable.NameKey(CargoArchetype.Slippery));
            Assert.AreEqual("cargo.heavy", CargoArchetypeTable.NameKey(CargoArchetype.Heavy));
        }
    }
}
