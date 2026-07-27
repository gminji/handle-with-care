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
    ///   "VanAnchor"    (the pose the shared Van is moved to), and optionally
    ///   "VanAnchors"   (several candidate docks — the run picks one, so the haul length varies), and
    ///   "ObstacleSlots" (candidate junk positions — a seeded subset is filled each run).
    ///
    /// Per-run variety rides on a single replicated <see cref="LayoutSeed"/>: every client feeds it to the
    /// pure <see cref="MapLayout"/> and rebuilds the identical dock choice and obstacle field, so the
    /// variety costs one int on the wire (the same trick <see cref="DailyModifier"/> plays with the day).
    /// Registered in ServiceLocator.
    /// </summary>
    public sealed class MapManager : NetworkBehaviour
    {
        [SerializeField] private MapDefinition[] maps;
        [SerializeField] private CargoSpawner cargoSpawner;
        [SerializeField] private PlayerSpawner playerSpawner;
        [SerializeField] private Transform van;
        [Header("Per-run obstacles")]
        [Tooltip("Material for the generated obstacles. Unassigned = Unity's default (visible, just untinted).")]
        [SerializeField] private Material obstacleMaterial;
        [Tooltip("Roughly what share of the candidate slots fill each run, in percent.")]
        [Range(0, 100)][SerializeField] private int obstacleDensity = 55;

        public readonly NetworkVariable<int> ActiveMap =
            new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>Server-rolled per-run layout seed: which van dock, and which obstacles are out.</summary>
        public readonly NetworkVariable<int> LayoutSeed =
            new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        /// <summary>Live map-vote tally, packed by <see cref="AugmentOffer"/> (slot = map index).</summary>
        public readonly NetworkVariable<int> MapVotesPacked =
            new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private readonly System.Collections.Generic.Dictionary<ulong, int> _mapBallots = new();
        private bool _warnedTooManyMaps;

        private const string ObstacleRootName = "ObstaclesRuntime";

        /// <summary>True while the connected crew should be picking the map together: online only (solo,
        /// co-op-with-AI and the tutorial keep the pick-then-launch menu flow), sitting in the lobby, with a
        /// votable number of maps. Both the shell (which panel to show) and the picker read this, so there
        /// is one definition of "the crew is voting".</summary>
        public bool CrewVoteActive
        {
            get
            {
                if (!IsSpawned) return false;
                if (GameModeState.Solo || GameModeState.WithAi || GameModeState.Tutorial) return false;
                var rm = ServiceLocator.Get<RoundManager>();
                if (rm == null || rm.Phase.Value != RoundPhase.Lobby) return false;
                if (MapCount < 2) return false;
                if (MapCount > AugmentOffer.MaxSlots)
                {
                    if (!_warnedTooManyMaps)
                    {
                        _warnedTooManyMaps = true;
                        Debug.LogWarning($"[MapManager] {MapCount} maps exceeds the {AugmentOffer.MaxSlots}-slot vote tally; " +
                                         "the crew map vote is disabled and the map will be rolled at random.");
                    }
                    return false;
                }
                return true;
            }
        }

        /// <summary>Votes currently cast for a map (0 when none).</summary>
        public int VotesFor(int mapIndex) => Mathf.Max(0, AugmentOffer.Slot(MapVotesPacked.Value, mapIndex));

        /// <summary>SERVER (via client request). Cast or change this client's map vote.</summary>
        [Rpc(SendTo.Server)]
        public void SubmitMapVoteRpc(int mapIndex, RpcParams rpcParams = default)
        {
            if (!CrewVoteActive) return;
            if (mapIndex < 0 || mapIndex >= MapCount || mapIndex >= AugmentOffer.MaxSlots) return;
            _mapBallots[rpcParams.Receive.SenderClientId] = mapIndex;   // re-voting replaces
            PublishMapVotes();
        }

        private void PublishMapVotes()
        {
            var counts = new int[AugmentOffer.MaxSlots];
            foreach (var kv in _mapBallots)
                if (kv.Value >= 0 && kv.Value < counts.Length) counts[kv.Value]++;
            MapVotesPacked.Value = AugmentOffer.Pack(counts, counts.Length);
        }

        public int MapCount => maps != null ? maps.Length : 0;
        public string MapNameKey(int i) => (maps != null && i >= 0 && i < maps.Length) ? maps[i].nameKey : string.Empty;

        public override void OnNetworkSpawn()
        {
            ServiceLocator.Register(this);
            ActiveMap.OnValueChanged += HandleMapChanged;
            LayoutSeed.OnValueChanged += HandleLayoutChanged;
            if (IsServer)
            {
                ActiveMap.Value = ResolveInitialMap();
                LayoutSeed.Value = NewSeed();
            }
            Apply(ActiveMap.Value); // initial apply (also covers late-join clients)
        }

        public override void OnNetworkDespawn()
        {
            ActiveMap.OnValueChanged -= HandleMapChanged;
            LayoutSeed.OnValueChanged -= HandleLayoutChanged;
            if (ServiceLocator.Get<MapManager>() == this) ServiceLocator.Unregister<MapManager>();
        }

        /// <summary>SERVER. Re-pick the map AND re-roll the layout (called on (re)start). Honors
        /// SelectedMap / random. A fresh seed means a new dock distance and a new obstacle field even when
        /// the crew replays the same map.</summary>
        public void RollMap()
        {
            if (!IsServer) return;
            ActiveMap.Value = ResolveInitialMap();
            LayoutSeed.Value = NewSeed();
            Apply(ActiveMap.Value);   // same-map restarts don't fire OnValueChanged, so re-apply explicitly
        }

        private static int NewSeed() => UnityEngine.Random.Range(1, int.MaxValue);

        private int ResolveInitialMap()
        {
            int n = MapCount;
            if (n <= 0) return 0;

            // Online: the crew voted in the lobby, so their ballot beats whatever the menu had selected.
            // Ties go to a seeded random pick, decided here on the server and replicated as ActiveMap.
            if (_mapBallots.Count > 0)
            {
                var counts = new int[AugmentOffer.MaxSlots];
                foreach (var kv in _mapBallots)
                    if (kv.Value >= 0 && kv.Value < counts.Length) counts[kv.Value]++;
                uint seed = (uint)(MapVotesPacked.Value * 2654435761u + (uint)_mapBallots.Count);
                int won = VoteTally.Resolve(counts, seed);
                _mapBallots.Clear();
                PublishMapVotes();
                if (won >= 0 && won < n) return won;
            }

            int sel = GameModeState.SelectedMap;
            return (sel < 0 || sel >= n) ? UnityEngine.Random.Range(0, n) : sel; // -1 / OOB = random
        }

        private void HandleMapChanged(int _, int next) => Apply(next);
        private void HandleLayoutChanged(int _, int __) => Apply(ActiveMap.Value);

        private void Apply(int idx)
        {
            if (maps == null || maps.Length == 0) return;
            idx = Mathf.Clamp(idx, 0, maps.Length - 1);

            for (int i = 0; i < maps.Length; i++)
                if (maps[i] != null && maps[i].root != null) maps[i].root.SetActive(i == idx);

            var m = maps[idx];
            if (m == null || m.root == null) return;
            var rootT = m.root.transform;

            var vanAnchor = PickVanAnchor(rootT);
            if (van != null && vanAnchor != null)
                van.SetPositionAndRotation(vanAnchor.position, vanAnchor.rotation);

            BuildObstacles(rootT);

            var depot = ChildrenOf(rootT, "DepotSpawns");
            if (cargoSpawner != null && depot != null) cargoSpawner.SetDepotSpawnPoints(depot);

            var players = ChildrenOf(rootT, "PlayerSpawns");
            if (playerSpawner != null && players != null) playerSpawner.SetSpawnPoints(players);
        }

        // Which dock the van parks at this run. "VanAnchors" (plural) holds the candidates; a map that only
        // ships the original single "VanAnchor" keeps its fixed distance, so this is backwards compatible.
        private Transform PickVanAnchor(Transform rootT)
        {
            var multi = rootT.Find("VanAnchors");
            if (multi != null && multi.childCount > 0)
                return multi.GetChild(MapLayout.VanAnchorIndex(LayoutSeed.Value, multi.childCount));
            return rootT.Find("VanAnchor");
        }

        // Fill a seeded subset of the map's candidate obstacle slots. Identical on every client (same seed →
        // same result), so these are plain scene props: no NetworkObjects, no spawn traffic. Rebuilt from
        // scratch on every apply, which is also how they get cleaned up when the map or seed changes.
        private void BuildObstacles(Transform rootT)
        {
            var existing = rootT.Find(ObstacleRootName);
            if (existing != null) DestroyImmediateSafe(existing.gameObject);

            var slots = rootT.Find("ObstacleSlots");
            if (slots == null || slots.childCount == 0) return;

            var holder = new GameObject(ObstacleRootName);
            holder.transform.SetParent(rootT, false);
            holder.hideFlags = HideFlags.DontSave;   // runtime-only: never leaks into the saved scene

            int seed = LayoutSeed.Value;
            for (int i = 0; i < slots.childCount; i++)
            {
                if (!MapLayout.SlotActive(seed, i, obstacleDensity)) continue;
                var slot = slots.GetChild(i);
                var kind = MapLayout.KindFor(seed, i);
                float s = MapLayout.ScaleFor(seed, i, 0.7f, 1.35f);

                var go = GameObject.CreatePrimitive(kind == ObstacleKind.Crate ? PrimitiveType.Cube
                                                  : kind == ObstacleKind.Barrier ? PrimitiveType.Cube
                                                  : PrimitiveType.Cylinder);
                go.name = kind + "_" + i;
                go.transform.SetParent(holder.transform, false);
                go.transform.SetPositionAndRotation(slot.position, Quaternion.Euler(0f, MapLayout.YawFor(seed, i), 0f));
                go.transform.localScale = SizeOf(kind) * s;
                go.transform.position += Vector3.up * (go.transform.localScale.y * 0.5f);

                if (obstacleMaterial != null)
                {
                    var r = go.GetComponent<Renderer>();
                    if (r != null) r.sharedMaterial = obstacleMaterial;
                }
            }
        }

        // Footprints chosen so nothing can fully plug the 1.5-wide bridge: everything is at most ~1.1 across
        // at base scale, and the slots themselves are authored clear of the gate and bridge centreline.
        private static Vector3 SizeOf(ObstacleKind kind) => kind switch
        {
            ObstacleKind.Crate   => new Vector3(0.9f, 0.9f, 0.9f),
            ObstacleKind.Barrel  => new Vector3(0.8f, 0.55f, 0.8f),   // cylinder: y is half-height
            ObstacleKind.Cone    => new Vector3(0.45f, 0.4f, 0.45f),
            _                    => new Vector3(1.1f, 0.5f, 0.35f),   // low barrier — hop it or go around
        };

        private static void DestroyImmediateSafe(GameObject go)
        {
            if (Application.isPlaying) Destroy(go); else DestroyImmediate(go);
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
