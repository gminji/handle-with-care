using System.Collections.Generic;

namespace SlopCo.Gameplay
{
    /// <summary>
    /// Per-player run tally + pure award selection for the end-of-run "CREW MVP" callout — NO UnityEngine /
    /// NGO dependency, so it is unit-testable in EditMode (mirrors CargoMath / VoiceActivity, the project's
    /// "pure math + EditMode test" convention). <see cref="PlayerScoreboard"/> builds the tally list from the
    /// replicated attribution events; this class just picks the winners.
    ///
    /// CarrierId is a player's NetworkObjectId (unique per human AND per AI bot). 0 means "unattributed"
    /// and is never produced here (the scoreboard drops id 0 before tallying).
    /// </summary>
    public readonly struct PlayerTally
    {
        public readonly ulong CarrierId;
        public readonly int Delivered;   // total $ value successfully delivered
        public readonly int Destroyed;   // total $ value smashed

        public PlayerTally(ulong carrierId, int delivered, int destroyed)
        {
            CarrierId = carrierId;
            Delivered = delivered;
            Destroyed = destroyed;
        }
    }

    public static class MvpAward
    {
        /// <summary>CarrierId of the top deliverer (Delivered &gt; 0). 0 when nobody delivered. Ties break to
        /// the lowest CarrierId so the result is deterministic across clients.</summary>
        public static ulong TopDeliverer(IReadOnlyList<PlayerTally> tallies)
        {
            ulong best = 0; int bestVal = 0;
            if (tallies == null) return 0;
            for (int i = 0; i < tallies.Count; i++)
            {
                var t = tallies[i];
                if (t.Delivered <= 0) continue;
                if (t.Delivered > bestVal || (t.Delivered == bestVal && (best == 0 || t.CarrierId < best)))
                {
                    best = t.CarrierId; bestVal = t.Delivered;
                }
            }
            return best;
        }

        /// <summary>CarrierId of the top destroyer (Destroyed &gt; 0). 0 when nobody smashed anything. Ties
        /// break to the lowest CarrierId (deterministic).</summary>
        public static ulong TopDestroyer(IReadOnlyList<PlayerTally> tallies)
        {
            ulong best = 0; int bestVal = 0;
            if (tallies == null) return 0;
            for (int i = 0; i < tallies.Count; i++)
            {
                var t = tallies[i];
                if (t.Destroyed <= 0) continue;
                if (t.Destroyed > bestVal || (t.Destroyed == bestVal && (best == 0 || t.CarrierId < best)))
                {
                    best = t.CarrierId; bestVal = t.Destroyed;
                }
            }
            return best;
        }

        /// <summary>Number of distinct players who did anything (Delivered &gt; 0 OR Destroyed &gt; 0). The card
        /// hides the MVP block below 2 so a solo run never shows a trivial self-award.</summary>
        public static int ParticipantCount(IReadOnlyList<PlayerTally> tallies)
        {
            if (tallies == null) return 0;
            int n = 0;
            for (int i = 0; i < tallies.Count; i++)
            {
                var t = tallies[i];
                if (t.Delivered > 0 || t.Destroyed > 0) n++;
            }
            return n;
        }
    }
}
