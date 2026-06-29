using System;
using Unity.Netcode;
using UnityEngine;
using SlopCo.Core;
using SlopCo.UI;

namespace SlopCo.Player
{
    public enum PingEmoteKind : byte { Emote = 0, Ping = 1 }

    /// <summary>
    /// Networked quick-comms. The local human opens the radial wheel (<see cref="PingEmoteWheel"/>, owner-local)
    /// and fires a ping or emote; the choice is relayed owner→server→everyone using the same anti-spoof RPC
    /// pattern as <see cref="SlopCo.Audio.PlayerVoice"/> (reliable delivery, since these are discrete events, not
    /// a stream). Each client renders the result locally: an emote pops a team-colored bubble over the sender's
    /// head (<see cref="EmoteBubble"/>); a ping drops a slice-colored world marker at the aimed point
    /// (<see cref="SlopCo.Gameplay.PingMarker"/>). A static <see cref="OnPingEmote"/> event lets
    /// <see cref="SlopCo.Audio.GameAudio"/> play a cue with no extra netcode. Lives on the player prefab.
    /// </summary>
    public sealed class PingEmoteController : NetworkBehaviour
    {
        public readonly struct WheelItem
        {
            public readonly PingEmoteKind Kind;
            public readonly string Glyph;    // decorative; the localized label always carries the meaning
            public readonly string LocKey;
            public readonly Color Color;
            public WheelItem(PingEmoteKind k, string g, string loc, Color c) { Kind = k; Glyph = g; LocKey = loc; Color = c; }
        }

        // Index order == wheel slice order (slice 0 at 12 o'clock, clockwise). Single source of truth shared by
        // the wheel UI (draw slices) and the display dispatch (render by index).
        public static readonly WheelItem[] Items =
        {
            new WheelItem(PingEmoteKind.Ping,  "!", "ping.here",    new Color(1f, 1f, 1f)),        // 0 Here
            new WheelItem(PingEmoteKind.Ping,  "▲", "ping.danger",  new Color(1f, 0.35f, 0.25f)), // 1 Danger ▲
            new WheelItem(PingEmoteKind.Ping,  "$", "ping.cargo",   new Color(1f, 0.85f, 0.3f)),   // 2 Cargo
            new WheelItem(PingEmoteKind.Emote, "♥", "emote.thanks", new Color(1f, 0.45f, 0.6f)),  // 3 Thanks ♥
            new WheelItem(PingEmoteKind.Emote, "✓", "emote.yes",    new Color(0.4f, 1f, 0.5f)),   // 4 Yes ✓
            new WheelItem(PingEmoteKind.Emote, "✕", "emote.no",     new Color(0.9f, 0.5f, 0.3f)), // 5 No ✕
        };

        /// <summary>Raised on every client when any player's ping/emote is shown (drives GameAudio). bool = isPing.</summary>
        public static event Action<bool, Vector3> OnPingEmote;

        [SerializeField] private PlayerController playerController;
        [SerializeField] private PingEmoteWheel wheel;

        private float _lastServerSend = -999f;
        private float _lastLocalSend = -999f;
        private GameObject _liveBubble;
        private GameObject _liveMarker;

        /// <summary>True only for the local human (owner + player object). Bots are non-player-objects, so
        /// <c>IsLocalPlayer</c> is false for them and they never open the wheel.</summary>
        public bool IsLocalHuman => IsOwner && IsLocalPlayer;

        private void Awake()
        {
            if (playerController == null) playerController = GetComponent<PlayerController>();
            if (wheel == null) wheel = GetComponent<PingEmoteWheel>();
        }

        public override void OnNetworkSpawn()
        {
            // Only the local human gets the wheel (it stays disabled on remote copies and bots).
            if (IsLocalHuman && wheel != null) wheel.enabled = true;
        }

        public override void OnNetworkDespawn()
        {
            // The bubble is a child of the player → auto-destroyed; the marker is unparented → clean it up.
            if (_liveMarker != null) Destroy(_liveMarker);
        }

        /// <summary>OWNER-LOCAL entry point, called by the wheel on release. Soft client cooldown for snappy
        /// feedback; the server enforces the authoritative rate limit.</summary>
        public void Send(int index, Vector3 worldPos)
        {
            if (!IsOwner) return;
            if (index < 0 || index >= Items.Length) return;
            if (Time.time - _lastLocalSend < GameConstants.PingEmoteCooldown) return;
            _lastLocalSend = Time.time;
            SubmitServerRpc((byte)index, worldPos);
        }

        [Rpc(SendTo.Server)]
        private void SubmitServerRpc(byte index, Vector3 worldPos, RpcParams rpc = default)
        {
            if (rpc.Receive.SenderClientId != OwnerClientId) return;                    // anti-spoof (PlayerVoice)
            if (Time.time - _lastServerSend < GameConstants.PingEmoteCooldown) return;  // authoritative rate limit
            _lastServerSend = Time.time;
            if (index >= Items.Length) return;
            ShowClientRpc(index, worldPos);
        }

        [Rpc(SendTo.Everyone)]
        private void ShowClientRpc(byte index, Vector3 worldPos)
        {
            if (index >= Items.Length) return;
            WheelItem item = Items[index];
            string label = Localization.Get(item.LocKey);
            Color teamCol = playerController != null ? playerController.CurrentColor : Color.white;

            if (item.Kind == PingEmoteKind.Emote)
            {
                if (_liveBubble != null) Destroy(_liveBubble);
                _liveBubble = EmoteBubble.Spawn(transform, item.Glyph, label, teamCol);
            }
            else
            {
                if (_liveMarker != null) Destroy(_liveMarker);
                _liveMarker = SlopCo.Gameplay.PingMarker.Spawn(worldPos, item.Glyph, label, item.Color);
            }

            OnPingEmote?.Invoke(item.Kind == PingEmoteKind.Ping, worldPos);
        }
    }
}
