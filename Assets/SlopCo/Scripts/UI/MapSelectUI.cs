using UnityEngine;
using UnityEngine.UI;
using SlopCo.Core;

namespace SlopCo.UI
{
    /// <summary>
    /// Map picker shown after a mode (Solo / AI / Online) is chosen. Each button commits its map index
    /// (Random = -1) via <see cref="UIManager.ChooseMap"/>, which launches the pending mode. Pick-and-go
    /// — no persistent highlight, since choosing immediately transitions out of the panel. All refs
    /// optional (null-checked).
    /// </summary>
    public sealed class MapSelectUI : MonoBehaviour
    {
        [SerializeField] private UIManager manager;
        [Tooltip("One button per concrete map, in map-index order.")]
        [SerializeField] private Button[] mapButtons;
        [SerializeField] private Button randomButton;

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

        private void Pick(int idx)
        {
            if (manager != null) manager.ChooseMap(idx);
            else GameModeState.SelectedMap = idx; // standalone fallback
        }
    }
}
