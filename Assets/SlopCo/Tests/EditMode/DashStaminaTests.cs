using NUnit.Framework;
using SlopCo.Player;

namespace SlopCo.Tests.EditMode
{
    /// <summary>
    /// Verifies the pure dash-stamina state machine (DashStamina) without any UnityEngine runtime.
    /// Constants are passed explicitly so the tests are independent of GameConstants tuning.
    /// </summary>
    public class DashStaminaTests
    {
        private const float Tol = 1e-4f;
        // drain 0.5/s, regen 0.25/s, exhaust 1.0s — local fixtures (not tied to GameConstants).
        private const float Drain = 0.5f, Regen = 0.25f, Exhaust = 1.0f;

        private static DashState Step(DashState s, float dt, bool dash, bool moving)
            => DashStamina.Step(s, dt, dash, moving, Drain, Regen, Exhaust);

        [Test]
        public void Dashing_WhileMoving_DrainsGauge()
        {
            var s = DashStamina.Initial;
            s = Step(s, 0.5f, dash: true, moving: true); // 0.5s * 0.5/s = 0.25
            Assert.That(s.gauge, Is.EqualTo(0.75f).Within(Tol));
            Assert.That(s.dashing, Is.True);
            Assert.That(s.exhausted, Is.False);
            Assert.That(DashStamina.SpeedMult(s, 1.8f), Is.EqualTo(1.8f).Within(Tol));
        }

        [Test]
        public void DrainingToEmpty_TriggersExhaustion_AndStops()
        {
            var s = DashStamina.Initial;
            s = Step(s, 2.5f, dash: true, moving: true); // overshoot empties gauge
            Assert.That(s.gauge, Is.EqualTo(0f).Within(Tol));
            Assert.That(s.exhausted, Is.True);
            Assert.That(s.exhaustT, Is.EqualTo(Exhaust).Within(Tol));
            Assert.That(s.dashing, Is.False);
            Assert.That(DashStamina.SpeedMult(s, 1.8f), Is.EqualTo(0f).Within(Tol)); // forced stop
        }

        [Test]
        public void WhileExhausted_CannotMove_AndRegens()
        {
            var s = DashStamina.Initial;
            s = Step(s, 2.5f, dash: true, moving: true); // exhaust
            float before = s.gauge;
            s = Step(s, 0.4f, dash: true, moving: true); // still exhausted: dash ignored
            Assert.That(s.exhausted, Is.True);
            Assert.That(DashStamina.SpeedMult(s, 1.8f), Is.EqualTo(0f).Within(Tol));
            Assert.That(s.gauge, Is.GreaterThan(before)); // regenerates during exhaustion
        }

        [Test]
        public void Exhaustion_RecoversAfterExhaustSeconds()
        {
            var s = DashStamina.Initial;
            s = Step(s, 2.5f, dash: true, moving: true); // exhaust (exhaustT = 1.0)
            s = Step(s, 1.0f, dash: false, moving: false); // wait out the stun
            Assert.That(s.exhausted, Is.False);
            Assert.That(s.exhaustT, Is.EqualTo(0f).Within(Tol));
            Assert.That(DashStamina.SpeedMult(s, 1.8f), Is.EqualTo(1f).Within(Tol)); // control restored
        }

        [Test]
        public void Idle_RegeneratesAndClampsAtOne()
        {
            var s = DashStamina.Initial;
            s = Step(s, 1.0f, dash: true, moving: true); // drain to 0.5
            Assert.That(s.gauge, Is.EqualTo(0.5f).Within(Tol));
            s = Step(s, 10f, dash: false, moving: false); // long idle → clamps at 1
            Assert.That(s.gauge, Is.EqualTo(1f).Within(Tol));
            Assert.That(s.dashing, Is.False);
        }

        [Test]
        public void DashHeldButNotMoving_DoesNotDrain()
        {
            var s = DashStamina.Initial;
            s = Step(s, 1.0f, dash: true, moving: true);  // drain to 0.5
            float g = s.gauge;
            s = Step(s, 1.0f, dash: true, moving: false); // holding dash but standing still → no drain, regen
            Assert.That(s.dashing, Is.False);
            Assert.That(s.gauge, Is.GreaterThanOrEqualTo(g));
        }

        [Test]
        public void PartialDrain_ThenRelease_Regenerates()
        {
            var s = DashStamina.Initial;
            s = Step(s, 0.5f, dash: true, moving: true);  // 0.75
            s = Step(s, 0.4f, dash: false, moving: true); // release → regen 0.1
            Assert.That(s.gauge, Is.EqualTo(0.85f).Within(Tol));
            Assert.That(s.dashing, Is.False);
        }

        [Test]
        public void SpeedMult_NeutralWhenIdle()
        {
            var s = DashStamina.Initial;
            Assert.That(DashStamina.SpeedMult(s, 1.8f), Is.EqualTo(1f).Within(Tol));
        }

        [Test]
        public void Refill_AddsStamina_AndClearsExhaustion()
        {
            var s = DashStamina.Initial;
            s = Step(s, 2.5f, dash: true, moving: true);   // exhaust → gauge 0, exhausted
            Assert.That(s.exhausted, Is.True);
            s = DashStamina.Refill(s, 0.4f);               // item refill
            Assert.That(s.gauge, Is.EqualTo(0.4f).Within(Tol));
            Assert.That(s.exhausted, Is.False);            // exhaustion cleared once gauge > 0
            Assert.That(DashStamina.SpeedMult(s, 1.8f), Is.EqualTo(1f).Within(Tol));
        }

        [Test]
        public void Refill_ClampsAtOne()
        {
            var s = DashStamina.Initial;                   // gauge 1
            s = DashStamina.Refill(s, 0.5f);
            Assert.That(s.gauge, Is.EqualTo(1f).Within(Tol));
        }

        [Test]
        public void NegativeDt_IsTreatedAsZero()
        {
            var s = DashStamina.Initial;
            s = Step(s, -5f, dash: true, moving: true);
            Assert.That(s.gauge, Is.EqualTo(1f).Within(Tol));
            Assert.That(s.exhausted, Is.False);
        }
    }
}
