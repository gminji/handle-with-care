---
type: design
slug: friend-slop-game
date: 2026-06-24
project: friend-slop
work_type: feat
based_on: analyze.md
engine: Unity 6 (LTS)
scope: vertical-slice
change_files:
  - .gitignore
  - README.md
  - ASSET_MANIFEST.md
  - Packages/manifest.json
  - ProjectSettings/ProjectVersion.txt
  - Assets/SlopCo/SlopCo.asmdef
  - Assets/SlopCo/Scripts/Core/GameBootstrap.cs
  - Assets/SlopCo/Scripts/Core/GameConstants.cs
  - Assets/SlopCo/Scripts/Core/ServiceLocator.cs
  - Assets/SlopCo/Scripts/Networking/INetworkSession.cs
  - Assets/SlopCo/Scripts/Networking/LocalNetworkSession.cs
  - Assets/SlopCo/Scripts/Networking/SteamNetworkSession.cs
  - Assets/SlopCo/Scripts/Networking/NetworkSessionManager.cs
  - Assets/SlopCo/Scripts/Networking/ConnectionApprovalHandler.cs
  - Assets/SlopCo/Scripts/Networking/PlayerSpawner.cs
  - Assets/SlopCo/Scripts/Player/PlayerInputReader.cs
  - Assets/SlopCo/Scripts/Player/PlayerController.cs
  - Assets/SlopCo/Scripts/Player/ClientNetworkTransform.cs
  - Assets/SlopCo/Scripts/Player/PlayerAnimator.cs
  - Assets/SlopCo/Scripts/Player/PlayerCarryController.cs
  - Assets/SlopCo/Scripts/Cargo/CargoItem.cs
  - Assets/SlopCo/Scripts/Cargo/CargoCondition.cs
  - Assets/SlopCo/Scripts/Cargo/CargoMath.cs
  - Assets/SlopCo/Scripts/Cargo/CarryHandle.cs
  - Assets/SlopCo/Scripts/Gameplay/RoundManager.cs
  - Assets/SlopCo/Scripts/Gameplay/QuotaSystem.cs
  - Assets/SlopCo/Scripts/Gameplay/QuotaMath.cs
  - Assets/SlopCo/Scripts/Gameplay/CargoSpawner.cs
  - Assets/SlopCo/Scripts/Gameplay/DeliveryZone.cs
  - Assets/SlopCo/Scripts/UI/HudController.cs
  - Assets/SlopCo/Scripts/UI/LobbyUI.cs
  - Assets/SlopCo/Scripts/Audio/ProximityVoiceBridge.cs
  - Assets/SlopCo/Scripts/Assets/AssetManifest.cs
  - Assets/SlopCo/Scripts/Assets/NetworkPrefabRegistry.cs
  - Assets/SlopCo/Tests/EditMode/SlopCo.Tests.EditMode.asmdef
  - Assets/SlopCo/Tests/EditMode/QuotaSystemTests.cs
  - Assets/SlopCo/Tests/EditMode/CargoConditionTests.cs
file_count: 36
step_count: 9
risk_level: high
review_applied: true
---

# SLOP CO. (codename CARGO) — Vertical Slice Design

> **Scope contract:** This design covers a **compilable, coherent vertical slice** — the project
> scaffold, all core networked gameplay C# systems, an asset manifest, and EditMode test stubs.
> It is **NOT** a shippable game. Binary Unity assets (`.unity` scenes, `.prefab`, imported FBX,
> `.asset` ScriptableObject instances) and the Kenney art are produced/downloaded by a developer in
> the Unity Editor following `README.md`. Every C# script targets **Unity 6 + NGO 2.x** and compiles
> with the Steam SDK **absent** (gated behind `USE_STEAM`).

## 1. Design Overview

**Architecture:** Host-authoritative client-server over Unity Netcode for GameObjects (NGO) 2.x.
The host is both server and a player. 2–4 players. All gameplay truth (cargo physics, condition,
scoring, round state) lives on the **server/host**; clients send input *intents* and render
replicated state.

**Assembly / namespace layout:** one runtime asmdef `SlopCo` (namespace root `SlopCo`), one test
asmdef `SlopCo.Tests.EditMode`. Subdomains map 1:1 to namespaces:

| Namespace | Folder | Responsibility |
|-----------|--------|----------------|
| `SlopCo.Core` | Scripts/Core | Bootstrap, constants, lightweight service locator |
| `SlopCo.Networking` | Scripts/Networking | Session abstraction, connection, spawning |
| `SlopCo.Player` | Scripts/Player | Input, movement, animation, carry behaviour |
| `SlopCo.Cargo` | Scripts/Cargo | Networked physics cargo, condition/depreciation, grab handles |
| `SlopCo.Gameplay` | Scripts/Gameplay | Round state machine, quota, cargo spawning, delivery/scoring |
| `SlopCo.UI` | Scripts/UI | HUD, lobby panel |
| `SlopCo.Audio` | Scripts/Audio | Proximity-voice integration point (Dissonance) |
| `SlopCo.GameAssets` | Scripts/Assets | Asset manifest + network prefab registry (ScriptableObjects) |

**System map (data flow):**
```
LobbyUI ─▶ NetworkSessionManager ─▶ INetworkSession (Local | Steam)
                                          │ starts NGO host/client
ConnectionApprovalHandler ◀──────────────┘
PlayerSpawner (server) ─▶ spawns Player prefab per client
Player: PlayerInputReader ─▶ PlayerController(move) ─▶ PlayerAnimator
                                      └▶ PlayerCarryController ──┐
                                                                ▼
CargoSpawner(server) ─▶ CargoItem(+CargoCondition,+CarryHandle) ◀ grab/co-carry/drop/throw
                                      │ collisions (server) ─▶ CargoCondition depreciates
                                      ▼
DeliveryZone(server) ─▶ payout = condition × speedBonus ─▶ QuotaSystem ─▶ RoundManager
RoundManager (NetworkVariables) ─▶ HudController (all clients render)
ProximityVoiceBridge: positions players for spatial voice
```

## 2. Project Structure

```
friend-slop/
├─ .gitignore                         # Unity-standard ignores (Library/, Temp/, etc.)
├─ README.md                          # Unity version, asset download, Steam enable, how to play
├─ ASSET_MANIFEST.md                  # Human-readable Kenney download + prefab mapping list
├─ Packages/
│  └─ manifest.json                   # UPM dependencies
├─ ProjectSettings/
│  └─ ProjectVersion.txt              # pins Unity 6 (6000.0.x)
└─ Assets/
   └─ SlopCo/
      ├─ SlopCo.asmdef                # runtime assembly (refs Unity.Netcode.Runtime, InputSystem)
      ├─ Scripts/
      │  ├─ Core/        GameBootstrap.cs, GameConstants.cs, ServiceLocator.cs
      │  ├─ Networking/  INetworkSession.cs, LocalNetworkSession.cs, SteamNetworkSession.cs,
      │  │               NetworkSessionManager.cs, ConnectionApprovalHandler.cs, PlayerSpawner.cs
      │  ├─ Player/      PlayerInputReader.cs, PlayerController.cs, PlayerAnimator.cs,
      │  │               PlayerCarryController.cs
      │  ├─ Cargo/       CargoItem.cs, CargoCondition.cs, CarryHandle.cs
      │  ├─ Gameplay/    RoundManager.cs, QuotaSystem.cs, CargoSpawner.cs, DeliveryZone.cs
      │  ├─ UI/          HudController.cs, LobbyUI.cs
      │  ├─ Audio/       ProximityVoiceBridge.cs
      │  └─ Assets/      AssetManifest.cs, NetworkPrefabRegistry.cs
      ├─ Tests/
      │  └─ EditMode/    SlopCo.Tests.EditMode.asmdef, QuotaSystemTests.cs, CargoConditionTests.cs
      ├─ Art/            (gitignored target for downloaded Kenney FBX — created by dev)
      ├─ Prefabs/        (Player.prefab, Cargo_*.prefab — built by dev in Editor)
      ├─ Scenes/         (Bootstrap.unity, Game.unity — built by dev)
      └─ Settings/       (URP assets — created via URP wizard)
```

**`Packages/manifest.json` dependencies (Unity 6 lines):**
- `com.unity.netcode.gameobjects` : `2.2.0`  (NGO 2.x — unified `[Rpc]`, `NetworkRigidbody`)
- `com.unity.inputsystem` : `1.11.2`
- `com.unity.render-pipelines.universal` : `17.0.4`
- `com.unity.cinemachine` : `3.1.2`  (follow camera)
- `com.unity.test-framework` : `1.4.6`
- `com.unity.ugui` : `2.0.0`  (HUD/lobby canvas)
- *(Steam is NOT a UPM package — Facepunch.Steamworks is a DLL the dev drops into `Assets/SlopCo/ThirdParty/` and enables via the `USE_STEAM` scripting define; documented in README.)*

## 3. Core Systems & Data (class-by-class)

### Core
- **`GameConstants.cs`** — static class: `MaxPlayers = 4`, layer/tag name constants
  (`Layer_Player`, `Layer_Cargo`, `Tag_DeliveryZone`), tuning defaults (move speed, carry break
  force, impact→damage curve constants, round time, base quota).
- **`ServiceLocator.cs`** — minimal static registry (`Register<T>/Get<T>`) so UI and gameplay can
  find `RoundManager`/`NetworkSessionManager` without singletons-everywhere. Cleared on scene unload.
- **`GameBootstrap.cs`** (`MonoBehaviour`) — lives in Bootstrap scene; wires `NetworkManager`
  callbacks (`OnServerStarted`, `OnClientConnectedCallback`, `OnClientDisconnectCallback`),
  registers `ConnectionApprovalHandler`, registers services, loads lobby UI.

### Networking
- **`INetworkSession.cs`** — interface decoupling lobby UI from transport:
  `Task<bool> HostAsync(int maxPlayers)`, `Task<bool> JoinAsync(string joinCode)`,
  `void Leave()`, `string LobbyDisplayCode { get; }`, events `OnStarted/OnJoined/OnFailed/OnLeft`.
- **`LocalNetworkSession.cs`** — default impl. Uses `NetworkManager.Singleton` + `UnityTransport`
  (loopback / direct IP) → `StartHost()` / `StartClient()`. Works with **no Steam SDK**. This is the
  path the vertical slice actually runs on.
- **`SteamNetworkSession.cs`** — entire body wrapped in `#if USE_STEAM ... #else (throws NotSupported) #endif`.
  Creates a Steam lobby (`SteamMatchmaking.CreateLobbyAsync`), sets `SteamNetworkingSockets` transport,
  exposes lobby id as join code, handles `GameLobbyJoinRequested` (overlay "Invite Friend"). Structured
  but compiles to a stub when `USE_STEAM` is undefined → project always builds.
- **`NetworkSessionManager.cs`** (`MonoBehaviour`) — chooses impl (`#if USE_STEAM` → Steam else Local),
  exposes `HostGame()/JoinGame()/Leave()`, re-raises session events, registered in `ServiceLocator`.
- **`ConnectionApprovalHandler.cs`** — sets `NetworkManager.ConnectionApprovalCallback`; rejects when
  `ConnectedClientsIds.Count >= MaxPlayers` or round already in progress; returns spawn approval (no
  auto-spawn — `PlayerSpawner` owns spawning).
- **`PlayerSpawner.cs`** (`MonoBehaviour`, server-only logic) — on `OnClientConnectedCallback`
  (server), `Instantiate` player prefab and `playerNetObj.SpawnAsPlayerObject(clientId)`; positions at
  a free depot spawn point; despawn handled by NGO on disconnect.

### Player
- **`PlayerInputReader.cs`** (`MonoBehaviour`) — wraps the generated Input System actions; exposes
  `Vector2 Move`, `bool JumpPressed`, `bool GrabHeld`, `event ThrowPressed`. Owner-only reads.
- **`PlayerController.cs`** (`NetworkBehaviour`) — owner reads input → moves a `CharacterController`
  (or kinematic rigidbody) locally; `NetworkTransform` replicates pose to others (client-authoritative
  movement for responsiveness, server-validated bounds). Exposes `Velocity` for the animator. Camera
  rig (Cinemachine) enabled only for `IsOwner`.
- **`PlayerAnimator.cs`** (`NetworkBehaviour`) — reads replicated `Velocity` + a small
  `NetworkVariable<PlayerAnimState>` (Idle/Walk/Run/Jump/Carry) and drives the Kenney Mini Characters
  Animator parameters (`Speed`, `IsCarrying`, `JumpTrigger`). Works on all clients off replicated state.
- **`PlayerCarryController.cs`** (`NetworkBehaviour`) — the interaction core. Owner detects a nearby
  `CarryHandle` (overlap), presses grab → `RequestGrabRpc(SendTo.Server)`; server validates
  (handle free / cargo mass allows this player), assigns the player to a handle slot, and for
  one-hand items parents/attaches via a server-side joint; for two-person items tracks both grabbers
  and only lifts when slot count ≥ required. Drop → `RequestReleaseRpc`. Throw → `RequestThrowRpc`
  applies impulse on server. Holds a `NetworkVariable<NetworkObjectReference> heldCargo`.

### Cargo
- **`CarryHandle.cs`** (`MonoBehaviour`) — plain marker on a cargo grab point: local attach transform,
  `HandleId`, back-reference to owning `CargoItem`. A `CargoItem` has 1 (light) or 2 (heavy) handles.
- **`CargoItem.cs`** (`NetworkBehaviour`, requires `Rigidbody` + `NetworkRigidbody`) — server-authoritative
  physics object. Fields: `CargoMassClass {OneHand, TwoPerson}`, `int baseValue`, handle list,
  `NetworkVariable<CarryState>` (Loose/Held/Delivered) and grabber client-id slots. Server-only
  `OnCollisionEnter` forwards impact magnitude to `CargoCondition`. Authority: server simulates; NGO
  `NetworkRigidbody` replicates kinematics → no client physics fighting.
- **`CargoCondition.cs`** (`NetworkBehaviour`) — `NetworkVariable<float> Condition` (1.0→0.0,
  server-write). `ApplyImpact(float magnitude)` (server) maps magnitude→damage via curve in
  `GameConstants`, clamps, raises `ClientRpc` `PlayDamageFxRpc` (spawn "-$$$" popup + crunch sfx →
  spectator readability). `CurrentValue => baseValue * Condition` rounded. Pure-math parts are unit-testable.

### Gameplay
- **`RoundManager.cs`** (`NetworkBehaviour`, server drives) — the state machine.
  `enum Phase { Lobby, Briefing, Hauling, Payout, GameOver }` in `NetworkVariable<Phase>`.
  Also `NetworkVariable<float> TimeRemaining`, `NetworkVariable<int> RoundNumber`. Transitions:
  Lobby→(host start)→Briefing(3s)→Hauling(timer)→Payout(eval quota)→ next Briefing or GameOver.
  Server ticks timer; on phase change raises `OnPhaseChangedRpc` for sfx/ui cues.
- **`QuotaSystem.cs`** (`NetworkBehaviour`) — `NetworkVariable<int> Cash`, `NetworkVariable<int> Quota`.
  `AddDelivery(int value)` (server). `EvaluateQuota()` returns met/failed; `EscalateQuota()` raises next
  quota (e.g. `quota = ceil(quota * 1.4) + 50`). Escalation/eval math is pure → unit-tested.
- **`CargoSpawner.cs`** (`NetworkBehaviour`, server) — at Briefing, spawns N cargo prefabs (from
  `NetworkPrefabRegistry`) at depot spawn points using `NetworkManager.SpawnManager.InstantiateAndSpawn`.
  Cleans up undelivered cargo at Payout.
- **`DeliveryZone.cs`** (`NetworkBehaviour`, server) — trigger volume at the van. `OnTriggerEnter`
  (server) for a `CargoItem` in Held/Loose state → compute `payout = CurrentValue × speedBonus`
  (speedBonus from time left), `QuotaSystem.AddDelivery(payout)`, mark Delivered, despawn cargo,
  fire `OnDeliveredRpc` (cha-ching fx + floating "+$$$").

### UI
- **`HudController.cs`** (`MonoBehaviour`) — subscribes to `RoundManager`/`QuotaSystem` NetworkVariable
  `OnValueChanged` → updates uGUI Text for Cash / Quota / Timer / Round / Phase banner. Read-only client.
- **`LobbyUI.cs`** (`MonoBehaviour`) — Host / Join(code) buttons → `NetworkSessionManager`; shows
  connected player list; host-only "Start Round" → `RoundManager.RequestStartRpc`. Hides on Hauling.

### Audio
- **`ProximityVoiceBridge.cs`** (`MonoBehaviour`) — integration POINT only. Defines `IVoiceProvider`
  (`SetLocalPlayer`, `RegisterRemote(transform, clientId)`, `Mute(clientId, bool)`) with a `NullVoiceProvider`
  default. Documents (in comments) the Dissonance wiring: add `DissonanceComms` + NGO comms network +
  `VoiceBroadcastTrigger`/`VoiceReceiptTrigger` with positional blend. Hard-cut on death = unregister.

### Game Assets
- **`AssetManifest.cs`** (`ScriptableObject`) — serialized list of `{ packName, kenneyUrl, license="CC0",
  usage }` + per-prefab mapping notes. Lets the project carry the download list as data; mirrors `ASSET_MANIFEST.md`.
- **`NetworkPrefabRegistry.cs`** (`ScriptableObject`) — typed references: `playerPrefab`, `cargoPrefabs[]`.
  Read by spawners; the same prefabs are registered in NGO's `NetworkManager.NetworkPrefabsList`.

## 4. Networking Design

**Ownership model**
- **Players:** owned by their client. Movement is client-authoritative via `NetworkTransform`
  (`AuthorityMode = Owner`) for snappy local control; server clamps illegal positions.
- **Cargo:** **server-authoritative** physics (`NetworkRigidbody`, server simulates). Clients never
  simulate cargo physics → eliminates the classic shared-rigidbody fight. Co-carry is expressed as
  the server applying forces/joint targets toward grabbers' hand transforms.
- **Round/Quota/Scoring:** server-only writes via `NetworkVariable<T>`; clients read.

**RPC / NetworkVariable inventory**
| Interaction | Mechanism |
|-------------|-----------|
| Host/Join | `INetworkSession` → `NetworkManager.StartHost/StartClient` |
| Connection gate | `ConnectionApprovalCallback` |
| Player spawn | `SpawnAsPlayerObject(clientId)` (server) |
| Move/pose | `NetworkTransform` (owner authority) |
| Anim state | `NetworkVariable<PlayerAnimState>` + replicated velocity |
| Grab | `RequestGrabRpc(SendTo.Server)` → server validates → `NetworkVariable heldCargo` + `CargoItem.CarryState` |
| Co-carry lift | server checks grabber slot count ≥ required before lifting |
| Drop | `RequestReleaseRpc(SendTo.Server)` |
| Throw | `RequestThrowRpc(SendTo.Server)` → server impulse |
| Impact damage | server `OnCollisionEnter` → `CargoCondition.ApplyImpact` → `PlayDamageFxRpc(SendTo.Everyone)` |
| Delivery | server `OnTriggerEnter` → score → `OnDeliveredRpc(SendTo.Everyone)` |
| Round phase | `NetworkVariable<Phase>` + `OnPhaseChangedRpc(SendTo.Everyone)` |

> NGO 2.x note: methods use the unified `[Rpc(SendTo.Server)]` / `[Rpc(SendTo.Everyone)]` attribute and
> must be named `…Rpc`. `NetworkVariable<T>` requires `T : INetworkSerializable`/unmanaged or a struct
> implementing it (enums OK).

**Spawn flow:** client connects → approval → server `PlayerSpawner` spawns player at depot →
`RoundManager` (Lobby) waits for host start.

**Host-leaves MVP:** no host migration. On host disconnect, clients detect `OnClientDisconnectCallback`
for the server and return to `LobbyUI` with a "Host left — session ended" message. (Phase 2: NGO
Distributed Authority.)

## 5. Implementation Steps (dependency-ordered)

1. **Project scaffold** → `.gitignore`, `README.md`, `ASSET_MANIFEST.md`, `Packages/manifest.json`,
   `ProjectSettings/ProjectVersion.txt`, `Assets/SlopCo/SlopCo.asmdef`.
2. **Core foundation** → `GameConstants.cs`, `ServiceLocator.cs`, `GameBootstrap.cs`.
3. **Asset data** → `AssetManifest.cs`, `NetworkPrefabRegistry.cs`.
4. **Networking layer** → `INetworkSession.cs`, `LocalNetworkSession.cs`, `SteamNetworkSession.cs`,
   `NetworkSessionManager.cs`, `ConnectionApprovalHandler.cs`, `PlayerSpawner.cs`.
5. **Player** → `PlayerInputReader.cs`, `PlayerController.cs`, `PlayerAnimator.cs`.
6. **Cargo** → `CarryHandle.cs`, `CargoCondition.cs`, `CargoItem.cs`.
7. **Carry interaction** → `PlayerCarryController.cs` (depends on cargo + player).
8. **Gameplay loop** → `QuotaSystem.cs`, `RoundManager.cs`, `CargoSpawner.cs`, `DeliveryZone.cs`.
9. **UI + Audio + Tests** → `HudController.cs`, `LobbyUI.cs`, `ProximityVoiceBridge.cs`,
   `Tests/EditMode/SlopCo.Tests.EditMode.asmdef`, `QuotaSystemTests.cs`, `CargoConditionTests.cs`.

## 6. change_files
See YAML `change_files` (33 entries). All C# compiles against Unity 6 + NGO 2.x with Steam absent.
Binary `.unity`/`.prefab`/`.asset`/FBX are dev-side per `README.md`.

## 7. Risks & Edge Cases
1. **Networked physics desync (#1 risk):** mitigated by server-only cargo simulation +
   `NetworkRigidbody`; clients never simulate cargo. Co-carry uses server-applied forces toward hand
   targets, not per-client joints. Prototype this first.
2. **Co-carry authority:** both grabbers tracked server-side; lift gated on required slot count; on a
   grabber disconnect mid-carry the server releases the slot and may drop the item.
3. **Animation networking:** driven off replicated velocity + small enum NetworkVariable, not per-frame
   RPCs (bandwidth-safe).
4. **Steam-absent compilation:** all Steam code under `#if USE_STEAM`; default `LocalNetworkSession`
   path guarantees the project builds and runs with zero Steam dependency.
5. **No runtime here:** cannot compile/play Unity in this environment → verification is static (API
   correctness + pure-logic unit tests + manifest completeness), not a runtime test. Stated honestly.

### Recommended Decomposition (per size-gate policy)
This slice is **33 files (>15)** but is a single tightly-coupled codebase; splitting into separate
pipeline cycles would fragment compile-interdependent scripts. **Orchestrator override:** implement in
ONE pass via parallel agents (ultracode), grouped by the 9 steps above (which form natural,
dependency-ordered chunks). Rationale recorded in pipeline output.

## 8. Test / Verification Strategy
- **EditMode unit tests (runnable by dev, authored here):** `QuotaSystemTests` (escalation curve,
  quota met/failed boundaries), `CargoConditionTests` (impact→damage clamping, value = base×condition).
  Pure logic extracted so it tests without a NetworkManager.
- **Static verification (here):** every script uses correct NGO 2.x APIs; no Steam symbol leaks outside
  `#if USE_STEAM`; namespaces/asmdef references resolve; manifest packages exist for Unity 6.
- **Manual playtest checklist (dev, local):** README documents: open in Unity 6 → import Kenney packs →
  build Player/Cargo prefabs + 2 scenes → ParrelSync/2 editors → Host + Join → grab/co-carry/drop/throw →
  damage popups → deliver → quota escalates → host-leave returns clients to lobby.

## 9. Public API Surface (CONTRACT — single source of truth)

> Every cross-file symbol is pinned here. Implementers MUST match these exactly. Enums first
> member = 0 (NetworkVariable default). No file may redefine a type owned by another file.

### Shared enums (owning file)
```csharp
// Cargo/CargoItem.cs
public enum CargoMassClass : byte { OneHand = 0, TwoPerson = 1 }
public enum CarryState     : byte { Loose = 0, Held = 1, Throwing = 2, Delivered = 3 }
// Player/PlayerAnimator.cs
public enum PlayerAnimState : byte { Idle = 0, Walk = 1, Run = 2, Jump = 3, Carry = 4 }
// Gameplay/RoundManager.cs
public enum RoundPhase : byte { Lobby = 0, Briefing = 1, Hauling = 2, Payout = 3, GameOver = 4 }
```

### Pure-math statics (no NGO — EditMode testable)
```csharp
// Gameplay/QuotaMath.cs   (static)
public static int    Escalate(int currentQuota);            // ceil(q*1.4)+50
public static bool   IsMet(int cash, int quota);            // cash >= quota
// Cargo/CargoMath.cs      (static)
public static float  DamageFromImpact(float impulseMag, float toughness); // clamp01 curve
public static float  ApplyDamage(float condition, float damage);          // clamp01(condition-damage)
public static int    Value(int baseValue, float condition01);             // round(base*condition)
public static int    Payout(int value, float speedBonus01);               // round(value*(1+speedBonus))
```

### Cross-namespace signatures (owning file → callers)
```csharp
// Cargo/CargoCondition.cs   (NetworkBehaviour)
public NetworkVariable<float> Condition; // read Everyone / write Server, init 1f
public int  CurrentValue { get; }        // CargoMath.Value(baseValue, Condition.Value)
public void ApplyImpact(float impulseMagnitude);   // SERVER only; updates Condition; PlayDamageFxRpc

// Cargo/CargoItem.cs        (NetworkBehaviour; req Rigidbody+NetworkTransform[Server]+NetworkRigidbody)
public CargoMassClass MassClass { get; }
public NetworkVariable<CarryState> State;           // server-write
public IReadOnlyList<CarryHandle> Handles { get; }
public bool TryClaimHandle(int handleId, ulong clientId);   // SERVER; false if occupied/illegal
public void ReleaseHandle(ulong clientId);                  // SERVER
public void RequestThrow(ulong clientId, Vector3 dir, float charge01); // SERVER; sequences throw
// server FixedUpdate runs the PD carry drive toward grabber hand targets

// Cargo/CarryHandle.cs      (MonoBehaviour, plain)
public int HandleId { get; }
public Transform AttachPoint { get; }
public CargoItem Owner { get; }       // assigned in Awake via GetComponentInParent

// Player/PlayerCarryController.cs (NetworkBehaviour)
public NetworkVariable<NetworkObjectReference> HeldCargo;  // default cleared on drop
public bool IsCarrying { get; }       // HeldCargo.Value.TryGet(out _) — null-safe
[Rpc(SendTo.Server)] void RequestGrabRpc(NetworkObjectReference cargo, int handleId);
[Rpc(SendTo.Server)] void RequestReleaseRpc();
[Rpc(SendTo.Server)] void RequestThrowRpc(Vector3 dir, float charge01);

// Player/PlayerController.cs (NetworkBehaviour) — uses ClientNetworkTransform (Owner auth)
public Vector3 PlanarVelocity { get; }   // read by PlayerAnimator
public NetworkVariable<int> ColorIndex;  // server-assigned by PlayerSpawner; tints material

// Gameplay/QuotaSystem.cs (NetworkBehaviour)
public NetworkVariable<int> Cash;  public NetworkVariable<int> Quota;   // server-write
public void AddDelivery(int payout);    // SERVER
public bool EvaluateQuota();            // SERVER; QuotaMath.IsMet
public void EscalateQuota();            // SERVER; QuotaMath.Escalate

// Gameplay/RoundManager.cs (NetworkBehaviour)
public NetworkVariable<RoundPhase> Phase;  public NetworkVariable<float> TimeRemaining;
public NetworkVariable<int> RoundNumber;   // all server-write
[Rpc(SendTo.Server)] public void RequestStartRpc();   // host/lobby → begin Briefing
[Rpc(SendTo.Everyone)] void OnPhaseChangedRpc(RoundPhase phase);

// Gameplay/DeliveryZone.cs (NetworkBehaviour) — server OnTriggerEnter → QuotaSystem.AddDelivery
// Gameplay/CargoSpawner.cs (NetworkBehaviour) — NetworkObject.InstantiateAndSpawn(prefab, NetworkManager.Singleton, …)

// Networking/INetworkSession.cs
Task<bool> HostAsync(int maxPlayers);  Task<bool> JoinAsync(string code);  void Leave();
string LobbyDisplayCode { get; }
event Action OnStarted, OnJoined, OnLeft;  event Action<string> OnFailed;

// Networking/NetworkSessionManager.cs (MonoBehaviour, in ServiceLocator)
public void HostGame();  public void JoinGame(string code);  public void Leave();
public INetworkSession Session { get; }

// Audio/ProximityVoiceBridge.cs
public interface IVoiceProvider { void SetLocalPlayer(Transform t); void RegisterRemote(ulong id, Transform t); void Unregister(ulong id); void Mute(ulong id, bool m); }
public sealed class NullVoiceProvider : IVoiceProvider { /* no-op; see Dissonance notes */ }
```

### Authority matrix (PIN)
| Object | NetworkTransform | Rigidbody | Authority | Physics sim |
|--------|------------------|-----------|-----------|-------------|
| Player | `ClientNetworkTransform` (Owner) | none (CharacterController) | Owner | local |
| Cargo  | `NetworkTransform` (Server) + `NetworkRigidbody` (`UseRigidBodyForMotion=true`) | yes | Server | server only |

NetworkManager: tick rate 50 Hz; cargo `Rigidbody.interpolation = Interpolate`.

## 10. Fun-Risk Register (distinct from engineering risk)

> A green compile + passing unit tests does **NOT** prove the game is fun-to-watch. None of the four
> viral pillars are runtime-validated by this compile-only slice. They are the real product risk and
> require a Unity runtime + playtest to de-risk.

| Viral pillar | Status in slice | How to validate (dev, runtime) |
|--------------|-----------------|-------------------------------|
| Proximity-voice cutoff comedy | **Integration point only** (Dissonance not wired) | Wire Dissonance positional channels; confirm voice hard-cuts on death |
| Co-carry feel (the core loop) | Code authored (PD drive), **feel unproven** | Prototype FIRST in two editors; tune kp/kd/maxForce until the fumble is funny not frustrating |
| Slapstick throw | Code authored, **feel unproven** | Tune charge/arc/impulse + landing damage |
| 4-player scrum readability | Color tint + FX hooks authored | Playtest: can a viewer tell who dropped the piano in <3s? |

**Primary slice success criterion (named):** the **grab2 → stagger → smash → "-$$$" → scream** loop.
This is what the slice exists to make buildable; tune it before anything else.

---
**Next step:** `/am:implement friend-slop-game`
