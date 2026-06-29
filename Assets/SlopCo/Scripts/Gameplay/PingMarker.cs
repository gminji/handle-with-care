using UnityEngine;
using SlopCo.Core;

namespace SlopCo.Gameplay
{
    /// <summary>
    /// Transient world-space ping marker: a Canvas-free <see cref="TextMesh"/> billboard dropped at an aimed
    /// world point, gently bobbing, facing the camera, then holding and fading before it self-destructs. Reuses
    /// the <see cref="FloatingNumber"/> rendering (built-in font, no new art). Presentation only — spawned
    /// locally on every client by PingEmoteController; unparented so it stays where it was placed.
    /// </summary>
    public sealed class PingMarker : MonoBehaviour
    {
        private TextMesh _tm;
        private Camera _cam;
        private Vector3 _base;
        private float _age;
        private float _life;

        public static GameObject Spawn(Vector3 hitPoint, string glyph, string label, Color color)
        {
            var go = new GameObject("PingMarker");
            go.transform.position = hitPoint + Vector3.up * GameConstants.PingMarkerHeight;

            var tm = go.AddComponent<TextMesh>();
            tm.text = string.IsNullOrEmpty(label) ? glyph : glyph + "\n" + label;
            tm.color = color;
            tm.fontSize = 64;
            tm.characterSize = GameConstants.PingMarkerScale;
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

            var m = go.AddComponent<PingMarker>();
            m._base = go.transform.position;
            m._life = GameConstants.PingMarkerSeconds;
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

            // Gentle bob in place so it reads as a live marker.
            transform.position = _base + Vector3.up * (Mathf.Sin(Time.time * 3f) * 0.12f);

            if (_cam == null) _cam = Camera.main;
            if (_cam != null)
                transform.rotation = Quaternion.LookRotation(transform.position - _cam.transform.position);

            // Hold full opacity, then fade over the last 40% of life.
            if (_tm != null)
            {
                float t = _life > 0f ? _age / _life : 1f;
                float a = t < 0.6f ? 1f : Mathf.Clamp01(1f - (t - 0.6f) / 0.4f);
                var c = _tm.color; c.a = a; _tm.color = c;
            }

            if (_age >= _life) Destroy(gameObject);
        }
    }
}
