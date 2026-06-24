using UnityEngine;

namespace SlopCo.GameAssets
{
    /// <summary>
    /// Typed references to the networked prefabs spawners need. These same prefabs MUST also be added
    /// to the NetworkManager's Network Prefabs list (NGO requirement for spawning). This SO just lets
    /// gameplay code reference them without scene-wiring every spawner.
    /// </summary>
    [CreateAssetMenu(fileName = "NetworkPrefabRegistry", menuName = "SlopCo/Network Prefab Registry", order = 1)]
    public sealed class NetworkPrefabRegistry : ScriptableObject
    {
        [Tooltip("Player prefab: must have NetworkObject + ClientNetworkTransform + PlayerController.")]
        public GameObject playerPrefab;

        [Tooltip("Cargo prefabs: each must have NetworkObject + NetworkTransform(Server) + NetworkRigidbody + CargoItem + CargoCondition.")]
        public GameObject[] cargoPrefabs = new GameObject[0];

        public bool HasCargo => cargoPrefabs != null && cargoPrefabs.Length > 0;
    }
}
