using SlopCo.Gameplay;

namespace SlopCo.Networking
{
    /// <summary>
    /// Who may join a running session, in one place: <see cref="ConnectionApprovalHandler"/> refuses with it and
    /// <see cref="HostBeacon"/> stops advertising with it, so LAN quick match never walks into a refusal.
    /// A haul is the one phase a newcomer can't drop into — the bombs and their crew-scaled loads were sized for
    /// the crew that started it. Briefing and payout are open, so a late or reconnecting friend joins the shop
    /// and hauls from the next day. Pure (no NetworkManager), EditMode-testable.
    /// </summary>
    public static class JoinPolicy
    {
        /// <summary>Localization keys sent as the NGO disconnect reason; the refused client shows them.</summary>
        public const string ReasonFull = "join.reject.full";
        public const string ReasonHauling = "join.reject.hauling";

        /// <summary>The localization key explaining why this join is refused, or null to let it in. The host's own
        /// local client is never refused — that would fail StartHost itself.</summary>
        public static string RejectReason(bool isHostSelf, int connected, int maxPlayers, RoundPhase phase)
        {
            if (isHostSelf) return null;
            if (connected >= maxPlayers) return ReasonFull;
            if (phase == RoundPhase.Hauling) return ReasonHauling;
            return null;
        }

        /// <summary>Should the LAN beacon advertise this session right now? Only when a newcomer would be let in.</summary>
        public static bool ShouldAdvertise(int connected, int maxPlayers, RoundPhase phase) =>
            RejectReason(false, connected, maxPlayers, phase) == null;

        /// <summary>Is this disconnect reason one of our refusals? NGO's DisconnectReason falls back to its own
        /// diagnostic text ("[Disconnect Event][Client-1]…") when the server sent none — e.g. the host quit — and
        /// that must never reach the lobby screen.</summary>
        public static bool IsRefusal(string reason) => reason == ReasonFull || reason == ReasonHauling;
    }
}
