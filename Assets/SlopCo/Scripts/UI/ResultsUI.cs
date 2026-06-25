using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using SlopCo.Core;
using SlopCo.Gameplay;
using SlopCo.Networking;

namespace SlopCo.UI
{
    /// <summary>
    /// The end-of-run card — the screenshot streamers post and the natural clip end-point. Shows during
    /// Payout (DAY N SURVIVED / SHORT) and GameOver (FIRED), reading already-replicated cash/quota plus
    /// the <see cref="RunStats"/> tally. On the terminal FIRED card it also surfaces two actions —
    /// Restart (host-only) and Return-to-Title — wired to the existing engine paths
    /// (<see cref="RoundManager.RequestStartRpc"/> / <see cref="Networking.NetworkSessionManager.Leave"/>),
    /// so a fired player can play again or bail out without hunting for the lobby controls.
    /// </summary>
    public sealed class ResultsUI : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Text titleText;
        [SerializeField] private Text bodyText;
        [Header("Game-over actions (shown only on FIRED)")]
        [SerializeField] private Button restartButton;  // host-only
        [SerializeField] private Button menuButton;      // return to title

        private void Awake()
        {
            if (restartButton != null) restartButton.onClick.AddListener(OnRestart);
            if (menuButton != null) menuButton.onClick.AddListener(OnReturnToTitle);
        }

        private void OnEnable()
        {
            RoundManager.OnPhaseChanged += HandlePhase;
            if (panel != null) panel.SetActive(false);
            SetButtons(false);
        }

        private void OnDisable()
        {
            RoundManager.OnPhaseChanged -= HandlePhase;
        }

        private void HandlePhase(RoundPhase phase)
        {
            if (phase == RoundPhase.Payout || phase == RoundPhase.GameOver)
            {
                Show(phase);
            }
            else
            {
                if (panel != null) panel.SetActive(false);
                SetButtons(false);
            }
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
                    if (stats.BestCombo >= 2) b += $"\nBest chain:  x{stats.BestCombo}";
                }

                // --- Personal records: the cross-run "beat your best, one more run" hook ---
                int chain = stats != null ? stats.BestCombo : 0;
                int prevDay = BestRecords.BestDay, prevCash = BestRecords.BestCash, prevChain = BestRecords.BestChain;
                // A survived-day record only counts a day you actually cleared (Payout + quota met) —
                // never the day a detonation/shortfall ended the run.
                bool newDay   = (phase == RoundPhase.Payout && met) && BestRecords.SubmitDay(day);
                bool newCash  = BestRecords.SubmitCash(cash);
                bool newChain = chain >= 2 && BestRecords.SubmitChain(chain);
                // First-ever run (no prior record) saves silently — no "NEW RECORD" spam when everything beats zero.
                bool flourishDay   = newDay   && prevDay   > 0;
                bool flourishCash  = newCash  && prevCash  > 0;
                bool flourishChain = newChain && prevChain > 0;
                bool flourish = flourishDay || flourishCash || flourishChain;

                b += "\n\n— RECORDS —";
                b += $"\nBest day:  Day {BestRecords.BestDay}";
                if (BestRecords.BestCash  > 0)  b += $"     Best cash:  ${BestRecords.BestCash}";
                if (BestRecords.BestChain >= 2) b += $"     Best chain:  x{BestRecords.BestChain}";
                // Survived but didn't beat the record yet? Dangle the carrot to pull "one more run".
                if (phase == RoundPhase.Payout && met && !newDay && prevDay > day)
                    b += $"\nRecord is Day {prevDay} — {prevDay - day} to go!";

                if (flourish)
                {
                    string what = flourishDay ? $"DAY {day} SURVIVED" : flourishCash ? $"${cash}" : $"x{chain} CHAIN";
                    b += $"\n<color=#FFD24A><b>★ NEW RECORD!   {what}   ★</b></color>";
                    if (titleText != null) titleText.color = new Color(1f, 0.84f, 0.29f); // gold overrides the phase tint
                    ScreenShake.Add(0.5f);          // static global punch — no wiring needed
                    BestRecords.RaiseNewRecord();   // GameAudio plays the record stinger
                }

                bodyText.text = b;
            }

            // Action buttons only make sense on the terminal FIRED card; Payout auto-advances to the next day.
            bool gameOver = phase == RoundPhase.GameOver;
            var nm = NetworkManager.Singleton;
            bool isHost = nm != null && nm.IsListening && nm.IsServer;
            if (restartButton != null) restartButton.gameObject.SetActive(gameOver && isHost);
            if (menuButton != null) menuButton.gameObject.SetActive(gameOver);
        }

        private void SetButtons(bool on)
        {
            if (restartButton != null) restartButton.gameObject.SetActive(on);
            if (menuButton != null) menuButton.gameObject.SetActive(on);
        }

        // Host-only path; RequestStartRpc is server-gated so a stray client click is a harmless no-op.
        private void OnRestart() => ServiceLocator.Get<RoundManager>()?.RequestStartRpc();

        // Leave() disconnects only this peer; UIManager.Update() then falls back to MainMenu. No
        // OnPhaseChanged fires on shutdown, so hide the card explicitly to avoid a lingering panel.
        private void OnReturnToTitle()
        {
            if (panel != null) panel.SetActive(false);
            SetButtons(false);
            ServiceLocator.Get<NetworkSessionManager>()?.Leave();
        }
    }
}
