using UnityEngine;

namespace SlopCo.Gameplay
{
    /// <summary>
    /// A hazard that a player's kick can drive off. Implemented by <see cref="RatHazard"/> and the
    /// abduction/theft hazards. Called SERVER-SIDE ONLY by <c>KickAbility</c> once the boot connects —
    /// implementations own their own reaction (flee, drop what they stole, despawn) and must be safe
    /// to call repeatedly.
    /// </summary>
    public interface IKickable
    {
        /// <summary>SERVER. This hazard was kicked from <paramref name="fromPos"/>.</summary>
        void OnKicked(Vector3 fromPos);
    }
}
