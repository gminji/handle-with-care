using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using SlopCo.Core;

namespace SlopCo.Gameplay
{
    /// <summary>
    /// Spawns the roaming hazards (UFO, thief) during a haul and clears them at Payout. Unlike the rats —
    /// which are the price of a specific augment (<see cref="AugmentSystem.SpawnRatsIfNeeded"/>) — these show
    /// up on their own, so every day has interruptions to react to.
    ///
    /// Server-only. Prefabs are OPTIONAL: an unassigned slot simply means that hazard never appears, so the
    /// game runs fine before the art exists. Driven by <see cref="RoundManager"/>; registered in ServiceLocator.
    /// </summary>
    public sealed class HazardDirector : NetworkBehaviour
    {
        [Tooltip("OPTIONAL — flying saucer that abducts a hauler. Null = no UFOs.")]
        [SerializeField] private GameObject ufoPrefab;
        [Tooltip("OPTIONAL — thief that snatches dropped cargo. Null = no thieves.")]
        [SerializeField] private GameObject thiefPrefab;
        [Tooltip("Where roaming hazards enter from. Empty = they arrive at the play-area edge.")]
        [SerializeField] private Transform[] entryPoints;

        private readonly List<NetworkObject> _alive = new();
        private bool _running;
        private float _nextSpawn;
        private int _spawnCount;

        public override void OnNetworkSpawn() => ServiceLocator.Register(this);

        public override void OnNetworkDespawn()
        {
            ClearHazards();
            if (ServiceLocator.Get<HazardDirector>() == this) ServiceLocator.Unregister<HazardDirector>();
        }

        /// <summary>SERVER. Start the hazard cadence for this haul (RoundManager calls it at Hauling).</summary>
        public void BeginHaul()
        {
            if (!IsServer) return;
            _running = true;
            _nextSpawn = GameConstants.HazardFirstDelay;
            _spawnCount = 0;
        }

        /// <summary>SERVER. Stop spawning and remove anything still roaming (Payout / game over).</summary>
        public void ClearHazards()
        {
            if (!IsServer) return;
            _running = false;
            foreach (var h in _alive)
                if (h != null && h.IsSpawned) h.Despawn(true);
            _alive.Clear();
        }

        private void Update()
        {
            if (!IsServer || !_running) return;

            _alive.RemoveAll(h => h == null || !h.IsSpawned);
            if (_alive.Count >= GameConstants.HazardMaxAlive) return;

            _nextSpawn -= Time.deltaTime;
            if (_nextSpawn > 0f) return;
            _nextSpawn = GameConstants.HazardInterval;

            // Alternate the two so a day never turns into all-UFOs; fall through when one has no prefab.
            bool wantUfo = (_spawnCount % 2) == 0;
            var prefab = wantUfo ? (ufoPrefab != null ? ufoPrefab : thiefPrefab)
                                 : (thiefPrefab != null ? thiefPrefab : ufoPrefab);
            if (prefab == null) { _running = false; return; }   // no hazard art wired — stop asking
            _spawnCount++;

            bool isUfo = prefab == ufoPrefab;
            Vector3 pos = EntryPosition(isUfo);
            var no = NetworkObject.InstantiateAndSpawn(
                prefab, NetworkManager.Singleton,
                ownerClientId: NetworkManager.ServerClientId,
                destroyWithScene: true, isPlayerObject: false,
                position: pos, rotation: Quaternion.identity);
            if (no != null) _alive.Add(no);
        }

        // Hazards arrive from off-stage: a UFO drops out of the sky, a thief walks in from the perimeter.
        private Vector3 EntryPosition(bool fromTheSky)
        {
            Vector3 ground;
            if (entryPoints != null && entryPoints.Length > 0)
            {
                var t = entryPoints[Random.Range(0, entryPoints.Length)];
                ground = t != null ? t.position : Vector3.zero;
            }
            else
            {
                float a = Random.Range(0f, Mathf.PI * 2f);
                float r = GameConstants.PlayAreaRadius * 0.7f;
                ground = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
            }
            return fromTheSky ? ground + Vector3.up * GameConstants.UfoCruiseHeight : ground + Vector3.up * 0.5f;
        }
    }
}
