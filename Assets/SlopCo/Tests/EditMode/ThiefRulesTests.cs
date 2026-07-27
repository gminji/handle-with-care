using NUnit.Framework;
using SlopCo.Gameplay;

namespace SlopCo.Tests.EditMode
{
    /// <summary>
    /// Pins what the thief is allowed to take (ThiefRules). Pure logic, no UnityEngine dependency —
    /// same shape as DailyModifierTests. The rule matters: stealing cargo that was never dropped, or
    /// snatching it mid-bounce, would read as a bug rather than a hazard.
    /// </summary>
    public class ThiefRulesTests
    {
        const float RestSpeed = 0.9f, Settle = 0.35f;

        static bool Stealable(bool held = false, bool delivered = false, ulong carrier = 7UL,
                              float speed = 0f, float restingFor = 1f) =>
            ThiefRules.IsStealable(held, delivered, carrier, speed, restingFor, RestSpeed, Settle);

        [Test]
        public void DroppedAndSettledCargo_IsFairGame() => Assert.IsTrue(Stealable());

        [Test]
        public void HeldCargo_IsSafe() => Assert.IsFalse(Stealable(held: true));

        [Test]
        public void DeliveredCargo_IsSafe() => Assert.IsFalse(Stealable(delivered: true));

        [Test]
        public void NeverCarriedCargo_IsSafe()
        {
            // Untouched depot cargo must not be stolen out from under the crew before the round starts.
            Assert.IsFalse(Stealable(carrier: 0UL));
        }

        [Test]
        public void StillMovingCargo_IsSafe()
        {
            Assert.IsFalse(Stealable(speed: 5f));
            Assert.IsFalse(Stealable(speed: RestSpeed + 0.01f));
            Assert.IsTrue (Stealable(speed: RestSpeed));      // exactly at the threshold counts as at rest
        }

        [Test]
        public void CargoThatHasNotSettledYet_IsSafe()
        {
            Assert.IsFalse(Stealable(restingFor: 0f));
            Assert.IsFalse(Stealable(restingFor: Settle - 0.01f));
            Assert.IsTrue (Stealable(restingFor: Settle));
        }

        [Test]
        public void AdvanceRest_AccumulatesWhileStill()
        {
            float r = 0f;
            r = ThiefRules.AdvanceRest(r, 0.1f, RestSpeed, 0.2f);
            r = ThiefRules.AdvanceRest(r, 0.1f, RestSpeed, 0.2f);
            Assert.AreEqual(0.4f, r, 1e-4f);
        }

        [Test]
        public void AdvanceRest_ResetsOnAnyMovement()
        {
            float r = ThiefRules.AdvanceRest(1.5f, 4f, RestSpeed, 0.2f);
            Assert.AreEqual(0f, r, 1e-5f);
        }

        [Test]
        public void AdvanceRest_IgnoresNegativeDeltaTime() =>
            Assert.AreEqual(1f, ThiefRules.AdvanceRest(1f, 0f, RestSpeed, -5f), 1e-5f);

        [Test]
        public void BouncingCargo_BecomesStealableOnlyAfterItSettles()
        {
            // A thrown crate skitters, then stops: not stealable until the settle window elapses.
            float rest = 0f;
            rest = ThiefRules.AdvanceRest(rest, 6f, RestSpeed, 0.1f);   // still flying
            Assert.IsFalse(Stealable(speed: 6f, restingFor: rest));
            rest = ThiefRules.AdvanceRest(rest, 0.2f, RestSpeed, 0.2f); // landed, 0.2s
            Assert.IsFalse(Stealable(speed: 0.2f, restingFor: rest));
            rest = ThiefRules.AdvanceRest(rest, 0.2f, RestSpeed, 0.2f); // 0.4s
            Assert.IsTrue(Stealable(speed: 0.2f, restingFor: rest));
        }
    }
}
