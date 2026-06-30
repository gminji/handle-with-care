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

        // ── Matchmaking (LAN UDP quick-match discovery) ─────────
        public const ushort DiscoveryPort = 7778;             // UDP beacon/discovery port (≠ DefaultPort)
        public const string MatchGameId = "slopco";           // beacon guard so unrelated apps are ignored
        public const byte MatchBeaconVersion = 1;             // bump on wire-format change → old beacons rejected
        public const float BeaconIntervalSeconds = 1f;        // host broadcast cadence while in lobby
        public const float DiscoveryTimeoutSeconds = 3f;      // how long a quick-match scan listens
        public const float MatchRescanJitterSeconds = 0.75f;  // random extra wait + 1 rescan when 0 found (race relief)
        public const float JoinConfirmSeconds = 2f;           // wait for IsConnectedClient before declaring join OK
        public const float NetworkIdleTimeoutSeconds = 3f;    // wait for NGO Shutdown to finish before re-Start

        // ── Voice chat (custom NGO proximity voice) ─────────────
        // Fixed wire format: mono PCM16 @ 16 kHz. 20 ms frame = 320 samples = 640 bytes, which stays
        // inside a single unreliable-RPC MTU (no fragmentation) — do NOT batch frames past this.
        public const int   VoiceSampleRate    = 16000;          // Hz, fixed wire rate
        public const int   VoiceFrameSamples  = 320;            // 20 ms @ 16 kHz
        public const float VoiceActivityRms   = 0.012f;         // VAD gate — below this we send nothing
        public const int   VoiceRingSeconds   = 1;              // playback ring buffer span per remote
        public const float VoiceMaxDistance   = 25f;            // 3D AudioSource max audible range (proximity)
        public const float VoiceMinDistance   = 2f;             // full-volume radius before rolloff begins
        public const float VoiceDefaultVolume = 0.8f;           // default voice master (SettingsManager)

        // ── Voice activity indicator (head billboard; presentation only, netcode-free) ──
        public const float VoiceIndicatorWindow     = 0.35f;    // debounce: keep showing this long after the last voiced frame (absorbs inter-syllable gaps)
        public const float VoiceIndicatorHeight     = 2.6f;     // billboard height above the player (just above the 2.2 camera look point)
        public const float VoiceIndicatorBaseScale  = 0.18f;    // TextMesh characterSize baseline (near FloatingNumber's 0.14)
        public const float VoiceIndicatorPulseAmp   = 0.25f;    // pulse amplitude (±) while speaking
        public const float VoiceIndicatorPulseSpeed = 12f;      // pulse rate (radians/sec → ~1.9 beats/sec)

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

        // ── Dash (hold-to-sprint stamina; owner-local, NetworkTransform replicates the result) ──
        public const float DashSpeedMultiplier = 1.8f;   // sprint speed factor while dashing
        public const float DashDrainPerSecond  = 0.5f;   // gauge(0..1) drained per sec dashing → ~2s full sprint
        public const float DashRegenPerSecond  = 0.35f;  // gauge refilled per sec when not dashing → ~2.9s recharge
        public const float DashExhaustSeconds  = 1.0f;   // forced-stop duration on full depletion

        // ── Items (drop + inventory) ──
        public const float ItemDropIntervalSeconds = 25f;  // gacha-capsule drop cadence during Hauling
        public const float ItemDropBeyondDistance  = 14f;  // how far past the delivery van a capsule lands (intentional detour)
        public const float ItemDropAngleJitter     = 0.6f; // ± radians random spread around the van's outward facing

        // ── Animation thresholds ────────────────────────────────
        public const float AnimWalkThreshold = 0.15f;
        public const float AnimRunThreshold = 3.2f;

        // ── Player knockback (explosion game-feel) ──────────────
        // Players are CharacterControllers, so AddExplosionForce can't fling them — the owner injects this
        // shove locally on CargoBomb.OnDetonated (NetworkTransform replicates the result; no server RPC).
        public const float BlastKnockbackSpeed  = 14f;  // peak horizontal shove at the blast center (units/sec)
        public const float BlastKnockbackRadius = 9f;   // a bit wider than the bomb blast so near-misses stagger
        public const float BlastKnockbackPopUp  = 4.5f; // vertical pop so bodies actually leave the ground
        public const float BlastKnockbackDecay  = 22f;  // how fast the shove bleeds off (units/sec per sec)

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

        // ── Bomb (the hook) ─────────────────────────────────────
        // Post-spawn grace: the fuse is paused and the bomb cannot detonate for this long after it
        // appears, so a fresh/thrown/dropped bomb never explodes before the player can react.
        public const float BombArmingSeconds = 2.5f;

        // ── Augments (roguelite shop) ───────────────────────────
        public const float PayoutWindowSeconds = 9f;  // extended Payout so the between-round shop is browsable
        public const int AugmentShopChoices = 3;       // cards offered each visit

        // ── Delivery combo (the chase) ──────────────────────────
        public const float ComboWindowSeconds = 6f;   // deliver again within this to keep the chain alive
        public const float ComboPayoutStep = 0.25f;    // +25% payout per chain level above 1
        public const float ComboMaxMult = 3f;          // payout multiplier ceiling

        // ── Ping / Emote wheel (co-op comms; reliable RPC relay, presentation-only display) ──
        public const float PingEmoteCooldown   = 0.8f;  // server-side per-player min interval between sends (anti-spam)
        public const float PingMarkerSeconds   = 4.0f;  // world ping marker lifetime before it fades out
        public const float PingMarkerHeight    = 1.2f;  // marker hover height above the ray hit point
        public const float PingMarkerScale     = 0.22f; // marker TextMesh characterSize (a touch over FloatingNumber 0.14)
        public const float EmoteBubbleSeconds  = 2.5f;  // head emote bubble lifetime
        public const float EmoteBubbleHeight   = 3.0f;  // bubble height above player origin (above the voice ♪ at 2.6)
        public const float EmoteBubbleScale    = 0.20f; // bubble TextMesh characterSize
        public const float WheelRadiusPixels   = 150f;  // slice-label ring radius on the runtime overlay
        public const float WheelInnerDeadzone  = 0.25f; // normalized cursor/stick radius below which release = cancel
        public const float PingAimMaxDistance  = 120f;  // max camera→cursor raycast distance for ping placement
    }
}
