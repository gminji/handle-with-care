---
type: implement-result
slug: friend-slop-game
date: 2026-06-24
status: success
files_created: 36
method: coherent-authoring (API-contract-first; not naive parallel)
review_corrections_applied: true
---

# Implementation Result — SLOP CO. vertical slice

**Status: success.** All 36 files from design.md `change_files` created. Authored directly against the
pinned Public API Surface (design §9) so cross-file symbols cohere — heeding the panel's process
correction over naive parallelization.

## What was built
- **Scaffold/config (5):** `.gitignore`, `Packages/manifest.json` (NGO 2.2, Input System, URP 17,
  uGUI, Test Framework), `ProjectSettings/ProjectVersion.txt` (Unity 6000.0.x), `README.md`,
  `ASSET_MANIFEST.md`. Plus `SlopCo.asmdef` + test asmdef.
- **Core (3):** GameConstants, ServiceLocator, GameBootstrap (host-leaves → lobby).
- **Networking (6):** INetworkSession + LocalNetworkSession (default, no Steam) + SteamNetworkSession
  (`#if USE_STEAM`, compiles both ways), NetworkSessionManager, ConnectionApprovalHandler
  (`CreatePlayerObject=false`), PlayerSpawner (`netObj.SpawnAsPlayerObject` + color assign).
- **Player (5):** ClientNetworkTransform (owner auth), PlayerInputReader (code-defined actions),
  PlayerController (CharacterController + camera + color tint), PlayerAnimator (local velocity-derived),
  PlayerCarryController (grab/drop/throw RPCs, null-safe HeldCargo).
- **Cargo (4):** CargoMath (pure), CarryHandle, CargoCondition (depreciation + FX event), CargoItem
  (server PD co-carry drive in FixedUpdate, sequenced throw, collision→damage, `linearVelocity`).
- **Gameplay (5):** QuotaMath (pure), QuotaSystem, RoundManager (Lobby→Briefing→Hauling→Payout→
  GameOver), CargoSpawner (`NetworkObject.InstantiateAndSpawn`), DeliveryZone (payout = value×speedBonus).
- **UI/Audio (3):** HudController (polled state + "-$$$/+$$$" popups + big-smash flash), LobbyUI,
  ProximityVoiceBridge (IVoiceProvider + NullVoiceProvider + Dissonance wiring guide).
- **Tests (2):** QuotaSystemTests, CargoConditionTests against pure math (no NGO context).

## All review must-fixes applied
SpawnAsPlayerObject instance pattern ✓ · CreatePlayerObject=false ✓ · static
NetworkObject.InstantiateAndSpawn ✓ · ClientNetworkTransform owner-auth ✓ · server-only PD co-carry
in FixedUpdate (no raw AddForce in RPC) ✓ · sequenced throw ✓ · enum defaults = 0 ✓ ·
explicit server-write NetworkVariable perms ✓ · null-safe NetworkObjectReference.TryGet ✓ · pure-math
extraction for tests ✓ · code-defined Input (no .inputactions binary) ✓ · color tint + big-smash flash ✓.

## Known limitations (carried to verify)
- **Not machine-compiled** — no Unity toolchain in this environment. Verification is static.
- Binary assets (scenes/prefabs/URP/SO instances/Kenney FBX) are dev-side per README.
- Voice deferred (Fun-Risk Register) — slice is silent.
- Co-carry/throw *feel*, voice comedy, and 4-player readability are runtime-tune items, not proven here.
