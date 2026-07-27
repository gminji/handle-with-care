using UnityEngine;

namespace SlopCo.Player
{
    /// <summary>
    /// Pure kick geometry: who is inside the boot's arc, and how hard they get launched. No scene /
    /// NetworkManager dependency (only Vector3), so it is EditMode-testable like <see cref="ExplosionShove"/>.
    /// The server uses this to resolve a kick; the impulse is then applied by the VICTIM'S owner
    /// (CharacterControllers can't be pushed from the server).
    /// </summary>
    public static class KickMath
    {
        /// <summary>Is <paramref name="target"/> within <paramref name="range"/> of the kicker AND inside the
        /// ±<paramref name="halfAngleDeg"/> arc around its facing? Height is ignored (planar check) so you can
        /// still boot someone standing on a crate. A target at the kicker's exact position always counts.</summary>
        public static bool InCone(Vector3 kicker, Vector3 forward, Vector3 target, float range, float halfAngleDeg)
        {
            if (range <= 0f) return false;
            Vector3 to = target - kicker; to.y = 0f;
            float dist = to.magnitude;
            if (dist > range) return false;
            if (dist < 0.0001f) return true;                       // point-blank overlap

            Vector3 f = forward; f.y = 0f;
            if (f.sqrMagnitude < 1e-6f) return false;              // no facing → no kick
            float cos = Vector3.Dot(f.normalized, to / dist);
            return cos >= Mathf.Cos(Mathf.Clamp(halfAngleDeg, 0f, 180f) * Mathf.Deg2Rad);
        }

        /// <summary>Horizontal launch velocity for a kicked body. Direction is a 50/50 blend of "away from the
        /// kicker" and "where the kicker is facing" — a pure radial push feels like an explosion, a pure facing
        /// push shoves people sideways through you. Strength falls off with distance but never below
        /// <see cref="MinFalloff"/> of the peak, because a connecting kick should always be felt.</summary>
        public const float MinFalloff = 0.45f;

        public static Vector3 Impulse(Vector3 kicker, Vector3 forward, Vector3 target, float range, float maxSpeed)
        {
            if (range <= 0f) return Vector3.zero;
            Vector3 away = target - kicker; away.y = 0f;
            float dist = away.magnitude;
            if (dist > range) return Vector3.zero;

            Vector3 f = forward; f.y = 0f;
            f = f.sqrMagnitude > 1e-6f ? f.normalized : Vector3.forward;
            Vector3 radial = dist > 0.0001f ? away / dist : f;      // dead-center → straight ahead

            Vector3 dir = (radial + f) * 0.5f;
            dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : f;

            float falloff = MinFalloff + (1f - MinFalloff) * (1f - dist / range);
            return dir * (maxSpeed * falloff);
        }
    }
}
