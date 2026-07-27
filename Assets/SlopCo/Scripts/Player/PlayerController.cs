using Unity.Netcode;
using UnityEngine;
using SlopCo.Core;
using SlopCo.Cargo;

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
        [Tooltip("Item inventory for use/discard (wired in part 5; auto-found if on the same object).")]
        [SerializeField] private SlopCo.Items.PlayerInventory inventory;

        /// <summary>Server-assigned per-player color (PlayerSpawner writes this).</summary>
        public readonly NetworkVariable<int> ColorIndex =
            new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public Vector3 PlanarVelocity { get; private set; }

        /// <summary>The local *human* player (set only on the real owning human, never bots/AI). On a host the
        /// server owns both the human and all bots, so IsOwner/IsLocalPlayer can't disambiguate — DashGauge reads this.</summary>
        public static PlayerController LocalHuman { get; private set; }

        /// <summary>Owner-local dash stamina (0..1) and exhausted flag — for the local HUD dash gauge.</summary>
        public float DashGauge01 => _dash.gauge;
        public bool DashExhausted => _dash.exhausted;

        // ── Item effects (owner-local; PlayerInventory fires the owner-targeted RPC below) ──
        public void ApplyTempSpeedBuff(float mult, float seconds) { _itemBuffMult = mult; _itemBuffT = seconds; }
        public void RefillStamina(float amount01) { _dash = DashStamina.Refill(_dash, amount01); }

        /// <summary>OWNER. Someone booted us. The server resolved the hit (see <c>KickAbility</c>) but only the
        /// owner may move its own CharacterController, so the launch lands here — same path as the blast shove.
        /// The vertical pop rides <c>_shoveVel.y</c>, NOT <c>_verticalVel</c>, so gravity still behaves.</summary>
        [Rpc(SendTo.Owner)]
        public void ApplyKnockbackRpc(Vector3 horizontalImpulse, float popUp)
        {
            _shoveVel += new Vector3(horizontalImpulse.x, popUp, horizontalImpulse.z);
            ScreenShake.Add(GameConstants.KickShakeVictim);
        }

        /// <summary>OWNER. A UFO has us in its beam. The server picked the victim and already made us drop any
        /// cargo; the lift/hold/drop itself runs here because only the owner may move this CharacterController.
        /// Passing the saucer by reference (not a fixed point) keeps us hanging under it as it drifts.</summary>
        [Rpc(SendTo.Owner)]
        public void BeginAbductionRpc(NetworkObjectReference ufo)
        {
            if (!ufo.TryGet(out var ufoObj)) return;
            _abductor = ufoObj.transform;
            _abductT = 0f;
            _abductStartY = transform.position.y;
            _falling = false;
            _shoveVel = Vector3.zero;   // a shove mid-beam would fight the lift
        }

        /// <summary>OWNER. Beam cut early (the saucer was kicked or despawned) — drop from wherever we are.</summary>
        [Rpc(SendTo.Owner)]
        public void EndAbductionRpc() => ReleaseFromBeam();

        private void ReleaseFromBeam()
        {
            if (_abductor == null) return;
            _abductor = null;
            _abductT = 0f;
            _verticalVel = 0f;                       // let gravity take over from a standstill
            _falling = true;
            _fallPeakY = transform.position.y;
            if (input != null && _beamSuppressedInput) { input.Suppressed = false; _beamSuppressedInput = false; }
        }

        /// <summary>True while a saucer has this player off the ground — the HUD/AI can read it, and the
        /// hazard checks it so two UFOs never fight over the same victim.</summary>
        public bool IsAbducted => _abductor != null;

        /// <summary>OWNER. Apply an item effect locally — a timed speed buff or an instant stamina refill.</summary>
        [Rpc(SendTo.Owner)]
        public void ApplyItemEffectRpc(bool isSpeed, float magnitude, float duration)
        {
            if (isSpeed) ApplyTempSpeedBuff(1f + magnitude, duration);
            else RefillStamina(magnitude);
        }

        /// <summary>This player's assigned team color (the same palette spectators read), exposed for HUD /
        /// head-indicator tinting. Safe on remote copies — ColorIndex is replicated (defaults to 0 pre-sync).</summary>
        public Color CurrentColor => Palette[Mathf.Clamp(ColorIndex.Value, 0, Palette.Length - 1)];

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
        private bool _isBot;
        private Vector3 _shoveVel; // owner-local blast knockback (decays each frame), added on top of input
        private DashState _dash = DashStamina.Initial; // owner-local dash stamina (NetworkTransform replicates the resulting motion)
        private float _itemBuffMult = 1f, _itemBuffT = 0f; // owner-local temporary speed buff from items
        private bool _fpHidden;   // are our own renderers currently hidden for the first-person view?

        // ── UFO abduction (owner-local; the server only decides WHO gets taken) ──
        private Transform _abductor;     // the saucer we are hanging from (null = not abducted)
        private float _abductT;          // seconds into the abduction
        private float _abductStartY;     // ground height we were plucked from
        private bool  _falling;          // released and on the way down — waiting to measure the landing
        private float _fallPeakY;
        private bool  _beamSuppressedInput;   // only WE may clear Suppressed, never someone else's overlay

        /// <summary>SERVER. Flag this instance as an AI bot BEFORE <c>NetworkObject.Spawn()</c> so
        /// OnNetworkSpawn drives it with an <see cref="AiBrain"/> instead of physical input.</summary>
        public void MarkAsBot() => _isBot = true;

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            if (input == null) input = GetComponent<PlayerInputReader>();
            if (carry == null) carry = GetComponent<PlayerCarryController>();
            if (inventory == null) inventory = GetComponent<SlopCo.Items.PlayerInventory>();
        }

        public override void OnNetworkSpawn()
        {
            ApplyColor(ColorIndex.Value);
            ColorIndex.OnValueChanged += OnColorChanged;

            if (IsOwner)
            {
                // Owner-local knockback: only the owner shoves its own CC (NetworkTransform replicates the
                // result). Bots are owned by the server, so they get flung too — intended comedy.
                CargoBomb.OnDetonated += OnBlast;
                if (_isBot)
                {
                    // AI teammate: an AiBrain feeds input; no physical devices, no camera.
                    var brain = GetComponent<AiBrain>();
                    if (brain != null) brain.enabled = true;
                    input?.UseAi(brain);
                }
                else
                {
                    input?.Enable();
                    _cam = Camera.main;
                    LocalHuman = this;   // the real local human (never a bot) — DashGauge reads this
                }
            }
            else
            {
                input?.Disable();
            }
        }

        public override void OnNetworkDespawn()
        {
            ColorIndex.OnValueChanged -= OnColorChanged;
            if (IsOwner)
            {
                CargoBomb.OnDetonated -= OnBlast;
                input?.Disable();
            }
            if (LocalHuman == this) LocalHuman = null;
        }

        // A bomb went off — fling this body away from the blast (owner-local; horizontal falloff + a pop so
        // it leaves the ground). No server RPC: the owner-authoritative transform would snap a server push back.
        private void OnBlast(Vector3 pos)
        {
            if (!IsOwner) return;
            Vector3 imp = ExplosionShove.Impulse(transform.position, pos,
                              GameConstants.BlastKnockbackRadius, GameConstants.BlastKnockbackSpeed);
            if (imp.sqrMagnitude <= 0.0001f) return;          // out of range — no pop, no shove
            imp.y = GameConstants.BlastKnockbackPopUp;          // pop rides _shoveVel.y, NOT _verticalVel
            _shoveVel += imp;
        }

        private void Update()
        {
            if (!IsOwner || input == null) return;

            // Viewpoint toggle (third ⇄ first). Persisted immediately so the choice survives the session.
            if (input.TogglePovPressed)
            {
                SettingsManager.SetFirstPerson(!SettingsManager.FirstPerson);
                SettingsManager.Save();
            }

            // Off the ground in a tractor beam: the saucer owns our motion until it lets go.
            if (_abductor != null) { TickAbduction(); return; }

            if (inventory != null)
            {
                if (input.UseConsumablePressed) inventory.RequestUseConsumableRpc();
                if (input.UsePermanentPressed)  inventory.RequestUsePermanentRpc();
                if (input.DiscardPressed)       inventory.RequestDiscardConsumableRpc();
                if (input.CyclePressed)         inventory.RequestCyclePermanentRpc();
            }

            Vector2 m = input.Move;
            Vector3 dir = new Vector3(m.x, 0f, m.y);
            if (dir.sqrMagnitude > 1f) dir.Normalize();

            var aug = ServiceLocator.Get<SlopCo.Gameplay.AugmentSystem>();
            float speed = GameConstants.PlayerMoveSpeed * (aug != null ? aug.MoveSpeedMult : 1f) *
                          (carry != null && carry.IsCarrying
                              ? GameConstants.PlayerCarrySpeedMultiplier * (aug != null ? aug.CarrySpeedMult : 1f)
                              : 1f);

            // Dash: hold-to-sprint drains stamina; full depletion forces a brief exhausted stop (SpeedMult 0).
            bool moving = dir.sqrMagnitude > 0.0001f;
            _dash = DashStamina.Step(_dash, Time.deltaTime, input.DashHeld, moving,
                        GameConstants.DashDrainPerSecond, GameConstants.DashRegenPerSecond, GameConstants.DashExhaustSeconds);
            speed *= DashStamina.SpeedMult(_dash, GameConstants.DashSpeedMultiplier);
            if (_itemBuffT > 0f) { _itemBuffT -= Time.deltaTime; speed *= _itemBuffMult; }   // item speed buff (Fancy Shoes / Hype Horn)

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
            // Add the owner-local blast shove (horizontal + vertical pop), then bleed it off.
            _cc.Move((velocity + _shoveVel) * Time.deltaTime);
            _shoveVel = Vector3.MoveTowards(_shoveVel, Vector3.zero, GameConstants.BlastKnockbackDecay * Time.deltaTime);
            PlanarVelocity = new Vector3(horizontal.x, 0f, horizontal.z);
            TrackLanding();

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

            // Third person = the pulled-back 3/4 view (world + horizon); first person = head-height down
            // the facing direction. CameraRig owns the placement math (pure, unit-tested).
            bool fp = SettingsManager.FirstPerson;
            ApplyFirstPersonVisibility(fp);
            Vector3 desired = CameraRig.DesiredPosition(fp, t.position, transform.forward);
            // First person is rigid on purpose: smoothing the eye position lags the body and reads as drift.
            float k = fp ? 1f : 1f - Mathf.Exp(-10f * Time.deltaTime);
            _cam.transform.position = Vector3.Lerp(_cam.transform.position, desired, k);
            Vector3 look = CameraRig.DesiredLookAt(fp, t.position, transform.forward) - _cam.transform.position;
            if (look.sqrMagnitude > 0.001f)
                _cam.transform.rotation = Quaternion.Slerp(_cam.transform.rotation, Quaternion.LookRotation(look), k);

            // Camera shake (smashes/deliveries add trauma; the owner camera samples + applies it).
            ScreenShake.Sample(Time.deltaTime, out Vector2 shakeOff, out float roll);
            _cam.transform.position += _cam.transform.right * shakeOff.x + _cam.transform.up * shakeOff.y;
            _cam.transform.rotation *= Quaternion.Euler(0f, 0f, roll);
        }

        // OWNER. One frame of the beam: rise toward the saucer, hang under it, then get dropped.
        private void TickAbduction()
        {
            if (input != null) { input.Suppressed = true; _beamSuppressedInput = true; }
            _abductT += Time.deltaTime;

            var phase = SlopCo.Gameplay.AbductionMath.Phase(_abductT, GameConstants.UfoLiftSeconds, GameConstants.UfoHoldSeconds);
            if (phase == SlopCo.Gameplay.AbductPhase.Done) { ReleaseFromBeam(); return; }

            Vector3 hang = _abductor.position - Vector3.up * GameConstants.UfoCarryGap;
            Vector3 here = transform.position;
            Vector3 target = phase == SlopCo.Gameplay.AbductPhase.Lift
                ? Vector3.Lerp(new Vector3(here.x, _abductStartY, here.z), hang,
                               SlopCo.Gameplay.AbductionMath.LiftT(_abductT, GameConstants.UfoLiftSeconds))
                : hang;

            _cc.Move(target - here);
            _verticalVel = 0f;
            PlanarVelocity = Vector3.zero;
        }

        // OWNER. Measure the drop and bill the landing to stamina — the whole point of being abducted.
        private void TrackLanding()
        {
            if (!_falling) return;
            float y = transform.position.y;
            if (y > _fallPeakY) _fallPeakY = y;
            if (!_cc.isGrounded) return;

            _falling = false;
            float drop = _fallPeakY - y;
            float cost = SlopCo.Gameplay.AbductionMath.StaminaPenalty(drop, GameConstants.FallFreeHeight,
                             GameConstants.FallStaminaPerMetre, GameConstants.FallStaminaMax);
            if (cost <= 0f) return;
            _dash = DashStamina.Drain(_dash, cost, GameConstants.DashExhaustSeconds);
            ScreenShake.Add(Mathf.Clamp(cost, 0.2f, 0.8f));
        }

        // First person: hide OUR OWN character mesh so the head/torso doesn't fill the view. Local-only —
        // this instance's renderers on this machine; every other client still sees the full body.
        private void ApplyFirstPersonVisibility(bool firstPerson)
        {
            if (_fpHidden == firstPerson || tintRenderers == null) return;
            _fpHidden = firstPerson;
            foreach (var r in tintRenderers)
                if (r != null) r.enabled = !firstPerson;
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
