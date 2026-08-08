using NUnit.Framework;
using SlopCo.Core;

namespace SlopCo.Tests.EditMode
{
    /// <summary>
    /// Truth-table regression guard for the cursor-lock predicate — the round-1 review CRITICAL was
    /// exactly this predicate. Every boolean input gets an isolated single-flip case off a known-true
    /// baseline, so a future edit that silently drops one of them fails loudly here instead of only
    /// showing up as a manual QA report.
    /// </summary>
    public class CursorPolicyTests
    {
        // Baseline: everything that must hold for the cursor to lock, all true/zero.
        static bool WantLock(bool inGame = true, bool firstPerson = true,
                              bool pause = false, bool options = false, bool controls = false, bool help = false,
                              bool roundOverlayUp = false, bool votePaused = false, int freeCursorHolders = 0)
            => CursorPolicy.WantLock(inGame, firstPerson, pause, options, controls, help,
                                      roundOverlayUp, votePaused, freeCursorHolders);

        [Test]
        public void Baseline_FirstPersonInGameNoOverlaysNoHolders_WantsLock()
        {
            Assert.IsTrue(WantLock());
        }

        [Test]
        public void Pause_Alone_PreventsLock()
        {
            Assert.IsFalse(WantLock(pause: true));
        }

        [Test]
        public void Options_Alone_PreventsLock()
        {
            Assert.IsFalse(WantLock(options: true));
        }

        [Test]
        public void Controls_Alone_PreventsLock()
        {
            Assert.IsFalse(WantLock(controls: true));
        }

        [Test]
        public void Help_Alone_PreventsLock()
        {
            Assert.IsFalse(WantLock(help: true));
        }

        [Test]
        public void RoundOverlayUp_Alone_PreventsLock()
        {
            // Payout results / augment shop — mouse-only screens InGameInteractive doesn't see (AC6).
            Assert.IsFalse(WantLock(roundOverlayUp: true));
        }

        [Test]
        public void VotePaused_Alone_PreventsLock()
        {
            // Disconnect vote overlay (AC7).
            Assert.IsFalse(WantLock(votePaused: true));
        }

        [Test]
        public void FreeCursorHolder_Alone_PreventsLock()
        {
            // Ping/emote wheel or any other holder registered via UIManager.SetFreeCursor (AC9).
            Assert.IsFalse(WantLock(freeCursorHolders: 1));
        }

        [Test]
        public void ThirdPerson_NeverLocks()
        {
            // AC10 — third person must never have its cursor grabbed by this predicate.
            Assert.IsFalse(WantLock(firstPerson: false));
        }

        [Test]
        public void NotInGame_NeverLocks()
        {
            // Menus/lobby.
            Assert.IsFalse(WantLock(inGame: false));
        }

        // ── CaptureMouse ─────────────────────────────────────────────────────────────────────

        [Test]
        public void CaptureMouse_TruthTable()
        {
            Assert.IsTrue(CursorPolicy.CaptureMouse(wantLock: true, focused: true));
            Assert.IsFalse(CursorPolicy.CaptureMouse(wantLock: true, focused: false)); // AC12: alt-tab must not fight for the cursor
            Assert.IsFalse(CursorPolicy.CaptureMouse(wantLock: false, focused: true));
            Assert.IsFalse(CursorPolicy.CaptureMouse(wantLock: false, focused: false));
        }

        // ── ShouldReadLook ───────────────────────────────────────────────────────────────────

        [Test]
        public void ShouldReadLook_TruthTable()
        {
            Assert.IsTrue(CursorPolicy.ShouldReadLook(captured: true, wasCaptured: true));
            Assert.IsFalse(CursorPolicy.ShouldReadLook(captured: true, wasCaptured: false)); // rising edge: discard the warp-tainted frame (AC8/AC9/AC12)
            Assert.IsFalse(CursorPolicy.ShouldReadLook(captured: false, wasCaptured: true));
            Assert.IsFalse(CursorPolicy.ShouldReadLook(captured: false, wasCaptured: false));
        }

        // ── SettingsManager.ClampLookSensitivity ────────────────────────────────────────────
        // Pure — touches no PlayerPrefs, so these boundary cases are safe to assert directly.

        [Test]
        public void ClampLookSensitivity_BelowMin_ClampsToMin()
        {
            Assert.AreEqual(0.2f, SettingsManager.ClampLookSensitivity(0.05f), 1e-5f);
        }

        [Test]
        public void ClampLookSensitivity_AboveMax_ClampsToMax()
        {
            Assert.AreEqual(3.0f, SettingsManager.ClampLookSensitivity(5f), 1e-5f);
        }

        [Test]
        public void ClampLookSensitivity_WithinRange_Unchanged()
        {
            Assert.AreEqual(1.4f, SettingsManager.ClampLookSensitivity(1.4f), 1e-5f);
        }

        // A corrupted opt_looksens pref must never reach the look maths. Mathf.Clamp alone cannot catch
        // this: comparisons against NaN are all false, so it hands NaN straight back — and a NaN yaw ends
        // up in Quaternion.Euler on an owner-authoritative transform, replicating a corrupt pose to the
        // whole lobby with no way back.
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void ClampLookSensitivity_NonFinite_FallsBackToDefault(float bad)
        {
            Assert.AreEqual(SettingsManager.LookSensitivityDefault,
                            SettingsManager.ClampLookSensitivity(bad), 1e-5f);
        }
    }
}
