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
        private bool _autoStartSolo;

        private void Start()
        {
            SettingsManager.Load();
            _screen = Screen.MainMenu;
            Apply();
        }

        // ── Button hooks ───────────────────────────────────────
        public void OnPlay()        { GameModeState.Solo = false; GameModeState.Tutorial = false; _screen = Screen.Lobby; _options = _controls = false; Apply(); }

        /// <summary>Single-player: solo-tuned, host yourself and start immediately — no friend needed.</summary>
        public void OnPlaySolo() => StartSolo(false);

        /// <summary>Guided tutorial: solo + calm fuse + step-by-step coaching.</summary>
        public void OnPlayTutorial() => StartSolo(true);

        /// <summary>Co-op with AI teammates: host yourself, spawn bots, start immediately — no friend needed.</summary>
        public void OnPlayWithAi()
        {
            GameModeState.Solo = false;       // co-op tuning (a bot can co-carry two-person items)
            GameModeState.Tutorial = false;
            GameModeState.WithAi = true;
            GameModeState.BotCount = 1;
            _options = _controls = false;
            _autoStartSolo = true;            // reuse the solo self-host auto-start
            ServiceLocator.Get<SlopCo.Networking.NetworkSessionManager>()?.HostGame();
            Apply();
        }

        private void StartSolo(bool tutorial)
        {
            GameModeState.Solo = true;
            GameModeState.Tutorial = tutorial;
            _options = _controls = false;
            _autoStartSolo = true;
            ServiceLocator.Get<SlopCo.Networking.NetworkSessionManager>()?.HostGame();
            Apply();
        }
        public void OpenOptions()   { _options = true; Apply(); }
        public void CloseOptions()  { _options = false; SettingsManager.Save(); Apply(); }
        public void OpenControls()  { _controls = true; Apply(); }
        public void CloseControls() { _controls = false; Apply(); }
        public void ResumeGame()    { _pause = false; Apply(); }

        public void OnBackToMenu()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening) nm.Shutdown();
            GameModeState.Solo = false;
            GameModeState.Tutorial = false;
            GameModeState.WithAi = false;
            _autoStartSolo = false;
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

            // Solo: once the self-host is up, kick the run off automatically (no lobby wait).
            if (_autoStartSolo && connected && rm != null && rm.Phase.Value == RoundPhase.Lobby)
            {
                rm.RequestStartRpc();
                _autoStartSolo = false;
            }

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
