using Unity.Netcode;
using UnityEngine;
using SlopCo.Core;
using SlopCo.Cargo;

namespace SlopCo.Gameplay
{
    /// <summary>
    /// A crook that loiters near the cargo, waiting for you to fumble. The moment a piece of cargo leaves a
    /// player's hands and settles on the floor (<see cref="ThiefRules.IsStealable"/>) it sprints in, hoists
    /// the loot and bolts for the edge of the lot — where it dumps the cargo and vanishes. Losing a delivery
    /// outright would end a bomb-mode day on a coin flip, so the punishment is the CHASE: your bomb is now
    /// 60-odd metres away with the fuse still burning.
    ///
    /// Server-driven like <see cref="RatHazard"/>. Kicking it (<see cref="IKickable"/>) makes it drop
    /// everything and run — the counterplay. Spawned by <see cref="HazardDirector"/>.
    /// </summary>
    public sealed class ThiefHazard : NetworkBehaviour, IKickable
    {
        private enum Stage : byte { Loiter, Sprint, Escape, Flee }

        [Tooltip("Where the stolen cargo rides. Auto-created if unassigned.")]
        [SerializeField] private Transform holdPoint;

        private Stage _stage = Stage.Loiter;
        private float _stageT;
        private float _wanderPhase;

        private CargoItem _mark;        // the cargo being watched / chased
        private float _markRestingFor;  // how long the mark has been still
        private CargoItem _loot;        // what we actually grabbed
        private Rigidbody _lootBody;
        private bool _lootWasKinematic;
        private Vector3 _escapeTarget;

        private void Awake()
        {
            if (holdPoint == null)
            {
                var go = new GameObject("HoldPoint");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = new Vector3(0f, GameConstants.ThiefCarryHeight, 0.7f);
                holdPoint = go.transform;
            }
            _wanderPhase = Random.Range(0f, Mathf.PI * 2f);
        }

        private void Update()
        {
            if (!IsServer || DisconnectVote.GameFrozen) return;
            _stageT += Time.deltaTime;

            switch (_stage)
            {
                case Stage.Loiter: TickLoiter(); break;
                case Stage.Sprint: TickSprint(); break;
                case Stage.Escape: TickEscape(); break;
                case Stage.Flee:   TickFlee();   break;
            }

            if (_loot != null) CarryLoot();
        }

        // Circle the nearest cargo and watch for a fumble.
        private void TickLoiter()
        {
            if (_stageT >= GameConstants.ThiefGiveUpSeconds) { BeginFlee(); return; }

            if (_mark == null || _mark.State.Value == CarryState.Delivered) { _mark = NearestCargo(); _markRestingFor = 0f; }
            if (_mark == null) return;

            var body = _mark.GetComponent<Rigidbody>();
            float speed = body != null ? body.linearVelocity.magnitude : 0f;
            _markRestingFor = ThiefRules.AdvanceRest(_markRestingFor, speed, GameConstants.ThiefRestSpeed, Time.deltaTime);

            bool held = _mark.State.Value == CarryState.Held;
            bool delivered = _mark.State.Value == CarryState.Delivered;
            if (ThiefRules.IsStealable(held, delivered, _mark.LastCarrier, speed, _markRestingFor,
                                       GameConstants.ThiefRestSpeed, GameConstants.ThiefSettleSeconds))
            {
                _stage = Stage.Sprint;
                _stageT = 0f;
                return;
            }

            // Idle orbit around the mark so it reads as "casing the place", not standing still.
            _wanderPhase += Time.deltaTime * 0.9f;
            Vector3 ring = _mark.transform.position
                         + new Vector3(Mathf.Cos(_wanderPhase), 0f, Mathf.Sin(_wanderPhase)) * GameConstants.ThiefLoiterRadius;
            MoveTowards(ring, GameConstants.ThiefLoiterSpeed);
        }

        // Run it down. If someone picks it back up first, go back to waiting.
        private void TickSprint()
        {
            if (_mark == null || _mark.State.Value != CarryState.Loose) { _stage = Stage.Loiter; _stageT = 0f; return; }

            Vector3 to = _mark.transform.position - transform.position; to.y = 0f;
            if (to.sqrMagnitude > GameConstants.ThiefGrabRange * GameConstants.ThiefGrabRange)
            {
                MoveTowards(_mark.transform.position, GameConstants.ThiefSprintSpeed);
                return;
            }
            Grab(_mark);
        }

        // Bolt for the perimeter, then dump the loot and disappear.
        private void TickEscape()
        {
            if (_loot == null) { BeginFlee(); return; }

            MoveTowards(_escapeTarget, GameConstants.ThiefSprintSpeed);
            Vector3 flat = _escapeTarget - transform.position; flat.y = 0f;
            if (flat.sqrMagnitude > 4f) return;

            DropLoot();
            BeginFlee();
        }

        private void TickFlee()
        {
            MoveTowards(_escapeTarget, GameConstants.ThiefSprintSpeed);
            if (_stageT >= 4f && NetworkObject != null && NetworkObject.IsSpawned) NetworkObject.Despawn(true);
        }

        private void Grab(CargoItem cargo)
        {
            _loot = cargo;
            _lootBody = cargo.GetComponent<Rigidbody>();
            if (_lootBody != null)
            {
                _lootWasKinematic = _lootBody.isKinematic;
                _lootBody.linearVelocity = Vector3.zero;
                _lootBody.angularVelocity = Vector3.zero;
                _lootBody.isKinematic = true;   // ride along instead of fighting the thief's transform
            }
            _escapeTarget = PerimeterPoint();
            _stage = Stage.Escape;
            _stageT = 0f;
            ScreenShake.Add(0.25f);
        }

        private void DropLoot()
        {
            if (_loot == null) return;
            if (_lootBody != null)
            {
                _lootBody.isKinematic = _lootWasKinematic;
                _lootBody.linearVelocity = Vector3.zero;
                _lootBody.angularVelocity = Vector3.zero;
            }
            _loot = null;
            _lootBody = null;
        }

        private void CarryLoot()
        {
            // A player who wrestles it back (or a delivery) wins it — let go immediately.
            if (_loot.State.Value == CarryState.Held || _loot.State.Value == CarryState.Delivered) { DropLoot(); return; }
            _loot.transform.position = holdPoint.position;
        }

        private void BeginFlee()
        {
            DropLoot();
            _escapeTarget = PerimeterPoint();
            _stage = Stage.Flee;
            _stageT = 0f;
        }

        /// <summary>SERVER. Booted — drop the loot on the spot and run for it.</summary>
        public void OnKicked(Vector3 fromPos)
        {
            if (!IsServer) return;
            BeginFlee();
            ScreenShake.Add(0.3f);
        }

        // Straight out from the middle of the lot, just past the play boundary.
        private Vector3 PerimeterPoint()
        {
            Vector3 out2 = transform.position; out2.y = 0f;
            if (out2.sqrMagnitude < 1f) out2 = new Vector3(1f, 0f, 0f);
            return out2.normalized * (GameConstants.PlayAreaRadius * 0.95f);
        }

        private void MoveTowards(Vector3 worldTarget, float speed)
        {
            Vector3 to = worldTarget - transform.position; to.y = 0f;
            if (to.sqrMagnitude > 0.01f)
            {
                Vector3 dir = to.normalized;
                transform.position += dir * (speed * Time.deltaTime);
                transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            }
            transform.position = new Vector3(transform.position.x, 0f, transform.position.z);
        }

        private CargoItem NearestCargo()
        {
            CargoItem best = null;
            float bestSqr = float.MaxValue;
            foreach (var c in Object.FindObjectsByType<CargoItem>(FindObjectsSortMode.None))
            {
                if (c == null || c.State.Value == CarryState.Delivered) continue;
                float d = (c.transform.position - transform.position).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = c; }
            }
            return best;
        }

        public override void OnNetworkDespawn()
        {
            // Never leave stolen cargo frozen in mid-air if we vanish (round end, host teardown).
            if (IsServer) DropLoot();
        }
    }
}
