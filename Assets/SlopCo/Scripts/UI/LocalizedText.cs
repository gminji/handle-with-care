using UnityEngine;
using UnityEngine.UI;
using SlopCo.Core;

namespace SlopCo.UI
{
    /// <summary>
    /// Attach to a uGUI Text to make it follow the selected language: its content becomes
    /// <c>Localization.Get(key)</c> and re-applies whenever the language changes.
    /// </summary>
    [RequireComponent(typeof(Text))]
    public sealed class LocalizedText : MonoBehaviour
    {
        [SerializeField] private string key;
        private Text _text;

        private void Awake() => _text = GetComponent<Text>();

        private void OnEnable()
        {
            Localization.OnLanguageChanged += Apply;
            Apply();
        }

        private void OnDisable() => Localization.OnLanguageChanged -= Apply;

        public void SetKey(string k) { key = k; Apply(); }

        private void Apply()
        {
            if (_text == null) _text = GetComponent<Text>();
            if (_text != null && !string.IsNullOrEmpty(key)) _text.text = Localization.Get(key);
        }
    }
}
