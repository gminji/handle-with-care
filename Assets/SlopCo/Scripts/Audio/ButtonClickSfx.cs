using UnityEngine;
using UnityEngine.UI;

namespace SlopCo.Audio
{
    /// <summary>
    /// Auto-wires every uGUI Button in the scene (including those under inactive panels) to play the
    /// <see cref="GameAudio"/> UI click on press. One component on the GameAudio object — no per-button
    /// editing. Runtime-created buttons aren't covered, but all menu/lobby/HUD buttons are scene objects.
    /// </summary>
    public sealed class ButtonClickSfx : MonoBehaviour
    {
        private void Start()
        {
            var audio = Object.FindFirstObjectByType<GameAudio>();
            if (audio == null) return;
            var buttons = Object.FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var b in buttons)
                if (b != null) b.onClick.AddListener(audio.PlayClick);
        }
    }
}
