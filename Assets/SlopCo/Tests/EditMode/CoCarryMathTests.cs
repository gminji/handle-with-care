using NUnit.Framework;
using SlopCo.Cargo;

namespace SlopCo.Tests.EditMode
{
    /// <summary>
    /// Verifies the pure crew-scaled co-carry math (CoCarryMath) without any UnityEngine runtime.
    /// Constants are passed explicitly so the tests are independent of GameConstants tuning
    /// (DashStaminaTests-style local fixture).
    /// </summary>
    public class CoCarryMathTests
    {
        private const float Tol = 1e-3f;

        // Local fixtures mirroring GameConstants' co-carry section (not tied to GameConstants tuning).
        private const float LoadPer = 0.65f;
        private const float FullGrip = 0.72f;
        private const float Floor = 0.30f;
        private const float Bonus = 0.15f;
        private const float Base = 0.60f;
        private const float Under = 0.28f;
        private const int MaxCrew = 4;
        private const float Ceil = 0.9f;

        private static float Load(int baseCrew, int crewSize, int handleCount, bool solo = false, int maxCrew = MaxCrew)
            => CoCarryMath.LoadCrew(baseCrew, crewSize, handleCount, solo, maxCrew, LoadPer);

        private static float Lift(int grabbers, float loadCrew)
            => CoCarryMath.LiftStrength(grabbers, loadCrew, Under);

        private static float Haul(int grabbers, float loadCrew)
            => CoCarryMath.HaulSpeedFactor(grabbers, loadCrew, Base, FullGrip, Floor, Bonus);

        // ── LoadCrew ──

        [Test]
        public void Load_Solo_IsOne_ForTwoPersonItem()
        {
            Assert.That(Load(2, 4, 4, solo: true), Is.EqualTo(1f).Within(Tol));
        }

        [Test]
        public void Load_OneHandItem_IsOne_AtEveryCrew()
        {
            for (int crew = 1; crew <= 4; crew++)
                Assert.That(Load(1, crew, 1), Is.EqualTo(1f).Within(Tol), "crew " + crew);
        }

        [Test]
        public void Load_TwoPersonItem_Crew1To4()
        {
            Assert.That(Load(2, 1, 4), Is.EqualTo(1.00f).Within(Tol));
            Assert.That(Load(2, 2, 4), Is.EqualTo(2.00f).Within(Tol));
            Assert.That(Load(2, 3, 4), Is.EqualTo(2.65f).Within(Tol));
            Assert.That(Load(2, 4, 4), Is.EqualTo(3.30f).Within(Tol));
        }

        [Test]
        public void Load_ClampedByHandleCount()
        {
            Assert.That(Load(2, 4, 2), Is.EqualTo(2.00f).Within(Tol));
        }

        [Test]
        public void Load_HandleCountBelowBaseCrew_DoesNotDropBelowBaseCrew()
        {
            Assert.That(Load(2, 4, 1), Is.EqualTo(2.00f).Within(Tol));
        }

        [Test]
        public void Load_ClampsCrewAboveMaxPlayers()
        {
            Assert.That(Load(2, 9, 4), Is.EqualTo(Load(2, 4, 4)).Within(Tol));
        }

        [Test]
        public void Load_LastPlayerStanding_FallsBackToSolo()
        {
            Assert.That(Load(2, 1, 4), Is.EqualTo(1f).Within(Tol));
        }

        // ── LiftStrength ──

        [Test]
        public void Lift_FirstGrabber_IsExactlyLegacy0p28()
        {
            Assert.That(Lift(1, 2.00f), Is.EqualTo(0.280f).Within(Tol));
            Assert.That(Lift(1, 2.65f), Is.EqualTo(0.280f).Within(Tol));
            Assert.That(Lift(1, 3.30f), Is.EqualTo(0.280f).Within(Tol));
        }

        [Test]
        public void Lift_Crew2_MatchesLegacyBinary()
        {
            Assert.That(Lift(1, 2.00f), Is.EqualTo(0.280f).Within(Tol));
            Assert.That(Lift(2, 2.00f), Is.EqualTo(1.000f).Within(Tol));
        }

        [Test]
        public void Lift_Crew3_Table()
        {
            Assert.That(Lift(1, 2.65f), Is.EqualTo(0.280f).Within(Tol));
            Assert.That(Lift(2, 2.65f), Is.EqualTo(0.716f).Within(Tol));
            Assert.That(Lift(3, 2.65f), Is.EqualTo(1.000f).Within(Tol));
        }

        [Test]
        public void Lift_Crew4_Table()
        {
            Assert.That(Lift(1, 3.30f), Is.EqualTo(0.280f).Within(Tol));
            Assert.That(Lift(2, 3.30f), Is.EqualTo(0.593f).Within(Tol));
            Assert.That(Lift(3, 3.30f), Is.EqualTo(0.906f).Within(Tol));
            Assert.That(Lift(4, 3.30f), Is.EqualTo(1.000f).Within(Tol));
        }

        [Test]
        public void Lift_SoloAndOneHand_AreOne()
        {
            Assert.That(Lift(1, 1.00f), Is.EqualTo(1.000f).Within(Tol));
        }

        // ── HaulSpeedFactor ──

        [Test]
        public void HaulSpeed_Solo_MatchesLegacy0p600()
        {
            Assert.That(Haul(1, 1.00f), Is.EqualTo(0.600f).Within(Tol));
        }

        [Test]
        public void HaulSpeed_OneHandItem_WithFourGrabbers_DoesNotExceedLegacySpeed()
        {
            for (int g = 1; g <= 4; g++)
                Assert.That(Haul(g, 1.00f), Is.EqualTo(0.600f).Within(Tol), "grabbers " + g);
        }

        [Test]
        public void HaulSpeed_LastPlayerStanding_MatchesLegacy0p600()
        {
            Assert.That(Haul(1, 1.00f), Is.EqualTo(0.600f).Within(Tol));
        }

        [Test]
        public void HaulSpeed_Crew2_Table()
        {
            Assert.That(Haul(1, 2.00f), Is.EqualTo(0.468f).Within(Tol));
            Assert.That(Haul(2, 2.00f), Is.EqualTo(0.720f).Within(Tol));
        }

        [Test]
        public void HaulSpeed_Crew3_Table()
        {
            Assert.That(Haul(1, 2.65f), Is.EqualTo(0.406f).Within(Tol));
            Assert.That(Haul(2, 2.65f), Is.EqualTo(0.596f).Within(Tol));
            Assert.That(Haul(3, 2.65f), Is.EqualTo(0.734f).Within(Tol));
        }

        [Test]
        public void HaulSpeed_Crew4_Table()
        {
            Assert.That(Haul(1, 3.30f), Is.EqualTo(0.369f).Within(Tol));
            Assert.That(Haul(2, 3.30f), Is.EqualTo(0.521f).Within(Tol));
            Assert.That(Haul(3, 3.30f), Is.EqualTo(0.674f).Within(Tol));
            Assert.That(Haul(4, 3.30f), Is.EqualTo(0.743f).Within(Tol));
        }

        [Test]
        public void HaulSpeed_Crew2_FullGrip_IsFasterThanLegacy()
        {
            Assert.That(Haul(2, 2.00f), Is.GreaterThan(0.600f));
        }

        [Test]
        public void HaulSpeed_SameTwoGrabbers_DecreasesAsCrewGrows()
        {
            float crew2 = Haul(2, 2.00f);
            float crew3 = Haul(2, 2.65f);
            float crew4 = Haul(2, 3.30f);
            Assert.That(crew2, Is.GreaterThan(crew3));
            Assert.That(crew3, Is.GreaterThan(crew4));
        }

        [Test]
        public void HaulSpeed_MonotoneInGrabbers_Property()
        {
            for (int crew = 1; crew <= 4; crew++)
            {
                float loadCrew = Load(2, crew, 4);
                int maxG = crew < 4 ? crew : 4;
                for (int g = 1; g < maxG; g++)
                {
                    float lower = Haul(g, loadCrew);
                    float upper = Haul(g + 1, loadCrew);
                    Assert.That(upper, Is.GreaterThanOrEqualTo(lower), $"crew={crew} g={g}->{g + 1}");
                }
            }
        }

        [Test]
        public void HaulSpeed_OverCrewRatio_Saturates()
        {
            float saturated = Haul(40, 2f);
            float rMax = (2f + 1f) / 2f;
            float expected = FullGrip * (1f + Bonus * (rMax - 1f));
            Assert.That(saturated, Is.EqualTo(expected).Within(Tol));
        }

        [Test]
        public void HaulSpeed_ZeroGrabbers_IsNeutralOne()
        {
            Assert.That(Haul(0, 2f), Is.EqualTo(1f).Within(Tol));
        }

        // ── ComposedCarryMult ──

        [Test]
        public void Composed_WithForklift_RespectsCeiling()
        {
            Assert.That(CoCarryMath.ComposedCarryMult(0.743f, 1.50f, Ceil), Is.EqualTo(0.9f).Within(Tol));
        }

        [Test]
        public void Composed_NeverExceedsCeiling()
        {
            float[] augs = { 1.0f, 1.35f, 1.50f, 1.65f, 2.15f };
            for (int crew = 1; crew <= 4; crew++)
            {
                float loadCrew = Load(2, crew, 4);
                int maxG = crew < 4 ? crew : 4;
                for (int g = 1; g <= maxG; g++)
                {
                    float haul = Haul(g, loadCrew);
                    foreach (var aug in augs)
                    {
                        float composed = CoCarryMath.ComposedCarryMult(haul, aug, Ceil);
                        Assert.That(composed, Is.LessThanOrEqualTo(Ceil), $"crew={crew} g={g} aug={aug}");
                    }
                }
            }
        }

        [Test]
        public void Composed_BelowCeiling_IsPlainProduct()
        {
            Assert.That(CoCarryMath.ComposedCarryMult(0.468f, 1.0f, Ceil), Is.EqualTo(0.468f).Within(Tol));
        }

        // ── RampToward ──

        [Test]
        public void Ramp_ReachesTargetWithinHalfSecond()
        {
            float current = 0.216f;
            const float target = 0.743f;
            const float dt = 0.02f;
            const float rate = 1.2f;
            int ticks = 0;
            while (current != target && ticks < 1000)
            {
                current = CoCarryMath.RampToward(current, target, dt, rate);
                ticks++;
            }
            Assert.That(ticks, Is.LessThanOrEqualTo(25));
            Assert.That(current, Is.EqualTo(target).Within(Tol));
        }

        [Test]
        public void Ramp_NeverOvershoots()
        {
            float r = CoCarryMath.RampToward(0.2f, 0.25f, 1f, 1.2f);
            Assert.That(r, Is.EqualTo(0.25f).Within(Tol));
        }

        [Test]
        public void Ramp_ZeroDt_SnapsToTarget()
        {
            Assert.That(CoCarryMath.RampToward(0.2f, 0.8f, 0f, 1.2f), Is.EqualTo(0.8f).Within(Tol));
        }

        // ── PickFreeHandle ──

        [Test]
        public void PickFreeHandle_PreferredIsFree_ReturnsPreferred()
        {
            var free = new[] { true, true, true, true };
            var sqr = new[] { 5f, 0.1f, 0.1f, 0.1f };
            Assert.That(CoCarryMath.PickFreeHandle(0, free, sqr, 4), Is.EqualTo(0));
        }

        [Test]
        public void PickFreeHandle_PreferredTaken_ReturnsNearestFree()
        {
            var free = new[] { false, true, true, false };
            var sqr = new[] { 0f, 4f, 1f, 0f };
            Assert.That(CoCarryMath.PickFreeHandle(0, free, sqr, 4), Is.EqualTo(2));
        }

        [Test]
        public void PickFreeHandle_Ties_PickLowestIndex()
        {
            var free = new[] { true, true, true };
            var sqr = new[] { 2f, 2f, 2f };
            Assert.That(CoCarryMath.PickFreeHandle(5, free, sqr, 3), Is.EqualTo(0));
        }

        [Test]
        public void PickFreeHandle_AllOccupied_ReturnsMinusOne()
        {
            var free = new[] { false, false, false };
            var sqr = new[] { 1f, 2f, 3f };
            Assert.That(CoCarryMath.PickFreeHandle(1, free, sqr, 3), Is.EqualTo(-1));
        }

        [Test]
        public void PickFreeHandle_UnusableHandle_IsSkipped()
        {
            var free = new[] { true, true };
            var sqr = new[] { float.PositiveInfinity, 1f };
            Assert.That(CoCarryMath.PickFreeHandle(0, free, sqr, 2), Is.EqualTo(1));
        }

        // ── Degenerate inputs ──

        [Test]
        public void DegenerateInputs_DoNotThrowOrGoNegative()
        {
            Assert.That(CoCarryMath.LoadCrew(0, -3, 0, false, 4, LoadPer), Is.GreaterThanOrEqualTo(1f));
            Assert.That(CoCarryMath.LiftStrength(-1, 0f, Under), Is.GreaterThanOrEqualTo(0f));
            Assert.That(CoCarryMath.HaulSpeedFactor(-1, 0f, Base, FullGrip, Floor, Bonus), Is.GreaterThanOrEqualTo(0f));
        }

        // ── NeedsMoreHands ──

        [Test]
        public void NeedsMoreHands_BombBoundaries()
        {
            Assert.That(CoCarryMath.NeedsMoreHands(1, 2.00f, 4), Is.True);
            Assert.That(CoCarryMath.NeedsMoreHands(2, 2.00f, 4), Is.False);
            Assert.That(CoCarryMath.NeedsMoreHands(2, 2.65f, 4), Is.True);
            Assert.That(CoCarryMath.NeedsMoreHands(3, 2.65f, 4), Is.False);
            Assert.That(CoCarryMath.NeedsMoreHands(3, 3.30f, 4), Is.True);
            Assert.That(CoCarryMath.NeedsMoreHands(4, 3.30f, 4), Is.False);
        }

        [Test]
        public void NeedsMoreHands_HandleCountCapsIt()
        {
            Assert.That(CoCarryMath.NeedsMoreHands(2, 3.30f, 2), Is.False);
        }

        [Test]
        public void NeedsMoreHands_ZeroGrabbers_IsFalse()
        {
            Assert.That(CoCarryMath.NeedsMoreHands(0, 3.30f, 4), Is.False);
        }
    }
}
