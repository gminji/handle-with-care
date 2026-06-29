using System;
using UnityEngine;
using SlopCo.Core;

namespace SlopCo.Networking
{
    /// <summary>
    /// Scene-level facade over the chosen <see cref="IMatchmaker"/>. Lobby UI calls <see cref="QuickMatch"/>
    /// without knowing the discovery mechanism. Registered in ServiceLocator (mirrors NetworkSessionManager).
    /// Defaults to the free <see cref="LanMatchmaker"/>; a UGS/Steam matchmaker can be swapped in behind the
    /// same seam later.
    /// </summary>
    public sealed class MatchmakingManager : MonoBehaviour
    {
        public IMatchmaker Matchmaker { get; private set; }
        public bool IsMatching => Matchmaker != null && Matchmaker.IsMatching;

        /// <summary>Status text for the lobby UI (searching / joining / hosting / result).</summary>
        public event Action<string> OnStatus;
        /// <summary>Fired once when a quick match completes.</summary>
        public event Action<MatchResult> OnResult;

        private void Awake()
        {
            Matchmaker = new LanMatchmaker();
            Matchmaker.OnStatus += HandleStatus;
            ServiceLocator.Register(this);
        }

        private void OnDestroy()
        {
            if (Matchmaker != null) Matchmaker.OnStatus -= HandleStatus;
            if (ServiceLocator.Get<MatchmakingManager>() == this)
                ServiceLocator.Unregister<MatchmakingManager>();
        }

        /// <summary>Fire-and-forget quick match (UI button). Result surfaces via OnResult/OnStatus.</summary>
        public async void QuickMatch()
        {
            if (Matchmaker == null || Matchmaker.IsMatching) return;
            MatchResult result = await Matchmaker.QuickMatchAsync(GameConstants.MaxPlayers);
            OnResult?.Invoke(result);
            HandleStatus(Describe(result));
        }

        public void Cancel() => Matchmaker?.Cancel();

        private void HandleStatus(string s) => OnStatus?.Invoke(s);

        private static string Describe(MatchResult r)
        {
            switch (r.Outcome)
            {
                case MatchOutcome.JoinedExisting: return $"Joined {r.Detail}";
                case MatchOutcome.BecameHost:     return "Hosting — waiting for players…";
                case MatchOutcome.Cancelled:      return "Match cancelled.";
                default:                          return $"Match failed: {r.Detail}";
            }
        }
    }
}
