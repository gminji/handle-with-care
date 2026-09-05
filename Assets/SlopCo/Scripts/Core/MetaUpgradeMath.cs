namespace SlopCo.Core
{
    /// <summary>
    /// One permanent, cross-run upgrade track bought on the title screen. The integer value IS the
    /// PlayerPrefs slot index (<see cref="MetaUpgrades"/> stores <c>slop.meta.lv0..4</c>), so entries may
    /// be appended but never reordered or renumbered — that would silently move a player's saved levels
    /// onto a different effect.
    /// </summary>
    public enum MetaUpgrade
    {
        /// <summary>돈 획득 배율 — scales the cash banked at the end of a run.</summary>
        CashGain = 0,
        /// <summary>이동 속도 — permanent walk/haul speed multiplier.</summary>
        MoveSpeed = 1,
        /// <summary>해로운 효과 지속시간 감소 — shortens dash exhaustion and the UFO beam hold.</summary>
        DebuffShorter = 2,
        /// <summary>이로운 효과 지속시간 증가 — lengthens timed item buffs.</summary>
        BuffLonger = 3,
        /// <summary>대시 지구력 회복 — faster dash-stamina regeneration.</summary>
        DashRecovery = 4,
    }

    /// <summary>
    /// Pure cost/effect curve for the permanent upgrade shop (<see cref="MetaUpgrades"/> owns the saved
    /// state, <c>SlopCo.UI.UpgradeShopUI</c> owns the screen). No UnityEngine dependency at all — same
    /// reason <see cref="QuotaMath"/>-style tables live apart from their NetworkBehaviour: the numbers are
    /// unit-testable without an editor, a scene, or PlayerPrefs.
    ///
    /// Every track runs 0..<see cref="MaxLevel"/> and each level costs strictly more than the last
    /// (<see cref="Cost"/> compounds by ×1.6), which is the whole shape of the ask: 레벨이 높아질수록 많은 돈.
    /// </summary>
    public static class MetaUpgradeMath
    {
        /// <summary>Levels are 0 (unbought) through 5 — the ceiling the shop paints as MAX.</summary>
        public const int MaxLevel = 5;

        /// <summary>Number of purchasable tracks; matches the <see cref="MetaUpgrade"/> member count.</summary>
        public const int TrackCount = 5;

        // Integer growth (×8/5 = ×1.6 per level) rather than a float pow: the price ladder must be
        // byte-identical on every machine and every Unity version, and it is asserted level-by-level in
        // MetaUpgradeMathTests. Successive integer division truncates, which only ever makes the next
        // step cheaper by <1 — never enough to break the strictly-increasing contract at these bases.
        private const int GrowthNum = 8, GrowthDen = 5;

        // Price of level 1 per track (index = (int)MetaUpgrade). Tuned against a run's banked earnings
        // (RunStats.TotalDelivered, typically a few hundred to a few thousand): ~1 level per run early,
        // several runs per level at the top.
        private static readonly int[] FirstLevelCost = { 300, 260, 280, 240, 220 };

        // Signed per-level magnitude step. DebuffShorter is NEGATIVE on purpose: its multiplier scales a
        // DURATION, so "better" means smaller. At MaxLevel it lands on 0.60 — never at or below zero,
        // which would turn a timed penalty into a division-by-nothing or an instant no-op.
        private static readonly float[] PerLevelStep = { 0.10f, 0.04f, -0.08f, 0.12f, 0.10f };

        private static readonly string[] NameKeys =
            { "meta.cash.name", "meta.speed.name", "meta.debuff.name", "meta.buff.name", "meta.dash.name" };

        private static readonly string[] DescKeys =
            { "meta.cash.desc", "meta.speed.desc", "meta.debuff.desc", "meta.buff.desc", "meta.dash.desc" };

        public static bool IsValidTrack(int track) => track >= 0 && track < TrackCount;

        /// <summary>Clamp a level read from PlayerPrefs (which a player can hand-edit) into 0..MaxLevel.</summary>
        public static int ClampLevel(int level) => level < 0 ? 0 : (level > MaxLevel ? MaxLevel : level);

        /// <summary>True once the track can be bought no further.</summary>
        public static bool IsMaxed(int level) => ClampLevel(level) >= MaxLevel;

        /// <summary>
        /// Price of the NEXT level for a track currently at <paramref name="currentLevel"/>. Returns 0 for
        /// a maxed or invalid track — that 0 means "not for sale", never "free", so callers must test
        /// <see cref="IsMaxed"/> before treating an affordable-looking 0 as a purchase.
        /// </summary>
        public static int Cost(int track, int currentLevel)
        {
            if (!IsValidTrack(track)) return 0;
            currentLevel = ClampLevel(currentLevel);
            if (currentLevel >= MaxLevel) return 0;

            long c = FirstLevelCost[track];
            for (int i = 0; i < currentLevel; i++) c = c * GrowthNum / GrowthDen;
            return (int)c;
        }

        /// <summary>Total cash sunk into a track to reach <paramref name="level"/> from scratch.</summary>
        public static int TotalCost(int track, int level)
        {
            if (!IsValidTrack(track)) return 0;
            level = ClampLevel(level);
            int sum = 0;
            for (int i = 0; i < level; i++) sum += Cost(track, i);
            return sum;
        }

        /// <summary>Signed magnitude step per level, as a fraction (e.g. 0.10 = +10%/level).</summary>
        public static float StepPerLevel(int track) => IsValidTrack(track) ? PerLevelStep[track] : 0f;

        /// <summary>
        /// The multiplier a track applies at a given level; exactly 1.0 at level 0 for every track, so an
        /// unbought save is indistinguishable from no upgrade system at all.
        /// </summary>
        public static float Multiplier(int track, int level)
            => IsValidTrack(track) ? 1f + PerLevelStep[track] * ClampLevel(level) : 1f;

        public static string NameKey(int track) => IsValidTrack(track) ? NameKeys[track] : string.Empty;
        public static string DescKey(int track) => IsValidTrack(track) ? DescKeys[track] : string.Empty;
    }
}
