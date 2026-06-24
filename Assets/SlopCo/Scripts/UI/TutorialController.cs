using UnityEngine;
using UnityEngine.UI;
using SlopCo.Core;
using SlopCo.Player;
using SlopCo.Gameplay;

namespace SlopCo.UI
{
    /// <summary>
    /// Guided, reactive tutorial (tutorial mode only). Coaches the three core verbs and advances by
    /// DETECTING the player actually doing them — move, grab the bomb, deliver it — so a brand-new buyer
    /// learns by doing in &lt;1 minute. Runs in solo with a calm (non-burning) fuse so there is no death
    /// pressure while learning. Pure client-side overlay driven off replicated state; no netcode.
    /// </summary>
    public sealed class TutorialController : MonoBehaviour
    {
        [SerializeField] private GameObject panel;
        [SerializeField] private Text stepText;
        [SerializeField] private Text counterText;

        private enum Step { Move = 0, Grab = 1, Deliver = 2, Done = 3 }
        private Step _step;
        private bool _delivered;
        private bool _haveStart;
        private Vector3 _startPos;

        private void OnEnable() => DeliveryZone.OnDelivered += HandleDelivered;
        private void OnDisable() => DeliveryZone.OnDelivered -= HandleDelivered;
        private void HandleDelivered(Vector3 pos, int payout) => _delivered = true;

        private void Update()
        {
            if (!GameModeState.Tutorial)
            {
                if (panel != null && panel.activeSelf) panel.SetActive(false);
                Reset();
                return;
            }
            if (panel != null && !panel.activeSelf) panel.SetActive(true);

            var local = GetLocalPlayer();

            switch (_step)
            {
                case Step.Move:
                    SetStep(Localization.Get("tut.move"));
                    if (local != null)
                    {
                        if (!_haveStart) { _startPos = local.transform.position; _haveStart = true; }
                        if ((local.transform.position - _startPos).sqrMagnitude > 4f) Advance();
                    }
                    break;

                case Step.Grab:
                    SetStep(Localization.Get("tut.grab"));
                    var carry = local != null ? local.GetComponent<PlayerCarryController>() : null;
                    if (carry != null && carry.IsCarrying) Advance();
                    break;

                case Step.Deliver:
                    SetStep(Localization.Get("tut.deliver"));
                    if (_delivered) Advance();
                    break;

                case Step.Done:
                    SetStep(Localization.Get("tut.done"));
                    break;
            }

            if (counterText != null)
                counterText.text = _step == Step.Done
                    ? Localization.Get("tut.complete")
                    : $"{Localization.Get("tut.step")}  {(int)_step + 1} / 3";
        }

        private void SetStep(string t) { if (stepText != null) stepText.text = t; }

        private void Advance()
        {
            if (_step < Step.Done) _step++;
        }

        private void Reset()
        {
            _step = Step.Move; _delivered = false; _haveStart = false;
        }

        private PlayerController GetLocalPlayer()
        {
            foreach (var p in Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
                if (p.IsOwner) return p;
            return null;
        }
    }
}
