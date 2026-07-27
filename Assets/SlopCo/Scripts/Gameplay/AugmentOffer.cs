namespace SlopCo.Gameplay
{
    /// <summary>
    /// Packs the crew's three shop choices (and later their vote counts) into ONE int so they ride the
    /// project's established <c>NetworkVariable&lt;int&gt;</c> pattern instead of introducing a NetworkList.
    /// Pure bit twiddling — EditMode-testable, no UnityEngine dependency.
    ///
    /// Layout: 3 slots × 8 bits. A slot holds <c>value + 1</c> so 0 reads as "empty", which also makes the
    /// default value of a fresh NetworkVariable (0) mean "no offer yet".
    /// </summary>
    public static class AugmentOffer
    {
        public const int MaxSlots = 3;

        /// <summary>Pack up to <see cref="MaxSlots"/> small non-negative values (ids or counts).</summary>
        public static int Pack(int[] values, int count)
        {
            int packed = 0;
            if (values == null) return 0;
            if (count > MaxSlots) count = MaxSlots;
            if (count > values.Length) count = values.Length;
            for (int i = 0; i < count; i++)
            {
                int v = values[i];
                if (v < 0) v = -1;                 // clamps to "empty"
                if (v > 254) v = 254;
                packed |= ((v + 1) & 0xFF) << (i * 8);
            }
            return packed;
        }

        /// <summary>Value in a slot, or -1 when the slot is empty.</summary>
        public static int Slot(int packed, int index)
        {
            if (index < 0 || index >= MaxSlots) return -1;
            return ((packed >> (index * 8)) & 0xFF) - 1;
        }

        /// <summary>How many leading slots are filled.</summary>
        public static int Count(int packed)
        {
            int n = 0;
            for (int i = 0; i < MaxSlots; i++)
            {
                if (Slot(packed, i) < 0) break;
                n++;
            }
            return n;
        }

        /// <summary>Unpack into a fresh array of exactly <see cref="Count"/> entries.</summary>
        public static int[] Unpack(int packed)
        {
            int n = Count(packed);
            var arr = new int[n];
            for (int i = 0; i < n; i++) arr[i] = Slot(packed, i);
            return arr;
        }
    }
}
