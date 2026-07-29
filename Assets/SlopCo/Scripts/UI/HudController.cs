using UnityEngine;
using UnityEngine.UI;
using SlopCo.Core;
using SlopCo.Gameplay;
using SlopCo.Cargo;

namespace SlopCo.UI
{
    /// <summary>
    /// Reads replicated round/quota state (polled — robust against NetworkObject lifecycle) and renders
    /// the HUD. Also turns the depreciation + delivery FX events into instant, spectator-readable
    /// feedback: a "-$$$"/"+$$$" popup and a brief red flash on a big smash (the punchline on-frame).
    /// All UI references are optional so a partial canvas still works.
    /// </summary>
    public sealed class HudController : MonoBehaviour
    {
        [SerializeField] private Text cashText;
        [SerializeField] private Text quotaText;
        [SerializeField] private Text timerText;
        [SerializeField] private Text roundText;
        [SerializeField] private Text phaseText;
        [SerializeField] private Text popupText;
        [Tooltip("Optional full-screen Image pulsed on a big smash.")]
        [SerializeField] private Image flashImage;
        [Header("Combo")]
        [SerializeField] private Text comboText;   // "x3 CHAIN!"
        [SerializeField] private Image comboBar;    // window timer fill

        private float _popupTimer;
        private float _flashTimer;
        private float _comboPunch;
        private const float PopupDuration = UIMotion.DurPopup;
        private const float FlashDuration = UIMotion.DurFlash;

        // Change-detection caches so Update() (60fps × 3min Payout window) doesn't reallocate the cash/quota/
        // day/chain strings every frame — the AugmentShopUI:35 _lastCash idiom. int.MinValue never matches a
        // real replicated value, so the first frame after enable/language-switch always repaints once.
        private int _lastCash = int.MinValue;
        private int _lastQuota = int.MinValue;
        private int _lastDay = int.MinValue;
        private int _lastChain = int.MinValue;
        private RoundPhase _lastPhase = (RoundPhase)255;

        // _lastPhase must be reset too: it is gated on the phase VALUE, so without this a language switch
        // mid-phase would leave the phase label stuck in the old language until the phase next changes.
        private void InvalidateLabels()
        {
            _lastCash = _lastQuota = _lastDay = _lastChain = int.MinValue;
            _lastPhase = (RoundPhase)255;
        }

        private void OnEnable()
        {
            CargoCondition.OnDamageFx += HandleDamage;
            DeliveryZone.OnDelivered += HandleDelivered;
            ComboSystem.OnCombo += HandleCombo;
            Localization.OnLanguageChanged += InvalidateLabels;
        }

        private void OnDisable()
        {
            CargoCondition.OnDamageFx -= HandleDamage;
            DeliveryZone.OnDelivered -= HandleDelivered;
            ComboSystem.OnCombo -= HandleCombo;
            Localization.OnLanguageChanged -= InvalidateLabels;
        }

        private void HandleCombo(int combo, Vector3 worldPos) => _comboPunch = 1f;

        private void Update()
        {
            var quota = ServiceLocator.Get<QuotaSystem>();
            if (quota != null)
            {
                int cash = quota.Cash.Value, target = quota.Quota.Value;
                if (cash != _lastCash) { _lastCash = cash; if (cashText != null) cashText.text = $"${cash}"; }
                if (target != _lastQuota)
                {
                    _lastQuota = target;
                    if (quotaText != null) quotaText.text = Localization.Get("hud.quota").Replace("{quota}", target.ToString());
                }
            }

            var round = ServiceLocator.Get<RoundManager>();
            if (round != null)
            {
                if (timerText != null) timerText.text = FormatTime(round.TimeRemaining.Value);
                int day = round.RoundNumber.Value;
                if (day != _lastDay)
                {
                    _lastDay = day;
                    if (roundText != null) roundText.text = Localization.Get("hud.day").Replace("{day}", day.ToString());
                }
                var phase = round.Phase.Value;
                if (phase != _lastPhase)
                {
                    _lastPhase = phase;
                    if (phaseText != null) phaseText.text = PhaseLabel(phase);
                }
            }

            if (_popupTimer > 0f)
            {
                _popupTimer -= Time.deltaTime;
                if (_popupTimer <= 0f && popupText != null) popupText.text = string.Empty;
            }

            if (_flashTimer > 0f && flashImage != null)
            {
                _flashTimer -= Time.deltaTime;
                float a = Mathf.Clamp01(_flashTimer / FlashDuration) * UITheme.FlashAlpha;
                var c = flashImage.color; c.a = a; flashImage.color = c;
            }

            // Combo meter — the chase. Multiplier text punches on each chain; the bar drains the window.
            var combo = ServiceLocator.Get<ComboSystem>();
            int chain = combo != null ? combo.Combo.Value : 0;
            bool showCombo = chain >= 2;
            if (comboText != null)
            {
                if (comboText.gameObject.activeSelf != showCombo) comboText.gameObject.SetActive(showCombo);
                if (showCombo)
                {
                    if (chain != _lastChain)
                    {
                        _lastChain = chain;
                        comboText.text = Localization.Get("hud.chain").Replace("{chain}", chain.ToString());
                    }
                    comboText.transform.localScale = Vector3.one * (1f + UIMotion.PunchAmountLarge * _comboPunch);
                    comboText.color = UITheme.ComboRamp(Mathf.InverseLerp(2f, 8f, chain));
                }
            }
            if (comboBar != null)
            {
                if (comboBar.gameObject.activeSelf != showCombo) comboBar.gameObject.SetActive(showCombo);
                if (showCombo && combo != null) comboBar.fillAmount = Mathf.Clamp01(combo.WindowRemaining.Value / GameConstants.ComboWindowSeconds);
            }
            if (_comboPunch > 0f) _comboPunch = UIMotion.Decay(_comboPunch, UIMotion.PunchDecayRate, Time.deltaTime);
        }

        private void HandleDamage(Vector3 worldPos, int valueLost, bool bigSmash)
        {
            ShowPopup($"-${valueLost}", bigSmash ? UITheme.DangerFill : UITheme.Warning);
            if (bigSmash) _flashTimer = FlashDuration;
        }

        private void HandleDelivered(Vector3 worldPos, int payout)
            => ShowPopup($"+${payout}", UITheme.PayoutGreen);

        private void ShowPopup(string text, Color color)
        {
            if (popupText == null) return;
            popupText.text = text;
            popupText.color = color;
            _popupTimer = PopupDuration;
        }

        private static string FormatTime(float seconds)
        {
            if (seconds < 0f) seconds = 0f;
            int m = Mathf.FloorToInt(seconds / 60f);
            int s = Mathf.FloorToInt(seconds % 60f);
            return $"{m:0}:{s:00}";
        }

        private static string PhaseLabel(RoundPhase phase) => phase switch
        {
            RoundPhase.Lobby => Localization.Get("phase.lobby"),
            RoundPhase.Briefing => Localization.Get("phase.briefing"),
            RoundPhase.Hauling => Localization.Get("phase.hauling"),
            RoundPhase.Payout => Localization.Get("phase.payout"),
            RoundPhase.GameOver => Localization.Get("phase.gameover"),
            _ => string.Empty,
        };
    }
}
