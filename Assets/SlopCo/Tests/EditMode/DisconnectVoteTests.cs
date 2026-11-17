using NUnit.Framework;
using SlopCo.Gameplay;

namespace SlopCo.Tests.EditMode
{
    /// <summary>
    /// Pins which disconnects stop the run for a vote (DisconnectRules.IsDeparture, read by DisconnectVote). The
    /// case that matters: the server also hears a "disconnect" for a peer it refused at approval (JoinPolicy —
    /// full lobby, mid-haul join), and that must never pause the crew.
    /// </summary>
    public class DisconnectVoteTests
    {
        [TestCase(RoundPhase.Briefing)]
        [TestCase(RoundPhase.Hauling)]
        [TestCase(RoundPhase.Payout)]
        public void ACrewMemberLeavingMidRun_IsADeparture(RoundPhase phase) =>
            Assert.IsTrue(DisconnectRules.IsDeparture(wasInSession: true, isHost: false, phase));

        [TestCase(RoundPhase.Briefing)]
        [TestCase(RoundPhase.Hauling)]
        [TestCase(RoundPhase.Payout)]
        public void APeerRefusedAtTheDoor_IsNotADeparture(RoundPhase phase) =>
            Assert.IsFalse(DisconnectRules.IsDeparture(wasInSession: false, isHost: false, phase));

        [TestCase(RoundPhase.Lobby)]
        [TestCase(RoundPhase.GameOver)]
        public void WithNothingInProgress_NobodyLeavingIsADeparture(RoundPhase phase) =>
            Assert.IsFalse(DisconnectRules.IsDeparture(wasInSession: true, isHost: false, phase));

        [Test]
        public void TheHostLeaving_IsNotADeparture() =>
            Assert.IsFalse(DisconnectRules.IsDeparture(wasInSession: true, isHost: true, RoundPhase.Hauling));
    }
}
