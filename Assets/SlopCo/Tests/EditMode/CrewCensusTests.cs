using NUnit.Framework;
using SlopCo.Core;

namespace SlopCo.Tests.EditMode
{
    /// <summary>
    /// Verifies the CrewCensus static-state contract: increment/decrement, the 0-floor clamp (the
    /// thing that actually keeps a session boundary safe — see CrewCensus's own summary), reset, and
    /// a balanced register/unregister cycle returning to zero.
    /// </summary>
    public class CrewCensusTests
    {
        [SetUp]
        public void Setup() => CrewCensus.Reset();

        [Test]
        public void Register_Increments()
        {
            CrewCensus.Register();
            Assert.That(CrewCensus.Count, Is.EqualTo(1));
            CrewCensus.Register();
            Assert.That(CrewCensus.Count, Is.EqualTo(2));
        }

        [Test]
        public void Unregister_Decrements()
        {
            CrewCensus.Register();
            CrewCensus.Register();
            CrewCensus.Unregister();
            Assert.That(CrewCensus.Count, Is.EqualTo(1));
        }

        [Test]
        public void Unregister_BelowZero_ClampsAtZero()
        {
            CrewCensus.Unregister();
            Assert.That(CrewCensus.Count, Is.EqualTo(0));
            CrewCensus.Unregister();
            Assert.That(CrewCensus.Count, Is.EqualTo(0));
        }

        [Test]
        public void Reset_ReturnsToZero()
        {
            CrewCensus.Register();
            CrewCensus.Register();
            CrewCensus.Reset();
            Assert.That(CrewCensus.Count, Is.EqualTo(0));
        }

        [Test]
        public void RegisterUnregister_Balanced_ReturnsToZero()
        {
            for (int i = 0; i < 4; i++) CrewCensus.Register();
            Assert.That(CrewCensus.Count, Is.EqualTo(4));
            for (int i = 0; i < 4; i++) CrewCensus.Unregister();
            Assert.That(CrewCensus.Count, Is.EqualTo(0));
        }
    }
}
