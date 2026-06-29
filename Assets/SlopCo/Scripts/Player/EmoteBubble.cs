using UnityEngine;
using SlopCo.Core;

namespace SlopCo.Player
{
    /// <summary>
    /// Transient head-mounted emote bubble: a Canvas-free <see cref="TextMesh"/> billboard that pops above a
    /// player when they fire an emote, faces the camera, then fades and self-destructs. Reuses the
    /// <see cref="VoiceIndicator"/> / <see cref="SlopCo.Gameplay.FloatingNumber"/> rendering (built-in font, no
    /// new art asset). Presentation only — spawned locally on every client by <see cref="PingEmoteController"/>.
    /// </summary>
    public sealed class EmoteBubble : MonoBehaviour
    {
        private Transform _owner;
        private TextMesh _tm;
        private Camera _cam;
        private float _age;
        private float _life;

        /// <summary>Spawn a bubble child over <paramref name="owner"/>; returns the GameObject so the caller can cap to one.</summary>
        public static GameObject Spawn(Transform owner, string glyph, string label, Color color)
        {
            var go = new GameObject("EmoteBubble");
            go.transform.SetParent(owner, false);   // child → auto-destroyed with the player; world pose set each frame

            var tm = go.AddComponent<TextMesh>();
            tm.text = string.IsNullOrEmpty(label) ? glyph : glyph + "\n" + label;
            tm.color = color;
            tm.fontSize = 64;
            tm.characterSize = GameConstants.EmoteBubbleScale;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontStyle = FontStyle.Bold;

            var font = (Font)Resources.GetBuiltinResource(typeof(Font), "LegacyRuntime.ttf");
            if (font != null)
            {
                tm.font = font;
                var mr = go.GetComponent<MeshRenderer>();
                if (mr != null) mr.sharedMaterial = font.material;
            }

            var b = go.AddComponent<EmoteBubble>();
            b._owner = owner;
            b._life = GameConstants.EmoteBubbleSeconds;
            return go;
        }

        private void Awake()
        {
            _cam = Camera.main;
            _tm = GetComponent<TextMesh>();
        }

        private void Update()
        {
            _age += Time.deltaTime;

            if (_owner != null)
                transform.position = _owner.position + Vector3.up * GameConstants.EmoteBubbleHeight;

            if (_cam == null) _cam = Camera.main;
            if (_cam != null)
                transform.rotation = Quaternion.LookRotation(transform.position - _cam.transform.position);

            if (_tm != null)
            {
                float t = _life > 0f ? _age / _life : 1f;
                var c = _tm.color; c.a = Mathf.Clamp01(1f - t * t); _tm.color = c;
            }

            if (_age >= _life) Destroy(gameObject);
        }
    }
}
