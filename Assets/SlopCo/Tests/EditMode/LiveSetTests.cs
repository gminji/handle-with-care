using NUnit.Framework;
using SlopCo.Core;

namespace SlopCo.Tests.EditMode
{
    /// <summary>
    /// Pins LiveSet, the spawn-registered list that replaced per-frame FindObjectsByType scans (fuse meter, rats,
    /// bot targeting): a double spawn callback must not count an object twice, and despawn must really remove it.
    /// </summary>
    public class LiveSetTests
    {
        private sealed class Thing { }

        [Test]
        public void Add_IsIdempotent_AndKeepsInsertionOrder()
        {
            var set = new LiveSet<Thing>();
            var a = new Thing();
            var b = new Thing();
            set.Add(a);
            set.Add(b);
            set.Add(a);
            Assert.AreEqual(2, set.Count);
            Assert.AreSame(a, set[0]);
            Assert.AreSame(b, set[1]);
        }

        [Test]
        public void Add_IgnoresNull()
        {
            var set = new LiveSet<Thing>();
            set.Add(null);
            Assert.AreEqual(0, set.Count);
        }

        [Test]
        public void Remove_TakesItOut_AndReportsWhetherItWasThere()
        {
            var set = new LiveSet<Thing>();
            var a = new Thing();
            set.Add(a);
            Assert.IsTrue(set.Remove(a));
            Assert.IsFalse(set.Remove(a), "a second despawn is a no-op");
            Assert.IsFalse(set.Remove(null));
            Assert.AreEqual(0, set.Count);
        }

        [Test]
        public void Clear_Empties()
        {
            var set = new LiveSet<Thing>();
            set.Add(new Thing());
            set.Add(new Thing());
            set.Clear();
            Assert.AreEqual(0, set.Count);
        }
    }
}
