using NUnit.Framework;
using SlopCo.Core;
using SlopCo.Gameplay;

namespace SlopCo.Tests.EditMode
{
    /// <summary>
    /// Pins the pure clipboard brag (ShareText.Build) — same shape as RunGradeTests. Language is forced to
    /// English so the assertions are deterministic regardless of test order / leaked language state.
    /// </summary>
    public class ShareTextTests
    {
        [SetUp]
        public void ForceEnglish() => Localization.SetLanguage(Language.English);

        [Test]
        public void Build_IncludesGradeHeadlineAndStats()
        {
            string r = ShareText.Build("S", "Survived 7 days of chaos.", day: 7, cash: 940,
                                       deliveries: 12, bestDay: 7, bestChain: 4);
            StringAssert.Contains("CHAOS RATING S", r); // grade.title + letter
            StringAssert.Contains("Survived 7 days of chaos.", r);
            StringAssert.Contains("Day 7", r);
            StringAssert.Contains("$940", r);
            StringAssert.Contains("12 del", r);
            StringAssert.Contains("best Day 7 x4", r);
        }

        [Test]
        public void Build_LeavesNoUnreplacedTokens()
        {
            // Token-leak regression guard: a raw "{token}" on a shareable card is a visible bug.
            string r = ShareText.Build("A", "x4 delivery chain — certified menace.", 5, 500, 8, 5, 4);
            StringAssert.DoesNotContain("{", r);
        }

        [Test]
        public void Build_NoRecordsYet_OmitsBestLine()
        {
            string r = ShareText.Build("", "", day: 1, cash: 0, deliveries: 0, bestDay: 0, bestChain: 0);
            StringAssert.Contains("?", r);            // empty grade → placeholder, no crash
            StringAssert.DoesNotContain("best Day", r); // bestDay==0 → no records tail
        }
    }
}
