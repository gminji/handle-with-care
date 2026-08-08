using NUnit.Framework;
using UnityEngine;
using SlopCo.Player;

namespace SlopCo.Tests.EditMode
{
    /// <summary>
    /// Pins the two camera viewpoints (CameraRig) — pure Vector3 math, same shape as ExplosionShoveTests.
    /// Third person must stay the shipped 3/4 view; first person must sit at the eyes and never produce a
    /// degenerate look direction.
    /// </summary>
    public class CameraRigTests
    {
        static readonly Vector3 Target = new Vector3(3f, 0f, -7f);

        [Test]
        public void Third_KeepsTheShippedOffset()
        {
            var p = CameraRig.DesiredPosition(false, Target, Vector3.forward);
            Assert.AreEqual(Target + new Vector3(0f, 5f, -9f), p);
        }

        [Test]
        public void Third_IgnoresFacing()
        {
            var a = CameraRig.DesiredPosition(false, Target, Vector3.forward);
            var b = CameraRig.DesiredPosition(false, Target, Vector3.left);
            Assert.AreEqual(a, b);   // fixed-angle chase cam: turning must not swing the camera
        }

        [Test]
        public void Third_LooksAboveTheFeet()
        {
            var look = CameraRig.DesiredLookAt(false, Target, Vector3.forward);
            Assert.AreEqual(Target + Vector3.up * CameraRig.ThirdLookUp, look);
        }

        [Test]
        public void First_SitsAtEyeHeightNudgedForward()
        {
            var p = CameraRig.DesiredPosition(true, Target, Vector3.forward);
            Assert.AreEqual(CameraRig.FirstEyeHeight, p.y - Target.y, 1e-4f);
            Assert.AreEqual(CameraRig.FirstEyeAhead, p.z - Target.z, 1e-4f);
        }

        [Test]
        public void First_LooksAlongFacing_Horizontally()
        {
            var p = CameraRig.DesiredPosition(true, Target, Vector3.left);
            var look = CameraRig.DesiredLookAt(true, Target, Vector3.left);
            Vector3 dir = look - p;
            Assert.AreEqual(0f, dir.y, 1e-4f);                       // level gaze
            Assert.AreEqual(Vector3.left, dir.normalized);
        }

        [Test]
        public void First_TiltedFacing_StaysLevel()
        {
            // A facing with a vertical component must not tip the camera up or down.
            var f = new Vector3(1f, 4f, 0f);
            var p = CameraRig.DesiredPosition(true, Target, f);
            var look = CameraRig.DesiredLookAt(true, Target, f);
            Assert.AreEqual(0f, (look - p).y, 1e-4f);
            Assert.AreEqual(CameraRig.FirstEyeHeight, p.y - Target.y, 1e-4f);
        }

        [Test]
        public void First_DegenerateFacing_StillGivesAUsableLookDirection()
        {
            foreach (var f in new[] { Vector3.zero, Vector3.up, Vector3.down })
            {
                var p = CameraRig.DesiredPosition(true, Target, f);
                var look = CameraRig.DesiredLookAt(true, Target, f);
                Assert.Greater((look - p).sqrMagnitude, 0.001f, "look vector collapsed for facing " + f);
            }
        }

        [Test]
        public void Flatten_DropsVerticalAndNormalizes()
        {
            var v = CameraRig.Flatten(new Vector3(0f, 9f, 2f));
            Assert.AreEqual(Vector3.forward, v);
            Assert.AreEqual(1f, CameraRig.Flatten(new Vector3(3f, 0f, 4f)).magnitude, 1e-4f);
        }

        [Test]
        public void ViewpointsDiffer()
        {
            Assert.AreNotEqual(CameraRig.DesiredPosition(true, Target, Vector3.forward),
                               CameraRig.DesiredPosition(false, Target, Vector3.forward));
        }

        // ── Pitch overload (mouse look) ──────────────────────────────────────────────────────

        [Test]
        public void First_ZeroPitch_MatchesLegacyOverload()
        {
            var legacy = CameraRig.DesiredLookAt(true, Target, Vector3.forward);
            var withPitch = CameraRig.DesiredLookAt(true, Target, Vector3.forward, 0f);
            Assert.AreEqual(legacy, withPitch); // bit-for-bit: Cos(0)==1f, Sin(0)==0f exactly (R4)
        }

        [Test]
        public void First_PositivePitch_LooksUp()
        {
            var level = CameraRig.DesiredLookAt(true, Target, Vector3.forward, 0f);
            var up = CameraRig.DesiredLookAt(true, Target, Vector3.forward, 30f);
            Assert.Greater(up.y, level.y);
        }

        [Test]
        public void First_NegativePitch_LooksDown()
        {
            var level = CameraRig.DesiredLookAt(true, Target, Vector3.forward, 0f);
            var down = CameraRig.DesiredLookAt(true, Target, Vector3.forward, -30f);
            Assert.Less(down.y, level.y);
        }

        [Test]
        public void First_Pitch_KeepsHorizontalBearing()
        {
            var eye = Target + Vector3.up * CameraRig.FirstEyeHeight;
            var look = CameraRig.DesiredLookAt(true, Target, Vector3.forward, 40f);
            Vector3 horizontal = look - eye;
            horizontal.y = 0f;
            Assert.AreEqual(0f, horizontal.x, 1e-4f);
            Assert.Greater(horizontal.z, 0f); // still pointed down +Z, just angled up
        }

        [Test]
        public void First_PitchAtClampLimit_IsUnitFromTheEye()
        {
            // Anchor at the EYE point, not DesiredPosition — DesiredPosition adds f * FirstEyeAhead
            // (0.18), which would make the magnitude 9.9859 instead of FirstLookAhead (10). See §5.4.
            Vector3 eye = Target + Vector3.up * CameraRig.FirstEyeHeight;
            foreach (float pitch in new[] { CameraRig.PitchMax, CameraRig.PitchMin })
            {
                Vector3 look = CameraRig.DesiredLookAt(true, Target, Vector3.forward, pitch);
                Vector3 gaze = look - eye;
                Assert.AreEqual(CameraRig.FirstLookAhead, gaze.magnitude, 1e-3f, "pitch " + pitch);
                Assert.AreEqual(Mathf.Sin(pitch * Mathf.Deg2Rad), gaze.normalized.y, 1e-4f, "pitch " + pitch);
            }
        }

        [Test]
        public void First_PitchAffectsOnlyTheAimPoint()
        {
            var lookNeg = CameraRig.DesiredLookAt(true, Target, Vector3.forward, -85f);
            var look0 = CameraRig.DesiredLookAt(true, Target, Vector3.forward, 0f);
            var lookPos = CameraRig.DesiredLookAt(true, Target, Vector3.forward, 85f);
            Assert.AreNotEqual(lookNeg, look0);
            Assert.AreNotEqual(look0, lookPos);
            Assert.AreNotEqual(lookNeg, lookPos);

            // DesiredPosition takes no pitch argument at all — pitch must never leak into eye placement.
            var posNeg = CameraRig.DesiredPosition(true, Target, Vector3.forward);
            var pos0 = CameraRig.DesiredPosition(true, Target, Vector3.forward);
            var posPos = CameraRig.DesiredPosition(true, Target, Vector3.forward);
            Assert.AreEqual(posNeg, pos0);
            Assert.AreEqual(pos0, posPos);
        }

        [Test]
        public void Third_IgnoresPitch()
        {
            var a = CameraRig.DesiredLookAt(false, Target, Vector3.forward, -85f);
            var b = CameraRig.DesiredLookAt(false, Target, Vector3.forward, 0f);
            var c = CameraRig.DesiredLookAt(false, Target, Vector3.forward, 85f);
            Assert.AreEqual(a, b);
            Assert.AreEqual(b, c);
        }

        [Test]
        public void PitchLimits_AgreeWithLookMath()
        {
            // Two independent siblings own the same range on purpose — pinned here so an asymmetric
            // retune of one silently drifting from the other fails loudly instead of only in-game.
            Assert.AreEqual(LookMath.PitchMax, CameraRig.PitchMax);
            Assert.AreEqual(LookMath.PitchMin, CameraRig.PitchMin);
        }
    }
}
