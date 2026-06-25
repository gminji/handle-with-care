using UnityEngine;
using SlopCo.Core;
using SlopCo.Cargo;
using SlopCo.Gameplay;
using SlopCo.Player;

namespace SlopCo.Audio
{
    /// <summary>
    /// Turns the already-wired gameplay FX events into sound — the project's #1 sellability gap was
    /// total silence. Subscribes to the SAME broadcast events the HUD uses (so audio is correct on every
    /// client with no extra netcode): cargo smash, delivery, round phase. One 2D SFX bus; volume routed
    /// through <see cref="SettingsManager.Sfx"/>/<see cref="SettingsManager.Music"/> so the Options
    /// sliders are now real. Severity (valueLost) scales the smash volume/clip so a $200 catastrophe
    /// hits harder than a $3 scuff.
    /// </summary>
    public sealed class GameAudio : MonoBehaviour
    {
        [Header("Smash (by severity)")]
        [SerializeField] private AudioClip[] smashLight;
        [SerializeField] private AudioClip[] smashHeavy;
        [SerializeField] private AudioClip smashBig;
        [Header("Events")]
        [SerializeField] private AudioClip deliver;
        [SerializeField] private AudioClip phaseHaul;
        [SerializeField] private AudioClip phaseWin;
        [SerializeField] private AudioClip phaseFail;
        [Header("Interaction")]
        [SerializeField] private AudioClip grab;
        [Header("UI")]
        [SerializeField] private AudioClip uiClick;
        [Header("Sources")]
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioSource musicSource;

        private void Awake()
        {
            SettingsManager.Load();
            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
                sfxSource.playOnAwake = false;
                sfxSource.spatialBlend = 0f; // 2D — the punchline must always be audible
            }
        }

        private void OnEnable()
        {
            CargoCondition.OnDamageFx += HandleDamage;
            DeliveryZone.OnDelivered  += HandleDeliver;
            RoundManager.OnPhaseChanged += HandlePhase;
            PlayerCarryController.OnGrab += HandleGrab;
            ComboSystem.OnCombo += HandleCombo;
        }

        private void OnDisable()
        {
            CargoCondition.OnDamageFx -= HandleDamage;
            DeliveryZone.OnDelivered  -= HandleDeliver;
            RoundManager.OnPhaseChanged -= HandlePhase;
            PlayerCarryController.OnGrab -= HandleGrab;
            ComboSystem.OnCombo -= HandleCombo;
        }

        // Rising pitch as the chain climbs — the audible hype of "x4 CHAIN!".
        private void HandleCombo(int combo, Vector3 worldPos) =>
            PlayOneShot(deliver, SettingsManager.Sfx, Mathf.Min(1f + 0.08f * (combo - 1), 2f));

        private void HandleGrab(Vector3 worldPos) =>
            PlayOneShot(grab, SettingsManager.Sfx * 0.8f, Random.Range(0.95f, 1.05f));

        private void HandleDamage(Vector3 worldPos, int valueLost, bool bigSmash)
        {
            float sev = Mathf.Clamp01(valueLost / 60f);
            AudioClip clip = bigSmash ? smashBig
                           : valueLost >= 20 ? Pick(smashHeavy)
                           : Pick(smashLight);
            float vol = SettingsManager.Sfx * (bigSmash ? 1f : 0.45f + 0.55f * sev);
            PlayOneShot(clip, vol, Random.Range(0.9f, 1.1f));
        }

        private void HandleDeliver(Vector3 worldPos, int payout) =>
            PlayOneShot(deliver, SettingsManager.Sfx, 1f);

        private void HandlePhase(RoundPhase phase)
        {
            switch (phase)
            {
                case RoundPhase.Hauling: PlayOneShot(phaseHaul, SettingsManager.Sfx * 0.9f, 1f); break;
                case RoundPhase.Payout:  PlayOneShot(phaseWin,  SettingsManager.Sfx, 1f); break;
                case RoundPhase.GameOver: PlayOneShot(phaseFail, SettingsManager.Sfx, 1f); break;
            }
        }

        /// <summary>Hook for UI buttons if desired.</summary>
        public void PlayClick() => PlayOneShot(uiClick, SettingsManager.Sfx, 1f);

        /// <summary>Editor/test helper: fire a representative smash sound.</summary>
        public void TestSmash(int valueLost, bool big) => HandleDamage(Vector3.zero, valueLost, big);

        private static AudioClip Pick(AudioClip[] a) =>
            a == null || a.Length == 0 ? null : a[Random.Range(0, a.Length)];

        private void PlayOneShot(AudioClip clip, float volume, float pitch)
        {
            if (clip == null || sfxSource == null) return;
            sfxSource.pitch = pitch;
            sfxSource.PlayOneShot(clip, Mathf.Clamp01(volume));
        }
    }
}
