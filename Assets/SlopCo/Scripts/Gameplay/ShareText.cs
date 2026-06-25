using System.Text;
using SlopCo.Core;

namespace SlopCo.Gameplay
{
    /// <summary>
    /// Pure clipboard brag builder for the end-of-run share. Emits PLAIN text (no rich-text tags) so it
    /// pastes cleanly into chat/social. EditMode-testable like <see cref="RunGrade"/> — its only outside
    /// call is the static <see cref="Localization.Get"/> dictionary lookup, which is deterministic
    /// (<see cref="Localization.Current"/> defaults to English when the language pref was never loaded), so
    /// tests assert the English output without a NetworkManager. The headline is passed in already-localized
    /// and already-token-substituted, so the result never contains an unreplaced "{token}".
    /// </summary>
    public static class ShareText
    {
        public static string Build(string gradeLetter, string headline, int day, int cash,
                                   int deliveries, int bestDay, int bestChain)
        {
            var sb = new StringBuilder();
            sb.Append("SlopCo ").Append(Localization.Get("grade.title")).Append(' ')
              .Append(string.IsNullOrEmpty(gradeLetter) ? "?" : gradeLetter).Append('\n');
            if (!string.IsNullOrEmpty(headline)) sb.Append(headline).Append('\n');
            sb.Append("Day ").Append(day).Append(" · $").Append(cash)
              .Append(" · ").Append(deliveries).Append(" del");
            if (bestDay > 0)
            {
                sb.Append(" · best Day ").Append(bestDay);
                if (bestChain >= 2) sb.Append(" x").Append(bestChain);
            }
            return sb.ToString();
        }
    }
}
