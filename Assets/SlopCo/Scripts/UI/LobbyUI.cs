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

        private void Awake()
        {
            if (hostButton != null) hostButton.onClick.AddListener(OnHost);
            if (joinButton != null) joinButton.onClick.AddListener(OnJoin);
            if (quickMatchButton != null) quickMatchButton.onClick.AddListener(OnQuickMatch);
            if (startButton != null) startButton.onClick.AddListener(OnStart);
            if (leaveButton != null) leaveButton.onClick.AddListener(OnLeave);
        }

        private void OnHost() => ServiceLocator.Get<NetworkSessionManager>()?.HostGame();

        private void OnQuickMatch() => ServiceLocator.Get<MatchmakingManager>()?.QuickMatch();

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
            string code = joinCodeField != null ? joinCodeField.text : string.Empty;
            ServiceLocator.Get<NetworkSessionManager>()?.JoinGame(code);
        }

        private void OnStart() => ServiceLocator.Get<RoundManager>()?.RequestStartRpc();

        private void OnLeave() => ServiceLocator.Get<NetworkSessionManager>()?.Leave();

        private void Update()
        {
            HookMatchmaker();
            var nm = NetworkManager.Singleton;
            bool connected = nm != null && nm.IsListening && (nm.IsClient || nm.IsServer);

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
