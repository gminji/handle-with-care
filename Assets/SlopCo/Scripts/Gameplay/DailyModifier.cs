namespace SlopCo.Gameplay
{
    /// <summary>Today's cargo twist. None = a clean day (day 1); the rest are the rolled daily modifiers.</summary>
    public enum DayModifier : byte
    {
        None = 0, RushHour = 1, DoubleLoad = 2, HazardPay = 3,
        DemolitionDay = 4, // bigger booms (blast radius/force up)
        ScenicRoute = 5,   // longer fuse but leaner payout (the calm, cheap day)
        PaydayRun = 6,     // rich payout + an extra bomb (co-op): high risk, high reward
        GreasyDay = 7,     // near-frictionless cargo — skids out of your hands / across the floor
        RubberDay = 8,     // bouncy cargo — dropped/thrown crates boing around
    }

    /// <summary>
    /// Pure "today's modifier" policy — NO UnityEngine / NGO dependency, so it is EditMode-testable like
    /// <see cref="RunGrade"/> / <c>ShareText</c> / <c>MusicBus</c>. The modifier is a deterministic function
    /// of the already-replicated day number, so there is NO extra netcode: the server applies the effects
    /// authoritatively (spawn / payout) and clients compute the same roll only to show the briefing banner.
    /// Day 1 is always clean (a friendly first impression); day 2+ rolls one of three twists.
    /// </summary>
    public static class DailyModifier
    {
        static readonly DayModifier[] Active =
        {
            DayModifier.RushHour, DayModifier.DoubleLoad, DayModifier.HazardPay,
            DayModifier.DemolitionDay, DayModifier.ScenicRoute, DayModifier.PaydayRun,
            DayModifier.GreasyDay, DayModifier.RubberDay,
        };

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

        /// <summary>Fuse-burn multiplier (Rush Hour / Hazard Pay shorten the fuse; Scenic Route lengthens it).</summary>
        public static float FuseMult(DayModifier m) =>
            m == DayModifier.RushHour    ? 1.4f :
            m == DayModifier.HazardPay   ? 1.2f :
            m == DayModifier.ScenicRoute ? 0.7f : 1f;

        /// <summary>Extra bombs this day (Double Load / Payday Run = +1; co-op only, applied by the spawner).</summary>
        public static int CountBonus(DayModifier m) =>
            m == DayModifier.DoubleLoad || m == DayModifier.PaydayRun ? 1 : 0;

        /// <summary>Delivery payout multiplier (Payday richest; Hazard Pay richer; Demolition a push-your-luck
        /// bump; Rush Hour a small rush bonus; Scenic Route trades calm for a leaner cut).</summary>
        public static float PayoutMult(DayModifier m) =>
            m == DayModifier.HazardPay     ? 1.5f  :
            m == DayModifier.PaydayRun     ? 2f    :
            m == DayModifier.DemolitionDay ? 1.25f :
            m == DayModifier.RushHour      ? 1.1f  :
            m == DayModifier.ScenicRoute   ? 0.8f  : 1f;

        /// <summary>Detonation blast radius/force multiplier (Demolition Day = bigger booms). Bounded again
        /// at the bomb (Arm clamp) so a runaway value can never break the chain-reaction physics.</summary>
        public static float BlastMult(DayModifier m) =>
            m == DayModifier.DemolitionDay ? 1.6f : 1f;

        /// <summary>Cargo surface-friction multiplier (Greasy Day = near-frictionless → cargo skids out of
        /// grip and slides across the floor: the clip-native "slippery cargo" moment, no art needed).
        /// 1 = default grip. Applied server-side as a runtime PhysicsMaterial by <c>CargoBomb.SetSurface</c>.</summary>
        public static float FrictionMult(DayModifier m) =>
            m == DayModifier.GreasyDay ? 0.05f : 1f;

        /// <summary>Cargo bounciness/restitution (Rubber Day = springy cargo that boings when dropped/thrown).
        /// 0 = inert (no bounce). Applied server-side as a runtime PhysicsMaterial by <c>CargoBomb.SetSurface</c>.</summary>
        public static float Bounciness(DayModifier m) =>
            m == DayModifier.RubberDay ? 0.85f : 0f;

        /// <summary>Localization key for the briefing banner ("RUSH HOUR — short fuse!" etc.).</summary>
        public static string NameKey(DayModifier m) =>
            m == DayModifier.RushHour      ? "mod.rushhour"    :
            m == DayModifier.DoubleLoad    ? "mod.doubleload"  :
            m == DayModifier.HazardPay     ? "mod.hazardpay"   :
            m == DayModifier.DemolitionDay ? "mod.demolition"  :
            m == DayModifier.ScenicRoute   ? "mod.scenicroute" :
            m == DayModifier.PaydayRun     ? "mod.payday"      :
            m == DayModifier.GreasyDay     ? "mod.greasy"      :
            m == DayModifier.RubberDay     ? "mod.rubber"      : "mod.none";
    }
}
