using System.Collections.Generic;
using NUnit.Framework;
using SlopCo.Core;
using SlopCo.Networking;

namespace SlopCo.Tests.EditMode
{
    /// <summary>
    /// Verifies the pure matchmaking beacon codec + candidate selection without sockets / NGO.
    /// </summary>
    public class MatchmakerCodecTests
    {
        [Test]
        public void EncodeThenDecode_RoundTripsAllFields()
        {
            var b = new MatchBeacon(GameConstants.MatchGameId, 7777, 2, 4);
            byte[] wire = MatchmakerCodec.Encode(b);

            Assert.IsTrue(MatchmakerCodec.TryDecode(wire, GameConstants.MatchGameId, out var got));
            Assert.AreEqual(GameConstants.MatchGameId, got.GameId);
            Assert.AreEqual(7777, got.Port);
            Assert.AreEqual(2, got.Players);
            Assert.AreEqual(4, got.MaxPlayers);
            Assert.IsTrue(got.IsJoinable);
        }

        [Test]
        public void TryDecode_RejectsEmptyOrShort()
        {
            Assert.IsFalse(MatchmakerCodec.TryDecode(new byte[0], GameConstants.MatchGameId, out _));
            Assert.IsFalse(MatchmakerCodec.TryDecode(new byte[] { (byte)'S', (byte)'P', 1 }, GameConstants.MatchGameId, out _));
        }

        [Test]
        public void TryDecode_RejectsBadMagic()
        {
            byte[] wire = MatchmakerCodec.Encode(new MatchBeacon(GameConstants.MatchGameId, 7777, 1, 4));
            wire[0] = (byte)'X';
            Assert.IsFalse(MatchmakerCodec.TryDecode(wire, GameConstants.MatchGameId, out _));
        }

        [Test]
        public void TryDecode_RejectsMismatchedGameId()
        {
            byte[] wire = MatchmakerCodec.Encode(new MatchBeacon("otherapp", 7777, 1, 4));
            Assert.IsFalse(MatchmakerCodec.TryDecode(wire, GameConstants.MatchGameId, out _));
        }

        [Test]
        public void TrySelectBest_PicksMostFullJoinableHost()
        {
            var found = new List<MatchCandidate>
            {
                new MatchCandidate("10.0.0.1", new MatchBeacon(GameConstants.MatchGameId, 7777, 1, 4)),
                new MatchCandidate("10.0.0.2", new MatchBeacon(GameConstants.MatchGameId, 7777, 3, 4)), // fullest joinable
                new MatchCandidate("10.0.0.3", new MatchBeacon(GameConstants.MatchGameId, 7777, 2, 4)),
            };
            Assert.IsTrue(MatchmakerCodec.TrySelectBest(found, out var best));
            Assert.AreEqual("10.0.0.2", best.Address);
            Assert.AreEqual(3, best.Beacon.Players);
        }

        [Test]
        public void TrySelectBest_SkipsFullHosts()
        {
            var found = new List<MatchCandidate>
            {
                new MatchCandidate("10.0.0.1", new MatchBeacon(GameConstants.MatchGameId, 7777, 4, 4)), // full
                new MatchCandidate("10.0.0.2", new MatchBeacon(GameConstants.MatchGameId, 7777, 1, 4)),
            };
            Assert.IsTrue(MatchmakerCodec.TrySelectBest(found, out var best));
            Assert.AreEqual("10.0.0.2", best.Address);
        }

        [Test]
        public void TrySelectBest_AllFull_ReturnsFalse()
        {
            var found = new List<MatchCandidate>
            {
                new MatchCandidate("10.0.0.1", new MatchBeacon(GameConstants.MatchGameId, 7777, 4, 4)),
                new MatchCandidate("10.0.0.2", new MatchBeacon(GameConstants.MatchGameId, 7777, 4, 4)),
            };
            Assert.IsFalse(MatchmakerCodec.TrySelectBest(found, out _));
        }

        [Test]
        public void TrySelectBest_EmptyList_ReturnsFalse()
        {
            Assert.IsFalse(MatchmakerCodec.TrySelectBest(new List<MatchCandidate>(), out _));
            Assert.IsFalse(MatchmakerCodec.TrySelectBest(null, out _));
        }
    }
}
