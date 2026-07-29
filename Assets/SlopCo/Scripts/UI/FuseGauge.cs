using UnityEngine;
using UnityEngine.UI;
using SlopCo.Cargo;

namespace SlopCo.UI
{
    /// <summary>
    /// HUD fuse meter for bomb mode — shows the MOST critical bomb's remaining fuse (the replicated
    /// Condition, 1→0) as a filled bar that goes green→red and flashes when it's about to blow. The
    /// single most important readability cue of the hook: "how close are we to dying?" Hidden when no
    /// bombs are live.
    /// </summary>
    public sealed class FuseGauge : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Image fill;
        [SerializeField] private Text label;

        private void Update()
        {
            var bombs = Object.FindObjectsByType<CargoBomb>(FindObjectsSortMode.None);
            if (bombs.Length == 0)
            {
                if (root != null && root.activeSelf) root.SetActive(false);
                return;
            }

            float min = 1f;
            foreach (var b in bombs)
            {
                var c = b.GetComponent<CargoCondition>();
                if (c != null) min = Mathf.Min(min, c.Condition.Value);
            }
            float fuse = Mathf.Clamp01(min);

            if (root != null && !root.activeSelf) root.SetActive(true);
            bool critical = fuse < UITheme.FuseCriticalThreshold;
            if (fill != null)
            {
                fill.fillAmount = fuse;
                Color c = UITheme.FuseRamp(fuse);
                if (critical)
                {
                    float p = UIMotion.Pulse(Time.unscaledTime, UIMotion.PulseHzCritical);
                    c = Color.Lerp(c, Color.white, p * UIMotion.PulseFlashAmount);
                }
                fill.color = c;
            }
            if (label != null) label.text = SlopCo.Core.Localization.Get(critical ? "fuse.critical" : "fuse.label");
        }
    }
}
