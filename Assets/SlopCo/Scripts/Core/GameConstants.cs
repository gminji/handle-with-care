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

        // ── Kick (player-vs-player shove + hazard repellent) ────
        // Resolved server-side with KickMath, applied by the victim's owner (same reason as the blast
        // shove above: a CharacterController can't be pushed from the server).
        public const float KickRange       = 2.6f;   // reach of the boot (planar)
        public const float KickHalfAngle   = 65f;    // ± arc around the kicker's facing
        public const float KickSpeed       = 11f;    // peak launch speed (units/sec) at point-blank
        public const float KickPopUp       = 3.0f;   // vertical pop so the victim actually leaves the ground
        public const float KickCooldown    = 0.7f;   // seconds between kicks (validated on the server too)
        public const float KickShakeVictim = 0.45f;  // screen shake on the player who got booted
        public const float KickShakeKicker = 0.12f;  // lighter feedback for the kicker

        // ── UFO hazard (abduct → drop) ───────────────────────────
        // The saucer is server-driven, but the abduction itself runs on the VICTIM'S owner (same
        // CharacterController constraint as the kick/blast shove). Timing lives in AbductionMath.
        public const float UfoCruiseHeight    = 11f;   // hover altitude while hunting
        public const float UfoMoveSpeed       = 7f;    // horizontal chase speed
        public const float UfoCatchRadius     = 3.2f;  // planar distance at which the beam locks on
        public const float UfoLiftSeconds     = 1.6f;  // beam-up duration
        public const float UfoHoldSeconds     = 1.4f;  // dangling under the saucer before the drop
        public const float UfoCarryGap        = 2.6f;  // how far below the hull the victim hangs
        public const float UfoLeaveSeconds    = 3f;    // climb-out time after a drop (or a kick)
        public const float UfoHuntSeconds     = 14f;   // give up and leave if nobody gets caught
        // Landing cost: short hops are free, a full sky-drop nearly empties the gauge.
        public const float FallFreeHeight     = 2.5f;
        public const float FallStaminaPerMetre= 0.085f;
        public const float FallStaminaMax     = 0.9f;

        // ── Thief hazard (snatches cargo you dropped) ────────────
        public const float ThiefLoiterSpeed   = 2.2f;   // idle circling near the cargo it is eyeing
        public const float ThiefSprintSpeed   = 7.5f;   // the dash once something hits the floor
        public const float ThiefLoiterRadius  = 5f;     // how far out it circles while waiting
        public const float ThiefGrabRange     = 1.4f;   // close enough to snatch
        public const float ThiefRestSpeed     = 0.9f;   // cargo slower than this counts as "landed"
        public const float ThiefSettleSeconds = 0.35f;  // …and it must stay that slow this long
        public const float ThiefGiveUpSeconds = 26f;    // nothing dropped? wander off
        public const float ThiefCarryHeight   = 1.1f;   // where the loot rides while it runs

        // ── Disconnect vote (a player dropped mid-run) ───────────
        public const float DisconnectVoteSeconds = 20f;   // answer window; no answer = carry on

        // ── Hazard director (spawns the roaming hazards during a haul) ──
        public const float HazardFirstDelay   = 18f;   // grace before the first hazard of the day
        public const float HazardInterval     = 30f;   // seconds between hazard spawns
        public const int   HazardMaxAlive     = 2;     // concurrent roaming hazards

        // ── Carry / co-carry (PD force controller, server-side) ──
        public const float CarryGrabRadius = 1.6f;        // generous server grab tolerance
        // Authoritative anti-teleport bound on RequestGrabRpc, as a multiple of CarryGrabRadius.
        // TryGrabNearby's radius test runs owner-side only, so the server must not take "I am next to it"
        // on trust — otherwise one crafted RPC claims a free handle on ANY spawned cargo from anywhere and
        // drags it across the level via the PD drive. Deliberately loose: it exists to reject teleporting,
        // NOT to police gameplay, so a false reject (a real grab refused under lag) is far worse than a
        // griefer having to stand 5 u away instead of 30.
        // Budget, measured to the cargo's ORIGIN because that is what the check compares. OverlapSphere
        // can hit a collider CORNER, so the bound is the box half-diagonal, not the half-width:
        //   Widest prefab is Cargo_Bed (BoxCollider 1.9545 x 2.3) -> half-diagonal 1.509 u. It never
        //   scales; only CargoBomb takes the 1.25x Heavy multiplier, and even scaled the bomb is 1.06 u.
        //   Legitimate worst case = 1.6 reach + 1.509 = 3.11 u.
        //   3.2 x 1.6 = 5.12 u leaves ~2.0 u of headroom ~= 450 ms of run at PlayerMoveSpeed for the
        //   server's view of the grabber to lag the owner's. Course is ~28 u (LEVEL_DESIGN.md), so the
        //   cross-map claim this exists to stop is rejected by a factor of 5+.
        //   All cargo colliders are centred at x=0,z=0 (y offset only), so the origin is a sound proxy.
        public const float ServerGrabRangeSlack = 3.2f;
        public const float CarryPD_Spring = 600f;         // kp
        public const float CarryPD_Damper = 60f;          // kd (tuned toward critical damping)
        public const float CarryMaxForce = 4000f;         // clamp to prevent oscillation/launch
        public const float CarryAlignTorque = 35f;        // mild upright/align torque
        // Single grabber on a TwoPerson item: the LOW ANCHOR of the continuous lift ramp in CoCarryMath
        // (was a binary 0.28 / 1.0 switch). The first grabber still gets exactly 0.28 — every grabber
        // after that ramps linearly up to 1.0 at full crew, so a forced drop mid-haul decelerates
        // instead of falling off a cliff.
        public const float UnderCrewedLiftStrength = 0.28f;

        // ── Crew-scaled co-carry (heavier with a bigger crew, faster with more hands on it) ──
        // Load is measured in PERSON-UNITS. A TwoPerson item is "2 people heavy" in a 2-player lobby and
        // gains CoCarryLoadPerExtraPlayer per live crew member beyond that, capped by the handle count.
        // A load of 1 person-unit (solo mode, OneHand/Volatile cargo, last-player-standing) is pinned to the
        // flat legacy PlayerCarrySpeedMultiplier — a one-person load has no co-carry economy to scale.
        // NOTE: mass is deliberately NOT scaled. Nothing in the codebase reads Rigidbody.mass
        // (rg '\.mass\b' Assets/ -> 0 hits), so a scaled mass would be unobservable; "heavier" is
        // expressed as carry SPEED and REQUIRED HANDS instead.
        public const float CoCarryLoadPerExtraPlayer = 0.65f;  // +0.65 person-units of load per crew member past baseCrew
        public const float CoCarryFullGripSpeed      = 0.72f;  // carry-speed factor when hands exactly match the load (r = 1)
        public const float CoCarryUnderCrewedFloor   = 0.30f;  // carry-speed factor as r -> 0
        public const float CoCarryOverCrewBonus      = 0.15f;  // extra factor per +1.0 of r above 1
        public const float CoCarryHaulRampPerSecond  = 1.2f;   // how fast the replicated factor slews to a new target (~0.45s full swing)
        // Absolute ceiling applied ONCE at the composition site (PlayerController), AFTER augments:
        // AugmentSystem.CarrySpeedMult has a lower bound only and can reach 2.15 (Light + Forklift + Mule).
        public const float CoCarrySpeedMultMax       = 0.9f;   // hauling is never faster than unencumbered walking

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
        // Online only: the crew VOTES on the augment. The ballot closes this many seconds before the Payout
        // window ends so the reveal animation plays while the shop panel is still up.
        public const float VoteRevealSeconds = 3.2f;
        public const float VoteSpinSeconds   = 1.7f;   // roulette sweep before it settles on the winner

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
