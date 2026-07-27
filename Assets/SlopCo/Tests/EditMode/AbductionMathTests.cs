using NUnit.Framework;
using SlopCo.Gameplay;
using SlopCo.Player;

namespace SlopCo.Tests.EditMode
{
    /// <summary>
    /// Pins the UFO abduction arc and what the landing costs (AbductionMath), plus the stamina drain it
    /// feeds (DashStamina.Drain). Pure logic, no UnityEngine dependency — same shape as DailyModifierTests.
    /// </summary>
    public class AbductionMathTests
    {
        const float Lift = 1.6f, Hold = 1.4f;

        [Test]
        public void Phase_WalksLiftThenHoldThenDone()
        {
            Assert.AreEqual(AbductPhase.Lift, AbductionMath.Phase(0f, Lift, Hold));
            Assert.AreEqual(AbductPhase.Lift, AbductionMath.Phase(1.5f, Lift, Hold));
            Assert.AreEqual(AbductPhase.Hold, AbductionMath.Phase(Lift, Lift, Hold));
            Assert.AreEqual(AbductPhase.Hold, AbductionMath.Phase(2.9f, Lift, Hold));
            Assert.AreEqual(AbductPhase.Done, AbductionMath.Phase(Lift + Hold, Lift, Hold));
            Assert.AreEqual(AbductPhase.Done, AbductionMath.Phase(99f, Lift, Hold));
        }

        [Test]
        public void Phase_ZeroDurations_AreImmediatelyDone() =>
            Assert.AreEqual(AbductPhase.Done, AbductionMath.Phase(0f, 0f, 0f));

        [Test]
        public void LiftT_IsClampedAndMonotonic()
        {
            Assert.AreEqual(0f, AbductionMath.LiftT(0f, Lift), 1e-4f);
            Assert.AreEqual(1f, AbductionMath.LiftT(Lift, Lift), 1e-4f);
            Assert.AreEqual(1f, AbductionMath.LiftT(99f, Lift), 1e-4f);
            Assert.AreEqual(0f, AbductionMath.LiftT(-5f, Lift), 1e-4f);

            float prev = -1f;
            for (int i = 0; i <= 10; i++)
            {
                float t = AbductionMath.LiftT(Lift * i / 10f, Lift);
                Assert.GreaterOrEqual(t, prev);
                prev = t;
            }
        }

        [Test]
        public void LiftT_EasesSlowOffTheGround()
        {
            // Smoothstep: a quarter of the way through the beam, the victim has risen less than a quarter.
            Assert.Less(AbductionMath.LiftT(Lift * 0.25f, Lift), 0.25f);
            Assert.AreEqual(0.5f, AbductionMath.LiftT(Lift * 0.5f, Lift), 1e-4f);
        }

        [Test]
        public void LiftT_ZeroDuration_IsInstant() =>
            Assert.AreEqual(1f, AbductionMath.LiftT(0f, 0f), 1e-4f);

        // ── landing cost ──

        [Test]
        public void StaminaPenalty_ShortDropsAreFree()
        {
            Assert.AreEqual(0f, AbductionMath.StaminaPenalty(0f, 2.5f, 0.085f, 0.9f), 1e-5f);
            Assert.AreEqual(0f, AbductionMath.StaminaPenalty(2.5f, 2.5f, 0.085f, 0.9f), 1e-5f);
            Assert.AreEqual(0f, AbductionMath.StaminaPenalty(-3f, 2.5f, 0.085f, 0.9f), 1e-5f);
        }

        [Test]
        public void StaminaPenalty_ScalesWithHeightThenSaturates()
        {
            float small = AbductionMath.StaminaPenalty(5f, 2.5f, 0.085f, 0.9f);
            float big   = AbductionMath.StaminaPenalty(11f, 2.5f, 0.085f, 0.9f);
            Assert.Greater(big, small);
            Assert.AreEqual(0.2125f, small, 1e-4f);                                    // 2.5 over * 0.085
            Assert.AreEqual(0.9f, AbductionMath.StaminaPenalty(200f, 2.5f, 0.085f, 0.9f), 1e-5f);
        }

        [Test]
        public void StaminaPenalty_FullCruiseDrop_IsPunishingButNotTotal()
        {
            // The saucer hangs at ~11m; that drop should hurt a lot without being an instant full stun.
            float cost = AbductionMath.StaminaPenalty(11f, 2.5f, 0.085f, 0.9f);
            Assert.Greater(cost, 0.5f);
            Assert.LessOrEqual(cost, 0.9f);
        }

        // ── the drain it feeds ──

        [Test]
        public void Drain_RemovesStaminaAndClamps()
        {
            var s = DashStamina.Initial;
            s = DashStamina.Drain(s, 0.3f, 1f);
            Assert.AreEqual(0.7f, s.gauge, 1e-4f);
            Assert.IsFalse(s.exhausted);
        }

        [Test]
        public void Drain_ToEmpty_Exhausts()
        {
            var s = DashStamina.Drain(DashStamina.Initial, 1.5f, 1.25f);
            Assert.AreEqual(0f, s.gauge, 1e-5f);
            Assert.IsTrue(s.exhausted);
            Assert.AreEqual(1.25f, s.exhaustT, 1e-4f);
            Assert.IsFalse(s.dashing);
        }

        [Test]
        public void Drain_ZeroOrNegative_IsANoOp()
        {
            var s = DashStamina.Initial;
            Assert.AreEqual(s.gauge, DashStamina.Drain(s, 0f, 1f).gauge, 1e-5f);
            Assert.AreEqual(s.gauge, DashStamina.Drain(s, -1f, 1f).gauge, 1e-5f);
        }

        [Test]
        public void Drain_DoesNotRestartAnExistingExhaustion()
        {
            var s = DashStamina.Drain(DashStamina.Initial, 1f, 1f);   // exhausted, 1s left
            s = DashStamina.Step(s, 0.5f, false, false, 0.5f, 0.35f, 1f);
            float left = s.exhaustT;
            s = DashStamina.Drain(s, 0.9f, 1f);                       // hit the ground again while stunned
            Assert.AreEqual(left, s.exhaustT, 1e-4f, "a second drain must not refresh the stun timer");
        }
    }
}
