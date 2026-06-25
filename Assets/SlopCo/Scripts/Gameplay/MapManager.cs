using System;
using Unity.Netcode;
using UnityEngine;
using SlopCo.Core;
using SlopCo.Networking;

namespace SlopCo.Gameplay
{
    /// <summary>
    /// One selectable map: a static geometry root. Spawn anchors are discovered by child-name convention
    /// (see <see cref="MapManager"/>), so adding a map is just duplicating a root — no inspector arrays.
    /// </summary>
    [Serializable]
    public sealed class MapDefinition
    {
        public string nameKey;     // localization key (e.g. "map.0")
        public GameObject root;     // Map_i geometry root (toggled active)
    }

    /// <summary>
    /// Server-authoritative map selection for the single-scene architecture. Each map is a static
    /// (non-networked) geometry root toggled by a replicated index, so only one int crosses the wire.
    /// The host resolves <see cref="GameModeState.SelectedMap"/> (-1 = random) at spawn; every client
    /// (including late-joiners) applies <see cref="ActiveMap"/>: activates the chosen root, then rebinds
    /// the shared Van + cargo/player spawners to that root's anchors.
    ///
    /// Convention — each map root must contain child holders named exactly:
    ///   "PlayerSpawns" (its children = player spawn points),
    ///   "DepotSpawns"  (its children = cargo/bomb spawn points),
    ///   "VanAnchor"    (the pose the shared Van is moved to).
    /// Registered in ServiceLocator.
    /// </summary>
    public sealed class MapManager : NetworkBehaviour
    {
        [SerializeField] private MapDefinition[] maps;
        [SerializeField] private CargoSpawner cargoSpawner;
        [SerializeField] private PlayerSpawner playerSpawner;
        [SerializeField] private Transform van;

        public readonly NetworkVariable<int> ActiveMap =
            new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public int MapCount => maps != null ? maps.Length : 0;
        public string MapNameKey(int i) => (maps != null && i >= 0 && i < maps.Length) ? maps[i].nameKey : string.Empty;

        public override void OnNetworkSpawn()
        {
            ServiceLocator.Register(this);
            ActiveMap.OnValueChanged += HandleMapChanged;
            if (IsServer) ActiveMap.Value = ResolveInitialMap();
            Apply(ActiveMap.Value); // initial apply (also covers late-join clients)
        }

        public override void OnNetworkDespawn()
        {
            ActiveMap.OnValueChanged -= HandleMapChanged;
            if (ServiceLocator.Get<MapManager>() == this) ServiceLocator.Unregister<MapManager>();
        }

        /// <summary>SERVER. Re-pick the map (called on (re)start). Honors SelectedMap / random.</summary>
        public void RollMap()
        {
            if (IsServer) ActiveMap.Value = ResolveInitialMap();
        }

        private int ResolveInitialMap()
        {
            int n = MapCount;
            if (n <= 0) return 0;
            int sel = GameModeState.SelectedMap;
            return (sel < 0 || sel >= n) ? UnityEngine.Random.Range(0, n) : sel; // -1 / OOB = random
        }

        private void HandleMapChanged(int _, int next) => Apply(next);

        private void Apply(int idx)
        {
            if (maps == null || maps.Length == 0) return;
            idx = Mathf.Clamp(idx, 0, maps.Length - 1);

            for (int i = 0; i < maps.Length; i++)
                if (maps[i] != null && maps[i].root != null) maps[i].root.SetActive(i == idx);

            var m = maps[idx];
            if (m == null || m.root == null) return;
            var rootT = m.root.transform;

            var vanAnchor = rootT.Find("VanAnchor");
            if (van != null && vanAnchor != null)
                van.SetPositionAndRotation(vanAnchor.position, vanAnchor.rotation);

            var depot = ChildrenOf(rootT, "DepotSpawns");
            if (cargoSpawner != null && depot != null) cargoSpawner.SetDepotSpawnPoints(depot);

            var players = ChildrenOf(rootT, "PlayerSpawns");
            if (playerSpawner != null && players != null) playerSpawner.SetSpawnPoints(players);
        }

        private static Transform[] ChildrenOf(Transform root, string holderName)
        {
            var holder = root.Find(holderName);
            if (holder == null) return null;
            var arr = new Transform[holder.childCount];
            for (int i = 0; i < holder.childCount; i++) arr[i] = holder.GetChild(i);
            return arr;
        }
    }
}
