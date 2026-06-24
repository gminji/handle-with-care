using Unity.Netcode;
using UnityEngine;
using SlopCo.Core;

namespace SlopCo.Player
{
    /// <summary>
    /// Owner-authoritative player movement on a CharacterController (pairs with ClientNetworkTransform).
    /// The owner drives movement locally for snappy feel; NetworkTransform replicates pose to others.
    /// Carrying slows the player (tension + comedy). Server assigns <see cref="ColorIndex"/> for
    /// instant spectator readability; all clients tint their copy via MaterialPropertyBlock.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : NetworkBehaviour
    {
        [SerializeField] private PlayerInputReader input;
        [SerializeField] private PlayerCarryController carry;
        [SerializeField] private Transform cameraTarget;
        [Tooltip("Kenney character mesh renderers to tint per player color.")]
        [SerializeField] private Renderer[] tintRenderers;

        /// <summary>Server-assigned per-player color (PlayerSpawner writes this).</summary>
        public readonly NetworkVariable<int> ColorIndex =
            new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public Vector3 PlanarVelocity { get; private set; }

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly Color[] Palette =
        {
            new Color(0.90f, 0.30f, 0.30f), // red
            new Color(0.30f, 0.55f, 0.95f), // blue
            new Color(0.40f, 0.85f, 0.40f), // green
            new Color(0.95f, 0.85f, 0.30f), // yellow
        };

        private CharacterController _cc;
        private Camera _cam;
        private float _verticalVel;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (carry == null) carry = GetComponent<PlayerCarryController>();
        }

        public override void OnNetworkSpawn()
        {
            ApplyColor(ColorIndex.Value);
            ColorIndex.OnValueChanged += OnColorChanged;

            if (IsOwner)
            {
                input?.Enable();
                _cam = Camera.main;
            }
            else
            {
                input?.Disable();
            }
        }

        public override void OnNetworkDespawn()
        {
            ColorIndex.OnValueChanged -= OnColorChanged;
            if (IsOwner) input?.Disable();
        }

        private void Update()
        {
            if (!IsOwner || input == null) return;

            Vector2 m = input.Move;
            Vector3 dir = new Vector3(m.x, 0f, m.y);
            if (dir.sqrMagnitude > 1f) dir.Normalize();

            float speed = GameConstants.PlayerMoveSpeed *
                          (carry != null && carry.IsCarrying ? GameConstants.PlayerCarrySpeedMultiplier : 1f);
            Vector3 horizontal = dir * speed;

            if (_cc.isGrounded)
            {
                _verticalVel = -1f;
                if (input.JumpPressed) _verticalVel = GameConstants.PlayerJumpSpeed;
            }
            else
            {
                _verticalVel += GameConstants.Gravity * Time.deltaTime;
            }

            Vector3 velocity = horizontal;
            velocity.y = _verticalVel;
            _cc.Move(velocity * Time.deltaTime);
            PlanarVelocity = new Vector3(horizontal.x, 0f, horizontal.z);

            if (horizontal.sqrMagnitude > 0.01f)
            {
                Quaternion target = Quaternion.LookRotation(horizontal.normalized, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, target, 720f * Time.deltaTime);
            }

            EnforceBounds();
        }

        private void LateUpdate()
        {
            if (!IsOwner || _cam == null) return;
            Transform t = cameraTarget != null ? cameraTarget : transform;
            Vector3 desired = t.position + new Vector3(0f, 6f, -7f);
            float k = 1f - Mathf.Exp(-10f * Time.deltaTime);
            _cam.transform.position = Vector3.Lerp(_cam.transform.position, desired, k);
            Vector3 look = (t.position + Vector3.up * 1.2f) - _cam.transform.position;
            if (look.sqrMagnitude > 0.001f)
                _cam.transform.rotation = Quaternion.Slerp(_cam.transform.rotation, Quaternion.LookRotation(look), k);
        }

        // Soft, owner-side bounds — avoids a server overwrite fight with the owner-auth transform.
        private void EnforceBounds()
        {
            Vector3 p = transform.position;
            Vector2 flat = new Vector2(p.x, p.z);
            float r = GameConstants.PlayAreaRadius;
            if (flat.sqrMagnitude > r * r)
            {
                flat = flat.normalized * r;
                _cc.enabled = false;
                transform.position = new Vector3(flat.x, p.y, flat.y);
                _cc.enabled = true;
            }
        }

        private void OnColorChanged(int _, int next) => ApplyColor(next);

        private void ApplyColor(int idx)
        {
            if (tintRenderers == null || tintRenderers.Length == 0) return;
            Color c = Palette[Mathf.Clamp(idx, 0, Palette.Length - 1)];
            var mpb = new MaterialPropertyBlock();
            foreach (var r in tintRenderers)
            {
                if (r == null) continue;
                r.GetPropertyBlock(mpb);
                mpb.SetColor(BaseColorId, c);
                r.SetPropertyBlock(mpb);
            }
        }
    }
}
