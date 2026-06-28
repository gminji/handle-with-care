using System;
using System.Threading;
using Unity.Netcode;
using UnityEngine;
using SlopCo.Core;

namespace SlopCo.Audio
{
    /// <summary>
    /// Per-player voice engine (sits on the player prefab). The local player captures the mic, normalizes
    /// it to the fixed wire format (mono PCM16 @ 16 kHz, 20 ms frames), gates silence (VAD) and uploads
    /// frames to the server; the server relays each frame to everyone EXCEPT the speaker (echo-free), and
    /// every client streams the received audio through this player's 3D <see cref="AudioSource"/> so distance
    /// = volume (proximity). Death/leave hard-cuts via <see cref="SetMuted"/> (the genre's comedy beat).
    ///
    /// Threading: the RPC handler (<see cref="PlayVoiceClientRpc"/>) runs on the MAIN thread (producer) and
    /// <see cref="OnAudioRead"/> runs on the AUDIO thread (consumer). The playback ring is a strict
    /// single-producer/single-consumer queue: only the producer writes <c>_writeIdx</c>, only the consumer
    /// writes <c>_readIdx</c>; on a full buffer the producer DROPS the frame (never touches _readIdx).
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class PlayerVoice : NetworkBehaviour
    {
        // ── Playback ring (SPSC) ────────────────────────────────
        private float[] _ring;
        private int _cap;
        private long _writeIdx;     // producer-only writes (main thread)
        private long _readIdx;      // consumer-only writes (audio thread)
        private volatile bool _muted;

        private AudioSource _source;

        // ── Receive scratch (main thread only) ──────────────────
        private float[] _rxDecode;

        // ── Capture (owner/local player only) ───────────────────
        private bool _capturing;
        private AudioClip _micClip;
        private string _micDevice;
        private int _micRate;
        private int _micLen;
        private int _micReadPos;
        private float[] _devRead;   // fixed device-rate chunk (~one 20 ms frame worth)
        private float[] _wireTmp;   // resampled chunk at wire rate
        private float[] _wireAccum; // wire-rate FIFO to slice exact 320-sample frames
        private int _wireCount;
        private byte[] _txBytes;    // reused 640-byte send buffer

        public override void OnNetworkSpawn()
        {
            _cap = GameConstants.VoiceSampleRate * GameConstants.VoiceRingSeconds;
            _ring = new float[_cap];
            _rxDecode = new float[GameConstants.VoiceFrameSamples];

            ConfigureAudioSource();

            // Register with the active provider so the existing seam (Mute on death, etc.) can route to us.
            // Only real player objects bind (bots are server-owned non-player-objects and never speak).
            if (NetworkObject.IsPlayerObject && ProximityVoiceBridge.Provider is NgoVoiceProvider provider)
                provider.Bind(OwnerClientId, this);

            // Only the actual local human captures a mic (bots / remote copies do not).
            if (IsLocalPlayer)
                TryStartCapture();
        }

        public override void OnNetworkDespawn()
        {
            StopCapture();
            if (ProximityVoiceBridge.Provider is NgoVoiceProvider provider)
                provider.Unbind(OwnerClientId);
            if (_source != null) _source.Stop();
        }

        // ── Setup ───────────────────────────────────────────────
        private void ConfigureAudioSource()
        {
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = true;
            _source.spatialBlend = 1f;                 // fully 3D → proximity attenuation
            _source.rolloffMode = AudioRolloffMode.Linear;
            _source.minDistance = GameConstants.VoiceMinDistance;
            _source.maxDistance = GameConstants.VoiceMaxDistance;
            _source.volume = SettingsManager.VoiceVolume;

            // Streaming clip pulls from our ring via OnAudioRead (audio thread). Looping keeps it pumping.
            _source.clip = AudioClip.Create(
                "VoiceStream", _cap, 1, GameConstants.VoiceSampleRate, true, OnAudioRead);
            _source.Play();
        }

        // ── Mute (death/leave hard-cut) ─────────────────────────
        public void SetMuted(bool muted) => _muted = muted;

        // ── Capture (local player) ──────────────────────────────
        private void TryStartCapture()
        {
            if (_capturing) return;
            if (!SettingsManager.VoiceEnabled) return;

            if (Microphone.devices == null || Microphone.devices.Length == 0)
            {
                Debug.LogWarning("[PlayerVoice] No microphone device available — voice capture disabled (game continues, silent).");
                return;
            }

            _micDevice = null; // default device
            Microphone.GetDeviceCaps(_micDevice, out int min, out int max);
            int requested = GameConstants.VoiceSampleRate;
            if (min > 0) requested = Mathf.Max(requested, min);
            if (max > 0) requested = Mathf.Min(requested, max);

            _micClip = Microphone.Start(_micDevice, true, 1, requested);
            if (_micClip == null)
            {
                Debug.LogWarning("[PlayerVoice] Microphone.Start returned null — voice capture disabled.");
                return;
            }

            _micRate = _micClip.frequency;       // trust the ACTUAL device rate, not the request
            _micLen = _micClip.samples;
            _micReadPos = 0;

            // One device chunk maps to ~one 20 ms wire frame; sizes are fixed so capture is allocation-free.
            int devChunk = Mathf.Max(1, Mathf.RoundToInt(GameConstants.VoiceFrameSamples * (float)_micRate / GameConstants.VoiceSampleRate));
            _devRead = new float[devChunk];
            _wireTmp = new float[GameConstants.VoiceFrameSamples + 16];
            _wireAccum = new float[GameConstants.VoiceFrameSamples * 3];
            _wireCount = 0;
            _txBytes = new byte[GameConstants.VoiceFrameSamples * 2]; // 640 bytes

            _capturing = true;
        }

        private void StopCapture()
        {
            if (!_capturing) return;
            _capturing = false;
            if (Microphone.IsRecording(_micDevice)) Microphone.End(_micDevice);
            _micClip = null;
        }

        private void Update()
        {
            // Keep remote playback volume in sync with the live setting.
            if (_source != null) _source.volume = SettingsManager.VoiceVolume;

            if (!IsLocalPlayer) return;

            // React to the user toggling voice on/off mid-session.
            if (!SettingsManager.VoiceEnabled) { if (_capturing) StopCapture(); return; }
            if (!_capturing) { TryStartCapture(); return; }

            CaptureTick();
        }

        private void CaptureTick()
        {
            if (_micClip == null) return;

            // Catch up on all device samples produced since last tick, in fixed chunks.
            for (int guard = 0; guard < 64; guard++)
            {
                int pos = Microphone.GetPosition(_micDevice);
                if (pos < 0) break;
                int avail = pos - _micReadPos;
                if (avail < 0) avail += _micLen;           // ring wrap-around
                if (avail < _devRead.Length) break;

                _micClip.GetData(_devRead, _micReadPos);   // reads _devRead.Length samples, wrapping the clip
                _micReadPos = (_micReadPos + _devRead.Length) % _micLen;

                int wired = VoiceMath.ResampleLinear(_devRead, _micRate, _wireTmp, GameConstants.VoiceSampleRate);
                AppendWire(_wireTmp, wired);
                DrainWireFrames();
            }
        }

        private void AppendWire(float[] src, int n)
        {
            if (n <= 0) return;
            if (_wireCount + n > _wireAccum.Length)
                n = _wireAccum.Length - _wireCount;        // safety; draining keeps this from triggering
            if (n <= 0) return;
            Array.Copy(src, 0, _wireAccum, _wireCount, n);
            _wireCount += n;
        }

        private void DrainWireFrames()
        {
            int frame = GameConstants.VoiceFrameSamples;
            while (_wireCount >= frame)
            {
                var span = new ReadOnlySpan<float>(_wireAccum, 0, frame);
                if (VoiceMath.IsVoiced(span, GameConstants.VoiceActivityRms))
                {
                    VoiceMath.EncodePcm16(span, _txBytes);
                    UploadVoiceServerRpc(_txBytes);        // serialized synchronously → _txBytes reuse is safe
                }
                // Slide the FIFO down whether or not we sent (silence frames are simply dropped).
                Array.Copy(_wireAccum, frame, _wireAccum, 0, _wireCount - frame);
                _wireCount -= frame;
            }
        }

        // ── Transport (NGO 2.x universal RPC, unreliable) ───────
        [Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable)]
        private void UploadVoiceServerRpc(byte[] pcm16, RpcParams rpc = default)
        {
            ulong speaker = rpc.Receive.SenderClientId;
            if (speaker != OwnerClientId) return;          // anti-spoof: only the owner may upload

            // Relay to everyone EXCEPT the speaker (host included) — the correct echo-free target.
            var target = RpcTarget.Not(speaker, RpcTargetUse.Temp);
            PlayVoiceClientRpc(pcm16, new RpcParams { Send = new RpcSendParams { Target = target } });
        }

        [Rpc(SendTo.SpecifiedInParams, Delivery = RpcDelivery.Unreliable)]
        private void PlayVoiceClientRpc(byte[] pcm16, RpcParams rpc = default)
        {
            if (_muted) return;                            // hard-cut: drop incoming while muted
            int n = VoiceMath.DecodePcm16(pcm16, _rxDecode);
            EnqueuePlayback(_rxDecode, n);
        }

        // ── Ring producer (main thread) ─────────────────────────
        private void EnqueuePlayback(float[] samples, int n)
        {
            if (n <= 0 || _ring == null) return;
            long w = _writeIdx;
            long r = Volatile.Read(ref _readIdx);
            int free = _cap - (int)(w - r);
            if (free < n) return;                          // buffer full → drop frame (inaudible 20 ms loss)
            for (int i = 0; i < n; i++)
                _ring[(int)((w + i) % _cap)] = samples[i];
            Volatile.Write(ref _writeIdx, w + n);          // publish AFTER data is written (release barrier)
        }

        // ── Ring consumer (AUDIO thread) ────────────────────────
        private void OnAudioRead(float[] data)
        {
            if (_ring == null) { Array.Clear(data, 0, data.Length); return; }

            long w = Volatile.Read(ref _writeIdx);         // acquire barrier
            long r = _readIdx;

            if (_muted)                                    // drain + silence so unmute starts fresh
            {
                Array.Clear(data, 0, data.Length);
                Volatile.Write(ref _readIdx, w);
                return;
            }

            int avail = (int)(w - r);
            int give = Math.Min(data.Length, avail);
            for (int i = 0; i < give; i++)
                data[i] = _ring[(int)((r + i) % _cap)];
            for (int i = give; i < data.Length; i++)
                data[i] = 0f;                              // underrun → silence
            Volatile.Write(ref _readIdx, r + give);
        }
    }
}
