using UnityEngine;

namespace SlopCo.Audio
{
    /// <summary>
    /// Abstraction for spatial/proximity voice. The slice ships the no-op <see cref="NullVoiceProvider"/>
    /// (the game is SILENT — see the Fun-Risk Register: voice is the genre's #1 comedy engine and is the
    /// one feature deliberately deferred). The interface is the load-bearing seam the dev implements next.
    /// </summary>
    public interface IVoiceProvider
    {
        void SetLocalPlayer(Transform localPlayer);
        void RegisterRemote(ulong clientId, Transform remotePlayer);
        void Unregister(ulong clientId);
        void Mute(ulong clientId, bool muted); // hard-cut on death = the signature comedy beat
    }

    public sealed class NullVoiceProvider : IVoiceProvider
    {
        public void SetLocalPlayer(Transform localPlayer) { }
        public void RegisterRemote(ulong clientId, Transform remotePlayer) { }
        public void Unregister(ulong clientId) { }
        public void Mute(ulong clientId, bool muted) { }
    }

    /// <summary>
    /// Holds the active voice provider. Player spawn/despawn code can call Provider.RegisterRemote /
    /// Unregister; death code calls Provider.Mute.
    ///
    /// DISSONANCE WIRING (recommended next step — the highest-leverage viral feature):
    ///   1. Install "Dissonance Voice Chat" + its NGO integration package.
    ///   2. Add DissonanceComms + the NGO comms network to the Bootstrap scene.
    ///   3. On each player prefab add a VoiceBroadcastTrigger + VoiceReceiptTrigger set to a positional
    ///      channel; set the AudioSource spatial blend to 3D so distance attenuation works.
    ///   4. Implement an IVoiceProvider that maps clientId → DissonancePlayer and calls SetProvider here.
    ///   5. On player death, call Provider.Mute(clientId, true) so the voice hard-cuts mid-scream.
    /// </summary>
    public sealed class ProximityVoiceBridge : MonoBehaviour
    {
        public static IVoiceProvider Provider { get; private set; } = new NullVoiceProvider();

        public static void SetProvider(IVoiceProvider provider)
            => Provider = provider ?? new NullVoiceProvider();

        private void OnDestroy()
        {
            // Reset to null provider so a new session doesn't keep a stale Dissonance binding.
            Provider = new NullVoiceProvider();
        }
    }
}
