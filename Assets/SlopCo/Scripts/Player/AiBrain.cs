using Unity.Netcode;
using UnityEngine;
using SlopCo.Core;
using SlopCo.Cargo;
using SlopCo.Gameplay;

namespace SlopCo.Player
{
    /// <summary>
    /// Teammate brain for AI bots. Runs only on the owner (the host, which owns bot objects). Goal loop:
    /// TWO-PASS target selection — pass 1 claims the nearest LOOSE cargo, pass 2 (only if pass 1 finds
    /// nothing) joins the nearest already-Held cargo that still needs more hands (CargoItem.NeedsMoreHands).
    /// Claim always outranks joining: an unattended bomb burns the same fuse as an attended one and the
    /// round needs EVERY bomb delivered, so a free bomb is always worth more than a second pair of hands.
    /// Emits Move / GrabHeld / WantJump that <see cref="PlayerInputReader"/> feeds into the normal owner-auth
    /// pipeline (so a bot reuses the exact human movement/grab/throw code). Disabled by default on the
    /// player prefab; <see cref="PlayerController"/> enables it for bots.
    /// Bots are spawned SERVER-OWNED (not as player objects), so "owner" here is always the server — that is
    /// what makes the server-truth reads below (NeedsMoreHands / IsHandleFree) legal.
    /// </summary>
    public sealed class AiBrain : MonoBehaviour
    {
        public Vector2 Move { get; private set; }
        public bool GrabHeld { get; private set; }
        public bool WantJump { get; private set; }

        private NetworkObject _no;
        private PlayerCarryController _carry;
        private DeliveryZone _van;
        private Vector3 _lastPos;
        private float _stuckTimer;
        private float _retargetTimer;
        private CargoItem _target;
        // Remembers WHY _target was picked (claim vs. join) so the retarget condition can tell the two
        // apart: a claim target that gets claimed out from under us must be abandoned immediately (the
        // same anti-pileup guarantee today's Loose-only filter provides), but a join target staying Held
        // is exactly why it was chosen and must NOT be dropped for that reason.
        private bool _targetIsJoin;

        private ulong SelfId => _no != null ? _no.NetworkObjectId : 0UL;

        private void Awake()
        {
            _no = GetComponent<NetworkObject>();
            _carry = GetComponent<PlayerCarryController>();
        }

        private void Update()
        {
            // Only the owner (host) drives the bot; non-owners just receive its replicated transform.
            if (_no == null || !_no.IsOwner)
            {
                Move = Vector2.zero; GrabHeld = false; WantJump = false;
                return;
            }

            Vector3 pos = transform.position;
            Vector3 goal;

            if (_carry != null && _carry.IsCarrying)
            {
                // Carry it home — head for the van; keep gripping.
                goal = VanPos(pos);
                GrabHeld = true;
            }
            else
            {
                ulong selfId = SelfId;
                // Ticked unconditionally, OUTSIDE the || chain below: folding the decrement into the chain
                // would skip it whenever an earlier condition short-circuits, which happens to be harmless
                // today only because the block resets the timer anyway. Keep them separate so adding another
                // OR-branch later cannot silently stop the clock.
                _retargetTimer -= Time.deltaTime;

                // Re-pick target when: it's gone/invalid, OR (rev.3) it was a CLAIM target that someone else
                // just claimed out from under us — IsValidTarget alone would not catch this, because a
                // just-claimed Held+NeedsMoreHands cargo is STILL a valid JOIN target, so waiting for the
                // 0.5s retarget window would let every trailing bot pile onto the bomb a human just grabbed.
                if (_target == null || !IsValidTarget(_target, selfId)
                    || (!_targetIsJoin && !IsClaimable(_target))
                    || _retargetTimer <= 0f)
                {
                    _target = PickTarget(pos, selfId);
                    _targetIsJoin = _target != null && !IsClaimable(_target);
                    _retargetTimer = 0.5f;
                }

                if (_target != null)
                {
                    Vector3 h = NearestFreeHandlePos(_target, pos);
                    goal = h;
                    float reach = GameConstants.CarryGrabRadius * 0.85f;
                    GrabHeld = new Vector2(h.x - pos.x, h.z - pos.z).sqrMagnitude < reach * reach;
                }
                else { goal = pos; GrabHeld = false; }
            }

            Vector2 flat = new Vector2(goal.x - pos.x, goal.z - pos.z);
            Move = flat.sqrMagnitude > 0.04f ? flat.normalized : Vector2.zero;

            // Unstuck: if barely moving while we have somewhere to be, hop and re-pick a target.
            WantJump = false;
            if (Move != Vector2.zero)
            {
                if ((pos - _lastPos).sqrMagnitude < 0.0004f) _stuckTimer += Time.deltaTime;
                else _stuckTimer = 0f;
                if (_stuckTimer > 0.8f) { WantJump = true; _stuckTimer = 0f; _target = null; }
            }
            _lastPos = pos;
        }

        private Vector3 VanPos(Vector3 fallback)
        {
            if (_van == null) _van = Object.FindFirstObjectByType<DeliveryZone>();
            return _van != null ? _van.transform.position : fallback;
        }

        private static bool IsClaimable(CargoItem c) => c != null && c.State.Value == CarryState.Loose;

        private static bool IsJoinable(CargoItem c, ulong selfId) =>
            c != null && c.State.Value == CarryState.Held && c.NeedsMoreHands && !c.IsHeldBy(selfId);

        private static bool IsValidTarget(CargoItem c, ulong selfId) => IsClaimable(c) || IsJoinable(c, selfId);

        /// TWO PASSES, NOT ONE RANKING. An unattended bomb burns the same fuse as an attended one and the round
        /// needs EVERY bomb delivered, so a free bomb is ALWAYS worth more than a second pair of hands on a
        /// bomb that already has one. Joining is what we do when there is nothing left to pick up — never
        /// instead of picking something up. Single sweep, two winners.
        private static CargoItem PickTarget(Vector3 from, ulong selfId)
        {
            var all = Object.FindObjectsByType<CargoItem>(FindObjectsSortMode.None);
            CargoItem bestLoose = null; float bestLooseSqr = float.MaxValue;
            CargoItem bestJoin = null;  float bestJoinSqr = float.MaxValue;

            foreach (var c in all)
            {
                if (c == null) continue;
                float d = (c.transform.position - from).sqrMagnitude;
                if (IsClaimable(c)) { if (d < bestLooseSqr) { bestLooseSqr = d; bestLoose = c; } }
                else if (IsJoinable(c, selfId)) { if (d < bestJoinSqr) { bestJoinSqr = d; bestJoin = c; } }
            }
            return bestLoose != null ? bestLoose : bestJoin;   // PASS 1 wins outright
        }

        // Walk to a FREE handle, not merely the nearest one — otherwise two bots converge on the same grip.
        private static Vector3 NearestFreeHandlePos(CargoItem cargo, Vector3 from)
        {
            Vector3 best = cargo.transform.position; float bestSqr = float.MaxValue;
            foreach (var h in cargo.Handles)
            {
                if (h == null || h.AttachPoint == null) continue;
                if (!cargo.IsHandleFree(h.HandleId)) continue;
                float d = (h.AttachPoint.position - from).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = h.AttachPoint.position; }
            }
            return best;
        }
    }
}
