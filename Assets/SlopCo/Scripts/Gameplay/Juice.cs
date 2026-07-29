using UnityEngine;
using SlopCo.Core;
using SlopCo.Cargo;
using SlopCo.UI;    // UITheme — world FX shares the HUD's payout/damage colours (see HandleDamage/HandleDeliver)

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

        private float _hitStop;

        private void OnEnable()
        {
            CargoCondition.OnDamageFx += HandleDamage;
            DeliveryZone.OnDelivered += HandleDeliver;
            CargoBomb.OnDetonated += HandleDetonation;
        }

        private void OnDisable()
        {
            CargoCondition.OnDamageFx -= HandleDamage;
            DeliveryZone.OnDelivered -= HandleDeliver;
            CargoBomb.OnDetonated -= HandleDetonation;
            if (_hitStop > 0f) { _hitStop = 0f; Time.timeScale = 1f; } // restore on teardown
        }

        // The boom is the money shot: brief slow-mo + a heavy shake punch on any detonation.
        private void HandleDetonation(Vector3 pos)
        {
            ScreenShake.Add(1.2f);
            _hitStop = 0.35f;
            Time.timeScale = 0.18f;
        }

        private void Update()
        {
            if (_hitStop > 0f)
            {
                _hitStop -= Time.unscaledDeltaTime;
                if (_hitStop <= 0f) Time.timeScale = 1f;
            }
        }

        private void HandleDamage(Vector3 pos, int valueLost, bool bigSmash)
        {
            float sev = Mathf.Clamp01(valueLost / 60f);
            ScreenShake.Add(bigSmash ? 0.7f : 0.12f + 0.4f * sev);
            Burst(pos + Vector3.up * 0.5f,
                  bigSmash ? new Color(1f, 0.5f, 0.2f) : new Color(0.85f, 0.85f, 0.9f),
                  bigSmash ? 30 : 8 + Mathf.RoundToInt(sev * 14f),
                  bigSmash ? 6.5f : 3f);
            // Same tokens HudController:105 uses for the canvas-side popup — the two fire on one event and
            // must read as the same colour in world space and on the HUD.
            FloatingNumber.Spawn(pos + Vector3.up * 1.3f, "-$" + valueLost,
                  bigSmash ? UITheme.DangerFill : UITheme.Warning, bigSmash ? 1.7f : 1f);
        }

        private void HandleDeliver(Vector3 pos, int payout)
        {
            ScreenShake.Add(0.22f);
            Burst(pos + Vector3.up * 1f, UITheme.SuccessFill, 32, 5.5f);
            FloatingNumber.Spawn(pos + Vector3.up * 1.7f, "+$" + payout, UITheme.PayoutGreen, 1.5f);
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
