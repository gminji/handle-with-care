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

        public override void OnNetworkSpawn()
        {
            ServiceLocator.Register(this);
            if (quota == null) quota = GetComponent<QuotaSystem>();
            if (cargoSpawner == null) cargoSpawner = GetComponent<CargoSpawner>();
        }

        public override void OnNetworkDespawn()
        {
            if (ServiceLocator.Get<RoundManager>() == this) ServiceLocator.Unregister<RoundManager>();
        }

        /// <summary>Host/lobby → begin (or restart after GameOver) a run.</summary>
        [Rpc(SendTo.Server)]
        public void RequestStartRpc()
        {
            if (Phase.Value == RoundPhase.Lobby || Phase.Value == RoundPhase.GameOver)
                StartGame();
        }

        private void Update()
        {
            if (!IsServer) return;
            if (Phase.Value == RoundPhase.Lobby || Phase.Value == RoundPhase.GameOver) return;

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
            quota?.ResetForNewGame();
            RoundNumber.Value = 1;
            SetPhase(RoundPhase.Briefing, GameConstants.BriefingSeconds);
        }

        private void BeginHauling()
        {
            cargoSpawner?.SpawnRoundCargo();
            SetPhase(RoundPhase.Hauling, GameConstants.HaulSeconds);
        }

        private void BeginPayout()
        {
            cargoSpawner?.ClearRemainingCargo();
            _payoutMet = quota != null && quota.EvaluateQuota();
            SetPhase(RoundPhase.Payout, 4f);
        }

        private void EndPayout()
        {
            if (_payoutMet)
            {
                quota?.EscalateQuota();
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
            Phase.Value = phase;
            _phaseTimer = duration;
            TimeRemaining.Value = duration;
            OnPhaseChangedRpc(phase);
        }

        [Rpc(SendTo.Everyone)]
        private void OnPhaseChangedRpc(RoundPhase phase) => OnPhaseChanged?.Invoke(phase);
    }
}
