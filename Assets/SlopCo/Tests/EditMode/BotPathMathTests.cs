using NUnit.Framework;
using UnityEngine;
using SlopCo.Player;

namespace SlopCo.Tests.EditMode
{
    /// <summary>
    /// Pins the bot route-following rules (BotPathMath): when a waypoint counts as reached, when a route is
    /// stale, and which colliders the NavMesh bake treats as level geometry.
    /// </summary>
    public class BotPathMathTests
    {
        static readonly Vector3[] Route =
        {
            new Vector3(0f, 0f, 0f),
            new Vector3(0f, 0f, 3f),
            new Vector3(4f, 1.5f, 3f),   // top of a ramp
            new Vector3(8f, 1.5f, 3f),
        };

        // ── AdvanceCorner ──

        [Test]
        public void AdvanceCorner_FarFromNextCorner_StaysPut() =>
            Assert.AreEqual(1, BotPathMath.AdvanceCorner(Route, Route.Length, 1, new Vector3(0f, 0f, 1f), BotPathMath.CornerReach));

        [Test]
        public void AdvanceCorner_WithinReach_MovesToTheNextCorner() =>
            Assert.AreEqual(2, BotPathMath.AdvanceCorner(Route, Route.Length, 1, new Vector3(0.1f, 0f, 2.8f), BotPathMath.CornerReach));

        [Test]
        public void AdvanceCorner_SkipsEveryCornerAlreadyReached()
        {
            // Shoved straight onto the third corner: the second is far behind but the call starts at index 1,
            // so it stops there — corners are visited in order, never skipped past an unreached one.
            Assert.AreEqual(1, BotPathMath.AdvanceCorner(Route, Route.Length, 1, new Vector3(4f, 1.5f, 3f), BotPathMath.CornerReach));
            // Two corners stacked inside the reach are both consumed in one step.
            var stacked = new[] { Vector3.zero, new Vector3(0f, 0f, 0.2f), new Vector3(0f, 0f, 0.3f), new Vector3(0f, 0f, 5f) };
            Assert.AreEqual(3, BotPathMath.AdvanceCorner(stacked, stacked.Length, 1, Vector3.zero, BotPathMath.CornerReach));
        }

        [Test]
        public void AdvanceCorner_IgnoresHeight()
        {
            // Standing under the ramp-top corner (1.5 below it) still counts as being there.
            Assert.AreEqual(3, BotPathMath.AdvanceCorner(Route, Route.Length, 2, new Vector3(4f, 0f, 3f), BotPathMath.CornerReach));
        }

        [Test]
        public void AdvanceCorner_PastTheLastCorner_ReturnsCount() =>
            Assert.AreEqual(Route.Length, BotPathMath.AdvanceCorner(Route, Route.Length, 3, new Vector3(8f, 1.5f, 3.1f), BotPathMath.CornerReach));

        [Test]
        public void AdvanceCorner_BadInputs_AreSafe()
        {
            Assert.AreEqual(0, BotPathMath.AdvanceCorner(null, 0, 0, Vector3.zero, BotPathMath.CornerReach));
            Assert.AreEqual(1, BotPathMath.AdvanceCorner(Route, Route.Length, -5, new Vector3(0.1f, 0f, 0f), BotPathMath.CornerReach),
                "a negative index starts from the first corner");
            Assert.AreEqual(Route.Length, BotPathMath.AdvanceCorner(Route, 99, 4, Vector3.zero, BotPathMath.CornerReach),
                "a count past the buffer is clamped to the buffer");
        }

        // ── NeedsReplan ──

        [Test]
        public void NeedsReplan_NoRouteYet() =>
            Assert.IsTrue(BotPathMath.NeedsReplan(false, 0, 0, Vector3.zero, Vector3.zero, 0f));

        [Test]
        public void NeedsReplan_SameGoalSameMesh_KeepsTheRoute() =>
            Assert.IsFalse(BotPathMath.NeedsReplan(true, 3, 3, Vector3.zero, new Vector3(0.5f, 0f, 0.5f), 0.4f));

        [Test]
        public void NeedsReplan_MeshRebaked() =>
            Assert.IsTrue(BotPathMath.NeedsReplan(true, 3, 4, Vector3.zero, Vector3.zero, 0f));

        [Test]
        public void NeedsReplan_GoalDrifted() =>
            Assert.IsTrue(BotPathMath.NeedsReplan(true, 3, 3, Vector3.zero, new Vector3(BotPathMath.GoalDriftReplan + 0.1f, 0f, 0f), 0f));

        [Test]
        public void NeedsReplan_GoalOnlyMovedVertically_KeepsTheRoute() =>
            // A carrier lifting the cargo raises the handle; the route on the floor is still right.
            Assert.IsFalse(BotPathMath.NeedsReplan(true, 3, 3, Vector3.zero, new Vector3(0f, 2f, 0f), 0f));

        [Test]
        public void NeedsReplan_IntervalElapsed() =>
            Assert.IsTrue(BotPathMath.NeedsReplan(true, 3, 3, Vector3.zero, Vector3.zero, BotPathMath.ReplanInterval));

        // ── IsStalled ──

        private const float Walk = 4.5f;   // GameConstants.PlayerMoveSpeed

        [TestCase(1f / 30f)]
        [TestCase(1f / 60f)]
        [TestCase(1f / 144f)]
        [TestCase(1f / 240f)]
        public void IsStalled_AnHonestHaul_HoweverSlow_IsNotAStall_AtAnyFrameRate(float dt)
        {
            // 4.5 walking; 1.66 one bot alone on a four-hand cargo; 0.93 the same with Hands + Insured + Marathon
            // bought. The slow ones cover 0.0065–0.0115 u a frame at 144 fps — under the old fixed 0.02 u
            // threshold, which is why bots on a fast host kept hopping mid-haul.
            foreach (float speed in new[] { Walk, 1.66f, 0.93f })
                Assert.IsFalse(BotPathMath.IsStalled(Vector3.zero, new Vector3(speed * dt, 0f, 0f), speed, dt),
                    $"{speed} u/s");
        }

        [TestCase(1f / 30f)]
        [TestCase(1f / 60f)]
        [TestCase(1f / 144f)]
        public void IsStalled_PressingIntoAWall_IsAStall_AtAnyFrameRate(float dt)
        {
            Assert.IsTrue(BotPathMath.IsStalled(Vector3.one, Vector3.one, Walk, dt));
            Assert.IsTrue(BotPathMath.IsStalled(Vector3.one, Vector3.one, 0.93f, dt));
            // Sliding a little along the wall (0.3 of the 4.5 asked for) is still going nowhere.
            Assert.IsTrue(BotPathMath.IsStalled(Vector3.zero, new Vector3(0f, 0f, 0.3f * dt), Walk, dt));
        }

        [Test]
        public void IsStalled_ABotAskedForNothing_IsNeverAStall()
        {
            Assert.IsFalse(BotPathMath.IsStalled(Vector3.zero, Vector3.zero, 0f, 1f / 60f), "frozen: no speed asked for");
            Assert.IsFalse(BotPathMath.IsStalled(Vector3.zero, Vector3.zero, Walk, 0f), "no time in the frame");
        }

        // ── StepStallClock ──

        [TestCase(1f / 30f)]
        [TestCase(1f / 60f)]
        [TestCase(1f / 144f)]
        public void StallClock_ABlockedBot_TripsAfterStallSeconds_AtAnyFrameRate(float dt)
        {
            float clock = 0f, elapsed = 0f;
            while (clock <= BotPathMath.StallSeconds && elapsed < 5f)
            {
                clock = BotPathMath.StepStallClock(clock, Vector3.one, Vector3.one, Walk, dt);
                elapsed += dt;
            }
            Assert.That(elapsed, Is.InRange(BotPathMath.StallSeconds - dt, BotPathMath.StallSeconds + 2f * dt));
        }

        [Test]
        public void StallClock_Headway_ResetsIt()
        {
            const float dt = 1f / 60f;
            Assert.AreEqual(0f, BotPathMath.StepStallClock(0.7f, Vector3.zero, new Vector3(Walk * dt, 0f, 0f), Walk, dt));
        }

        [Test]
        public void StallClock_OneLongHitch_DoesNotTripTheHop()
        {
            // The step being judged can be the previous (short) frame's while dt is this (long) frame's.
            Vector3 honestStep = new Vector3(Walk / 60f, 0f, 0f);
            float clock = BotPathMath.StepStallClock(0f, Vector3.zero, honestStep, Walk, 1.5f);
            Assert.LessOrEqual(clock, BotPathMath.StallMaxStep);
            Assert.Less(clock, BotPathMath.StallSeconds);
        }

        // ── IsBakeSource ──

        [Test]
        public void IsBakeSource_OnlyStaticSolidColliders()
        {
            Assert.IsTrue (BotPathMath.IsBakeSource(isTrigger: false, hasRigidbody: false, isCharacterController: false), "course geometry");
            Assert.IsFalse(BotPathMath.IsBakeSource(isTrigger: true,  hasRigidbody: false, isCharacterController: false), "delivery zone / hazards");
            Assert.IsFalse(BotPathMath.IsBakeSource(isTrigger: false, hasRigidbody: true,  isCharacterController: false), "cargo");
            Assert.IsFalse(BotPathMath.IsBakeSource(isTrigger: false, hasRigidbody: false, isCharacterController: true),  "players and bots");
        }
    }
}
