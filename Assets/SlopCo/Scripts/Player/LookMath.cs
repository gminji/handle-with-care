using UnityEngine;

namespace SlopCo.Player
{
    /// <summary>
    /// Immutable first-person view state: yaw (body-relevant, wrapped [0,360)) and pitch
    /// (camera-only, clamped [-85,+85]). Positive pitch looks up.
    /// </summary>
    public struct LookState
    {
        public float Yaw;
        public float Pitch;
    }

    /// <summary>
    /// Pure yaw/pitch accumulation, view-relative movement, and viewpoint-transition seeding for
    /// first-person mouse look. No UnityEngine runtime/scene dependency beyond Vector2/Vector3/Mathf
    /// (EditMode-testable, mirrors <see cref="CameraRig"/> and <see cref="DashStamina"/>). This class
    /// and <see cref="CameraRig"/> are siblings — neither references the other; their shared pitch
    /// range is pinned to agree only inside the test assembly, which references both.
    /// </summary>
    public static class LookMath
    {
        // Degrees of yaw/pitch per pixel of raw mouse delta, at sensitivity 1.0. Mouse.delta is an
        // OS-scaled pointer movement, not a raw DPI count, so this is an empirical baseline, not a
        // derived constant.
        public const float DegreesPerMousePixel = 0.12f;
        // Angular rate of the gamepad right stick at full deflection, at sensitivity 1.0.
        public const float StickDegreesPerSecond = 180f;
        // Gimbal/flip guard — mirrors CameraRig.PitchMin/PitchMax by design (siblings, pinned in tests).
        public const float PitchMin = -85f;
        public const float PitchMax = 85f;

        /// <summary>Zeroed look state. Analogous to <c>DashStamina.Initial</c>.</summary>
        public static LookState Level => new LookState { Yaw = 0f, Pitch = 0f };

        /// <summary>
        /// Advance one frame of look input. <paramref name="mouseDelta"/> is an already-accumulated
        /// per-frame pixel delta and must NOT be scaled by dt. <paramref name="stick"/> is an angular
        /// rate (-1..1 axis position) and IS scaled by <paramref name="unscaledDt"/> — callers must pass
        /// <c>Time.unscaledDeltaTime</c> so gamepad look speed does not sag during hitstop
        /// (<c>Time.timeScale</c> dips). <paramref name="sensitivity"/> is assumed already clamped by the
        /// caller (see <c>SettingsManager.ClampLookSensitivity</c>).
        /// </summary>
        public static LookState Step(LookState s, Vector2 mouseDelta, Vector2 stick, float unscaledDt,
                                      float sensitivity, bool invertY)
        {
            float mouseYaw = mouseDelta.x * sensitivity * DegreesPerMousePixel;
            float mousePitch = mouseDelta.y * sensitivity * DegreesPerMousePixel;

            float stickYaw = stick.x * sensitivity * StickDegreesPerSecond * unscaledDt;
            float stickPitch = stick.y * sensitivity * StickDegreesPerSecond * unscaledDt;

            float yaw = s.Yaw + mouseYaw + stickYaw;
            // Unity's <Mouse>/delta.y is positive when the mouse moves up, and positive Pitch means
            // "looking up" (see LookState). So the default (invertY:false) case is a plain "+=" — the
            // common FPS-example "-=" accumulates Euler X (positive = down), the opposite convention.
            float pitchDelta = (mousePitch + stickPitch) * (invertY ? -1f : 1f);
            float pitch = s.Pitch + pitchDelta;

            // Defence in depth: reject a non-finite frame outright instead of folding it into the state.
            // Mathf.Clamp cannot sanitise NaN (every comparison against NaN is false, so it returns NaN
            // unchanged), and once NaN lands in Yaw it is self-sustaining — 0 * NaN is NaN, so even a
            // perfectly still mouse keeps reproducing it. From there it reaches Quaternion.Euler on an
            // owner-authoritative transform and ClientNetworkTransform replicates the corrupt pose to
            // every peer. Dropping the frame keeps the last good state and costs nothing.
            if (float.IsNaN(yaw) || float.IsInfinity(yaw) || float.IsNaN(pitch) || float.IsInfinity(pitch))
                return s;

            return new LookState
            {
                Yaw = WrapYaw(yaw),
                Pitch = Mathf.Clamp(pitch, PitchMin, PitchMax),
            };
        }

        /// <summary>
        /// Viewpoint-transition seeding. Entering first person (including the very first frame, where
        /// <paramref name="wasFirstPerson"/> is false) reseeds yaw from the character's current facing and
        /// zeroes pitch, so the view never jumps to world +Z on transition. Staying in the same viewpoint
        /// (first-person-to-first-person, or anything third-person/bot) preserves the accumulated state.
        /// </summary>
        public static LookState OnViewpointChanged(LookState s, bool firstPerson, bool wasFirstPerson, Vector3 forward)
            => (firstPerson && !wasFirstPerson) ? FromForward(forward) : s;

        /// <summary>Derives yaw from a horizontal facing direction; pitch is always 0. Degenerate input
        /// (zero / straight up / straight down) falls back to yaw 0.</summary>
        public static LookState FromForward(Vector3 forward)
        {
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-6f) return new LookState { Yaw = 0f, Pitch = 0f };
            float yaw = Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
            return new LookState { Yaw = WrapYaw(yaw), Pitch = 0f };
        }

        /// <summary>Unit look direction for yaw+pitch, matching Unity's Quaternion.Euler(0, yaw, 0) * forward
        /// convention for the horizontal component.</summary>
        public static Vector3 Forward(LookState s)
        {
            float yawRad = s.Yaw * Mathf.Deg2Rad;
            float pitchRad = s.Pitch * Mathf.Deg2Rad;
            float cosPitch = Mathf.Cos(pitchRad);
            return new Vector3(Mathf.Sin(yawRad) * cosPitch, Mathf.Sin(pitchRad), Mathf.Cos(yawRad) * cosPitch);
        }

        /// <summary>Flattens a WASD/arrow/left-stick composite into a yaw-relative world direction. Always
        /// returns y == 0 — pitch must never leak into locomotion (R7). Diagonal input longer than 1 is
        /// normalized so strafing isn't faster than moving straight.</summary>
        public static Vector3 MoveDirection(Vector2 move, float yaw)
        {
            float yawRad = yaw * Mathf.Deg2Rad;
            float s = Mathf.Sin(yawRad);
            float c = Mathf.Cos(yawRad);
            Vector3 forward = new Vector3(s, 0f, c);
            Vector3 right = new Vector3(c, 0f, -s);

            Vector3 dir = forward * move.y + right * move.x;
            dir.y = 0f;
            if (dir.sqrMagnitude > 1f) dir.Normalize();
            return dir;
        }

        /// <summary>Normalizes a yaw angle to [0, 360). Non-finite input yields 0 rather than propagating —
        /// Infinity % 360f is NaN, so the modulo below cannot be trusted to sanitise it.</summary>
        public static float WrapYaw(float yaw)
        {
            if (float.IsNaN(yaw) || float.IsInfinity(yaw)) return 0f;
            yaw %= 360f;
            if (yaw < 0f) yaw += 360f;
            return yaw;
        }
    }
}
