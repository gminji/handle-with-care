using SlopCo.Gameplay;

namespace SlopCo.Audio
{
    /// <summary>
    /// Pure phase→music policy: which loop should be playing right now. No UnityEngine dependency (only the
    /// <see cref="RoundPhase"/> enum), so it is EditMode-testable like <c>RunGrade</c> / <c>ShareText</c>.
    /// The only "rush" phase is Hauling — everything else (menu/briefing/payout/fired) rides the calm loop.
    /// </summary>
    public static class MusicBus
    {
        /// <summary>Target weight of the CALM loop for a phase: 1 = full calm, 0 = full haul.
        /// <see cref="GameAudio"/> cross-fades the two music sources toward this each frame.</summary>
        public static float CalmWeight(RoundPhase phase) => phase == RoundPhase.Hauling ? 0f : 1f;
    }
}
