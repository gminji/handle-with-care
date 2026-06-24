using UnityEngine;
using SlopCo.Core;
using SlopCo.Cargo;

namespace SlopCo.Gameplay
{
    /// <summary>
    /// Converts the already-broadcast FX events into on-screen WEIGHT: camera shake, a debris/confetti
    /// particle burst, and a world-space floating number — all scaled by severity so a $200 catastrophe
    /// reads bigger than a $3 scuff. This is the "OH MY GOD" / clip primitive the whole virality loop
    /// depends on. Runs on every client (events are replicated), no netcode added.
    /// </summary>
    public sealed class Juice : MonoBehaviour
    {
        private static Material _particleMat;

        private void OnEnable()
        {
            CargoCondition.OnDamageFx += HandleDamage;
            DeliveryZone.OnDelivered += HandleDeliver;
        }

        private void OnDisable()
        {
            CargoCondition.OnDamageFx -= HandleDamage;
            DeliveryZone.OnDelivered -= HandleDeliver;
        }

        private void HandleDamage(Vector3 pos, int valueLost, bool bigSmash)
        {
            float sev = Mathf.Clamp01(valueLost / 60f);
            ScreenShake.Add(bigSmash ? 0.7f : 0.12f + 0.4f * sev);
            Burst(pos + Vector3.up * 0.5f,
                  bigSmash ? new Color(1f, 0.5f, 0.2f) : new Color(0.85f, 0.85f, 0.9f),
                  bigSmash ? 30 : 8 + Mathf.RoundToInt(sev * 14f),
                  bigSmash ? 6.5f : 3f);
            FloatingNumber.Spawn(pos + Vector3.up * 1.3f, "-$" + valueLost,
                  bigSmash ? Color.red : new Color(1f, 0.6f, 0.3f), bigSmash ? 1.7f : 1f);
        }

        private void HandleDeliver(Vector3 pos, int payout)
        {
            ScreenShake.Add(0.22f);
            Burst(pos + Vector3.up * 1f, new Color(0.35f, 1f, 0.45f), 32, 5.5f);
            FloatingNumber.Spawn(pos + Vector3.up * 1.7f, "+$" + payout, new Color(0.4f, 1f, 0.5f), 1.5f);
        }

        private static Material ParticleMat()
        {
            if (_particleMat != null) return _particleMat;
            var sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            _particleMat = new Material(sh);
            return _particleMat;
        }

        private static void Burst(Vector3 pos, Color color, int count, float speed)
        {
            var go = new GameObject("FX_Burst");
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop();
            var main = ps.main;
            main.startLifetime = 0.7f;
            main.startSpeed = speed;
            main.startSize = 0.14f;
            main.gravityModifier = 1.3f;
            main.startColor = color;
            main.maxParticles = count;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.enabled = false;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Sphere; shape.radius = 0.12f;
            var r = ps.GetComponent<ParticleSystemRenderer>();
            r.material = ParticleMat();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            ps.Emit(count);
            Object.Destroy(go, 1.3f);
        }
    }
}
