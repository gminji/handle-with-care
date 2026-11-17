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
        public ObstacleSet obstacles; // props for this map's obstacle slots; empty = the four default primitives
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

        /// <summary>Root of the map currently in play (null before the first apply). The bot NavMesh is baked
        /// from its colliders.</summary>
        public Transform ActiveRoot { get; private set; }

        /// <summary>Bumps on every apply — a new map, van dock or obstacle field. <see cref="SlopCo.Player.BotNavMesh"/>
        /// rebakes when it moves.</summary>
        public int LayoutVersion { get; private set; }

        public override void OnNetworkSpawn()
        {
            ServiceLocator.Register(this);
            ActiveMap.OnValueChanged += HandleMapChanged;
            LayoutSeed.OnValueChanged += HandleLayoutChanged;
            if (IsServer)
            {
                ActiveMap.Value = ResolveInitialMap();
                RollLayout();               // applies the accepted layout
            }
            else Apply(ActiveMap.Value);    // initial apply (also covers late-join clients)
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
            RollLayout();
        }

        private static int NewSeed() => UnityEngine.Random.Range(1, int.MaxValue);

        /// <summary>Seeds the host tries before accepting a layout it could not prove deliverable. Roughly one
        /// raised-dock roll in eight blocks the route (measured over 500 seeds), so eight tries leave a blocked run
        /// at about one in ten million.</summary>
        public const int MaxLayoutTries = 8;

        /// <summary>SERVER. Roll the run's layout and keep only one whose obstacle field leaves every depot spawn
        /// a walkable route to the van (<see cref="MapLayout.PickSeed"/>). Each candidate is applied and checked
        /// within this frame; only the accepted seed reaches the clients, so they still rebuild it from one int.</summary>
        private void RollLayout()
        {
            bool ok = MapLayout.TryPickSeed(NewSeed, TryLayout, MaxLayoutTries, out int seed, out int tries);
            LayoutRerolls = tries - 1;
            if (!ok)
                Debug.LogWarning($"[MapManager] No deliverable layout in {tries} seeds on {MapNameKey(ActiveMap.Value)}; keeping seed {seed}.");
            else if (tries > 1)
                Debug.Log($"[MapManager] Layout re-rolled {tries - 1}x (seed {seed}): the obstacle field cut the depot off from the van.");
        }

        /// <summary>SERVER. How many candidate seeds the last roll threw away (shown by the debug overlay).</summary>
        public int LayoutRerolls { get; private set; }

        private bool TryLayout(int seed)
        {
            LayoutSeed.Value = seed;
            Apply(ActiveMap.Value);   // same-seed and same-map rolls don't fire OnValueChanged, so apply explicitly
            return LayoutIsDeliverable();
        }

        /// <summary>SERVER. Does every depot spawn of the applied layout reach the van? True when the map has
        /// nothing to check (no depot or dock authored).</summary>
        private bool LayoutIsDeliverable()
        {
            if (ActiveRoot == null) return true;
            var depot = ActiveRoot.Find("DepotSpawns");
            var dock = PickVanAnchor(ActiveRoot);
            if (depot == null || depot.childCount == 0 || dock == null) return true;
            var starts = new Vector3[depot.childCount];
            for (int i = 0; i < starts.Length; i++) starts[i] = depot.GetChild(i).position;
            return SlopCo.Player.BotNavMesh.RouteExists(ActiveRoot, starts, dock.position);
        }

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

            LayoutVersion++;
            var m = maps[idx];
            if (m == null || m.root == null) { ActiveRoot = null; return; }
            var rootT = m.root.transform;
            ActiveRoot = rootT;

            var vanAnchor = PickVanAnchor(rootT);
            if (van != null && vanAnchor != null)
                van.SetPositionAndRotation(vanAnchor.position, vanAnchor.rotation);

            BuildObstacles(rootT, m.obstacles);

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
        private void BuildObstacles(Transform rootT, ObstacleSet set) =>
            BuildObstacleField(rootT, rootT.Find("ObstacleSlots"), LayoutSeed.Value, obstacleDensity, obstacleMaterial, set);

        /// <summary>Share of obstacle slots that fill each run, in percent (scene-tuned).</summary>
        public int ObstacleDensity => obstacleDensity;

        /// <summary>The obstacle list of the map whose root is <paramref name="mapRoot"/>, or null (no such map, or
        /// it uses the default primitives).</summary>
        public ObstacleSet ObstacleSetFor(Transform mapRoot)
        {
            if (maps == null || mapRoot == null) return null;
            foreach (var m in maps)
                if (m != null && m.root != null && m.root.transform == mapRoot) return m.obstacles;
            return null;
        }

        /// <summary>
        /// Build the obstacle field <paramref name="seed"/> puts in <paramref name="slots"/>, under a fresh
        /// "ObstaclesRuntime" holder parented to <paramref name="parent"/> (replacing any previous holder there).
        /// Static and scene-free so the EditMode tests can rebuild exactly the field a given seed produces in play.
        /// Returns the holder, or null when there are no slots.
        /// </summary>
        public static GameObject BuildObstacleField(Transform parent, Transform slots, int seed, int density, Material material,
                                                    ObstacleSet set = null)
        {
            // Remove EVERY previous holder and make it unfindable at once. Destroy() only lands at the end of the
            // frame, but the host can apply several times in ONE frame (ActiveMap/LayoutSeed OnValueChanged fire
            // synchronously on the writer, the layout roll tries several seeds): Find() kept returning the same
            // dying holder, so a stale-seed layout survived next to the new one.
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var old = parent.GetChild(i);
                if (old.name != ObstacleRootName) continue;
                old.gameObject.SetActive(false);   // stops rendering/colliding this instant
                old.SetParent(null, false);        // a later same-frame apply can no longer see it
                DestroyImmediateSafe(old.gameObject);
            }

            if (slots == null || slots.childCount == 0) return null;

            var holder = new GameObject(ObstacleRootName);
            holder.transform.SetParent(parent, false);
            holder.hideFlags = HideFlags.DontSave;   // runtime-only: never leaks into the saved scene

            if (set != null && set.HasEntries)
            {
                for (int i = 0; i < slots.childCount; i++)
                {
                    if (!MapLayout.SlotActive(seed, i, density)) continue;
                    BuildListedProp(holder.transform, slots.GetChild(i), seed, i, set);
                }
                return holder;
            }

            for (int i = 0; i < slots.childCount; i++)
            {
                if (!MapLayout.SlotActive(seed, i, density)) continue;
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

                if (material != null)
                {
                    var r = go.GetComponent<Renderer>();
                    if (r != null) r.sharedMaterial = material;
                }
            }
            return holder;
        }

        // Footprints chosen so no single obstacle can fully plug the 1.5-wide bridge: everything is at most ~1.1
        // across at base scale, and the slots are authored clear of the gate and bridge centreline. Two NEIGHBOURING
        // slots can still close a platform lane between them (a scaled-up crate and barrel leave ~0.78 m against
        // the 0.8 m capsule) — that is what RollLayout's route check rejects.
        private static Vector3 SizeOf(ObstacleKind kind) => kind switch
        {
            ObstacleKind.Crate   => new Vector3(0.9f, 0.9f, 0.9f),
            ObstacleKind.Barrel  => new Vector3(0.8f, 0.55f, 0.8f),   // cylinder: y is half-height
            ObstacleKind.Cone    => new Vector3(0.45f, 0.4f, 0.45f),
            _                    => new Vector3(1.1f, 0.5f, 0.35f),   // low barrier — hop it or go around
        };

        // One prop from the map's list in this slot. Sized so its longest horizontal side is the entry's footprint
        // (≤ 1.1 m, the same rule as the primitives) times the seeded size roll, stood on the slot, and made solid
        // for the route check. Models arrive in any unit and pivot (FBX roots can carry X -90° and scale 100), so the
        // root's own rotation and scale are kept and everything is measured from the meshes.
        private static void BuildListedProp(Transform holder, Transform slot, int seed, int i, ObstacleSet set)
        {
            var entry = set.Get(MapLayout.EntryFor(seed, i, set.Count));
            if (entry == null || entry.prefab == null)
            {
                Debug.LogWarning($"[MapManager] Obstacle list '{set.name}' has an empty entry; slot {i} stays clear.");
                return;
            }

            var go = UnityEngine.Object.Instantiate(entry.prefab, holder, false);
            go.name = entry.prefab.name + "_" + i;
            var t = go.transform;
            Quaternion rot0 = t.localRotation;
            Vector3 scale0 = t.localScale;

            // ① in the root's own space (for a fallback BoxCollider); ② upright: root rotation and scale applied, no
            // yaw, in holder axes (for sizing and standing it on the slot). Read from the meshes, not the renderers,
            // whose bounds may not have caught up with a transform set this frame.
            var upright = Matrix4x4.TRS(Vector3.zero, rot0, scale0);
            var toRoot = t.worldToLocalMatrix;
            bool any = false;
            Bounds local = default, stood = default;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>(true))
                if (ActiveUnder(mf.transform, t)) Encapsulate(mf.sharedMesh, toRoot * mf.transform.localToWorldMatrix, upright, ref any, ref local, ref stood);
            foreach (var sk in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (ActiveUnder(sk.transform, t)) Encapsulate(sk.sharedMesh, toRoot * sk.transform.localToWorldMatrix, upright, ref any, ref local, ref stood);

            float side = any ? Mathf.Max(stood.size.x, stood.size.z) : 0f;
            if (side <= 1e-6f)
            {
                Debug.LogWarning($"[MapManager] Obstacle '{entry.prefab.name}' has no mesh to measure; slot {i} stays clear.");
                go.SetActive(false);   // out of this frame's route check even when the destroy is deferred
                DestroyImmediateSafe(go);
                return;
            }

            float f = Mathf.Min(entry.footprint, ObstacleSet.MaxFootprint) / side * MapLayout.ScaleFor(seed, i, 0.7f, 1.35f);
            var yaw = Quaternion.Euler(0f, MapLayout.YawFor(seed, i), 0f);
            t.localScale = scale0 * f;
            t.localRotation = yaw * rot0;
            // Yaw turns about Y, so the scaled upright box keeps its floor; centre it on the slot and stand it there.
            Vector3 c = yaw * (stood.center * f);
            Vector3 at = holder.InverseTransformPoint(slot.position);
            t.localPosition = new Vector3(at.x - c.x, at.y - stood.min.y * f, at.z - c.z);

            // The host bakes the route check in this same frame, and the bake skips bodies with a Rigidbody (they
            // move). A deferred Destroy would land too late, so strip them now, joints first (they require bodies).
            foreach (var j in go.GetComponentsInChildren<Joint>(true)) UnityEngine.Object.DestroyImmediate(j);
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(rb);
            if (go.GetComponentInChildren<Rigidbody>(true) != null)
                Debug.LogWarning($"[MapManager] Obstacle '{go.name}' kept a Rigidbody; the route check will not see it.");

            bool solid = false;
            foreach (var col in go.GetComponentsInChildren<Collider>(true))
                if (IsUsableCollider(col, t)) { solid = true; break; }
            if (!solid)
            {
                var box = go.AddComponent<BoxCollider>();
                box.center = local.center;
                box.size = local.size;
            }
        }

        private static void Encapsulate(Mesh mesh, Matrix4x4 toRoot, Matrix4x4 upright, ref bool any, ref Bounds local, ref Bounds stood)
        {
            if (mesh == null) return;
            var b = mesh.bounds;
            Vector3 min = b.min, max = b.max;
            for (int k = 0; k < 8; k++)
            {
                var p = new Vector3((k & 1) == 0 ? min.x : max.x, (k & 2) == 0 ? min.y : max.y, (k & 4) == 0 ? min.z : max.z);
                Vector3 r = toRoot.MultiplyPoint3x4(p);
                Vector3 u = upright.MultiplyPoint3x4(r);
                if (!any) { local = new Bounds(r, Vector3.zero); stood = new Bounds(u, Vector3.zero); any = true; }
                else { local.Encapsulate(r); stood.Encapsulate(u); }
            }
        }

        // Active within the prop itself (the holder's own state doesn't matter: the field is built before it shows).
        private static bool ActiveUnder(Transform t, Transform root)
        {
            for (; t != null; t = t.parent)
            {
                if (!t.gameObject.activeSelf) return false;
                if (t == root) return true;
            }
            return true;
        }

        // A collider the route check's bake can stand on: enabled, solid, not a player, and — for a MeshCollider —
        // a readable mesh, because the runtime NavMesh bake in a player build drops unreadable meshes and would
        // then wave a blocked layout through.
        private static bool IsUsableCollider(Collider col, Transform root)
        {
            if (!col.enabled || col.isTrigger || col is CharacterController || !ActiveUnder(col.transform, root)) return false;
            if (col is MeshCollider mc) return mc.sharedMesh != null && mc.sharedMesh.isReadable;
            return true;
        }

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
