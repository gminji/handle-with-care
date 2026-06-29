using System;
using NUnit.Framework;
using SlopCo.Gameplay;

namespace SlopCo.Tests.EditMode
{
    /// <summary>
    /// Verifies the pure title-flyby orbit math (FlybyOrbit) without any UnityEngine runtime.
    /// </summary>
    public class FlybyOrbitTests
    {
        private const float Tol = 1e-4f;
        private const float TwoPi = (float)(Math.PI * 2.0);

        [Test]
        public void Offsets_AtAngleZero_AreFrontOfOrbit()
        {
            Assert.That(FlybyOrbit.OffsetX(26f, 0f), Is.EqualTo(0f).Within(Tol));
            Assert.That(FlybyOrbit.OffsetZ(26f, 0f), Is.EqualTo(26f).Within(Tol));
        }

        [Test]
        public void Offsets_AtNinetyDegrees_SwapToSide()
        {
            float ninety = (float)(Math.PI / 2.0);
            Assert.That(FlybyOrbit.OffsetX(26f, ninety), Is.EqualTo(26f).Within(Tol));
            Assert.That(FlybyOrbit.OffsetZ(26f, ninety), Is.EqualTo(0f).Within(Tol));
        }

        [Test]
        public void Offsets_ZeroRadius_AreZero()
        {
            Assert.That(FlybyOrbit.OffsetX(0f, 1.23f), Is.EqualTo(0f).Within(Tol));
            Assert.That(FlybyOrbit.OffsetZ(0f, 1.23f), Is.EqualTo(0f).Within(Tol));
        }

        [Test]
        public void Offsets_StayOnRadiusCircle()
        {
            const float r = 26f;
            for (int i = 0; i < 16; i++)
            {
                float a = TwoPi * (i / 16f);
                float x = FlybyOrbit.OffsetX(r, a);
                float z = FlybyOrbit.OffsetZ(r, a);
                Assert.That(MathF.Sqrt(x * x + z * z), Is.EqualTo(r).Within(1e-3f), $"angle {a}");
            }
        }

        [Test]
        public void HeightForElevation_At45_EqualsDistance()
        {
            // tan(45°) ≈ 0.99999994 → use a tolerance, not exact equality.
            Assert.That(FlybyOrbit.HeightForElevation(26f, 45f), Is.EqualTo(26f).Within(1e-2f));
        }

        [Test]
        public void HeightForElevation_DegenerateInputs_AreZero()
        {
            Assert.That(FlybyOrbit.HeightForElevation(0f, 45f), Is.EqualTo(0f));
            Assert.That(FlybyOrbit.HeightForElevation(-5f, 45f), Is.EqualTo(0f));
            Assert.That(FlybyOrbit.HeightForElevation(26f, 0f), Is.EqualTo(0f));
        }

        [Test]
        public void HeightForElevation_IsMonotonicInElevation()
        {
            float prev = FlybyOrbit.HeightForElevation(26f, 10f);
            for (int deg = 20; deg <= 80; deg += 10)
            {
                float h = FlybyOrbit.HeightForElevation(26f, deg);
                Assert.That(h, Is.GreaterThan(prev), $"deg {deg}");
                prev = h;
            }
        }

        [Test]
        public void Advance_AddsSpeedTimesDt()
        {
            float a = FlybyOrbit.Advance(0f, 1f, 0.5f);
            Assert.That(a, Is.EqualTo(0.5f).Within(Tol));
        }

        [Test]
        public void Advance_WrapsPastTwoPi()
        {
            float a = FlybyOrbit.Advance(TwoPi - 0.1f, 1f, 0.3f); // 0.2 past 2π → wraps to ~0.2
            Assert.That(a, Is.EqualTo(0.2f).Within(1e-3f));
            Assert.That(a, Is.GreaterThanOrEqualTo(0f).And.LessThan(TwoPi));
        }

        [Test]
        public void Advance_ZeroSpeed_IsUnchanged()
        {
            Assert.That(FlybyOrbit.Advance(1.234f, 0f, 0.16f), Is.EqualTo(1.234f).Within(Tol));
        }

        [Test]
        public void Advance_NegativeSpeed_StaysNonNegative()
        {
            float a = FlybyOrbit.Advance(0.05f, -1f, 0.2f); // 0.05 - 0.2 = -0.15 → wraps to ~2π-0.15
            Assert.That(a, Is.GreaterThanOrEqualTo(0f).And.LessThan(TwoPi));
        }
    }
}
