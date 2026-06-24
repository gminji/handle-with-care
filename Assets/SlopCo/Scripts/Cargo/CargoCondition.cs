using System;
using Unity.Netcode;
using UnityEngine;
using SlopCo.Core;

namespace SlopCo.Cargo
{
    /// <summary>
    /// The depreciation system — the game's scoring-as-comedy core. Server-only impact handling lowers
    /// the replicated <see cref="Condition"/> (1→0); every smash broadcasts a damage FX (the "-$$$"
    /// popup + crunch) so the funniest moment IS the scoring event, readable to spectators instantly.
    /// </summary>
    public sealed class CargoCondition : NetworkBehaviour
    {
        [SerializeField] private int baseValue = 100;
        [Tooltip("Higher = takes less damage per impact. 1 = default.")]
        [SerializeField] private float toughness = GameConstants.DefaultCargoToughness;

        /// <summary>1.0 = pristine, 0.0 = worthless. Read everywhere; written server-only.</summary>
        public readonly NetworkVariable<float> Condition =
            new NetworkVariable<float>(1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>Raised on all clients when cargo takes damage: (worldPos, valueLost, bigSmash).</summary>
        public static event Action<Vector3, int, bool> OnDamageFx;

        public int BaseValue => baseValue;
        public int CurrentValue => CargoMath.Value(baseValue, Condition.Value);

        /// <summary>SERVER ONLY. Apply a physics impact (magnitude = relative collision speed).</summary>
        public void ApplyImpact(float impactMagnitude)
        {
            if (!IsServer) return;

            float damage = CargoMath.DamageFromImpact(impactMagnitude, toughness);
            if (damage <= 0f) return;

            float before = Condition.Value;
            float after = CargoMath.ApplyDamage(before, damage);
            if (Mathf.Approximately(before, after)) return;

            Condition.Value = after;

            int valueLost = CargoMath.Value(baseValue, before) - CargoMath.Value(baseValue, after);
            bool bigSmash = impactMagnitude >= GameConstants.BigSmashImpulse;
            PlayDamageFxRpc(transform.position, valueLost, bigSmash);
        }

        [Rpc(SendTo.Everyone)]
        private void PlayDamageFxRpc(Vector3 worldPos, int valueLost, bool bigSmash)
        {
            // Subscribers (HudController) spawn the floating "-$$$" popup and, on bigSmash, a brief
            // screen-shake / hit-stop so the punchline lands on-frame for the stream.
            OnDamageFx?.Invoke(worldPos, valueLost, bigSmash);
        }
    }
}
