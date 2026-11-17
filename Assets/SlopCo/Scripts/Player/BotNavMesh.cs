using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using SlopCo.Core;
using SlopCo.Gameplay;

namespace SlopCo.Player
{
    /// <summary>
    /// Host-only NavMesh for AI bots, baked at runtime from the ACTIVE map's colliders. The maps change per run
    /// (map pick, van dock, seeded obstacle field — see <see cref="MapManager"/>), so nothing is baked into the
    /// scene: whenever <see cref="MapManager.LayoutVersion"/> moves, this rebakes once the layout has settled.
    /// Bots only think on the host (<see cref="AiBrain"/> runs for the owner), so clients never build one.
    /// Created on demand by the first bot (<see cref="Get"/>) and lives in the scene like any runtime object.
    /// </summary>
    public sealed class BotNavMesh : MonoBehaviour
    {
        // Player.prefab's CharacterController: radius 0.4, height 1.8, slope limit 45°.
        public const float AgentRadius = 0.4f;
        public const float AgentHeight = 1.8f;
        public const float AgentSlope = 45f;

        /// <summary>The tallest ledge a route may cross. The controller only STEPS 0.3 by itself, but a blocked bot
        /// hops (<see cref="AiBrain"/>'s unstuck jump; apex = PlayerJumpSpeed² / 2·|Gravity| ≈ 0.69), and the maps
        /// count on that: the last-yard ledge in front of the raised van dock is 0.56 high by design. Baked with
        /// the bare step offset, that dock fell off the mesh and the partial route ended on the ground beside the
        /// dock wall. 0.6 keeps a margin under the apex; anything taller (platform sides at 1.05, crates) stays a
        /// wall the route goes around.</summary>
        public const float AgentClimb = 0.6f;

        /// <summary>How far a bot or a goal may sit off the mesh and still snap onto it (a cargo handle in a
        /// carrier's hands, the van's delivery zone inside the truck, a bot mid-jump).</summary>
        public const float SnapRadius = 3f;

        // Margin around the map's own geometry: the shared Ground plane extends well past every course, and
        // a bot shoved off the edge of the course should still find its way back.
        private static readonly Vector3 BoundsMargin = new Vector3(8f, 6f, 8f);

        private static BotNavMesh _instance;

        private NavMeshData _data;
        private NavMeshDataInstance _handle;
        private int _bakedLayout = int.MinValue;
        private int _seenLayout = int.MinValue;
        private int _seenFrame;
        private readonly List<NavMeshBuildSource> _sources = new List<NavMeshBuildSource>();
        private readonly List<NavMeshBuildMarkup> _markups = new List<NavMeshBuildMarkup>();

        /// <summary>True once a mesh for the current map is live. Until then bots steer straight at the goal.</summary>
        public bool Ready => _handle.valid;

        /// <summary>Bumps on every bake, so a route planned on the previous map/layout is known to be stale.</summary>
        public int Version { get; private set; }

        /// <summary>The live instance if a bot has asked for one, without creating it (debug overlay).</summary>
        public static BotNavMesh Current => _instance;

        public static BotNavMesh Get()
        {
            if (_instance == null) _instance = new GameObject("BotNavMesh").AddComponent<BotNavMesh>();
            return _instance;
        }

        private void OnDestroy()
        {
            if (_handle.valid) _handle.Remove();
            if (_data != null) Destroy(_data);
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return;
            var mm = ServiceLocator.Get<MapManager>();
            if (mm == null || mm.ActiveRoot == null) return;

            int layout = mm.LayoutVersion;
            if (layout == _bakedLayout) return;
            // The host can apply the map several times in ONE frame (map change, re-seed, explicit re-apply), and
            // the previous obstacle field is only destroyed at the end of that frame. Wait for a frame in which the
            // version held still so the bake sees the final van dock and obstacles, not an intermediate field.
            if (layout != _seenLayout) { _seenLayout = layout; _seenFrame = Time.frameCount; return; }
            if (Time.frameCount <= _seenFrame) return;

            _bakedLayout = layout;   // even if the bake fails: retrying every frame would only repeat the failure
            Bake(mm.ActiveRoot);
        }

        private void Bake(Transform mapRoot)
        {
            Physics.SyncTransforms();   // the van and the new obstacles were moved/created this run
            Bounds bounds = MapBounds(mapRoot);
            CollectLevelSources(bounds, _markups, _sources);
            if (_sources.Count == 0)
            {
                Debug.LogWarning($"[BotNavMesh] No level colliders under {mapRoot.name}; bots will steer straight at their goals.");
                return;
            }

            var settings = Settings();
            if (_data == null)
            {
                _data = NavMeshBuilder.BuildNavMeshData(settings, _sources, bounds, Vector3.zero, Quaternion.identity);
                if (_data != null) _handle = NavMesh.AddNavMeshData(_data);
            }
            else if (!NavMeshBuilder.UpdateNavMeshData(_data, settings, _sources, bounds))
            {
                Debug.LogWarning($"[BotNavMesh] Rebake failed for {mapRoot.name}; bots keep the previous mesh.");
                return;
            }

            if (_handle.valid) Version++;
        }

        /// <summary>The default (Humanoid) agent type, resized to the player's CharacterController.</summary>
        public static NavMeshBuildSettings Settings()
        {
            var s = NavMesh.GetSettingsByID(0);
            s.agentRadius = AgentRadius;
            s.agentHeight = AgentHeight;
            s.agentClimb = AgentClimb;
            s.agentSlope = AgentSlope;
            return s;
        }

        /// <summary>Every solid, static collider inside <paramref name="bounds"/> — see
        /// <see cref="BotPathMath.IsBakeSource"/> for what is left out (players, cargo, triggers).</summary>
        public static void CollectLevelSources(Bounds bounds, List<NavMeshBuildMarkup> markups, List<NavMeshBuildSource> results)
        {
            NavMeshBuilder.CollectSources(bounds, ~0, NavMeshCollectGeometry.PhysicsColliders, 0, markups, results);
            results.RemoveAll(s => s.component is Collider c
                                   && !BotPathMath.IsBakeSource(c.isTrigger, c.attachedRigidbody != null, c is CharacterController));
        }

        /// <summary>How far a route's end may land from the point it was asked for and still count as reaching
        /// it. Beyond this, SamplePosition has quietly picked somewhere else — the ground beside the raised dock
        /// (2.6 m away, 1.6 m down) instead of its top — and a "complete" route would lead to the wrong place.
        /// Depot points sit up to one agent radius inside an eroded edge; spawn points float up to 1 m up.</summary>
        public const float RouteSnapFlat = 0.8f;
        public const float RouteSnapUp = 1.2f;

        private static int _checkAgentType = -1;

        /// <summary>
        /// Can a crew walk from every point in <paramref name="from"/> to <paramref name="to"/> on
        /// <paramref name="mapRoot"/> as it stands right now (obstacles included)? Bakes a throwaway mesh under a
        /// private agent type so the bots' live mesh (agent type 0, possibly of the previous layout) can neither
        /// answer the query nor be disturbed by it. Host-only, a few milliseconds, once per rolled layout
        /// (<see cref="MapManager"/>). Returns true when there is nothing to judge by (no geometry, bake failure),
        /// so a broken check can never stall a run start.
        /// </summary>
        public static bool RouteExists(Transform mapRoot, Vector3[] from, Vector3 to)
        {
            Physics.SyncTransforms();   // the van and the new obstacles were moved/created this frame
            Bounds bounds = MapBounds(mapRoot);
            var sources = new List<NavMeshBuildSource>();
            CollectLevelSources(bounds, new List<NavMeshBuildMarkup>(), sources);
            if (sources.Count == 0) return true;

            var settings = CheckSettings();
            var data = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (data == null) return true;
            var handle = NavMesh.AddNavMeshData(data);
            try
            {
                if (!handle.valid) return true;
                var filter = new NavMeshQueryFilter { agentTypeID = settings.agentTypeID, areaMask = NavMesh.AllAreas };
                if (!SnapOnto(to, filter, out var end)) return false;
                var path = new NavMeshPath();
                foreach (var start in from)
                {
                    if (!SnapOnto(start, filter, out var begin)) return false;
                    if (!NavMesh.CalculatePath(begin, end, filter, path) || path.status != NavMeshPathStatus.PathComplete)
                        return false;
                }
                return true;
            }
            finally
            {
                if (handle.valid) handle.Remove();
                if (Application.isPlaying) Destroy(data); else DestroyImmediate(data);
            }
        }

        private static bool SnapOnto(Vector3 point, NavMeshQueryFilter filter, out Vector3 onMesh)
        {
            onMesh = point;
            if (!NavMesh.SamplePosition(point, out var hit, SnapRadius, filter)) return false;
            onMesh = hit.position;
            return BotPathMath.IsOnPoint(point, hit.position, RouteSnapFlat, RouteSnapUp);
        }

        // Same agent dimensions as Settings(), under an agent type of its own (created once per domain).
        private static NavMeshBuildSettings CheckSettings()
        {
            NavMeshBuildSettings s;
            if (_checkAgentType < 0) { s = NavMesh.CreateSettings(); _checkAgentType = s.agentTypeID; }
            else s = NavMesh.GetSettingsByID(_checkAgentType);
            var bot = Settings();
            s.agentRadius = bot.agentRadius;
            s.agentHeight = bot.agentHeight;
            s.agentClimb = bot.agentClimb;
            s.agentSlope = bot.agentSlope;
            return s;
        }

        /// <summary>The map's footprint: its solid colliders plus every authored point under it (spawns, depot,
        /// van docks), grown by a margin.</summary>
        public static Bounds MapBounds(Transform mapRoot)
        {
            var b = new Bounds(mapRoot.position, Vector3.zero);
            foreach (var c in mapRoot.GetComponentsInChildren<Collider>(false))
                if (!c.isTrigger) b.Encapsulate(c.bounds);
            foreach (var t in mapRoot.GetComponentsInChildren<Transform>(false))
                b.Encapsulate(t.position);
            b.Expand(BoundsMargin * 2f);
            return b;
        }
    }
}
