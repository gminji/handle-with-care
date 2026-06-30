using UnityEngine;

namespace SlopCo.Items
{
    /// <summary>
    /// Tiny self-destructing pickup "pop" — a sphere that scales up over a short lifetime, then destroys itself.
    /// Spawned CLIENT-side by <see cref="ItemCapsule"/>'s reveal (no networking, no art asset) so the gacha
    /// pickup reads on every screen. A richer per-item kit model is a later art pass (see ItemDef.modelKey).
    /// </summary>
    public sealed class RevealPop : MonoBehaviour
    {
        [SerializeField] private float lifetime = 0.5f;
        private float _t;
        private Renderer _r;
        private MaterialPropertyBlock _mpb;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private void Awake() { _r = GetComponent<Renderer>(); _mpb = new MaterialPropertyBlock(); }

        private void Update()
        {
            _t += Time.deltaTime;
            float k = _t / lifetime;
            if (k > 1f) k = 1f;
            transform.localScale = Vector3.one * Mathf.Lerp(0.3f, 1.4f, k);
            if (_r != null)
            {
                _r.GetPropertyBlock(_mpb);
                _mpb.SetColor(BaseColorId, new Color(1f, 0.9f, 0.4f));
                _r.SetPropertyBlock(_mpb);
            }
            if (_t >= lifetime) Destroy(gameObject);
        }

        /// <summary>Spawn a one-shot reveal pop at a world point (client-side, auto-destroys).</summary>
        public static void Spawn(Vector3 pos)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "ItemRevealPop";
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
            go.transform.position = pos;
            go.transform.localScale = Vector3.one * 0.3f;
            go.AddComponent<RevealPop>();
        }
    }
}
