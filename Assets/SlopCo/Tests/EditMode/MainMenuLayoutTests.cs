using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using SlopCo.UI;

namespace SlopCo.Tests.EditMode
{
    /// <summary>
    /// Layout regression net for the title screen (<c>MenuCanvas/MainMenuPanel</c>), against the live
    /// Bootstrap.unity — <see cref="ShopLayoutTests"/> pattern, same analytic canvas-space rect math.
    ///
    /// It pins the four defects the menu shipped with before the regroup, each of which is a silent
    /// failure that no exception would ever report:
    /// 1. a second, unaligned button column floating at x=360 with its own widths/heights/spacing,
    /// 2. PLAY WITH AI separated from the other three play modes it belongs with,
    /// 3. an inverted hierarchy — the primary action (PLAY ONLINE) wore the neutral card grey while two
    ///    secondary ones wore <see cref="UITheme.Primary"/>,
    /// 4. elements pushed off a 21:9 canvas (Ver at y=-470, title top at 435, against a ±405 half-height).
    ///
    /// Defect 4 is why the containment/overlap cases run at three aspect ratios. Everything else is
    /// aspect-independent by construction — every element is centre-anchored with a fixed sizeDelta, so
    /// its position relative to its siblings cannot move with the screen — and runs at 1920×1080 only.
    /// </summary>
    public class MainMenuLayoutTests
    {
        private Scene _scene;

        // Reading order, top to bottom. Options..Quit share one row (the system bar).
        private static readonly string[] Stack = { "Play", "Solo", "WithAiBtn", "Tutorial", "UpgradesBtn" };
        private static readonly string[] Bar = { "Options", "Controls", "HelpBtn", "Quit" };
        private static readonly string[] Order =
        {
            "Title", "Sub", "Play", "Solo", "WithAiBtn", "Tutorial", "UpgradesBtn",
            "Divider", "Options", "Controls", "HelpBtn", "Quit", "Ver",
        };

        // Every button label is localized, and to the key its position now claims: the regroup moved
        // PLAY ONLINE to the top of the stack, so a swap with SOLO would look plausible and read wrong.
        private static readonly Dictionary<string, string> LabelKeys = new Dictionary<string, string>
        {
            { "Play", "menu.online" },
            { "Solo", "menu.solo" },
            { "WithAiBtn", "menu.withai" },
            { "Tutorial", "menu.tutorial" },
            { "UpgradesBtn", "menu.upgrades" },
            { "Options", "menu.options" },
            { "Controls", "menu.controls" },
            { "HelpBtn", "menu.help" },
            { "Quit", "menu.quit" },
        };

        // Adjacent rects must clear each other by at least Space2 — the design system's minimum button
        // spacing (§5.1). Float epsilon only: every coordinate involved is exactly representable, but the
        // Title→Sub pair is authored at exactly Space2 and must not fail on a rounding artefact.
        private const float GapEpsilon = 0.01f;

        [OneTimeSetUp]
        public void OpenScene()
        {
            _scene = EditorSceneManager.OpenScene("Assets/SlopCo/Scenes/Bootstrap.unity", OpenSceneMode.Additive);
        }

        [OneTimeTearDown]
        public void CloseScene()
        {
            EditorSceneManager.CloseScene(_scene, true);
        }

        // ── Analytic canvas-space rect helpers (ShopLayoutTests:33-66 — same CanvasScaler math) ────────

        // CanvasScaler.m_MatchWidthOrHeight: 0 (match width) ⇒ the canvas is always exactly 1920 reference
        // units wide; only its height varies with the screen's aspect ratio.
        private static Rect CanvasRect(float screenW, float screenH)
        {
            float h = 1920f * screenH / screenW;
            return new Rect(-960f, -h / 2f, 1920f, h);
        }

        private static Rect ChildRect(RectTransform rt, Rect parent)
        {
            Vector2 aMin = parent.min + Vector2.Scale(rt.anchorMin, parent.size);
            Vector2 aMax = parent.min + Vector2.Scale(rt.anchorMax, parent.size);
            Vector2 size = (aMax - aMin) + rt.sizeDelta;
            Vector2 pivotPt = aMin + Vector2.Scale(aMax - aMin, rt.pivot) + rt.anchoredPosition;
            Vector2 min = pivotPt - Vector2.Scale(size, rt.pivot);
            return new Rect(min, size);
        }

        private static Rect ResolveRect(Transform t, Rect canvasRect)
        {
            var chain = new List<RectTransform>();
            for (var cur = t; cur != null && cur.GetComponent<Canvas>() == null; cur = cur.parent)
                chain.Add(cur.GetComponent<RectTransform>());
            chain.Reverse();
            Rect rect = canvasRect;
            foreach (var rt in chain) rect = ChildRect(rt, rect);
            return rect;
        }

        // GetRootGameObjects()+Find rather than GameObject.Find: MainMenuPanel is active in the scene but
        // its siblings are not, and this keeps one lookup shape for the whole file.
        private GameObject Find(string path)
        {
            foreach (var go in _scene.GetRootGameObjects())
            {
                if (go.name != "MenuCanvas") continue;
                var found = go.transform.Find("MainMenuPanel/" + path);
                return found != null ? found.gameObject : null;
            }
            return null;
        }

        private Rect RectOf(string name, float w = 1920f, float h = 1080f)
        {
            var go = Find(name);
            Assert.NotNull(go, "MainMenuPanel/" + name + " missing from the title screen");
            return ResolveRect(go.transform, CanvasRect(w, h));
        }

        // ── Containment / overlap — the two aspect-sensitive checks ────────────────────────────────────

        [TestCase(1920f, 1080f)]
        [TestCase(2560f, 1080f)]
        [TestCase(1440f, 1080f)]
        public void EveryElement_IsFullyInsideTheCanvas(float w, float h)
        {
            var canvas = CanvasRect(w, h);
            foreach (var name in Order)
            {
                var r = RectOf(name, w, h);
                Assert.That(r.xMin, Is.GreaterThanOrEqualTo(canvas.xMin), name + " left");
                Assert.That(r.xMax, Is.LessThanOrEqualTo(canvas.xMax), name + " right");
                Assert.That(r.yMin, Is.GreaterThanOrEqualTo(canvas.yMin), name + " bottom");
                Assert.That(r.yMax, Is.LessThanOrEqualTo(canvas.yMax), name + " top");
            }
        }

        [TestCase(1920f, 1080f)]
        [TestCase(2560f, 1080f)]
        [TestCase(1440f, 1080f)]
        public void NoTwoElements_Overlap(float w, float h)
        {
            for (int i = 0; i < Order.Length; i++)
                for (int j = i + 1; j < Order.Length; j++)
                    Assert.IsFalse(RectOf(Order[i], w, h).Overlaps(RectOf(Order[j], w, h)),
                                   Order[i] + " overlaps " + Order[j]);
        }

        // ── Structure: one stack, then one row ────────────────────────────────────────────────────────

        [Test]
        public void PlayStack_DescendsInFlowOrder_AndClearsBySpace2()
        {
            // ONLINE → SOLO → WITH AI → TUTORIAL → UPGRADES. This ordering is the whole point of the
            // regroup: WITH AI used to sit in a separate column, breaking the play group in half.
            for (int i = 1; i < Stack.Length; i++)
            {
                var above = RectOf(Stack[i - 1]);
                var below = RectOf(Stack[i]);
                Assert.That(below.yMax, Is.LessThan(above.yMin), Stack[i] + " must sit below " + Stack[i - 1]);
                Assert.That(above.yMin - below.yMax, Is.GreaterThanOrEqualTo(UITheme.Space2 - GapEpsilon),
                            Stack[i - 1] + "→" + Stack[i] + " gap is under Space2");
                Assert.That(below.center.x, Is.EqualTo(above.center.x).Within(0.01f),
                            Stack[i] + " is not on the stack's centre line");
            }
        }

        [Test]
        public void SystemBar_IsOneRow_OrderedLeftToRight_BelowTheStack()
        {
            var lowestStack = RectOf(Stack[Stack.Length - 1]);
            var divider = RectOf("Divider");
            Assert.That(divider.yMax, Is.LessThan(lowestStack.yMin), "Divider must sit below the stack");

            float rowY = RectOf(Bar[0]).center.y;
            for (int i = 0; i < Bar.Length; i++)
            {
                var r = RectOf(Bar[i]);
                Assert.That(r.center.y, Is.EqualTo(rowY).Within(0.01f), Bar[i] + " is off the system row");
                Assert.That(r.yMax, Is.LessThan(divider.yMin), Bar[i] + " must sit below the divider");
                if (i == 0) continue;

                var left = RectOf(Bar[i - 1]);
                Assert.That(r.xMin, Is.GreaterThan(left.xMax), Bar[i] + " must sit right of " + Bar[i - 1]);
                Assert.That(r.xMin - left.xMax, Is.GreaterThanOrEqualTo(UITheme.Space2 - GapEpsilon),
                            Bar[i - 1] + "→" + Bar[i] + " gap is under Space2");
            }
        }

        [Test]
        public void SiblingOrder_MatchesReadingOrder()
        {
            // Draw order and uGUI's automatic navigation both walk sibling order. A hierarchy that
            // disagrees with what the player sees is the defect that let the old right-hand column drift.
            for (int i = 0; i < Order.Length; i++)
            {
                var go = Find(Order[i]);
                Assert.NotNull(go, Order[i] + " missing");
                Assert.That(go.transform.GetSiblingIndex(), Is.EqualTo(i),
                            Order[i] + " is at the wrong place in the hierarchy");
            }
        }

        // ── Buttons: presence, wiring surface, hierarchy of the primary action ────────────────────────

        [Test]
        public void EveryMenuButton_IsPresent_Interactable_AndLocalized()
        {
            foreach (var pair in LabelKeys)
            {
                var go = Find(pair.Key);
                Assert.NotNull(go, pair.Key + " missing from the title screen");

                var button = go.GetComponent<Button>();
                Assert.NotNull(button, pair.Key + " has no Button component");
                Assert.That(button.interactable, Is.True, pair.Key + " is not interactable");
                Assert.That(button.onClick.GetPersistentEventCount(), Is.GreaterThanOrEqualTo(1),
                            pair.Key + " has no persistent onClick target — dead button, no error anywhere");

                var label = go.transform.Find("Text");
                Assert.NotNull(label, pair.Key + " has no Text child");
                var loc = label.GetComponent<LocalizedText>();
                Assert.NotNull(loc, pair.Key + " label is not localized");
                Assert.That(new SerializedObject(loc).FindProperty("key").stringValue, Is.EqualTo(pair.Value),
                            pair.Key + " label points at the wrong localization key");
            }
        }

        [Test]
        public void PrimaryAction_IsTheOnlyPrimaryFace_AndTheLargestButton()
        {
            UITheme.ResetPalette();

            var play = Find("Play");
            Assert.NotNull(play);
            var playImg = play.GetComponent<Image>();
            Assert.NotNull(playImg, "Play has no Image to tint");
            AssertColor(playImg.color, UITheme.Primary, "PLAY ONLINE face");

            var playRect = RectOf("Play");
            foreach (var name in LabelKeys.Keys)
            {
                if (name == "Play") continue;
                var img = Find(name).GetComponent<Image>();
                Assert.NotNull(img, name + " has no Image");
                Assert.IsFalse(IsSameColor(img.color, UITheme.Primary),
                               name + " also wears UITheme.Primary — the primary action stops reading as primary");

                var r = RectOf(name);
                Assert.That(playRect.width * playRect.height, Is.GreaterThan(r.width * r.height),
                            "PLAY ONLINE must be the largest button, but " + name + " is at least as large");
            }
        }

        [Test]
        public void EveryButton_MeetsTheMinimumHitArea()
        {
            // §5.1: minimum 44×44 reference px. The system bar is the tightest case at 240×52.
            foreach (var name in LabelKeys.Keys)
            {
                var r = RectOf(name);
                Assert.That(r.width, Is.GreaterThanOrEqualTo(44f), name + " is narrower than the minimum hit area");
                Assert.That(r.height, Is.GreaterThanOrEqualTo(44f), name + " is shorter than the minimum hit area");
            }
        }

        private static bool IsSameColor(Color a, Color b)
            => Mathf.Abs(a.r - b.r) < 0.01f && Mathf.Abs(a.g - b.g) < 0.01f
            && Mathf.Abs(a.b - b.b) < 0.01f && Mathf.Abs(a.a - b.a) < 0.01f;

        private static void AssertColor(Color actual, Color expected, string what)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.01f), what + " (r)");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.01f), what + " (g)");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.01f), what + " (b)");
            Assert.That(actual.a, Is.EqualTo(expected.a).Within(0.01f), what + " (a)");
        }
    }
}
