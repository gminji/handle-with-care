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

        private float _popupTimer;
        private float _flashTimer;
        private const float PopupDuration = 1.1f;
        private const float FlashDuration = 0.18f;

        private void OnEnable()
        {
            CargoCondition.OnDamageFx += HandleDamage;
            DeliveryZone.OnDelivered += HandleDelivered;
        }

        private void OnDisable()
        {
            CargoCondition.OnDamageFx -= HandleDamage;
            DeliveryZone.OnDelivered -= HandleDelivered;
        }

        private void Update()
        {
            var quota = ServiceLocator.Get<QuotaSystem>();
            if (quota != null)
            {
                if (cashText != null) cashText.text = $"${quota.Cash.Value}";
                if (quotaText != null) quotaText.text = $"QUOTA  ${quota.Quota.Value}";
            }

            var round = ServiceLocator.Get<RoundManager>();
            if (round != null)
            {
                if (timerText != null) timerText.text = FormatTime(round.TimeRemaining.Value);
                if (roundText != null) roundText.text = $"DAY {round.RoundNumber.Value}";
                if (phaseText != null) phaseText.text = PhaseLabel(round.Phase.Value);
            }

            if (_popupTimer > 0f)
            {
                _popupTimer -= Time.deltaTime;
                if (_popupTimer <= 0f && popupText != null) popupText.text = string.Empty;
            }

            if (_flashTimer > 0f && flashImage != null)
            {
                _flashTimer -= Time.deltaTime;
                float a = Mathf.Clamp01(_flashTimer / FlashDuration) * 0.35f;
                var c = flashImage.color; c.a = a; flashImage.color = c;
            }
        }

        private void HandleDamage(Vector3 worldPos, int valueLost, bool bigSmash)
        {
            ShowPopup($"-${valueLost}", bigSmash ? Color.red : new Color(1f, 0.6f, 0.3f));
            if (bigSmash) _flashTimer = FlashDuration;
        }

        private void HandleDelivered(Vector3 worldPos, int payout)
            => ShowPopup($"+${payout}", new Color(0.4f, 1f, 0.5f));

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
