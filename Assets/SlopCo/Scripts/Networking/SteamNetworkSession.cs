using System;
using System.Threading.Tasks;
using UnityEngine;

namespace SlopCo.Networking
{
    /// <summary>
    /// Steam-backed session: Steam lobbies for friend invites + SteamNetworkingSockets transport
    /// (free relay/NAT punch, no CCU cost). Selected by NetworkSessionManager only when the
    /// <c>USE_STEAM</c> scripting define is set. Compiles in both modes (no direct Facepunch type
    /// references here) so the project always builds; the actual SDK wiring is the dev's next step.
    ///
    /// IMPLEMENTATION GUIDE (do this when enabling Steam):
    ///   1. Add Facepunch.Steamworks DLLs to Assets/SlopCo/ThirdParty/ and a steam_appid.txt (480 to test).
    ///   2. Initialize: SteamClient.Init(yourAppId).
    ///   3. Host: var lobby = await SteamMatchmaking.CreateLobbyAsync(GameConstants.MaxPlayers);
    ///            lobby.Value.SetPublic()/SetFriendsOnly(); set a "host SteamId" lobby data key;
    ///            swap NetworkManager transport to a Facepunch SteamNetworkingSockets transport, then StartHost().
    ///   4. Join: subscribe SteamMatchmaking.OnGameLobbyJoinRequested (overlay "Invite Friend"/join-from-friends);
    ///            on join, read host SteamId from lobby data, set transport target, StartClient().
    ///   5. Expose lobby.Id as LobbyDisplayCode.
    /// </summary>
    public sealed class SteamNetworkSession : INetworkSession
    {
        public string LobbyDisplayCode { get; private set; } = string.Empty;
        public bool IsActive { get; private set; }

        public event Action OnStarted;
        public event Action OnJoined;
        public event Action OnLeft;
        public event Action<string> OnFailed;

        public Task<bool> HostAsync(int maxPlayers)
        {
#if USE_STEAM
            // TODO: real Facepunch wiring (see class summary). Until implemented, fail loudly so callers
            // can fall back to LocalNetworkSession rather than silently doing nothing.
            OnFailed?.Invoke("SteamNetworkSession.HostAsync: wiring TODO — implement Facepunch lobby + transport.");
            Debug.LogWarning("[SteamNetworkSession] USE_STEAM is defined but lobby wiring is not implemented yet.");
            return Task.FromResult(false);
#else
            OnFailed?.Invoke("Steam disabled (USE_STEAM undefined). Use LocalNetworkSession.");
            return Task.FromResult(false);
#endif
        }

        public Task<bool> JoinAsync(string code)
        {
#if USE_STEAM
            OnFailed?.Invoke("SteamNetworkSession.JoinAsync: wiring TODO — implement Facepunch lobby join.");
            return Task.FromResult(false);
#else
            OnFailed?.Invoke("Steam disabled (USE_STEAM undefined). Use LocalNetworkSession.");
            return Task.FromResult(false);
#endif
        }

        public void Leave()
        {
            IsActive = false;
            OnLeft?.Invoke();
        }
    }
}
