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
            else if (phase == RoundPhase.Hauling) { _haulHideTimer = 4.5f; } // linger, then fade
            else { _target = 0f; _haulHideTimer = 0f; }
        }

        private void Update()
        {
            if (_haulHideTimer > 0f)
            {
                _haulHideTimer -= Time.deltaTime;
                if (_haulHideTimer <= 0f) _target = 0f;
            }
            if (group != null)
                group.alpha = Mathf.MoveTowards(group.alpha, _target, Time.deltaTime * 1.5f);
        }
    }
}
