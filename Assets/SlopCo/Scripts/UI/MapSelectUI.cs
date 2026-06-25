using UnityEngine;
using UnityEngine.UI;
using SlopCo.Core;

namespace SlopCo.UI
{
    /// <summary>
    /// Main-menu map picker. Buttons set <see cref="GameModeState.SelectedMap"/> (one per map; the
    /// Random button = -1) and highlight the current pick. Pure state-setting — the existing
    /// PLAY / PLAY SOLO buttons start the run using whatever is selected, so no play-flow changes are
    /// needed. All references optional (null-checked) so a partial canvas still works.
    /// </summary>
    public sealed class MapSelectUI : MonoBehaviour
    {
        [Tooltip("One button per concrete map, in map-index order.")]
        [SerializeField] private Button[] mapButtons;
        [SerializeField] private Button randomButton;
        [Tooltip("Tint applied to the currently-selected button.")]
        [SerializeField] private Color selectedTint = new Color(0.45f, 1f, 0.5f);
        [SerializeField] private Color normalTint = Color.white;

        private void Awake()
        {
            if (mapButtons != null)
                for (int i = 0; i < mapButtons.Length; i++)
                {
                    int idx = i; // capture per-iteration
                    if (mapButtons[i] != null) mapButtons[i].onClick.AddListener(() => Pick(idx));
                }
            if (randomButton != null) randomButton.onClick.AddListener(() => Pick(-1));
        }

        private void OnEnable() => Refresh();

        private void Pick(int idx)
        {
            GameModeState.SelectedMap = idx;
            Refresh();
        }

        private void Refresh()
        {
            int sel = GameModeState.SelectedMap;
            if (mapButtons != null)
                for (int i = 0; i < mapButtons.Length; i++)
                    Tint(mapButtons[i], i == sel);
            Tint(randomButton, sel < 0);
        }

        private void Tint(Button b, bool on)
        {
            if (b == null) return;
            var img = b.GetComponent<Image>();
            if (img != null) img.color = on ? selectedTint : normalTint;
        }
    }
}
