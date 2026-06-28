using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SlopCo.Core;

namespace SlopCo.UI
{
    /// <summary>
    /// Binds the Options panel controls to <see cref="SettingsManager"/>. Volume = sliders;
    /// Fullscreen/VSync = toggle-buttons; Quality/Resolution = ◀▶ cyclers (gamepad-friendly, and no
    /// uGUI Dropdown template to hand-build). Reflects current values into the labels on enable.
    /// </summary>
    public sealed class OptionsUI : MonoBehaviour
    {
        [Header("Audio")]
        [SerializeField] private Slider masterSlider;
        [SerializeField] private Slider musicSlider;
        [SerializeField] private Slider sfxSlider;
        [SerializeField] private Slider voiceSlider;
        [SerializeField] private Text masterValue;
        [SerializeField] private Text musicValue;
        [SerializeField] private Text sfxValue;
        [SerializeField] private Text voiceValue;
        [SerializeField] private Text voiceEnabledValue; // ON/OFF toggle label for the mic

        [Header("Display")]
        [SerializeField] private Text fullscreenValue;
        [SerializeField] private Text vsyncValue;
        [SerializeField] private Text qualityValue;
        [SerializeField] private Text resolutionValue;
        [SerializeField] private Text languageValue;

        private List<Vector2Int> _resolutions;
        private int _resIndex;
        private bool _wired;

        private void Awake() => WireOnce();

        private void WireOnce()
        {
            if (_wired) return;
            _wired = true;
            SettingsManager.Load();
            if (masterSlider != null) masterSlider.onValueChanged.AddListener(v => { SettingsManager.SetMaster(v); SetLabel(masterValue, v); });
            if (musicSlider  != null) musicSlider.onValueChanged.AddListener(v => { SettingsManager.SetMusic(v);  SetLabel(musicValue, v); });
            if (sfxSlider    != null) sfxSlider.onValueChanged.AddListener(v => { SettingsManager.SetSfx(v);    SetLabel(sfxValue, v); });
            if (voiceSlider  != null) voiceSlider.onValueChanged.AddListener(v => { SettingsManager.SetVoiceVolume(v); SetLabel(voiceValue, v); });
            BuildResolutionList();
        }

        private void OnEnable()
        {
            WireOnce();
            Sync();
        }

        private void BuildResolutionList()
        {
            _resolutions = new List<Vector2Int>();
            var seen = new HashSet<long>();
            foreach (var r in Screen.resolutions)
            {
                long key = ((long)r.width << 20) | (uint)r.height;
                if (seen.Add(key)) _resolutions.Add(new Vector2Int(r.width, r.height));
            }
            if (_resolutions.Count == 0)
                _resolutions.Add(new Vector2Int(Screen.width, Screen.height));

            _resIndex = _resolutions.FindIndex(v => v.x == SettingsManager.ResWidth && v.y == SettingsManager.ResHeight);
            if (_resIndex < 0) _resIndex = _resolutions.Count - 1;
        }

        // ── Slider reflection ──
        private void Sync()
        {
            if (masterSlider != null) masterSlider.SetValueWithoutNotify(SettingsManager.Master);
            if (musicSlider  != null) musicSlider.SetValueWithoutNotify(SettingsManager.Music);
            if (sfxSlider    != null) sfxSlider.SetValueWithoutNotify(SettingsManager.Sfx);
            if (voiceSlider  != null) voiceSlider.SetValueWithoutNotify(SettingsManager.VoiceVolume);
            SetLabel(masterValue, SettingsManager.Master);
            SetLabel(musicValue, SettingsManager.Music);
            SetLabel(sfxValue, SettingsManager.Sfx);
            SetLabel(voiceValue, SettingsManager.VoiceVolume);
            if (voiceEnabledValue != null) voiceEnabledValue.text = SettingsManager.VoiceEnabled ? "ON" : "OFF";

            if (fullscreenValue != null) fullscreenValue.text = SettingsManager.Fullscreen ? "ON" : "OFF";
            if (vsyncValue != null)      vsyncValue.text = SettingsManager.VSync ? "ON" : "OFF";
            if (qualityValue != null && QualitySettings.names.Length > 0)
                qualityValue.text = QualitySettings.names[SettingsManager.QualityLevel];
            if (resolutionValue != null && _resolutions != null && _resolutions.Count > 0)
            {
                var r = _resolutions[Mathf.Clamp(_resIndex, 0, _resolutions.Count - 1)];
                resolutionValue.text = $"{r.x} x {r.y}";
            }
            if (languageValue != null)
                languageValue.text = Localization.LanguageNames[(int)Localization.Current];
        }

        public void LanguagePrev() { Localization.Cycle(-1); Sync(); }
        public void LanguageNext() { Localization.Cycle(1); Sync(); }

        private static void SetLabel(Text t, float v01) { if (t != null) t.text = Mathf.RoundToInt(v01 * 100f) + "%"; }

        // ── Button hooks (◀▶ / toggles) ──
        public void ToggleFullscreen() { SettingsManager.SetFullscreen(!SettingsManager.Fullscreen); Sync(); }
        public void ToggleVSync()      { SettingsManager.SetVSync(!SettingsManager.VSync); Sync(); }
        public void ToggleVoice()      { SettingsManager.SetVoiceEnabled(!SettingsManager.VoiceEnabled); Sync(); }

        public void QualityPrev() { SettingsManager.SetQuality(SettingsManager.QualityLevel - 1); Sync(); }
        public void QualityNext() { SettingsManager.SetQuality(SettingsManager.QualityLevel + 1); Sync(); }

        public void ResolutionPrev()
        {
            if (_resolutions == null || _resolutions.Count == 0) return;
            _resIndex = (_resIndex - 1 + _resolutions.Count) % _resolutions.Count;
            var r = _resolutions[_resIndex];
            SettingsManager.SetResolution(r.x, r.y);
            Sync();
        }

        public void ResolutionNext()
        {
            if (_resolutions == null || _resolutions.Count == 0) return;
            _resIndex = (_resIndex + 1) % _resolutions.Count;
            var r = _resolutions[_resIndex];
            SettingsManager.SetResolution(r.x, r.y);
            Sync();
        }
    }
}
