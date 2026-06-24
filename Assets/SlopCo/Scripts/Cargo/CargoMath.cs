using System;

namespace SlopCo.Cargo
{
    /// <summary>
    /// Pure cargo-condition + payout math — NO UnityEngine / NGO dependency, so it is unit-testable in
    /// EditMode without a NetworkManager. CargoCondition / DeliveryZone delegate arithmetic here.
    /// </summary>
    public static class CargoMath
    {
        public const float ImpactDamageMin = 1.5f;
        public const float ImpactDamageScale = 0.025f;

        /// <summary>
        /// Maps a physics impulse magnitude to condition damage in [0,1]. Impulses below
        /// <see cref="ImpactDamageMin"/> are harmless (gentle placement). Tougher cargo takes less.
        /// </summary>
        public static float DamageFromImpact(float impulseMagnitude, float toughness = 1f)
        {
            if (toughness <= 0f) toughness = 1f;
            float over = impulseMagnitude - ImpactDamageMin;
            if (over <= 0f) return 0f;
            float dmg = (over * ImpactDamageScale) / toughness;
            return Clamp01(dmg);
        }

        /// <summary>Apply damage to a condition value, clamped to [0,1].</summary>
        public static float ApplyDamage(float condition01, float damage01)
            => Clamp01(condition01 - Clamp01(damage01));

        /// <summary>Current sellable value = round(baseValue * condition).</summary>
        public static int Value(int baseValue, float condition01)
        {
            if (baseValue < 0) baseValue = 0;
            return (int)Math.Round(baseValue * Clamp01(condition01), MidpointRounding.AwayFromZero);
        }

        /// <summary>Payout at delivery = round(value * (1 + speedBonus)). speedBonus in [0,1].</summary>
        public static int Payout(int value, float speedBonus01)
            => (int)Math.Round(value * (1f + Clamp01(speedBonus01)), MidpointRounding.AwayFromZero);

        /// <summary>Speed bonus from fraction of haul time remaining (more time left = bigger bonus).</summary>
        public static float SpeedBonus(float secondsRemaining, float haulSeconds)
        {
            if (haulSeconds <= 0f) return 0f;
            return Clamp01(secondsRemaining / haulSeconds);
        }

        private static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
    }
}
