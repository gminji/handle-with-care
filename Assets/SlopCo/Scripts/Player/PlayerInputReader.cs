using UnityEngine;
using UnityEngine.InputSystem;

namespace SlopCo.Player
{
    /// <summary>
    /// Reads input via code-defined InputActions (keyboard + gamepad) so there is NO .inputactions
    /// binary asset dependency — the script compiles and runs standalone. Owner-only; enabled by
    /// PlayerController when IsOwner. Throw is a hold-to-charge / release-to-fire verb.
    /// </summary>
    public sealed class PlayerInputReader : MonoBehaviour
    {
        public Vector2 Move { get; private set; }
        public bool JumpPressed { get; private set; }
        public bool GrabHeld { get; private set; }
        public bool ThrowReleasedThisFrame { get; private set; }
        public float ThrowCharge01 { get; private set; }
        public bool DashHeld { get; private set; }

        /// <summary>When true, movement/jump/throw are zeroed but GRAB is preserved — lets a transient overlay
        /// (e.g. the ping/emote wheel) freeze the player WITHOUT dropping carried cargo. Owner-local.</summary>
        public bool Suppressed { get; set; }

        private const float MaxThrowChargeTime = 1.2f;

        private InputAction _move, _jump, _grab, _throw, _dash;
        private bool _enabled;
        private float _throwHeldTime;

        // AI mode: an AiBrain drives this reader instead of physical devices (bots reuse the human pipeline).
        private AiBrain _ai;
        private bool _aiMode;

        /// <summary>Switch this reader to AI-driven input (called by PlayerController for bots).</summary>
        public void UseAi(AiBrain brain) { _ai = brain; _aiMode = true; }

        private void Awake()
        {
            _move = new InputAction("Move", InputActionType.Value);
            _move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            _move.AddCompositeBinding("2DVector")   // arrow keys too
                .With("Up", "<Keyboard>/upArrow")
                .With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow")
                .With("Right", "<Keyboard>/rightArrow");
            _move.AddBinding("<Gamepad>/leftStick");

            _jump = new InputAction("Jump", InputActionType.Button, "<Keyboard>/space");
            _jump.AddBinding("<Gamepad>/buttonSouth");

            _grab = new InputAction("Grab", InputActionType.Button, "<Keyboard>/e");
            _grab.AddBinding("<Gamepad>/rightShoulder");

            _throw = new InputAction("Throw", InputActionType.Button, "<Mouse>/leftButton");
            _throw.AddBinding("<Gamepad>/rightTrigger");

            _dash = new InputAction("Dash", InputActionType.Button, "<Keyboard>/leftShift");
            _dash.AddBinding("<Gamepad>/leftShoulder");
        }

        public void Enable()
        {
            if (_enabled) return;
            _move.Enable(); _jump.Enable(); _grab.Enable(); _throw.Enable(); _dash.Enable();
            _enabled = true;
        }

        public void Disable()
        {
            if (!_enabled) return;
            _move.Disable(); _jump.Disable(); _grab.Disable(); _throw.Disable(); _dash.Disable();
            _enabled = false;
        }

        private void OnDestroy()
        {
            _move?.Dispose(); _jump?.Dispose(); _grab?.Dispose(); _throw?.Dispose(); _dash?.Dispose();
        }

        private void Update()
        {
            // AI-driven (bot): pull intents from the brain, ignore physical devices entirely.
            if (_aiMode)
            {
                Move = _ai != null ? _ai.Move : Vector2.zero;
                JumpPressed = _ai != null && _ai.WantJump;
                GrabHeld = _ai != null && _ai.GrabHeld;
                DashHeld = false;
                ThrowReleasedThisFrame = false; ThrowCharge01 = 0f;
                return;
            }

            if (!_enabled)
            {
                Move = Vector2.zero; JumpPressed = false; GrabHeld = false; DashHeld = false;
                ThrowReleasedThisFrame = false; ThrowCharge01 = 0f; _throwHeldTime = 0f;
                return;
            }

            Move = _move.ReadValue<Vector2>();
            JumpPressed = _jump.WasPressedThisFrame();
            GrabHeld = _grab.IsPressed();
            DashHeld = _dash.IsPressed();

            ThrowReleasedThisFrame = _throw.WasReleasedThisFrame();
            if (_throw.IsPressed())
            {
                _throwHeldTime += Time.deltaTime;
                ThrowCharge01 = Mathf.Clamp01(_throwHeldTime / MaxThrowChargeTime);
            }
            else if (ThrowReleasedThisFrame)
            {
                // keep ThrowCharge01 at its final value this frame so the carry controller can read it
            }
            else
            {
                _throwHeldTime = 0f;
                ThrowCharge01 = 0f;
            }

            if (Suppressed)
            {
                // Freeze locomotion/throw for a transient overlay (ping/emote wheel) but KEEP GrabHeld so the
                // player never drops carried cargo merely by opening the wheel.
                Move = Vector2.zero;
                JumpPressed = false;
                DashHeld = false;
                ThrowReleasedThisFrame = false;
                ThrowCharge01 = 0f;
                _throwHeldTime = 0f;
            }
        }
    }
}
