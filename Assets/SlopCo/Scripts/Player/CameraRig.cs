using UnityEngine;

namespace SlopCo.Player
{
    /// <summary>
    /// Pure camera-placement math for the two supported viewpoints. No scene / NetworkManager
    /// dependency (only Vector3), so it is EditMode-testable like <see cref="ExplosionShove"/> —
    /// <see cref="PlayerController"/> only smooths toward whatever this returns.
    ///
    /// THIRD person keeps the established pulled-back 3/4 view (world + horizon readable, cargo
    /// silhouettes visible). FIRST person sits at head height looking down the character's facing —
    /// the player turns by moving, exactly as in third person, so no separate look input is needed.
    /// </summary>
    public static class CameraRig
    {
        // ── Third person (the shipped 3/4 view — do not retune without re-pinning the tests) ──
        public const float ThirdHeight   = 5f;    // camera height above the follow target
        public const float ThirdDistance = 9f;    // how far behind (world -Z, a fixed-angle chase)
        public const float ThirdLookUp   = 2.2f;  // look point above the target's feet

        // ── First person ──
        public const float FirstEyeHeight = 1.65f;  // eye line above the target origin
        public const float FirstEyeAhead  = 0.18f;  // nudge forward so the character's own head/torso is behind the near plane
        public const float FirstLookAhead = 10f;    // look point distance down the facing direction

        // Mouse-look pitch range (gimbal/flip guard). Owned here, not referenced from LookMath — the
        // two pure classes are siblings and must not depend on each other. Their agreement is pinned by
        // CameraRigTests (the test assembly references both, which is the right place for that check).
        public const float PitchMin = -85f;
        public const float PitchMax = 85f;

        /// <summary>Where the camera wants to be for this viewpoint.</summary>
        public static Vector3 DesiredPosition(bool firstPerson, Vector3 target, Vector3 forward)
        {
            if (!firstPerson) return target + new Vector3(0f, ThirdHeight, -ThirdDistance);
            Vector3 f = Flatten(forward);
            return target + Vector3.up * FirstEyeHeight + f * FirstEyeAhead;
        }

        /// <summary>The world point the camera should aim at for this viewpoint, looking level (pitch 0).</summary>
        public static Vector3 DesiredLookAt(bool firstPerson, Vector3 target, Vector3 forward)
            => DesiredLookAt(firstPerson, target, forward, 0f);

        /// <summary>The world point the camera should aim at for this viewpoint, with a first-person pitch
        /// (degrees, positive = up) folded into the gaze direction. Third person ignores
        /// <paramref name="pitchDegrees"/> entirely — its look point is always above the feet. At
        /// pitch 0, Mathf.Cos(0)==1f and Mathf.Sin(0)==0f exactly, so this is bit-for-bit identical to
        /// the legacy (pitch-less) formula.</summary>
        public static Vector3 DesiredLookAt(bool firstPerson, Vector3 target, Vector3 forward, float pitchDegrees)
        {
            if (!firstPerson) return target + Vector3.up * ThirdLookUp;
            Vector3 f = Flatten(forward);
            float p = Mathf.Clamp(pitchDegrees, PitchMin, PitchMax) * Mathf.Deg2Rad;
            Vector3 gaze = f * Mathf.Cos(p) + Vector3.up * Mathf.Sin(p);   // unit length, from the eye point
            return target + Vector3.up * FirstEyeHeight + gaze * FirstLookAhead;
        }

        /// <summary>Horizontal facing, normalized. Degenerate input (straight up/down, zero) falls back
        /// to +Z so the first-person view can never produce a zero-length look vector.</summary>
        public static Vector3 Flatten(Vector3 forward)
        {
            forward.y = 0f;
            return forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
        }
    }
}
