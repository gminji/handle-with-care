using UnityEngine;
using SlopCo.Gameplay;

namespace SlopCo.UI
{
    /// <summary>
    /// Onboarding nudge: shows the core verbs during Briefing and fades out a few seconds into Hauling,
    /// so a brand-new buyer understands the loop in the first round without a manual. Driven off the
    /// replicated round phase; no input needed.
    /// </summary>
    public sealed class TutorialHint : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        private float _target;
        private float _haulHideTimer;

        private void OnEnable()
        {
            RoundManager.OnPhaseChanged += HandlePhase;
            if (group != null) group.alpha = 0f;
        }

        private void OnDisable() => RoundManager.OnPhaseChanged -= HandlePhase;

        private void HandlePhase(RoundPhase phase)
        {
            if (phase == RoundPhase.Briefing) { _target = 1f; _haulHideTimer = 0f; }
            else if (phase == RoundPhase.Hauling) { _haulHideTimer = UIMotion.HintLingerSeconds; } // linger, then fade
            else { _target = 0f; _haulHideTimer = 0f; }
        }

        private void Update()
        {
            if (_haulHideTimer > 0f)
            {
                // unscaledDeltaTime so linger and fade share a clock (Juice's hit-stop scales Time.deltaTime).
                _haulHideTimer -= Time.unscaledDeltaTime;
                if (_haulHideTimer <= 0f) _target = 0f;
            }
            // Was rate 1.5f (= 0.667s); now the shared 0.50s. Faster is the safe direction here — ShopLayoutTests
            // :126-131 excludes this panel's overlap with ShopRail only because it is fully transparent by Payout.
            UIMotion.Fade(group, _target);
        }
    }
}
