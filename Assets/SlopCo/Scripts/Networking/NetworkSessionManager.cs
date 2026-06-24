using UnityEngine;
using SlopCo.Core;

namespace SlopCo.Networking
{
    /// <summary>
    /// Scene-level facade over the chosen <see cref="INetworkSession"/>. Lobby UI calls Host/Join/Leave
    /// here without knowing whether Steam or local transport is active. Registered in ServiceLocator.
    /// </summary>
    public sealed class NetworkSessionManager : MonoBehaviour
    {
        public INetworkSession Session { get; private set; }

        private void Awake()
        {
#if USE_STEAM
            Session = new SteamNetworkSession();
#else
            Session = new LocalNetworkSession();
#endif
            ServiceLocator.Register(this);
        }

        private void OnDestroy()
        {
            if (ServiceLocator.Get<NetworkSessionManager>() == this)
                ServiceLocator.Unregister<NetworkSessionManager>();
        }

        /// <summary>Fire-and-forget host (UI button). Result surfaces via Session events.</summary>
        public async void HostGame() => await Session.HostAsync(GameConstants.MaxPlayers);

        /// <summary>Fire-and-forget join (UI button).</summary>
        public async void JoinGame(string code) => await Session.JoinAsync(code);

        public void Leave() => Session.Leave();
    }
}
