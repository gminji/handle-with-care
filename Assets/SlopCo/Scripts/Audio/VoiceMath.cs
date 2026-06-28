using System;

namespace SlopCo.Audio
{
    /// <summary>
    /// Pure voice-DSP helpers — NO UnityEngine / NGO dependency so they are unit-testable in EditMode
    /// (mirrors CargoMath / QuotaMath, the project's "pure math + EditMode test" convention).
    /// Wire format for the custom NGO proximity-voice pipeline: 16 kHz, mono, PCM16 little-endian.
    /// </summary>
    public static class VoiceMath
    {
        /// <summary>Clamp a normalized [-1,1] sample and quantize to signed 16-bit PCM.</summary>
        public static short FloatToPcm16(float s)
        {
            if (s > 1f) s = 1f;
            else if (s < -1f) s = -1f;
            // 32767 (not 32768) keeps +1.0 inside short range; round to nearest.
            int v = (int)MathF.Round(s * 32767f);
            if (v > short.MaxValue) v = short.MaxValue;
            else if (v < short.MinValue) v = short.MinValue;
            return (short)v;
        }

        /// <summary>Inverse of <see cref="FloatToPcm16"/>. Divide by 32768 so the range maps back into [-1,1).</summary>
        public static float Pcm16ToFloat(short v) => v / 32768f;

        /// <summary>
        /// Encode float samples to PCM16 little-endian bytes. Returns bytes written (= samples * 2).
        /// Writes min(src.Length, dst.Length/2) samples; never overruns <paramref name="dst"/>.
        /// </summary>
        public static int EncodePcm16(ReadOnlySpan<float> src, Span<byte> dst)
        {
            int n = Math.Min(src.Length, dst.Length / 2);
            for (int i = 0; i < n; i++)
            {
                short s = FloatToPcm16(src[i]);
                dst[i * 2] = (byte)(s & 0xFF);             // low byte first (LE)
                dst[i * 2 + 1] = (byte)((s >> 8) & 0xFF);  // high byte
            }
            return n * 2;
        }

        /// <summary>
        /// Decode PCM16 little-endian bytes to float samples. Returns samples written.
        /// Writes min(src.Length/2, dst.Length) samples; trailing odd byte is ignored.
        /// </summary>
        public static int DecodePcm16(ReadOnlySpan<byte> src, Span<float> dst)
        {
            int n = Math.Min(src.Length / 2, dst.Length);
            for (int i = 0; i < n; i++)
            {
                short s = (short)(src[i * 2] | (src[i * 2 + 1] << 8));
                dst[i] = Pcm16ToFloat(s);
            }
            return n;
        }

        /// <summary>Root-mean-square amplitude of the block (0..~1). Empty block = 0.</summary>
        public static float Rms(ReadOnlySpan<float> samples)
        {
            if (samples.Length == 0) return 0f;
            double sum = 0d;
            for (int i = 0; i < samples.Length; i++)
            {
                double v = samples[i];
                sum += v * v;
            }
            return (float)Math.Sqrt(sum / samples.Length);
        }

        /// <summary>Voice-activity gate: true when RMS strictly exceeds the threshold (silence is dropped).</summary>
        public static bool IsVoiced(ReadOnlySpan<float> samples, float rmsThreshold)
            => Rms(samples) > rmsThreshold;

        /// <summary>
        /// Linear resample from <paramref name="srcRate"/> to <paramref name="dstRate"/> (used to normalize a
        /// capture device that doesn't honor 16 kHz onto the fixed wire rate). Returns samples written into
        /// <paramref name="dst"/>. Equal rates = straight copy. Guards against empty/invalid input.
        /// </summary>
        public static int ResampleLinear(ReadOnlySpan<float> src, int srcRate, Span<float> dst, int dstRate)
        {
            if (src.Length == 0 || srcRate <= 0 || dstRate <= 0 || dst.Length == 0) return 0;

            if (srcRate == dstRate)
            {
                int copy = Math.Min(src.Length, dst.Length);
                src.Slice(0, copy).CopyTo(dst);
                return copy;
            }

            // Expected output length for the whole input, clamped to the destination capacity.
            long want = (long)src.Length * dstRate / srcRate;
            int outLen = (int)Math.Min(want, dst.Length);

            double step = (double)srcRate / dstRate;
            for (int i = 0; i < outLen; i++)
            {
                double pos = i * step;
                int idx = (int)pos;
                double frac = pos - idx;
                float a = src[idx];
                float b = idx + 1 < src.Length ? src[idx + 1] : a; // hold last sample at the tail
                dst[i] = (float)(a + (b - a) * frac);
            }
            return outLen;
        }
    }
}
