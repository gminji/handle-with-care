using Unity.Netcode;
using UnityEngine;
using SlopCo.Core;
using SlopCo.Player;

namespace SlopCo.Gameplay
{
    /// <summary>
    /// A flying saucer that drifts in, picks one hauler, beams them up and drops them — the landing costs
    /// stamina (see <see cref="AbductionMath"/>). Server-driven like <see cref="RatHazard"/> (a
    /// server-authoritative NetworkTransform replicates the pose), but the abduction itself is delegated to
    /// the VICTIM'S owner via <see cref="PlayerController.BeginAbductionRpc"/>, because a CharacterController
    /// can only be moved by its owner.
    ///
    /// Kicking it (see <see cref="IKickable"/>) cuts the beam and sends it home early — the counterplay.
    /// Spawned and cleared by <see cref="HazardDirector"/>.
    /// </summary>
    public sealed class UfoHazard : NetworkBehaviour, IKickable
    {
        private enum Stage : byte { Hunt, Beam, Leave }

        private Stage _stage = Stage.Hunt;
        private float _stageT;
        private PlayerController _victim;

        private void Update()
        {
            if (!IsServer || DisconnectVote.GameFrozen) return;
            _stageT += Time.deltaTime;

            switch (_stage)
            {
                case Stage.Hunt:  TickHunt();  break;
                case Stage.Beam:  TickBeam();  break;
                case Stage.Leave: TickLeave(); break;
            }
        }

        // Cruise toward the nearest grounded player; lock the beam once we are overhead.
        private void TickHunt()
        {
            if (_stageT >= GameConstants.UfoHuntSeconds) { Enter(Stage.Leave); return; }

            var target = NearestCandidate();
            if (target == null) return;

            Vector3 want = new Vector3(target.transform.position.x,
                                       target.transform.position.y + GameConstants.UfoCruiseHeight,
                                       target.transform.position.z);
            transform.position = Vector3.MoveTowards(transform.position, want, GameConstants.UfoMoveSpeed * Time.deltaTime);
            transform.Rotate(Vector3.up, 90f * Time.deltaTime, Space.World);   // idle spin

            Vector3 flat = target.transform.position - transform.position; flat.y = 0f;
            if (flat.sqrMagnitude > GameConstants.UfoCatchRadius * GameConstants.UfoCatchRadius) return;

            // Hands free first: a bomb dragged skyward by the carry PD drive would be a physics mess.
            target.GetComponent<PlayerCarryController>()?.ForceDrop();
            _victim = target;
            target.BeginAbductionRpc(NetworkObject);
            Enter(Stage.Beam);
        }

        // Hold station over the victim for the length of the abduction, then climb out.
        private void TickBeam()
        {
            if (_victim == null) { Enter(Stage.Leave); return; }

            Vector3 want = new Vector3(_victim.transform.position.x,
                                       Mathf.Max(transform.position.y, _victim.transform.position.y + GameConstants.UfoCarryGap + 1.5f),
                                       _victim.transform.position.z);
            transform.position = Vector3.MoveTowards(transform.position, want, GameConstants.UfoMoveSpeed * 0.5f * Time.deltaTime);
            transform.Rotate(Vector3.up, 220f * Time.deltaTime, Space.World);  // spin up while the beam is on

            if (_stageT >= GameConstants.UfoLiftSeconds + GameConstants.UfoHoldSeconds) Enter(Stage.Leave);
        }

        // Climb away and despawn — the victim is already falling under their own gravity.
        private void TickLeave()
        {
            transform.position += Vector3.up * (GameConstants.UfoMoveSpeed * 1.5f * Time.deltaTime);
            transform.Rotate(Vector3.up, 160f * Time.deltaTime, Space.World);
            if (_stageT >= GameConstants.UfoLeaveSeconds && NetworkObject != null && NetworkObject.IsSpawned)
                NetworkObject.Despawn(true);
        }

        private void Enter(Stage s)
        {
            _stage = s;
            _stageT = 0f;
            if (s == Stage.Leave) _victim = null;
        }

        /// <summary>SERVER. Booted — cut the beam, drop whoever we were holding, and leave.</summary>
        public void OnKicked(Vector3 fromPos)
        {
            if (!IsServer) return;
            if (_victim != null) _victim.EndAbductionRpc();
            ScreenShake.Add(0.35f);
            Enter(Stage.Leave);
        }

        // A player worth abducting: on the ground, not already in someone else's beam.
        private PlayerController NearestCandidate()
        {
            PlayerController best = null;
            float bestSqr = float.MaxValue;
            foreach (var p in Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            {
                if (p == null || p.IsAbducted) continue;
                float d = (p.transform.position - transform.position).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = p; }
            }
            return best;
        }

        public override void OnNetworkDespawn()
        {
            // Never strand a victim hanging in mid-air if we vanish (round end, host teardown).
            if (IsServer && _victim != null) _victim.EndAbductionRpc();
            _victim = null;
        }
    }
}
