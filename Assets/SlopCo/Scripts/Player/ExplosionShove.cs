using UnityEngine;

namespace SlopCo.Player
{
    /// <summary>
    /// Pure knockback math: the horizontal impulse a blast imparts to a body, with linear distance falloff.
    /// No NetworkManager / scene dependency (only Vector3), so it is EditMode-testable like RunGrade /
    /// ShareText / MusicBus. The vertical "pop" is applied by the caller (PlayerController) — this returns a
    /// purely horizontal push so the falloff curve stays the single source of truth pinned by tests.
    /// </summary>
    public static class ExplosionShove
    {
        /// <summary>Horizontal velocity (units/sec) to shove <paramref name="self"/> away from
        /// <paramref name="blast"/>. Zero outside <paramref name="radius"/> (or non-positive radius); peak
        /// <paramref name="maxSpeed"/> at the center. Dead-center falls back to an arbitrary (+X) direction.</summary>
        public static Vector3 Impulse(Vector3 self, Vector3 blast, float radius, float maxSpeed)
        {
            Vector3 away = self - blast;
            away.y = 0f;
            float dist = away.magnitude;
            if (radius <= 0f || dist > radius) return Vector3.zero;
            float falloff = 1f - dist / radius;                                // 1 at center → 0 at the edge
            Vector3 dir = dist > 0.0001f ? away / dist : Vector3.right;        // dead-center guard
            return dir * (maxSpeed * falloff);
        }
    }
}
