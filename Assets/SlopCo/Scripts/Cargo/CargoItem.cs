using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using SlopCo.Core;

namespace SlopCo.Cargo
{
    public enum CargoMassClass : byte { OneHand = 0, TwoPerson = 1 }
    public enum CarryState : byte { Loose = 0, Held = 1, Throwing = 2, Delivered = 3 }

    /// <summary>
    /// Server-authoritative physics cargo. Only the server simulates the Rigidbody (NGO NetworkRigidbody
    /// makes non-authority copies kinematic), so clients never fight over physics. Co-carry is driven by
    /// a critically-damped PD force controller in FixedUpdate toward the grabbers' hand transforms —
    /// NOT raw AddForce in RPC callbacks. A single grabber on a TwoPerson item gets a weak partial drag
    /// (staggering = comedy). Throw is sequenced to avoid the impulse being absorbed by the drive force.
    ///
    /// Crew-scaled co-carry: load is measured in PERSON-UNITS (CoCarryMath.LoadCrew), scaling with the
    /// live crew size (CrewCensus.Count) and capped by handle count. Both PD lift strength and haul speed
    /// derive from grabbers/loadCrew, so "heavier with a bigger crew, faster with more hands" is one
    /// knob set. Mass is deliberately NOT scaled — nothing in the codebase reads Rigidbody.mass, so a
    /// scaled mass would be unobservable; "heavier" is expressed as carry speed and required hands only.
    ///
    /// Required prefab components: Rigidbody + NetworkTransform(Server authority) + NetworkRigidbody
    /// (UseRigidBodyForMotion = true) + NetworkObject + CargoCondition.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class CargoItem : NetworkBehaviour
    {
        [SerializeField] private CargoMassClass massClass = CargoMassClass.OneHand;
        [SerializeField] private List<CarryHandle> handles = new();

        public CargoMassClass MassClass => massClass;
        public IReadOnlyList<CarryHandle> Handles => handles;

        /// <summary>SERVER. Spawn-time archetype mass override (set by CargoSpawner right after spawn, before any
        /// grab). BaseCrew reads massClass live, so a single set here is enough. Solo mode — and a OneHand
        /// override such as the Volatile archetype — pins CoCarryMath.LoadCrew to 1 person-unit, which returns
        /// the flat legacy carry multiplier at ANY number of grabbers, so a TwoPerson item still stays
        /// solo-carriable at exactly today's speed and lift strength.</summary>
        public void SetMassClass(CargoMassClass m) { if (!IsServer) return; massClass = m; }

        public readonly NetworkVariable<CarryState> State =
            new NetworkVariable<CarryState>(CarryState.Loose, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>Server-computed HAUL speed factor for whoever is holding this item right now (crew-scaled).
        /// Everyone-read so the OWNER of a carrier can apply it to its own CharacterController — the same
        /// server->client physics-parameter channel CargoBomb._archetype uses. Defaults to (and resets to) the
        /// flat legacy constant so any transient read is exactly today's behaviour.
        /// NOTE the name: this is NOT AugmentSystem.CarrySpeedMult — the two are multiplied together in
        /// PlayerController and then clamped once by CoCarryMath.ComposedCarryMult.</summary>
        public readonly NetworkVariable<float> HaulSpeedMult =
            new NetworkVariable<float>(GameConstants.PlayerCarrySpeedMultiplier,
                NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private Rigidbody _rb;
        private CargoCondition _condition;
        private int BaseCrew => massClass == CargoMassClass.TwoPerson ? 2 : 1;

        // Live, never cached at spawn: crew size changes mid-run (joins, leaves, bot despawn) and the
        // speed must follow within one server tick. Cached ONCE per tick by UpdateHaulScaling — DriveCarry
        // and NeedsMoreHands both read this field instead of recomputing.
        private float _loadCrew = 1f;
        private float _haulTarget = GameConstants.PlayerCarrySpeedMultiplier;
        private bool[] _freeMask;
        private float[] _sqrDist;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private int _dbgCrew = -1, _dbgGrab = -1;
        private float _dbgLoad = -1f;
#endif

        // Server-only grab bookkeeping. CarrierId is the GRABBING PLAYER's NetworkObjectId (unique per
        // player AND per AI bot) — NOT the clientId, because server-owned bots all share clientId 0 with
        // the host and would otherwise collide on the "one handle per carrier" rule.
        private readonly Dictionary<int, Transform> _grabbers = new();      // handleId -> hand target
        private readonly Dictionary<int, ulong> _grabberCarriers = new();   // handleId -> carrierId

        // The last player to claim a handle on this item (kept after release/throw = "last toucher").
        // Used to attribute deliveries and smashes to a player for the end-of-run MVP callout. 0 = never carried.
        private ulong _lastCarrier;
        /// <summary>NetworkObjectId of the last player to carry this item (0 = never carried). Server-truth,
        /// read by DeliveryZone to attribute the delivery.</summary>
        public ulong LastCarrier => _lastCarrier;

        // Throw sequencing (applied in FixedUpdate, one frame after the request).
        private bool _pendingThrow;
        private Vector3 _throwDir;
        private float _throwCharge;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _condition = GetComponent<CargoCondition>();
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            if (handles == null || handles.Count == 0)
                handles = new List<CarryHandle>(GetComponentsInChildren<CarryHandle>());

            // handleId is used as a LIST INDEX on the server (TryClaimHandle's range check + _grabbers[handleId])
            // while the client sends the SERIALIZED HandleId. Sort + assert that contract instead of trusting
            // hand-authored prefab data — doubling the bomb's handles doubles the odds of getting it wrong.
            handles.Sort((a, b) => (a == null ? int.MaxValue : a.HandleId).CompareTo(b == null ? int.MaxValue : b.HandleId));
            for (int i = 0; i < handles.Count; i++)
                if (handles[i] == null || handles[i].HandleId != i)
                    Debug.LogError($"[CargoItem] {name}: handles[{i}] must have HandleId {i} " +
                                   $"(got {(handles[i] == null ? "null" : handles[i].HandleId.ToString())}). Grabs will mis-route.");

            _freeMask = new bool[handles.Count];      // scratch for PickFreeHandle — no per-grab allocation
            _sqrDist  = new float[handles.Count];
        }

        // ── SERVER grab API (invoked by PlayerCarryController's server RPCs) ──

        public bool TryClaimHandle(int handleId, ulong carrierId, Transform handTarget)
        {
            if (!IsServer) return false;
            if (State.Value == CarryState.Delivered || State.Value == CarryState.Throwing) return false;
            if (handleId < 0 || handleId >= handles.Count) return false;
            if (_grabbers.ContainsKey(handleId)) return false;            // handle occupied
            if (_grabberCarriers.ContainsValue(carrierId)) return false;  // one handle per carrier

            _grabbers[handleId] = handTarget;
            _grabberCarriers[handleId] = carrierId;
            _lastCarrier = carrierId;          // remember the last toucher for delivery/smash attribution
            State.Value = CarryState.Held;
            return true;
        }

        /// <summary>SERVER. Claim <paramref name="preferredId"/>, or — if it is already occupied — the FREE
        /// handle nearest this grabber's hand. Without the fallback a HUMAN who holds the grab key gets exactly
        /// ONE attempt (the input latch stays true until the key is released, so with four handles "the nearest
        /// handle happened to be taken" becomes a silent dead input. The one-handle-per-carrier rule is
        /// preserved: a player already holding this item is rejected outright.</summary>
        public bool TryClaimAnyHandle(int preferredId, ulong carrierId, Transform handTarget)
        {
            if (!IsServer) return false;
            if (_grabberCarriers.ContainsValue(carrierId)) return false;   // one handle per carrier (CARRY-08)
            if (handTarget == null) return false;

            for (int i = 0; i < handles.Count; i++)
            {
                bool usable = handles[i] != null && handles[i].AttachPoint != null;
                _freeMask[i] = usable && !_grabbers.ContainsKey(i);
                _sqrDist[i]  = usable
                    ? (handles[i].AttachPoint.position - handTarget.position).sqrMagnitude
                    : float.PositiveInfinity;
            }

            int pick = CoCarryMath.PickFreeHandle(preferredId, _freeMask, _sqrDist, handles.Count);
            return pick >= 0 && TryClaimHandle(pick, carrierId, handTarget);
        }

        // ── SERVER TRUTH, INERT OFF-SERVER. _grabbers only ever exists on the server, so a client asking
        // these must get a value that means "I don't know" and reads as "do nothing" — not one that merely
        // looks plausible. Without the guard IsHandleFree would answer TRUE for every handle on a client. ──

        /// <summary>SERVER-TRUTH. How many hands are on this item right now (0 on non-server peers, where
        /// _grabbers is never populated). Read by AiBrain, which only runs on the owner of a server-owned bot
        /// (bots are spawned server-owned) — i.e. always the server.</summary>
        public int GrabberCount => IsServer ? _grabbers.Count : 0;

        /// <summary>SERVER-TRUTH. Is this handle index unclaimed? (Used by AiBrain to walk to a FREE handle
        /// instead of a taken one.) Returns FALSE (not true) on a non-server peer: _grabbers is empty there, so
        /// the naive answer would be "every handle is free" — an INERT default is required, not a plausible
        /// one. Same contract as GrabberCount's 0.</summary>
        public bool IsHandleFree(int handleId) =>
            IsServer && handleId >= 0 && handleId < handles.Count && !_grabbers.ContainsKey(handleId);

        /// <summary>SERVER-TRUTH. True when this item is already being carried but is still short of the hands
        /// its current load asks for AND has a free handle. This is the AI "come help" signal.
        /// Returns FALSE on a non-server peer — _grabbers is never populated there, so any answer computed off
        /// it would be a plausible-looking lie rather than a missing value. Delegates to
        /// CoCarryMath.NeedsMoreHands so the float boundary is EditMode-pinned.</summary>
        public bool NeedsMoreHands =>
            IsServer && CoCarryMath.NeedsMoreHands(_grabbers.Count, _loadCrew, handles.Count);

        public void ReleaseHandle(ulong carrierId)
        {
            if (!IsServer) return;
            ReleaseHandleInternal(carrierId);
            if (_grabbers.Count == 0 && State.Value == CarryState.Held)
                State.Value = CarryState.Loose;
        }

        public bool IsHeldBy(ulong carrierId) => _grabberCarriers.ContainsValue(carrierId);

        public void RequestThrow(ulong carrierId, Vector3 dir, float charge01)
        {
            if (!IsServer) return;
            if (!_grabberCarriers.ContainsValue(carrierId)) return;

            ReleaseHandleInternal(carrierId);

            // Only actually launch if no one else is still holding it (don't yank a piano from a friend).
            if (_grabbers.Count == 0)
            {
                _pendingThrow = true;
                _throwDir = dir.sqrMagnitude > 0.001f ? dir.normalized : transform.forward;
                _throwCharge = Mathf.Clamp01(charge01);
                State.Value = CarryState.Throwing;
            }
        }

        private void ReleaseHandleInternal(ulong carrierId)
        {
            int found = -1;
            foreach (var kv in _grabberCarriers)
                if (kv.Value == carrierId) { found = kv.Key; break; }
            if (found >= 0)
            {
                _grabbers.Remove(found);
                _grabberCarriers.Remove(found);
            }
        }

        private void FixedUpdate()
        {
            if (!IsServer) return;

            UpdateHaulScaling();                         // the only place _loadCrew is computed this tick
            if (_pendingThrow) { ApplyThrow(); return; }
            if (State.Value == CarryState.Held && _grabbers.Count > 0) DriveCarry();
        }

        // Live, never cached at spawn: crew size changes mid-run (joins, leaves, bot despawn) and the
        // speed must follow within one server tick. Called ONCE per tick from FixedUpdate.
        private float ComputeLoadCrew() => CoCarryMath.LoadCrew(
            BaseCrew, SlopCo.Core.CrewCensus.Count, handles.Count,
            SlopCo.Core.GameModeState.Solo, GameConstants.MaxPlayers, GameConstants.CoCarryLoadPerExtraPlayer);

        private void UpdateHaulScaling()
        {
            _loadCrew = ComputeLoadCrew();               // cached: DriveCarry and NeedsMoreHands read the field

            // Replicate the movement factor for the OWNER of whoever is holding this (CharacterController
            // movement is owner-authoritative, so the server cannot apply it directly). Reset to the flat
            // legacy constant when nobody holds it, so a transient read is never wrong in a dangerous direction.
            _haulTarget = _grabbers.Count > 0
                ? CoCarryMath.HaulSpeedFactor(_grabbers.Count, _loadCrew,
                      GameConstants.PlayerCarrySpeedMultiplier, GameConstants.CoCarryFullGripSpeed,
                      GameConstants.CoCarryUnderCrewedFloor, GameConstants.CoCarryOverCrewBonus)
                : GameConstants.PlayerCarrySpeedMultiplier;

            // Slew, don't teleport: a mid-run join steps loadCrew for every in-flight carrier at once.
            float next = _grabbers.Count > 0
                ? CoCarryMath.RampToward(HaulSpeedMult.Value, _haulTarget, Time.fixedDeltaTime,
                                         GameConstants.CoCarryHaulRampPerSecond)
                : _haulTarget;                            // nobody is reading it — snap so the next grab starts clean

            // Write on a real change OR on the SETTLING tick. RampToward returns exactly `target` once the
            // remainder is <= one step, and that final remainder can be <= the epsilon — with a plain
            // "> 0.001f" guard it would be skipped every tick, freezing the replicated value one rounding
            // unit short of the table.
            bool settling = next == _haulTarget && HaulSpeedMult.Value != next;
            if (settling || Mathf.Abs(next - HaulSpeedMult.Value) > 0.001f) HaulSpeedMult.Value = next;
            LogCoCarryIfChanged();                        // dev-only
        }

        private void DriveCarry()
        {
            Vector3 target = Vector3.zero;
            int n = 0;
            foreach (var t in _grabbers.Values)
                if (t != null) { target += t.position; n++; }
            if (n == 0) return;
            target /= n;

            // Continuous lift ramp: the first grabber still gets exactly UnderCrewedLiftStrength (0.28), each
            // additional one ramps toward 1.0 at full crew. No more binary cliff on a forced mid-air drop.
            // _loadCrew is the value UpdateHaulScaling already computed this tick — do not recompute.
            float strength = CoCarryMath.LiftStrength(_grabbers.Count, _loadCrew,
                                                      GameConstants.UnderCrewedLiftStrength);

            // PD gains are UNCHANGED: mass is not scaled, so equilibrium sag (m*g/k) and damping ratio
            // (kd / 2*sqrt(k*m)) are exactly what they were before this feature.
            Vector3 toTarget = target - _rb.worldCenterOfMass;
            Vector3 force = (GameConstants.CarryPD_Spring * toTarget) - (GameConstants.CarryPD_Damper * _rb.linearVelocity);
            force *= strength;
            force = Vector3.ClampMagnitude(force, GameConstants.CarryMaxForce);
            _rb.AddForce(force, ForceMode.Force);

            // Mild upright torque so heavy items don't tumble uncontrollably.
            Vector3 align = Vector3.Cross(transform.up, Vector3.up);
            _rb.AddTorque(align * (GameConstants.CarryAlignTorque * strength), ForceMode.Force);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// <summary>DEV. One-line server-side readout so playtesters report NUMBERS, not adjectives.</summary>
        public string CoCarryReadout =>
            $"crew={SlopCo.Core.CrewCensus.Count} load={_loadCrew:0.00} hands={_grabbers.Count} " +
            $"r={(_loadCrew > 0f ? _grabbers.Count / _loadCrew : 0f):0.000} haul={HaulSpeedMult.Value:0.000} " +
            $"({HaulSpeedMult.Value * GameConstants.PlayerMoveSpeed:0.00} u/s)";

        private void LogCoCarryIfChanged()
        {
            if (SlopCo.Core.CrewCensus.Count == _dbgCrew && _grabbers.Count == _dbgGrab && Mathf.Abs(_loadCrew - _dbgLoad) < 0.001f) return;
            _dbgCrew = SlopCo.Core.CrewCensus.Count; _dbgGrab = _grabbers.Count; _dbgLoad = _loadCrew;
            Debug.Log($"[CoCarry] {name}: {CoCarryReadout}");
        }
#else
        private void LogCoCarryIfChanged() { }
#endif

        private void ApplyThrow()
        {
            _pendingThrow = false;
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;

            float impulse = Mathf.Lerp(GameConstants.ThrowMinImpulse, GameConstants.ThrowMaxImpulse, _throwCharge);
            Vector3 dir = (_throwDir + Vector3.up * GameConstants.ThrowUpwardBias).normalized;
            _rb.AddForce(dir * impulse, ForceMode.Impulse);

            State.Value = CarryState.Loose;
        }

        // ── SERVER collision → depreciation. Landing a thrown item also smashes (ties throw to score). ──
        private void OnCollisionEnter(Collision collision)
        {
            if (!IsServer || _condition == null) return;
            _condition.ApplyImpact(collision.relativeVelocity.magnitude, _lastCarrier);
        }

        /// <summary>SERVER. Mark delivered so it can't be re-scored before despawn.</summary>
        public void MarkDelivered()
        {
            if (!IsServer) return;
            _grabbers.Clear();
            _grabberCarriers.Clear();
            State.Value = CarryState.Delivered;
        }

        public override void OnNetworkDespawn()
        {
            _grabbers.Clear();
            _grabberCarriers.Clear();
        }
    }
}
