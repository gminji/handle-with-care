using UnityEngine;

namespace SlopCo.Audio
{
    /// <summary>
    /// Installs the concrete <see cref="NgoVoiceProvider"/> into <see cref="ProximityVoiceBridge"/> for the
    /// session and clears it on teardown (mirrors NetworkSessionManager's install/clear lifecycle, so a new
    /// session never keeps a stale provider). Drop one on the Bootstrap scene. Per-player <see cref="PlayerVoice"/>
    /// instances self-register with the provider on spawn.
    /// </summary>
    [DefaultExecutionOrder(-100)] // install before player objects spawn and try to Bind
    public sealed class VoiceSystem : MonoBehaviour
    {
        private void Awake() => ProximityVoiceBridge.SetProvider(new NgoVoiceProvider());

        private void OnDestroy() => ProximityVoiceBridge.SetProvider(null); // → resets to NullVoiceProvider
    }
}
