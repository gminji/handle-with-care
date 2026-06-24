using Unity.Netcode;
using UnityEngine;
using SlopCo.Core;

namespace SlopCo.Networking
{
    /// <summary>
    /// NGO 2.x connection approval. Rejects connections once the lobby is full. Sets
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

            if (connected >= GameConstants.MaxPlayers)
            {
                response.Approved = false;
                response.CreatePlayerObject = false;
                response.Reason = "Lobby is full.";
                response.Pending = false;
                return;
            }

            // TODO (phase 2): also reject mid-round joins (read RoundManager phase) for cleaner rounds.
            response.Approved = true;
            response.CreatePlayerObject = false; // PlayerSpawner spawns players explicitly.
            response.Pending = false;
        }
    }
}
