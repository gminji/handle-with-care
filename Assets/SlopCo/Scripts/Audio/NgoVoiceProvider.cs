using System.Collections.Generic;
using UnityEngine;

namespace SlopCo.Audio
{
    /// <summary>
    /// Concrete <see cref="IVoiceProvider"/> for the custom NGO proximity-voice pipeline. It is a thin
    /// coordinator: each player's <see cref="PlayerVoice"/> NetworkBehaviour does the real capture/relay/
    /// playback and self-registers here on spawn via <see cref="Bind"/>. The provider keeps the
    /// <c>clientId → PlayerVoice</c> map so the existing seam (notably <c>Provider.Mute(clientId, true)</c>
    /// for the death hard-cut comedy beat) routes to the right player.
    ///
    /// Installed into <see cref="ProximityVoiceBridge.SetProvider"/> by <see cref="VoiceSystem"/>.
    /// </summary>
    public sealed class NgoVoiceProvider : IVoiceProvider
    {
        private readonly Dictionary<ulong, PlayerVoice> _voices = new();

        /// <summary>Called by a remote/local <see cref="PlayerVoice"/> from OnNetworkSpawn.</summary>
        public void Bind(ulong clientId, PlayerVoice voice)
        {
            if (voice == null) return;
            _voices[clientId] = voice;
        }

        /// <summary>Called by <see cref="PlayerVoice"/> from OnNetworkDespawn. Same effect as Unregister.</summary>
        public void Unbind(ulong clientId) => _voices.Remove(clientId);

        // ── IVoiceProvider seam ──────────────────────────────────
        // The owner is identified by NetworkBehaviour.IsOwner inside PlayerVoice, so SetLocalPlayer /
        // RegisterRemote are no-ops in this implementation (PlayerVoice self-binds). They remain to keep
        // the seam contract intact for alternative providers (e.g. a future Dissonance swap).
        public void SetLocalPlayer(Transform localPlayer) { }
        public void RegisterRemote(ulong clientId, Transform remotePlayer) { }

        public void Unregister(ulong clientId) => Unbind(clientId);

        public void Mute(ulong clientId, bool muted)
        {
            if (_voices.TryGetValue(clientId, out var v) && v != null)
                v.SetMuted(muted);
        }
    }
}
