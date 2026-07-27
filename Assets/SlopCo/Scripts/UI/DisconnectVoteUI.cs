using UnityEngine;
using UnityEngine.UI;
using SlopCo.Core;
using SlopCo.Gameplay;

namespace SlopCo.UI
{
    /// <summary>
    /// The "someone dropped" overlay: tells the remaining crew what happened, shows the live tally and a
    /// countdown, and offers the two buttons. The decision itself belongs to <see cref="DisconnectVote"/> on
    /// the server — this only sends ballots and reports state.
    ///
    /// Owns its own panel visibility (unlike the menu panels, which UIManager drives) because it must be
    /// able to appear over the in-game HUD the instant the server pauses the run. All refs optional.
    /// </summary>
    public sealed class DisconnectVoteUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Text titleText;
        [SerializeField] private Text bodyText;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button endButton;

        private bool _voted;
        private bool _wasPaused;

        private void Awake()
        {
            if (continueButton != null) continueButton.onClick.AddListener(() => Send(false));
            if (endButton != null) endButton.onClick.AddListener(() => Send(true));
            if (panel != null) panel.SetActive(false);
        }

        private void Send(bool endRun)
        {
            _voted = true;
            ServiceLocator.Get<DisconnectVote>()?.SubmitRpc(endRun);
        }

        private void Update()
        {
            var dv = ServiceLocator.Get<DisconnectVote>();
            bool paused = dv != null && dv.Paused.Value;

            if (paused != _wasPaused)
            {
                _wasPaused = paused;
                if (paused) _voted = false;          // fresh ballot each time someone drops
                if (panel != null) panel.SetActive(paused);
            }
            if (!paused || dv == null) return;

            if (titleText != null) titleText.text = Localization.Get("dc.title");

            int keep = dv.VotesFor(DisconnectVote.SlotContinue);
            int end  = dv.VotesFor(DisconnectVote.SlotEnd);
            if (bodyText != null)
            {
                string body = Localization.Get("dc.body").Replace("{n}", dv.DroppedCount.Value.ToString());
                body += "\n\n" + Localization.Get("dc.tally")
                        .Replace("{keep}", keep.ToString())
                        .Replace("{end}", end.ToString());
                body += "\n" + Localization.Get("dc.timer").Replace("{s}", Mathf.CeilToInt(dv.TimeLeft.Value).ToString());
                if (_voted) body += "   " + Localization.Get("dc.waiting");
                bodyText.text = body;
            }

            if (continueButton != null) continueButton.interactable = !_voted;
            if (endButton != null) endButton.interactable = !_voted;
        }
    }
}
