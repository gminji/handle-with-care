using UnityEngine;

namespace SlopCo.UI
{
    /// <summary>
    /// Pure radial-wheel selection math (no MonoBehaviour state) — unit-tested in EditMode like
    /// <see cref="SlopCo.Audio.VoiceActivity"/> / <see cref="SlopCo.Gameplay.FlybyOrbit"/>. Slice 0 is centered
    /// at 12 o'clock and slices advance clockwise. Screen-space convention: +y is up.
    /// </summary>
    public static class WheelMath
    {
        /// <summary>
        /// Slice index in [0,count) selected by a pointer offset, or -1 if inside the dead-zone (cancel).
        /// <paramref name="dir"/> is the pointer offset from the wheel center NORMALIZED to wheel-radius units
        /// (mouse: (pos-center)/radiusPx; gamepad: raw stick vector); <paramref name="deadzone"/> is compared
        /// against that normalized magnitude.
        /// </summary>
        public static int SelectSlice(Vector2 dir, int count, float deadzone)
        {
            if (count <= 0) return -1;
            if (dir.magnitude < deadzone) return -1;                 // dead-zone → cancel
            float ang = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;   // 0° at +y (up), increasing clockwise
            if (ang < 0f) ang += 360f;
            float arc = 360f / count;
            return Mathf.RoundToInt(ang / arc) % count;              // nearest slice (0 at 12 o'clock)
        }

        /// <summary>Screen-space unit direction of slice <paramref name="i"/>'s center (for label placement).</summary>
        public static Vector2 SliceCenterDir(int i, int count)
        {
            if (count <= 0) return Vector2.up;
            float ang = (360f / count) * i * Mathf.Deg2Rad;          // clockwise from up
            return new Vector2(Mathf.Sin(ang), Mathf.Cos(ang));
        }
    }
}
