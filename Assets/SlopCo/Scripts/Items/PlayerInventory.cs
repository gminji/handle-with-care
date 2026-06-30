using Unity.Netcode;

namespace SlopCo.Items
{
    /// <summary>
    /// Per-player inventory (server-authoritative, replicated). Lives on the player prefab alongside
    /// PlayerController (wired in part 5). Holds ONE consumable (dropped, discardable) and a bitmask of owned
    /// PERMANENT items with a selected id. Pure transitions live in <see cref="InventoryLogic"/>.
    ///
    /// PART 1 SCOPE: state + grant + discard + cycle. Effect application (use) is intentionally a stub —
    /// part 3 fills <see cref="RequestUseConsumableRpc"/> / <see cref="RequestUsePermanentRpc"/> with the
    /// self/ally effects; part 2 calls <see cref="ServerGrantConsumable"/> from the gacha capsule pickup.
    /// </summary>
    public sealed class PlayerInventory : NetworkBehaviour
    {
        /// <summary>Held consumable item id, or <see cref="InventoryLogic.Empty"/> when the slot is free.</summary>
        public readonly NetworkVariable<int> ConsumableId =
            new NetworkVariable<int>(InventoryLogic.Empty, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>Owned PERMANENT items as a bitmask (bit i = owns item id i).</summary>
        public readonly NetworkVariable<int> PermanentMask =
            new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>Currently selected permanent item id, or <see cref="InventoryLogic.Empty"/>.</summary>
        public readonly NetworkVariable<int> SelectedPermanent =
            new NetworkVariable<int>(InventoryLogic.Empty, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public bool HasConsumable => ConsumableId.Value != InventoryLogic.Empty;

        // ── SERVER grants (called by capsule pickup / augment shop in later parts) ──

        /// <summary>SERVER. Put a consumable into the slot if empty. Returns false if the slot is occupied
        /// (the capsule should stay in the world for someone else).</summary>
        public bool ServerGrantConsumable(int itemId)
        {
            if (!IsServer || !ItemCatalog.IsConsumable(itemId)) return false;
            if (!InventoryLogic.CanGrantConsumable(ConsumableId.Value)) return false;
            ConsumableId.Value = InventoryLogic.GrantConsumable(ConsumableId.Value, itemId);
            return true;
        }

        /// <summary>SERVER. Grant ownership of a permanent item (augment-choice). Auto-selects the first one.</summary>
        public void ServerGrantPermanent(int itemId)
        {
            if (!IsServer || !ItemCatalog.IsPermanent(itemId)) return;
            PermanentMask.Value = InventoryLogic.AddPermanent(PermanentMask.Value, itemId);
            if (SelectedPermanent.Value == InventoryLogic.Empty) SelectedPermanent.Value = itemId;
        }

        // ── Client requests (owner-driven, server-validated) ──

        /// <summary>Discard the held consumable to make room for another (permanently gone).</summary>
        [Rpc(SendTo.Server)]
        public void RequestDiscardConsumableRpc()
        {
            if (HasConsumable) ConsumableId.Value = InventoryLogic.DiscardConsumable(ConsumableId.Value);
        }

        /// <summary>Cycle the selected permanent item among owned ones.</summary>
        [Rpc(SendTo.Server)]
        public void RequestCyclePermanentRpc()
        {
            SelectedPermanent.Value = InventoryLogic.CycleSelected(PermanentMask.Value, SelectedPermanent.Value, InventoryLogic.MaskWidth);
        }

        /// <summary>Use the held consumable. PART 3 applies the effect + consumes; part-1 stub does nothing
        /// (so "use" never silently deletes an item before the effect exists).</summary>
        [Rpc(SendTo.Server)]
        public void RequestUseConsumableRpc()
        {
            // part 3: ItemEffects.Apply(this, ConsumableId.Value); then ConsumableId.Value = InventoryLogic.DiscardConsumable(...)
        }

        /// <summary>Use the selected permanent item (no consume). PART 3 applies the effect; part-1 stub no-ops.</summary>
        [Rpc(SendTo.Server)]
        public void RequestUsePermanentRpc()
        {
            // part 3: ItemEffects.Apply(this, SelectedPermanent.Value)
        }
    }
}
