namespace SlopCo.Player
{
    /// <summary>
    /// Pure dash-stamina state machine (NO UnityEngine dependency → EditMode-testable, mirrors
    /// FlybyOrbit / VoiceActivity). Hold-to-sprint drains the gauge; draining to empty forces a brief
    /// "exhausted" stop; releasing/idling regenerates. <see cref="PlayerController"/> owns one instance.
    /// </summary>
    public struct DashState
    {
        public float gauge;      // 0..1 (1 = full)
        public bool  exhausted;  // true = forced stop, regenerating
        public float exhaustT;   // seconds left in exhaustion
        public bool  dashing;    // true = actively sprinting this step
    }

    public static class DashStamina
    {
        public static DashState Initial => new DashState { gauge = 1f, exhausted = false, exhaustT = 0f, dashing = false };

        /// <summary>Advance one frame. dt seconds; dashHeld = sprint button; moving = has locomotion input.</summary>
        public static DashState Step(DashState s, float dt, bool dashHeld, bool moving,
                                     float drainPerSec, float regenPerSec, float exhaustSeconds)
        {
            if (dt < 0f) dt = 0f;

            if (s.exhausted)
            {
                s.dashing = false;
                s.gauge = Clamp01(s.gauge + regenPerSec * dt);   // recover while stunned
                s.exhaustT -= dt;
                if (s.exhaustT <= 0f) { s.exhausted = false; s.exhaustT = 0f; }
                return s;
            }

            if (dashHeld && moving && s.gauge > 0f)
            {
                s.dashing = true;
                s.gauge -= drainPerSec * dt;
                if (s.gauge <= 0f)
                {
                    s.gauge = 0f;
                    s.exhausted = true;
                    s.exhaustT = exhaustSeconds;
                    s.dashing = false;
                }
            }
            else
            {
                s.dashing = false;
                s.gauge = Clamp01(s.gauge + regenPerSec * dt);
            }
            return s;
        }

        /// <summary>Locomotion multiplier: 0 while exhausted (stop), dashMult while dashing, else 1.</summary>
        public static float SpeedMult(DashState s, float dashMult)
            => s.exhausted ? 0f : (s.dashing ? dashMult : 1f);

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
