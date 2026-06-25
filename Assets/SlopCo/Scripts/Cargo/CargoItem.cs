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

        public readonly NetworkVariable<CarryState> State =
            new NetworkVariable<CarryState>(CarryState.Loose, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private Rigidbody _rb;
        private CargoCondition _condition;
        // Solo mode: one player can carry anything (incl. two-person items) at full strength —
        // RequiredGrabbers drops to 1, so the under-crewed weak-drag branch in DriveCarry never trips.
        private int RequiredGrabbers => SlopCo.Core.GameModeState.Solo ? 1 : (massClass == CargoMassClass.TwoPerson ? 2 : 1);

        // Server-only grab bookkeeping. CarrierId is the GRABBING PLAYER's NetworkObjectId (unique per
        // player AND per AI bot) — NOT the clientId, because server-owned bots all share clientId 0 with
        // the host and would otherwise collide on the "one handle per carrier" rule.
        private readonly Dictionary<int, Transform> _grabbers = new();      // handleId -> hand target
        private readonly Dictionary<int, ulong> _grabberCarriers = new();   // handleId -> carrierId

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
            State.Value = CarryState.Held;
            return true;
        }

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

            if (_pendingThrow) { ApplyThrow(); return; }
            if (State.Value == CarryState.Held && _grabbers.Count > 0) DriveCarry();
        }

        private void DriveCarry()
        {
            Vector3 target = Vector3.zero;
            int n = 0;
            foreach (var t in _grabbers.Values)
                if (t != null) { target += t.position; n++; }
            if (n == 0) return;
            target /= n;

            // Under-crewed (e.g. 1 person on a 2-person piano) => weak drag = staggering comedy.
            float strength = _grabbers.Count < RequiredGrabbers ? GameConstants.UnderCrewedLiftStrength : 1f;

            // Critically-damped PD controller, force-clamped to prevent oscillation/launching.
            Vector3 toTarget = target - _rb.worldCenterOfMass;
            Vector3 force = (GameConstants.CarryPD_Spring * toTarget) - (GameConstants.CarryPD_Damper * _rb.linearVelocity);
            force *= strength;
            force = Vector3.ClampMagnitude(force, GameConstants.CarryMaxForce);
            _rb.AddForce(force, ForceMode.Force);

            // Mild upright torque so heavy items don't tumble uncontrollably.
            Vector3 align = Vector3.Cross(transform.up, Vector3.up);
            _rb.AddTorque(align * (GameConstants.CarryAlignTorque * strength), ForceMode.Force);
        }

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
            _condition.ApplyImpact(collision.relativeVelocity.magnitude);
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
