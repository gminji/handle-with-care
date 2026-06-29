using System.Collections.Generic;
using UnityEngine;
using SlopCo.Core;
using SlopCo.Cargo;

namespace SlopCo.Gameplay
{
    /// <summary>
    /// Per-player run tally for the end-of-run "CREW MVP" callout. Mirrors <see cref="RunStats"/>: fed by the
    /// replicated attribution events (<see cref="DeliveryZone.OnDeliveredAttributed"/> /
    /// <see cref="CargoCondition.OnSmashAttributed"/>) so every client's tally matches with no extra netcode,
    /// and reset at the start of a fresh run (first Briefing of round 1). CarrierId is a player's
    /// NetworkObjectId (unique per human AND per AI bot); id 0 (unattributed) is dropped.
    /// </summary>
    public sealed class PlayerScoreboard : MonoBehaviour
    {
        private readonly Dictionary<ulong, int> _delivered = new();
        private readonly Dictionary<ulong, int> _destroyed = new();

        private void Awake() => ServiceLocator.Register(this);

        private void OnDestroy()
        {
            if (ServiceLocator.Get<PlayerScoreboard>() == this) ServiceLocator.Unregister<PlayerScoreboard>();
        }

        private void OnEnable()
        {
            DeliveryZone.OnDeliveredAttributed += HandleDeliver;
            CargoCondition.OnSmashAttributed += HandleSmash;
            RoundManager.OnPhaseChanged += HandlePhase;
        }

        private void OnDisable()
        {
            DeliveryZone.OnDeliveredAttributed -= HandleDeliver;
            CargoCondition.OnSmashAttributed -= HandleSmash;
            RoundManager.OnPhaseChanged -= HandlePhase;
        }

        private void HandleDeliver(ulong carrierId, int payout)
        {
            if (carrierId == 0 || payout <= 0) return;
            _delivered.TryGetValue(carrierId, out int cur);
            _delivered[carrierId] = cur + payout;
        }

        private void HandleSmash(ulong carrierId, int valueLost)
        {
            if (carrierId == 0 || valueLost <= 0) return;
            _destroyed.TryGetValue(carrierId, out int cur);
            _destroyed[carrierId] = cur + valueLost;
        }

        private void HandlePhase(RoundPhase phase)
        {
            if (phase != RoundPhase.Briefing) return;
            var rm = ServiceLocator.Get<RoundManager>();
            if (rm != null && rm.RoundNumber.Value <= 1) ResetRun();
        }

        /// <summary>Snapshot of the current per-player tallies (union of everyone who delivered or smashed),
        /// for <see cref="MvpAward"/> to pick winners from.</summary>
        public IReadOnlyList<PlayerTally> Snapshot()
        {
            var ids = new HashSet<ulong>();
            foreach (var k in _delivered.Keys) ids.Add(k);
            foreach (var k in _destroyed.Keys) ids.Add(k);

            var list = new List<PlayerTally>(ids.Count);
            foreach (var id in ids)
            {
                _delivered.TryGetValue(id, out int d);
                _destroyed.TryGetValue(id, out int s);
                list.Add(new PlayerTally(id, d, s));
            }
            return list;
        }

        /// <summary>Total $ delivered by a specific player this run (0 if none).</summary>
        public int DeliveredBy(ulong carrierId)
        {
            _delivered.TryGetValue(carrierId, out int v);
            return v;
        }

        /// <summary>Total $ smashed by a specific player this run (0 if none).</summary>
        public int DestroyedBy(ulong carrierId)
        {
            _destroyed.TryGetValue(carrierId, out int v);
            return v;
        }

        public void ResetRun()
        {
            _delivered.Clear();
            _destroyed.Clear();
        }
    }
}
