namespace SlopCo.Cargo
{
    /// <summary>
    /// Pure co-carry math (NO UnityEngine dependency -> EditMode-testable, mirrors CargoMath /
    /// QuotaMath / DashStamina). Load is expressed in PERSON-UNITS: how many people this item is
    /// balanced for right now. Both downstream numbers (PD lift strength, haul speed) are derived
    /// from the single ratio grabbers/load, so there is one knob set, not two.
    /// Tuning constants are passed in explicitly so tests are independent of GameConstants.
    /// </summary>
    public static class CoCarryMath
    {
        /// <summary>How many person-units this item is "worth" right now. Solo mode and any item with
        /// baseCrew &lt;= 1 (OneHand, or the Volatile archetype's OneHand downshift) never scale.
        /// After a disconnect the live crew can drop to 1 — that is treated as solo too, since
        /// punishing an already-failing run against an escalated fuse is a dead end, not a challenge.
        /// The result is floored at baseCrew (never let an under-handled prefab silently downgrade)
        /// and ceilinged by handleCount (the hard physical limit on how many hands can grip it).</summary>
        public static float LoadCrew(int baseCrew, int crewSize, int handleCount,
                                     bool solo, int maxCrew, float loadPerExtraPlayer)
        {
            if (baseCrew < 1) baseCrew = 1;
            if (solo) return 1f;                 // Solo/Tutorial: one player carries anything (unchanged)
            if (baseCrew <= 1) return 1f;        // OneHand / Volatile never scales (unchanged)

            // LAST PLAYER STANDING: after disconnects the live crew can be 1. Treat that as solo instead of
            // holding the 2-person baseline — one player can carry anything at full strength, and punishing
            // an already-failing run against a Day-N escalated fuse is a dead end, not a challenge.
            if (crewSize <= 1) return 1f;

            int crew = crewSize > maxCrew ? maxCrew : crewSize;
            if (crew < baseCrew) crew = baseCrew;
            float load = baseCrew + loadPerExtraPlayer * (crew - baseCrew);

            // Handles are the hard ceiling on how many people can physically grip this item — never demand
            // more crew than the prefab can seat, or a 2-handle piano becomes uncarriable in a 4-player lobby.
            // FLOORED AT baseCrew: an UNDER-handled prefab (a TwoPerson item shipped with one handle) must not
            // silently downgrade to a one-person item (a data bug quietly turned into a balance change), but
            // it must not stay at the full crew-scaled load either. It clamps to the baseline.
            if (load > handleCount) load = handleCount < baseCrew ? baseCrew : handleCount;
            return load < 1f ? 1f : load;
        }

        /// <summary>Continuous PD lift-strength ramp. The first grabber always gets exactly
        /// underCrewedStrength (today's 0.28), full crew always gets 1.0, and the values in between
        /// ramp linearly — no more binary cliff on a forced mid-air drop.</summary>
        public static float LiftStrength(int grabbers, float loadCrew, float underCrewedStrength)
        {
            if (grabbers <= 0) return 0f;
            if (loadCrew <= 1f || grabbers >= loadCrew) return 1f;
            float t = (grabbers - 1f) / (loadCrew - 1f);
            if (t < 0f) t = 0f; else if (t > 1f) t = 1f;
            return underCrewedStrength + (1f - underCrewedStrength) * t;
        }

        /// <summary>Carry-speed factor derived from a single ratio r = grabbers / loadCrew. r &lt; 1
        /// drags (under-crewed), r == 1 gives fullGripSpeed exactly, r &gt; 1 hauls faster (over-crewed).
        /// A ONE-PERSON LOAD has no co-carry economy: solo mode, any OneHand cargo (incl. the Volatile
        /// archetype's downshift) and the last-player-standing case all land here and get EXACTLY
        /// today's flat constant, at any number of grabbers.</summary>
        public static float HaulSpeedFactor(int grabbers, float loadCrew, float baseCarryMult,
                                            float fullGripSpeed, float underCrewedFloor, float overCrewBonus)
        {
            if (grabbers <= 0) return 1f;        // not carrying -> no penalty at all
            if (loadCrew <= 1f) return baseCarryMult;

            float r = grabbers / loadCrew;

            // Saturation: one extra pair of hands beyond the load is the most that can ever help. With
            // today's constants this never binds; it exists so a future handle count / crew miscount
            // can't produce a runaway factor.
            float rMax = (loadCrew + 1f) / loadCrew;
            if (r > rMax) r = rMax;

            return r < 1f
                ? fullGripSpeed * (underCrewedFloor + (1f - underCrewedFloor) * r)   // under-crewed: drag
                : fullGripSpeed * (1f + overCrewBonus * (r - 1f));                   // over-crewed: haul faster
        }

        /// <summary>The value that actually reaches CharacterController.Move. The ceiling lives HERE,
        /// after the augment multiplier, because AugmentSystem.CarrySpeedMult has a lower bound only.</summary>
        public static float ComposedCarryMult(float haulFactor, float augmentMult, float maxMult)
        {
            float m = haulFactor * augmentMult;
            return m > maxMult ? maxMult : m;
        }

        /// <summary>Slew the replicated factor toward a new target so a mid-run join/leave decelerates
        /// instead of teleporting the carriers' speed in one tick.</summary>
        public static float RampToward(float current, float target, float dt, float ratePerSecond)
        {
            if (dt <= 0f || ratePerSecond <= 0f) return target;
            float step = ratePerSecond * dt;
            float d = target - current;
            if (d > step) return current + step;
            if (d < -step) return current - step;
            return target;
        }

        /// <summary>Pure handle picking (ties -> lowest index, all-occupied -> -1). Extracted out of
        /// CargoItem so the riskiest new branch is EditMode-testable without a Transform.</summary>
        public static int PickFreeHandle(int preferredId, bool[] free, float[] sqrDist, int count)
        {
            if (count <= 0) return -1;
            // "Usable" must mean the same thing in the fast path as in the scan below. CargoItem already
            // folds usability into free[] (an unusable handle is never free), so this is belt-and-braces —
            // but a preferred handle that is free with no reachable AttachPoint would otherwise be handed
            // back here and rejected only later, while the scan would have found a working handle.
            if (preferredId >= 0 && preferredId < count && free[preferredId] && Usable(sqrDist[preferredId]))
                return preferredId;

            int best = -1; float bestSqr = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (!free[i]) continue;
                float d = sqrDist[i];
                if (!Usable(d)) continue;                     // unusable handle (null AttachPoint)
                if (d < bestSqr) { bestSqr = d; best = i; }   // strict < => ties pick the LOWEST index
            }
            return best;
        }

        private static bool Usable(float sqrDist) => !float.IsNaN(sqrDist) && !float.IsPositiveInfinity(sqrDist);

        /// <summary>The AI "come help" predicate. Three conditions, all necessary:
        ///   grabbers > 0            — nobody on it yet means it is LOOSE, which is a CLAIM, not a join.
        ///   grabbers &lt; handleCount  — no free grip left = nothing to join even if the load asks for more.
        ///   grabbers &lt; loadCrew     — the load still wants hands. FLOAT comparison: this is the boundary that
        ///                             decides whether a bot walks toward a teammate, so it is pinned by tests.
        /// </summary>
        public static bool NeedsMoreHands(int grabbers, float loadCrew, int handleCount)
            => grabbers > 0 && grabbers < handleCount && grabbers < loadCrew;
    }
}
