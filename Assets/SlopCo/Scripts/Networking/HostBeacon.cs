using System;
using System.Net;
using System.Net.Sockets;
using Unity.Netcode;
using UnityEngine;
using SlopCo.Core;

namespace SlopCo.Networking
{
    /// <summary>
    /// While this peer is the NGO server and the lobby is not full, periodically broadcasts a UDP beacon so
    /// LAN quick-matchers (<see cref="LanMatchmaker"/>) can discover and auto-join it — no join code needed.
    /// Works for BOTH manual hosting and quick-match hosting (it keys off OnServerStarted, not the match flow).
    /// Place on the NetworkManager scene object.
    ///
    /// NOTE: advertises while server + not full. Phase-gating (stop advertising once a round is in progress)
    /// can be added when ConnectionApproval also rejects mid-round joins (its existing TODO).
    /// </summary>
    public sealed class HostBeacon : MonoBehaviour
    {
        private UdpClient _sender;
        private bool _serving;
        private float _timer;

        private void Start()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) { Debug.LogWarning("[HostBeacon] No NetworkManager; beacon disabled."); return; }
            nm.OnServerStarted += HandleServerStarted;
            nm.OnServerStopped += HandleServerStopped;
        }

        private void OnDestroy()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null)
            {
                nm.OnServerStarted -= HandleServerStarted;
                nm.OnServerStopped -= HandleServerStopped;
            }
            CloseSender();
        }

        private void HandleServerStarted()
        {
            try
            {
                _sender = new UdpClient { EnableBroadcast = true };
                _serving = true;
                _timer = GameConstants.BeaconIntervalSeconds; // send one immediately
            }
            catch (SocketException e)
            {
                Debug.LogWarning($"[HostBeacon] Could not open broadcast socket: {e.Message}. Discovery beacon disabled.");
                CloseSender();
                _serving = false;
            }
        }

        private void HandleServerStopped(bool _)
        {
            _serving = false;
            CloseSender();
        }

        private void Update()
        {
            if (!_serving || _sender == null) return;
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return;

            int players = nm.ConnectedClientsIds.Count;
            if (players >= GameConstants.MaxPlayers) return; // don't advertise a full lobby

            _timer += Time.unscaledDeltaTime;
            if (_timer < GameConstants.BeaconIntervalSeconds) return;
            _timer = 0f;

            var beacon = new MatchBeacon(GameConstants.MatchGameId, GameConstants.DefaultPort,
                                         (byte)Mathf.Clamp(players, 0, 255), (byte)GameConstants.MaxPlayers);
            byte[] wire = MatchmakerCodec.Encode(beacon);
            try
            {
                _sender.Send(wire, wire.Length, new IPEndPoint(IPAddress.Broadcast, GameConstants.DiscoveryPort));
            }
            catch (SocketException e)
            {
                Debug.LogWarning($"[HostBeacon] Beacon send failed: {e.Message}");
            }
        }

        private void CloseSender()
        {
            if (_sender == null) return;
            try { _sender.Dispose(); }
            catch (Exception e) { Debug.LogWarning($"[HostBeacon] sender dispose: {e.Message}"); }
            _sender = null;
        }
    }
}
