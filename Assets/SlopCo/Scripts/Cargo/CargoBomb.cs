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

        /// <summary>Raised on every client when a bomb detonates (worldPos) — drives hit-stop / shake juice.</summary>
        public static event System.Action<Vector3> OnDetonated;

        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");
        private CargoCondition _condition;
        private CargoItem _cargoItem;
        private Rigidbody _rb;
        private MaterialPropertyBlock _mpb;
        private bool _detonated;
        private float _armTimer; // server-side post-spawn grace: fuse can't burn/detonate while > 0
        private float _blastMult = 1f; // today's modifier scales the blast (Demolition Day = bigger boom)
        private static Material _boomMat;

        public override void OnNetworkSpawn()
        {
            _condition = GetComponent<CargoCondition>();
            _cargoItem = GetComponent<CargoItem>();
            _rb = GetComponent<Rigidbody>();
            _mpb = new MaterialPropertyBlock();
            _armTimer = GameConstants.BombArmingSeconds; // never blow the instant it appears
        }

        /// <summary>SERVER. Arm the fuse with a per-second burn rate (escalates each day) and an optional
        /// blast multiplier from today's modifier. Clamped so a runaway value can't break chain-reaction physics.
        /// The single-arg shape stays valid for callers that don't scale the blast (backward compatible).</summary>
        public void Arm(float fuseDecay, float blastMult = 1f)
        {
            fuseDecayPerSecond = Mathf.Max(0f, fuseDecay);
            _blastMult = Mathf.Clamp(blastMult, 0.1f, 3f);
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
            // Delivered to the van = defused: never blows once it is safely dropped off.
            if (_cargoItem != null && _cargoItem.State.Value == CarryState.Delivered) return;

            // Arming grace: a fair window right after spawn — fuse paused, can't detonate. A fresh bomb
            // (or one thrown/dropped this instant) never explodes before the player can react.
            if (_armTimer > 0f) { _armTimer -= Time.deltaTime; return; }

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

            float radius = blastRadius * _blastMult;   // today's modifier can supersize the boom
            float force = blastForce * _blastMult;
            foreach (var col in Physics.OverlapSphere(transform.position, radius))
            {
                var rb = col.attachedRigidbody;
                if (rb != null && rb != _rb)
                    rb.AddExplosionForce(force, transform.position, radius, 1.5f, ForceMode.Impulse);

                // CHAIN REACTION: a blast lights any nearby bomb's fuse → cascading detonations.
                var other = col.GetComponentInParent<CargoBomb>();
                if (other != null && other != this && !other._detonated && other._condition != null)
                    other._condition.Condition.Value = 0f; // detonates on its next server tick
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
            OnDetonated?.Invoke(pos); // hit-stop / extra juice listeners
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
