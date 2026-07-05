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

        [Header("Bomb Mode (the hook)")]
        [Tooltip("When on, rounds spawn live-bomb cargo to deliver before the fuse blows, instead of random cargo.")]
        [SerializeField] private bool bombMode = true;
        [SerializeField] private GameObject bombPrefab;
        [Tooltip("Day-1 fuse burn rate; escalates each surviving day.")]
        [SerializeField] private float baseFuseDecayPerSecond = 0.04f;

        /// <summary>True while the game runs as bomb-delivery mode.</summary>
        public bool BombMode => bombMode;
        /// <summary>Number of deliverables spawned this round (the round is won when all are delivered).</summary>
        public int LastSpawnCount { get; private set; }

        private readonly List<NetworkObject> _spawned = new();
        private readonly System.Random _rng = new System.Random(12345); // deterministic for repeatable tests

        /// <summary>MapManager. Rebind the depot spawn anchors when the active map changes.</summary>
        public void SetDepotSpawnPoints(Transform[] points) => depotSpawnPoints = points;

        /// <summary>SERVER. Spawn this round's deliverable(s) at the depot.</summary>
        public void SpawnRoundCargo()
        {
            if (!IsServer) return;
            if (bombMode) { SpawnBombs(); return; }
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

        /// <summary>SERVER. Spawn this day's live bomb(s); fuse + count escalate each surviving day.</summary>
        private void SpawnBombs()
        {
            ClearRemainingCargo();
            LastSpawnCount = 0;
            if (bombPrefab == null) { Debug.LogError("[CargoSpawner] BombMode on but bombPrefab unassigned."); return; }

            int day = 1;
            var rm = ServiceLocator.Get<RoundManager>();
            if (rm != null) day = Mathf.Max(1, rm.RoundNumber.Value);
            // Solo: never more than one bomb (you only have two hands) — only the fuse escalates.
            int count = GameModeState.Solo ? 1 : 1 + (day - 1) / 3;          // +1 bomb every 3 days (co-op)
            float decay = baseFuseDecayPerSecond * (1f + 0.2f * (day - 1)); // fuse burns faster each day
            decay *= ServiceLocator.Get<AugmentSystem>()?.FuseMult ?? 1f;     // augments can speed the fuse
            // Today's modifier (deterministic from the day — no extra netcode). Rush Hour shortens the fuse,
            // Double Load adds a bomb (co-op only — solo has just two hands).
            var dayMod = DailyModifier.Roll(day);
            decay *= DailyModifier.FuseMult(dayMod);
            if (!GameModeState.Solo) count += DailyModifier.CountBonus(dayMod);
            if (GameModeState.Tutorial) decay = 0f;                          // calm fuse while learning

            for (int i = 0; i < count; i++)
            {
                (Vector3 pos, Quaternion rot) = SpawnPose(i);
                var netObj = NetworkObject.InstantiateAndSpawn(
                    bombPrefab, NetworkManager.Singleton, ownerClientId: NetworkManager.ServerClientId,
                    destroyWithScene: true, isPlayerObject: false, position: pos, rotation: rot);
                if (netObj == null) continue;
                _spawned.Add(netObj);
                // This bomb's archetype (deterministic per day+index): a round gets a MIX of Volatile (short fuse,
                // grab-and-run), Slippery, Heavy and Standard instead of N identical bombs. Escalates by day.
                var arch = SlopCo.Cargo.CargoArchetypeTable.Roll(day, i);
                var bomb = netObj.GetComponent<SlopCo.Cargo.CargoBomb>();
                if (bomb != null)
                {
                    bomb.Arm(decay * SlopCo.Cargo.CargoArchetypeTable.FuseMult(arch), DailyModifier.BlastMult(dayMod));
                    // Surface = the slipperier of today's modifier and this bomb's archetype (MIN) so Greasy Day and
                    // a Slippery bomb don't double-slip; bounce stays day-driven.
                    float friction = Mathf.Min(DailyModifier.FrictionMult(dayMod), SlopCo.Cargo.CargoArchetypeTable.FrictionMult(arch));
                    bomb.SetSurface(friction, DailyModifier.Bounciness(dayMod));
                    bomb.SetArchetype(arch); // replicate the tint/scale telegraph to clients
                }
                // Mass override only for Volatile (OneHand) / Heavy (TwoPerson); Standard & Slippery inherit the
                // prefab's shipped mass (the bomb is TwoPerson) so co-op is never regressed.
                var massOverride = SlopCo.Cargo.CargoArchetypeTable.MassOverride(arch);
                if (massOverride.HasValue)
                {
                    var item = netObj.GetComponent<SlopCo.Cargo.CargoItem>();
                    if (item != null) item.SetMassClass(massOverride.Value);
                }
                LastSpawnCount++;
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
