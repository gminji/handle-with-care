using Unity.Netcode;
using UnityEngine;
using SlopCo.Core;
using SlopCo.Gameplay;

namespace SlopCo.Networking
{
    /// <summary>
    /// NGO 2.x connection approval. Rejects connections once the lobby is full or while a haul is in progress
    /// (<see cref="JoinPolicy"/>); the refusal reason is a localization key the client shows in the lobby. Sets
    /// CreatePlayerObject = false so PlayerSpawner is the single owner of player spawning
    /// (prevents double-spawn). Place on the same scene object family as the NetworkManager.
    /// </summary>
    public sealed class ConnectionApprovalHandler : MonoBehaviour
    {
        private void Start()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null)
            {
                Debug.LogError("[ConnectionApproval] No NetworkManager in scene.");
                return;
            }

            nm.NetworkConfig.ConnectionApproval = true;
            nm.ConnectionApprovalCallback = Approve;
        }

        private void Approve(NetworkManager.ConnectionApprovalRequest request,
                             NetworkManager.ConnectionApprovalResponse response)
        {
            var nm = NetworkManager.Singleton;
            int connected = nm != null ? nm.ConnectedClientsIds.Count : 0;
            var rm = ServiceLocator.Get<RoundManager>();
            RoundPhase phase = rm != null ? rm.Phase.Value : RoundPhase.Lobby;   // no round yet = still the lobby
            bool hostSelf = request.ClientNetworkId == NetworkManager.ServerClientId;

            string reject = JoinPolicy.RejectReason(hostSelf, connected, GameConstants.MaxPlayers, phase);
            response.Approved = reject == null;
            if (reject != null) response.Reason = reject;
            response.CreatePlayerObject = false; // PlayerSpawner spawns players explicitly.
            response.Pending = false;
        }
    }
}
