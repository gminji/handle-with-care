---
type: review
slug: friend-slop-game
date: 2026-06-24
mode: auto
rounds: 1
panel: [ngo-correctness, netcode-physics, scope-feasibility, design-fun]
verdict: approve-with-changes
---

# Design Review — SLOP CO. vertical slice (expert panel, --auto)

**Overall verdict: APPROVE-WITH-CHANGES.** All four lenses approved the architecture as sound
(server-authoritative cargo via NetworkRigidbody is the correct answer to the #1 risk; clean
authority split; bandwidth-conscious animation; honest scope framing). No lens blocked. The
following changes are **mandatory** and applied to design.md before implementation.

## Must-fix (blocking before implement)
1. **`SpawnAsPlayerObject`** is an instance method on `NetworkObject`, not static / not on
   `NetworkManager`. Pattern: server `Instantiate(playerPrefab)` → `netObj.SpawnAsPlayerObject(clientId)`.
   Set `response.CreatePlayerObject = false` in connection approval to avoid double-spawn.
2. **Co-carry mechanism must be pinned to ONE model.** Chosen: **server-side critically-damped PD
   force controller** (`F = clamp(kp·(target−pos) − kd·vel, ±maxForce)`) applied in `FixedUpdate`
   on the server only. No raw `AddForce` from Rpc callbacks. Single grabber on a 2-person item gets a
   weak partial drag (staggering = comedy); full drive only when grabbers ≥ required.
3. **All cargo physics mutations run server-only (`IsServer`) in `FixedUpdate`**, never in the Rpc
   callback frame. Throw order: clear drive → next FixedUpdate apply impulse once → set Loose state.
4. **Owner-authoritative player movement** via `ClientNetworkTransform : NetworkTransform`
   (`OnIsServerAuthoritative() => false`). Bounds enforced soft, owner-side (no per-frame server
   overwrite of an owner-auth transform — that snaps/jitters). Player has NO NetworkRigidbody.
5. **Cargo prefab component trio:** `Rigidbody` + `NetworkTransform` (Server authority) +
   `NetworkRigidbody` (`UseRigidBodyForMotion = true`). Documented in README + manifest.

## API corrections (applied)
- `NetworkObject.InstantiateAndSpawn(GameObject prefab, NetworkManager nm, …)` (static) for cargo.
- `ConnectionApprovalCallback` = `Action<ConnectionApprovalRequest, ConnectionApprovalResponse>`;
  set `Approved / CreatePlayerObject=false / Pending=false / Reason`.
- `[Rpc(SendTo.*)]` + mandatory `…Rpc` suffix — already correct.
- `NetworkVariable<T>`: float/int/enum/`NetworkObjectReference` all valid. Construct server-write
  explicitly. Enum defaults to 0 → ensure `Phase.Lobby=0`, `CarryState.Loose=0`, `PlayerAnimState.Idle=0`.
- `NetworkObjectReference.TryGet` may fail on clients pre-spawn → null-check everywhere; clear to
  `default` on drop/despawn.

## Should-fix (applied where cheap)
- **EditMode tests must not instantiate NetworkBehaviours** → pure math extracted to `QuotaMath`,
  `CargoMath` static classes (no NGO references).
- **Input System** → code-defined `InputAction`s (no `.inputactions` binary dependency).
- **Spectator readability** → per-player `NetworkVariable<int> ColorIndex` (server-assigned) tinting
  the Kenney material; impact hit-stop/screen-shake on large `PlayDamageFxRpc`.
- **Throw promoted to first-class verb** (charge→arc→impulse; landing applies impact damage → ties to
  depreciation).
- **Tick rate** target 50–60 Hz; `Rigidbody.interpolation = Interpolate` on cargo.

## Honesty correction (applied)
- Scope contract softened: C# is **authored to compile / statically reviewed for NGO 2.x API
  correctness — NOT machine-verified** (no Unity toolchain in this environment).
- Added a **Fun-Risk Register**: the four viral pillars (voice-cutoff comedy, co-carry feel, slapstick
  throw, 4-player scrum readability) are **not** validated by a compile-only deliverable; only their
  scaffolding compiles. Voice is an integration point (Dissonance), not implemented in the slice.

## Process correction (applied)
- Do **not** blindly parallelize tightly-coupled C# with no compiler to catch signature drift. A
  **Public API Surface** contract (owning file + exact signature for every cross-namespace symbol) is
  added to design.md and is authored as the single source of truth; core coupled scripts are written
  coherently against it.
