namespace SlopCo.Gameplay
{
    /// <summary>
    /// Pure vote counting shared by every crew decision (augment pick, map pick, "should we bail?").
    /// NO UnityEngine dependency, so it is EditMode-testable like <see cref="DailyModifier"/>, whose
    /// hashing it reuses for the tie-break.
    ///
    /// The tie-break MUST be resolved once, on the server, and only the result replicated — if each client
    /// rolled its own the crew would disagree about what they just voted for.
    /// </summary>
    public static class VoteTally
    {
        /// <summary>Winning option index, or -1 when nobody voted. Ties are broken uniformly at random from
        /// the leaders using <paramref name="seed"/>; <paramref name="wasTie"/> reports whether that happened
        /// (the UI calls it out so a random pick never looks like a bug).</summary>
        public static int Resolve(int[] counts, uint seed, out bool wasTie)
        {
            wasTie = false;
            if (counts == null || counts.Length == 0) return -1;

            int best = 0;
            for (int i = 0; i < counts.Length; i++) if (counts[i] > best) best = counts[i];
            if (best <= 0) return -1;                       // no votes cast at all

            int leaders = 0;
            for (int i = 0; i < counts.Length; i++) if (counts[i] == best) leaders++;
            if (leaders == 1)
            {
                for (int i = 0; i < counts.Length; i++) if (counts[i] == best) return i;
                return -1;
            }

            wasTie = true;
            int pick = (int)(((ulong)Mix(seed) * (ulong)leaders) >> 32);   // uniform over the leaders
            int seen = 0;
            for (int i = 0; i < counts.Length; i++)
            {
                if (counts[i] != best) continue;
                if (seen == pick) return i;
                seen++;
            }
            return -1;
        }

        /// <summary>Convenience overload for callers that don't care whether it was a tie.</summary>
        public static int Resolve(int[] counts, uint seed) => Resolve(counts, seed, out _);

        /// <summary>True when the leading option is shared by two or more choices (and anyone voted).</summary>
        public static bool IsTie(int[] counts)
        {
            if (counts == null || counts.Length == 0) return false;
            int best = 0;
            for (int i = 0; i < counts.Length; i++) if (counts[i] > best) best = counts[i];
            if (best <= 0) return false;
            int leaders = 0;
            for (int i = 0; i < counts.Length; i++) if (counts[i] == best) leaders++;
            return leaders > 1;
        }

        /// <summary>Total ballots cast.</summary>
        public static int Total(int[] counts)
        {
            if (counts == null) return 0;
            int n = 0;
            for (int i = 0; i < counts.Length; i++) if (counts[i] > 0) n += counts[i];
            return n;
        }

        // lowbias32 finalizer — the project's standard mixer (see DailyModifier.Roll).
        private static uint Mix(uint h)
        {
            h ^= h >> 16; h *= 0x7feb352dU; h ^= h >> 15; h *= 0x846ca68bU; h ^= h >> 16;
            return h;
        }
    }
}
