using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using SlopCo.Core;
using SlopCo.GameAssets;

namespace SlopCo.Gameplay
{
    /// <summary>
    /// Server-only cargo spawning at the depot each round, using the NGO 2.x static
    /// <c>NetworkObject.InstantiateAndSpawn</c>. Prefabs must also be registered in the NetworkManager's
    /// Network Prefabs list. Tracks spawned objects so leftovers can be cleared at payout.
    /// </summary>
    public sealed class CargoSpawner : NetworkBehaviour
    {
        [SerializeField] private NetworkPrefabRegistry registry;
        [SerializeField] private Transform[] depotSpawnPoints;
        [SerializeField] private int cargoPerRound = GameConstants.CargoPerRound;

        private readonly List<NetworkObject> _spawned = new();
        private readonly System.Random _rng = new System.Random(12345); // deterministic for repeatable tests

        /// <summary>SERVER. Spawn this round's cargo at the depot.</summary>
        public void SpawnRoundCargo()
        {
            if (!IsServer) return;
            if (registry == null || !registry.HasCargo)
            {
                Debug.LogError("[CargoSpawner] No cargo prefabs in NetworkPrefabRegistry.");
                return;
            }

            ClearRemainingCargo();

            for (int i = 0; i < cargoPerRound; i++)
            {
                var prefab = registry.cargoPrefabs[_rng.Next(registry.cargoPrefabs.Length)];
                if (prefab == null) continue;

                (Vector3 pos, Quaternion rot) = SpawnPose(i);
                var netObj = NetworkObject.InstantiateAndSpawn(
                    prefab,
                    NetworkManager.Singleton,
                    ownerClientId: NetworkManager.ServerClientId,
                    destroyWithScene: true,
                    isPlayerObject: false,
                    position: pos,
                    rotation: rot);

                if (netObj != null) _spawned.Add(netObj);
            }
        }

        /// <summary>SERVER. Despawn any cargo still in the world (undelivered leftovers).</summary>
        public void ClearRemainingCargo()
        {
            if (!IsServer) return;
            foreach (var obj in _spawned)
                if (obj != null && obj.IsSpawned) obj.Despawn(true);
            _spawned.Clear();
        }

        private (Vector3, Quaternion) SpawnPose(int index)
        {
            if (depotSpawnPoints != null && depotSpawnPoints.Length > 0)
            {
                var t = depotSpawnPoints[index % depotSpawnPoints.Length];
                if (t != null) return (t.position + Vector3.up * 0.5f, t.rotation);
            }
            // Fallback grid near origin.
            return (new Vector3((index % 3) * 1.5f, 1f, (index / 3) * 1.5f), Quaternion.identity);
        }
    }
}
