namespace SlopCo.Core
{
    /// <summary>
    /// Pure predicates for whether the mouse cursor should be captured, plus the single static flag
    /// that carries that decision from its one writer (<c>UIManager</c>) to its one reader
    /// (<c>PlayerInputReader</c>). Mirrors the <c>DisconnectVote.GameFrozen</c> shape of "a global
    /// gameplay gate propagated as a static flag". Cursor lock and first-person look input must always
    /// read the same value — that coupling is exactly why this predicate was pulled into its own
    /// EditMode-testable class instead of living inline in <c>UIManager</c> (round-1 review found the
    /// CRITICAL here).
    /// </summary>
    public static class CursorPolicy
    {
        /// <summary>Published by UIManager, read by PlayerInputReader. No other writer.</summary>
        public static bool MouseCaptured;

        /// <summary>Statics outlive scene loads, and only <c>UIManager.OnDisable</c> clears this one — so a
        /// scene with no UIManager would inherit whatever the previous scene left behind (look input stuck
        /// live, or stuck dead with nobody around to re-enable it). Reset at subsystem registration so every
        /// play session starts from a known state regardless of which scene loads first.</summary>
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => MouseCaptured = false;

        /// <summary>Pure truth table for whether the cursor should be locked. First person and in-game
        /// are necessary; any pause/options/controls/help overlay, the results/augment-shop overlay, an
        /// active disconnect vote, or any outstanding free-cursor holder (ping wheel, etc.) all veto it.</summary>
        public static bool WantLock(bool inGame, bool firstPerson,
                                     bool pause, bool options, bool controls, bool help,
                                     bool roundOverlayUp, bool votePaused, int freeCursorHolders)
            => inGame && firstPerson
               && !pause && !options && !controls && !help
               && !roundOverlayUp && !votePaused
               && freeCursorHolders == 0;

        /// <summary>Lock intent + window focus → actual mouse capture. Focus is folded into this pure
        /// layer too, so the whole predicate stays a fixed truth table (no alt-tab special-casing
        /// scattered elsewhere).</summary>
        public static bool CaptureMouse(bool wantLock, bool focused) => wantLock && focused;

        /// <summary>Whether this frame's look delta may be adopted. The frame capture starts (rising
        /// edge, false → true) is discarded — it mixes whatever accumulated while the cursor was free
        /// with the OS pointer warp the lock itself causes.</summary>
        public static bool ShouldReadLook(bool captured, bool wasCaptured) => captured && wasCaptured;
    }
}
