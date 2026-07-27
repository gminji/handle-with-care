using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using SlopCo.Core;
using SlopCo.Gameplay;

namespace SlopCo.UI
{
    /// <summary>
    /// The map picker, in two modes.
    ///
    /// OFFLINE (solo / co-op-with-AI / tutorial): shown from the menu after a mode is chosen. Each button
    /// commits its map index (Random = -1) via <see cref="UIManager.ChooseMap"/>, which launches the pending
    /// mode. Pick-and-go, no highlight.
    ///
    /// ONLINE: matchmaking happens FIRST, so this appears inside the lobby once the crew is connected and
    /// every click is a VOTE (<see cref="MapManager.SubmitMapVoteRpc"/>) with a live tally on the buttons.
    /// The host's START resolves it — ties are broken at random, server-side. In this mode the panel slides
    /// to a right-hand column so it never sits on top of the lobby card.
    /// All refs optional (null-checked).
    /// </summary>
    public sealed class MapSelectUI : MonoBehaviour
    {
        [SerializeField] private UIManager manager;
        [Tooltip("One button per concrete map, in map-index order.")]
        [SerializeField] private Button[] mapButtons;
        [SerializeField] private Button randomButton;
        [SerializeField] private Text titleText;

        // Menu layout (as authored) vs the lobby-vote column, captured on first use so the authored
        // positions stay the single source of truth.
        private Vector2[] _menuButtonPos;
        private Vector2 _menuRandomPos, _menuTitlePos;
        private bool _captured;
        private bool _voteLayout;
        private int _myVote = -1;

        private void Awake()
        {
            if (mapButtons != null)
                for (int i = 0; i < mapButtons.Length; i++)
                {
                    int idx = i; // capture per-iteration
                    if (mapButtons[i] != null) mapButtons[i].onClick.AddListener(() => Pick(idx));
                }
            if (randomButton != null) randomButton.onClick.AddListener(() => Pick(-1));
        }

        private void OnEnable() => _myVote = -1;

        private void Pick(int idx)
        {
            if (VoteActive())
            {
                if (idx < 0) return;                       // no "random" ballot — that is the tie-break's job
                _myVote = idx;
                ServiceLocator.Get<MapManager>()?.SubmitMapVoteRpc(idx);
                return;
            }
            if (manager != null) manager.ChooseMap(idx);
            else GameModeState.SelectedMap = idx;           // standalone fallback
        }

        private static bool VoteActive()
        {
            var mm = ServiceLocator.Get<MapManager>();
            return mm != null && mm.CrewVoteActive;
        }

        private void Update()
        {
            bool vote = VoteActive();
            ApplyLayout(vote);
            if (vote) PaintTally();
        }

        private void CaptureMenuLayout()
        {
            if (_captured) return;
            _captured = true;
            if (mapButtons != null)
            {
                _menuButtonPos = new Vector2[mapButtons.Length];
                for (int i = 0; i < mapButtons.Length; i++)
                    if (mapButtons[i] != null) _menuButtonPos[i] = ((RectTransform)mapButtons[i].transform).anchoredPosition;
            }
            if (randomButton != null) _menuRandomPos = ((RectTransform)randomButton.transform).anchoredPosition;
            if (titleText != null) _menuTitlePos = ((RectTransform)titleText.transform).anchoredPosition;
        }

        // Vote mode parks the picker in a right-hand column; the lobby card owns the centre of the screen.
        private void ApplyLayout(bool vote)
        {
            CaptureMenuLayout();
            if (vote == _voteLayout) return;
            _voteLayout = vote;

            if (titleText != null)
            {
                ((RectTransform)titleText.transform).anchoredPosition = vote ? new Vector2(560f, 250f) : _menuTitlePos;
                var loc = titleText.GetComponent<LocalizedText>();
                if (loc != null) loc.SetKey(vote ? "map.vote" : "map.select");
            }
            if (mapButtons != null)
                for (int i = 0; i < mapButtons.Length; i++)
                {
                    if (mapButtons[i] == null) continue;
                    var rt = (RectTransform)mapButtons[i].transform;
                    rt.anchoredPosition = vote ? new Vector2(560f, 130f - i * 90f)
                                               : (_menuButtonPos != null && i < _menuButtonPos.Length ? _menuButtonPos[i] : rt.anchoredPosition);
                }
            // Random is a menu-only shortcut: in a crew vote the tie-break already covers "we can't agree".
            if (randomButton != null)
            {
                randomButton.gameObject.SetActive(!vote);
                ((RectTransform)randomButton.transform).anchoredPosition = _menuRandomPos;
            }
            if (!vote) RestoreLabels();
        }

        private void PaintTally()
        {
            var mm = ServiceLocator.Get<MapManager>();
            if (mm == null || mapButtons == null) return;
            for (int i = 0; i < mapButtons.Length; i++)
            {
                if (mapButtons[i] == null) continue;
                bool real = i < mm.MapCount;
                mapButtons[i].gameObject.SetActive(real);
                if (!real) continue;

                var label = mapButtons[i].GetComponentInChildren<Text>(true);
                if (label == null) continue;
                var loc = label.GetComponent<LocalizedText>();
                if (loc != null) loc.enabled = false;     // we drive the text while the tally is live
                int n = mm.VotesFor(i);
                label.text = Localization.Get(mm.MapNameKey(i)) + (n > 0 ? "   " + n : string.Empty)
                           + (i == _myVote ? "  ◀" : string.Empty);
            }
        }

        private void RestoreLabels()
        {
            if (mapButtons == null) return;
            foreach (var b in mapButtons)
            {
                if (b == null) continue;
                b.gameObject.SetActive(true);
                var label = b.GetComponentInChildren<Text>(true);
                // Re-enabling runs LocalizedText.OnEnable, which re-applies the authored key for us.
                var loc = label != null ? label.GetComponent<LocalizedText>() : null;
                if (loc != null) loc.enabled = true;
            }
        }
    }
}
