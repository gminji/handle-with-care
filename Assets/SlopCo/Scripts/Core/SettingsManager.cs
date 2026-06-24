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

        public static float Master { get; private set; } = 0.8f;
        public static float Music  { get; private set; } = 0.8f;
        public static float Sfx    { get; private set; } = 0.8f;
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
