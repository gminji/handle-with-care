using NUnit.Framework;
using UnityEngine;
using SlopCo.UI;

namespace SlopCo.Tests.EditMode
{
    /// <summary>
    /// Verifies the pure radial-wheel selection math (WheelMath) without any MonoBehaviour runtime — mirrors
    /// FlybyOrbitTests / VoiceActivityTests. Convention: slice 0 at 12 o'clock (+y up), clockwise.
    /// </summary>
    public class WheelMathTests
    {
        private const int Count = 6;
        private const float Dz = 0.25f;

        [Test]
        public void SelectSlice_InsideDeadzone_ReturnsCancel()
        {
            Assert.That(WheelMath.SelectSlice(Vector2.zero, Count, Dz), Is.EqualTo(-1));
            Assert.That(WheelMath.SelectSlice(new Vector2(0f, 0.2f), Count, Dz), Is.EqualTo(-1));
        }

        [Test]
        public void SelectSlice_StraightUp_IsSliceZero()
        {
            Assert.That(WheelMath.SelectSlice(new Vector2(0f, 1f), Count, Dz), Is.EqualTo(0));
        }

        [Test]
        public void SelectSlice_EachSliceCenter_MapsToItself()
        {
            for (int i = 0; i < Count; i++)
            {
                Vector2 dir = WheelMath.SliceCenterDir(i, Count); // unit vector at slice i's center
                Assert.That(WheelMath.SelectSlice(dir, Count, Dz), Is.EqualTo(i), $"slice {i}");
            }
        }

        [Test]
        public void SelectSlice_WrapsNear360_BackToZero()
        {
            // A hair clockwise of straight-up (~359°) must round back to slice 0, not slice Count.
            float a = 359f * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
            Assert.That(WheelMath.SelectSlice(dir, Count, Dz), Is.EqualTo(0));
        }

        [Test]
        public void SelectSlice_AlwaysInRange()
        {
            for (int deg = 0; deg < 360; deg += 7)
            {
                float a = deg * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
                int s = WheelMath.SelectSlice(dir, Count, Dz);
                Assert.That(s, Is.InRange(0, Count - 1), $"deg {deg}");
            }
        }

        [Test]
        public void SelectSlice_NonPositiveCount_ReturnsCancel()
        {
            Assert.That(WheelMath.SelectSlice(new Vector2(0f, 1f), 0, Dz), Is.EqualTo(-1));
        }

        [Test]
        public void SliceCenterDir_SliceZero_IsUp()
        {
            Vector2 d = WheelMath.SliceCenterDir(0, Count);
            Assert.That(d.x, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(d.y, Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void SliceCenterDir_AllUnitLength()
        {
            for (int i = 0; i < Count; i++)
            {
                Vector2 d = WheelMath.SliceCenterDir(i, Count);
                Assert.That(d.magnitude, Is.EqualTo(1f).Within(1e-4f), $"slice {i}");
            }
        }
    }
}
