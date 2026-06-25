using NUnit.Framework;
using UnityEngine;
using SlopCo.Player;

namespace SlopCo.Tests.EditMode
{
    /// <summary>
    /// Pins the knockback falloff (ExplosionShove.Impulse) — pure Vector3 math, same shape as RunGradeTests.
    /// </summary>
    public class ExplosionShoveTests
    {
        [Test]
        public void Impulse_AtCenter_IsMaxSpeed()
        {
            var v = ExplosionShove.Impulse(Vector3.zero, Vector3.zero, radius: 9f, maxSpeed: 14f);
            Assert.AreEqual(14f, v.magnitude, 0.01f);
            Assert.AreEqual(0f, v.y, 1e-5f); // horizontal only
        }

        [Test]
        public void Impulse_AtEdge_IsZero()
        {
            var v = ExplosionShove.Impulse(new Vector3(9f, 0f, 0f), Vector3.zero, radius: 9f, maxSpeed: 14f);
            Assert.AreEqual(0f, v.magnitude, 0.01f);
        }

        [Test]
        public void Impulse_BeyondRadius_IsZero()
        {
            var v = ExplosionShove.Impulse(new Vector3(20f, 0f, 0f), Vector3.zero, radius: 9f, maxSpeed: 14f);
            Assert.AreEqual(Vector3.zero, v);
        }

        [Test]
        public void Impulse_HalfDistance_IsHalfSpeed_AwayDirection()
        {
            // self at +X 4.5 of radius 9 → falloff 0.5, pushed further +X.
            var v = ExplosionShove.Impulse(new Vector3(4.5f, 0f, 0f), Vector3.zero, radius: 9f, maxSpeed: 14f);
            Assert.AreEqual(7f, v.magnitude, 0.01f);
            Assert.Greater(v.x, 0f);
            Assert.AreEqual(0f, v.z, 1e-5f);
        }

        [Test]
        public void Impulse_NonPositiveRadius_IsZero()
        {
            var v = ExplosionShove.Impulse(new Vector3(1f, 0f, 0f), Vector3.zero, radius: 0f, maxSpeed: 14f);
            Assert.AreEqual(Vector3.zero, v);
        }

        [Test]
        public void Impulse_IgnoresVerticalSeparation()
        {
            // A blast directly below should still push horizontally only (y separation zeroed).
            var v = ExplosionShove.Impulse(new Vector3(3f, 5f, 0f), Vector3.zero, radius: 9f, maxSpeed: 14f);
            Assert.AreEqual(0f, v.y, 1e-5f);
            Assert.Greater(v.x, 0f);
        }
    }
}
