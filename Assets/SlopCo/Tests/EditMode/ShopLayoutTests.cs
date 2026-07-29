using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using SlopCo.UI;

namespace SlopCo.Tests.EditMode
{
    /// <summary>
    /// Regression net for the Payout screen overlap fix (augment-select-overlap, design.md §6 Step 7).
    /// Two independent halves: (A) analytic canvas-space rect overlap/containment checks against the live
    /// Bootstrap.unity scene at three aspect ratios, and (B) pure-logic asserts on
    /// <see cref="ShopCardView.State"/> (README.md:111 pattern — see also LoadoutViewTests).
    /// </summary>
    public class ShopLayoutTests
    {
        private Scene _scene;

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

        // ── Analytic canvas-space rect helpers (design.md §6 Step 7 formula) ──────────────────────────

        // CanvasScaler.m_MatchWidthOrHeight: 0 (match width) ⇒ the canvas is always exactly 1920 reference
        // units wide; only its height varies with the screen's aspect ratio. There is no live canvas in
        // EditMode, so this reproduces the scaler's math analytically instead.
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

        // Walks up from `t` to (but excluding) its Canvas root, collecting the RectTransform chain, then
        // applies ChildRect top-down. Equivalent to the parent→child recursion in design.md §6 Step 7,
        // generalized to arbitrary depth so callers can pass any nested path.
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

        // Looked up via GetRootGameObjects()+Find (not GameObject.Find) because ResultsPanel/ShopPanel
        // (and their descendants) are inactive by default in the scene, which GameObject.Find can't see.
        private GameObject Find(string rootName, string path)
        {
            foreach (var go in _scene.GetRootGameObjects())
            {
                if (go.name != rootName) continue;
                if (string.IsNullOrEmpty(path)) return go;
                var found = go.transform.Find(path);
                return found != null ? found.gameObject : null;
            }
            return null;
        }

        private Rect RectOf(string rootName, string path, float w, float h)
        {
            var go = Find(rootName, path);
            Assert.NotNull(go, $"{rootName}/{path} missing — scene step not applied");
            return ResolveRect(go.transform, CanvasRect(w, h));
        }

        // Rect.Contains(Rect) does not exist — this is the 4-edge containment helper design.md calls for.
        private static void AssertContains(Rect outer, Rect inner, string what)
        {
            Assert.That(inner.xMin, Is.GreaterThanOrEqualTo(outer.xMin), what + " left");
            Assert.That(inner.xMax, Is.LessThanOrEqualTo(outer.xMax), what + " right");
            Assert.That(inner.yMin, Is.GreaterThanOrEqualTo(outer.yMin), what + " bottom");
            Assert.That(inner.yMax, Is.LessThanOrEqualTo(outer.yMax), what + " top");
        }

        // ── A. Layout regression (scene-based) — three aspect ratios: 16:9, 21:9 (ultrawide), 4:3 ──────

        [TestCase(1920f, 1080f)]
        [TestCase(2560f, 1080f)]
        [TestCase(1440f, 1080f)]
        public void ShopRail_DoesNotOverlap_ResultsCard_Or_HudRound(float w, float h)
        {
            var card = RectOf("MenuCanvas", "ResultsRoot/ResultsPanel/Card", w, h);
            var rail = RectOf("MenuCanvas", "AugmentShop/ShopPanel/ShopRail", w, h);
            Assert.IsFalse(card.Overlaps(rail), "Results Card overlaps ShopRail");

            // The key automated check for the 21:9 HUD-occlusion risk called out in design.md §5.2 —
            // the "DAY N" label is the only HudCanvas element whose x-range reaches the rail.
            var round = RectOf("HudCanvas", "Round", w, h);
            Assert.IsFalse(round.Overlaps(rail), "HudCanvas/Round overlaps ShopRail");
        }

        [TestCase(1920f, 1080f)]
        [TestCase(2560f, 1080f)]
        [TestCase(1440f, 1080f)]
        public void ShopRail_DoesNotOverlap_ModifierBanner_Or_TutorialPanel(float w, float h)
        {
            var rail = RectOf("MenuCanvas", "AugmentShop/ShopPanel/ShopRail", w, h);
            var banner = RectOf("MenuCanvas", "ModifierBannerRoot", w, h);
            Assert.IsFalse(rail.Overlaps(banner), "ShopRail overlaps ModifierBannerRoot");

            var tutorial = RectOf("MenuCanvas", "TutorialRoot/TutorialPanel", w, h);
            Assert.IsFalse(rail.Overlaps(tutorial), "ShopRail overlaps TutorialPanel");

            // HintPanel (MenuCanvas/HintRoot/HintPanel) is deliberately NOT checked here: at 21:9 it
            // geometrically overlaps ShopRail by ~30×70 (design.md §5.2), but TutorialHint.cs:25-30 drives
            // its CanvasGroup to alpha 0 in every phase other than Briefing/Hauling — including Payout,
            // the only phase ShopRail is ever visible in — and the scene's serialized default is also
            // m_Alpha: 0. So the two rects are never simultaneously visible in practice. If TutorialHint's
            // phase branching ever changes to show the hint during Payout, this exclusion must be revisited.
            //
            // CAUTION — visibility is NOT the same as raycast blocking, and reasoning only about what is
            // visible is exactly what let the Options BACK button bug ship: HintPanel sat at alpha 0 with
            // blocksRaycasts still true, so its (invisible) HintText ate every click in its 1100×90 rect.
            // UIMotion now derives blocksRaycasts/interactable from alpha, which is what makes the
            // "never simultaneously visible" argument above sufficient. Do not weaken that derivation
            // without revisiting this exclusion. See UIMotionRaycastTests.
        }

        [TestCase(1920f, 1080f)]
        [TestCase(2560f, 1080f)]
        [TestCase(1440f, 1080f)]
        public void ShopRail_Children_AreMutuallyDisjoint_AndContainedInRail(float w, float h)
        {
            var rail = RectOf("MenuCanvas", "AugmentShop/ShopPanel/ShopRail", w, h);
            var title = RectOf("MenuCanvas", "AugmentShop/ShopPanel/ShopRail/Title", w, h);
            var card0 = RectOf("MenuCanvas", "AugmentShop/ShopPanel/ShopRail/Card0", w, h);
            var card1 = RectOf("MenuCanvas", "AugmentShop/ShopPanel/ShopRail/Card1", w, h);
            var card2 = RectOf("MenuCanvas", "AugmentShop/ShopPanel/ShopRail/Card2", w, h);

            Assert.IsFalse(title.Overlaps(card0), "Title overlaps Card0");
            Assert.IsFalse(title.Overlaps(card1), "Title overlaps Card1");
            Assert.IsFalse(title.Overlaps(card2), "Title overlaps Card2");
            Assert.IsFalse(card0.Overlaps(card1), "Card0 overlaps Card1");
            Assert.IsFalse(card0.Overlaps(card2), "Card0 overlaps Card2");
            Assert.IsFalse(card1.Overlaps(card2), "Card1 overlaps Card2");

            AssertContains(rail, title, "ShopRail/Title");
            AssertContains(rail, card0, "ShopRail/Card0");
            AssertContains(rail, card1, "ShopRail/Card1");
            AssertContains(rail, card2, "ShopRail/Card2");
        }

        [TestCase("Card0")]
        [TestCase("Card1")]
        [TestCase("Card2")]
        public void CardInternals_AreMutuallyDisjoint_AndContainedInCard(string card)
        {
            // Card-internal offsets are the same at every aspect ratio (the rail itself is anchored to a
            // fixed screen edge and its children never re-anchor relative to height), so one resolution
            // is enough to catch the Step 6-6 coordinate-omission failure mode (design.md §6 Step 6 note).
            const float w = 1920f, h = 1080f;
            var cardPath = "AugmentShop/ShopPanel/ShopRail/" + card;
            var cardRect = RectOf("MenuCanvas", cardPath, w, h);
            var name = RectOf("MenuCanvas", cardPath + "/Name", w, h);
            var desc = RectOf("MenuCanvas", cardPath + "/Desc", w, h);
            var cost = RectOf("MenuCanvas", cardPath + "/Cost", w, h);

            Assert.IsFalse(name.Overlaps(desc), card + ": Name overlaps Desc");
            Assert.IsFalse(desc.Overlaps(cost), card + ": Desc overlaps Cost");
            Assert.IsFalse(name.Overlaps(cost), card + ": Name overlaps Cost");

            AssertContains(cardRect, name, card + "/Name");
            AssertContains(cardRect, desc, card + "/Desc");
            AssertContains(cardRect, cost, card + "/Cost");
        }

        // ── B. Pure-logic regression — ShopCardView.State ─────────────────────────────────────────────

        [Test]
        public void ShopCardView_State_OwnedTakesPriority()
        {
            Assert.That(ShopCardView.State(owned: true, cash: 999, cost: 120), Is.EqualTo(1));
        }

        [Test]
        public void ShopCardView_State_ExactlyAffordable_IsBoundary()
        {
            Assert.That(ShopCardView.State(owned: false, cash: 150, cost: 150), Is.EqualTo(0));
        }

        [Test]
        public void ShopCardView_State_OneShort_IsInsufficient()
        {
            Assert.That(ShopCardView.State(owned: false, cash: 149, cost: 150), Is.EqualTo(2));
        }
    }
}
