using NUnit.Framework;
using UnityEngine;
using SlopCo.Core;

namespace SlopCo.Tests.EditMode
{
    /// <summary>
    /// Pins the first-launch OS-locale → shipped-language mapping (LanguageDetect.FromSystem).
    /// Pure lookup, same shape as ExplosionShoveTests.
    /// </summary>
    public class LanguageDetectTests
    {
        [Test]
        public void Korean_MapsToKorean() =>
            Assert.AreEqual(Language.Korean, LanguageDetect.FromSystem(SystemLanguage.Korean));

        [Test]
        public void Japanese_MapsToJapanese() =>
            Assert.AreEqual(Language.Japanese, LanguageDetect.FromSystem(SystemLanguage.Japanese));

        [Test]
        public void EveryChineseVariant_MapsToChinese()
        {
            Assert.AreEqual(Language.Chinese, LanguageDetect.FromSystem(SystemLanguage.Chinese));
            Assert.AreEqual(Language.Chinese, LanguageDetect.FromSystem(SystemLanguage.ChineseSimplified));
            Assert.AreEqual(Language.Chinese, LanguageDetect.FromSystem(SystemLanguage.ChineseTraditional));
        }

        [Test]
        public void UnshippedLanguage_FallsBackToEnglish()
        {
            Assert.AreEqual(Language.English, LanguageDetect.FromSystem(SystemLanguage.German));
            Assert.AreEqual(Language.English, LanguageDetect.FromSystem(SystemLanguage.Portuguese));
            Assert.AreEqual(Language.English, LanguageDetect.FromSystem(SystemLanguage.Unknown));
        }

        [Test]
        public void English_MapsToEnglish() =>
            Assert.AreEqual(Language.English, LanguageDetect.FromSystem(SystemLanguage.English));

        // Every mapped result must be a valid index into the LanguageNames table the Options row reads.
        [Test]
        public void MappedLanguage_IsAlwaysAValidNameIndex()
        {
            foreach (SystemLanguage sys in System.Enum.GetValues(typeof(SystemLanguage)))
            {
                int i = (int)LanguageDetect.FromSystem(sys);
                Assert.GreaterOrEqual(i, 0);
                Assert.Less(i, Localization.LanguageNames.Length);
            }
        }
    }
}
