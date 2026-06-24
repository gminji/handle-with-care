using Unity.Netcode;
using SlopCo.Core;

namespace SlopCo.Gameplay
{
    /// <summary>
    /// Tracks the crew's accumulated <see cref="Cash"/> against the round <see cref="Quota"/>. Cash
    /// builds up within a round; on a successful payout the quota escalates and cash resets (rising,
    /// shared, time-boxed pressure — the Lethal Company loop). All math lives in pure QuotaMath so it
    /// is unit-tested without a NetworkManager.
    /// </summary>
    public sealed class QuotaSystem : NetworkBehaviour
    {
        public readonly NetworkVariable<int> Cash =
            new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public readonly NetworkVariable<int> Quota =
            new NetworkVariable<int>(GameConstants.StartingQuota, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public override void OnNetworkSpawn()
        {
            ServiceLocator.Register(this);
            if (IsServer)
            {
                Cash.Value = 0;
                Quota.Value = GameConstants.StartingQuota;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (ServiceLocator.Get<QuotaSystem>() == this) ServiceLocator.Unregister<QuotaSystem>();
        }

        /// <summary>SERVER. Add a delivery payout to the running cash total.</summary>
        public void AddDelivery(int payout)
        {
            if (!IsServer) return;
            if (payout < 0) payout = 0;
            Cash.Value += payout;
        }

        /// <summary>True if the crew has met this round's quota.</summary>
        public bool EvaluateQuota() => QuotaMath.IsMet(Cash.Value, Quota.Value);

        /// <summary>SERVER. Raise the quota for next round and reset accumulated cash.</summary>
        public void EscalateQuota()
        {
            if (!IsServer) return;
            Quota.Value = QuotaMath.Escalate(Quota.Value);
            Cash.Value = 0;
        }

        /// <summary>SERVER. Reset to starting values for a brand new game.</summary>
        public void ResetForNewGame()
        {
            if (!IsServer) return;
            Cash.Value = 0;
            Quota.Value = GameConstants.StartingQuota;
        }
    }
}
