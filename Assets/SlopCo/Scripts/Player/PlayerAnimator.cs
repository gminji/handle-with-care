using Unity.Netcode;
using UnityEngine;
using SlopCo.Core;

namespace SlopCo.Player
{
    public enum PlayerAnimState : byte { Idle = 0, Walk = 1, Run = 2, Jump = 3, Carry = 4 }

    /// <summary>
    /// Drives the Kenney Mini Characters Animator on EVERY client without per-frame RPCs: speed is
    /// derived locally from the replicated NetworkTransform position delta, and carry state is read
    /// from the replicated HeldCargo (so remote players animate correctly too). Animator params:
    /// "Speed" (float), "IsCarrying" (bool).
    /// </summary>
    public sealed class PlayerAnimator : NetworkBehaviour
    {
        [SerializeField] private Animator animator;
        [SerializeField] private PlayerCarryController carry;

        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private static readonly int CarryHash = Animator.StringToHash("IsCarrying");

        public PlayerAnimState State { get; private set; } = PlayerAnimState.Idle;

        private Vector3 _lastPos;
        private float _smoothedSpeed;

        private void Awake()
        {
            if (animator == null) animator = GetComponentInChildren<Animator>();
            if (carry == null) carry = GetComponent<PlayerCarryController>();
        }

        public override void OnNetworkSpawn() => _lastPos = transform.position;

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector3 delta = transform.position - _lastPos;
            delta.y = 0f;
            _lastPos = transform.position;

            float instantaneous = delta.magnitude / dt;
            _smoothedSpeed = Mathf.Lerp(_smoothedSpeed, instantaneous, 1f - Mathf.Exp(-12f * dt));

            bool carrying = carry != null && carry.IsCarrying;
            State = ResolveState(_smoothedSpeed, carrying);

            if (animator != null)
            {
                animator.SetFloat(SpeedHash, _smoothedSpeed);
                animator.SetBool(CarryHash, carrying);
            }
        }

        private static PlayerAnimState ResolveState(float speed, bool carrying)
        {
            if (carrying) return PlayerAnimState.Carry;
            if (speed >= GameConstants.AnimRunThreshold) return PlayerAnimState.Run;
            if (speed >= GameConstants.AnimWalkThreshold) return PlayerAnimState.Walk;
            return PlayerAnimState.Idle;
        }
    }
}
