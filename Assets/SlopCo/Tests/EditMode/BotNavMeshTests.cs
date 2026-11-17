using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using SlopCo.Core;
using SlopCo.Player;

namespace SlopCo.Tests.EditMode
{
    /// <summary>
    /// Bakes real NavMeshes through the same calls the host makes at runtime (BotNavMesh.CollectLevelSources /
    /// Settings) and checks the routes bots would get. First on small hand-built geometry — a wall with a gap,
    /// ledges a bot can and can't get onto, cargo that must not carve the floor — then on both shipped maps.
    /// </summary>
    public class BotNavMeshTests
    {
        private readonly List<GameObject> _made = new List<GameObject>();
        private NavMeshData _data;
        private NavMeshDataInstance _instance;

        [TearDown]
        public void Cleanup()
        {
            if (_instance.valid) _instance.Remove();
            if (_data != null) Object.DestroyImmediate(_data);
            _data = null;
            foreach (var go in _made) if (go != null) Object.DestroyImmediate(go);
            _made.Clear();
        }

        // The bake collects EVERY collider inside the bounds, including those of whatever scene is open in the
        // editor when the tests run (Bootstrap's Ground sits at y = 0). Build the test geometry far above it so
        // the result never depends on the open scene. Positions below are relative to this origin.
        private static readonly Vector3 Origin = new Vector3(0f, 500f, 0f);

        private GameObject Box(string name, Vector3 center, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.position = Origin + center;
            go.transform.localScale = size;
            _made.Add(go);
            return go;
        }

        private GameObject Floor() => Box("Floor", new Vector3(0f, -0.1f, 0f), new Vector3(24f, 0.2f, 24f));

        private static readonly Bounds TestBounds = new Bounds(Origin, new Vector3(30f, 10f, 30f));

        private void Bake(Bounds bounds)
        {
            Physics.SyncTransforms();
            var sources = new List<NavMeshBuildSource>();
            BotNavMesh.CollectLevelSources(bounds, new List<NavMeshBuildMarkup>(), sources);
            _data = NavMeshBuilder.BuildNavMeshData(BotNavMesh.Settings(), sources, bounds, Vector3.zero, Quaternion.identity);
            Assert.That(_data, Is.Not.Null, "bake failed");
            _instance = NavMesh.AddNavMeshData(_data);
            Assert.That(_instance.valid, Is.True, "baked mesh was not registered");
        }

        private static NavMeshPath Route(Vector3 from, Vector3 to)
        {
            from += Origin;
            to += Origin;
            Assert.That(NavMesh.SamplePosition(from, out var a, BotNavMesh.SnapRadius, NavMesh.AllAreas), Is.True, $"no mesh near {from}");
            Assert.That(NavMesh.SamplePosition(to, out var b, BotNavMesh.SnapRadius, NavMesh.AllAreas), Is.True, $"no mesh near {to}");
            var path = new NavMeshPath();
            NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path);
            return path;
        }

        // ── Hand-built geometry ──

        [Test]
        public void Wall_RouteGoesThroughTheGap()
        {
            Floor();
            // A wall along x = 0 from z = -12 to z = +5; the only way across is the gap at z > 5.
            Box("Wall", new Vector3(0f, 1.5f, -3.5f), new Vector3(0.5f, 3f, 17f));
            Bake(TestBounds);

            var path = Route(new Vector3(-5f, 0f, 0f), new Vector3(5f, 0f, 0f));
            Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete));
            float maxZ = float.MinValue;
            foreach (var c in path.corners) maxZ = Mathf.Max(maxZ, c.z - Origin.z);
            Assert.That(maxZ, Is.GreaterThan(5f), "the route must bend round the end of the wall, not through it");
        }

        [Test]
        public void Platform_TooTallToHop_IsNotRouted()
        {
            Floor();
            // 1.0 m, like the side of the course platforms (1.05): no jump gets a bot up there, so the route
            // must not pretend it can — it has to go round to a ramp.
            Box("Platform", new Vector3(6f, 0.5f, 0f), new Vector3(4f, 1f, 4f));
            Bake(TestBounds);

            var path = Route(new Vector3(-5f, 0f, 0f), new Vector3(6f, 1f, 0f));
            Assert.That(path.status, Is.Not.EqualTo(NavMeshPathStatus.PathComplete));
        }

        [Test]
        public void Ledge_ABotCanHop_IsRouted()
        {
            Floor();
            // 0.4 m: over the controller's 0.3 m step offset, so the bot has to hop it — and the route still
            // has to cross it, or the raised van dock (behind a 0.56 m ledge) would be unreachable.
            Box("Ledge", new Vector3(6f, 0.2f, 0f), new Vector3(4f, 0.4f, 4f));
            Bake(TestBounds);

            var path = Route(new Vector3(-5f, 0f, 0f), new Vector3(6f, 0.4f, 0f));
            Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete));
        }

        [Test]
        public void Step_ThePlayerCanStepUp_IsRouted()
        {
            Floor();
            Box("Step", new Vector3(6f, 0.1f, 0f), new Vector3(4f, 0.2f, 4f));
            Bake(TestBounds);

            var path = Route(new Vector3(-5f, 0f, 0f), new Vector3(6f, 0.2f, 0f));
            Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete));
        }

        [Test]
        public void Climb_StaysUnderTheJumpApex()
        {
            // Routes cross ledges up to AgentClimb because a blocked bot hops. If the jump is ever tuned down,
            // this is what says the routes now promise ledges the bots can no longer clear.
            float apex = GameConstants.PlayerJumpSpeed * GameConstants.PlayerJumpSpeed / (2f * -GameConstants.Gravity);
            Assert.That(BotNavMesh.AgentClimb, Is.LessThan(apex));
        }

        [Test]
        public void MovingBodies_AreNotBakedAsWalls()
        {
            Floor();
            var level = Box("Crate", new Vector3(-6f, 0.5f, -6f), Vector3.one);
            var cargo = Box("Cargo", new Vector3(0f, 0.5f, 0f), new Vector3(1f, 1f, 1f));
            cargo.AddComponent<Rigidbody>();
            var zone = Box("Zone", new Vector3(3f, 0.5f, 0f), new Vector3(2f, 1f, 2f));
            zone.GetComponent<Collider>().isTrigger = true;
            var player = new GameObject("Player");
            _made.Add(player);
            player.transform.position = Origin + new Vector3(-3f, 0.9f, 0f);
            player.AddComponent<CharacterController>();
            Physics.SyncTransforms();

            var sources = new List<NavMeshBuildSource>();
            BotNavMesh.CollectLevelSources(TestBounds, new List<NavMeshBuildMarkup>(), sources);
            var baked = new HashSet<Object>();
            foreach (var s in sources) baked.Add(s.component);

            Assert.That(baked.Contains(level.GetComponent<Collider>()), Is.True, "static level geometry must be baked");
            Assert.That(baked.Contains(cargo.GetComponent<Collider>()), Is.False, "cargo moves");
            Assert.That(baked.Contains(zone.GetComponent<Collider>()), Is.False, "triggers aren't solid");
            Assert.That(baked.Contains(player.GetComponent<CharacterController>()), Is.False, "players move");
        }
    }

    /// <summary>
    /// Both shipped maps, opened from Bootstrap.unity: every player spawn must reach every depot spawn, and every
    /// depot spawn must reach every van dock — with the controller-sized agent, not the default one. This is the
    /// route the bots used to lose against the course walls. The raised dock (VanDock0) sits behind the course's
    /// last-yard ledge, so this is also what holds BotNavMesh.AgentClimb high enough to cross it. (The per-run
    /// obstacle slots are authored clear of the gate and bridge, so the bare course is what has to connect.)
    /// </summary>
    public class BotNavMeshMapTests
    {
        private Scene _scene;
        private NavMeshData _data;
        private NavMeshDataInstance _instance;
        // Which maps were active before a test switched them. When Bootstrap is the scene already open in the
        // editor, CloseScene below can't unload it (it is the last scene), so the switch must be undone by hand
        // or the next save would write the test's map selection into the scene.
        private readonly List<(GameObject map, bool active)> _mapsBefore = new List<(GameObject, bool)>();
        private bool _openedHere;

        private const string ScenePath = "Assets/SlopCo/Scenes/Bootstrap.unity";

        [OneTimeSetUp]
        public void OpenScene()
        {
            // Bootstrap may already be the scene being edited: use it as it is and leave it open afterwards,
            // rather than closing the user's scene (and its unsaved edits) on the way out.
            var loaded = SceneManager.GetSceneByPath(ScenePath);
            _openedHere = !loaded.isLoaded;
            _scene = _openedHere ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive) : loaded;
        }

        [OneTimeTearDown]
        public void CloseScene()
        {
            if (_openedHere) EditorSceneManager.CloseScene(_scene, true);
        }

        [TearDown]
        public void RemoveMesh()
        {
            if (_instance.valid) _instance.Remove();
            if (_data != null) Object.DestroyImmediate(_data);
            _data = null;
            foreach (var (map, active) in _mapsBefore) if (map != null) map.SetActive(active);
            _mapsBefore.Clear();
        }

        private Transform Maps()
        {
            foreach (var go in _scene.GetRootGameObjects())
                if (go.name == "Maps") return go.transform;
            Assert.Fail("Maps root missing from Bootstrap.unity");
            return null;
        }

        private static Vector3[] PointsUnder(Transform map, string holder)
        {
            var h = map.Find(holder);
            Assert.That(h, Is.Not.Null, $"{map.name} has no {holder}");
            Assert.That(h.childCount, Is.GreaterThan(0), $"{map.name}/{holder} is empty");
            var pts = new Vector3[h.childCount];
            for (int i = 0; i < h.childCount; i++) pts[i] = h.GetChild(i).position;
            return pts;
        }

        [TestCase("Map_0")]
        [TestCase("Map_1")]
        public void EveryHaulLeg_IsConnected(string mapName)
        {
            var maps = Maps();
            Transform map = null;
            for (int i = 0; i < maps.childCount; i++)
            {
                var m = maps.GetChild(i);
                _mapsBefore.Add((m.gameObject, m.gameObject.activeSelf));
                m.gameObject.SetActive(m.name == mapName);   // only the map in play is baked, as at runtime
                if (m.name == mapName) map = m;
            }
            Assert.That(map, Is.Not.Null, $"{mapName} missing");
            Physics.SyncTransforms();

            var bounds = BotNavMesh.MapBounds(map);
            var sources = new List<NavMeshBuildSource>();
            BotNavMesh.CollectLevelSources(bounds, new List<NavMeshBuildMarkup>(), sources);
            _data = NavMeshBuilder.BuildNavMeshData(BotNavMesh.Settings(), sources, bounds, Vector3.zero, Quaternion.identity);
            Assert.That(_data, Is.Not.Null, "bake failed");
            _instance = NavMesh.AddNavMeshData(_data);

            var spawns = PointsUnder(map, "PlayerSpawns");
            var depot = PointsUnder(map, "DepotSpawns");
            var docks = PointsUnder(map, "VanAnchors");

            var broken = new List<string>();
            foreach (var s in spawns) foreach (var d in depot) Check(s, d, "spawn→depot", broken);
            foreach (var d in depot) foreach (var v in docks) Check(d, v, "depot→van", broken);
            Assert.That(broken, Is.Empty, $"{mapName}: unreachable legs:\n" + string.Join("\n", broken));
        }

        // How far an authored point may sit from the mesh and still count as ON it. Spawn points float up to 1 m
        // over the floor, and a depot point at the foot of the ramp sits one agent radius inside the ramp's
        // eroded edge. Anything farther means the point itself is unreachable and SamplePosition quietly picked
        // somewhere else — e.g. the ground beside the raised dock (2.6 m away, 1.6 m down) instead of its top,
        // which would turn a broken leg into a "complete" route to the wrong place.
        private const float MaxSnapFlat = 0.8f;
        private const float MaxSnapUp = 1.2f;

        private static bool OnPoint(Vector3 authored, Vector3 snapped)
        {
            Vector3 d = snapped - authored;
            return d.x * d.x + d.z * d.z <= MaxSnapFlat * MaxSnapFlat && Mathf.Abs(d.y) <= MaxSnapUp;
        }

        private static void Check(Vector3 from, Vector3 to, string leg, List<string> broken)
        {
            if (!NavMesh.SamplePosition(from, out var a, BotNavMesh.SnapRadius, NavMesh.AllAreas)) { broken.Add($"{leg} {from}: start off-mesh"); return; }
            if (!NavMesh.SamplePosition(to, out var b, BotNavMesh.SnapRadius, NavMesh.AllAreas)) { broken.Add($"{leg} {to}: end off-mesh"); return; }
            if (!OnPoint(from, a.position)) { broken.Add($"{leg} {from}: start is off the mesh (nearest {a.position})"); return; }
            if (!OnPoint(to, b.position)) { broken.Add($"{leg} {to}: end is off the mesh (nearest {b.position})"); return; }
            var path = new NavMeshPath();
            NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path);
            if (path.status != NavMeshPathStatus.PathComplete) broken.Add($"{leg} {from} → {to}: {path.status}");
        }
    }
}
