namespace SlopCo.Gameplay
{
    /// <summary>
    /// Pure "may the thief take this?" policy — NO UnityEngine dependency, so it is EditMode-testable like
    /// <see cref="DailyModifier"/>. Kept out of <see cref="ThiefHazard"/> so the one rule that decides whether
    /// a piece of cargo is fair game has tests instead of living inside an Update loop.
    /// </summary>
    public static class ThiefRules
    {
        /// <summary>Cargo is stealable once it has slipped out of someone's hands and come to rest on the floor:
        /// nobody is holding it, it isn't already delivered, SOMEONE carried it at least once (so untouched depot
        /// cargo is safe), and it has been moving slower than <paramref name="restSpeed"/> for at least
        /// <paramref name="settleSeconds"/> — that delay is what makes it "landed" rather than "mid-bounce".</summary>
        public static bool IsStealable(bool held, bool delivered, ulong lastCarrier,
                                       float speed, float restingFor,
                                       float restSpeed, float settleSeconds)
        {
            if (held || delivered) return false;
            if (lastCarrier == 0UL) return false;      // never picked up — the thief only takes dropped goods
            if (speed > restSpeed) return false;
            return restingFor >= settleSeconds;
        }

        /// <summary>How long cargo has now been at rest, given the previous streak and this frame's speed.
        /// Any burst of movement resets the streak to zero.</summary>
        public static float AdvanceRest(float restingFor, float speed, float restSpeed, float dt)
        {
            if (speed > restSpeed) return 0f;
            if (dt < 0f) dt = 0f;
            return restingFor + dt;
        }
    }
}
