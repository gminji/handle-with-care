using UnityEngine;
using SlopCo.Core;
using SlopCo.Cargo;

namespace SlopCo.Gameplay
{
    /// <summary>
    /// Accumulates the shareable run tally the end-of-run card needs: total cargo value destroyed, the
    /// single biggest smash (the "YOU smashed the $400 vase" beat), and deliveries. Fed by the same
    /// replicated FX events as the HUD/juice, so every client's tally matches with no extra netcode.
    /// Resets at the start of a fresh run (first Briefing of round 1).
    /// </summary>
    public sealed class RunStats : MonoBehaviour
    {
        public int TotalDestroyed { get; private set; }
        public int BiggestSmash { get; private set; }
        public int TotalDelivered { get; private set; }
        public int DeliveryCount { get; private set; }
        public int BestCombo { get; private set; }

        private void Awake() => ServiceLocator.Register(this);

        private void OnDestroy()
        {
            if (ServiceLocator.Get<RunStats>() == this) ServiceLocator.Unregister<RunStats>();
        }

        private void OnEnable()
        {
            CargoCondition.OnDamageFx += HandleDamage;
            DeliveryZone.OnDelivered += HandleDeliver;
            RoundManager.OnPhaseChanged += HandlePhase;
            ComboSystem.OnCombo += HandleCombo;
        }

        private void OnDisable()
        {
            CargoCondition.OnDamageFx -= HandleDamage;
            DeliveryZone.OnDelivered -= HandleDeliver;
            RoundManager.OnPhaseChanged -= HandlePhase;
            ComboSystem.OnCombo -= HandleCombo;
        }

        private void HandleCombo(int combo, Vector3 pos)
        {
            if (combo > BestCombo) BestCombo = combo;
        }

        private void HandleDamage(Vector3 pos, int valueLost, bool bigSmash)
        {
            TotalDestroyed += valueLost;
            if (valueLost > BiggestSmash) BiggestSmash = valueLost;
        }

        private void HandleDeliver(Vector3 pos, int payout)
        {
            TotalDelivered += payout;
            DeliveryCount++;
        }

        private void HandlePhase(RoundPhase phase)
        {
            if (phase != RoundPhase.Briefing) return;
            var rm = ServiceLocator.Get<RoundManager>();
            if (rm != null && rm.RoundNumber.Value <= 1) ResetRun();
        }

        public void ResetRun()
        {
            TotalDestroyed = 0; BiggestSmash = 0; TotalDelivered = 0; DeliveryCount = 0; BestCombo = 0;
        }
    }
}
