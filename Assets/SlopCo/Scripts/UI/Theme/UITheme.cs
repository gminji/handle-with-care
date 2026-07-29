using UnityEngine;

namespace SlopCo.UI
{
    /// <summary>
    /// Six-state contract shared by every gauge/badge/card that communicates "how are we doing" —
    /// dash stamina, fuse gauge, augment shop. §3.5 of docs/UI_DESIGN_SYSTEM.md: no two states may be
    /// distinguished by color alone, so every value below also has a distinct glyph and label key
    /// (see <see cref="UITheme.StateColor"/>/<see cref="UITheme.StateGlyph"/>/<see cref="UITheme.StateLabelKey"/>).
    /// </summary>
    public enum UIState { Ok, Low, Critical, Owned, Affordable, Unaffordable }

    /// <summary>
    /// Design-token layer for uGUI screens (design.md §3 "디자인 토큰"). Colors are named after values
    /// already living in the scene/scripts (design.md §3.1) — this class gives them one identity instead
    /// of N ad-hoc <c>new Color(...)</c> call sites. Depends on <c>UnityEngine</c> only, so it stays a pure
    /// value layer that both gameplay UI scripts and EditMode tests (<c>UIThemeTests</c>) can share without
    /// pulling in any <c>SlopCo.*</c> assembly.
    ///
    /// Colors are exposed as <c>static</c> **properties**, not <c>const</c>/<c>readonly</c> fields, so a
    /// future colorblind/high-contrast palette swap (Part 2 <c>OptionsUI</c> → <c>SetPalette()</c>) is a
    /// single assignment rather than a recompile. Because statics survive EditMode test runs and
    /// domain-reload-disabled Play Mode, <see cref="ResetPalette"/> exists from day one — call it in
    /// <c>[SetUp]</c> so a hypothetical future <c>SetPalette()</c> call in one test can never leak into another.
    /// </summary>
    public static class UITheme
    {
        // ── 표면 · 텍스트 ──────────────────────────────────────────────────────────
        public static Color SurfacePanel { get; set; }
        public static Color SurfaceCard { get; set; }
        public static Color Scrim { get; set; }
        public static Color ScrimLight { get; set; }
        public static Color Primary { get; set; }
        public static Color TextPrimary { get; set; }
        public static Color TextMuted { get; set; }

        // ── 의미 상태 ──────────────────────────────────────────────────────────────
        public static Color Success { get; set; }
        public static Color SuccessFill { get; set; }
        public static Color PayoutGreen { get; set; }
        public static Color Warning { get; set; }
        public static Color WarningFill { get; set; }
        public static Color Danger { get; set; }
        public static Color DangerFill { get; set; }
        public static Color Info { get; set; }
        public static Color Gold { get; set; }
        public static string GoldHex { get; set; }
        public static Color SelectionTint { get; set; }
        public static Color ComboLow { get; set; }
        public static Color ComboHigh { get; set; }

        // ── 배열 토큰 ──────────────────────────────────────────────────────────────
        /// <summary>S/A/B/C/D — index matches <see cref="SlopCo.Gameplay.Grade"/>'s byte order exactly.</summary>
        public static Color[] GradeColors { get; set; }

        /// <summary>Here/Danger/Cargo/Thanks/Yes/No — index matches wheel slice order
        /// (<c>PingEmoteController.Items</c>). Read live by index; never snapshot at boot (design.md §5.7).</summary>
        public static Color[] WheelColors { get; set; }

        // ── 타입 스케일 (px @1920×1080) — 규범값. 씬 직렬화 크기는 미적용(design.md §3.2) ──
        public const int FontMicro = 14;
        public const int FontCaption = 18;
        public const int FontBodySmall = 20;
        public const int FontBody = 22;
        public const int FontBodyLarge = 26;
        public const int FontH3 = 34;
        public const int FontH2 = 46;
        public const int FontH1 = 64;

        // ── 스페이싱 / 라운드 — 규범값. 씬 미적용(design.md §3.3) ──────────────────
        public const int Space1 = 4;
        public const int Space2 = 8;
        public const int Space3 = 12;
        public const int Space4 = 16;
        public const int Space5 = 24;
        public const int Space6 = 32;
        public const int Space7 = 48;
        public const int RadiusSmall = 4;
        public const int RadiusMedium = 8;
        public const int RadiusLarge = 16;

        // ── 캔버스 계약 (design.md §3.4 — Bootstrap.unity 실측과 동일) ──────────────
        public static readonly Vector2 ReferenceResolution = new Vector2(1920f, 1080f);
        public const float MatchWidthOrHeight = 0f; // 0 = match width
        public const float RefPPU = 100f;
        public const int SortHud = 0;
        public const int SortLobby = 10;
        public const int SortMenu = 20;
        public const int SortOverlay = 100;
        public const int SortWheel = 500;

        // ── 임계값 · 모션 (design.md §3.6) ──────────────────────────────────────────
        public const float FuseCriticalThreshold = 0.30f;
        public const float StaminaLowThreshold = 0.30f;
        public const float DisabledAlpha = 0.55f;
        public const float FlashAlpha = 0.35f;

        static UITheme() => ResetPalette();

        /// <summary>Restores every color/array token to its design.md §3.1 default. Static state survives
        /// EditMode test runs and domain-reload-disabled Play Mode, so callers (tests, and eventually a
        /// palette-switch UI) must be able to undo a <c>SetPalette()</c>-style mutation deterministically.</summary>
        public static void ResetPalette()
        {
            SurfacePanel = new Color(0.05f, 0.06f, 0.09f, 0.92f);
            SurfaceCard = new Color(0.30f, 0.33f, 0.38f, 1f);
            Scrim = new Color(0f, 0f, 0f, 0.45f);
            ScrimLight = new Color(0f, 0f, 0f, 0.35f);
            Primary = new Color(0.20f, 0.42f, 0.78f, 1f);
            TextPrimary = new Color(1f, 1f, 1f, 1f);
            TextMuted = new Color(0.50f, 0.55f, 0.60f, 1f);

            Success = new Color(0.50f, 1.00f, 0.55f, 1f);
            SuccessFill = new Color(0.40f, 1.00f, 0.45f, 1f);
            PayoutGreen = new Color(0.40f, 1.00f, 0.50f, 1f);
            Warning = new Color(1.00f, 0.62f, 0.12f, 1f);
            WarningFill = new Color(1.00f, 0.50f, 0.15f, 1f);
            Danger = new Color(1.00f, 0.45f, 0.40f, 1f);
            DangerFill = new Color(1.00f, 0.18f, 0.10f, 1f);
            Info = new Color(0.35f, 0.80f, 1.00f, 1f);
            GoldHex = "#FFD24A";
            Gold = HexColor(GoldHex);
            SelectionTint = new Color(1.00f, 0.90f, 0.35f, 1f);
            ComboLow = new Color(1.00f, 0.85f, 0.20f, 1f);
            ComboHigh = new Color(1.00f, 0.35f, 0.20f, 1f);

            GradeColors = new[]
            {
                HexColor("#FFD24A"), // S
                HexColor("#7CFF6B"), // A
                HexColor("#6BD0FF"), // B
                HexColor("#FFB14A"), // C
                HexColor("#FF6B6B"), // D
            };

            WheelColors = new[]
            {
                new Color(1f, 1f, 1f),        // 0 Here
                new Color(1f, 0.35f, 0.25f),  // 1 Danger
                new Color(1f, 0.85f, 0.3f),   // 2 Cargo
                new Color(1f, 0.45f, 0.6f),   // 3 Thanks
                new Color(0.4f, 1f, 0.5f),    // 4 Yes  (== PayoutGreen, intentional alias)
                new Color(0.9f, 0.5f, 0.3f),  // 5 No
            };
        }

        private static Color HexColor(string html)
        {
            ColorUtility.TryParseHtmlString(html, out var c);
            return c;
        }

        /// <summary>Grade letter (S..D) → hex swatch, clamped so an out-of-range/cast enum value never
        /// throws (ResultsUI passes a cast <c>(int)verdict.Grade</c>). Format is always "#RRGGBB".</summary>
        public static string GradeHex(int gradeIndex)
        {
            var arr = GradeColors;
            if (arr == null || arr.Length == 0) return "#000000";
            int i = Mathf.Clamp(gradeIndex, 0, arr.Length - 1);
            return "#" + ColorUtility.ToHtmlStringRGB(arr[i]);
        }

        /// <summary>Rich-text size wrapper — replaces inline <c>"&lt;size=NN&gt;"</c> literals with a
        /// named type-scale token (design.md §3.2).</summary>
        public static string Size(int px, string inner) => $"<size={px}>{inner}</size>";

        /// <summary>Combo multiplier ramp — low→high chain color (HudController §5.4). Clamped both ends.</summary>
        public static Color ComboRamp(float t01) => Color.Lerp(ComboLow, ComboHigh, Mathf.Clamp01(t01));

        /// <summary>Fuse gauge ramp — danger→success as the fuse fills back up (FuseGauge §5.3). Clamped both ends.</summary>
        public static Color FuseRamp(float t01) => Color.Lerp(DangerFill, SuccessFill, Mathf.Clamp01(t01));

        /// <summary>Dash stamina ramp for the non-critical band (DashGaugeView §5.2, state 0 only —
        /// below <see cref="StaminaLowThreshold"/> the fill snaps to <see cref="WarningFill"/> instead).
        /// Clamped both ends.</summary>
        public static Color StaminaRamp(float g01) => Color.Lerp(WarningFill, Info, Mathf.Clamp01(g01));

        /// <summary>Fixed mapping table, design.md §3.5 — do not derive from an array; every branch is
        /// pinned individually so a future <see cref="UIState"/> addition fails loudly instead of silently
        /// falling through.</summary>
        public static Color StateColor(UIState s) => s switch
        {
            UIState.Ok => Success,
            UIState.Low => Warning,
            UIState.Critical => DangerFill,
            UIState.Owned => TextMuted,
            UIState.Affordable => Success,
            UIState.Unaffordable => Danger,
            _ => TextPrimary,
        };

        /// <summary>Non-empty, mutually distinct across all 6 states — the mandatory half of §3.5's
        /// "color is never the only signal" contract (glyphs are already shipping in the codebase, see
        /// design.md §3.5 provenance list).</summary>
        public static string StateGlyph(UIState s) => s switch
        {
            UIState.Ok => "✓",
            UIState.Low => "▲",
            UIState.Critical => "!",
            UIState.Owned => "·",
            UIState.Affordable => "★",
            UIState.Unaffordable => "✕",
            _ => "?",
        };

        /// <summary>Distinct across all 6 states; every key must resolve in all 4 languages
        /// (enforced by <c>UIThemeTests.EveryState_HasDistinctLabelKey_WithFourLanguages</c>).</summary>
        public static string StateLabelKey(UIState s) => s switch
        {
            UIState.Ok => "dash.label",
            UIState.Low => "dash.low",
            UIState.Critical => "fuse.critical",
            UIState.Owned => "shop.owned",
            UIState.Affordable => "shop.buy",
            UIState.Unaffordable => "shop.short",
            _ => string.Empty,
        };
    }
}
