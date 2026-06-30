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
            if (fill != null)
            {
                fill.fillAmount = g;
                Color full = new Color(0.35f, 0.8f, 1f), low = new Color(1f, 0.5f, 0.15f);
                fill.color = ex ? new Color(1f, 0.25f, 0.2f) : Color.Lerp(low, full, g);
            }
            if (label != null) label.text = SlopCo.Core.Localization.Get(ex ? "dash.exhausted" : "dash.label");
        }
    }
}
