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

        /// <summary>Which entry of a map's obstacle list sits in this slot, in [0, count). A salt of its own, so it
        /// is independent of <see cref="KindFor"/> — maps without a list keep exactly the field they always had.</summary>
        public static int EntryFor(int seed, int slot, int count) => Pick(seed, 4129 + slot, count);

        /// <summary>Y rotation in degrees so identical props don't line up like a shop display.</summary>
        public static float YawFor(int seed, int slot) => Hash(seed, 3313 + slot) / (float)uint.MaxValue * 360f;

        /// <summary>
        /// The host's seed pick: roll until <paramref name="deliverable"/> accepts a seed, at most
        /// <paramref name="maxTries"/> times (at least once). Slots fill independently, so now and then two
        /// neighbours close the only lane between them; the host checks each candidate and only the accepted
        /// seed is ever replicated, so clients still rebuild the field from one int. If every try fails the last
        /// seed is kept — a run that might be blocked beats a run that never starts.
        /// Returns whether <paramref name="seed"/> was accepted; <paramref name="tries"/> is how many were rolled.
        /// </summary>
        public static bool TryPickSeed(System.Func<int> roll, System.Func<int, bool> deliverable, int maxTries,
                                       out int seed, out int tries)
        {
            if (maxTries < 1) maxTries = 1;
            seed = 0;
            for (tries = 1; tries <= maxTries; tries++)
            {
                seed = roll();
                if (deliverable(seed)) return true;
            }
            tries = maxTries;
            return false;
        }
    }
}
