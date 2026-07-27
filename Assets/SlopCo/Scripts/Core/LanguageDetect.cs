using UnityEngine;

namespace SlopCo.Core
{
    /// <summary>
    /// Pure OS-locale → <see cref="Language"/> mapping used for the FIRST-LAUNCH default (see
    /// <see cref="Localization.Load"/>). No PlayerPrefs / scene dependency, so it is EditMode-testable
    /// like <c>ExplosionShove</c> / <c>DashStamina</c>. Anything the game does not ship a table for
    /// falls back to English — a wrong-but-readable UI beats an empty one.
    /// </summary>
    public static class LanguageDetect
    {
        /// <summary>Which shipped language best matches the machine's OS language.</summary>
        public static Language FromSystem(SystemLanguage sys) => sys switch
        {
            SystemLanguage.Korean               => Language.Korean,
            SystemLanguage.Japanese             => Language.Japanese,
            SystemLanguage.Chinese              => Language.Chinese,
            SystemLanguage.ChineseSimplified    => Language.Chinese,
            SystemLanguage.ChineseTraditional   => Language.Chinese,
            _                                   => Language.English,
        };
    }
}
