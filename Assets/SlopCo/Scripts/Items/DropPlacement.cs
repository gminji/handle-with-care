using System;

namespace SlopCo.Items
{
    /// <summary>
    /// Pure drop-position math (NO UnityEngine → EditMode-testable, mirrors FlybyOrbit / DashStamina).
    /// Consumable capsules drop "slightly beyond the destination": start at the van, head outward along the
    /// van's facing (rotated by a random angle), and step out by <c>distance</c> — a deliberate detour so the
    /// crew must choose between delivering and grabbing the drop. Returns planar (x,z); caller adds height.
    /// </summary>
    public static class DropPlacement
    {
        public static (float x, float z) Beyond(float vanX, float vanZ, float dirX, float dirZ,
                                                float distance, float angleRad)
        {
            float len = MathF.Sqrt(dirX * dirX + dirZ * dirZ);
            if (len < 1e-4f) { dirX = 0f; dirZ = 1f; len = 1f; }   // degenerate facing → default +Z
            dirX /= len; dirZ /= len;

            float c = MathF.Cos(angleRad), s = MathF.Sin(angleRad);
            float rx = dirX * c - dirZ * s;   // rotate the unit dir by angleRad
            float rz = dirX * s + dirZ * c;

            if (distance < 0f) distance = 0f;
            return (vanX + rx * distance, vanZ + rz * distance);
        }
    }
}
