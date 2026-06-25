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
        private LineRenderer _aim;

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

            // Owner-local throw telegraph: a predicted arc while charging — the wind-up that turns a
            // silent drop into a readable "I'm about to yeet this at you" clip moment.
            bool charging = IsCarrying && input.ThrowCharge01 > 0.02f;
            if (charging) DrawAimArc(input.ThrowCharge01);
            else if (_aim != null) _aim.enabled = false;

            if (input.ThrowReleasedThisFrame && IsCarrying)
            {
                if (_aim != null) _aim.enabled = false;
                RequestThrowRpc(transform.forward, input.ThrowCharge01);
            }
        }

        private void DrawAimArc(float charge)
        {
            if (_aim == null)
            {
                var go = new GameObject("AimArc");
                go.transform.SetParent(transform, false);
                _aim = go.AddComponent<LineRenderer>();
                _aim.useWorldSpace = true;
                _aim.numCapVertices = 2;
                var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (sh == null) sh = Shader.Find("Sprites/Default");
                _aim.material = new Material(sh);
            }
            _aim.enabled = true;

            Vector3 origin = (handAnchor != null ? handAnchor.position : transform.position) + Vector3.up * 0.2f;
            Vector3 dir = (transform.forward + Vector3.up * GameConstants.ThrowUpwardBias).normalized;
            float speed = Mathf.Lerp(GameConstants.ThrowMinImpulse, GameConstants.ThrowMaxImpulse, charge);
            Vector3 v = dir * speed;
            const int N = 16; const float dt = 0.06f;
            _aim.positionCount = N;
            Vector3 p = origin;
            for (int i = 0; i < N; i++) { _aim.SetPosition(i, p); v += Physics.gravity * dt; p += v * dt; }

            Color c = Color.Lerp(new Color(1f, 0.9f, 0.3f), Color.red, charge);
            _aim.startColor = c;
            _aim.endColor = new Color(c.r, c.g, c.b, 0.15f);
            _aim.startWidth = 0.10f + 0.10f * charge;
            _aim.endWidth = 0.03f;
        }

        private void TryGrabNearby()
        {
            // Owner-side detection (generous radius); the server re-validates handle availability.
            // Cargo colliders sit on the ROOT while CarryHandles are child markers without colliders,
            // so resolve the CargoItem from each hit and pick its nearest handle (not GetComponentInParent
            // on the handle, which never matches the root collider).
            Collider[] hits = Physics.OverlapSphere(transform.position, GameConstants.CarryGrabRadius);
            CarryHandle best = null;
            float bestSqr = float.MaxValue;

            foreach (var col in hits)
            {
                var cargo = col.GetComponentInParent<CargoItem>();
                if (cargo == null) continue;
                foreach (var handle in cargo.Handles)
                {
                    if (handle == null || handle.AttachPoint == null) continue;
                    float d = (handle.AttachPoint.position - transform.position).sqrMagnitude;
                    if (d < bestSqr) { bestSqr = d; best = handle; }
                }
            }

            if (best == null || best.Owner == null) return;
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
            // Carrier id = this player's NetworkObjectId (unique per human AND per server-owned bot),
            // not OwnerClientId — bots share clientId 0 with the host.
            if (cargo.TryClaimHandle(handleId, NetworkObjectId, handTarget))
                HeldCargo.Value = cargoObj;
        }

        [Rpc(SendTo.Server)]
        private void RequestReleaseRpc()
        {
            if (HeldCargo.Value.TryGet(out var cargoObj))
            {
                var cargo = cargoObj.GetComponent<CargoItem>();
                if (cargo != null) cargo.ReleaseHandle(NetworkObjectId);
            }
            HeldCargo.Value = default;
        }

        [Rpc(SendTo.Server)]
        private void RequestThrowRpc(Vector3 dir, float charge01)
        {
            if (HeldCargo.Value.TryGet(out var cargoObj))
            {
                var cargo = cargoObj.GetComponent<CargoItem>();
                if (cargo != null) cargo.RequestThrow(NetworkObjectId, dir, charge01);
            }
            HeldCargo.Value = default;
        }

        public override void OnNetworkDespawn()
        {
            // If a carrier disconnects mid-haul, the server frees its handle (item drops / staggers).
            if (IsServer && HeldCargo.Value.TryGet(out var cargoObj))
            {
                var cargo = cargoObj.GetComponent<CargoItem>();
                if (cargo != null) cargo.ReleaseHandle(NetworkObjectId);
            }
        }
    }
}
