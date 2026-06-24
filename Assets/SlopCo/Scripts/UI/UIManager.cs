using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using SlopCo.Core;
using SlopCo.Gameplay;

namespace SlopCo.UI
{
    /// <summary>
    /// The front-end shell / screen-state owner. Drives which top-level panel is visible
    /// (MainMenu → Lobby → in-game HUD) plus the Options, Pause and Controls overlays. ESC opens/closes
    /// Pause while in a session. This is the single owner of panel visibility — LobbyUI no longer toggles
    /// its own panel. Buttons call the public methods here.
    /// </summary>
    public sealed class UIManager : MonoBehaviour
    {
        [Header("Screens")]
        [SerializeField] private GameObject mainMenuPanel;
        [SerializeField] private GameObject lobbyPanel;
        [SerializeField] private GameObject hudRoot;
        [Header("Overlays")]
        [SerializeField] private GameObject optionsPanel;
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject controlsPanel;

        private enum Screen { MainMenu, Lobby, InGame }
        private Screen _screen = Screen.MainMenu;
        private bool _options, _pause, _controls;

        private void Start()
        {
            SettingsManager.Load();
            _screen = Screen.MainMenu;
            Apply();
        }

        // ── Button hooks ───────────────────────────────────────
        public void OnPlay()        { _screen = Screen.Lobby; _options = _controls = false; Apply(); }
        public void OpenOptions()   { _options = true; Apply(); }
        public void CloseOptions()  { _options = false; SettingsManager.Save(); Apply(); }
        public void OpenControls()  { _controls = true; Apply(); }
        public void CloseControls() { _controls = false; Apply(); }
        public void ResumeGame()    { _pause = false; Apply(); }

        public void OnBackToMenu()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening) nm.Shutdown();
            _screen = Screen.MainMenu;
            _pause = _options = _controls = false;
            Apply();
        }

        public void OnQuit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void Update()
        {
            // Derive the base screen from session state.
            var nm = NetworkManager.Singleton;
            bool connected = nm != null && nm.IsListening && (nm.IsClient || nm.IsServer);
            var rm = ServiceLocator.Get<RoundManager>();
            bool inRound = rm != null && rm.Phase.Value != RoundPhase.Lobby && rm.Phase.Value != RoundPhase.GameOver;

            if (connected)
                _screen = inRound ? Screen.InGame : Screen.Lobby;
            else if (_screen != Screen.MainMenu)
                _screen = Screen.MainMenu; // dropped/left → back to menu

            // ESC: close an overlay, else toggle pause while connected.
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                if (_options) _options = false;
                else if (_controls) _controls = false;
                else if (connected) _pause = !_pause;
            }
            if (!connected) _pause = false; // never pause outside a session

            Apply();
        }

        private void Apply()
        {
            if (mainMenuPanel != null) mainMenuPanel.SetActive(_screen == Screen.MainMenu && !_options && !_controls);
            if (lobbyPanel != null)    lobbyPanel.SetActive(_screen == Screen.Lobby && !_options);
            if (hudRoot != null)       hudRoot.SetActive(_screen == Screen.InGame);
            if (optionsPanel != null)  optionsPanel.SetActive(_options);
            if (controlsPanel != null) controlsPanel.SetActive(_controls);
            if (pausePanel != null)    pausePanel.SetActive(_pause && !_options && !_controls);
        }
    }
}
