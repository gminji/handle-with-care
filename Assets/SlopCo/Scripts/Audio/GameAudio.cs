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
        [Tooltip("Kick impact. Falls back to the grab thud (pitched down) when unassigned.")]
        [SerializeField] private AudioClip kickHit;
        [Tooltip("Kick that hit nothing. Optional — silence is fine for a whiff.")]
        [SerializeField] private AudioClip kickWhiff;
        [Header("UI")]
        [SerializeField] private AudioClip uiClick;
        [Header("Comms (ping/emote wheel)")]
        [SerializeField] private AudioClip ping;
        [SerializeField] private AudioClip emote;
        [Header("Music (loops — assign CC0 tracks in the Bootstrap scene)")]
        [SerializeField] private AudioClip musicCalm;   // menu / briefing / payout / fired
        [SerializeField] private AudioClip musicHaul;   // the run — driving / urgent
        [SerializeField, Range(0.05f, 5f)] private float musicFadeSpeed = 1.2f;
        [Header("Sources")]
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioSource musicSource; // deprecated: kept for scene-ref compat; music now uses the runtime sources below

        // Two looping sources cross-faded by volume (a single source can't blend two clips). Created at
        // runtime so the only scene work is dropping in the two CC0 clips above.
        private AudioSource _calmSrc, _haulSrc;
        private float _calmMix = 1f;                 // 1 = calm fully up, 0 = haul fully up
        private RoundPhase _phase = RoundPhase.Lobby;

        private void Awake()
        {
            SettingsManager.Load();
            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
                sfxSource.playOnAwake = false;
                sfxSource.spatialBlend = 0f; // 2D — the punchline must always be audible
            }
            StartMusic();
        }

        private AudioSource MakeMusicSource(AudioClip clip)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.clip = clip;
            s.loop = true;
            s.playOnAwake = false;
            s.spatialBlend = 0f; // 2D music bed
            s.volume = 0f;       // Update() fades it in to the live Music level
            return s;
        }

        private void StartMusic()
        {
            // Null clips (not yet sourced) simply leave the build silent — no error, no spam.
            if (musicCalm != null) { _calmSrc = MakeMusicSource(musicCalm); _calmSrc.Play(); }
            if (musicHaul != null) { _haulSrc = MakeMusicSource(musicHaul); _haulSrc.Play(); }
        }

        // Cross-fade toward the current phase's track and keep both sources at the live Music volume.
        // unscaledDeltaTime so Juice's timeScale hit-stops don't freeze the fade.
        private void Update()
        {
            float target = MusicBus.CalmWeight(_phase);
            _calmMix = Mathf.MoveTowards(_calmMix, target, musicFadeSpeed * Time.unscaledDeltaTime);
            float music = SettingsManager.Music;
            if (_calmSrc != null) _calmSrc.volume = music * _calmMix;
            if (_haulSrc != null) _haulSrc.volume = music * (1f - _calmMix);
        }

        private void OnEnable()
        {
            CargoCondition.OnDamageFx += HandleDamage;
            DeliveryZone.OnDelivered  += HandleDeliver;
            RoundManager.OnPhaseChanged += HandlePhase;
            PlayerCarryController.OnGrab += HandleGrab;
            ComboSystem.OnCombo += HandleCombo;
            BestRecords.OnNewRecord += HandleNewRecord;
            PingEmoteController.OnPingEmote += HandlePingEmote;
            SlopCo.Player.KickAbility.OnKickFx += HandleKick;
        }

        private void OnDisable()
        {
            CargoCondition.OnDamageFx -= HandleDamage;
            DeliveryZone.OnDelivered  -= HandleDeliver;
            RoundManager.OnPhaseChanged -= HandlePhase;
            PlayerCarryController.OnGrab -= HandleGrab;
            ComboSystem.OnCombo -= HandleCombo;
            BestRecords.OnNewRecord -= HandleNewRecord;
            PingEmoteController.OnPingEmote -= HandlePingEmote;
            SlopCo.Player.KickAbility.OnKickFx -= HandleKick;
        }

        // The boot: a solid thud on contact, a light swish on a miss. With no dedicated clips assigned the
        // impact borrows the grab thud pitched down — audible feedback without new scene wiring.
        private void HandleKick(Vector3 worldPos, bool connected)
        {
            if (connected)
                PlayOneShot(kickHit != null ? kickHit : grab, SettingsManager.Sfx,
                            kickHit != null ? Random.Range(0.95f, 1.05f) : 0.6f);
            else if (kickWhiff != null)
                PlayOneShot(kickWhiff, SettingsManager.Sfx * 0.6f, Random.Range(1.05f, 1.15f));
        }

        // Rising pitch as the chain climbs — the audible hype of "x4 CHAIN!".
        private void HandleCombo(int combo, Vector3 worldPos) =>
            PlayOneShot(deliver, SettingsManager.Sfx, Mathf.Min(1f + 0.08f * (combo - 1), 2f));

        // A bright, pitched-up sparkle on a personal best — reuses the assigned delivery cue (no new
        // serialized clip / scene wiring); distinct from the lower-pitched win jingle on the Payout phase.
        private void HandleNewRecord() =>
            PlayOneShot(deliver, SettingsManager.Sfx, 1.6f);

        private void HandleGrab(Vector3 worldPos) =>
            PlayOneShot(grab, SettingsManager.Sfx * 0.8f, Random.Range(0.95f, 1.05f));

        // Ping/emote wheel cue: a flat blip for a ping, a slightly pitch-varied pop for an emote (null-safe).
        private void HandlePingEmote(bool isPing, Vector3 _) =>
            PlayOneShot(isPing ? ping : emote, SettingsManager.Sfx, isPing ? 1f : Random.Range(0.95f, 1.05f));

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
            _phase = phase; // drives the music cross-fade in Update()
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
