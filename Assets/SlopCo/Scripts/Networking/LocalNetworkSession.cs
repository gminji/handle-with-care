using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using SlopCo.Core;

namespace SlopCo.Networking
{
    /// <summary>
    /// Default session: NGO over UnityTransport on a direct IP (loopback by default). Requires NO Steam
    /// SDK, so the project always builds and is playable in two editors / LAN. This is the path the
    /// vertical slice runs on.
    /// </summary>
    public sealed class LocalNetworkSession : INetworkSession
    {
        public string LobbyDisplayCode { get; private set; } = GameConstants.DefaultJoinCode;
        public bool IsActive => NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;

        public event Action OnStarted;
        public event Action OnJoined;
        public event Action OnLeft;
        public event Action<string> OnFailed;

        public Task<bool> HostAsync(int maxPlayers)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) { OnFailed?.Invoke("No NetworkManager in scene."); return Task.FromResult(false); }

            ConfigureTransport(GameConstants.DefaultJoinCode);
            bool ok = nm.StartHost();
            if (ok) { LobbyDisplayCode = GameConstants.DefaultJoinCode; OnStarted?.Invoke(); }
            else OnFailed?.Invoke("StartHost failed.");
            return Task.FromResult(ok);
        }

        public Task<bool> JoinAsync(string code)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) { OnFailed?.Invoke("No NetworkManager in scene."); return Task.FromResult(false); }

            string ip = string.IsNullOrWhiteSpace(code) ? GameConstants.DefaultJoinCode : code.Trim();
            ConfigureTransport(ip);
            bool ok = nm.StartClient();
            if (ok) { LobbyDisplayCode = ip; OnJoined?.Invoke(); }
            else OnFailed?.Invoke("StartClient failed.");
            return Task.FromResult(ok);
        }

        public void Leave()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening) nm.Shutdown();
            OnLeft?.Invoke();
        }

        private static void ConfigureTransport(string ip)
        {
            var nm = NetworkManager.Singleton;
            var utp = nm.GetComponent<UnityTransport>();
            if (utp != null) utp.SetConnectionData(ip, GameConstants.DefaultPort);
            else Debug.LogWarning("[LocalNetworkSession] UnityTransport not found on NetworkManager; using its configured defaults.");
        }
    }
}
