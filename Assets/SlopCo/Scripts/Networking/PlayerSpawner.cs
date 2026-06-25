using Unity.Netcode;
using UnityEngine;
using SlopCo.Core;
using SlopCo.GameAssets;
using SlopCo.Player;

namespace SlopCo.Networking
{
    /// <summary>
    /// Server-only player spawning. On each client connect, instantiates the player prefab and calls
    /// <c>NetworkObject.SpawnAsPlayerObject(clientId)</c> (the correct NGO 2.x instance method), then
    /// assigns a unique color index for spectator readability. Approval sets CreatePlayerObject=false
    /// so this is the only spawn path.
    /// </summary>
    public sealed class PlayerSpawner : MonoBehaviour
    {
        [SerializeField] private NetworkPrefabRegistry registry;
        [SerializeField] private Transform[] spawnPoints;

        private int _spawnCount;

        /// <summary>MapManager. Rebind the player spawn anchors when the active map changes.</summary>
        public void SetSpawnPoints(Transform[] points) => spawnPoints = points;

        private void Start()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null) { Debug.LogError("[PlayerSpawner] No NetworkManager."); return; }
            nm.OnClientConnectedCallback += HandleClientConnected;
        }

        private void OnDestroy()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null) nm.OnClientConnectedCallback -= HandleClientConnected;
        }

        private void HandleClientConnected(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return; // only the server spawns players
            SpawnPlayer(clientId);

            // Co-op-with-AI: once the host's own player is in, spawn the AI teammates alongside it.
            if (clientId == NetworkManager.ServerClientId && GameModeState.WithAi)
                SpawnBots(Mathf.Clamp(GameModeState.BotCount, 1, GameConstants.MaxPlayers - 1));
        }

        /// <summary>SERVER. Spawn AI teammates as server-owned player instances (not player-objects).</summary>
        private void SpawnBots(int count)
        {
            if (registry == null || registry.playerPrefab == null) return;
            for (int i = 0; i < count; i++)
            {
                (Vector3 pos, Quaternion rot) = NextSpawn();
                GameObject go = Instantiate(registry.playerPrefab, pos, rot);

                var pc = go.GetComponent<PlayerController>();
                if (pc != null) pc.MarkAsBot(); // before Spawn so OnNetworkSpawn wires the AiBrain

                var netObj = go.GetComponent<NetworkObject>();
                if (netObj == null) { Destroy(go); continue; }
                netObj.Spawn(); // server-owned (host owns + drives it); NOT a client player-object

                if (pc != null) pc.ColorIndex.Value = _spawnCount % GameConstants.MaxPlayers;
                _spawnCount++;
            }
        }

        private void SpawnPlayer(ulong clientId)
        {
            if (registry == null || registry.playerPrefab == null)
            {
                Debug.LogError("[PlayerSpawner] Missing NetworkPrefabRegistry / playerPrefab.");
                return;
            }

            (Vector3 pos, Quaternion rot) = NextSpawn();
            GameObject go = Instantiate(registry.playerPrefab, pos, rot);

            var netObj = go.GetComponent<NetworkObject>();
            if (netObj == null)
            {
                Debug.LogError("[PlayerSpawner] Player prefab missing NetworkObject.");
                Destroy(go);
                return;
            }

            netObj.SpawnAsPlayerObject(clientId); // NGO 2.x: instance method, server-only

            // Server assigns color (NetworkVariable is server-write) for instant who-is-who readability.
            var controller = go.GetComponent<PlayerController>();
            if (controller != null)
                controller.ColorIndex.Value = _spawnCount % GameConstants.MaxPlayers;

            _spawnCount++;
        }

        private (Vector3, Quaternion) NextSpawn()
        {
            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                var t = spawnPoints[_spawnCount % spawnPoints.Length];
                if (t != null) return (t.position, t.rotation);
            }
            // Fallback: small ring around the origin so players don't stack.
            float ang = _spawnCount * 1.25f;
            return (new Vector3(Mathf.Cos(ang) * 2f, 1f, Mathf.Sin(ang) * 2f), Quaternion.identity);
        }
    }
}
