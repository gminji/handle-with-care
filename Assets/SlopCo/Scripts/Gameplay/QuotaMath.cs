using System;

namespace SlopCo.Gameplay
{
    /// <summary>
    /// Pure quota math — NO UnityEngine / NGO dependency, so it is unit-testable in EditMode without
    /// a NetworkManager. QuotaSystem (a NetworkBehaviour) delegates its arithmetic here.
    /// </summary>
    public static class QuotaMath
    {
        public const float EscalationFactor = 1.4f;
        public const int EscalationFlat = 50;

        /// <summary>Next round's quota: ceil(current * 1.4) + 50.</summary>
        public static int Escalate(int currentQuota)
        {
            if (currentQuota < 0) currentQuota = 0;
            return (int)Math.Ceiling(currentQuota * (double)EscalationFactor) + EscalationFlat;
        }

        /// <summary>Quota is met when accumulated cash reaches the quota.</summary>
        public static bool IsMet(int cash, int quota) => cash >= quota;

        /// <summary>How far short the crew is (0 if met). Useful for "you owe $X" UI.</summary>
        public static int Shortfall(int cash, int quota) => Math.Max(0, quota - cash);
    }
}
