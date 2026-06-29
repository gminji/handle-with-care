using System;
using System.Threading.Tasks;

namespace SlopCo.Networking
{
    /// <summary>Result of a quick-match attempt.</summary>
    public enum MatchOutcome { JoinedExisting, BecameHost, Cancelled, Failed }

    public readonly struct MatchResult
    {
        public readonly MatchOutcome Outcome;
        public readonly string Detail;
        public MatchResult(MatchOutcome outcome, string detail) { Outcome = outcome; Detail = detail; }
    }

    /// <summary>
    /// Beacon payload a host broadcasts so others can discover it. The host's address is NOT in the payload
    /// (it is taken from the UDP sender endpoint, which can't be spoofed by a stale/NAT'd field).
    /// </summary>
    public readonly struct MatchBeacon
    {
        public readonly string GameId;
        public readonly ushort Port;
        public readonly byte Players;
        public readonly byte MaxPlayers;
        public MatchBeacon(string gameId, ushort port, byte players, byte maxPlayers)
        { GameId = gameId; Port = port; Players = players; MaxPlayers = maxPlayers; }

        public bool IsJoinable => Players < MaxPlayers;
    }

    /// <summary>A discovered host: its address (from the UDP endpoint) plus the decoded beacon.</summary>
    public readonly struct MatchCandidate
    {
        public readonly string Address;
        public readonly MatchBeacon Beacon;
        public MatchCandidate(string address, in MatchBeacon beacon) { Address = address; Beacon = beacon; }
    }

    /// <summary>
    /// Automatic matchmaking seam: pair online players without exchanging a code. The LAN implementation
    /// (<see cref="LanMatchmaker"/>) is the free, build-now default; a cloud (UGS) or Steam implementation can
    /// replace it behind this interface later. Sits above <see cref="INetworkSession"/> (transport-agnostic).
    /// </summary>
    public interface IMatchmaker
    {
        /// <summary>Find a joinable session and join it, or become a host if none is found.</summary>
        Task<MatchResult> QuickMatchAsync(int maxPlayers);

        /// <summary>Abort an in-flight quick match.</summary>
        void Cancel();

        bool IsMatching { get; }

        /// <summary>Human-readable status for the lobby UI (searching / joining / hosting…).</summary>
        event Action<string> OnStatus;
    }
}
