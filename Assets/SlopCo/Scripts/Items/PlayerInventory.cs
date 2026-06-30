using Unity.Netcode;
using SlopCo.Player;

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

        /// <summary>SERVER. Use the held consumable: apply its effect, then consume it.</summary>
        [Rpc(SendTo.Server)]
        public void RequestUseConsumableRpc()
        {
            if (!HasConsumable) return;
            ApplyEffectServer(ItemCatalog.Get(ConsumableId.Value));
            ConsumableId.Value = InventoryLogic.DiscardConsumable(ConsumableId.Value);
        }

        /// <summary>SERVER. Use the selected permanent item (reusable — no consume).</summary>
        [Rpc(SendTo.Server)]
        public void RequestUsePermanentRpc()
        {
            int id = SelectedPermanent.Value;
            if (!ItemCatalog.IsPermanent(id) || !InventoryLogic.OwnsPermanent(PermanentMask.Value, id)) return;
            ApplyEffectServer(ItemCatalog.Get(id));
        }

        /// <summary>SERVER. Resolve the effect target (self or nearest teammate) and fire an owner-targeted RPC
        /// so the effect is applied OWNER-locally (speed/stamina are owner-local state).</summary>
        private void ApplyEffectServer(ItemDef def)
        {
            PlayerController target = ItemEffects.IsAlly(def.effect) ? ItemEffects.NearestOtherPlayer(this) : null;
            if (target == null) target = GetComponent<PlayerController>();   // self (or ally fallback when alone)
            if (target == null) return;
            target.ApplyItemEffectRpc(ItemEffects.IsSpeed(def.effect), def.magnitude, def.duration);
        }
    }
}
