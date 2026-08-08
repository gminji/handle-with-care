using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using SlopCo.Core;
using SlopCo.Player;

namespace SlopCo.UI
{
    /// <summary>
    /// Owner-local radial comms wheel. Disabled by default; <see cref="PingEmoteController"/> enables it only on
    /// the local human's player. Hold the wheel key (C / gamepad Select) to open a 6-slice overlay — built once at
    /// runtime, so there is no scene art to wire — point at a slice (mouse position or right-stick) and release to
    /// fire. Selection uses the pure, unit-tested <see cref="WheelMath"/>. While open, player move/throw is frozen
    /// (<see cref="PlayerInputReader.Disable"/>) and the cursor is shown; both are restored on every close path
    /// (release, force-close, disable). Pings raycast the aimed world point; emotes / gamepad / a ray miss fall
    /// back to the player's own position ("rally here").
    /// </summary>
    public sealed class PingEmoteWheel : MonoBehaviour
    {
        [SerializeField] private PingEmoteController controller;
        [SerializeField] private PlayerInputReader inputReader;

        private InputAction _open;
        private Camera _cam;
        private UIManager _ui;

        private bool _isOpen;
        private int _selected = -1;

        // Runtime overlay (built lazily, reused).
        private Canvas _canvas;
        private Text[] _labels;

        private void Awake()
        {
            if (controller == null) controller = GetComponent<PingEmoteController>();
            if (inputReader == null) inputReader = GetComponent<PlayerInputReader>();

            _open = new InputAction("PingWheel", InputActionType.Button, "<Keyboard>/c");
            _open.AddBinding("<Gamepad>/select");
        }

        private void OnEnable() => _open.Enable();

        private void OnDisable()
        {
            _open.Disable();
            if (_isOpen) Close(send: false);
        }

        private void OnDestroy() => _open?.Dispose();

        private void Update()
        {
            if (_ui == null) _ui = Object.FindFirstObjectByType<UIManager>();
            bool canOpen = _ui != null && _ui.InGameInteractive;

            if (!_isOpen)
            {
                if (canOpen && _open.IsPressed()) Open();
                return;
            }

            // While open: bail out (restoring input/cursor) if the context turns invalid (pause / round end / leave).
            if (!canOpen) { Close(send: false); return; }

            UpdateSelection();

            if (!_open.IsPressed()) Close(send: true);   // release fires the highlighted slice
        }

        private void Open()
        {
            EnsureOverlay();
            _isOpen = true;
            _selected = -1;
            if (_canvas != null) _canvas.gameObject.SetActive(true);
            RefreshLabels();

            if (_ui == null) _ui = Object.FindFirstObjectByType<UIManager>();
            _ui?.SetFreeCursor(this, true);

            if (inputReader != null) inputReader.Suppressed = true;   // freeze move/throw (grab preserved → no cargo drop) while open
        }

        private void Close(bool send)
        {
            if (send && _selected >= 0 && controller != null)
                controller.Send(_selected, ResolveWorldPos(_selected));

            _isOpen = false;
            _selected = -1;
            if (_canvas != null) _canvas.gameObject.SetActive(false);

            // Close() can run outside Update() (e.g. OnDisable), where _ui may not have been resolved yet.
            if (_ui == null) _ui = Object.FindFirstObjectByType<UIManager>();
            _ui?.SetFreeCursor(this, false);

            if (inputReader != null) inputReader.Suppressed = false;
        }

        private void UpdateSelection()
        {
            Vector2 dir;
            var mouse = Mouse.current;
            if (mouse != null)
            {
                Vector2 center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                // WheelRadiusPixels is in canvas REFERENCE units, but the mouse reports raw device pixels.
                // Now that the canvas scales with the screen (see EnsureOverlay), the drawn ring grows with
                // resolution and the two must be reconciled — at 1440p the ring is ~200px wide while the
                // selection maths would still think it is 150, selecting a slice before the cursor reaches it.
                // Computed from Screen.width rather than read off _canvas.scaleFactor, because the scaler
                // writes that in its own Update and it is still 1 on the frame the wheel is re-activated.
                float scale = Screen.width / UITheme.ReferenceResolution.x;   // match = 0 → width drives the scale
                dir = (mouse.position.ReadValue() - center) / (GameConstants.WheelRadiusPixels * scale);
            }
            else
            {
                dir = Gamepad.current != null ? Gamepad.current.rightStick.ReadValue() : Vector2.zero;
            }

            _selected = WheelMath.SelectSlice(dir, PingEmoteController.Items.Length, GameConstants.WheelInnerDeadzone);
            HighlightLabels();
        }

        private Vector3 ResolveWorldPos(int index)
        {
            var items = PingEmoteController.Items;
            if (index < 0 || index >= items.Length) return transform.position;

            if (items[index].Kind == PingEmoteKind.Ping)
            {
                if (_cam == null) _cam = Camera.main;
                var mouse = Mouse.current;
                if (_cam != null && mouse != null)
                {
                    Ray r = _cam.ScreenPointToRay(mouse.position.ReadValue());
                    if (Physics.Raycast(r, out RaycastHit hit, GameConstants.PingAimMaxDistance, ~0, QueryTriggerInteraction.Ignore))
                        return hit.point;
                }
            }
            return transform.position;   // emote / gamepad / ray miss → "rally here"
        }

        // ── Runtime overlay (built once; no scene art asset) ────────────────────
        private void EnsureOverlay()
        {
            if (_canvas != null) return;

            var go = new GameObject("PingEmoteCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(transform, false);   // parented for lifecycle; overlay ignores parent for rendering
            _canvas = go.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = UITheme.SortWheel;

            // The CanvasScaler used to be added and then never configured, leaving it on the default
            // ConstantPixelSize — the only canvas in the game that did not scale with the screen. These four
            // values are exactly what the three authored canvases use (Bootstrap.unity :9929-9934 / :13904-13909
            // / :20162-20167), so the wheel finally matches them.
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = UITheme.ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = UITheme.MatchWidthOrHeight;   // 0 = match width
            scaler.referencePixelsPerUnit = UITheme.RefPPU;

            var bg = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(go.transform, false);
            var bgImg = bg.GetComponent<Image>();
            bgImg.color = UITheme.ScrimLight;   // lighter than the modal Scrim — the world must stay legible behind the wheel
            bgImg.raycastTarget = false;
            var bgRt = bgImg.rectTransform;
            bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = Vector2.zero; bgRt.offsetMax = Vector2.zero;

            var font = (Font)Resources.GetBuiltinResource(typeof(Font), "LegacyRuntime.ttf");
            var items = PingEmoteController.Items;
            _labels = new Text[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                var lblGo = new GameObject("Slice" + i, typeof(RectTransform), typeof(Text));
                lblGo.transform.SetParent(go.transform, false);
                var txt = lblGo.GetComponent<Text>();
                txt.font = font;
                txt.fontSize = UITheme.FontBodyLarge;
                txt.fontStyle = FontStyle.Bold;
                txt.alignment = TextAnchor.MiddleCenter;
                txt.horizontalOverflow = HorizontalWrapMode.Overflow;
                txt.verticalOverflow = VerticalWrapMode.Overflow;
                txt.raycastTarget = false;
                var rt = txt.rectTransform;
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(180f, 70f);
                rt.anchoredPosition = WheelMath.SliceCenterDir(i, items.Length) * GameConstants.WheelRadiusPixels;
                _labels[i] = txt;
            }

            go.SetActive(false);
        }

        private void RefreshLabels()
        {
            if (_labels == null) return;
            var items = PingEmoteController.Items;
            for (int i = 0; i < _labels.Length && i < items.Length; i++)
            {
                if (_labels[i] == null) continue;
                _labels[i].text = items[i].Glyph + "\n" + Localization.Get(items[i].LocKey);
            }
            HighlightLabels();
        }

        private void HighlightLabels()
        {
            if (_labels == null) return;
            var items = PingEmoteController.Items;
            for (int i = 0; i < _labels.Length && i < items.Length; i++)
            {
                if (_labels[i] == null) continue;
                bool sel = i == _selected;
                // Resolved from the theme each time rather than cached on the item, so a future palette swap
                // reaches the wheel (see the Items comment in PingEmoteController).
                Color c = UITheme.WheelColors[i];
                c.a = sel ? 1f : UITheme.DisabledAlpha;
                _labels[i].color = c;
                _labels[i].transform.localScale = Vector3.one * (sel ? UIMotion.WheelSelectedScale : 1f);
            }
        }
    }
}
