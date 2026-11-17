using NUnit.Framework;
using SlopCo.UI;

namespace SlopCo.Tests.EditMode
{
    /// <summary>
    /// Pins the debug overlay's numbers (FrameStats) and wording (DebugOverlayText). The overlay reports GC per
    /// frame, so its own bookkeeping must not allocate after construction — FrameStats is a fixed ring buffer.
    /// </summary>
    public class DebugOverlayTextTests
    {
        [Test]
        public void FrameStats_Empty_IsAllZero()
        {
            var s = new FrameStats(8);
            Assert.AreEqual(0, s.Count);
            Assert.AreEqual(0f, s.AverageMs);
            Assert.AreEqual(0f, s.MaxMs);
            Assert.AreEqual(0f, s.Fps);
            Assert.AreEqual(0, s.AverageGcBytes);
            Assert.AreEqual(0f, s.AllocatingFrameShare);
        }

        [Test]
        public void FrameStats_AveragesAndWorstFrame()
        {
            var s = new FrameStats(8);
            s.Push(0.010f, 0);
            s.Push(0.020f, 1000);
            s.Push(0.030f, 2000);
            Assert.AreEqual(20f, s.AverageMs, 1e-3f);
            Assert.AreEqual(30f, s.MaxMs, 1e-3f);
            Assert.AreEqual(50f, s.Fps, 1e-2f);
            Assert.AreEqual(1000, s.AverageGcBytes);
            Assert.AreEqual(2f / 3f, s.AllocatingFrameShare, 1e-4f);
        }

        [Test]
        public void FrameStats_WindowForgetsOldFrames()
        {
            var s = new FrameStats(2);
            s.Push(1.0f, 5000);   // a hitch that has scrolled out of the window
            s.Push(0.010f, 0);
            s.Push(0.010f, 0);
            Assert.AreEqual(2, s.Count);
            Assert.AreEqual(10f, s.MaxMs, 1e-3f);
            Assert.AreEqual(0, s.AverageGcBytes);
        }

        [Test]
        public void FrameStats_NegativeInputs_CountAsZero()
        {
            var s = new FrameStats(4);
            s.Push(-1f, -1);   // -1 GC = "recorder not available"
            Assert.AreEqual(0f, s.MaxMs);
            Assert.AreEqual(0, s.AverageGcBytes);
        }

        [TestCase(0L, "0 B")]
        [TestCase(-5L, "0 B")]
        [TestCase(512L, "512 B")]
        [TestCase(1536L, "1.5 KB")]
        [TestCase(3L * 1024 * 1024 + 200 * 1024, "3.2 MB")]
        public void Bytes_PicksAUnit(long bytes, string expected)
        {
            Assert.AreEqual(expected, DebugOverlayText.Bytes(bytes));
        }

        [Test]
        public void BotLabel_ShowsRouteStallAndHops()
        {
            Assert.AreEqual("Haul 2/5 stuck 0.4s hops 3", DebugOverlayText.BotLabel("Haul", 2, 5, 0.4f, 3));
            Assert.AreEqual("Claim direct", DebugOverlayText.BotLabel("Claim", 0, 0, 0f, 0), "no route: steering straight");
            Assert.AreEqual("Join 5/5", DebugOverlayText.BotLabel("Join", 7, 5, 0.01f, 0), "index clamps to the count");
        }

        [Test]
        public void Role_NamesThePeer()
        {
            Assert.AreEqual("Offline", DebugOverlayText.Role(false, false, false));
            Assert.AreEqual("Host", DebugOverlayText.Role(true, true, true));
            Assert.AreEqual("Server", DebugOverlayText.Role(true, true, false));
            Assert.AreEqual("Client", DebugOverlayText.Role(true, false, true));
        }
    }
}
