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
        [Header("Share (shown on every card)")]
        [SerializeField] private Button shareButton;    // capture PNG + open folder
        [SerializeField] private Button copyButton;     // copy brag text to clipboard
        [SerializeField] private Text shareStatus;      // tiny "Saved ✓" / "Copied ✓" toast
        [SerializeField] private ShareCapture shareCapture;

        // Snapshot of the currently-shown card, captured at the end of Show() for the share/copy actions.
        private string _shareSummary = "";
        private string _gradeLetter = "";
        private bool _crewAwarded;   // crew XP is banked once per run (on GameOver), reset when a new run starts

        private void Awake()
        {
            if (restartButton != null) restartButton.onClick.AddListener(OnRestart);
            if (menuButton != null) menuButton.onClick.AddListener(OnReturnToTitle);
            if (shareButton != null) shareButton.onClick.AddListener(OnShare);
            if (copyButton != null) copyButton.onClick.AddListener(OnCopy);
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
                _crewAwarded = false; // a new run is under way — allow the next GameOver to bank crew XP
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
                    titleText.text = Localization.Get("phase.gameover"); // "FIRED." — reuse existing key
                    titleText.color = new Color(1f, 0.4f, 0.35f);
                }
                else
                {
                    titleText.text = Localization.Get(met ? "res.day.survived" : "res.day.short")
                        .Replace("{day}", day.ToString());
                    titleText.color = met ? new Color(0.45f, 1f, 0.5f) : new Color(1f, 0.7f, 0.3f);
                }
            }

            if (bodyText != null)
            {
                string b = Localization.Get("res.cashquota")
                               .Replace("{cash}", cash.ToString())
                               .Replace("{quota}", target.ToString()) + "\n\n";
                if (stats != null)
                {
                    b += Localization.Get("res.deliveries")
                             .Replace("{count}", stats.DeliveryCount.ToString())
                             .Replace("{total}", stats.TotalDelivered.ToString()) + "\n";
                    b += Localization.Get("res.destroyed").Replace("{val}", stats.TotalDestroyed.ToString()) + "\n";
                    b += Localization.Get("res.smash").Replace("{val}", stats.BiggestSmash.ToString());
                    if (stats.BestCombo >= 2)
                        b += "\n" + Localization.Get("res.bestchain").Replace("{chain}", stats.BestCombo.ToString());
                }

                // --- Personal records: the cross-run "beat your best, one more run" hook ---
                int chain = stats != null ? stats.BestCombo : 0;
                int prevDay = BestRecords.BestDay, prevCash = BestRecords.BestCash, prevChain = BestRecords.BestChain;
                // A survived-day record only counts a day you actually cleared (Payout + quota met) —
                // never the day a detonation/shortfall ended the run.
                bool newDay   = (phase == RoundPhase.Payout && met) && BestRecords.SubmitDay(day);
                bool newCash  = BestRecords.SubmitCash(cash);
                bool newChain = chain >= 2 && BestRecords.SubmitChain(chain);

                // --- Chaos Rating: fold the run into one shareable grade + auto-headline ---
                // Show() fires every Payout, so this is a *running* grade (cumulative RunStats); the final
                // FIRED card is the run's verdict = the screenshot. SubmitGrade is a monotonic raise.
                bool survived = (phase == RoundPhase.Payout) && met;
                var verdict = RunGrade.Evaluate(
                    stats != null ? stats.DeliveryCount  : 0,
                    stats != null ? stats.TotalDelivered : 0,
                    stats != null ? stats.TotalDestroyed : 0,
                    stats != null ? stats.BiggestSmash   : 0,
                    chain, day, survived);
                int prevGrade = BestRecords.BestGrade;                 // read before submit (records pattern)
                bool newGrade = BestRecords.SubmitGrade((int)verdict.Grade);

                // First-ever run (no prior record) saves silently — no "NEW RECORD" spam when everything beats zero.
                bool flourishDay   = newDay   && prevDay   > 0;
                bool flourishCash  = newCash  && prevCash  > 0;
                bool flourishChain = newChain && prevChain > 0;
                bool flourishGrade = newGrade && prevGrade != BestRecords.NoGrade;
                bool flourish = flourishDay || flourishCash || flourishChain || flourishGrade;

                b += "\n\n— " + Localization.Get("res.records") + " —";
                b += "\n" + Localization.Get("res.best.day").Replace("{day}", BestRecords.BestDay.ToString());
                if (BestRecords.BestCash  > 0)  b += "     " + Localization.Get("res.best.cash").Replace("{cash}", BestRecords.BestCash.ToString());
                if (BestRecords.BestChain >= 2) b += "     " + Localization.Get("res.bestchain").Replace("{chain}", BestRecords.BestChain.ToString());
                // Survived but didn't beat the record yet? Dangle the carrot to pull "one more run".
                if (phase == RoundPhase.Payout && met && !newDay && prevDay > day)
                    b += "\n" + Localization.Get("res.record.togo")
                             .Replace("{best}", prevDay.ToString())
                             .Replace("{gap}", (prevDay - day).ToString());

                // Chaos Rating block — the big screenshot badge + auto-generated punchline.
                b += "\n\n— " + Localization.Get("grade.title") + " —";
                b += $"\n<size=46><color={GradeColorHex(verdict.Grade)}><b>{RunGrade.Letter(verdict.Grade)}</b></color></size>   {BuildHeadline(verdict.Flavor, stats, day)}";

                if (flourish)
                {
                    string what = flourishGrade ? Localization.Get("res.what.rank").Replace("{letter}", RunGrade.Letter(verdict.Grade))
                                : flourishDay   ? Localization.Get("res.what.day").Replace("{day}", day.ToString())
                                : flourishCash  ? $"${cash}"   // pure number — no translation needed
                                :                 Localization.Get("res.what.chain").Replace("{chain}", chain.ToString());
                    b += $"\n<color=#FFD24A><b>{Localization.Get("res.newrecord").Replace("{what}", what)}</b></color>";
                    if (titleText != null) titleText.color = new Color(1f, 0.84f, 0.29f); // gold overrides the phase tint
                    ScreenShake.Add(0.5f);          // static global punch — no wiring needed
                    BestRecords.RaiseNewRecord();   // GameAudio plays the record stinger
                }

                // --- Crew Rank: bank this run's score once (on the terminal FIRED card) + show the tier ---
                if (phase == RoundPhase.GameOver)
                {
                    bool rankUp = false;
                    if (!_crewAwarded) { _crewAwarded = true; rankUp = CrewRank.AddRun(verdict.Score); }
                    int tier = CrewRank.Tier;
                    int toNext = CrewRankMath.XpToNext(CrewRank.Xp);
                    b += "\n\n— " + Localization.Get("crew.title") + " —";
                    b += "\n" + Localization.Get("crew.line")
                             .Replace("{rank}", (tier + 1).ToString())
                             .Replace("{name}", Localization.Get("rank." + tier));
                    b += "\n<size=18>" + (toNext > 0
                            ? Localization.Get("crew.progress").Replace("{run}", verdict.Score.ToString()).Replace("{tonext}", toNext.ToString())
                            : Localization.Get("crew.maxed").Replace("{run}", verdict.Score.ToString())) + "</size>";
                    if (rankUp)
                    {
                        b += "\n<color=#FFD24A><b>" + Localization.Get("crew.rankup").Replace("{name}", Localization.Get("rank." + tier)) + "</b></color>";
                        ScreenShake.Add(0.5f);
                        BestRecords.RaiseNewRecord(); // reuse the record stinger for the rank-up moment
                    }
                }

                bodyText.text = b;

                // Snapshot for SHARE/COPY — plain-text brag (no rich tags) + the grade letter for the filename.
                _gradeLetter = RunGrade.Letter(verdict.Grade);
                _shareSummary = ShareText.Build(_gradeLetter, BuildHeadline(verdict.Flavor, stats, day),
                    day, cash, stats != null ? stats.DeliveryCount : 0, BestRecords.BestDay, BestRecords.BestChain);
            }

            // Action buttons only make sense on the terminal FIRED card; Payout auto-advances to the next day.
            bool gameOver = phase == RoundPhase.GameOver;
            var nm = NetworkManager.Singleton;
            bool isHost = nm != null && nm.IsListening && nm.IsServer;
            if (restartButton != null) restartButton.gameObject.SetActive(gameOver && isHost);
            if (menuButton != null) menuButton.gameObject.SetActive(gameOver);

            // SHARE/COPY make sense on every shown card (Payout + FIRED) — the screenshot moment is the point.
            if (shareButton != null) shareButton.gameObject.SetActive(true);
            if (copyButton != null) copyButton.gameObject.SetActive(true);
            if (shareStatus != null) shareStatus.text = "";
        }

        // Grade → rich-text color (gold S → red D). The badge color carries the verdict at a glance.
        private static string GradeColorHex(Grade g) => g switch
        {
            Grade.S => "#FFD24A",   // gold
            Grade.A => "#7CFF6B",   // green
            Grade.B => "#6BD0FF",   // blue
            Grade.C => "#FFB14A",   // orange
            _       => "#FF6B6B",   // red (D)
        };

        // Flavor → localized headline template with {token} substitution. Missing key/token falls back
        // safely (Localization.Get returns the key; absent tokens are simply not replaced).
        private static string BuildHeadline(RunFlavor f, RunStats s, int day)
        {
            string key = f switch
            {
                RunFlavor.Chainmaster => "grade.flavor.chain",
                RunFlavor.Demolisher  => "grade.flavor.demo",
                RunFlavor.Survivor    => "grade.flavor.survive",
                RunFlavor.Hustler     => "grade.flavor.hustle",
                _                     => "grade.flavor.rookie",
            };
            string t = Localization.Get(key);
            int deliveries = s != null ? s.DeliveryCount  : 0;
            int delivered  = s != null ? s.TotalDelivered : 0;
            int destroyed  = s != null ? s.TotalDestroyed : 0;
            int chain      = s != null ? s.BestCombo      : 0;
            return t.Replace("{day}", day.ToString())
                    .Replace("{deliveries}", deliveries.ToString())
                    .Replace("{delivered}", delivered.ToString())
                    .Replace("{destroyed}", destroyed.ToString())
                    .Replace("{chain}", chain.ToString());
        }

        private void SetButtons(bool on)
        {
            if (restartButton != null) restartButton.gameObject.SetActive(on);
            if (menuButton != null) menuButton.gameObject.SetActive(on);
            if (shareButton != null) shareButton.gameObject.SetActive(on);
            if (copyButton != null) copyButton.gameObject.SetActive(on);
        }

        // SHARE — capture the card to a PNG and open the Clips folder; flash the result on the status label.
        private void OnShare()
        {
            if (shareCapture == null) return;
            if (shareStatus != null) shareStatus.text = "…";
            shareCapture.CaptureAndOpen(_gradeLetter, ok =>
            {
                if (shareStatus != null) shareStatus.text = Localization.Get(ok ? "share.saved" : "share.failed");
            });
        }

        // COPY — drop the plain-text brag onto the clipboard (no rich tags), then confirm on the status label.
        private void OnCopy()
        {
            bool ok = ShareCapture.CopyToClipboard(_shareSummary);
            if (shareStatus != null) shareStatus.text = Localization.Get(ok ? "share.copied" : "share.failed");
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
