using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using SlopCo.Core;
using SlopCo.Gameplay;

namespace SlopCo.Player
{
    /// <summary>
    /// The boot. The owner asks, the SERVER resolves who is inside the arc (<see cref="KickMath"/>), and the
    /// launch is applied by each victim's OWNER via <see cref="PlayerController.ApplyKnockbackRpc"/> — a
    /// CharacterController can't be pushed from the server, the same constraint the blast shove works around.
    /// Hazards implementing <see cref="IKickable"/> are driven off by the same swing, so a kick is both a
    /// prank on your friends and a real tool against rats, thieves and UFOs.
    ///
    /// Cooldown is enforced on BOTH sides: locally for responsiveness, on the server so a modified client
    /// can't machine-gun the arc.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public sealed class KickAbility : NetworkBehaviour
    {
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerController controller;

        /// <summary>Presentation cue raised on every client when a kick swings: world position and whether it
        /// connected. <see cref="SlopCo.Audio.GameAudio"/> turns this into the whiff/impact sound.</summary>
        public static event Action<Vector3, bool> OnKickFx;

        private float _localCooldown;   // owner-side, for input feel
        private float _serverCooldown;  // authoritative

        private void Awake()
        {
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (controller == null) controller = GetComponent<PlayerController>();
        }

        private void Update()
        {
            if (IsServer && _serverCooldown > 0f) _serverCooldown -= Time.deltaTime;
            if (!IsOwner || input == null) return;

            if (_localCooldown > 0f) _localCooldown -= Time.deltaTime;
            if (!input.KickPressed || _localCooldown > 0f) return;

            _localCooldown = GameConstants.KickCooldown;
            ScreenShake.Add(GameConstants.KickShakeKicker);   // the kicker feels the swing immediately
            RequestKickRpc(transform.position, transform.forward);
        }

        /// <summary>SERVER. Resolve one swing. Position/facing come from the owner (it owns the transform), but
        /// the reach is re-derived here from constants, so a client can only pick WHERE it stands, not how far
        /// its leg goes. A stale/teleported origin is rejected against the server's copy of the transform.</summary>
        [Rpc(SendTo.Server)]
        private void RequestKickRpc(Vector3 origin, Vector3 forward)
        {
            if (_serverCooldown > 0f) return;
            _serverCooldown = GameConstants.KickCooldown;

            // Trust the server's own transform if the claimed origin has drifted (lag or tampering).
            if ((origin - transform.position).sqrMagnitude > 9f) origin = transform.position;

            bool connected = false;
            var seen = new HashSet<EntityId>();
            var hits = Physics.OverlapSphere(origin, GameConstants.KickRange);
            foreach (var col in hits)
            {
                if (col == null) continue;

                var victim = col.GetComponentInParent<PlayerController>();
                if (victim != null && victim != controller && seen.Add(victim.GetEntityId()))
                {
                    if (!KickMath.InCone(origin, forward, victim.transform.position,
                                         GameConstants.KickRange, GameConstants.KickHalfAngle)) continue;
                    Vector3 imp = KickMath.Impulse(origin, forward, victim.transform.position,
                                                   GameConstants.KickRange, GameConstants.KickSpeed);
                    if (imp.sqrMagnitude <= 0.0001f) continue;
                    victim.ApplyKnockbackRpc(imp, GameConstants.KickPopUp);
                    connected = true;
                    continue;
                }

                var hazard = col.GetComponentInParent<IKickable>();
                if (hazard != null)
                {
                    var mb = hazard as MonoBehaviour;
                    if (mb == null || !seen.Add(mb.GetEntityId())) continue;
                    if (!KickMath.InCone(origin, forward, mb.transform.position,
                                         GameConstants.KickRange, GameConstants.KickHalfAngle)) continue;
                    hazard.OnKicked(origin);
                    connected = true;
                }
            }

            Vector3 f = forward; f.y = 0f;
            f = f.sqrMagnitude > 1e-6f ? f.normalized : transform.forward;
            KickFxRpc(origin + f * 0.9f + Vector3.up * 0.9f, connected);
        }

        /// <summary>Every client: sound + a readable little impact pop at the boot.</summary>
        [Rpc(SendTo.Everyone)]
        private void KickFxRpc(Vector3 pos, bool connected)
        {
            OnKickFx?.Invoke(pos, connected);
            if (connected)
                FloatingNumber.Spawn(pos + Vector3.up * 0.4f, "POW!", new Color(1f, 0.85f, 0.25f), 1.25f);
        }
    }
}
