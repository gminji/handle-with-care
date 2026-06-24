using UnityEngine;

namespace SlopCo.Gameplay
{
    /// <summary>
    /// A world-space "-$80" / "+$240" number that pops at the impact point, rises, faces the camera and
    /// fades. This is the game's signature comedy-as-scoring hook made legible in a 3-second muted clip —
    /// spawned at the worldPos the FX events already broadcast. Self-destructing; no pooling needed at
    /// slice scale. Uses TextMesh so it needs no Canvas.
    /// </summary>
    public sealed class FloatingNumber : MonoBehaviour
    {
        private TextMesh _tm;
        private Camera _cam;
        private float _age;
        private float _life = 1.1f;

        public static void Spawn(Vector3 pos, string text, Color color, float scale)
        {
            var go = new GameObject("FloatingNumber");
            go.transform.position = pos;
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.color = color;
            tm.fontSize = 64;
            tm.characterSize = 0.14f * scale;
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
            go.AddComponent<FloatingNumber>();
        }

        private void Awake()
        {
            _cam = Camera.main;
            _tm = GetComponent<TextMesh>();
        }

        private void Update()
        {
            if (_cam == null) _cam = Camera.main;
            _age += Time.deltaTime;
            float t = _age / _life;

            transform.position += Vector3.up * (Time.deltaTime * 1.3f);
            if (_cam != null)
                transform.rotation = Quaternion.LookRotation(transform.position - _cam.transform.position);

            if (_tm != null)
            {
                var c = _tm.color; c.a = Mathf.Clamp01(1f - t * t); _tm.color = c;
            }
            transform.localScale = Vector3.one * (1f + 0.35f * Mathf.Sin(Mathf.Clamp01(t * 2f) * Mathf.PI));

            if (_age >= _life) Destroy(gameObject);
        }
    }
}
