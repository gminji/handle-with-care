namespace SlopCo.Items
{
    /// <summary>
    /// Pure inventory-slot transitions (NO UnityEngine / Netcode dependency → EditMode-testable, mirrors
    /// DashStamina / WheelMath). <see cref="PlayerInventory"/> applies these to its replicated NetworkVariables.
    /// Consumable = a single slot (one item, discardable). Permanent = a 32-bit ownership mask with a selected id.
    /// </summary>
    public static class InventoryLogic
    {
        public const int Empty = -1;
        public const int MaskWidth = 32;

        // ── Consumable single slot ──
        public static bool CanGrantConsumable(int slot) => slot == Empty;
        /// <summary>Grant only into an empty slot (no silent overwrite).</summary>
        public static int GrantConsumable(int slot, int itemId) => slot == Empty ? itemId : slot;
        public static int DiscardConsumable(int slot) => Empty;

        // ── Permanent ownership bitmask (bit i = owns item id i) ──
        public static bool OwnsPermanent(int mask, int id) => id >= 0 && id < MaskWidth && (mask & (1 << id)) != 0;
        public static int AddPermanent(int mask, int id) => (id >= 0 && id < MaskWidth) ? (mask | (1 << id)) : mask;
        public static int PermanentCount(int mask)
        {
            int n = 0;
            while (mask != 0) { n += mask & 1; mask = (int)((uint)mask >> 1); }
            return n;
        }

        /// <summary>Next owned permanent id after <paramref name="current"/> (wraps). Empty if none owned;
        /// returns the same id if exactly one is owned.</summary>
        public static int CycleSelected(int mask, int current, int width)
        {
            if (mask == 0) return Empty;
            if (width <= 0) width = MaskWidth;
            int start = current < 0 ? -1 : current;
            for (int step = 1; step <= width; step++)
            {
                int cand = ((start + step) % width + width) % width;
                if (OwnsPermanent(mask, cand)) return cand;
            }
            return current;
        }
    }
}
