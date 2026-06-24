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
            if (fill != null)
            {
                fill.fillAmount = fuse;
                Color c = Color.Lerp(new Color(1f, 0.18f, 0.1f), new Color(0.4f, 1f, 0.45f), fuse);
                if (fuse < 0.3f)
                {
                    float p = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 12f);
                    c = Color.Lerp(c, Color.white, p * 0.6f);
                }
                fill.color = c;
            }
            if (label != null) label.text = fuse < 0.3f ? "!! FUSE !!" : "FUSE";
        }
    }
}
