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
        [SerializeField] private GameObject mapSelectPanel;
        [Header("Overlays")]
        [SerializeField] private GameObject optionsPanel;
        [SerializeField] private GameObject pausePanel;
        [SerializeField] private GameObject controlsPanel;

        private enum Screen { MainMenu, Lobby, InGame }
        private enum PendingMode { None, Solo, Ai, Online }
        private Screen _screen = Screen.MainMenu;
        private bool _options, _pause, _controls;
        private bool _mapSelect;
        private PendingMode _pending;
        private bool _autoStartSolo;
        private bool _lobbyIntent;   // online: keep the lobby visible while not yet connected (Host/Join here)

        private void Start()
        {
            SettingsManager.Load();
            _screen = Screen.MainMenu;
            Apply();
        }

        // ── Button hooks ───────────────────────────────────────
        // Mode buttons no longer start immediately: they record the intent and open the map picker.
        // Choosing a map then launches the pending mode (see ChooseMap).
        public void OnPlay()       { _pending = PendingMode.Online; ShowMapSelect(); }

        /// <summary>Single-player: solo-tuned, host yourself — after picking a map.</summary>
        public void OnPlaySolo()   { _pending = PendingMode.Solo;   ShowMapSelect(); }

        /// <summary>Co-op with AI teammates: host + bots — after picking a map.</summary>
        public void OnPlayWithAi() { _pending = PendingMode.Ai;     ShowMapSelect(); }

        /// <summary>Guided tutorial: solo + calm fuse — fixed flow, bypasses map select.</summary>
        public void OnPlayTutorial() => StartSolo(true);

        private void ShowMapSelect() { _options = _controls = false; _mapSelect = true; Apply(); }

        /// <summary>Map button (or Random = -1) chosen → commit the map and launch the pending mode.</summary>
        public void ChooseMap(int idx)
        {
            GameModeState.SelectedMap = idx;
            _mapSelect = false;
            switch (_pending)
            {
                case PendingMode.Solo:   StartSolo(false);   break;
                case PendingMode.Ai:     StartWithAi();      break;
                case PendingMode.Online: EnterOnlineLobby(); break;
                default:                 Apply();            break;
            }
            _pending = PendingMode.None;
        }

        public void OnBackFromMapSelect() { _mapSelect = false; _pending = PendingMode.None; Apply(); }

        private void StartSolo(bool tutorial)
        {
            GameModeState.Solo = true;
            GameModeState.Tutorial = tutorial;
            GameModeState.WithAi = false;
            _options = _controls = _mapSelect = false;
            _autoStartSolo = true;
            ServiceLocator.Get<SlopCo.Networking.NetworkSessionManager>()?.HostGame();
            Apply();
        }

        private void StartWithAi()
        {
            GameModeState.Solo = false;       // co-op tuning (a bot can co-carry two-person items)
            GameModeState.Tutorial = false;
            GameModeState.WithAi = true;
            GameModeState.BotCount = 1;
            _options = _controls = _mapSelect = false;
            _autoStartSolo = true;            // reuse the solo self-host auto-start
            ServiceLocator.Get<SlopCo.Networking.NetworkSessionManager>()?.HostGame();
            Apply();
        }

        private void EnterOnlineLobby()
        {
            GameModeState.Solo = false;
            GameModeState.Tutorial = false;
            GameModeState.WithAi = false;
            _lobbyIntent = true;   // online hosts/joins from the lobby — keep it shown until connected
            _screen = Screen.Lobby;
            _options = _controls = _mapSelect = false;
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
            _lobbyIntent = false;
            _screen = Screen.MainMenu;
            _pause = _options = _controls = _mapSelect = false;
            _pending = PendingMode.None;
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
            {
                _lobbyIntent = false;                                  // in a session now
                _screen = inRound ? Screen.InGame : Screen.Lobby;
            }
            else if (_lobbyIntent)
                _screen = Screen.Lobby;                               // online: stay in the lobby to Host/Join
            else if (_screen != Screen.MainMenu)
                _screen = Screen.MainMenu;                            // dropped/left → back to menu

            // ESC: close an overlay, else toggle pause while connected.
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                if (_options) _options = false;
                else if (_controls) _controls = false;
                else if (_mapSelect) { _mapSelect = false; _pending = PendingMode.None; }
                else if (_lobbyIntent) { _lobbyIntent = false; _screen = Screen.MainMenu; } // back out of online lobby
                else if (connected) _pause = !_pause;
            }
            if (!connected) _pause = false; // never pause outside a session

            Apply();
        }

        private void Apply()
        {
            bool mainMenu = _screen == Screen.MainMenu;
            if (mainMenuPanel != null) mainMenuPanel.SetActive(mainMenu && !_options && !_controls && !_mapSelect);
            if (mapSelectPanel != null) mapSelectPanel.SetActive(mainMenu && _mapSelect && !_options && !_controls);
            if (lobbyPanel != null)    lobbyPanel.SetActive(_screen == Screen.Lobby && !_options);
            if (hudRoot != null)       hudRoot.SetActive(_screen == Screen.InGame);
            if (optionsPanel != null)  optionsPanel.SetActive(_options);
            if (controlsPanel != null) controlsPanel.SetActive(_controls);
            if (pausePanel != null)    pausePanel.SetActive(_pause && !_options && !_controls);
        }
    }
}
