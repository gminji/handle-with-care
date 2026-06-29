using UnityEngine;
using SlopCo.Audio;
using SlopCo.Core;

namespace SlopCo.Player
{
    /// <summary>
    /// Head-mounted Voice Activity Indicator: while a player speaks through the proximity voice chat, a small
    /// team-colored sound-wave billboard pulses above their head and fades out when they stop. Its whole point
    /// is to make the (audio-only, invisible-in-a-muted-clip) voice feature VISIBLE in screenshots / clips /
    /// trailers — the genre's #1 sharing surface — and to read "who is talking" at a glance in co-op.
    ///
    /// Presentation only: it READS <see cref="PlayerVoice.SpeakingIntensity"/> (derived locally on every client
    /// from already-replicated voice frames) and the player's <see cref="PlayerController.CurrentColor"/>. No
    /// netcode of its own — each client renders every player's indicator independently. Reuses the Canvas-free
    /// TextMesh billboard pattern from <see cref="SlopCo.Gameplay.FloatingNumber"/> (built-in font, faces
    /// Camera.main), so it needs no new art asset.
    /// </summary>
    public sealed class VoiceIndicator : MonoBehaviour
    {
        [Tooltip("Voice engine on this player. Auto-filled from this GameObject if left empty.")]
        [SerializeField] private PlayerVoice voice;
        [Tooltip("Player controller (for team-color tint). Auto-filled from this GameObject if left empty.")]
        [SerializeField] private PlayerController playerController;
        [Tooltip("Billboard height above the player's origin.")]
        [SerializeField] private float heightOffset = GameConstants.VoiceIndicatorHeight;

        // A music/sound glyph (U+266A eighth note) present in the built-in LegacyRuntime (Arial) font that
        // reads unmistakably as "making sound" and is guaranteed to render (no new art asset).
        private const string SoundGlyph = "♪";

        private TextMesh _tm;
        private Transform _billboard;
        private Camera _cam;

        private void Awake()
        {
            if (voice == null) voice = GetComponent<PlayerVoice>();
            if (playerController == null) playerController = GetComponent<PlayerController>();
        }

        private void OnDisable()
        {
            // Hide if the component is disabled mid-speech (e.g. despawn) so nothing lingers.
            if (_billboard != null) _billboard.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (voice == null) return;

            float intensity = voice.SpeakingIntensity;
            if (intensity <= 0f)
            {
                if (_billboard != null && _billboard.gameObject.activeSelf)
                    _billboard.gameObject.SetActive(false);
                return;
            }

            EnsureBillboard();
            if (_billboard == null) return;
            if (!_billboard.gameObject.activeSelf) _billboard.gameObject.SetActive(true);

            // Position above the head and face the camera (FloatingNumber pattern).
            if (_cam == null) _cam = Camera.main;
            Vector3 pos = transform.position + Vector3.up * heightOffset;
            _billboard.position = pos;
            if (_cam != null)
                _billboard.rotation = Quaternion.LookRotation(pos - _cam.transform.position);

            // Pulse the size with the live speech, and tint with the team color; alpha fades on the debounce tail.
            float size = VoiceActivity.PulseScale(
                Time.time, intensity,
                GameConstants.VoiceIndicatorBaseScale,
                GameConstants.VoiceIndicatorPulseAmp,
                GameConstants.VoiceIndicatorPulseSpeed);
            _tm.characterSize = size;

            Color c = playerController != null ? playerController.CurrentColor : Color.white;
            c.a = Mathf.Clamp01(intensity);
            _tm.color = c;
        }

        private void EnsureBillboard()
        {
            if (_billboard != null) return;

            var go = new GameObject("VoiceIndicator");
            go.transform.SetParent(transform, false); // child → auto-destroyed with the player; world pose set each frame
            _billboard = go.transform;

            _tm = go.AddComponent<TextMesh>();
            _tm.text = SoundGlyph;
            _tm.fontSize = 64;
            _tm.characterSize = GameConstants.VoiceIndicatorBaseScale;
            _tm.anchor = TextAnchor.MiddleCenter;
            _tm.alignment = TextAlignment.Center;
            _tm.fontStyle = FontStyle.Bold;

            var font = (Font)Resources.GetBuiltinResource(typeof(Font), "LegacyRuntime.ttf");
            if (font != null)
            {
                _tm.font = font;
                var mr = go.GetComponent<MeshRenderer>();
                if (mr != null) mr.sharedMaterial = font.material;
            }
        }
    }
}
