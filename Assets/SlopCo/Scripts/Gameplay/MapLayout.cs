namespace SlopCo.Gameplay
{
    /// <summary>What kind of junk is blocking the route.</summary>
    public enum ObstacleKind : byte { Crate = 0, Barrel = 1, Cone = 2, Barrier = 3 }

    /// <summary>
    /// Pure per-run layout roll — NO UnityEngine dependency, so it is EditMode-testable like
    /// <see cref="DailyModifier"/>, whose hashing this reuses (lowbias32 finalizer + Lemire multiply-shift;
    /// a plain `% n` on a hashed small int is degenerate).
    ///
    /// Everything here is a function of (seed, slot), so the SERVER only replicates one int and every client
    /// rebuilds the identical van position and obstacle field with no extra netcode — the same trick
    /// <see cref="DailyModifier"/> plays with the day number.
    /// </summary>
    public static class MapLayout
    {
        /// <summary>Well-mixed hash of a (seed, salt) pair.</summary>
        public static uint Hash(int seed, int salt)
        {
            uint h = (uint)seed * 2654435761u ^ (uint)(salt * 40503);
            h ^= h >> 16; h *= 0x7feb352dU; h ^= h >> 15; h *= 0x846ca68bU; h ^= h >> 16;
            return h;
        }

        /// <summary>Uniform index in [0, count). Returns 0 for a non-positive count.</summary>
        public static int Pick(int seed, int salt, int count)
        {
            if (count <= 1) return 0;
            return (int)(((ulong)Hash(seed, salt) * (ulong)count) >> 32);
        }

        /// <summary>Which van dock this run uses — this is what makes the haul short or long.</summary>
        public static int VanAnchorIndex(int seed, int anchorCount) => Pick(seed, 101, anchorCount);

        /// <summary>Is this candidate obstacle position occupied this run? <paramref name="percent"/> is the
        /// rough share of slots that fill (0 = a clear route, 100 = every slot). Slots are independent, so the
        /// actual count varies run to run — that variance IS the variety.</summary>
        public static bool SlotActive(int seed, int slot, int percent)
        {
            if (percent <= 0) return false;
            if (percent >= 100) return true;
            return (int)(((ulong)Hash(seed, 977 + slot) * 100UL) >> 32) < percent;
        }

        /// <summary>What's sitting in this slot.</summary>
        public static ObstacleKind KindFor(int seed, int slot) =>
            (ObstacleKind)Pick(seed, 1531 + slot, 4);

        /// <summary>Size multiplier for this slot's obstacle, in [min, max].</summary>
        public static float ScaleFor(int seed, int slot, float min, float max)
        {
            if (max <= min) return min;
            float t = Hash(seed, 2417 + slot) / (float)uint.MaxValue;
            return min + (max - min) * t;
        }

        /// <summary>Y rotation in degrees so identical props don't line up like a shop display.</summary>
        public static float YawFor(int seed, int slot) => Hash(seed, 3313 + slot) / (float)uint.MaxValue * 360f;
    }
}
