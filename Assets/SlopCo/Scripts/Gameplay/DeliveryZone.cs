using System;
using Unity.Netcode;
using UnityEngine;
using SlopCo.Core;
using SlopCo.Cargo;

namespace SlopCo.Gameplay
{
    /// <summary>
    /// The delivery van trigger. When cargo enters (server-side), payout = currentValue × (1 + speed
    /// bonus from time remaining); the cargo is marked delivered, scored, and despawned. Broadcasts a
    /// "+$$$" cha-ching FX. Attach to the Car Kit van with a trigger collider.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class DeliveryZone : NetworkBehaviour
    {
        [SerializeField] private QuotaSystem quota;

        /// <summary>Raised on all clients on a successful delivery: (worldPos, payout).</summary>
        public static event Action<Vector3, int> OnDelivered;

        private void Awake()
        {
            var col = GetComponent<Collider>();
            col.isTrigger = true;
        }

        public override void OnNetworkSpawn()
        {
            if (quota == null) quota = ServiceLocator.Get<QuotaSystem>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsServer) return;

            var cargo = other.GetComponentInParent<CargoItem>();
            if (cargo == null || cargo.State.Value == CarryState.Delivered) return;

            var condition = cargo.GetComponent<CargoCondition>();
            if (condition == null) return;

            if (quota == null) quota = ServiceLocator.Get<QuotaSystem>();

            var rm = ServiceLocator.Get<RoundManager>();
            float secondsLeft = rm != null ? rm.TimeRemaining.Value : 0f;
            float speedBonus = CargoMath.SpeedBonus(secondsLeft, GameConstants.HaulSeconds);
            int payout = CargoMath.Payout(condition.CurrentValue, speedBonus);

            cargo.MarkDelivered();
            quota?.AddDelivery(payout);
            OnDeliveredRpc(transform.position, payout);

            var netObj = cargo.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned) netObj.Despawn(true);
        }

        [Rpc(SendTo.Everyone)]
        private void OnDeliveredRpc(Vector3 worldPos, int payout) => OnDelivered?.Invoke(worldPos, payout);
    }
}
