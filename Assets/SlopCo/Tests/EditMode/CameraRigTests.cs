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
    }
}
