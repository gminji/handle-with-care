using System;
using UnityEngine;

namespace SlopCo.Core
{
    /// <summary>
    /// The permanent (cross-run) upgrade wallet and levels, backed by local PlayerPrefs exactly like
    /// <see cref="BestRecords"/>, <see cref="CrewRank"/> and <see cref="SettingsManager"/>. A run's
    /// earnings are banked once on the terminal FIRED card (<c>SlopCo.UI.ResultsUI</c>), and the title
    /// screen's upgrade menu (<c>SlopCo.UI.UpgradeShopUI</c>) spends that bank.
    ///
    /// Deliberately NOT networked. Every effect these levels drive is applied owner-locally on the
    /// player's own <c>PlayerController</c>, and the bank is a personal savings account — so each player
    /// in a co-op session brings their own purchases, and nobody inherits the host's. The one place that
    /// distinction matters is <see cref="CashGainMult"/>: it scales what YOU take out of the run, not the
    /// crew's shared in-round <c>QuotaSystem.Cash</c>, which would otherwise mean the host's savings
    /// silently retuned everyone else's quota pressure.
    ///
    /// Levels are cached in statics rather than re-read per access because
    /// <see cref="MoveSpeedMult"/> and friends are read every frame from the movement loop.
    /// </summary>
    public static class MetaUpgrades
    {
        private const string KBank = "slop.meta.bank";
        private const string KLevelPrefix = "slop.meta.lv";   // + track index, e.g. slop.meta.lv0

        /// <summary>Raised whenever the bank or any level changes — the shop screen repaints off this.</summary>
        public static event Action OnChanged;

        private static readonly int[] _levels = new int[MetaUpgradeMath.TrackCount];
        private static int _bank;
        private static bool _loaded;

        /// <summary>Cash available to spend in the upgrade menu.</summary>
        public static int Bank { get { Load(); return _bank; } }

        /// <summary>Read PlayerPrefs into the cache once. Idempotent; called lazily by every accessor, so
        /// unlike <see cref="SettingsManager.Load"/> there is no boot-order requirement.</summary>
        public static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _bank = Mathf.Max(0, PlayerPrefs.GetInt(KBank, 0));
            for (int i = 0; i < _levels.Length; i++)
                _levels[i] = MetaUpgradeMath.ClampLevel(PlayerPrefs.GetInt(KLevelPrefix + i, 0));
        }

        /// <summary>Saved level (0..<see cref="MetaUpgradeMath.MaxLevel"/>) of one track.</summary>
        public static int Level(int track)
        {
            Load();
            return MetaUpgradeMath.IsValidTrack(track) ? _levels[track] : 0;
        }

        public static int Level(MetaUpgrade id) => Level((int)id);

        /// <summary>Price of the next level of a track, or 0 when it is already maxed.</summary>
        public static int NextCost(int track) => MetaUpgradeMath.Cost(track, Level(track));

        /// <summary>True when a track has no levels left to buy.</summary>
        public static bool IsMaxed(int track) => MetaUpgradeMath.IsMaxed(Level(track));

        /// <summary>True when the bank covers the next level of a track (false once maxed).</summary>
        public static bool CanAfford(int track) => !IsMaxed(track) && Bank >= NextCost(track);

        /// <summary>
        /// Bank one run's earnings, scaled by the <see cref="MetaUpgrade.CashGain"/> track. Returns the
        /// amount actually credited (0 for a run that earned nothing) so the results card can show it.
        /// </summary>
        public static int Deposit(int earned)
        {
            Load();
            if (earned <= 0) return 0;

            int credited = Mathf.RoundToInt(earned * CashGainMult);
            if (credited <= 0) return 0;

            // Saturate instead of wrapping: PlayerPrefs stores a 32-bit int, and a negative bank would
            // lock the shop permanently with no in-game way back.
            _bank = (int)Math.Min((long)_bank + credited, int.MaxValue);
            PlayerPrefs.SetInt(KBank, _bank);
            PlayerPrefs.Save();
            OnChanged?.Invoke();
            return credited;
        }

        /// <summary>
        /// Spend the bank on one level of a track. Returns false (and changes nothing) when the track is
        /// invalid, already maxed, or unaffordable — the shop's button state mirrors those same three
        /// conditions, so this is the authoritative re-check, not a duplicate of it.
        /// </summary>
        public static bool TryBuy(int track)
        {
            Load();
            if (!MetaUpgradeMath.IsValidTrack(track)) return false;

            int level = _levels[track];
            if (MetaUpgradeMath.IsMaxed(level)) return false;

            int cost = MetaUpgradeMath.Cost(track, level);
            if (_bank < cost) return false;

            _bank -= cost;
            _levels[track] = level + 1;
            PlayerPrefs.SetInt(KBank, _bank);
            PlayerPrefs.SetInt(KLevelPrefix + track, _levels[track]);
            PlayerPrefs.Save();
            OnChanged?.Invoke();
            return true;
        }

        /// <summary>Wipe every level and the bank (a fresh-save reset; also keeps EditMode tests from
        /// leaking saved progress between runs).</summary>
        public static void ResetAll()
        {
            _loaded = true;
            _bank = 0;
            PlayerPrefs.SetInt(KBank, 0);
            for (int i = 0; i < _levels.Length; i++)
            {
                _levels[i] = 0;
                PlayerPrefs.SetInt(KLevelPrefix + i, 0);
            }
            PlayerPrefs.Save();
            OnChanged?.Invoke();
        }

        // ── Aggregate multipliers (1.0 = unbought) ────────────────────────────────────────────────
        /// <summary>Scales the cash banked at the end of a run (never the crew's in-round cash).</summary>
        public static float CashGainMult => Mult(MetaUpgrade.CashGain);

        /// <summary>Permanent walk/haul speed factor, composed alongside <c>AugmentSystem.MoveSpeedMult</c>.</summary>
        public static float MoveSpeedMult => Mult(MetaUpgrade.MoveSpeed);

        /// <summary>DURATION factor for harmful effects — below 1.0 once bought, so they end sooner.</summary>
        public static float DebuffDurationMult => Mult(MetaUpgrade.DebuffShorter);

        /// <summary>DURATION factor for beneficial timed effects — above 1.0 once bought.</summary>
        public static float BuffDurationMult => Mult(MetaUpgrade.BuffLonger);

        /// <summary>Dash-stamina regeneration factor.</summary>
        public static float DashRegenMult => Mult(MetaUpgrade.DashRecovery);

        private static float Mult(MetaUpgrade id) => MetaUpgradeMath.Multiplier((int)id, Level(id));
    }
}
