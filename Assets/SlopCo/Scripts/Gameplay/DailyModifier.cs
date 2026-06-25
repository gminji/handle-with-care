namespace SlopCo.Gameplay
{
    /// <summary>Today's cargo twist. None = a clean day (day 1); the rest are the rolled daily modifiers.</summary>
    public enum DayModifier : byte { None = 0, RushHour = 1, DoubleLoad = 2, HazardPay = 3 }

    /// <summary>
    /// Pure "today's modifier" policy — NO UnityEngine / NGO dependency, so it is EditMode-testable like
    /// <see cref="RunGrade"/> / <c>ShareText</c> / <c>MusicBus</c>. The modifier is a deterministic function
    /// of the already-replicated day number, so there is NO extra netcode: the server applies the effects
    /// authoritatively (spawn / payout) and clients compute the same roll only to show the briefing banner.
    /// Day 1 is always clean (a friendly first impression); day 2+ rolls one of three twists.
    /// </summary>
    public static class DailyModifier
    {
        static readonly DayModifier[] Active = { DayModifier.RushHour, DayModifier.DoubleLoad, DayModifier.HazardPay };

        /// <summary>Deterministic daily roll from the day number. Uses the lowbias32 integer finalizer for a
        /// well-mixed hash, then Lemire's multiply-shift (high bits) to land an even index across the three —
        /// plain low-bit `% 3` on a hashed small int is degenerate, so it is deliberately avoided.</summary>
        public static DayModifier Roll(int day)
        {
            if (day <= 1) return DayModifier.None;
            uint h = (uint)day;
            h ^= h >> 16; h *= 0x7feb352dU; h ^= h >> 15; h *= 0x846ca68bU; h ^= h >> 16; // lowbias32 finalizer
            int idx = (int)(((ulong)h * (ulong)Active.Length) >> 32);                       // uniform [0, Active.Length)
            return Active[idx];
        }

        /// <summary>Fuse-burn multiplier (Rush Hour / Hazard Pay shorten the fuse).</summary>
        public static float FuseMult(DayModifier m) =>
            m == DayModifier.RushHour ? 1.4f : m == DayModifier.HazardPay ? 1.2f : 1f;

        /// <summary>Extra bombs this day (Double Load = +1; co-op only, applied by the spawner).</summary>
        public static int CountBonus(DayModifier m) => m == DayModifier.DoubleLoad ? 1 : 0;

        /// <summary>Delivery payout multiplier (Hazard Pay pays richer; Rush Hour a small bump for the rush).</summary>
        public static float PayoutMult(DayModifier m) =>
            m == DayModifier.HazardPay ? 1.5f : m == DayModifier.RushHour ? 1.1f : 1f;

        /// <summary>Localization key for the briefing banner ("RUSH HOUR — short fuse!" etc.).</summary>
        public static string NameKey(DayModifier m) =>
            m == DayModifier.RushHour   ? "mod.rushhour"   :
            m == DayModifier.DoubleLoad ? "mod.doubleload" :
            m == DayModifier.HazardPay  ? "mod.hazardpay"  : "mod.none";
    }
}
