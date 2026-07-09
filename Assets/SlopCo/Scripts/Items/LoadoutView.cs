namespace SlopCo.Items
{
    /// <summary>
    /// Pure display-selection logic for the teammate loadout HUD (NO UnityEngine / Netcode dependency →
    /// EditMode-testable, mirrors <see cref="InventoryLogic"/> / DashStamina). Picks WHICH item ids a
    /// teammate row should show from that teammate's replicated inventory state; the HUD turns the ids
    /// into localized strings. Uses <see cref="ItemCatalog"/> (also Unity-free) for id validity.
    /// </summary>
    public static class LoadoutView
    {
        /// <summary>The permanent item ("augment-choice") id to show, or <see cref="InventoryLogic.Empty"/>.</summary>
        public static int AugmentDisplayId(int selectedPermanent)
            => ItemCatalog.IsValid(selectedPermanent) ? selectedPermanent : InventoryLogic.Empty;

        /// <summary>The item id to show, preferring the currently HELD consumable over the most-recently
        /// USED item. <see cref="InventoryLogic.Empty"/> when neither is valid.</summary>
        public static int ItemDisplayId(int consumableId, int lastUsedId)
        {
            if (ItemCatalog.IsValid(consumableId)) return consumableId; // currently holding
            if (ItemCatalog.IsValid(lastUsedId)) return lastUsedId;     // most recently used
            return InventoryLogic.Empty;
        }

        /// <summary>True when the item shown is currently HELD (vs a past "used" record) — for label prefixing.</summary>
        public static bool ItemIsHeld(int consumableId) => ItemCatalog.IsValid(consumableId);
    }
}
