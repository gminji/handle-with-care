namespace SlopCo.Gameplay
{
    /// <summary>Where an abduction is in its arc.</summary>
    public enum AbductPhase : byte { Lift = 0, Hold = 1, Done = 2 }

    /// <summary>
    /// Pure UFO-abduction timing and landing cost — NO UnityEngine dependency, so it is EditMode-testable
    /// like <see cref="DailyModifier"/> / <c>DashStamina</c>. The victim's owner drives the actual motion
    /// (a CharacterController can only be moved by its owner); this decides the phase and what the fall costs.
    /// </summary>
    public static class AbductionMath
    {
        /// <summary>Phase at <paramref name="elapsed"/> seconds into an abduction.</summary>
        public static AbductPhase Phase(float elapsed, float lift, float hold)
        {
            if (lift < 0f) lift = 0f;
            if (hold < 0f) hold = 0f;
            if (elapsed < lift) return AbductPhase.Lift;
            if (elapsed < lift + hold) return AbductPhase.Hold;
            return AbductPhase.Done;
        }

        /// <summary>Beam progress 0..1 during the lift, smoothstep-eased so the victim peels off the ground
        /// slowly and then snaps up — the readable "tractor beam" shape. Clamped outside the lift window.</summary>
        public static float LiftT(float elapsed, float lift)
        {
            if (lift <= 0f) return 1f;
            float t = elapsed / lift;
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            return t * t * (3f - 2f * t);
        }

        /// <summary>Stamina (0..1 gauge units) burned by hitting the ground from <paramref name="fallHeight"/>
        /// metres. Short drops are free (<paramref name="freeHeight"/>) so ordinary jumping never costs
        /// anything; beyond that it scales linearly and saturates at <paramref name="max"/>.</summary>
        public static float StaminaPenalty(float fallHeight, float freeHeight, float perMetre, float max)
        {
            float over = fallHeight - freeHeight;
            if (over <= 0f || perMetre <= 0f) return 0f;
            float cost = over * perMetre;
            return cost > max ? max : cost;
        }
    }
}
