using NUnit.Framework;
using SlopCo.Audio;
using SlopCo.Gameplay;

namespace SlopCo.Tests.EditMode
{
    /// <summary>
    /// Pins the phase→music policy (MusicBus.CalmWeight): only Hauling rides the haul loop; every other
    /// phase rides the calm loop. Same pure-function test shape as RunGradeTests / ShareTextTests.
    /// </summary>
    public class MusicBusTests
    {
        [Test]
        public void CalmWeight_Hauling_IsHaul()
        {
            Assert.AreEqual(0f, MusicBus.CalmWeight(RoundPhase.Hauling));
        }

        [TestCase(RoundPhase.Lobby)]
        [TestCase(RoundPhase.Briefing)]
        [TestCase(RoundPhase.Payout)]
        [TestCase(RoundPhase.GameOver)]
        public void CalmWeight_NonHauling_IsCalm(RoundPhase phase)
        {
            Assert.AreEqual(1f, MusicBus.CalmWeight(phase));
        }
    }
}
