using UnityEngine;
using UnityEngine.UI;
using SlopCo.Core;
using SlopCo.Gameplay;

namespace SlopCo.UI
{
    /// <summary>
    /// The end-of-run card — the screenshot streamers post and the natural clip end-point. Shows during
    /// Payout (DAY N SURVIVED / SHORT) and GameOver (FIRED), reading already-replicated cash/quota plus
    /// the <see cref="RunStats"/> tally. Turns the previously dead "PAYOUT" beat into a shareable payoff.
    /// </summary>
    public sealed class ResultsUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Text titleText;
        [SerializeField] private Text bodyText;

        private void OnEnable()
        {
            RoundManager.OnPhaseChanged += HandlePhase;
            if (panel != null) panel.SetActive(false);
        }

        private void OnDisable()
        {
            RoundManager.OnPhaseChanged -= HandlePhase;
        }

        private void HandlePhase(RoundPhase phase)
        {
            if (phase == RoundPhase.Payout || phase == RoundPhase.GameOver) Show(phase);
            else if (panel != null) panel.SetActive(false);
        }

        private void Show(RoundPhase phase)
        {
            if (panel != null) panel.SetActive(true);

            var quota = ServiceLocator.Get<QuotaSystem>();
            var round = ServiceLocator.Get<RoundManager>();
            var stats = ServiceLocator.Get<RunStats>();

            int cash = quota != null ? quota.Cash.Value : 0;
            int target = quota != null ? quota.Quota.Value : 0;
            int day = round != null ? round.RoundNumber.Value : 0;
            bool met = cash >= target;

            if (titleText != null)
            {
                if (phase == RoundPhase.GameOver)
                {
                    titleText.text = "FIRED.";
                    titleText.color = new Color(1f, 0.4f, 0.35f);
                }
                else
                {
                    titleText.text = met ? $"DAY {day} — SURVIVED" : $"DAY {day} — SHORT!";
                    titleText.color = met ? new Color(0.45f, 1f, 0.5f) : new Color(1f, 0.7f, 0.3f);
                }
            }

            if (bodyText != null)
            {
                string b = $"CASH  ${cash}      QUOTA  ${target}\n\n";
                if (stats != null)
                {
                    b += $"Deliveries:  {stats.DeliveryCount}   (+${stats.TotalDelivered})\n";
                    b += $"Cargo destroyed:  ${stats.TotalDestroyed}\n";
                    b += $"Biggest single smash:  -${stats.BiggestSmash}";
                }
                bodyText.text = b;
            }
        }
    }
}
