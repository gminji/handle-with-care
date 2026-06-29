using System;

namespace SlopCo.Gameplay
{
    /// <summary>
    /// Pure orbit math for the title-screen map flyby — NO UnityEngine dependency, so it is unit-testable in
    /// EditMode (mirrors VoiceActivity / CargoMath, the project's "pure math + EditMode test" convention).
    /// Uses <see cref="System.MathF"/> instead of UnityEngine.Mathf to stay engine-free. <see cref="MenuFlyby"/>
    /// assembles the Vector3 camera pose from these scalar components.
    /// </summary>
    public static class FlybyOrbit
    {
        private const float DegToRad = MathF.PI / 180f;
        private const float TwoPi = MathF.PI * 2f;

        /// <summary>Horizontal X offset on the orbit (angle 0 → 0, angle 90° → +radius).</summary>
        public static float OffsetX(float radius, float angleRad) => radius * MathF.Sin(angleRad);

        /// <summary>Horizontal Z offset on the orbit (angle 0 → +radius).</summary>
        public static float OffsetZ(float radius, float angleRad) => radius * MathF.Cos(angleRad);

        /// <summary>
        /// Camera height that looks down at <paramref name="elevationDeg"/> from a given horizontal distance.
        /// At 45° the height equals the horizontal distance (tan 45° = 1). Clamped so a negative distance or a
        /// degenerate elevation never produces a negative/NaN height.
        /// </summary>
        public static float HeightForElevation(float horizontalDistance, float elevationDeg)
        {
            if (horizontalDistance <= 0f) return 0f;
            float e = elevationDeg;
            if (e <= 0f) return 0f;
            if (e >= 89.9f) e = 89.9f;          // avoid tan() blow-up near vertical
            return horizontalDistance * MathF.Tan(e * DegToRad);
        }

        /// <summary>Advance the orbit angle by <paramref name="speedRadPerSec"/> over <paramref name="dt"/>
        /// seconds, wrapped into [0, 2π).</summary>
        public static float Advance(float angleRad, float speedRadPerSec, float dt)
        {
            float a = angleRad + speedRadPerSec * dt;
            a %= TwoPi;
            if (a < 0f) a += TwoPi;             // keep the result non-negative even for negative speed
            return a;
        }
    }
}
