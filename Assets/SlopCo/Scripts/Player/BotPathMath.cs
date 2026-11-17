using UnityEngine;

namespace SlopCo.Player
{
    /// <summary>
    /// Pure path-following rules for AI bots: when a planned route is stale, which waypoint to steer at, and
    /// which colliders count as level geometry for the bake. No NavMesh / scene dependency (only Vector3), so it
    /// is EditMode-testable like <see cref="KickMath"/>. <see cref="AiBrain"/> plans with the NavMesh that
    /// <see cref="BotNavMesh"/> bakes and steers through these rules.
    /// </summary>
    public static class BotPathMath
    {
        /// <summary>Ground-plane distance at which a waypoint counts as reached. Larger than one frame of
        /// travel at a dash (4.5 u/s × 1.8 ≈ 0.14 u at 60 fps) so a corner can't be stepped over and missed.</summary>
        public const float CornerReach = 0.45f;

        /// <summary>How far the goal may drift from the one the route was planned for before re-planning. Loose
        /// cargo gets kicked, thrown and blown around, so the old route ends somewhere stale.</summary>
        public const float GoalDriftReplan = 1.0f;

        /// <summary>Re-plan at least this often even when nothing obvious changed — a held cargo drifts slowly,
        /// and a route planned from a spot the bot was shoved away from no longer starts where it stands.</summary>
        public const float ReplanInterval = 1.0f;

        /// <summary>First waypoint at or after <paramref name="index"/> that is still farther than
        /// <paramref name="reach"/> from <paramref name="pos"/> on the ground plane (height ignored, so a corner
        /// at the top of a ramp is reached the moment the bot is over it). Returns <paramref name="count"/> once
        /// every waypoint is behind the bot.</summary>
        public static int AdvanceCorner(Vector3[] corners, int count, int index, Vector3 pos, float reach)
        {
            if (corners == null) return count;
            count = Mathf.Min(count, corners.Length);
            if (index < 0) index = 0;
            float reachSqr = reach * reach;
            while (index < count && FlatSqr(corners[index] - pos) <= reachSqr) index++;
            return index;
        }

        /// <summary>Does the bot need a fresh route? Yes when it has none yet, when the NavMesh was rebaked since
        /// the route was planned (new map / obstacle field), when the goal moved more than
        /// <see cref="GoalDriftReplan"/>, or when <see cref="ReplanInterval"/> has passed. A failed plan (no mesh
        /// under the bot or the goal) still counts as "planned", so a hopeless query is retried on the interval
        /// rather than every frame.</summary>
        public static bool NeedsReplan(bool planned, int plannedVersion, int meshVersion,
                                       Vector3 plannedGoal, Vector3 goal, float sinceReplan)
        {
            if (!planned) return true;
            if (plannedVersion != meshVersion) return true;
            if (FlatSqr(goal - plannedGoal) > GoalDriftReplan * GoalDriftReplan) return true;
            return sinceReplan >= ReplanInterval;
        }

        /// <summary>A bot covering less than this share of the ground its controller asked for is blocked. An
        /// unencumbered walk (4.5 u/s) at 60 fps gives the threshold the old fixed check had (≈0.02 u a frame).</summary>
        public const float StallFraction = 0.25f;

        /// <summary>Blocked for this long → hop and re-plan (AiBrain).</summary>
        public const float StallSeconds = 0.8f;

        /// <summary>The longest frame the stall clock counts. AiBrain and the controller run in no fixed order, so
        /// the step being judged may be the PREVIOUS frame's: without the cap, one long hitch would set a short
        /// honest step against a huge allowance and trip the hop on the spot.</summary>
        public const float StallMaxStep = 0.1f;

        /// <summary>Did the bot fail to get anywhere this frame? Judged against what its controller ASKED for
        /// (PlayerController.PlanarVelocity), not against a fixed distance or speed: a fixed 0.02 u a frame read
        /// every slow haul as stuck once the host ran past ~100 fps, and a fixed speed does the same to a haul
        /// slowed by augments (Hands + Insured + Marathon bring a lone two-hand carry down to ≈0.9 u/s). A bot
        /// that was asked for nothing (frozen for a vote, no time in the frame) is not blocked.</summary>
        public static bool IsStalled(Vector3 from, Vector3 to, float commandedSpeed, float dt)
        {
            if (dt <= 0f || commandedSpeed <= 0f) return false;
            float limit = StallFraction * commandedSpeed * dt;
            return (to - from).sqrMagnitude < limit * limit;
        }

        /// <summary>Advance the stall clock by one frame: it runs while the bot is blocked and resets the moment
        /// it makes headway. The bot hops when the clock passes <see cref="StallSeconds"/>.</summary>
        public static float StepStallClock(float clock, Vector3 from, Vector3 to, float commandedSpeed, float dt)
        {
            float step = Mathf.Min(dt, StallMaxStep);
            return IsStalled(from, to, commandedSpeed, step) ? clock + step : 0f;
        }

        /// <summary>Is this collider part of the level the bots walk on? Triggers (delivery zone, hazards) are
        /// not solid; anything with a Rigidbody (cargo, item capsules) moves; CharacterControllers are players
        /// and bots. Baking any of those would carve holes in the floor wherever they happened to be standing.</summary>
        public static bool IsBakeSource(bool isTrigger, bool hasRigidbody, bool isCharacterController) =>
            !isTrigger && !hasRigidbody && !isCharacterController;

        /// <summary>Did snapping <paramref name="asked"/> onto the mesh land close enough to count as that point:
        /// within <paramref name="maxFlat"/> on the ground plane and <paramref name="maxUp"/> vertically?
        /// Farther means the point itself is off the mesh and the snap picked somewhere else.</summary>
        public static bool IsOnPoint(Vector3 asked, Vector3 snapped, float maxFlat, float maxUp) =>
            FlatSqr(snapped - asked) <= maxFlat * maxFlat && Mathf.Abs(snapped.y - asked.y) <= maxUp;

        private static float FlatSqr(Vector3 v) => v.x * v.x + v.z * v.z;
    }
}
