using UnityEngine;
using UnityEngine.UI;
using SlopCo.Core;

namespace SlopCo.UI
{
    /// <summary>
    /// The title-screen 강화 (permanent upgrade) menu. Spends the cross-run bank that
    /// <see cref="MetaUpgrades"/> keeps in PlayerPrefs — everything a finished run earned, banked by
    /// <see cref="ResultsUI"/> on the terminal FIRED card — on 0..5 levels of each
    /// <see cref="MetaUpgrade"/> track, every level dearer than the last.
    ///
    /// The whole card is built once at runtime from a bare panel root, the <see cref="PingEmoteWheel"/>
    /// pattern: five near-identical rows would be five hand-authored scene subtrees that drift apart the
    /// moment a track is added, so the scene holds only the panel + this component and the rows come from
    /// <see cref="MetaUpgradeMath.TrackCount"/>. Panel-root visibility stays <see cref="UIManager"/>'s
    /// (LobbyUI.cs:77 pattern) — this component only drives its own content.
    ///
    /// Every button here is yellow (<see cref="UITheme.UpgradeYellow"/>), including the title-screen
    /// entry button that opens it.
    /// </summary>
    public sealed class UpgradeShopUI : MonoBehaviour
    {
        // ── Layout (canvas reference units; CanvasScaler matches WIDTH, so only the height varies) ──
        // Everything stays inside ±385 vertically: a 21:9 screen leaves the canvas only 823 units tall
        // (±411), which is the tightest case any of the authored menus have to survive.
        private const float RowWidth = 1180f, RowHeight = 88f, RowSpacing = 96f, RowsCenterY = 10f;
        private const float BuyWidth = 280f, BuyHeight = 64f;

        private struct Row
        {
            public Text Name, Desc, Level, Cost;
            public Button Buy;
        }

        private Row[] _rows;
        private Text _title, _bank, _hint, _backLabel;
        private bool _built;

        private void Awake() => Build();

        private void OnEnable()
        {
            Build();
            Localization.OnLanguageChanged += Repaint;
            MetaUpgrades.OnChanged += Repaint;
            Repaint();   // the bank grows while this panel is closed (a run was banked) — resync on open
        }

        private void OnDisable()
        {
            Localization.OnLanguageChanged -= Repaint;
            MetaUpgrades.OnChanged -= Repaint;
        }

        // ── Content ──────────────────────────────────────────────────────────────────────────────
        private void Repaint()
        {
            if (!_built) return;

            int bank = MetaUpgrades.Bank;
            if (_title != null) _title.text = Localization.Get("meta.title");
            if (_bank != null) _bank.text = Localization.Get("meta.bank").Replace("{bank}", bank.ToString());
            if (_hint != null) _hint.text = Localization.Get("meta.hint");
            if (_backLabel != null) _backLabel.text = Localization.Get("opt.back");

            for (int i = 0; i < _rows.Length; i++)
            {
                var row = _rows[i];
                int level = MetaUpgrades.Level(i);
                bool maxed = MetaUpgradeMath.IsMaxed(level);
                int cost = MetaUpgradeMath.Cost(i, level);
                bool affordable = !maxed && bank >= cost;

                if (row.Name != null) row.Name.text = Localization.Get(MetaUpgradeMath.NameKey(i));
                if (row.Desc != null) row.Desc.text = Localization.Get(MetaUpgradeMath.DescKey(i)) + "   " + EffectSummary(i, level);
                if (row.Level != null)
                {
                    row.Level.text = Localization.Get("meta.level")
                                         .Replace("{lv}", level.ToString())
                                         .Replace("{max}", MetaUpgradeMath.MaxLevel.ToString())
                                     + "\n" + Pips(level);
                    row.Level.color = maxed ? UITheme.UpgradeYellow : UITheme.TextPrimary;
                }

                // Price label carries a glyph as well as a color — §3.5 of the design system: no state is
                // ever distinguished by color alone. Same three verdicts the augment shop paints.
                if (row.Cost != null)
                {
                    row.Cost.text = maxed
                        ? UITheme.StateGlyph(UIState.Owned) + " " + Localization.Get("meta.max")
                        : (affordable ? UITheme.StateGlyph(UIState.Affordable) : UITheme.StateGlyph(UIState.Unaffordable))
                          + " $" + cost;
                    // Dark ink on the yellow face; a greyed-out button already reads as unavailable, so
                    // the unaffordable case keeps the same ink and lets the button tint carry it.
                    row.Cost.color = UITheme.UpgradeYellowInk;
                }

                if (row.Buy != null) row.Buy.interactable = affordable;
            }
        }

        /// <summary>"+20%" / "-40%" for the CURRENT level — the concrete number behind the flavour text.
        /// Level 0 shows nothing rather than a meaningless "+0%".</summary>
        private static string EffectSummary(int track, int level)
        {
            if (level <= 0) return string.Empty;
            float pct = (MetaUpgradeMath.Multiplier(track, level) - 1f) * 100f;
            string sign = pct >= 0f ? "+" : "-";
            return $"<color={UITheme.GoldHex}>{sign}{Mathf.RoundToInt(Mathf.Abs(pct))}%</color>";
        }

        private static string Pips(int level)
        {
            var sb = new System.Text.StringBuilder(MetaUpgradeMath.MaxLevel);
            for (int i = 0; i < MetaUpgradeMath.MaxLevel; i++) sb.Append(i < level ? '●' : '○');
            return sb.ToString();
        }

        private void Buy(int track)
        {
            // TryBuy re-checks affordability/max itself, so a stale button state can only ever no-op.
            // No ScreenShake here on purpose: the title screen has no PlayerController to sample the
            // trauma, so it would sit in the static accumulator and discharge into the next run's camera.
            MetaUpgrades.TryBuy(track);
            Repaint();   // OnChanged already fires on success; this also covers the rejected case
        }

        private void Close()
        {
            var ui = GetComponentInParent<UIManager>();
            if (ui != null) ui.CloseUpgrades();
        }

        // ── Runtime construction (built once; no scene art to keep in sync) ──────────────────────
        private void Build()
        {
            if (_built) return;
            _built = true;

            var font = (Font)Resources.GetBuiltinResource(typeof(Font), "LegacyRuntime.ttf");

            _title = MakeText(transform, "Title", font, UITheme.FontH2, FontStyle.Bold, TextAnchor.MiddleCenter,
                              new Vector2(0f, 355f), new Vector2(1200f, 60f), UITheme.UpgradeYellow);
            _bank = MakeText(transform, "Bank", font, UITheme.FontBodyLarge, FontStyle.Bold, TextAnchor.MiddleCenter,
                             new Vector2(0f, 300f), new Vector2(1200f, 44f), UITheme.Success);

            _rows = new Row[MetaUpgradeMath.TrackCount];
            float top = RowsCenterY + (MetaUpgradeMath.TrackCount - 1) * RowSpacing * 0.5f;
            for (int i = 0; i < _rows.Length; i++) _rows[i] = MakeRow(i, font, top - i * RowSpacing);

            _hint = MakeText(transform, "Hint", font, UITheme.FontCaption, FontStyle.Normal, TextAnchor.MiddleCenter,
                             new Vector2(0f, -270f), new Vector2(1300f, 36f), UITheme.TextMuted);

            // Reuses the Options screen's BACK label rather than minting a 4-language duplicate of it.
            var back = MakeButton(transform, "Back", font, new Vector2(0f, -340f), new Vector2(300f, BuyHeight),
                                  UITheme.FontBody, out _backLabel);
            back.onClick.AddListener(Close);
        }

        private Row MakeRow(int track, Font font, float y)
        {
            var rowGo = new GameObject("Row" + track, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            rowGo.transform.SetParent(transform, false);
            var rowImg = rowGo.GetComponent<Image>();
            rowImg.color = UITheme.ScrimLight;
            rowImg.raycastTarget = false;
            var rowRt = rowImg.rectTransform;
            rowRt.anchorMin = rowRt.anchorMax = new Vector2(0.5f, 0.5f);
            rowRt.pivot = new Vector2(0.5f, 0.5f);
            rowRt.sizeDelta = new Vector2(RowWidth, RowHeight);
            rowRt.anchoredPosition = new Vector2(0f, y);

            var row = new Row
            {
                Name = MakeText(rowGo.transform, "Name", font, UITheme.FontBodyLarge, FontStyle.Bold, TextAnchor.MiddleLeft,
                                new Vector2(-300f, 20f), new Vector2(560f, 32f), UITheme.TextPrimary),
                Desc = MakeText(rowGo.transform, "Desc", font, UITheme.FontBodySmall, FontStyle.Normal, TextAnchor.MiddleLeft,
                                new Vector2(-300f, -16f), new Vector2(560f, 28f), UITheme.TextMuted),
                Level = MakeText(rowGo.transform, "Level", font, UITheme.FontBodySmall, FontStyle.Bold, TextAnchor.MiddleCenter,
                                 new Vector2(120f, 0f), new Vector2(200f, 60f), UITheme.TextPrimary),
            };

            row.Buy = MakeButton(rowGo.transform, "Buy", font, new Vector2(430f, 0f), new Vector2(BuyWidth, BuyHeight),
                                 UITheme.FontBody, out var costLabel);
            row.Cost = costLabel;
            int captured = track;   // capture per-iteration, exactly like AugmentShopUI's card wiring
            row.Buy.onClick.AddListener(() => Buy(captured));
            return row;
        }

        private static Text MakeText(Transform parent, string name, Font font, int size, FontStyle style,
                                     TextAnchor anchor, Vector2 pos, Vector2 sizeDelta, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);
            var txt = go.GetComponent<Text>();
            txt.font = font;
            txt.fontSize = size;
            txt.fontStyle = style;
            txt.alignment = anchor;
            txt.color = color;
            txt.raycastTarget = false;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            var rt = txt.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = sizeDelta;
            rt.anchoredPosition = pos;
            return txt;
        }

        // Yellow face + dark ink, per the menu's colour brief. Built rather than cloned so the BUY and
        // BACK buttons cannot drift apart.
        private static Button MakeButton(Transform parent, string name, Font font, Vector2 pos, Vector2 size,
                                         int fontSize, out Text label)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = UITheme.UpgradeYellow;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            rt.anchoredPosition = pos;

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;

            label = MakeText(go.transform, "Text", font, fontSize, FontStyle.Bold, TextAnchor.MiddleCenter,
                             Vector2.zero, size, UITheme.UpgradeYellowInk);
            return btn;
        }
    }
}
