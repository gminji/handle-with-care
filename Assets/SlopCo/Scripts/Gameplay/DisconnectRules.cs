namespace SlopCo.Gameplay
{
    /// <summary>
    /// Which disconnects stop a run for a crew vote (<see cref="DisconnectVote"/>). Pure (no NetworkManager),
    /// so it is EditMode-testable without starting a session.
    /// </summary>
    public static class DisconnectRules
    {
        /// <summary>Is this disconnect a crew member walking out of a run in progress — the only kind worth
        /// stopping for? Not a peer that never got in (the server also hears a "disconnect" for a connection it
        /// refused at approval), not the host (its leaving tears the session down anyway), and not while nothing
        /// is in progress (lobby, game over).</summary>
        public static bool IsDeparture(bool wasInSession, bool isHost, RoundPhase phase) =>
            wasInSession && !isHost && phase != RoundPhase.Lobby && phase != RoundPhase.GameOver;
    }
}
