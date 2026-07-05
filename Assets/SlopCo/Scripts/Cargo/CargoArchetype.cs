using UnityEngine;

namespace SlopCo.Cargo
{
    /// <summary>Per-cargo archetype, rolled per bomb within a round — gives a single round a MIX of carry-load,
    /// fuse length and surface friction instead of N identical bombs. Standard is a pure no-op (keeps the prefab's
    /// shipped mass; the bomb prefab is TwoPerson in co-op).</summary>
    public enum CargoArchetype : byte { Standard = 0, Volatile = 1, Slippery = 2, Heavy = 3 }

    /// <summary>
    /// Pure archetype policy — same shape as <see cref="SlopCo.Gameplay.DailyModifier"/> (RunGrade / MusicBus):
    /// a deterministic function of (day, index), so there is NO extra netcode for the roll. The server applies the
    /// effects authoritatively (fuse / friction / mass) and clients get the visual via one replicated byte. Only
    /// <see cref="TintColor"/> touches a UnityEngine type; the rest is engine-agnostic and EditMode-testable.
    /// Effects: Volatile = short fuse + single-hand (grab-and-sprint panic); Slippery = near-frictionless skid;
    /// Heavy = telegraphed two-person + bigger; Standard = untouched default.
    /// </summary>
    public static class CargoArchetypeTable
    {
        // Day 1 is always Standard (a friendly first impression, like DailyModifier day 1 = None). Day 2+ rolls a
        // weighted pick that escalates toward harder archetypes over surviving days.
        public static CargoArchetype Roll(int day, int index)
        {
            if (day <= 1) return CargoArchetype.Standard;

            // Mix (day, index) then run the lowbias32 finalizer (the same well-distributed hash DailyModifier uses).
            uint h = (uint)(day * 73856093) ^ (uint)(index * 19349663);
            h ^= h >> 16; h *= 0x7feb352dU; h ^= h >> 15; h *= 0x846ca68bU; h ^= h >> 16;
            float r = (h >> 8) * (1f / (1 << 24)); // uniform [0,1)

            float t = Mathf.Clamp01((day - 2) / 8f); // fully escalated by ~day 10
            float wStd = Mathf.Lerp(0.70f, 0.30f, t);
            float wVol = Mathf.Lerp(0.12f, 0.25f, t);
            float wSlp = Mathf.Lerp(0.10f, 0.22f, t);
            float wHvy = Mathf.Lerp(0.08f, 0.23f, t);
            float sum = wStd + wVol + wSlp + wHvy; // always > 0 — no div-by-zero
            r *= sum;
            if (r < wStd) return CargoArchetype.Standard;
            if (r < wStd + wVol) return CargoArchetype.Volatile;
            if (r < wStd + wVol + wSlp) return CargoArchetype.Slippery;
            return CargoArchetype.Heavy;
        }

        /// <summary>Fuse-burn multiplier (Volatile burns faster = shorter fuse; DailyModifier-compatible: &gt;1 = shorter).</summary>
        public static float FuseMult(CargoArchetype a) =>
            a == CargoArchetype.Volatile ? 1.6f : 1f;

        /// <summary>Cargo surface-friction multiplier (Slippery = near-frictionless skid). 1 = default grip.
        /// Combined with the day modifier via MIN at the spawner so effects don't double-slip.</summary>
        public static float FrictionMult(CargoArchetype a) =>
            a == CargoArchetype.Slippery ? 0.10f : 1f;

        /// <summary>Mass-class override, or null to INHERIT the prefab's shipped mass. Standard/Slippery inherit (the
        /// bomb prefab is TwoPerson) so they never regress co-op; only Volatile down-shifts to single-hand
        /// (grab-and-run) and Heavy pins two-person explicitly.</summary>
        public static CargoMassClass? MassOverride(CargoArchetype a) =>
            a == CargoArchetype.Volatile ? CargoMassClass.OneHand :
            a == CargoArchetype.Heavy    ? CargoMassClass.TwoPerson : (CargoMassClass?)null;

        /// <summary>Uniform scale multiplier (Heavy is visibly bigger; kept under the depot spawn spacing).</summary>
        public static float ScaleMult(CargoArchetype a) =>
            a == CargoArchetype.Heavy ? 1.25f : 1f;

        /// <summary>Base-color tint applied client-side as a telegraph. Standard returns clear (alpha 0) = no tint,
        /// so a normal cargo keeps the prefab material untouched (backward compatible).</summary>
        public static Color TintColor(CargoArchetype a) =>
            a == CargoArchetype.Volatile ? new Color(1f,    0.45f, 0.12f, 1f) : // hot orange
            a == CargoArchetype.Slippery ? new Color(0.25f, 0.85f, 0.95f, 1f) : // cyan
            a == CargoArchetype.Heavy    ? new Color(0.35f, 0.5f,  0.9f,  1f) : // steel blue
                                           new Color(0f, 0f, 0f, 0f);           // Standard = no tint

        /// <summary>Localization key for an optional in-world label ("VOLATILE" etc.).</summary>
        public static string NameKey(CargoArchetype a) =>
            a == CargoArchetype.Volatile ? "cargo.volatile" :
            a == CargoArchetype.Slippery ? "cargo.slippery" :
            a == CargoArchetype.Heavy    ? "cargo.heavy"    : "cargo.standard";
    }
}
