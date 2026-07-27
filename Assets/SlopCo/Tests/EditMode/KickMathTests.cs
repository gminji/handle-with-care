using NUnit.Framework;
using UnityEngine;
using SlopCo.Player;

namespace SlopCo.Tests.EditMode
{
    /// <summary>
    /// Pins the kick arc and launch curve (KickMath) — pure Vector3 math, same shape as ExplosionShoveTests.
    /// The server resolves every kick through these two functions, so the reach and the falloff live here.
    /// </summary>
    public class KickMathTests
    {
        const float Range = 2.6f;
        const float HalfAngle = 65f;
        const float Speed = 11f;

        // ── InCone ──

        [Test]
        public void InCone_StraightAhead_Hits() =>
            Assert.IsTrue(KickMath.InCone(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 2f), Range, HalfAngle));

        [Test]
        public void InCone_BehindTheKicker_Misses() =>
            Assert.IsFalse(KickMath.InCone(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, -2f), Range, HalfAngle));

        [Test]
        public void InCone_BeyondRange_Misses() =>
            Assert.IsFalse(KickMath.InCone(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, Range + 0.5f), Range, HalfAngle));

        [Test]
        public void InCone_JustInsideAndOutsideTheArc()
        {
            // 60° off the facing is inside a ±65° arc; 75° is outside.
            var inside  = Quaternion.Euler(0f, 60f, 0f) * Vector3.forward * 2f;
            var outside = Quaternion.Euler(0f, 75f, 0f) * Vector3.forward * 2f;
            Assert.IsTrue (KickMath.InCone(Vector3.zero, Vector3.forward, inside,  Range, HalfAngle));
            Assert.IsFalse(KickMath.InCone(Vector3.zero, Vector3.forward, outside, Range, HalfAngle));
        }

        [Test]
        public void InCone_IgnoresHeight()
        {
            // Someone standing on a crate directly in front is still kickable.
            var up = new Vector3(0f, 3f, 2f);
            Assert.IsTrue(KickMath.InCone(Vector3.zero, Vector3.forward, up, Range, HalfAngle));
        }

        [Test]
        public void InCone_PointBlankOverlap_Hits() =>
            Assert.IsTrue(KickMath.InCone(Vector3.zero, Vector3.forward, Vector3.zero, Range, HalfAngle));

        [Test]
        public void InCone_NoFacingOrNoRange_Misses()
        {
            Assert.IsFalse(KickMath.InCone(Vector3.zero, Vector3.zero, new Vector3(0f, 0f, 1f), Range, HalfAngle));
            Assert.IsFalse(KickMath.InCone(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 1f), 0f, HalfAngle));
        }

        // ── Impulse ──

        [Test]
        public void Impulse_IsHorizontal()
        {
            var v = KickMath.Impulse(Vector3.zero, Vector3.forward, new Vector3(0f, 2f, 1.5f), Range, Speed);
            Assert.AreEqual(0f, v.y, 1e-5f);   // the vertical pop is the caller's job
        }

        [Test]
        public void Impulse_PointBlank_IsFullSpeedAlongFacing()
        {
            var v = KickMath.Impulse(Vector3.zero, Vector3.forward, Vector3.zero, Range, Speed);
            Assert.AreEqual(Speed, v.magnitude, 0.01f);
            Assert.AreEqual(Vector3.forward, v.normalized);
        }

        [Test]
        public void Impulse_AtMaxRange_KeepsTheFloorStrength()
        {
            var v = KickMath.Impulse(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, Range), Range, Speed);
            Assert.AreEqual(Speed * KickMath.MinFalloff, v.magnitude, 0.01f);
            Assert.Greater(v.magnitude, 0f, "a connecting kick must always launch the victim");
        }

        [Test]
        public void Impulse_FallsOffWithDistance()
        {
            float near = KickMath.Impulse(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 0.5f), Range, Speed).magnitude;
            float far  = KickMath.Impulse(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 2.4f), Range, Speed).magnitude;
            Assert.Greater(near, far);
        }

        [Test]
        public void Impulse_BeyondRange_IsZero() =>
            Assert.AreEqual(Vector3.zero, KickMath.Impulse(Vector3.zero, Vector3.forward, new Vector3(0f, 0f, 9f), Range, Speed));

        [Test]
        public void Impulse_BlendsAwayDirectionWithFacing()
        {
            // Victim off to the right of a forward-facing kicker: pushed forward AND outward, never backwards.
            var v = KickMath.Impulse(Vector3.zero, Vector3.forward, new Vector3(1.5f, 0f, 0.3f), Range, Speed);
            Assert.Greater(v.x, 0f, "should be shoved away from the kicker");
            Assert.Greater(v.z, 0f, "should also carry the kicker's facing");
        }

        [Test]
        public void Impulse_NonPositiveRange_IsZero() =>
            Assert.AreEqual(Vector3.zero, KickMath.Impulse(Vector3.zero, Vector3.forward, Vector3.forward, 0f, Speed));
    }
}
