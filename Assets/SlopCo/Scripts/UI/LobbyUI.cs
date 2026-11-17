using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using SlopCo.Core;
using SlopCo.Networking;
using SlopCo.Gameplay;

namespace SlopCo.UI
{
    /// <summary>
    /// The lobby panel: Host / Join(code) / Start / Leave. Talks to NetworkSessionManager (transport
    /// agnostic) and RoundManager (host-only start). Shows only during Lobby/GameOver phases. All
    /// references optional. (For Steam builds the Join code path is replaced by overlay invites.)
    /// </summary>
    public sealed class LobbyUI : MonoBehaviour
    {
        [SerializeField] private GameObject lobbyPanel;
        [SerializeField] private Button hostButton;
        [SerializeField] private Button joinButton;
        [SerializeField] private Button quickMatchButton;
        [SerializeField] private Button startButton;
        [SerializeField] private Button leaveButton;
        [SerializeField] private InputField joinCodeField;
        [SerializeField] private Text statusText;
        [SerializeField] private Text playerCountText;

        private bool _mmHooked;
        private string _matchStatus = string.Empty;

        // Why the host turned our last join away (a localization key from JoinPolicy, sent as the NGO disconnect
        // reason). Shown in place of "OFFLINE" until the next connect attempt or a successful connection.
        private NetworkManager _netHooked;
        private string _refusalKey;
        private UIManager _ui;

        private void Awake()
        {
            if (hostButton != null) hostButton.onClick.AddListener(OnHost);
            if (joinButton != null) joinButton.onClick.AddListener(OnJoin);
            if (quickMatchButton != null) quickMatchButton.onClick.AddListener(OnQuickMatch);
            if (startButton != null) startButton.onClick.AddListener(OnStart);
            if (leaveButton != null) leaveButton.onClick.AddListener(OnLeave);
        }

        private void OnHost()
        {
            _refusalKey = null;
            ServiceLocator.Get<NetworkSessionManager>()?.HostGame();
        }

        private void OnQuickMatch()
        {
            _refusalKey = null;
            ServiceLocator.Get<MatchmakingManager>()?.QuickMatch();
        }

        // NetworkManager may not exist yet when this panel wakes, so hook its disconnect event lazily (same as
        // the matchmaker above).
        private void HookNetwork()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm == _netHooked) return;
            nm.OnClientDisconnectCallback += HandleClientDisconnect;
            _netHooked = nm;
        }

        private void OnDestroy()
        {
            if (_netHooked != null) _netHooked.OnClientDisconnectCallback -= HandleClientDisconnect;
        }

        private void HandleClientDisconnect(ulong _)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.IsServer) return;   // only a client that was turned away has a reason to read
            string reason = nm.DisconnectReason;
            if (JoinPolicy.IsRefusal(reason)) _refusalKey = reason;
        }

        // MatchmakingManager registers in its own Awake, so hook its status event lazily.
        private void HookMatchmaker()
        {
            if (_mmHooked) return;
            var mm = ServiceLocator.Get<MatchmakingManager>();
            if (mm == null) return;
            mm.OnStatus += s => _matchStatus = s;
            _mmHooked = true;
        }

        private void OnJoin()
        {
            _refusalKey = null;
            string code = joinCodeField != null ? joinCodeField.text : string.Empty;
            ServiceLocator.Get<NetworkSessionManager>()?.JoinGame(code);
        }

        private void OnStart() => ServiceLocator.Get<RoundManager>()?.RequestStartRpc();

        private void OnLeave() => ServiceLocator.Get<NetworkSessionManager>()?.Leave();

        private void Update()
        {
            HookMatchmaker();
            HookNetwork();
            var nm = NetworkManager.Singleton;
            bool connected = nm != null && nm.IsListening && (nm.IsClient || nm.IsServer);
            if (connected && nm.IsConnectedClient) _refusalKey = null;   // in a session now — the old refusal is moot
            // Back on the title screen: don't greet the next lobby visit with a refusal from an earlier attempt.
            // (Not "the panel is hidden" — the Options and Help overlays hide it too, and the reason has to
            // still be there when they close.)
            if (_ui == null) _ui = FindFirstObjectByType<UIManager>();
            if (_ui != null && _ui.OnTitleScreen) _refusalKey = null;

            var rm = ServiceLocator.Get<RoundManager>();
            bool lobbyPhase = rm == null
                              || rm.Phase.Value == RoundPhase.Lobby
                              || rm.Phase.Value == RoundPhase.GameOver;

            var mm = ServiceLocator.Get<MatchmakingManager>();
            bool matching = mm != null && mm.IsMatching;

            // Panel visibility is owned by UIManager now; LobbyUI only drives its own controls.
            if (hostButton != null) hostButton.interactable = !connected && !matching;
            if (joinButton != null) joinButton.interactable = !connected && !matching;
            if (quickMatchButton != null) quickMatchButton.interactable = !connected && !matching;
            if (leaveButton != null) leaveButton.interactable = connected;
            if (startButton != null)
                startButton.gameObject.SetActive(connected && nm.IsServer && lobbyPhase);

            if (statusText != null)
            {
                string s;
                if (matching)
                {
                    s = _matchStatus; // searching / joining / hosting…
                }
                else if (!connected && !string.IsNullOrEmpty(_refusalKey))
                {
                    s = Localization.Get(_refusalKey);   // e.g. "haul in progress — join at payout"
                }
                else
                {
                    s = Localization.Get(connected ? (nm.IsServer ? "lobby.hosting" : "lobby.connected") : "lobby.offline");
                    if (connected && nm.IsServer)
                    {
                        var sess = ServiceLocator.Get<NetworkSessionManager>()?.Session;
                        if (sess != null) s += "   " + sess.LobbyDisplayCode; // share this code with friends to join
                    }
                }
                statusText.text = s;
            }
            if (playerCountText != null)
                playerCountText.text = connected
                    ? $"Players {nm.ConnectedClientsIds.Count}/{GameConstants.MaxPlayers}"
                    : string.Empty;
        }
    }
}
