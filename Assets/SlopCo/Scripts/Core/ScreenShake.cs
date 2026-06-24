using UnityEngine;

namespace SlopCo.Core
{
    /// <summary>
    /// Trauma-based camera shake. Anything (a smash, a delivery) calls <see cref="Add"/>; the owner's
    /// camera calls <see cref="Sample"/> once per frame and applies the offset. Trauma is squared for a
    /// punchy falloff and decays over time. Pure math, no scene object — keeps the single camera owner
    /// (PlayerController) authoritative over the transform.
    /// </summary>
    public static class ScreenShake
    {
        private static float _trauma;

        public static void Add(float amount) => _trauma = Mathf.Clamp01(_trauma + amount);

        /// <summary>Decay + sample. Returns a local-space camera offset and a roll angle (degrees).</summary>
        public static void Sample(float dt, out Vector2 offset, out float roll)
        {
            float shake = _trauma * _trauma;           // punchy falloff
            float t = Time.time * 28f;
            offset = new Vector2(
                (Mathf.PerlinNoise(t, 0f) - 0.5f) * 2f,
                (Mathf.PerlinNoise(0f, t) - 0.5f) * 2f) * (0.5f * shake);
            roll = (Mathf.PerlinNoise(t, 9f) - 0.5f) * 2f * (7f * shake);
            _trauma = Mathf.Max(0f, _trauma - dt * 1.7f);
        }
    }
}
