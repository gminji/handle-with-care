using Unity.Netcode;
using UnityEngine;
using SlopCo.Player;

namespace SlopCo.Items
{
    /// <summary>
    /// SERVER-side helpers for resolving item-effect targets. Effects themselves are applied OWNER-locally
    /// (speed buff / stamina live on each owner's <see cref="PlayerController"/> / DashState), so the server
    /// only picks the target and fires an owner-targeted RPC — see <see cref="PlayerInventory"/>.
    /// </summary>
    public static class ItemEffects
    {
        /// <summary>True if the effect targets a teammate rather than the user.</summary>
        public static bool IsAlly(ItemEffect e) => e == ItemEffect.AllySpeedBuff || e == ItemEffect.AllyStaminaRefill;

        /// <summary>True if the effect is a speed buff (vs a stamina refill).</summary>
        public static bool IsSpeed(ItemEffect e) => e == ItemEffect.SelfSpeedBurst || e == ItemEffect.AllySpeedBuff;

        /// <summary>SERVER. Nearest OTHER player to the given inventory's owner (by client id), or null if alone.</summary>
        public static PlayerController NearestOtherPlayer(PlayerInventory self)
        {
            if (self == null) return null;
            Vector3 p = self.transform.position;
            PlayerController best = null;
            float bestSq = float.MaxValue;
            foreach (var pc in Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None))
            {
                if (pc == null) continue;
                var no = pc.GetComponent<NetworkObject>();
                if (no == null || no.OwnerClientId == self.OwnerClientId) continue;   // skip the user
                float d = (pc.transform.position - p).sqrMagnitude;
                if (d < bestSq) { bestSq = d; best = pc; }
            }
            return best;
        }
    }
}
