namespace SlopCo.UI
{
    /// <summary>
    /// Rolling frame statistics for the debug overlay: the last <c>capacity</c> frames' times and GC allocations.
    /// Pure C# with a fixed ring buffer (no allocation after construction), so it is EditMode-testable and the
    /// overlay can sample every frame without adding to the garbage it reports.
    /// </summary>
    public sealed class FrameStats
    {
        private readonly float[] _seconds;
        private readonly long[] _gcBytes;
        private int _next;
        private int _count;

        public FrameStats(int capacity)
        {
            if (capacity < 1) capacity = 1;
            _seconds = new float[capacity];
            _gcBytes = new long[capacity];
        }

        public int Count => _count;

        /// <summary>Record one frame. <paramref name="gcBytes"/> below zero means "not measured" (counted as 0).</summary>
        public void Push(float seconds, long gcBytes)
        {
            _seconds[_next] = seconds < 0f ? 0f : seconds;
            _gcBytes[_next] = gcBytes < 0 ? 0 : gcBytes;
            _next = (_next + 1) % _seconds.Length;
            if (_count < _seconds.Length) _count++;
        }

        public float AverageMs
        {
            get
            {
                if (_count == 0) return 0f;
                float sum = 0f;
                for (int i = 0; i < _count; i++) sum += _seconds[i];
                return sum / _count * 1000f;
            }
        }

        public float MaxMs
        {
            get
            {
                float max = 0f;
                for (int i = 0; i < _count; i++) if (_seconds[i] > max) max = _seconds[i];
                return max * 1000f;
            }
        }

        /// <summary>Frames per second over the window (0 before the first frame).</summary>
        public float Fps
        {
            get
            {
                float ms = AverageMs;
                return ms > 0f ? 1000f / ms : 0f;
            }
        }

        public long AverageGcBytes
        {
            get
            {
                if (_count == 0) return 0;
                long sum = 0;
                for (int i = 0; i < _count; i++) sum += _gcBytes[i];
                return sum / _count;
            }
        }

        /// <summary>Share of the window's frames that allocated anything.</summary>
        public float AllocatingFrameShare
        {
            get
            {
                if (_count == 0) return 0f;
                int n = 0;
                for (int i = 0; i < _count; i++) if (_gcBytes[i] > 0) n++;
                return (float)n / _count;
            }
        }
    }

    /// <summary>Formatting rules for the debug overlay (pure, EditMode-testable).</summary>
    public static class DebugOverlayText
    {
        private static readonly System.Globalization.CultureInfo Inv = System.Globalization.CultureInfo.InvariantCulture;

        /// <summary>"0 B", "512 B", "1.5 KB", "3.2 MB".</summary>
        public static string Bytes(long bytes)
        {
            if (bytes < 0) bytes = 0;
            if (bytes < 1024) return bytes + " B";
            if (bytes < 1024 * 1024) return (bytes / 1024f).ToString("0.0", Inv) + " KB";
            return (bytes / (1024f * 1024f)).ToString("0.0", Inv) + " MB";
        }

        /// <summary>One bot's label: intent, route progress and how long it has been blocked.</summary>
        public static string BotLabel(string intent, int cornerIndex, int cornerCount, float stallSeconds, int hops)
        {
            string route = cornerCount > 0 ? $" {System.Math.Min(cornerIndex, cornerCount)}/{cornerCount}" : " direct";
            string stall = stallSeconds > 0.05f ? " stuck " + stallSeconds.ToString("0.0", Inv) + "s" : "";
            string hop = hops > 0 ? $" hops {hops}" : "";
            return intent + route + stall + hop;
        }

        /// <summary>Network role of this peer as the overlay names it.</summary>
        public static string Role(bool listening, bool isServer, bool isClient)
        {
            if (!listening) return "Offline";
            if (isServer && isClient) return "Host";
            return isServer ? "Server" : "Client";
        }
    }
}
