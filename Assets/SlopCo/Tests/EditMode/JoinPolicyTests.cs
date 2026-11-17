using NUnit.Framework;
using SlopCo.Core;
using SlopCo.Gameplay;
using SlopCo.Networking;

namespace SlopCo.Tests.EditMode
{
    /// <summary>
    /// Pins who may join a running session (JoinPolicy): everyone with room, except during a haul. The approval
    /// callback and the LAN beacon both read this, so the two can't disagree.
    /// </summary>
    public class JoinPolicyTests
    {
        const int Max = 4;

        [TestCase(RoundPhase.Lobby)]
        [TestCase(RoundPhase.Briefing)]
        [TestCase(RoundPhase.Payout)]
        [TestCase(RoundPhase.GameOver)]
        public void OutsideAHaul_WithRoom_LetsTheNewcomerIn(RoundPhase phase) =>
            Assert.IsNull(JoinPolicy.RejectReason(false, 2, Max, phase));

        [Test]
        public void DuringAHaul_RefusesWithTheHaulReason() =>
            Assert.AreEqual(JoinPolicy.ReasonHauling, JoinPolicy.RejectReason(false, 1, Max, RoundPhase.Hauling));

        [TestCase(RoundPhase.Lobby)]
        [TestCase(RoundPhase.Hauling)]
        public void FullLobby_RefusesAsFull_InAnyPhase(RoundPhase phase) =>
            Assert.AreEqual(JoinPolicy.ReasonFull, JoinPolicy.RejectReason(false, Max, Max, phase));

        [TestCase(RoundPhase.Lobby)]
        [TestCase(RoundPhase.Hauling)]
        public void TheHostsOwnClient_IsNeverRefused(RoundPhase phase) =>
            Assert.IsNull(JoinPolicy.RejectReason(true, Max, Max, phase));

        [Test]
        public void Beacon_AdvertisesOnlyWhenAJoinWouldBeAccepted()
        {
            Assert.IsTrue (JoinPolicy.ShouldAdvertise(1, Max, RoundPhase.Lobby));
            Assert.IsTrue (JoinPolicy.ShouldAdvertise(1, Max, RoundPhase.Payout));
            Assert.IsFalse(JoinPolicy.ShouldAdvertise(1, Max, RoundPhase.Hauling), "mid-haul hosts stay quiet");
            Assert.IsFalse(JoinPolicy.ShouldAdvertise(Max, Max, RoundPhase.Lobby), "full hosts stay quiet");
        }

        [Test]
        public void IsRefusal_OnlyOurOwnReasons()
        {
            Assert.IsTrue (JoinPolicy.IsRefusal(JoinPolicy.ReasonHauling));
            Assert.IsTrue (JoinPolicy.IsRefusal(JoinPolicy.ReasonFull));
            // NGO's own fallback text when the host quits without a reason — must not reach the lobby screen.
            Assert.IsFalse(JoinPolicy.IsRefusal("[Disconnect Event][Client-1][TransportClientId-1][ServerDisconnected] Disconnected"));
            Assert.IsFalse(JoinPolicy.IsRefusal(string.Empty));
            Assert.IsFalse(JoinPolicy.IsRefusal(null));
        }

        [Test]
        public void EveryRefusalReason_HasLobbyText()
        {
            // Localization.Get returns the key itself when the entry is missing.
            Assert.AreNotEqual(JoinPolicy.ReasonHauling, Localization.Get(JoinPolicy.ReasonHauling));
            Assert.AreNotEqual(JoinPolicy.ReasonFull, Localization.Get(JoinPolicy.ReasonFull));
        }
    }
}
