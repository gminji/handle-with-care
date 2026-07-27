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
        public bool UseConsumablePressed { get; private set; }
        public bool UsePermanentPressed { get; private set; }
        public bool DiscardPressed { get; private set; }
        public bool CyclePressed { get; private set; }
        /// <summary>Viewpoint toggle (third ⇄ first person) pressed this frame.</summary>
        public bool TogglePovPressed { get; private set; }
        /// <summary>Kick pressed this frame — shoves teammates and drives off hazards.</summary>
        public bool KickPressed { get; private set; }

        /// <summary>When true, movement/jump/throw are zeroed but GRAB is preserved — lets a transient overlay
        /// (e.g. the ping/emote wheel) freeze the player WITHOUT dropping carried cargo. Owner-local.</summary>
        public bool Suppressed { get; set; }

        /// <summary>Hard freeze — EVERY intent reads false/zero, including grab and kick. Used while the run
        /// is paused for a disconnect vote, where <see cref="Suppressed"/> is too soft (it deliberately keeps
        /// grab and kick alive). Carried cargo is unaffected: the server keeps the handle, nothing is dropped.</summary>
        public bool Frozen { get; set; }

        private const float MaxThrowChargeTime = 1.2f;

        private InputAction _move, _jump, _grab, _throw, _dash, _useItem, _usePerm, _discard, _cycle, _pov, _kick;
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

            _useItem = new InputAction("UseItem", InputActionType.Button, "<Keyboard>/q");
            _useItem.AddBinding("<Gamepad>/buttonWest");
            _usePerm = new InputAction("UsePermanent", InputActionType.Button, "<Keyboard>/f");
            _usePerm.AddBinding("<Gamepad>/dpad/up");
            _discard = new InputAction("Discard", InputActionType.Button, "<Keyboard>/g");
            _discard.AddBinding("<Gamepad>/dpad/down");
            _cycle = new InputAction("CyclePermanent", InputActionType.Button, "<Keyboard>/r");
            _cycle.AddBinding("<Gamepad>/dpad/left");
            _pov = new InputAction("TogglePov", InputActionType.Button, "<Keyboard>/v");
            _pov.AddBinding("<Gamepad>/rightStickPress");
            _kick = new InputAction("Kick", InputActionType.Button, "<Mouse>/rightButton");
            _kick.AddBinding("<Gamepad>/buttonEast");
        }

        public void Enable()
        {
            if (_enabled) return;
            _move.Enable(); _jump.Enable(); _grab.Enable(); _throw.Enable(); _dash.Enable();
            _useItem.Enable(); _usePerm.Enable(); _discard.Enable(); _cycle.Enable(); _pov.Enable(); _kick.Enable();
            _enabled = true;
        }

        public void Disable()
        {
            if (!_enabled) return;
            _move.Disable(); _jump.Disable(); _grab.Disable(); _throw.Disable(); _dash.Disable();
            _useItem.Disable(); _usePerm.Disable(); _discard.Disable(); _cycle.Disable(); _pov.Disable(); _kick.Disable();
            _enabled = false;
        }

        private void OnDestroy()
        {
            _move?.Dispose(); _jump?.Dispose(); _grab?.Dispose(); _throw?.Dispose(); _dash?.Dispose();
            _useItem?.Dispose(); _usePerm?.Dispose(); _discard?.Dispose(); _cycle?.Dispose(); _pov?.Dispose(); _kick?.Dispose();
        }

        private void Update()
        {
            // Hard freeze (disconnect vote): nobody — human or bot — acts until the crew decides.
            if (Frozen)
            {
                Move = Vector2.zero; JumpPressed = false; GrabHeld = false; DashHeld = false;
                UseConsumablePressed = false; UsePermanentPressed = false; DiscardPressed = false; CyclePressed = false;
                TogglePovPressed = false; KickPressed = false;
                ThrowReleasedThisFrame = false; ThrowCharge01 = 0f; _throwHeldTime = 0f;
                return;
            }

            // AI-driven (bot): pull intents from the brain, ignore physical devices entirely.
            if (_aiMode)
            {
                Move = _ai != null ? _ai.Move : Vector2.zero;
                JumpPressed = _ai != null && _ai.WantJump;
                GrabHeld = _ai != null && _ai.GrabHeld;
                DashHeld = false;
                UseConsumablePressed = false; UsePermanentPressed = false; DiscardPressed = false; CyclePressed = false;
                ThrowReleasedThisFrame = false; ThrowCharge01 = 0f; TogglePovPressed = false; KickPressed = false;
                return;
            }

            if (!_enabled)
            {
                Move = Vector2.zero; JumpPressed = false; GrabHeld = false; DashHeld = false;
                UseConsumablePressed = false; UsePermanentPressed = false; DiscardPressed = false; CyclePressed = false;
                ThrowReleasedThisFrame = false; ThrowCharge01 = 0f; _throwHeldTime = 0f; TogglePovPressed = false; KickPressed = false;
                return;
            }

            Move = _move.ReadValue<Vector2>();
            JumpPressed = _jump.WasPressedThisFrame();
            GrabHeld = _grab.IsPressed();
            DashHeld = _dash.IsPressed();
            UseConsumablePressed = _useItem.WasPressedThisFrame();
            UsePermanentPressed = _usePerm.WasPressedThisFrame();
            DiscardPressed = _discard.WasPressedThisFrame();
            CyclePressed = _cycle.WasPressedThisFrame();
            TogglePovPressed = _pov.WasPressedThisFrame();
            KickPressed = _kick.WasPressedThisFrame();

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
                // player never drops carried cargo merely by opening the wheel. KICK also stays live: being
                // suppressed is exactly the state you're in while a UFO has you, and kicking the saucer is
                // the way out.
                Move = Vector2.zero;
                JumpPressed = false;
                DashHeld = false;
                UseConsumablePressed = false; UsePermanentPressed = false; DiscardPressed = false; CyclePressed = false;
                TogglePovPressed = false;
                ThrowReleasedThisFrame = false;
                ThrowCharge01 = 0f;
                _throwHeldTime = 0f;
            }
        }
    }
}
