using UnityEngine;

namespace SlopCo.Core
{
    /// <summary>
    /// Persistent player settings (audio + display), backed by PlayerPrefs. These are *real* options:
    /// Master volume drives <see cref="AudioListener.volume"/>; Fullscreen/Resolution/Quality/VSync drive
    /// the actual <see cref="Screen"/>/<see cref="QualitySettings"/>. Music/Sfx are stored and exposed so
    /// audio sources (added with the audio pass) read them. Call <see cref="Load"/> once at boot.
    /// </summary>
    public static class SettingsManager
    {
        const string K_Master = "opt_master", K_Music = "opt_music", K_Sfx = "opt_sfx";
        const string K_Fullscreen = "opt_fullscreen", K_Quality = "opt_quality", K_VSync = "opt_vsync";
        const string K_ResW = "opt_resw", K_ResH = "opt_resh";
        const string K_VoiceOn = "opt_voiceon", K_VoiceVol = "opt_voicevol";
        const string K_FirstPerson = "opt_firstperson";
        const string K_LookSens = "opt_looksens", K_InvertY = "opt_invy";

        public const float LookSensitivityMin = 0.2f, LookSensitivityMax = 3.0f;
        public const float LookSensitivityDefault = 1.0f;   // also the fallback for a corrupted pref

        public static float Master { get; private set; } = 0.8f;
        public static float Music  { get; private set; } = 0.8f;
        public static float Sfx    { get; private set; } = 0.8f;
        // Voice chat: mic open/closed + playback volume for incoming voice. Defaults to OFF — an open mic
        // on first launch is a nasty surprise (and a privacy one); players opt in from Options.
        public static bool  VoiceEnabled { get; private set; }
        public static float VoiceVolume  { get; private set; } = 0.8f;
        /// <summary>Camera viewpoint: false = the default pulled-back third person, true = first person.
        /// Toggled in Options or with the in-game POV key; <see cref="SlopCo.Player.PlayerController"/> reads it live.</summary>
        public static bool  FirstPerson { get; private set; }
        /// <summary>Mouse-look sensitivity multiplier, already clamped to [<see cref="LookSensitivityMin"/>,
        /// <see cref="LookSensitivityMax"/>]. Read live by <c>SlopCo.Player.LookMath.Step</c>. Owned here
        /// (not in LookMath) so SlopCo.Core never depends on SlopCo.Player — see design §3.3.</summary>
        public static float LookSensitivity { get; private set; } = 1.0f;
        /// <summary>Invert the pitch (Y) axis for mouse look.</summary>
        public static bool  InvertLookY { get; private set; }
        public static bool  Fullscreen { get; private set; }
        public static int   QualityLevel { get; private set; }
        public static bool  VSync { get; private set; }
        public static int   ResWidth  { get; private set; }
        public static int   ResHeight { get; private set; }

        static bool _loaded;

        public static void Load()
        {
            if (_loaded) return;
            _loaded = true;

            Master = PlayerPrefs.GetFloat(K_Master, 0.8f);
            Music  = PlayerPrefs.GetFloat(K_Music, 0.8f);
            Sfx    = PlayerPrefs.GetFloat(K_Sfx, 0.8f);
            Fullscreen   = PlayerPrefs.GetInt(K_Fullscreen, Screen.fullScreen ? 1 : 0) == 1;
            QualityLevel = Mathf.Clamp(PlayerPrefs.GetInt(K_Quality, QualitySettings.GetQualityLevel()),
                                       0, Mathf.Max(0, QualitySettings.names.Length - 1));
            VSync     = PlayerPrefs.GetInt(K_VSync, QualitySettings.vSyncCount > 0 ? 1 : 0) == 1;
            ResWidth  = PlayerPrefs.GetInt(K_ResW, Screen.width);
            ResHeight = PlayerPrefs.GetInt(K_ResH, Screen.height);
            VoiceEnabled = PlayerPrefs.GetInt(K_VoiceOn, 0) == 1;   // default OFF; a saved choice still wins
            VoiceVolume  = PlayerPrefs.GetFloat(K_VoiceVol, 0.8f);
            FirstPerson  = PlayerPrefs.GetInt(K_FirstPerson, 0) == 1;   // default: third person
            LookSensitivity = ClampLookSensitivity(PlayerPrefs.GetFloat(K_LookSens, LookSensitivityDefault));
            InvertLookY  = PlayerPrefs.GetInt(K_InvertY, 0) == 1;

            ApplyAll();
            Localization.Load();
        }

        public static void SetMaster(float v)
        {
            Master = Mathf.Clamp01(v);
            AudioListener.volume = Master;
            PlayerPrefs.SetFloat(K_Master, Master);
        }

        public static void SetMusic(float v) { Music = Mathf.Clamp01(v); PlayerPrefs.SetFloat(K_Music, Music); }
        public static void SetSfx(float v)   { Sfx = Mathf.Clamp01(v);   PlayerPrefs.SetFloat(K_Sfx, Sfx); }

        public static void SetVoiceEnabled(bool on) { VoiceEnabled = on; PlayerPrefs.SetInt(K_VoiceOn, on ? 1 : 0); }
        public static void SetVoiceVolume(float v)  { VoiceVolume = Mathf.Clamp01(v); PlayerPrefs.SetFloat(K_VoiceVol, VoiceVolume); }

        public static void SetFirstPerson(bool on) { FirstPerson = on; PlayerPrefs.SetInt(K_FirstPerson, on ? 1 : 0); }

        /// <summary>Pure clamp — touches no PlayerPrefs, so it is safe to unit-test without side effects.
        /// Rejects non-finite input FIRST: Mathf.Clamp is a pair of comparisons, and every comparison
        /// against NaN is false, so it would hand NaN straight back. That matters because this value is
        /// multiplied into the look delta every frame, 0 * NaN is NaN, and a NaN yaw reaches
        /// <c>Quaternion.Euler</c> on an owner-authoritative transform — poisoning the pose that
        /// ClientNetworkTransform replicates to the whole lobby, with no path back to a sane value.
        /// A hand-edited or corrupted <c>opt_looksens</c> pref is enough to trigger it.</summary>
        public static float ClampLookSensitivity(float v)
        {
            if (float.IsNaN(v) || float.IsInfinity(v)) return LookSensitivityDefault;
            return Mathf.Clamp(v, LookSensitivityMin, LookSensitivityMax);
        }

        public static void SetLookSensitivity(float v)
        {
            LookSensitivity = ClampLookSensitivity(v);
            PlayerPrefs.SetFloat(K_LookSens, LookSensitivity);
        }

        public static void SetInvertLookY(bool on) { InvertLookY = on; PlayerPrefs.SetInt(K_InvertY, on ? 1 : 0); }

        public static void SetFullscreen(bool f)
        {
            Fullscreen = f;
            Screen.fullScreenMode = f ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            PlayerPrefs.SetInt(K_Fullscreen, f ? 1 : 0);
        }

        public static void SetQuality(int level)
        {
            QualityLevel = Mathf.Clamp(level, 0, Mathf.Max(0, QualitySettings.names.Length - 1));
            QualitySettings.SetQualityLevel(QualityLevel, true);
            // SetQualityLevel can reset vSync — re-assert ours.
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            PlayerPrefs.SetInt(K_Quality, QualityLevel);
        }

        public static void SetVSync(bool on)
        {
            VSync = on;
            QualitySettings.vSyncCount = on ? 1 : 0;
            PlayerPrefs.SetInt(K_VSync, on ? 1 : 0);
        }

        public static void SetResolution(int w, int h)
        {
            ResWidth = w; ResHeight = h;
            Screen.SetResolution(w, h, Screen.fullScreenMode);
            PlayerPrefs.SetInt(K_ResW, w);
            PlayerPrefs.SetInt(K_ResH, h);
        }

        /// <summary>Re-apply every setting to the engine (used on boot).</summary>
        public static void ApplyAll()
        {
            AudioListener.volume = Master;
            Screen.fullScreenMode = Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            QualitySettings.SetQualityLevel(QualityLevel, true);
            QualitySettings.vSyncCount = VSync ? 1 : 0;
        }

        public static void Save() => PlayerPrefs.Save();
    }
}
