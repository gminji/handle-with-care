using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using SlopCo.Core;

namespace SlopCo.Gameplay
{
    /// <summary>
    /// Somebody rage-quit / crashed / lost wifi mid-run. Rather than letting the remaining crew keep hauling
    /// a job that is now impossible (or silently killing the run), the server PAUSES everything, tells the
    /// survivors what happened, and puts it to a vote: carry on, or call it a day.
    ///
    /// Server-authoritative: the pause flag and the tally are replicated, the decision is made once here.
    /// A tie — or nobody answering before the timer runs out — means CARRY ON: ending someone's run is the
    /// destructive option, so it needs an actual majority. Lives on the GameSystems NetworkObject.
    /// </summary>
    public sealed class DisconnectVote : NetworkBehaviour
    {
        public const int SlotContinue = 0;
        public const int SlotEnd = 1;

        /// <summary>Everything gameplay freezes while this is true (round timer, fuses, hazards, input).</summary>
        public readonly NetworkVariable<bool> Paused =
            new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>Live tally, packed by <see cref="AugmentOffer"/>: slot 0 = continue, slot 1 = end.</summary>
        public readonly NetworkVariable<int> VotesPacked =
            new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>Seconds left to answer (for the UI countdown).</summary>
        public readonly NetworkVariable<float> TimeLeft =
            new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>How many players have dropped during this run.</summary>
        public readonly NetworkVariable<int> DroppedCount =
            new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>Cheap global read for the systems that must freeze — mirrors <see cref="Paused"/> so
        /// per-frame checks in fuses and hazards don't each pay a ServiceLocator lookup.</summary>
        public static bool GameFrozen { get; private set; }

        private readonly Dictionary<ulong, int> _ballots = new();

        // SERVER. Clients that actually made it into the session. NGO raises OnClientDisconnectCallback on the
        // server for a connection it REFUSED at approval too (full lobby, mid-haul join — see
        // SlopCo.Networking.JoinPolicy). That peer was never part of the crew, and pausing for it would let
        // anyone freeze a haul just by trying to join it.
        private readonly HashSet<ulong> _crew = new();

        public override void OnNetworkSpawn()
        {
            ServiceLocator.Register(this);
            Paused.OnValueChanged += HandlePausedChanged;
            GameFrozen = Paused.Value;
            if (IsServer && NetworkManager.Singleton != null)
            {
                var nm = NetworkManager.Singleton;
                foreach (ulong id in nm.ConnectedClientsIds) _crew.Add(id);
                nm.OnClientConnectedCallback += HandleClientConnected;
                nm.OnClientDisconnectCallback += HandleClientDisconnect;
            }
        }

        public override void OnNetworkDespawn()
        {
            Paused.OnValueChanged -= HandlePausedChanged;
            if (IsServer && NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= HandleClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientDisconnect;
            }
            _crew.Clear();
            GameFrozen = false;
            if (ServiceLocator.Get<DisconnectVote>() == this) ServiceLocator.Unregister<DisconnectVote>();
        }

        private void HandlePausedChanged(bool _, bool next) => GameFrozen = next;

        private void HandleClientConnected(ulong clientId) => _crew.Add(clientId);

        // SERVER. Someone left. Only a mid-round departure by a crew member is worth stopping for
        // (DisconnectRules.IsDeparture).
        private void HandleClientDisconnect(ulong clientId)
        {
            if (!IsServer) return;
            bool wasInSession = _crew.Remove(clientId);

            var rm = ServiceLocator.Get<RoundManager>();
            if (rm == null) return;
            if (!DisconnectRules.IsDeparture(wasInSession, clientId == NetworkManager.ServerClientId, rm.Phase.Value)) return;

            DroppedCount.Value++;
            _ballots.Remove(clientId);

            // Last human standing: no one to ask, and a one-player vote is just a confirmation dialog.
            if (RemainingVoters() <= 0) { Resume(); return; }

            Paused.Value = true;
            TimeLeft.Value = GameConstants.DisconnectVoteSeconds;
            PublishCounts();
        }

        private int RemainingVoters()
        {
            var nm = NetworkManager.Singleton;
            return nm != null ? nm.ConnectedClientsIds.Count : 0;
        }

        /// <summary>SERVER (via client request). Vote to keep playing (false) or end the run (true).</summary>
        [Rpc(SendTo.Server)]
        public void SubmitRpc(bool endRun, RpcParams rpcParams = default)
        {
            if (!Paused.Value) return;
            _ballots[rpcParams.Receive.SenderClientId] = endRun ? SlotEnd : SlotContinue;
            PublishCounts();
            if (_ballots.Count >= RemainingVoters()) Resolve();
        }

        private void PublishCounts()
        {
            var counts = CountBallots();
            VotesPacked.Value = AugmentOffer.Pack(counts, counts.Length);
        }

        private int[] CountBallots()
        {
            var counts = new int[AugmentOffer.MaxSlots];
            foreach (var kv in _ballots)
                if (kv.Value >= 0 && kv.Value < counts.Length) counts[kv.Value]++;
            return counts;
        }

        private void Update()
        {
            if (!IsServer || !Paused.Value) return;
            TimeLeft.Value = Mathf.Max(0f, TimeLeft.Value - Time.unscaledDeltaTime);
            if (TimeLeft.Value <= 0f) Resolve();
        }

        // SERVER. Majority ends the run; a tie or an empty ballot keeps it alive. Ending is the destructive
        // choice, so it never happens by default or by coin flip.
        private void Resolve()
        {
            var counts = CountBallots();
            bool end = counts[SlotEnd] > counts[SlotContinue];
            Resume();
            if (end) ServiceLocator.Get<RoundManager>()?.EndRunNow();
        }

        private void Resume()
        {
            Paused.Value = false;
            TimeLeft.Value = 0f;
            VotesPacked.Value = 0;
            _ballots.Clear();
        }

        /// <summary>Votes currently cast for a slot.</summary>
        public int VotesFor(int slot) => Mathf.Max(0, AugmentOffer.Slot(VotesPacked.Value, slot));
    }
}
