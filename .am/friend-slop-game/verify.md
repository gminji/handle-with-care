---
type: verify
slug: friend-slop-game
date: 2026-06-24
verdict: PASS
detail: PASS-WITH-WARNINGS (no blocking issues)
method: static (no Unity toolchain in environment)
aspects: [compile-coherence, ngo-api, unity-api, test-math]
---

# Verification — SLOP CO. vertical slice

**Verdict: PASS** (PASS-WITH-WARNINGS; zero compile-blocking or runtime-breaking issues across all
four static-audit lanes, each of which READ the actual generated files).

## Aspect results
| Aspect | Verdict | Summary |
|--------|---------|---------|
| Compile-coherence | pass-with-warnings | All 30 source/test files read; every cross-file symbol, namespace, signature, enum, `using`, asmdef ref, and access modifier checked — all resolve. |
| NGO 2.x API | pass-with-warnings | Every NGO 2.2 API confirmed correct: NetworkVariable ctor+perms, `[Rpc]`+`Rpc` suffix, `SpawnAsPlayerObject` instance call, static `InstantiateAndSpawn`, ConnectionApproval shape, `NetworkObjectReference.TryGet`/implicit conversion, lifecycle, `ClientNetworkTransform.OnIsServerAuthoritative`. |
| Unity 6 API | pass | Input System code-defined actions, `Rigidbody.linearVelocity`, CharacterController, URP `_BaseColor` MaterialPropertyBlock, legacy uGUI (no TMP dependency), manifest packages + asmdef refs — all valid for 6000.0.x. |
| Test math | pass | Every `[Test]` expected value recomputed by hand (incl. `(double)1.4f` float nuance) and confirmed correct; tests touch only pure-math statics; EditMode asmdef valid. |

## Fixed during verify
- Removed 5 **dead duplicate** constants from `GameConstants.cs` (CarryHoldHeight, ImpactDamageMin,
  ImpactDamageScale, QuotaEscalationFactor, QuotaEscalationFlat) — drift risk vs the live values in
  the pure-math `CargoMath`/`QuotaMath` (the single source of truth). Verified non-referenced, so safe.

## Non-blocking warnings (carried, no action required for the slice)
- `UnityTransport` resolves inside `Unity.Netcode.Runtime` for NGO 2.2; if a future NGO splits UTP into
  its own assembly, add that asmdef reference. (No action for 2.2.0.)
- Keep the project on **URP** (tinting uses `_BaseColor`; Built-in's `_Color` would silently no-op).
- Legacy `UnityEngine.UI.Text` shows a soft deprecation nudge in newer Editor UX but compiles/runs fine.
- Cargo prefab MUST carry `NetworkRigidbody` (`UseRigidBodyForMotion=true`) for the server PD physics to
  replicate — a prefab-wiring requirement (documented in README), not a code issue.

## Residual risk (honest caveat)
Static verification confirms the code **will compile and the APIs are used correctly**, but it cannot
prove runtime behavior or the **Fun-Risk Register** items (co-carry feel, voice comedy, throw feel,
4-player readability). Those require opening the project in Unity 6 and playtesting — start by tuning
the co-carry PD controller (the named primary success criterion).
