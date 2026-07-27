using Unity.Netcode;
using UnityEngine;
using SlopCo.Core;
using SlopCo.Player;

namespace SlopCo.Gameplay
{
    /// <summary>
    /// The "rats" augment downside. Server-owned hazard that chases whichever player is currently carrying
    /// cargo and, on contact, trips them — forcing a drop (<see cref="PlayerCarryController.ForceDrop"/>)
    /// plus a little screen shake. Moves server-side (a server-authoritative NetworkTransform replicates
    /// the pose). Spawned/cleared by <see cref="AugmentSystem"/> via RoundManager hooks.
    /// </summary>
    public sealed class RatHazard : NetworkBehaviour, IKickable
    {
        [SerializeField] private float speed = 3.2f;
        [SerializeField] private float bumpRange = 1.3f;
        [Tooltip("Seconds a kicked rat spends scurrying away before it resumes the chase.")]
        [SerializeField] private float kickedFleeSeconds = 3f;
        [SerializeField] private float fleeSpeedMultiplier = 2.2f;
        private float _cooldown;
        private float _fleeT;
        private Vector3 _fleeDir;

        /// <summary>SERVER. Booted — scurry off for a few seconds instead of tripping anyone.</summary>
        public void OnKicked(Vector3 fromPos)
        {
            if (!IsServer) return;
            Vector3 away = transform.position - fromPos; away.y = 0f;
            _fleeDir = away.sqrMagnitude > 0.0001f ? away.normalized : -transform.forward;
            _fleeT = kickedFleeSeconds;
            _cooldown = Mathf.Max(_cooldown, kickedFleeSeconds);   // can't trip anyone while running away
            ScreenShake.Add(0.2f);
        }

        private void Update()
        {
            if (!IsServer || DisconnectVote.GameFrozen) return;

            if (_fleeT > 0f)
            {
                _fleeT -= Time.deltaTime;
                transform.position += _fleeDir * (speed * fleeSpeedMultiplier * Time.deltaTime);
                transform.position = new Vector3(transform.position.x, 0.5f, transform.position.z);
                if (_fleeDir.sqrMagnitude > 0.0001f)
                    transform.rotation = Quaternion.LookRotation(_fleeDir, Vector3.up);
                _cooldown -= Time.deltaTime;
                return;
            }

            var target = NearestCarrier();
            if (target == null) return;

            Vector3 to = target.transform.position - transform.position; to.y = 0f;
            float distSqr = to.sqrMagnitude;
            if (distSqr > 0.04f)
            {
                Vector3 dir = to.normalized;
                transform.position += dir * speed * Time.deltaTime;
                transform.rotation = Quaternion.LookRotation(dir, Vector3.up);
            }
            transform.position = new Vector3(transform.position.x, 0.5f, transform.position.z);

            _cooldown -= Time.deltaTime;
            if (_cooldown <= 0f && distSqr < bumpRange * bumpRange)
            {
                target.ForceDrop();   // trip them → drop the cargo
                _cooldown = 2.5f;
                ScreenShake.Add(0.4f);
            }
        }

        private PlayerCarryController NearestCarrier()
        {
            PlayerCarryController best = null;
            float bestSqr = float.MaxValue;
            foreach (var c in Object.FindObjectsByType<PlayerCarryController>(FindObjectsSortMode.None))
            {
                if (c == null || !c.IsCarrying) continue;
                float d = (c.transform.position - transform.position).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = c; }
            }
            return best;
        }
    }
}
