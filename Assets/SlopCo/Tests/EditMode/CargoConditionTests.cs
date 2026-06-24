using NUnit.Framework;
using SlopCo.Cargo;

namespace SlopCo.Tests.EditMode
{
    /// <summary>
    /// Verifies the pure cargo condition + payout arithmetic (CargoMath) without NGO types.
    /// </summary>
    public class CargoConditionTests
    {
        [Test]
        public void DamageFromImpact_BelowThreshold_IsZero()
        {
            Assert.AreEqual(0f, CargoMath.DamageFromImpact(1.0f), 1e-5f);
            Assert.AreEqual(0f, CargoMath.DamageFromImpact(1.5f), 1e-5f);
        }

        [Test]
        public void DamageFromImpact_ScalesAboveThreshold()
        {
            // (10 - 1.5) * 0.025 = 0.2125
            Assert.AreEqual(0.2125f, CargoMath.DamageFromImpact(10f), 1e-4f);
        }

        [Test]
        public void DamageFromImpact_ClampedToOne()
        {
            Assert.AreEqual(1f, CargoMath.DamageFromImpact(10000f), 1e-5f);
        }

        [Test]
        public void DamageFromImpact_ToughnessReducesDamage()
        {
            float soft = CargoMath.DamageFromImpact(10f, 1f);
            float tough = CargoMath.DamageFromImpact(10f, 2f);
            Assert.Less(tough, soft);
        }

        [Test]
        public void ApplyDamage_ClampsToZero()
        {
            Assert.AreEqual(0f, CargoMath.ApplyDamage(0.1f, 0.5f), 1e-5f);
            Assert.AreEqual(0.5f, CargoMath.ApplyDamage(1f, 0.5f), 1e-5f);
        }

        [Test]
        public void Value_ScalesWithCondition()
        {
            Assert.AreEqual(100, CargoMath.Value(100, 1f));
            Assert.AreEqual(50, CargoMath.Value(100, 0.5f));
            Assert.AreEqual(0, CargoMath.Value(100, 0f));
        }

        [Test]
        public void Payout_AddsSpeedBonus()
        {
            Assert.AreEqual(150, CargoMath.Payout(100, 0.5f));
            Assert.AreEqual(100, CargoMath.Payout(100, 0f));
        }

        [Test]
        public void SpeedBonus_IsFractionOfTimeRemaining_Clamped()
        {
            Assert.AreEqual(0.5f, CargoMath.SpeedBonus(90f, 180f), 1e-5f);
            Assert.AreEqual(1f, CargoMath.SpeedBonus(250f, 180f), 1e-5f); // clamped
            Assert.AreEqual(0f, CargoMath.SpeedBonus(0f, 180f), 1e-5f);
        }
    }
}
