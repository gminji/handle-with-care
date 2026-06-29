using System.Collections.Generic;
using NUnit.Framework;
using SlopCo.Gameplay;

namespace SlopCo.Tests.EditMode
{
    /// <summary>
    /// Verifies the pure MVP award selection (MvpAward) without any UnityEngine / NGO runtime.
    /// </summary>
    public class MvpAwardTests
    {
        private static List<PlayerTally> List(params PlayerTally[] t) => new List<PlayerTally>(t);

        [Test]
        public void TopDeliverer_Empty_IsZero()
        {
            Assert.That(MvpAward.TopDeliverer(new List<PlayerTally>()), Is.EqualTo(0UL));
            Assert.That(MvpAward.TopDeliverer(null), Is.EqualTo(0UL));
        }

        [Test]
        public void TopDeliverer_AllZeroDelivered_IsZero()
        {
            var l = List(new PlayerTally(5, 0, 300), new PlayerTally(6, 0, 100));
            Assert.That(MvpAward.TopDeliverer(l), Is.EqualTo(0UL));
        }

        [Test]
        public void TopDeliverer_PicksHighestDelivered()
        {
            var l = List(new PlayerTally(5, 100, 0), new PlayerTally(6, 400, 0), new PlayerTally(7, 250, 0));
            Assert.That(MvpAward.TopDeliverer(l), Is.EqualTo(6UL));
        }

        [Test]
        public void TopDeliverer_Tie_BreaksToLowestCarrierId()
        {
            var l = List(new PlayerTally(9, 400, 0), new PlayerTally(3, 400, 0), new PlayerTally(7, 400, 0));
            Assert.That(MvpAward.TopDeliverer(l), Is.EqualTo(3UL));
        }

        [Test]
        public void TopDeliverer_IgnoresDestroyed()
        {
            // Player 6 smashed the most but delivered least → not the deliverer.
            var l = List(new PlayerTally(5, 500, 10), new PlayerTally(6, 100, 9999));
            Assert.That(MvpAward.TopDeliverer(l), Is.EqualTo(5UL));
        }

        [Test]
        public void TopDestroyer_PicksHighestDestroyed_TieLowestId()
        {
            var l = List(new PlayerTally(5, 0, 200), new PlayerTally(6, 0, 800), new PlayerTally(2, 0, 800));
            Assert.That(MvpAward.TopDestroyer(l), Is.EqualTo(2UL));
        }

        [Test]
        public void TopDestroyer_AllZero_IsZero()
        {
            var l = List(new PlayerTally(5, 300, 0), new PlayerTally(6, 100, 0));
            Assert.That(MvpAward.TopDestroyer(l), Is.EqualTo(0UL));
        }

        [Test]
        public void ParticipantCount_CountsAnyActivity()
        {
            var l = List(
                new PlayerTally(5, 300, 0),   // delivered only
                new PlayerTally(6, 0, 200),   // destroyed only
                new PlayerTally(7, 0, 0),     // idle → not counted
                new PlayerTally(8, 50, 50));  // both → counted once
            Assert.That(MvpAward.ParticipantCount(l), Is.EqualTo(3));
        }

        [Test]
        public void ParticipantCount_Empty_IsZero()
        {
            Assert.That(MvpAward.ParticipantCount(new List<PlayerTally>()), Is.EqualTo(0));
            Assert.That(MvpAward.ParticipantCount(null), Is.EqualTo(0));
        }

        [Test]
        public void SamePlayer_CanWinBothAwards()
        {
            var l = List(new PlayerTally(5, 999, 999), new PlayerTally(6, 10, 10));
            Assert.That(MvpAward.TopDeliverer(l), Is.EqualTo(5UL));
            Assert.That(MvpAward.TopDestroyer(l), Is.EqualTo(5UL));
        }
    }
}
