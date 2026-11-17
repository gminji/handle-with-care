using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using SlopCo.Gameplay;

namespace SlopCo.Tests.EditMode
{
    /// <summary>
    /// The shipped Harbor and Rooftop obstacle lists: both exist with the props the maps need, every prop is a
    /// solid static body whose collider stays inside its model, and real seeded fields built from them keep every
    /// prop within the 1.1 m footprint rule and standing on its slot.
    /// </summary>
    public class ObstacleSetTests
    {
        private const string HarborPath = "Assets/SlopCo/Settings/HarborObstacles.asset";
        private const string RooftopPath = "Assets/SlopCo/Settings/RooftopObstacles.asset";

        // Upper end of the per-slot size roll, the same as MapManager.BuildListedProp's MapLayout.ScaleFor(…, 0.7f, 1.35f).
        private const float LargestRoll = 1.35f;
        private const float Tolerance = 1e-3f;

        private readonly List<GameObject> _made = new List<GameObject>();

        [TearDown]
        public void Cleanup()
        {
            foreach (var go in _made) if (go != null) Object.DestroyImmediate(go);
            _made.Clear();
        }

        // Far above the open scene (Bootstrap's Ground sits at y = 0), like BotNavMeshTests.
        private static readonly Vector3 Origin = new Vector3(0f, 500f, 0f) + Vector3.up * 50f;

        // A missing list would silently fall back to the default primitives in game, so a missing asset fails here.
        private static ObstacleSet Load(string path)
        {
            var set = AssetDatabase.LoadAssetAtPath<ObstacleSet>(path);
            Assert.That(set, Is.Not.Null, $"{path} is missing");
            return set;
        }

        private static IEnumerable<(ObstacleSet set, ObstacleSet.Entry entry)> AllEntries()
        {
            foreach (var path in new[] { HarborPath, RooftopPath })
            {
                var set = Load(path);
                for (int i = 0; i < set.Count; i++)
                {
                    var e = set.Get(i);
                    Assert.That(e, Is.Not.Null, $"{set.name}[{i}] is empty");
                    Assert.That(e.prefab, Is.Not.Null, $"{set.name}[{i}] has no prefab");
                    yield return (set, e);
                }
            }
        }

        // Mesh-bounds corners of every enabled MeshFilter, in the prefab root's own (upright, unscaled) space.
        private static Bounds MeshBoundsInRoot(GameObject prefab)
        {
            var toRoot = prefab.transform.worldToLocalMatrix;
            bool any = false;
            var b = new Bounds();
            foreach (var mf in prefab.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                var m = toRoot * mf.transform.localToWorldMatrix;
                var mb = mf.sharedMesh.bounds;
                for (int k = 0; k < 8; k++)
                {
                    var p = m.MultiplyPoint3x4(new Vector3((k & 1) == 0 ? mb.min.x : mb.max.x, (k & 2) == 0 ? mb.min.y : mb.max.y, (k & 4) == 0 ? mb.min.z : mb.max.z));
                    if (!any) { b = new Bounds(p, Vector3.zero); any = true; } else b.Encapsulate(p);
                }
            }
            Assert.That(any, Is.True, $"{prefab.name} has no mesh");
            return b;
        }

        private static bool AnyNameHas(ObstacleSet set, string part)
        {
            for (int i = 0; i < set.Count; i++)
                if (set.Get(i).prefab != null && set.Get(i).prefab.name.Contains(part)) return true;
            return false;
        }

        [Test]
        public void Lists_Exist_AndHaveTheRequiredProps()
        {
            var harbor = Load(HarborPath);
            var rooftop = Load(RooftopPath);
            Assert.That(harbor.Count, Is.EqualTo(4), "Harbor list size");
            Assert.That(rooftop.Count, Is.EqualTo(5), "Rooftop list size");

            foreach (var part in new[] { "Pallet", "Drum", "RopeCoil" })
                Assert.That(AnyNameHas(harbor, part), Is.True, $"Harbor list has no {part}");
            Assert.That(AnyNameHas(harbor, "CrateStack") || AnyNameHas(harbor, "ContainerDoor"), Is.True, "Harbor list has no crate stack / container door");

            foreach (var part in new[] { "ACUnit", "PlantPot", "DryingRack", "TrashCan" })
                Assert.That(AnyNameHas(rooftop, part), Is.True, $"Rooftop list has no {part}");
            Assert.That(AnyNameHas(rooftop, "Vent") || AnyNameHas(rooftop, "Chimney"), Is.True, "Rooftop list has no vent / chimney");
        }

        [Test]
        public void Footprint_AtLargestRoll_IsAtMost1_1m()
        {
            foreach (var (set, e) in AllEntries())
                Assert.That(e.footprint * LargestRoll, Is.LessThanOrEqualTo(ObstacleSet.MaxFootprint),
                    $"{set.name}/{e.prefab.name}: footprint {e.footprint} grows past {ObstacleSet.MaxFootprint} m at ×{LargestRoll}");
        }

        [Test]
        public void Prefabs_AreSolid_WithoutRigidbody()
        {
            foreach (var (set, e) in AllEntries())
            {
                var p = e.prefab;
                bool solid = false;
                foreach (var col in p.GetComponentsInChildren<Collider>())
                {
                    if (!col.enabled || col.isTrigger) continue;
                    if (col is MeshCollider mc && (mc.sharedMesh == null || !mc.sharedMesh.isReadable)) continue;
                    solid = true;
                }
                Assert.That(solid, Is.True, $"{set.name}/{p.name} has no enabled solid collider");
                Assert.That(p.GetComponentsInChildren<Rigidbody>(true), Is.Empty, $"{set.name}/{p.name} has a Rigidbody");
                Assert.That(p.GetComponentsInChildren<Joint>(true), Is.Empty, $"{set.name}/{p.name} has a Joint");

                bool mesh = false;
                foreach (var mf in p.GetComponentsInChildren<MeshFilter>())
                    if (mf.sharedMesh != null) mesh = true;
                Assert.That(mesh, Is.True, $"{set.name}/{p.name} has no enabled mesh");
                var b = MeshBoundsInRoot(p);
                Assert.That(Mathf.Min(b.size.x, b.size.z), Is.GreaterThan(0f), $"{set.name}/{p.name} mesh is flat");
            }
        }

        [Test]
        public void Colliders_StayInsideTheMesh()
        {
            foreach (var (set, e) in AllEntries())
            {
                var p = e.prefab;
                var mesh = MeshBoundsInRoot(p);
                var toRoot = p.transform.worldToLocalMatrix;
                foreach (var box in p.GetComponentsInChildren<BoxCollider>())
                {
                    // Corners transformed into root space, not the collider's world AABB.
                    var m = toRoot * box.transform.localToWorldMatrix;
                    Vector3 min = box.center - box.size * 0.5f, max = box.center + box.size * 0.5f;
                    for (int k = 0; k < 8; k++)
                    {
                        var q = m.MultiplyPoint3x4(new Vector3((k & 1) == 0 ? min.x : max.x, (k & 2) == 0 ? min.y : max.y, (k & 4) == 0 ? min.z : max.z));
                        Assert.That(q.x, Is.InRange(mesh.min.x - Tolerance, mesh.max.x + Tolerance), $"{set.name}/{p.name}: collider sticks out along x");
                        Assert.That(q.z, Is.InRange(mesh.min.z - Tolerance, mesh.max.z + Tolerance), $"{set.name}/{p.name}: collider sticks out along z");
                    }
                }
            }
        }

        [Test]
        public void Pallet_StaysLowEnoughToHop()
        {
            var harbor = Load(HarborPath);
            ObstacleSet.Entry pallet = null;
            for (int i = 0; i < harbor.Count; i++)
                if (harbor.Get(i).prefab != null && harbor.Get(i).prefab.name.Contains("Pallet")) pallet = harbor.Get(i);
            Assert.That(pallet, Is.Not.Null, "Harbor list has no pallet");

            // Sized like BuildListedProp: longest horizontal side = footprint × roll. 0.35 m sits well under the 0.69 m jump apex.
            var b = MeshBoundsInRoot(pallet.prefab);
            float f = pallet.footprint * LargestRoll / Mathf.Max(b.size.x, b.size.z);
            Assert.That(b.size.y * f, Is.LessThanOrEqualTo(0.35f), "the pallet is too tall to hop at the largest roll");
        }

        // The prop's mesh corners in holder space with this slot's yaw taken back out (as in BotNavMeshTests.Upright).
        private static Bounds Upright(Transform prop, int seed, int slot)
        {
            var unyaw = Quaternion.Inverse(Quaternion.Euler(0f, MapLayout.YawFor(seed, slot), 0f));
            var toHolder = prop.parent.worldToLocalMatrix;
            bool any = false;
            var b = new Bounds();
            foreach (var mf in prop.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                var m = toHolder * mf.transform.localToWorldMatrix;
                var mb = mf.sharedMesh.bounds;
                for (int k = 0; k < 8; k++)
                {
                    var p = new Vector3((k & 1) == 0 ? mb.min.x : mb.max.x, (k & 2) == 0 ? mb.min.y : mb.max.y, (k & 4) == 0 ? mb.min.z : mb.max.z);
                    var q = unyaw * m.MultiplyPoint3x4(p);
                    if (!any) { b = new Bounds(q, Vector3.zero); any = true; } else b.Encapsulate(q);
                }
            }
            Assert.That(any, Is.True, $"{prop.name} has no mesh");
            return b;
        }

        [Test]
        public void RealLists_BuildWithinTheRule()
        {
            foreach (var path in new[] { HarborPath, RooftopPath })
            {
                var set = Load(path);
                var root = new GameObject("TestMapRoot").transform;
                root.position = Origin;
                _made.Add(root.gameObject);

                // Twelve slots, some raised, so "stands on the slot" means more than y = 0.
                var slots = new GameObject("ObstacleSlots").transform;
                slots.SetParent(root, false);
                for (int i = 0; i < 12; i++)
                {
                    var s = new GameObject("Slot" + i).transform;
                    s.SetParent(slots, false);
                    s.localPosition = new Vector3((i % 4) * 4f - 6f, (i % 3) * 0.25f, (i / 4) * 4f - 4f);
                }

                var seen = new HashSet<string>();
                for (int seed = 1; seed <= 40; seed++)
                {
                    var field = MapManager.BuildObstacleField(root, slots, seed, 100, null, set);
                    Assert.That(field, Is.Not.Null);
                    foreach (Transform c in field.transform)
                    {
                        int slot = int.Parse(c.name.Substring(c.name.LastIndexOf('_') + 1));
                        seen.Add(c.name.Substring(0, c.name.LastIndexOf('_')));
                        var u = Upright(c, seed, slot);
                        Assert.That(Mathf.Max(u.size.x, u.size.z), Is.LessThanOrEqualTo(ObstacleSet.MaxFootprint + Tolerance),
                            $"{set.name} seed {seed}: {c.name} is wider than {ObstacleSet.MaxFootprint} m");
                        float slotY = field.transform.InverseTransformPoint(slots.GetChild(slot).position).y;
                        Assert.That(u.min.y, Is.EqualTo(slotY).Within(Tolerance), $"{set.name} seed {seed}: {c.name} floats or sinks");
                    }
                }

                for (int i = 0; i < set.Count; i++)
                    Assert.That(seen, Does.Contain(set.Get(i).prefab.name), $"{set.name}: {set.Get(i).prefab.name} never came up in 40 seeds");
            }
        }
    }
}
