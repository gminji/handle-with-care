using Unity.Netcode;
using UnityEngine;
using SlopCo.Core;
using SlopCo.Gameplay;

namespace SlopCo.Cargo
{
    /// <summary>
    /// THE HOOK. Turns a cargo into a LIVE BOMB whose fuse is the replicated <see cref="CargoCondition"/>
    /// (1 = calm, 0 = boom). The fuse burns down slowly on its own AND every bump drops it (the existing
    /// impact→Condition pipeline), so "carry it gently before it goes off in your friend's arms". As the
    /// fuse shortens the mesh glows redder and pulses faster on every client; at zero the server detonates:
    /// explosion force on nearby rigidbodies, screen shake + particle burst on everyone, and the run ends.
    /// Reuses Condition / ScreenShake / RoundManager — no netcode or physics-model changes.
    /// </summary>
    [RequireComponent(typeof(CargoCondition))]
    public sealed class CargoBomb : NetworkBehaviour
    {
        [Tooltip("Mesh renderers that pulse red as the fuse burns.")]
        [SerializeField] private Renderer[] glowRenderers;
        [Tooltip("Fuse burned per second even if untouched (bumps burn it faster via impact damage).")]
        [SerializeField] private float fuseDecayPerSecond = 0.03f;
        [Tooltip("Explosion radius / force at detonation.")]
        [SerializeField] private float blastRadius = 6f;
        [SerializeField] private float blastForce = 900f;

        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private CargoCondition _condition;
        private Rigidbody _rb;
        private MaterialPropertyBlock _mpb;
        private bool _detonated;
        private static Material _boomMat;

        public override void OnNetworkSpawn()
        {
            _condition = GetComponent<CargoCondition>();
            _rb = GetComponent<Rigidbody>();
            _mpb = new MaterialPropertyBlock();
        }

        private void Update()
        {
            if (_condition == null) return;
            float fuse = Mathf.Clamp01(_condition.Condition.Value); // 1 calm → 0 boom
            float danger = 1f - fuse;

            // Visual fuse — pulse faster + redder as it burns (all clients, off the replicated value).
            float hz = Mathf.Lerp(1.5f, 22f, danger);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * hz);
            float intensity = (0.25f + 2.75f * danger) * (0.4f + 0.6f * pulse);
            Color glow = new Color(1f, 0.12f, 0.06f) * intensity;
            if (glowRenderers != null)
                foreach (var r in glowRenderers)
                {
                    if (r == null) continue;
                    r.GetPropertyBlock(_mpb);
                    _mpb.SetColor(EmissionId, glow);
                    r.SetPropertyBlock(_mpb);
                }

            if (!IsServer) return;
            // Server burns the fuse over time; impacts already drop Condition via CargoCondition.ApplyImpact.
            if (!_detonated && fuse > 0f)
                _condition.Condition.Value = Mathf.Max(0f, _condition.Condition.Value - fuseDecayPerSecond * Time.deltaTime);
            if (!_detonated && _condition.Condition.Value <= 0.001f)
                Detonate();
        }

        private void Detonate()
        {
            _detonated = true;
            DetonateFxRpc(transform.position);

            foreach (var col in Physics.OverlapSphere(transform.position, blastRadius))
            {
                var rb = col.attachedRigidbody;
                if (rb != null && rb != _rb)
                    rb.AddExplosionForce(blastForce, transform.position, blastRadius, 1.5f, ForceMode.Impulse);
            }

            var rm = ServiceLocator.Get<RoundManager>();
            if (rm != null) rm.EndRunNow();

            var netObj = GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned) netObj.Despawn(true);
        }

        [Rpc(SendTo.Everyone)]
        private void DetonateFxRpc(Vector3 pos)
        {
            ScreenShake.Add(1f);
            Boom(pos);
        }

        private static void Boom(Vector3 pos)
        {
            if (_boomMat == null)
            {
                var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
                if (sh == null) sh = Shader.Find("Sprites/Default");
                _boomMat = new Material(sh);
            }
            var go = new GameObject("FX_Boom");
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop();
            var main = ps.main;
            main.startLifetime = 0.9f;
            main.startSpeed = 11f;
            main.startSize = 0.35f;
            main.gravityModifier = 1.1f;
            main.startColor = new Color(1f, 0.55f, 0.15f);
            main.maxParticles = 60;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.enabled = false;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.2f;
            ps.GetComponent<ParticleSystemRenderer>().material = _boomMat;
            ps.Emit(60);
            Object.Destroy(go, 1.4f);
        }
    }
}
