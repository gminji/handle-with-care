using UnityEngine;
using UnityEngine.UI;
using SlopCo.Player;

namespace SlopCo.UI
{
    /// <summary>
    /// HUD dash-stamina bar for the local owner: fill = gauge (drained orange → full blue), turns red and
    /// shows "EXHAUSTED" on depletion. Mirrors <see cref="FuseGauge"/>. Reads <see cref="PlayerController.LocalHuman"/>
    /// (NOT FindObjects+IsOwner — a host owns server-side bots too). Hidden until a local human player exists.
    /// </summary>
    public sealed class DashGauge : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Image fill;
        [SerializeField] private Text label;

        private void Update()
        {
            var owner = PlayerController.LocalHuman;
            if (owner == null) { if (root != null && root.activeSelf) root.SetActive(false); return; }
            if (root != null && !root.activeSelf) root.SetActive(true);

            float g = Mathf.Clamp01(owner.DashGauge01);
            bool ex = owner.DashExhausted;
            int state = DashGaugeView.State(g, ex);
            if (fill != null)
            {
                fill.fillAmount = g;
                fill.color = state switch
                {
                    2 => UITheme.DangerFill,
                    1 => UITheme.WarningFill,   // below threshold — snap, don't lerp, so fill matches the label flip
                    _ => UITheme.StaminaRamp(g),
                };
            }
            if (label != null) label.text = SlopCo.Core.Localization.Get(DashGaugeView.LabelKey(state));
        }
    }

    /// <summary>
    /// Pure dash-stamina state ladder (design.md §5.2, §6-D) — the color-only "부족" signal FuseGauge already
    /// fixes gets the same treatment here: state 1 (LOW) gets its own label AND a color snap, not a lerp, so
    /// the fill and the text flip at the exact same gauge value instead of drifting apart.
    /// </summary>
    public static class DashGaugeView
    {
        public static int State(float g01, bool exhausted)
            => exhausted ? 2 : (g01 < UITheme.StaminaLowThreshold ? 1 : 0);

        public static string LabelKey(int s) => s switch
        {
            2 => "dash.exhausted",
            1 => "dash.low",
            _ => "dash.label",
        };
    }
}
