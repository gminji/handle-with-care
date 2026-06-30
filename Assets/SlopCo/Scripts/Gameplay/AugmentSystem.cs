using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using SlopCo.Core;

namespace SlopCo.Gameplay
{
    public enum AugEffect { CarrySpeed, MoveSpeed, Payout, FuseSlow }
    public enum AugDownside { None, Rats, FasterFuse, SlowerCarry, LessPayout, SlowerMove }

    /// <summary>One purchasable augment: an effect plus a trade-off (roguelite "pick your poison").</summary>
    [System.Serializable]
    public struct AugmentDef
    {
        public int id;
        public string nameKey, descKey;
        public int cost;
        public AugEffect effect; public float magnitude;
        public AugDownside downside; public float downsideMag;
    }

    /// <summary>
    /// Roguelite augment shop core. Crew spends the persistent <see cref="QuotaSystem.Cash"/> (bomb mode
    /// never resets it) to buy augments that scale cargo-hauling. Ownership is a replicated bitmask
    /// (<see cref="OwnedMask"/>) — server-authoritative buy via RPC, read everywhere for the aggregate
    /// multipliers. The "rats" downside is OPTIONAL: if <see cref="ratPrefab"/> is unassigned it's a no-op,
    /// so the framework works without the enemy prefab. Lives on the GameSystems NetworkObject.
    /// </summary>
    public sealed class AugmentSystem : NetworkBehaviour
    {
        [SerializeField] private QuotaSystem quota;
        [SerializeField] private GameObject ratPrefab;        // OPTIONAL — null = rats downside is a no-op
        [SerializeField] private Transform[] ratSpawnPoints;

        // Owned augments as a replicated BITMASK (bit i = owns Catalog[i]). NetworkVariable (the project's
        // established pattern) rather than NetworkList (no precedent here) — simpler + lower risk.
        public readonly NetworkVariable<int> OwnedMask =
            new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly List<NetworkObject> _rats = new();

        private static readonly AugmentDef[] Catalog =
        {
            new AugmentDef{ id=0, nameKey="aug.light.name",  descKey="aug.light.desc",  cost=150, effect=AugEffect.CarrySpeed, magnitude=0.35f, downside=AugDownside.Rats,        downsideMag=1f },
            new AugmentDef{ id=1, nameKey="aug.adr.name",    descKey="aug.adr.desc",    cost=200, effect=AugEffect.MoveSpeed,  magnitude=0.20f, downside=AugDownside.FasterFuse,  downsideMag=0.30f },
            new AugmentDef{ id=2, nameKey="aug.hands.name",  descKey="aug.hands.desc",  cost=180, effect=AugEffect.Payout,     magnitude=0.25f, downside=AugDownside.SlowerCarry, downsideMag=0.15f },
            new AugmentDef{ id=3, nameKey="aug.sprint.name", descKey="aug.sprint.desc", cost=120, effect=AugEffect.MoveSpeed,  magnitude=0.15f, downside=AugDownside.LessPayout,  downsideMag=0.15f },
            new AugmentDef{ id=4,  nameKey="aug.steady.name",   descKey="aug.steady.desc",   cost=160, effect=AugEffect.FuseSlow,   magnitude=0.25f, downside=AugDownside.LessPayout,  downsideMag=0.10f },
            new AugmentDef{ id=5,  nameKey="aug.insured.name",  descKey="aug.insured.desc",  cost=220, effect=AugEffect.FuseSlow,   magnitude=0.40f, downside=AugDownside.SlowerCarry, downsideMag=0.20f },
            new AugmentDef{ id=6,  nameKey="aug.caffeine.name", descKey="aug.caffeine.desc", cost=210, effect=AugEffect.MoveSpeed,  magnitude=0.30f, downside=AugDownside.FasterFuse,  downsideMag=0.25f },
            new AugmentDef{ id=7,  nameKey="aug.forklift.name", descKey="aug.forklift.desc", cost=240, effect=AugEffect.CarrySpeed, magnitude=0.50f, downside=AugDownside.SlowerMove,  downsideMag=0.20f },
            new AugmentDef{ id=8,  nameKey="aug.hazard.name",   descKey="aug.hazard.desc",   cost=230, effect=AugEffect.Payout,     magnitude=0.40f, downside=AugDownside.Rats,        downsideMag=1f },
            new AugmentDef{ id=9,  nameKey="aug.marathon.name", descKey="aug.marathon.desc", cost=200, effect=AugEffect.MoveSpeed,  magnitude=0.40f, downside=AugDownside.SlowerCarry, downsideMag=0.25f },
            new AugmentDef{ id=10, nameKey="aug.greased.name",  descKey="aug.greased.desc",  cost=170, effect=AugEffect.Payout,     magnitude=0.30f, downside=AugDownside.FasterFuse,  downsideMag=0.20f },
            new AugmentDef{ id=11, nameKey="aug.mule.name",     descKey="aug.mule.desc",     cost=150, effect=AugEffect.CarrySpeed, magnitude=0.30f, downside=AugDownside.SlowerMove,  downsideMag=0.15f },
        };

        public static int CatalogCount => Catalog.Length;
        public static AugmentDef Get(int id) => Catalog[Mathf.Clamp(id, 0, Catalog.Length - 1)];
        public bool Owns(int id) => id >= 0 && id < Catalog.Length && (OwnedMask.Value & (1 << id)) != 0;

        public override void OnNetworkSpawn()
        {
            ServiceLocator.Register(this);
            if (quota == null) quota = GetComponent<QuotaSystem>();
        }

        public override void OnNetworkDespawn()
        {
            ClearRats();
            if (ServiceLocator.Get<AugmentSystem>() == this) ServiceLocator.Unregister<AugmentSystem>();
        }

        // Aggregate multipliers (1.0 = neutral) summed across owned augments' effects + downsides.
        public float MoveSpeedMult  { get { float m = 1f; for (int id = 0; id < Catalog.Length; id++) { if (!Owns(id)) continue; var a = Catalog[id]; if (a.effect == AugEffect.MoveSpeed) m += a.magnitude; if (a.downside == AugDownside.SlowerMove) m -= a.downsideMag; } return Mathf.Max(0.3f, m); } }
        public float CarrySpeedMult { get { float m = 1f; for (int id = 0; id < Catalog.Length; id++) { if (!Owns(id)) continue; var a = Catalog[id]; if (a.effect == AugEffect.CarrySpeed) m += a.magnitude; if (a.downside == AugDownside.SlowerCarry) m -= a.downsideMag; } return Mathf.Max(0.3f, m); } }
        public float FuseMult       { get { float m = 1f; for (int id = 0; id < Catalog.Length; id++) { if (!Owns(id)) continue; var a = Catalog[id]; if (a.effect == AugEffect.FuseSlow) m -= a.magnitude; if (a.downside == AugDownside.FasterFuse) m += a.downsideMag; } return Mathf.Max(0.3f, m); } }
        public float PayoutMult     { get { float m = 1f; for (int id = 0; id < Catalog.Length; id++) { if (!Owns(id)) continue; var a = Catalog[id]; if (a.effect == AugEffect.Payout) m += a.magnitude; if (a.downside == AugDownside.LessPayout) m -= a.downsideMag; } return Mathf.Max(0.2f, m); } }

        private bool RatsActive { get { for (int id = 0; id < Catalog.Length; id++) if (Owns(id) && Catalog[id].downside == AugDownside.Rats) return true; return false; } }

        /// <summary>SERVER (via client request). Buy an augment if affordable and not already owned.</summary>
        [Rpc(SendTo.Server)]
        public void RequestBuyRpc(int id)
        {
            if (id < 0 || id >= Catalog.Length || Owns(id)) return;
            var a = Catalog[id];
            if (quota == null || quota.Cash.Value < a.cost) return;
            quota.Cash.Value -= a.cost;
            OwnedMask.Value |= (1 << id);
        }

        // ── Rats downside (optional) — RoundManager drives spawn at Hauling / clear at Payout ──
        public void SpawnRatsIfNeeded()
        {
            if (!IsServer || ratPrefab == null || !RatsActive) return;
            int n = (ratSpawnPoints != null && ratSpawnPoints.Length > 0) ? Mathf.Min(2, ratSpawnPoints.Length) : 1;
            for (int i = 0; i < n; i++)
            {
                Vector3 pos = (ratSpawnPoints != null && ratSpawnPoints.Length > 0 && ratSpawnPoints[i % ratSpawnPoints.Length] != null)
                    ? ratSpawnPoints[i % ratSpawnPoints.Length].position + Vector3.up * 0.5f
                    : new Vector3(i * 2f, 0.5f, 0f);
                var no = NetworkObject.InstantiateAndSpawn(
                    ratPrefab, NetworkManager.Singleton,
                    ownerClientId: NetworkManager.ServerClientId,
                    destroyWithScene: true, isPlayerObject: false,
                    position: pos, rotation: Quaternion.identity);
                if (no != null) _rats.Add(no);
            }
        }

        public void ClearRats()
        {
            if (!IsServer) return;
            foreach (var r in _rats) if (r != null && r.IsSpawned) r.Despawn(true);
            _rats.Clear();
        }
    }
}
