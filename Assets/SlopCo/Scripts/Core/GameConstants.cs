namespace SlopCo.Core
{
    /// <summary>
    /// Central tuning + identity constants. Designers tweak numbers here; gameplay code references
    /// these instead of magic numbers. Pure data — no UnityEngine dependency beyond primitives.
    /// </summary>
    public static class GameConstants
    {
        // ── Session ─────────────────────────────────────────────
        public const int MaxPlayers = 4;
        public const ushort DefaultPort = 7777;
        public const string DefaultJoinCode = "127.0.0.1";

        // ── Tags / Layers (must match Project Settings) ─────────
        public const string Tag_DeliveryZone = "DeliveryZone";
        public const string Tag_Cargo = "Cargo";
        public const string Tag_Player = "Player";

        // ── Player movement ─────────────────────────────────────
        public const float PlayerMoveSpeed = 4.5f;
        public const float PlayerCarrySpeedMultiplier = 0.6f; // hauling is slower (comedy + tension)
        public const float PlayerJumpSpeed = 5.0f;
        public const float Gravity = -18f;
        public const float PlayAreaRadius = 80f; // soft owner-side bounds for the slice

        // ── Animation thresholds ────────────────────────────────
        public const float AnimWalkThreshold = 0.15f;
        public const float AnimRunThreshold = 3.2f;

        // ── Carry / co-carry (PD force controller, server-side) ──
        public const float CarryGrabRadius = 1.6f;        // generous server grab tolerance
        public const float CarryPD_Spring = 600f;         // kp
        public const float CarryPD_Damper = 60f;          // kd (tuned toward critical damping)
        public const float CarryMaxForce = 4000f;         // clamp to prevent oscillation/launch
        public const float CarryAlignTorque = 35f;        // mild upright/align torque
        // Single grabber on a TwoPerson item: weak partial drag (staggering = comedy).
        public const float UnderCrewedLiftStrength = 0.28f;

        // ── Throw ───────────────────────────────────────────────
        public const float ThrowMinImpulse = 4f;
        public const float ThrowMaxImpulse = 12f;
        public const float ThrowUpwardBias = 0.35f;

        // ── Cargo condition / depreciation ──────────────────────
        // NOTE: the impact→damage curve constants live in CargoMath (pure, unit-tested) — the single
        // source of truth — so they are NOT duplicated here.
        public const float DefaultCargoToughness = 1.0f;
        public const float BigSmashImpulse = 9f;     // threshold for screen-shake / hit-stop FX

        // ── Round / quota ───────────────────────────────────────
        public const float BriefingSeconds = 3f;
        public const float HaulSeconds = 180f;       // ~3 min rounds
        public const int StartingQuota = 150;
        // NOTE: quota escalation constants live in QuotaMath (pure, unit-tested) — the single source.
        public const int CargoPerRound = 6;
    }
}
