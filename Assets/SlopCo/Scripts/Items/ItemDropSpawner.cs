using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using SlopCo.Core;
using SlopCo.Gameplay;

namespace SlopCo.Items
{
    /// <summary>
    /// SERVER. Periodically drops a gacha-capsule (a one-time consumable) during the Hauling phase, placed
    /// "slightly beyond the delivery van" via <see cref="DropPlacement"/> — a deliberate detour so the crew
    /// must choose between delivering and grabbing loot. Mirrors <see cref="CargoSpawner"/>'s
    /// <c>NetworkObject.InstantiateAndSpawn</c> pattern. The capsule prefab MUST also be in the NetworkManager
    /// Network Prefabs list. Lives on the GameSystems NetworkObject; wired in part 5/this part.
    /// </summary>
    public sealed class ItemDropSpawner : NetworkBehaviour
    {
        [SerializeField] private GameObject capsulePrefab;
        [Tooltip("The delivery van (drops land beyond it). If null, falls back to world origin.")]
        [SerializeField] private Transform destination;

        private float _timer;
        private readonly System.Random _rng = new System.Random();
        private readonly List<NetworkObject> _capsules = new();

        public override void OnNetworkDespawn() { if (IsServer) ClearCapsules(); }

        private void Update()
        {
            if (!IsServer || capsulePrefab == null) return;

            var rm = ServiceLocator.Get<RoundManager>();
            if (rm == null || rm.Phase.Value != RoundPhase.Hauling) { _timer = 0f; return; }

            _timer += Time.deltaTime;
            if (_timer < GameConstants.ItemDropIntervalSeconds) return;
            _timer = 0f;
            SpawnCapsule();
        }

        private void SpawnCapsule()
        {
            int itemId = PickRandomConsumable();
            if (itemId < 0) return;

            Vector3 vanPos = destination != null ? destination.position : Vector3.zero;
            Vector3 fwd = destination != null ? destination.forward : Vector3.forward;
            float angle = ((float)_rng.NextDouble() * 2f - 1f) * GameConstants.ItemDropAngleJitter;
            var (x, z) = DropPlacement.Beyond(vanPos.x, vanPos.z, fwd.x, fwd.z,
                                              GameConstants.ItemDropBeyondDistance, angle);
            Vector3 pos = new Vector3(x, vanPos.y + 1f, z);

            var no = NetworkObject.InstantiateAndSpawn(
                capsulePrefab, NetworkManager.Singleton, ownerClientId: NetworkManager.ServerClientId,
                destroyWithScene: true, isPlayerObject: false, position: pos, rotation: Quaternion.identity);
            if (no == null) return;
            _capsules.Add(no);
            var cap = no.GetComponent<ItemCapsule>();
            if (cap != null) cap.ServerSetItem(itemId);
        }

        private int PickRandomConsumable()
        {
            // Collect consumable ids from the catalog, pick one uniformly.
            int count = 0;
            for (int id = 0; id < ItemCatalog.Count; id++) if (ItemCatalog.IsConsumable(id)) count++;
            if (count == 0) return -1;
            int pick = _rng.Next(count);
            for (int id = 0; id < ItemCatalog.Count; id++)
                if (ItemCatalog.IsConsumable(id) && pick-- == 0) return id;
            return -1;
        }

        private void ClearCapsules()
        {
            foreach (var c in _capsules) if (c != null && c.IsSpawned) c.Despawn(true);
            _capsules.Clear();
        }
    }
}
