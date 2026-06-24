using Unity.Netcode;
using UnityEngine;

namespace SlopCo.Core
{
    /// <summary>
    /// Lightweight app entry point. Persists across scene loads, handles the host-leaves MVP case
    /// (no host migration: clients shut down and return to lobby), and clears the ServiceLocator on
    /// teardown so a new session never reads stale references.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        private void Awake() => DontDestroyOnLoad(gameObject);

        private void Start()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null) nm.OnClientDisconnectCallback += HandleDisconnect;
        }

        private void OnDestroy()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null) nm.OnClientDisconnectCallback -= HandleDisconnect;
            ServiceLocator.Clear();
        }

        private void HandleDisconnect(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) return;

            // MVP: if we are a client and the host (server) dropped, the session is over.
            if (!nm.IsServer && clientId == NetworkManager.ServerClientId)
            {
                Debug.Log("[GameBootstrap] Host left — ending session, returning to lobby.");
                if (nm.IsListening) nm.Shutdown();
            }
        }
    }
}
