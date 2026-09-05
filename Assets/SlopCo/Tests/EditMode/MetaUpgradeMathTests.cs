using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SlopCo.Core;
using SlopCo.UI;

namespace SlopCo.Tests.EditMode
{
    /// <summary>
    /// Pure-layer net for the title-screen 강화 shop's price ladder and effect curve
    /// (<see cref="MetaUpgradeMath"/>). Nothing here touches <see cref="MetaUpgrades"/>: that class writes
    /// PlayerPrefs, and an EditMode run must not spend or grant a developer's real saved progress — the
    /// same reason SettingsManager's clamp is tested through the pure
    /// <c>ClampLookSensitivity</c> rather than through <c>SetLookSensitivity</c>.
    /// </summary>
    public class MetaUpgradeMathTests
    {
        private static IEnumerable<int> Tracks() => Enumerable.Range(0, MetaUpgradeMath.TrackCount);

        [Test]
        public void TrackCount_MatchesTheEnum()
        {
            // A track added to MetaUpgrade without a matching cost/step/key entry would otherwise ship as a
            // silently free, zero-effect row rather than a red test.
            Assert.That(MetaUpgradeMath.TrackCount,
                        Is.EqualTo(System.Enum.GetValues(typeof(MetaUpgrade)).Length));
        }

        [Test]
        public void Cost_StrictlyIncreases_WithLevel_OnEveryTrack()
        {
            foreach (int track in Tracks())
            {
                for (int lv = 1; lv < MetaUpgradeMath.MaxLevel; lv++)
                {
                    int prev = MetaUpgradeMath.Cost(track, lv - 1);
                    int here = MetaUpgradeMath.Cost(track, lv);
                    Assert.That(here, Is.GreaterThan(prev),
                        $"track {track}: level {lv + 1} must cost more than level {lv} (레벨이 높아질수록 많은 돈)");
                }
            }
        }

        [Test]
        public void Cost_IsPositive_BelowMax_AndZero_AtMax()
        {
            foreach (int track in Tracks())
            {
                for (int lv = 0; lv < MetaUpgradeMath.MaxLevel; lv++)
                {
                    Assert.That(MetaUpgradeMath.Cost(track, lv), Is.GreaterThan(0), $"track {track} level {lv}");
                    Assert.That(MetaUpgradeMath.IsMaxed(lv), Is.False, $"track {track} level {lv}");
                }
                // 0 at max means "not for sale". A caller that skips IsMaxed and only checks affordability
                // would read that as free — the contract this pins is that IsMaxed is true there too.
                Assert.That(MetaUpgradeMath.Cost(track, MetaUpgradeMath.MaxLevel), Is.Zero);
                Assert.That(MetaUpgradeMath.IsMaxed(MetaUpgradeMath.MaxLevel), Is.True);
            }
        }

        [Test]
        public void Cost_ClampsOutOfRangeLevels_InsteadOfThrowing()
        {
            // Levels come from PlayerPrefs, which a player can hand-edit to anything at all.
            foreach (int track in Tracks())
            {
                Assert.That(MetaUpgradeMath.Cost(track, -5), Is.EqualTo(MetaUpgradeMath.Cost(track, 0)));
                Assert.That(MetaUpgradeMath.Cost(track, 999), Is.Zero);
            }
            Assert.That(MetaUpgradeMath.ClampLevel(-1), Is.Zero);
            Assert.That(MetaUpgradeMath.ClampLevel(99), Is.EqualTo(MetaUpgradeMath.MaxLevel));
        }

        [Test]
        public void TotalCost_EqualsTheSumOfEveryLevelPrice()
        {
            foreach (int track in Tracks())
            {
                int running = 0;
                for (int lv = 0; lv < MetaUpgradeMath.MaxLevel; lv++)
                {
                    Assert.That(MetaUpgradeMath.TotalCost(track, lv), Is.EqualTo(running), $"track {track} level {lv}");
                    running += MetaUpgradeMath.Cost(track, lv);
                }
                Assert.That(MetaUpgradeMath.TotalCost(track, MetaUpgradeMath.MaxLevel), Is.EqualTo(running));
            }
        }

        [Test]
        public void Multiplier_IsExactlyNeutral_AtLevelZero()
        {
            // An unbought save must be bit-identical to no upgrade system at all — this value is multiplied
            // into the movement loop every frame.
            foreach (int track in Tracks())
                Assert.That(MetaUpgradeMath.Multiplier(track, 0), Is.EqualTo(1f), $"track {track}");
        }

        [Test]
        public void Multiplier_MovesTheRightWay_AndNeverCollapses()
        {
            // DebuffShorter scales a DURATION, so it is the one track that must go DOWN — and must stay
            // safely above zero at max, or a timed penalty becomes an instantaneous no-op.
            foreach (int track in Tracks())
            {
                bool shrinks = track == (int)MetaUpgrade.DebuffShorter;
                float prev = MetaUpgradeMath.Multiplier(track, 0);
                for (int lv = 1; lv <= MetaUpgradeMath.MaxLevel; lv++)
                {
                    float here = MetaUpgradeMath.Multiplier(track, lv);
                    if (shrinks) Assert.That(here, Is.LessThan(prev), $"track {track} level {lv}");
                    else Assert.That(here, Is.GreaterThan(prev), $"track {track} level {lv}");
                    Assert.That(here, Is.GreaterThan(0.1f), $"track {track} level {lv} must stay a usable factor");
                    prev = here;
                }
            }
        }

        [Test]
        public void EveryTrack_HasDistinctNameAndDescKeys_WithFourLanguages()
        {
            var keys = new List<string>();
            foreach (int track in Tracks())
            {
                keys.Add(MetaUpgradeMath.NameKey(track));
                keys.Add(MetaUpgradeMath.DescKey(track));
            }

            Assert.That(keys.Distinct().Count(), Is.EqualTo(keys.Count), "every track needs its own name/desc key");
            foreach (string k in keys)
            {
                Assert.That(Localization.Has(k), Is.True, $"missing localization entry '{k}'");
                Assert.That(Localization.VariantCount(k), Is.EqualTo(4), $"'{k}' must have all 4 languages (EN/KO/JA/ZH)");
            }
        }

        [Test]
        public void ShopChromeKeys_Exist_InFourLanguages()
        {
            // The row-building UI resolves these by literal string; a typo would ship the raw key as the
            // on-screen label (Localization.Get falls back to the key itself), not an error.
            foreach (string k in new[] { "menu.upgrades", "meta.title", "meta.bank", "meta.level", "meta.max",
                                         "meta.hint", "res.banked", "opt.back" })
            {
                Assert.That(Localization.Has(k), Is.True, $"missing localization entry '{k}'");
                Assert.That(Localization.VariantCount(k), Is.EqualTo(4), $"'{k}' must have all 4 languages");
            }
        }

        [Test]
        public void InvalidTrack_IsInert()
        {
            foreach (int bad in new[] { -1, MetaUpgradeMath.TrackCount, 1000 })
            {
                Assert.That(MetaUpgradeMath.IsValidTrack(bad), Is.False);
                Assert.That(MetaUpgradeMath.Cost(bad, 0), Is.Zero);
                Assert.That(MetaUpgradeMath.TotalCost(bad, 3), Is.Zero);
                Assert.That(MetaUpgradeMath.Multiplier(bad, 3), Is.EqualTo(1f));
                Assert.That(MetaUpgradeMath.StepPerLevel(bad), Is.Zero);
                Assert.That(MetaUpgradeMath.NameKey(bad), Is.Empty);
                Assert.That(MetaUpgradeMath.DescKey(bad), Is.Empty);
            }
        }

        [Test]
        public void UpgradeYellow_IsItsOwnToken_AndReadableUnderItsInk()
        {
            UITheme.ResetPalette();

            // The menu's stated colour brief is yellow: red and green both high, blue low.
            Assert.That(UITheme.UpgradeYellow.r, Is.GreaterThan(0.8f));
            Assert.That(UITheme.UpgradeYellow.g, Is.GreaterThan(0.6f));
            Assert.That(UITheme.UpgradeYellow.b, Is.LessThan(0.4f));
            Assert.That(UITheme.UpgradeYellow.a, Is.EqualTo(1f));

            // Label ink must be dark against that face — white-on-yellow is the failure this guards.
            float face = UITheme.UpgradeYellow.grayscale, ink = UITheme.UpgradeYellowInk.grayscale;
            Assert.That(face - ink, Is.GreaterThan(0.5f), "yellow button labels must stay legible");
            Assert.That(ink, Is.LessThan(UITheme.TextPrimary.grayscale));
        }
    }
}
