using NUnit.Framework;
using SlopCo.Gameplay;

namespace SlopCo.Tests.EditMode
{
    /// <summary>
    /// Verifies the pure quota arithmetic (QuotaMath) without any NetworkManager/NetworkBehaviour —
    /// this is exactly the logic that CAN be statically verified in a no-runtime environment.
    /// </summary>
    public class QuotaSystemTests
    {
        [Test]
        public void Escalate_RaisesByFactorAndFlat()
        {
            // ceil(150 * 1.4) + 50 = 210 + 50
            Assert.AreEqual(260, QuotaMath.Escalate(150));
        }

        [Test]
        public void Escalate_RoundsUp()
        {
            // ceil(151 * 1.4) + 50 = ceil(211.4) + 50 = 212 + 50
            Assert.AreEqual(262, QuotaMath.Escalate(151));
        }

        [Test]
        public void Escalate_NegativeInputClampedToZeroBase()
        {
            // ceil(0) + 50
            Assert.AreEqual(50, QuotaMath.Escalate(-10));
        }

        [Test]
        public void IsMet_BoundaryAndBeyond()
        {
            Assert.IsTrue(QuotaMath.IsMet(260, 260));
            Assert.IsTrue(QuotaMath.IsMet(400, 260));
            Assert.IsFalse(QuotaMath.IsMet(259, 260));
        }

        [Test]
        public void Shortfall_ComputesRemainingAndFloorsAtZero()
        {
            Assert.AreEqual(10, QuotaMath.Shortfall(250, 260));
            Assert.AreEqual(0, QuotaMath.Shortfall(260, 260));
            Assert.AreEqual(0, QuotaMath.Shortfall(999, 260));
        }
    }
}
