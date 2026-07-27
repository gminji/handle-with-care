using System;
using Unity.Netcode;
using UnityEngine;
using SlopCo.Core;

namespace SlopCo.Gameplay
{
    public enum RoundPhase : byte { Lobby = 0, Briefing = 1, Hauling = 2, Payout = 3, GameOver = 4 }

    /// <summary>
    /// Server-driven round state machine: Lobby → Briefing → Hauling → Payout → (Briefing | GameOver).
    /// The host starts a run via <see cref="RequestStartRpc"/>. Phase/timer/round are replicated; the
    /// HUD reads them. Enum default is Lobby (= 0) so late-joiners read a sane initial phase.
    /// </summary>
    public sealed class RoundManager : NetworkBehaviour
    {
        [SerializeField] private QuotaSystem quota;
        [SerializeField] private CargoSpawner cargoSpawner;

        public readonly NetworkVariable<RoundPhase> Phase =
            new NetworkVariable<RoundPhase>(RoundPhase.Lobby, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<float> TimeRemaining =
            new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<int> RoundNumber =
            new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>Transient phase-change cue for clients (stingers / banners).</summary>
        public static event Action<RoundPhase> OnPhaseChanged;

        private float _phaseTimer;
        private bool _payoutMet;
        private int _bombsToDeliver;
        private int _bombsDelivered;

        public override void OnNetworkSpawn()
        {
            ServiceLocator.Register(this);
            if (quota == null) quota = GetComponent<QuotaSystem>();
            if (cargoSpawner == null) cargoSpawner = GetComponent<CargoSpawner>();
            DeliveryZone.OnDelivered += HandleDelivered;
        }

        public override void OnNetworkDespawn()
        {
            DeliveryZone.OnDelivered -= HandleDelivered;
            if (ServiceLocator.Get<RoundManager>() == this) ServiceLocator.Unregister<RoundManager>();
        }

        // Bomb mode: the day is won the instant every bomb is safely delivered (no need to burn the clock).
        private void HandleDelivered(Vector3 worldPos, int payout)
        {
            if (!IsServer) return;
            _bombsDelivered++;
            if (cargoSpawner != null && cargoSpawner.BombMode
                && Phase.Value == RoundPhase.Hauling && _bombsDelivered >= _bombsToDeliver)
                BeginPayout();
        }

        /// <summary>Host/lobby → begin (or restart after GameOver) a run.</summary>
        [Rpc(SendTo.Server)]
        public void RequestStartRpc()
        {
            if (Phase.Value == RoundPhase.Lobby || Phase.Value == RoundPhase.GameOver)
                StartGame();
        }

        /// <summary>SERVER. End the run immediately — e.g. the bomb cargo detonated in your arms.</summary>
        public void EndRunNow()
        {
            if (IsServer) SetPhase(RoundPhase.GameOver, 0f);
        }

        private void Update()
        {
            if (!IsServer) return;
            if (Phase.Value == RoundPhase.Lobby || Phase.Value == RoundPhase.GameOver) return;
            if (DisconnectVote.GameFrozen) return;   // someone dropped — the crew is deciding whether to go on

            _phaseTimer -= Time.deltaTime;
            TimeRemaining.Value = Mathf.Max(0f, _phaseTimer);
            if (_phaseTimer <= 0f) Advance();
        }

        private void Advance()
        {
            switch (Phase.Value)
            {
                case RoundPhase.Briefing: BeginHauling(); break;
                case RoundPhase.Hauling: BeginPayout(); break;
                case RoundPhase.Payout: EndPayout(); break;
            }
        }

        private void StartGame()
        {
            // (Re)resolve the map first so this run — including a restart after GameOver — uses the
            // selected map, or re-rolls when the player chose Random. Clients sync via ActiveMap.
            ServiceLocator.Get<MapManager>()?.RollMap();
            quota?.ResetForNewGame();
            // Bomb mode is a survival loop, not a cash-quota grind — keep quota trivially met so the
            // payout card reads "SURVIVED"; the only fail state is a detonation.
            if (cargoSpawner != null && cargoSpawner.BombMode && quota != null) quota.Quota.Value = 1;
            RoundNumber.Value = 1;
            SetPhase(RoundPhase.Briefing, GameConstants.BriefingSeconds);
        }

        private void BeginHauling()
        {
            cargoSpawner?.SpawnRoundCargo();
            ServiceLocator.Get<AugmentSystem>()?.SpawnRatsIfNeeded(); // augment hazard, this round
            ServiceLocator.Get<HazardDirector>()?.BeginHaul();        // roaming hazards (UFO / thief)
            _bombsToDeliver = cargoSpawner != null ? cargoSpawner.LastSpawnCount : 0;
            _bombsDelivered = 0;
            SetPhase(RoundPhase.Hauling, GameConstants.HaulSeconds);
        }

        private void BeginPayout()
        {
            cargoSpawner?.ClearRemainingCargo();
            ServiceLocator.Get<AugmentSystem>()?.ClearRats(); // hazards gone during the shop beat
            ServiceLocator.Get<HazardDirector>()?.ClearHazards();
            _payoutMet = (cargoSpawner != null && cargoSpawner.BombMode)
                ? _bombsDelivered >= _bombsToDeliver
                : quota != null && quota.EvaluateQuota();
            SetPhase(RoundPhase.Payout, GameConstants.PayoutWindowSeconds);
        }

        private void EndPayout()
        {
            if (_payoutMet)
            {
                if (cargoSpawner == null || !cargoSpawner.BombMode) quota?.EscalateQuota();
                RoundNumber.Value += 1;
                SetPhase(RoundPhase.Briefing, GameConstants.BriefingSeconds);
            }
            else
            {
                SetPhase(RoundPhase.GameOver, 0f);
            }
        }

        private void SetPhase(RoundPhase phase, float duration)
        {
            // A run can end mid-haul (detonation), so sweep the roaming hazards here rather than only at Payout.
            if (phase == RoundPhase.GameOver || phase == RoundPhase.Lobby)
                ServiceLocator.Get<HazardDirector>()?.ClearHazards();
            Phase.Value = phase;
            _phaseTimer = duration;
            TimeRemaining.Value = duration;
            OnPhaseChangedRpc(phase);
        }

        [Rpc(SendTo.Everyone)]
        private void OnPhaseChangedRpc(RoundPhase phase) => OnPhaseChanged?.Invoke(phase);
    }
}
