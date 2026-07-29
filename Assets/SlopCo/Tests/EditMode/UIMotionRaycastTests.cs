using NUnit.Framework;
using UnityEngine;
using SlopCo.UI;

namespace SlopCo.Tests.EditMode
{
    /// <summary>
    /// Regression net for the "invisible CanvasGroup eats clicks" defect
    /// (.am/options-back-button/analyze.md).
    ///
    /// <para>A <see cref="CanvasGroup"/> at <c>alpha = 0</c> is invisible but STILL RECEIVES RAYCASTS —
    /// alpha only controls what you see. <c>MenuCanvas/HintRoot/HintPanel</c> shipped with
    /// <c>alpha = 0, blocksRaycasts = true</c> and a child <c>HintText</c> whose 1100x90 rect spans the
    /// lower centre of the screen, fully covering <c>OptionsPanel/OBack</c> (300x64). Every click on the
    /// Options BACK button was silently swallowed by text nobody could see.</para>
    ///
    /// <para>The fix makes <see cref="UIMotion"/> derive <c>blocksRaycasts</c>/<c>interactable</c> from
    /// alpha, so the defect cannot be reintroduced by any future fade. These tests pin that contract.</para>
    ///
    /// <para>Every case drives the animator with an explicit <c>dt</c>. The <c>Time</c>-reading overloads
    /// must never be exercised here: in EditMode <c>Time.unscaledDeltaTime</c> is the editor's arbitrary
    /// frame delta (measured at 39.58 in one session), which would snap alpha straight to the target and
    /// make the mid-transition case flaky.</para>
    /// </summary>
    public class UIMotionRaycastTests
    {
        private GameObject _go;
        private CanvasGroup _group;

        [SetUp]
        public void CreateGroup()
        {
            // HideAndDontSave: EditMode tests run against whatever scene is active, and ShopLayoutTests
            // opens the committed Bootstrap.unity additively. A plain new GameObject() would dirty an
            // 801KB asset whose diff nobody can review by eye.
            _go = new GameObject("UIMotionRaycastTests_Temp") { hideFlags = HideFlags.HideAndDontSave };
            _group = _go.AddComponent<CanvasGroup>();
        }

        [TearDown]
        public void DestroyGroup()
        {
            if (_go != null) UnityEngine.Object.DestroyImmediate(_go);
            _go = null;
            _group = null;
        }

        /// <summary>Drive the fade to completion at a fixed timestep.</summary>
        private void DriveTo(float target, float duration = UIMotion.DurBanner, float dt = 1f / 60f)
        {
            int guard = Mathf.CeilToInt(duration / dt) + 4;   // bounded: never hang the test runner
            for (int i = 0; i < guard; i++) UIMotion.Fade(_group, target, duration, dt);
        }

        // ── The bug ─────────────────────────────────────────────────────────────────────────────────
        [Test]
        public void Fade_ToZero_ClearsBlocksRaycasts()
        {
            _group.alpha = 1f;
            _group.blocksRaycasts = true;
            _group.interactable = true;

            DriveTo(0f);

            Assert.That(_group.alpha, Is.EqualTo(0f).Within(0.0001f), "fade must actually reach 0");
            Assert.That(_group.blocksRaycasts, Is.False,
                "an invisible CanvasGroup must not eat clicks — this is the Options BACK button bug");
            Assert.That(_group.interactable, Is.False,
                "an invisible CanvasGroup must not keep its Selectables interactable");
        }

        [Test]
        public void Fade_ToOne_RestoresBlocksRaycasts()
        {
            _group.alpha = 0f;
            _group.blocksRaycasts = false;
            _group.interactable = false;

            DriveTo(1f);

            Assert.That(_group.alpha, Is.EqualTo(1f).Within(0.0001f), "fade must actually reach 1");
            Assert.That(_group.blocksRaycasts, Is.True, "a fully visible CanvasGroup must accept clicks again");
            Assert.That(_group.interactable, Is.True, "a fully visible CanvasGroup must be interactable again");
        }

        [Test]
        public void Fade_MidTransition_StillBlocks()
        {
            _group.alpha = 1f;

            // One step of a 0.5s fade at 60fps ≈ 0.967 — visible, so it must still block.
            UIMotion.Fade(_group, 0f, UIMotion.DurBanner, 1f / 60f);

            Assert.That(_group.alpha, Is.GreaterThan(0f), "precondition: one step must not reach 0");
            Assert.That(_group.blocksRaycasts, Is.True,
                "a partially visible banner must still block — a half-faded panel letting clicks through is the same bug inverted");
        }

        [Test]
        public void Fade_NullGroup_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => UIMotion.Fade((CanvasGroup)null, 0f, UIMotion.DurBanner, 1f / 60f),
                "null-safety of the CanvasGroup overload is relied on by both callers' optional refs");
        }

        // ── SetAlpha: the same contract on the non-animated path ────────────────────────────────────
        [Test]
        public void SetAlpha_Zero_ClearsBlocksRaycasts()
        {
            _group.alpha = 1f;
            _group.blocksRaycasts = true;

            UIMotion.SetAlpha(_group, 0f);

            Assert.That(_group.blocksRaycasts, Is.False,
                "OnEnable's direct alpha assignment must derive blocking too, or the first frame still eats clicks");
        }

        [Test]
        public void SetAlpha_NullGroup_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => UIMotion.SetAlpha(null, 0f));
        }
    }
}
