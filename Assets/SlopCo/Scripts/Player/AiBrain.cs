using Unity.Netcode;
using UnityEngine;
using SlopCo.Core;
using SlopCo.Cargo;
using SlopCo.Gameplay;

namespace SlopCo.Player
{
    /// <summary>
    /// Teammate brain for AI bots. Runs only on the owner (the host, which owns bot objects). Simple goal
    /// loop: seek the nearest loose cargo handle, grab when close, then walk it to the delivery van. Emits
    /// Move / GrabHeld / WantJump that <see cref="PlayerInputReader"/> feeds into the normal owner-auth
    /// pipeline (so a bot reuses the exact human movement/grab/throw code). Disabled by default on the
    /// player prefab; <see cref="PlayerController"/> enables it for bots.
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
                if (_target == null || _target.State.Value != CarryState.Loose || (_retargetTimer -= Time.deltaTime) <= 0f)
                {
                    _target = NearestLooseCargo(pos);
                    _retargetTimer = 0.5f;
                }

                if (_target != null)
                {
                    Vector3 h = NearestHandlePos(_target, pos);
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

        private static CargoItem NearestLooseCargo(Vector3 from)
        {
            var all = Object.FindObjectsByType<CargoItem>(FindObjectsSortMode.None);
            CargoItem best = null; float bestSqr = float.MaxValue;
            foreach (var c in all)
            {
                if (c == null || c.State.Value != CarryState.Loose) continue;
                float d = (c.transform.position - from).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = c; }
            }
            return best;
        }

        private static Vector3 NearestHandlePos(CargoItem cargo, Vector3 from)
        {
            Vector3 best = cargo.transform.position; float bestSqr = float.MaxValue;
            foreach (var h in cargo.Handles)
            {
                if (h == null || h.AttachPoint == null) continue;
                float d = (h.AttachPoint.position - from).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = h.AttachPoint.position; }
            }
            return best;
        }
    }
}
