using Unity.Netcode;
using UnityEngine;
using SlopCo.Core;
using SlopCo.Cargo;

namespace SlopCo.Player
{
    /// <summary>
    /// The interaction core. Owner detects a nearby <see cref="CarryHandle"/> and requests a grab; the
    /// SERVER validates and tracks ownership (the cargo's PD drive then pulls toward this player's hand).
    /// Hold grab to keep carrying, release to drop, click/trigger to throw (charged). All cargo physics
    /// happen server-side in CargoItem; this class only sends intents and replicates a held-handle.
    /// </summary>
    public sealed class PlayerCarryController : NetworkBehaviour
    {
        [SerializeField] private PlayerInputReader input;
        [Tooltip("Hand/chest anchor the carried item is pulled toward (the PD target).")]
        [SerializeField] private Transform handAnchor;

        /// <summary>Replicated handle to the cargo this player is gripping (default = none).</summary>
        public readonly NetworkVariable<NetworkObjectReference> HeldCargo =
            new NetworkVariable<NetworkObjectReference>(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>Null-safe: true only if the referenced cargo currently resolves on this client.</summary>
        public bool IsCarrying => HeldCargo.Value.TryGet(out _);

        private bool _grabLatched;

        private void Awake()
        {
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (handAnchor == null) handAnchor = transform;
        }

        private void Update()
        {
            if (!IsOwner || input == null) return;

            bool wantGrab = input.GrabHeld;
            if (wantGrab && !_grabLatched)
            {
                _grabLatched = true;
                TryGrabNearby();
            }
            else if (!wantGrab && _grabLatched)
            {
                _grabLatched = false;
                if (IsCarrying) RequestReleaseRpc();
            }

            if (input.ThrowReleasedThisFrame && IsCarrying)
                RequestThrowRpc(transform.forward, input.ThrowCharge01);
        }

        private void TryGrabNearby()
        {
            // Owner-side detection (generous radius); the server re-validates handle availability.
            Collider[] hits = Physics.OverlapSphere(transform.position, GameConstants.CarryGrabRadius);
            CarryHandle best = null;
            float bestSqr = float.MaxValue;

            foreach (var col in hits)
            {
                var handle = col.GetComponentInParent<CarryHandle>();
                if (handle == null || handle.Owner == null) continue;
                float d = (handle.AttachPoint.position - transform.position).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = handle; }
            }

            if (best == null) return;
            var netObj = best.Owner.GetComponent<NetworkObject>();
            if (netObj != null) RequestGrabRpc(netObj, best.HandleId);
        }

        [Rpc(SendTo.Server)]
        private void RequestGrabRpc(NetworkObjectReference cargoRef, int handleId)
        {
            if (!cargoRef.TryGet(out var cargoObj)) return;
            var cargo = cargoObj.GetComponent<CargoItem>();
            if (cargo == null) return;

            Transform handTarget = handAnchor != null ? handAnchor : transform;
            if (cargo.TryClaimHandle(handleId, OwnerClientId, handTarget))
                HeldCargo.Value = cargoObj;
        }

        [Rpc(SendTo.Server)]
        private void RequestReleaseRpc()
        {
            if (HeldCargo.Value.TryGet(out var cargoObj))
            {
                var cargo = cargoObj.GetComponent<CargoItem>();
                if (cargo != null) cargo.ReleaseHandle(OwnerClientId);
            }
            HeldCargo.Value = default;
        }

        [Rpc(SendTo.Server)]
        private void RequestThrowRpc(Vector3 dir, float charge01)
        {
            if (HeldCargo.Value.TryGet(out var cargoObj))
            {
                var cargo = cargoObj.GetComponent<CargoItem>();
                if (cargo != null) cargo.RequestThrow(OwnerClientId, dir, charge01);
            }
            HeldCargo.Value = default;
        }

        public override void OnNetworkDespawn()
        {
            // If a carrier disconnects mid-haul, the server frees its handle (item drops / staggers).
            if (IsServer && HeldCargo.Value.TryGet(out var cargoObj))
            {
                var cargo = cargoObj.GetComponent<CargoItem>();
                if (cargo != null) cargo.ReleaseHandle(OwnerClientId);
            }
        }
    }
}
