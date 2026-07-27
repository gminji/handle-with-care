using NUnit.Framework;
using SlopCo.Gameplay;

namespace SlopCo.Tests.EditMode
{
    /// <summary>
    /// Pins crew vote counting (VoteTally) and the offer packing it rides on (AugmentOffer). Pure logic,
    /// no UnityEngine dependency. This is shared by the augment vote, the map vote and the disconnect vote,
    /// so the tie-break and the "nobody voted" case are worth nailing down once.
    /// </summary>
    public class VoteTallyTests
    {
        [Test]
        public void ClearWinner_Wins()
        {
            int w = VoteTally.Resolve(new[] { 1, 3, 2 }, 12345u, out bool tie);
            Assert.AreEqual(1, w);
            Assert.IsFalse(tie);
        }

        [Test]
        public void NobodyVoted_ReturnsMinusOne()
        {
            int w = VoteTally.Resolve(new[] { 0, 0, 0 }, 999u, out bool tie);
            Assert.AreEqual(-1, w);
            Assert.IsFalse(tie);
        }

        [Test]
        public void EmptyOrNull_ReturnsMinusOne()
        {
            Assert.AreEqual(-1, VoteTally.Resolve(new int[0], 1u, out _));
            Assert.AreEqual(-1, VoteTally.Resolve(null, 1u, out _));
        }

        [Test]
        public void Tie_IsFlaggedAndPicksALeader()
        {
            for (uint seed = 1; seed < 200; seed++)
            {
                int w = VoteTally.Resolve(new[] { 2, 2, 1 }, seed, out bool tie);
                Assert.IsTrue(tie);
                Assert.IsTrue(w == 0 || w == 1, "tie-break must never pick a losing option (got " + w + ")");
            }
        }

        [Test]
        public void Tie_IsResolvedRandomlyAcrossSeeds()
        {
            bool sawZero = false, sawOne = false;
            for (uint seed = 1; seed < 200; seed++)
            {
                int w = VoteTally.Resolve(new[] { 2, 2, 0 }, seed, out _);
                if (w == 0) sawZero = true;
                if (w == 1) sawOne = true;
            }
            Assert.IsTrue(sawZero && sawOne, "a tie must actually be random, not always the first option");
        }

        [Test]
        public void Tie_IsDeterministicForOneSeed()
        {
            // Every client is told the result, but the server must also be reproducible for a given ballot.
            Assert.AreEqual(VoteTally.Resolve(new[] { 3, 3, 3 }, 4242u),
                            VoteTally.Resolve(new[] { 3, 3, 3 }, 4242u));
        }

        [Test]
        public void ThreeWayTie_CanLandOnAnyOption()
        {
            var seen = new bool[3];
            for (uint seed = 1; seed < 300; seed++) seen[VoteTally.Resolve(new[] { 1, 1, 1 }, seed)] = true;
            Assert.IsTrue(seen[0] && seen[1] && seen[2]);
        }

        [Test]
        public void IsTie_MatchesResolve()
        {
            Assert.IsTrue (VoteTally.IsTie(new[] { 2, 2 }));
            Assert.IsFalse(VoteTally.IsTie(new[] { 2, 1 }));
            Assert.IsFalse(VoteTally.IsTie(new[] { 0, 0 }), "no votes at all is not a tie");
        }

        [Test]
        public void Total_CountsBallots()
        {
            Assert.AreEqual(6, VoteTally.Total(new[] { 1, 2, 3 }));
            Assert.AreEqual(0, VoteTally.Total(new int[0]));
            Assert.AreEqual(0, VoteTally.Total(null));
        }

        // ── AugmentOffer packing ──

        [Test]
        public void Offer_RoundTrips()
        {
            int packed = AugmentOffer.Pack(new[] { 11, 0, 7 }, 3);
            Assert.AreEqual(11, AugmentOffer.Slot(packed, 0));
            Assert.AreEqual(0,  AugmentOffer.Slot(packed, 1));
            Assert.AreEqual(7,  AugmentOffer.Slot(packed, 2));
            Assert.AreEqual(3,  AugmentOffer.Count(packed));
            CollectionAssert.AreEqual(new[] { 11, 0, 7 }, AugmentOffer.Unpack(packed));
        }

        [Test]
        public void Offer_ZeroMeansNoOfferYet()
        {
            // A freshly spawned NetworkVariable<int> is 0 — that must read as "nothing offered".
            Assert.AreEqual(0, AugmentOffer.Count(0));
            Assert.AreEqual(-1, AugmentOffer.Slot(0, 0));
            Assert.AreEqual(0, AugmentOffer.Unpack(0).Length);
        }

        [Test]
        public void Offer_PartialAndOversizedInputsAreSafe()
        {
            int packed = AugmentOffer.Pack(new[] { 4 }, 3);          // count beyond the array
            Assert.AreEqual(1, AugmentOffer.Count(packed));
            Assert.AreEqual(4, AugmentOffer.Slot(packed, 0));

            int clamped = AugmentOffer.Pack(new[] { 1, 2, 3, 4, 5 }, 5);   // more than MaxSlots
            Assert.AreEqual(AugmentOffer.MaxSlots, AugmentOffer.Count(clamped));

            Assert.AreEqual(-1, AugmentOffer.Slot(packed, -1));
            Assert.AreEqual(-1, AugmentOffer.Slot(packed, 99));
            Assert.AreEqual(0, AugmentOffer.Pack(null, 3));
        }

        [Test]
        public void Offer_PacksVoteCountsToo()
        {
            // The same packing carries the live tally back to the clients.
            int packed = AugmentOffer.Pack(new[] { 0, 2, 1 }, 3);
            Assert.AreEqual(0, AugmentOffer.Slot(packed, 0));
            Assert.AreEqual(2, AugmentOffer.Slot(packed, 1));
            Assert.AreEqual(1, AugmentOffer.Slot(packed, 2));
        }
    }
}
