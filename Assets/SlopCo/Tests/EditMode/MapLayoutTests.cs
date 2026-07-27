using NUnit.Framework;
using SlopCo.Gameplay;

namespace SlopCo.Tests.EditMode
{
    /// <summary>
    /// Pins the per-run layout roll (MapLayout). Determinism is the whole contract: the server replicates
    /// one seed and every client must rebuild the SAME van dock and obstacle field, so "same seed → same
    /// result" and "different seeds → actually different results" both need holding down.
    /// Pure logic, no UnityEngine dependency — same shape as DailyModifierTests.
    /// </summary>
    public class MapLayoutTests
    {
        [Test]
        public void SameSeed_GivesIdenticalLayout()
        {
            const int seed = 918273;
            Assert.AreEqual(MapLayout.VanAnchorIndex(seed, 3), MapLayout.VanAnchorIndex(seed, 3));
            for (int slot = 0; slot < 16; slot++)
            {
                Assert.AreEqual(MapLayout.SlotActive(seed, slot, 55), MapLayout.SlotActive(seed, slot, 55));
                Assert.AreEqual(MapLayout.KindFor(seed, slot), MapLayout.KindFor(seed, slot));
                Assert.AreEqual(MapLayout.ScaleFor(seed, slot, 0.7f, 1.35f),
                                MapLayout.ScaleFor(seed, slot, 0.7f, 1.35f));
                Assert.AreEqual(MapLayout.YawFor(seed, slot), MapLayout.YawFor(seed, slot));
            }
        }

        [Test]
        public void VanAnchorIndex_StaysInRange()
        {
            for (int seed = 1; seed < 400; seed++)
            {
                int i = MapLayout.VanAnchorIndex(seed, 3);
                Assert.GreaterOrEqual(i, 0);
                Assert.Less(i, 3);
            }
        }

        [Test]
        public void VanAnchorIndex_UsesEveryDock()
        {
            // If one dock never came up, the "haul length varies" promise would be a lie.
            var seen = new bool[3];
            for (int seed = 1; seed < 400; seed++) seen[MapLayout.VanAnchorIndex(seed, 3)] = true;
            Assert.IsTrue(seen[0] && seen[1] && seen[2]);
        }

        [Test]
        public void VanAnchorIndex_DegenerateCounts_AreSafe()
        {
            Assert.AreEqual(0, MapLayout.VanAnchorIndex(123, 1));
            Assert.AreEqual(0, MapLayout.VanAnchorIndex(123, 0));
            Assert.AreEqual(0, MapLayout.VanAnchorIndex(123, -4));
        }

        [Test]
        public void SlotActive_HonoursTheExtremes()
        {
            for (int slot = 0; slot < 12; slot++)
            {
                Assert.IsFalse(MapLayout.SlotActive(42, slot, 0),   "0% must leave the route clear");
                Assert.IsTrue (MapLayout.SlotActive(42, slot, 100), "100% must fill every slot");
            }
        }

        [Test]
        public void SlotActive_DensityRoughlyTracksThePercentage()
        {
            int filled = 0, total = 0;
            for (int seed = 1; seed <= 200; seed++)
                for (int slot = 0; slot < 12; slot++)
                {
                    total++;
                    if (MapLayout.SlotActive(seed, slot, 50)) filled++;
                }
            float share = filled / (float)total;
            Assert.Greater(share, 0.35f);
            Assert.Less(share, 0.65f);
        }

        [Test]
        public void DifferentSeeds_ProduceDifferentObstacleFields()
        {
            int differences = 0;
            for (int slot = 0; slot < 12; slot++)
                if (MapLayout.SlotActive(11, slot, 55) != MapLayout.SlotActive(12, slot, 55)) differences++;
            Assert.Greater(differences, 0, "consecutive seeds must not yield the same field");
        }

        [Test]
        public void KindFor_CoversEveryObstacleType()
        {
            var seen = new bool[4];
            for (int seed = 1; seed < 300; seed++) seen[(int)MapLayout.KindFor(seed, 0)] = true;
            for (int i = 0; i < 4; i++) Assert.IsTrue(seen[i], "obstacle kind " + (ObstacleKind)i + " never rolled");
        }

        [Test]
        public void ScaleFor_StaysWithinBounds()
        {
            for (int seed = 1; seed < 300; seed++)
            {
                float s = MapLayout.ScaleFor(seed, seed % 13, 0.7f, 1.35f);
                Assert.GreaterOrEqual(s, 0.7f);
                Assert.LessOrEqual(s, 1.35f);
            }
            Assert.AreEqual(0.7f, MapLayout.ScaleFor(5, 1, 0.7f, 0.7f), 1e-5f);   // degenerate range
        }

        [Test]
        public void YawFor_IsAFullCircle()
        {
            bool low = false, high = false;
            for (int seed = 1; seed < 300; seed++)
            {
                float y = MapLayout.YawFor(seed, 3);
                Assert.GreaterOrEqual(y, 0f);
                Assert.LessOrEqual(y, 360f);
                if (y < 90f) low = true;
                if (y > 270f) high = true;
            }
            Assert.IsTrue(low && high);
        }

        [Test]
        public void Slots_DoNotAllShareOneOutcome()
        {
            // Guards against a hash that ignores the slot salt — every slot would light up together.
            int active = 0;
            for (int slot = 0; slot < 12; slot++) if (MapLayout.SlotActive(777, slot, 50)) active++;
            Assert.Greater(active, 0);
            Assert.Less(active, 12);
        }
    }
}
