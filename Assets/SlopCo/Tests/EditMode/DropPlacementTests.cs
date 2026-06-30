using System;
using NUnit.Framework;
using SlopCo.Items;

namespace SlopCo.Tests.EditMode
{
    /// <summary>Verifies the pure drop-position math (DropPlacement) without UnityEngine.</summary>
    public class DropPlacementTests
    {
        private const float Tol = 1e-3f;

        [Test]
        public void Beyond_AngleZero_StepsAlongFacing()
        {
            // van at (10,0), facing +Z, distance 14 → (10, 14)
            var (x, z) = DropPlacement.Beyond(10f, 0f, 0f, 1f, 14f, 0f);
            Assert.That(x, Is.EqualTo(10f).Within(Tol));
            Assert.That(z, Is.EqualTo(14f).Within(Tol));
        }

        [Test]
        public void Beyond_NormalizesFacing()
        {
            // unnormalized facing (0,5) should behave like (0,1)
            var (x, z) = DropPlacement.Beyond(0f, 0f, 0f, 5f, 10f, 0f);
            Assert.That(x, Is.EqualTo(0f).Within(Tol));
            Assert.That(z, Is.EqualTo(10f).Within(Tol));
        }

        [Test]
        public void Beyond_DegenerateFacing_DefaultsToPlusZ()
        {
            var (x, z) = DropPlacement.Beyond(3f, 3f, 0f, 0f, 5f, 0f);
            Assert.That(x, Is.EqualTo(3f).Within(Tol));
            Assert.That(z, Is.EqualTo(8f).Within(Tol));
        }

        [Test]
        public void Beyond_NinetyDegrees_RotatesFacing()
        {
            // CCW rotation: facing +Z rotated +90° → -X (sign is irrelevant for drops — angle jitter is random ±)
            var (x, z) = DropPlacement.Beyond(0f, 0f, 0f, 1f, 6f, (float)(Math.PI / 2.0));
            Assert.That(x, Is.EqualTo(-6f).Within(Tol));
            Assert.That(z, Is.EqualTo(0f).Within(Tol));
        }

        [Test]
        public void Beyond_NegativeDistance_Clamped()
        {
            var (x, z) = DropPlacement.Beyond(2f, 2f, 0f, 1f, -5f, 0f);
            Assert.That(x, Is.EqualTo(2f).Within(Tol));
            Assert.That(z, Is.EqualTo(2f).Within(Tol));
        }

        [Test]
        public void Beyond_StaysOnDistanceRadius()
        {
            const float d = 12f;
            for (int i = 0; i < 12; i++)
            {
                float a = (float)(Math.PI * 2.0 * i / 12.0);
                var (x, z) = DropPlacement.Beyond(0f, 0f, 0f, 1f, d, a);
                Assert.That(MathF.Sqrt(x * x + z * z), Is.EqualTo(d).Within(Tol), $"angle {a}");
            }
        }
    }
}
