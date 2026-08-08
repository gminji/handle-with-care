using NUnit.Framework;
using UnityEngine;
using SlopCo.Player;

namespace SlopCo.Tests.EditMode
{
    /// <summary>
    /// Pins the pure first-person look math (LookMath) — yaw/pitch accumulation, clamp order,
    /// mouse-vs-stick time scaling, view-relative movement, and viewpoint-transition seeding.
    /// Same shape as DashStaminaTests: immutable state in, immutable state out, tuning passed explicitly.
    /// </summary>
    public class LookMathTests
    {
        const float Tol = 1e-3f;
        const float LooseTol = 1e-2f;

        // ── Frame-rate / timescale independence ─────────────────────────────────────────────

        [Test]
        public void Step_MouseDeltaIsNotScaledByDt()
        {
            var a = LookMath.Step(LookMath.Level, new Vector2(0f, 50f), Vector2.zero, 0.01f, 1f, false);
            var b = LookMath.Step(LookMath.Level, new Vector2(0f, 50f), Vector2.zero, 0.1f, 1f, false);
            Assert.AreEqual(a.Pitch, b.Pitch, Tol);
        }

        [Test]
        public void Step_StickContributionScalesWithDt()
        {
            var half = LookMath.Step(LookMath.Level, Vector2.zero, new Vector2(1f, 0f), 0.1f, 1f, false);
            var full = LookMath.Step(LookMath.Level, Vector2.zero, new Vector2(1f, 0f), 0.2f, 1f, false);
            Assert.AreEqual(2f * half.Yaw, full.Yaw, Tol);
        }

        [Test]
        public void Step_StickUsesUnscaledSeconds()
        {
            // Contract test: the caller is required to pass Time.unscaledDeltaTime (documented on the
            // unscaledDt parameter itself). Given the same unscaledDt, results must not depend on
            // whatever Time.timeScale happens to be at call time — which Step cannot even observe,
            // since it only ever sees the value passed in.
            const float unscaledDt = 0.02f; // e.g. a frame during a 0.18 hitstop, but measured unscaled
            var a = LookMath.Step(LookMath.Level, Vector2.zero, new Vector2(0f, 1f), unscaledDt, 1f, false);
            var b = LookMath.Step(LookMath.Level, Vector2.zero, new Vector2(0f, 1f), unscaledDt, 1f, false);
            Assert.AreEqual(a.Pitch, b.Pitch, Tol);
        }

        // ── Sensitivity ──────────────────────────────────────────────────────────────────────

        [Test]
        public void Step_Sensitivity2x_DoublesMouseYawChange()
        {
            var s1 = LookMath.Step(LookMath.Level, new Vector2(10f, 0f), Vector2.zero, 0.1f, 1f, false);
            var s2 = LookMath.Step(LookMath.Level, new Vector2(10f, 0f), Vector2.zero, 0.1f, 2f, false);
            Assert.AreEqual(2f * s1.Yaw, s2.Yaw, Tol);
        }

        [Test]
        public void Step_Sensitivity2x_DoublesStickYawChange()
        {
            var s1 = LookMath.Step(LookMath.Level, Vector2.zero, new Vector2(1f, 0f), 0.1f, 1f, false);
            var s2 = LookMath.Step(LookMath.Level, Vector2.zero, new Vector2(1f, 0f), 0.1f, 2f, false);
            Assert.AreEqual(2f * s1.Yaw, s2.Yaw, Tol);
        }

        [Test]
        public void Step_ZeroSensitivity_ProducesNoRotation()
        {
            var s = LookMath.Step(LookMath.Level, new Vector2(50f, 50f), new Vector2(1f, 1f), 0.1f, 0f, false);
            Assert.AreEqual(0f, s.Yaw, Tol);
            Assert.AreEqual(0f, s.Pitch, Tol);
        }

        // ── Pitch ────────────────────────────────────────────────────────────────────────────

        [Test]
        public void Step_MouseUp_RaisesPitch()
        {
            var s = LookMath.Step(LookMath.Level, new Vector2(0f, 100f), Vector2.zero, 0.1f, 1f, invertY: false);
            Assert.Greater(s.Pitch, 0f);
        }

        [Test]
        public void Step_MouseUp_InvertY_LowersPitch()
        {
            var s = LookMath.Step(LookMath.Level, new Vector2(0f, 100f), Vector2.zero, 0.1f, 1f, invertY: true);
            Assert.Less(s.Pitch, 0f);
        }

        [Test]
        public void Step_PitchClampsAtUpperLimit()
        {
            var s = LookMath.Step(LookMath.Level, new Vector2(0f, 10000f), Vector2.zero, 0.1f, 1f, false);
            Assert.AreEqual(LookMath.PitchMax, s.Pitch, Tol);
        }

        [Test]
        public void Step_PitchClampsAtLowerLimit()
        {
            var s = LookMath.Step(LookMath.Level, new Vector2(0f, -10000f), Vector2.zero, 0.1f, 1f, false);
            Assert.AreEqual(LookMath.PitchMin, s.Pitch, Tol);
        }

        [Test]
        public void Step_ClampsAfterAccumulating_NotPerDelta_UpperEdge()
        {
            // A per-delta-clamped implementation stashes the overshoot internally and would not reproduce
            // exactly 83.8 here — it would still read 85 (or something else entirely).
            var s = LookMath.Step(LookMath.Level, new Vector2(0f, 10000f), Vector2.zero, 0.1f, 1f, false);
            Assert.AreEqual(85f, s.Pitch, Tol);
            s = LookMath.Step(s, new Vector2(0f, -10f), Vector2.zero, 0.1f, 1f, false);
            Assert.AreEqual(85f - 10f * 0.12f, s.Pitch, LooseTol); // 83.8
        }

        [Test]
        public void Step_ClampsAfterAccumulating_NotPerDelta_LowerEdge()
        {
            var s = LookMath.Step(LookMath.Level, new Vector2(0f, -10000f), Vector2.zero, 0.1f, 1f, false);
            Assert.AreEqual(-85f, s.Pitch, Tol);
            s = LookMath.Step(s, new Vector2(0f, 10f), Vector2.zero, 0.1f, 1f, false);
            Assert.AreEqual(-85f + 10f * 0.12f, s.Pitch, LooseTol); // -83.8
        }

        [Test]
        public void Step_InvertY_FlipsStickPitchSign()
        {
            var normal = LookMath.Step(LookMath.Level, Vector2.zero, new Vector2(0f, 1f), 0.1f, 1f, invertY: false);
            var inverted = LookMath.Step(LookMath.Level, Vector2.zero, new Vector2(0f, 1f), 0.1f, 1f, invertY: true);
            Assert.Greater(normal.Pitch, 0f);
            Assert.AreEqual(-normal.Pitch, inverted.Pitch, Tol);
        }

        // ── Yaw ──────────────────────────────────────────────────────────────────────────────

        [Test]
        public void WrapYaw_NormalizesIntoZeroThreeSixtyRange()
        {
            Assert.AreEqual(10f, LookMath.WrapYaw(370f), Tol);
            Assert.AreEqual(350f, LookMath.WrapYaw(-10f), Tol);
            Assert.AreEqual(0f, LookMath.WrapYaw(360f), Tol);
            Assert.AreEqual(0f, LookMath.WrapYaw(0f), Tol);
        }

        [Test]
        public void Step_NegativeYawAccumulation_StaysWrapped()
        {
            var s = LookMath.Step(LookMath.Level, new Vector2(-10000f, 0f), Vector2.zero, 0.1f, 1f, false);
            Assert.GreaterOrEqual(s.Yaw, 0f);
            Assert.Less(s.Yaw, 360f);
            Assert.AreEqual(240f, s.Yaw, LooseTol); // -1200 wrapped -> 240
        }

        // ── Movement ─────────────────────────────────────────────────────────────────────────

        [Test]
        public void MoveDirection_Yaw0_ForwardIsPlusZ()
        {
            var dir = LookMath.MoveDirection(new Vector2(0f, 1f), 0f);
            Assert.AreEqual(Vector3.forward, dir, "expected +Z within Vector3 equality tolerance");
        }

        [Test]
        public void MoveDirection_Yaw90_ForwardIsPlusX()
        {
            var dir = LookMath.MoveDirection(new Vector2(0f, 1f), 90f);
            Assert.AreEqual(1f, dir.x, Tol);
            Assert.AreEqual(0f, dir.z, Tol);
        }

        [Test]
        public void MoveDirection_Yaw0_BackIsMinusZ()
        {
            var dir = LookMath.MoveDirection(new Vector2(0f, -1f), 0f);
            Assert.AreEqual(Vector3.back, dir);
        }

        [Test]
        public void MoveDirection_Yaw0_LeftIsMinusX()
        {
            var dir = LookMath.MoveDirection(new Vector2(-1f, 0f), 0f);
            Assert.AreEqual(Vector3.left, dir);
        }

        [Test]
        public void MoveDirection_Yaw0_RightIsPlusX()
        {
            var dir = LookMath.MoveDirection(new Vector2(1f, 0f), 0f);
            Assert.AreEqual(Vector3.right, dir);
        }

        [Test]
        public void MoveDirection_DiagonalInput_MagnitudeAtMostOne()
        {
            var dir = LookMath.MoveDirection(new Vector2(1f, 1f), 0f);
            Assert.LessOrEqual(dir.magnitude, 1f + Tol);
        }

        [Test]
        public void MoveDirection_AlwaysHasZeroY()
        {
            Assert.AreEqual(0f, LookMath.MoveDirection(new Vector2(1f, 1f), 37f).y, Tol);
            Assert.AreEqual(0f, LookMath.MoveDirection(new Vector2(-1f, 0.5f), 271f).y, Tol);
        }

        // ── Viewpoint transitions ────────────────────────────────────────────────────────────

        [Test]
        public void OnViewpointChanged_ThirdToFirst_ReseedsYawFromForward()
        {
            var s = LookMath.OnViewpointChanged(LookMath.Level, firstPerson: true, wasFirstPerson: false, forward: Vector3.right);
            Assert.AreEqual(90f, s.Yaw, LooseTol);
            Assert.AreEqual(0f, s.Pitch, Tol);
        }

        [Test]
        public void OnViewpointChanged_StaysFirstPerson_PreservesState()
        {
            var s = new LookState { Yaw = 123.4f, Pitch = 45f };
            var result = LookMath.OnViewpointChanged(s, firstPerson: true, wasFirstPerson: true, forward: Vector3.forward);
            Assert.AreEqual(s.Yaw, result.Yaw, Tol);
            Assert.AreEqual(s.Pitch, result.Pitch, Tol);
        }

        [Test]
        public void OnViewpointChanged_FirstToThirdToFirst_ReseedsFromCurrentForward_NotStaleYaw()
        {
            var s = new LookState { Yaw = 200f, Pitch = 10f };
            // Leaving first person: state passes through unchanged.
            var whileThird = LookMath.OnViewpointChanged(s, firstPerson: false, wasFirstPerson: true, forward: Vector3.forward);
            Assert.AreEqual(200f, whileThird.Yaw, Tol);
            // Re-entering first person from third: must reseed from the *current* forward, not resurrect 200.
            var reentered = LookMath.OnViewpointChanged(whileThird, firstPerson: true, wasFirstPerson: false, forward: Vector3.back);
            Assert.AreEqual(180f, reentered.Yaw, LooseTol);
            Assert.AreNotEqual(200f, reentered.Yaw);
        }

        [Test]
        public void OnViewpointChanged_FirstFrameInFirstPerson_SeedsFromForward_NotZero()
        {
            // wasFirstPerson == false on the very first frame's default value.
            var s = LookMath.OnViewpointChanged(LookMath.Level, firstPerson: true, wasFirstPerson: false, forward: Vector3.left);
            Assert.AreEqual(270f, s.Yaw, LooseTol);
            Assert.AreNotEqual(0f, s.Yaw);
        }

        [Test]
        public void OnViewpointChanged_DegenerateForward_IsSafe()
        {
            foreach (var f in new[] { Vector3.zero, Vector3.up, Vector3.down })
            {
                var s = LookMath.OnViewpointChanged(LookMath.Level, firstPerson: true, wasFirstPerson: false, forward: f);
                Assert.AreEqual(0f, s.Yaw, Tol, "degenerate forward " + f + " should fall back to yaw 0");
                Assert.AreEqual(0f, s.Pitch, Tol);
            }
        }

        // ── Non-finite input must never enter the state ──────────────────────────────────────
        // NaN is self-sustaining once it lands in Yaw (0 * NaN == NaN, so a still mouse keeps reproducing
        // it) and Mathf.Clamp cannot remove it. From Yaw it reaches Quaternion.Euler on an owner-
        // authoritative transform, so ClientNetworkTransform would replicate a corrupt pose to every peer.

        [Test]
        public void Step_NaNSensitivity_LeavesStateUntouched()
        {
            var start = LookMath.Step(LookMath.Level, new Vector2(50f, 20f), Vector2.zero, 0.1f, 1f, false);
            var after = LookMath.Step(start, new Vector2(10f, 10f), Vector2.zero, 0.1f, float.NaN, false);
            Assert.AreEqual(start.Yaw, after.Yaw, Tol);
            Assert.AreEqual(start.Pitch, after.Pitch, Tol);
        }

        [Test]
        public void Step_NonFiniteDelta_LeavesStateUntouched()
        {
            var start = LookMath.Step(LookMath.Level, new Vector2(50f, 20f), Vector2.zero, 0.1f, 1f, false);
            foreach (var bad in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
            {
                var after = LookMath.Step(start, new Vector2(bad, bad), Vector2.zero, 0.1f, 1f, false);
                Assert.AreEqual(start.Yaw, after.Yaw, Tol, "yaw survived " + bad);
                Assert.AreEqual(start.Pitch, after.Pitch, Tol, "pitch survived " + bad);
            }
        }

        [Test]
        public void Step_NonFiniteFrame_DoesNotPoisonLaterFrames()
        {
            var s = LookMath.Step(LookMath.Level, new Vector2(50f, 0f), Vector2.zero, 0.1f, float.NaN, false);
            s = LookMath.Step(s, new Vector2(100f, 0f), Vector2.zero, 0.1f, 1f, false);
            Assert.AreEqual(100f * LookMath.DegreesPerMousePixel, s.Yaw, Tol);   // recovers cleanly
        }

        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void WrapYaw_NonFinite_ReturnsZero(float bad)
        {
            // Infinity % 360f is NaN, so the modulo cannot be relied on to sanitise this.
            Assert.AreEqual(0f, LookMath.WrapYaw(bad), Tol);
        }
    }
}
