using System;

namespace SlopCo.Audio
{
    /// <summary>
    /// Pure presentation math for the Voice Activity Indicator — NO UnityEngine / NGO dependency, so it is
    /// unit-testable in EditMode (mirrors <see cref="VoiceMath"/> / CargoMath / QuotaMath, the project's
    /// "pure math + EditMode test" convention). Uses <see cref="System.MathF"/> instead of UnityEngine.Mathf
    /// to stay engine-free.
    ///
    /// Inputs come from <see cref="PlayerVoice"/>: the timestamp of the last voiced frame (sent locally or
    /// received from a remote) and the current time. From those it derives whether to show the indicator,
    /// how strongly (for fade-out), and a living "speech pulse" scale.
    /// </summary>
    public static class VoiceActivity
    {
        /// <summary>
        /// True while the player counts as speaking: the last voiced frame is within <paramref name="window"/>
        /// seconds (debounce absorbs the brief silences between syllables). A non-positive
        /// <paramref name="lastVoicedTime"/> means "never spoke" → false.
        /// </summary>
        public static bool IsActive(float lastVoicedTime, float now, float window)
        {
            if (lastVoicedTime <= 0f) return false;
            return (now - lastVoicedTime) <= window;
        }

        /// <summary>
        /// Display intensity in [0,1]: full (1) the instant a voiced frame lands, decaying linearly to 0 at
        /// the tail of <paramref name="window"/> so the indicator fades out instead of popping off. "Never
        /// spoke" (<paramref name="lastVoicedTime"/> &lt;= 0) → 0.
        /// </summary>
        public static float Intensity(float lastVoicedTime, float now, float window)
        {
            if (lastVoicedTime <= 0f) return 0f;
            float t = now - lastVoicedTime;
            if (t <= 0f) return 1f;
            if (t >= window) return 0f;
            return 1f - (t / window);
        }

        /// <summary>
        /// Billboard scale that pulses around <paramref name="baseScale"/> while speaking:
        /// <c>baseScale * (1 + amp * intensity * sin(now * speed))</c>. At intensity 0 it returns exactly
        /// <paramref name="baseScale"/> (no pulse); at intensity 1 it stays within
        /// [baseScale*(1-amp), baseScale*(1+amp)]. Deterministic in <paramref name="now"/>.
        /// </summary>
        public static float PulseScale(float now, float intensity, float baseScale, float amp, float speed)
        {
            return baseScale * (1f + amp * intensity * MathF.Sin(now * speed));
        }
    }
}
