using System;
using Unity.Netcode;
using UnityEngine;
using SlopCo.Core;

namespace SlopCo.Gameplay
{
    /// <summary>
    /// Delivery streak/combo: consecutive deliveries within a window escalate a multiplier that scales the
    /// payout (server-authoritative) and fires a hype cue. Turns one-off deliveries into a chase — the most
    /// clip-worthy + replay-driving lever — and accelerates the Cash → augment economy. Lives on the
    /// GameSystems NetworkObject; <see cref="Combo"/>/<see cref="WindowRemaining"/> replicate to the HUD.
    /// </summary>
    public sealed class ComboSystem : NetworkBehaviour
    {
        public readonly NetworkVariable<int> Combo =
            new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public readonly NetworkVariable<float> WindowRemaining =
            new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>Raised on every client when the combo advances (combo &gt;= 2): (combo, worldPos).</summary>
        public static event Action<int, Vector3> OnCombo;

        private float _timer;

        public override void OnNetworkSpawn() => ServiceLocator.Register(this);
        public override void OnNetworkDespawn() { if (ServiceLocator.Get<ComboSystem>() == this) ServiceLocator.Unregister<ComboSystem>(); }

        /// <summary>SERVER. Count a delivery, advance/start the combo, and return the payout multiplier.</summary>
        public float RegisterDeliveryAndGetMult(Vector3 pos)
        {
            if (!IsServer) return 1f;
            Combo.Value = (_timer > 0f) ? Combo.Value + 1 : 1;
            _timer = GameConstants.ComboWindowSeconds;
            WindowRemaining.Value = _timer;
            float mult = Mathf.Min(1f + GameConstants.ComboPayoutStep * (Combo.Value - 1), GameConstants.ComboMaxMult);
            if (Combo.Value >= 2) OnComboRpc(Combo.Value, pos);
            return mult;
        }

        private void Update()
        {
            if (!IsServer || _timer <= 0f) return;
            _timer -= Time.deltaTime;
            WindowRemaining.Value = Mathf.Max(0f, _timer);
            if (_timer <= 0f) Combo.Value = 0; // chain broken
        }

        [Rpc(SendTo.Everyone)]
        private void OnComboRpc(int combo, Vector3 pos) => OnCombo?.Invoke(combo, pos);
    }
}
