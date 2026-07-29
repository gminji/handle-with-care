using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using SlopCo.Core;
using SlopCo.Gameplay;
using SlopCo.UI;

namespace SlopCo.Tests.EditMode
{
    /// <summary>
    /// Token-contract gate for the UI design system (design.md §6.1, .am/game-ui-design-system/design.md).
    /// Exists to prove the token layer is correct BEFORE ~60 consumer call sites (FuseGauge, DashGauge,
    /// HudController, ResultsUI, AugmentShopUI, Juice, PingEmoteController, PingEmoteWheel) start depending
    /// on it (design.md §7 Step 5: "60개 호출부가 토큰에 의존하기 전에 토큰을 증명"). Two specific historical
    /// bugs this file pins shut: <c>ResultsUI.cs:178</c>'s 4/255 gold color drift (test #1), and the
    /// color-only status signaling in <c>DashGauge.cs:29-30</c> / <c>AugmentShopUI.cs:194-198</c> that the
    /// §3.5 glyph+label contract exists to close (tests #2/#3/#4b).
    ///
    /// [DISCREPANCY 1] Tests #8/#9 ("DashGaugeView"/"ShopCardView.Marker") reference consumer types that
    /// design.md assigns to <c>DashGauge.cs</c>/<c>AugmentShopUI.cs</c> (§5.2/§5.1). Those types now exist and
    /// these tests call them directly; the temporary local mirrors used before the consumer pass are gone.
    ///
    /// [DISCREPANCY 2] Test #5/#6 need to enumerate every localization key; design.md §4's API list (Has/
    /// VariantCount/Variant) has no key-enumeration entry, so a minimal <c>Localization.Keys</c> property was
    /// added to make the gate possible at all (not itself part of the pinned 3-method list, but required by it).
    ///
    /// [RESOLVED — was DISCREPANCY 3] design.md §3.5 originally pinned Warning↔Danger at 0.166, a figure
    /// produced by applying the Viénot–Brettel–Mollon matrix straight to gamma-space RGB. That is not the
    /// published algorithm, which operates on linear RGB. Done correctly the distance is 0.064 — the pair
    /// genuinely collides for a deuteranope. Rather than keep the flattering arithmetic, this file uses the
    /// correct transform and test #4 asserts the invariant the design actually commits to: an undeclared
    /// sub-threshold pair FAILS, and a declared one must carry a distinct glyph AND a distinct label key.
    /// See DeclaredStateCollisions. design.md §3.5 and docs/UI_DESIGN_SYSTEM.md §3.5 corrected to match.
    /// </summary>
    public class UIThemeTests
    {
        [SetUp]
        public void ResetTheme() => UITheme.ResetPalette();

        // ── #1 ──────────────────────────────────────────────────────────────────────────────────────
        [Test]
        public void Gold_HexAndColor_AreTheSameValue()
        {
            ColorUtility.TryParseHtmlString(UITheme.GoldHex, out var fromHex);
            const float tol = 1f / 255f;
            Assert.That(UITheme.Gold.r, Is.EqualTo(fromHex.r).Within(tol), "Gold.r vs GoldHex — catches ResultsUI:178's historical 4/255 drift");
            Assert.That(UITheme.Gold.g, Is.EqualTo(fromHex.g).Within(tol), "Gold.g vs GoldHex");
            Assert.That(UITheme.Gold.b, Is.EqualTo(fromHex.b).Within(tol), "Gold.b vs GoldHex");
        }

        // ── #2 ──────────────────────────────────────────────────────────────────────────────────────
        [Test]
        public void EveryState_HasNonEmptyDistinctGlyph()
        {
            var states = AllStates();
            var glyphs = states.Select(UITheme.StateGlyph).ToList();
            foreach (var g in glyphs) Assert.That(g, Is.Not.Null.And.Not.Empty, "every UIState needs a glyph — design.md §3.5's unconditional 3x-encoding contract");
            Assert.That(glyphs.Distinct().Count(), Is.EqualTo(glyphs.Count), "all 6 state glyphs must be mutually distinct");
        }

        // ── #3 ──────────────────────────────────────────────────────────────────────────────────────
        [Test]
        public void EveryState_HasDistinctLabelKey_WithFourLanguages()
        {
            var states = AllStates();
            var keys = states.Select(UITheme.StateLabelKey).ToList();
            Assert.That(keys.Distinct().Count(), Is.EqualTo(keys.Count), "all 6 state label keys must be mutually distinct");
            foreach (var k in keys)
                Assert.That(Localization.VariantCount(k), Is.EqualTo(4), $"label key '{k}' must have all 4 languages (EN/KO/JA/ZH)");
        }

        // ── #4 ──────────────────────────────────────────────────────────────────────────────────────
        [Test]
        public void StateColors_AreSeparableUnderDeuteranopia()
        {
            var states = AllStates();
            int nonAliasedPairs = 0;
            for (int i = 0; i < states.Count; i++)
            {
                for (int j = i + 1; j < states.Count; j++)
                {
                    var ca = UITheme.StateColor(states[i]);
                    var cb = UITheme.StateColor(states[j]);
                    if (ColorsEqual(ca, cb)) continue; // aliased pair (Ok/Affordable both Success) — excluded per design.md §3.5

                    nonAliasedPairs++;
                    float d = DeuteranopiaDistance(ca, cb);
                    if (d >= SeparabilityThreshold) continue;

                    // Below threshold: permitted ONLY if declared, and only because the glyph and label
                    // carry the distinction instead. This is the ISO-7010 contract from design.md §3.5 —
                    // colour separation is the secondary goal, glyph+label is the binding one.
                    Assert.That(IsDeclaredCollision(states[i], states[j]), Is.True,
                        $"{states[i]}↔{states[j]} collides under deuteranopia (distance {d:F3} < {SeparabilityThreshold}) " +
                        "and is NOT in DeclaredStateCollisions. Either separate the colours or declare the pair " +
                        "and justify it in design.md §3.5 — do not lower the threshold.");

                    Assert.That(UITheme.StateGlyph(states[i]), Is.Not.EqualTo(UITheme.StateGlyph(states[j])),
                        $"{states[i]}↔{states[j]} is a declared colour collision, so its glyphs MUST differ");
                    Assert.That(UITheme.StateLabelKey(states[i]), Is.Not.EqualTo(UITheme.StateLabelKey(states[j])),
                        $"{states[i]}↔{states[j]} is a declared colour collision, so its label keys MUST differ");
                }
            }
            // 6 states → C(6,2) = 15 pairs, minus the single aliased pair (Ok and Affordable are both Success) = 14.
            // design.md §3.5's table lists 10 rows because it enumerates DISTINCT COLOURS (5 of them → C(5,2) = 10);
            // Success is reached by two states, so each of its colour pairs is realised by two state pairs.
            // Both counts describe the same thing — this assertion guards the state-pair view.
            Assert.That(nonAliasedPairs, Is.EqualTo(14), "expected exactly 14 non-aliased state pairs among the 6 UIState values (design.md §3.5)");
        }

        // ── #4b ─────────────────────────────────────────────────────────────────────────────────────
        [Test]
        public void StateGlyphsAndLabels_AreMutuallyDistinct()
        {
            var states = AllStates();
            var glyphs = states.Select(UITheme.StateGlyph).Distinct().Count();
            var labels = states.Select(UITheme.StateLabelKey).Distinct().Count();
            // Unconditional contract (design.md §3.5): true regardless of whether any two states share a color.
            Assert.That(glyphs, Is.EqualTo(6), "all 6 glyphs must be mutually distinct, independent of color separability");
            Assert.That(labels, Is.EqualTo(6), "all 6 label keys must be mutually distinct, independent of color separability");
        }

        // ── #5 ──────────────────────────────────────────────────────────────────────────────────────
        [Test]
        public void AllLocalizationKeys_HaveFourNonEmptyVariants()
        {
            var bad = new List<string>();
            foreach (var key in Localization.Keys)
            {
                int count = Localization.VariantCount(key);
                if (count != 4) { bad.Add($"{key} (count={count})"); continue; }
                for (int i = 0; i < 4; i++)
                    if (string.IsNullOrEmpty(Localization.Variant(key, i))) { bad.Add($"{key}[{i}] empty"); break; }
            }
            // design.md §8 risk 3: this is expected to catch pre-existing gaps, not just the 5 new keys —
            // testing only the new keys would be circular (grading your own homework).
            Assert.That(bad, Is.Empty, "keys with missing/incomplete language variants: " + string.Join(", ", bad));
        }

        // ── #6 ──────────────────────────────────────────────────────────────────────────────────────
        [Test]
        public void AllLocalizationKeys_KeepPlaceholderParity()
        {
            var bad = new List<string>();
            foreach (var key in Localization.Keys)
            {
                var enTokens = Tokens(Localization.Variant(key, 0));
                if (enTokens.Count == 0) continue;
                int count = Localization.VariantCount(key);
                for (int i = 1; i < count; i++)
                {
                    var otherTokens = Tokens(Localization.Variant(key, i));
                    var missing = enTokens.Except(otherTokens).ToList();
                    if (missing.Count > 0) bad.Add($"{key}[{i}] missing {{{string.Join(",", missing)}}}");
                }
            }
            // Get():51-60 silently falls back to English on a miss, so a dropped {token} is invisible at
            // runtime — this is the only net that catches it.
            Assert.That(bad, Is.Empty, "placeholder parity violations (an EN {token} missing from a translation): " + string.Join("; ", bad));
        }

        // ── #7 ──────────────────────────────────────────────────────────────────────────────────────
        [Test]
        public void GradeColors_HaveOneEntryPerGrade()
        {
            int gradeCount = System.Enum.GetValues(typeof(Grade)).Length;
            Assert.That(UITheme.GradeColors.Length, Is.EqualTo(gradeCount),
                "GradeColors must grow with the Grade enum — a missing entry means a magenta badge shipped, not a red test");
        }

        // ── #8 (see [DISCREPANCY 1]) ────────────────────────────────────────────────────────────────
        [Test]
        public void DashGaugeView_State_BoundaryIsExclusiveBelow()
        {
            Assert.That(SlopCo.UI.DashGaugeView.State(0.30f, false), Is.EqualTo(0), "exactly at the threshold must read Ok (>= not <)");
            Assert.That(SlopCo.UI.DashGaugeView.State(0.299f, false), Is.EqualTo(1), "just under the threshold must read Low");
            Assert.That(SlopCo.UI.DashGaugeView.State(0.9f, true), Is.EqualTo(2), "exhausted always wins regardless of gauge value");
        }

        // ── #9 (see [DISCREPANCY 1]) ────────────────────────────────────────────────────────────────
        [Test]
        public void ShopCardView_Marker_IsDistinctAndNonEmpty()
        {
            string affordable = SlopCo.UI.ShopCardView.Marker(0), owned = SlopCo.UI.ShopCardView.Marker(1), unaffordable = SlopCo.UI.ShopCardView.Marker(2);
            Assert.That(affordable, Is.Not.Empty, "affordable marker must not be empty");
            Assert.That(owned, Is.Not.Empty, "owned marker must not be empty");
            Assert.That(unaffordable, Is.Not.Empty, "insufficient-funds marker must not be empty");
            Assert.That(new[] { affordable, owned, unaffordable }.Distinct().Count(), Is.EqualTo(3),
                "the 3 shop card states must map to 3 distinct glyphs (design.md §5.1)");
        }

        // ── #10 ─────────────────────────────────────────────────────────────────────────────────────
        [TestCase(UIMotion.DurBase)]
        [TestCase(UIMotion.DurBanner)]
        public void Fade_ReachesTargetExactlyWithinDuration(float duration)
        {
            float atDuration = UIMotion.Fade(0f, 1f, duration, duration);
            Assert.That(atDuration, Is.EqualTo(1f).Within(1e-5f), "a dt equal to the full duration must land exactly on target");

            float beforeDuration = UIMotion.Fade(0f, 1f, duration, duration * 0.9f);
            Assert.That(beforeDuration, Is.LessThan(1f), "one step short of the duration must not have reached target yet");
        }

        // ── §6.2 documentation-level contrast checks (generous — see design.md §6.2) ────────────────
        [Test]
        public void TextMuted_ContrastAgainstWorstCasePanel_MeetsWcagAA()
        {
            var bg = CompositeOverWhite(UITheme.SurfacePanel);
            float ratio = ContrastRatio(UITheme.TextMuted, bg);
            Assert.That(ratio, Is.GreaterThanOrEqualTo(4.5f),
                $"TextMuted contrast {ratio:F2}:1 must clear WCAG AA (4.5:1) against the worst-case (white-composited) panel — design.md §6.2 pins this at ~4.62, a deliberately narrow 0.12 margin (brighter would reopen the §3.5 deuteranopia conflict)");
        }

        [Test]
        public void DangerFill_ContrastAgainstWorstCasePanel_IsDocumentedNearButBelowAA()
        {
            var bg = CompositeOverWhite(UITheme.SurfacePanel);
            float ratio = ContrastRatio(UITheme.DangerFill, bg);
            // design.md §6.2: ~4.26, a known/accepted AA shortfall — DangerFill's only uses are a non-text
            // bar fill (3:1 threshold applies, not 4.5:1) and a popup with no panel behind it (no real
            // composite occurs). Asserted generously; this is documentation, not a strict gate.
            Assert.That(ratio, Is.InRange(3.5f, 4.5f),
                $"DangerFill contrast {ratio:F2}:1 should stay near the previously-measured ~4.26 — a large move means the token or SurfacePanel alpha changed and §6.2 needs re-deriving");
        }

        // ── helpers ─────────────────────────────────────────────────────────────────────────────────

        private static List<UIState> AllStates() => new List<UIState>
        {
            UIState.Ok, UIState.Low, UIState.Critical, UIState.Owned, UIState.Affordable, UIState.Unaffordable,
        };

        private static bool ColorsEqual(Color a, Color b) =>
            Mathf.Approximately(a.r, b.r) && Mathf.Approximately(a.g, b.g) && Mathf.Approximately(a.b, b.b);



        private static readonly Regex TokenPattern = new Regex(@"\{[^{}]+\}", RegexOptions.Compiled);

        private static HashSet<string> Tokens(string s) =>
            new HashSet<string>(TokenPattern.Matches(s ?? string.Empty).Cast<Match>().Select(m => m.Value));

        private const float SeparabilityThreshold = 0.15f;

        /// <summary>State-colour pairs that are KNOWN to collide for a deuteranope and are permitted anyway,
        /// because the glyph and label carry the distinction (design.md §3.5).
        ///
        /// Warning(1.00,0.62,0.12) ↔ Danger(1.00,0.45,0.40) = 0.064. This is not a palette mistake — it is a
        /// property of the eye. Deuteranopia collapses the red-green axis, and any amber that still reads as
        /// amber to trichromats sits on top of any red that still reads as red. The pre-revision Warning
        /// (1.00,0.70,0.30) scored 0.086 against the same Danger, i.e. worse in absolute terms and equally
        /// unseparated; sweeping the amber range finds no candidate that clears 0.15 against Danger without
        /// colliding with Gold instead. The two never appear as competing states of one widget in any case:
        /// Low renders WarningFill on the dash bar, Critical renders DangerFill, and Danger appears only on
        /// the shop's Unaffordable label.
        ///
        /// Adding an entry here is a design decision, not a way to make a red test green.</summary>
        private static readonly (UIState, UIState)[] DeclaredStateCollisions =
        {
            (UIState.Low, UIState.Unaffordable),   // Warning ↔ Danger, 0.064
        };

        private static bool IsDeclaredCollision(UIState a, UIState b)
        {
            foreach (var (x, y) in DeclaredStateCollisions)
                if ((x == a && y == b) || (x == b && y == a)) return true;
            return false;
        }

        private static float SrgbToLinear(float c) =>
            c <= 0.04045f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);

        private static float LinearToSrgb(float c) =>
            c <= 0.0031308f ? c * 12.92f : 1.055f * Mathf.Pow(c, 1f / 2.4f) - 0.055f;

        /// <summary>Viénot–Brettel–Mollon (1999) deuteranope simulation. The published algorithm operates on
        /// LINEAR RGB, so we round-trip through the sRGB EOTF. Applying the matrix straight to gamma values
        /// (as an earlier revision of this file did) inflates every distance and would let Warning↔Danger
        /// read as 0.166 "separated" when it is really 0.064 — see the declared-collision list in
        /// DeclaredStateCollisions below.</summary>
        private static Color SimulateDeuteranopia(Color c)
        {
            float r = SrgbToLinear(c.r), g = SrgbToLinear(c.g), b = SrgbToLinear(c.b);
            float rp = 0.625f * r + 0.375f * g;
            float gp = 0.700f * r + 0.300f * g;
            float bp = 0.300f * g + 0.700f * b;
            return new Color(LinearToSrgb(rp), LinearToSrgb(gp), LinearToSrgb(bp));
        }

        private static float DeuteranopiaDistance(Color a, Color b)
        {
            var da = SimulateDeuteranopia(a);
            var db = SimulateDeuteranopia(b);
            float dr = da.r - db.r, dg = da.g - db.g, dbl = da.b - db.b;
            return Mathf.Sqrt(dr * dr + dg * dg + dbl * dbl);
        }

        private static float Linearize(float c) => c <= 0.04045f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);

        private static float RelativeLuminance(Color c)
        {
            float r = Linearize(c.r), g = Linearize(c.g), b = Linearize(c.b);
            return 0.2126f * r + 0.7152f * g + 0.0722f * b;
        }

        private static float ContrastRatio(Color a, Color b)
        {
            float la = RelativeLuminance(a) + 0.05f;
            float lb = RelativeLuminance(b) + 0.05f;
            return la > lb ? la / lb : lb / la;
        }

        private static Color CompositeOverWhite(Color fg) => new Color(
            fg.r * fg.a + 1f * (1f - fg.a),
            fg.g * fg.a + 1f * (1f - fg.a),
            fg.b * fg.a + 1f * (1f - fg.a));
    }
}
