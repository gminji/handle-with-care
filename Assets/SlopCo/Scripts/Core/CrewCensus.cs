using UnityEngine;   // solely for [RuntimeInitializeOnLoadMethod] — see the class summary below

namespace SlopCo.Core
{
    /// <summary>
    /// Live headcount of carry-capable player bodies (humans AND server-owned AI bots). NGO's
    /// ConnectedClientsIds.Count is NOT usable for this: bots share clientId 0 with the host
    /// (the same trap CargoItem.cs warns about with carrierId), so a host + 2 bots would report 1.
    /// Maintained by PlayerCarryController's spawn/despawn, i.e. it tracks mid-run joins and leaves
    /// with zero extra netcode. Lives in Core so Cargo never has to reference Player.
    /// MUST be reset at every session boundary: a shipped build has no domain reload, so a
    /// menu -> run -> menu cycle would otherwise leak the count upward forever. Three layers of
    /// defense: UIManager.OnBackToMenu (cleans up the PREVIOUS session's leftovers), UIManager's
    /// NetworkManager.OnServerStopped/OnClientStopped subscription (the real end-of-session reset,
    /// fires once every despawn for THIS session has already run), and ResetOnLoad below (safety net
    /// in case domain reload is ever disabled in the editor).
    /// </summary>
    public static class CrewCensus
    {
        public static int Count { get; private set; }

        /// <summary>Called once from PlayerCarryController.OnNetworkSpawn (humans AND bots).</summary>
        public static void Register() => Count++;

        /// <summary>Called once from PlayerCarryController.OnNetworkDespawn. Clamped at 0 so a stray
        /// extra Unregister (e.g. a Reset() racing a late despawn) can never go negative.</summary>
        public static void Unregister()
        {
            if (Count > 0) Count--;
        }

        /// <summary>Zero the census at a session boundary (menu return, server/client stop).</summary>
        public static void Reset() => Count = 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad() => Reset();
    }
}
