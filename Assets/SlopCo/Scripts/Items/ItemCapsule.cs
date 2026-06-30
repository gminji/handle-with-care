using Unity.Netcode;
using UnityEngine;

namespace SlopCo.Items
{
    /// <summary>
    /// A dropped gacha capsule holding one consumable item id (replicated). On player contact (server), the
    /// item is granted into that player's consumable slot (if free) and the capsule reveals + despawns. The
    /// "reveal" (small effect + the real item model) is presentation; part 5 fills <see cref="RevealRpc"/> with
    /// the kit model + particle. If the toucher's slot is full the capsule is left for someone else.
    /// Requires a trigger Collider + NetworkObject; the prefab must be in the NetworkManager prefab list.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class ItemCapsule : NetworkBehaviour
    {
        public readonly NetworkVariable<int> ItemId =
            new NetworkVariable<int>(InventoryLogic.Empty, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        [Tooltip("Optional gacha-shell visual (hidden on reveal). Part 5 wires the kit capsule mesh here.")]
        [SerializeField] private GameObject shell;

        private bool _claimed;

        private void Awake() => GetComponent<Collider>().isTrigger = true;

        /// <summary>SERVER. Set which consumable this capsule yields (called by the spawner right after spawn).</summary>
        public void ServerSetItem(int id) { if (IsServer) ItemId.Value = id; }

        private void OnTriggerEnter(Collider other)
        {
            if (!IsServer || _claimed) return;
            var inv = other.GetComponentInParent<PlayerInventory>();
            if (inv == null) return;
            if (!inv.ServerGrantConsumable(ItemId.Value)) return;   // slot full → leave the capsule in the world

            _claimed = true;
            RevealRpc(ItemId.Value, transform.position);
            var no = GetComponent<NetworkObject>();
            if (no != null && no.IsSpawned) no.Despawn(true);
        }

        /// <summary>ALL CLIENTS. Pickup juice — small effect + reveal the real item model at the pickup point.
        /// PART 5 implements the kit model + particle; part-2 stub keeps the pickup functional without art.</summary>
        [Rpc(SendTo.Everyone)]
        private void RevealRpc(int itemId, Vector3 worldPos)
        {
            if (shell != null) shell.SetActive(false);
            // part 5: Instantiate reveal particle + the item's kit model (detached one-shot at worldPos).
        }
    }
}
