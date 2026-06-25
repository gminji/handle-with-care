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
    public sealed class RatHazard : NetworkBehaviour
    {
        [SerializeField] private float speed = 3.2f;
        [SerializeField] private float bumpRange = 1.3f;
        private float _cooldown;

        private void Update()
        {
            if (!IsServer) return;

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
