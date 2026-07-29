using UnityEngine;
using UnityEngine.UI;
using SlopCo.Core;
using SlopCo.Gameplay;

namespace SlopCo.UI
{
    /// <summary>
    /// Briefing reveal for the day's modifier ("★ RUSH HOUR — short fuse! ★"). Driven off the replicated
    /// round phase + day number (no extra netcode): on Briefing it reads <see cref="RoundManager.RoundNumber"/>,
    /// computes the same deterministic <see cref="DailyModifier.Roll"/> the server used, and fades a localized
    /// label in; it lingers a few seconds into Hauling then fades out. Day 1 (None) shows nothing. Re-evaluates
    /// each frame while building so a late RoundNumber sync self-corrects. All refs are null-safe.
    /// </summary>
    public sealed class DailyModifierBanner : MonoBehaviour
    {
        [SerializeField] private CanvasGroup group;
        [SerializeField] private Text label;

        private float _target;
        private float _hideTimer;
        private bool _briefing;

        private void OnEnable()
        {
            RoundManager.OnPhaseChanged += HandlePhase;
            if (group != null) group.alpha = 0f;
        }

        private void OnDisable() => RoundManager.OnPhaseChanged -= HandlePhase;

        private void HandlePhase(RoundPhase phase)
        {
            if (phase == RoundPhase.Briefing) { _briefing = true; _hideTimer = 0f; Refresh(); }
            else if (phase == RoundPhase.Hauling) { _briefing = false; _hideTimer = UIMotion.BannerLingerSeconds; } // linger then fade
            else { _briefing = false; _target = 0f; _hideTimer = 0f; }
        }

        private void Refresh()
        {
            var rm = ServiceLocator.Get<RoundManager>();
            int day = rm != null ? rm.RoundNumber.Value : 1;
            var mod = DailyModifier.Roll(day);
            if (mod == DayModifier.None) { _target = 0f; return; } // clean day — nothing to reveal
            if (label != null) label.text = "★ " + Localization.Get(DailyModifier.NameKey(mod)) + " ★";
            _target = 1f;
        }

        private void Update()
        {
            // While the briefing banner is up, keep recomputing so a one-tick-late RoundNumber self-corrects.
            if (_briefing) Refresh();
            if (_hideTimer > 0f)
            {
                // unscaledDeltaTime so the linger runs on the same clock as the fade below — Juice's
                // big-smash hit-stop (Time.timeScale = 0.18f) must not stretch one and not the other.
                _hideTimer -= Time.unscaledDeltaTime;
                if (_hideTimer <= 0f) _target = 0f;
            }
            UIMotion.Fade(group, _target);
        }
    }
}
