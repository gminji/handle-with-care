using System;
using System.Threading.Tasks;

namespace SlopCo.Networking
{
    /// <summary>
    /// Decouples lobby UI / gameplay from the concrete transport (local direct-IP vs Steam lobbies).
    /// Async so a Steam implementation can await lobby creation; the local impl completes synchronously.
    /// </summary>
    public interface INetworkSession
    {
        /// <summary>Start as host (server + client). Returns true on success.</summary>
        Task<bool> HostAsync(int maxPlayers);

        /// <summary>Join an existing session by code (IP for local, lobby id for Steam).</summary>
        Task<bool> JoinAsync(string code);

        /// <summary>Shut down the session and return to lobby.</summary>
        void Leave();

        /// <summary>Human-shareable code for the current session (IP or lobby id).</summary>
        string LobbyDisplayCode { get; }

        bool IsActive { get; }

        event Action OnStarted;          // host started
        event Action OnJoined;           // client joined
        event Action OnLeft;             // session ended locally
        event Action<string> OnFailed;   // arg = reason
    }
}
