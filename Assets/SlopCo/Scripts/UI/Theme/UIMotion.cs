using UnityEngine;

namespace SlopCo.UI
{
    /// <summary>
    /// Motion tokens (design.md §3.6/§4) — duration/rate/amplitude constants plus a small set of
    /// <c>dt</c>-injectable animator cores so EditMode tests can drive every animator deterministically
    /// without touching <see cref="Time"/>. Convenience overloads read <see cref="Time.unscaledDeltaTime"/>:
    /// <c>Juice.cs:39</c> drops <c>Time.timeScale</c> to 0.18 on a big detonation hit-stop, and
    /// <c>GameAudio.cs:84</c> already made the same unscaled choice for the same reason, so UI motion
    /// (fades, punches, banners) must not stall/slow with it. Sibling linger timers
    /// (<c>TutorialHint:36</c>, <c>DailyModifierBanner:55</c>) move to unscaled alongside their fades so a
    /// fade and its own show/hide timer never drift onto different clocks.
    /// </summary>
    public static class UIMotion
    {
        public const float DurInstant = 0.06f;
        public const float DurFast = 0.12f;
        public const float DurBase = 0.20f;
        public const float DurSlow = 0.35f;
        public const float DurBanner = 0.50f;
        public const float DurFlash = 0.18f;
        public const float DurPopup = 1.10f;
        public const float DurRevealPop = 0.45f;
        public const float PunchDecayRate = 3f;
        public const float PulseHzCritical = 12f;
        public const float PulseFlashAmount = 0.6f;

        // Punch amplitudes and selection scales (was: magic numbers in HudController/AugmentShopUI/PingEmoteWheel).
        public const float PunchAmountLarge = 0.35f;   // HudController combo text
        public const float PunchAmountCard = 0.18f;    // AugmentShopUI reveal
        public const float SelectedScale = 1.08f;
        public const float UnselectedScale = 0.94f;
        public const float WheelSelectedScale = 1.25f;
        public const float ShakeRecord = 0.5f;         // ResultsUI new-record / rank-up punch

        // How long a transient banner stays fully opaque before it starts fading.
        // Both were tuned per-file before the token layer; they stay distinct because the tutorial hint
        // carries more words than the modifier banner and needs the extra dwell.
        public const float BannerLingerSeconds = 3.0f;
        public const float HintLingerSeconds = 4.5f;

        /// <summary>Duration-based fade core: <c>MoveTowards(current, target, dt/duration)</c>. This
        /// specific form (not a fixed per-second rate) is what preserves the existing rate↔duration
        /// equivalences already shipping (2f/s ≈ 0.5s, 1.5f/s ≈ 0.667s) even for partial fades that don't
        /// start at 0/1 (design.md §3.6).</summary>
        public static float Fade(float current, float target, float duration, float dt)
            => Mathf.MoveTowards(current, target, dt / duration);

        /// <summary>Convenience overload — unscaled time (see class remarks).</summary>
        public static float Fade(float current, float target, float duration = DurBanner)
            => Fade(current, target, duration, Time.unscaledDeltaTime);

        /// <summary>Null-safe <see cref="CanvasGroup"/> convenience overload — unscaled time.</summary>
        public static void Fade(CanvasGroup group, float target, float duration = DurBanner)
        {
            if (group == null) return;
            group.alpha = Fade(group.alpha, target, duration);
        }

        /// <summary>0..1 pulse, e.g. the fuse-critical flash (<c>PulseHzCritical</c>).</summary>
        public static float Pulse(float time, float hz) => 0.5f + 0.5f * Mathf.Sin(time * hz);

        /// <summary>0→1→0 punch envelope over a single 0..1 progress value.</summary>
        public static float PunchCurve(float t01) => Mathf.Sin(Mathf.Clamp01(t01) * Mathf.PI);

        /// <summary>Scale multiplier built from <see cref="PunchCurve"/> — 1 at rest, <c>1+amount</c> at peak.</summary>
        public static float PunchScale(float t01, float amount) => 1f + PunchCurve(t01) * amount;

        /// <summary>Linear decay toward zero at a fixed rate/sec, floored at 0 — the
        /// <c>HudController._comboPunch</c> idiom (<c>Mathf.Max(0f, v - rate*dt)</c>) promoted to a token.</summary>
        public static float Decay(float v, float rate, float dt) => Mathf.Max(0f, v - rate * dt);
    }
}
