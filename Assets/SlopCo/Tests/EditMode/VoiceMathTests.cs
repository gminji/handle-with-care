using NUnit.Framework;
using SlopCo.Audio;

namespace SlopCo.Tests.EditMode
{
    /// <summary>
    /// Verifies the pure voice-DSP arithmetic (VoiceMath) without any NGO / UnityEngine audio runtime.
    /// </summary>
    public class VoiceMathTests
    {
        [Test]
        public void Pcm16RoundTrip_PreservesSampleWithinOneLsb()
        {
            float[] probes = { -1f, -0.5f, -0.01f, 0f, 0.01f, 0.5f, 0.999f };
            foreach (float p in probes)
            {
                short q = VoiceMath.FloatToPcm16(p);
                float back = VoiceMath.Pcm16ToFloat(q);
                // One quantization step at 16-bit ~= 1/32768; allow a touch over for rounding.
                Assert.That(back, Is.EqualTo(p).Within(1.5f / 32768f), $"probe {p}");
            }
        }

        [Test]
        public void FloatToPcm16_ClampsOutOfRange()
        {
            // Symmetric ×32767 mapping: out-of-range floats clamp to ±32767 (no wrap/overflow).
            Assert.AreEqual(32767, VoiceMath.FloatToPcm16(2f));
            Assert.AreEqual(-32767, VoiceMath.FloatToPcm16(-2f));
        }

        [Test]
        public void EncodePcm16_WritesTwoLittleEndianBytesPerSample()
        {
            float[] src = { 1f }; // → 32767 = 0x7FFF
            byte[] dst = new byte[2];
            int written = VoiceMath.EncodePcm16(src, dst);
            Assert.AreEqual(2, written);
            Assert.AreEqual(0xFF, dst[0]); // low byte first
            Assert.AreEqual(0x7F, dst[1]); // high byte
        }

        [Test]
        public void EncodeThenDecode_RecoversSamples()
        {
            float[] src = { -0.75f, -0.25f, 0f, 0.25f, 0.75f };
            byte[] mid = new byte[src.Length * 2];
            float[] outBuf = new float[src.Length];

            Assert.AreEqual(src.Length * 2, VoiceMath.EncodePcm16(src, mid));
            Assert.AreEqual(src.Length, VoiceMath.DecodePcm16(mid, outBuf));
            for (int i = 0; i < src.Length; i++)
                Assert.That(outBuf[i], Is.EqualTo(src[i]).Within(1.5f / 32768f), $"index {i}");
        }

        [Test]
        public void EncodePcm16_NeverOverrunsDestination()
        {
            float[] src = { 0.5f, 0.5f, 0.5f };
            byte[] dst = new byte[2];            // room for only 1 sample
            Assert.AreEqual(2, VoiceMath.EncodePcm16(src, dst));
        }

        [Test]
        public void Rms_IsZeroForSilenceAndMonotonicWithAmplitude()
        {
            float[] silence = { 0f, 0f, 0f, 0f };
            float[] quiet = { 0.1f, -0.1f, 0.1f, -0.1f };
            float[] loud = { 0.6f, -0.6f, 0.6f, -0.6f };

            Assert.AreEqual(0f, VoiceMath.Rms(silence), 1e-6f);
            Assert.Less(VoiceMath.Rms(quiet), VoiceMath.Rms(loud));
            Assert.AreEqual(0.6f, VoiceMath.Rms(loud), 1e-4f);
        }

        [Test]
        public void IsVoiced_GatesAtThreshold()
        {
            float[] quiet = { 0.005f, -0.005f, 0.005f, -0.005f }; // RMS = 0.005
            Assert.IsFalse(VoiceMath.IsVoiced(quiet, 0.012f));
            Assert.IsTrue(VoiceMath.IsVoiced(quiet, 0.001f));
        }

        [Test]
        public void ResampleLinear_EqualRates_IsExactCopy()
        {
            float[] src = { 0.1f, 0.2f, 0.3f, 0.4f };
            float[] dst = new float[4];
            int n = VoiceMath.ResampleLinear(src, 16000, dst, 16000);
            Assert.AreEqual(4, n);
            CollectionAssert.AreEqual(src, dst);
        }

        [Test]
        public void ResampleLinear_Downsample_HalvesLength()
        {
            // 48000 → 16000 is a 3:1 decimation: 6 samples → 2.
            float[] src = { 0f, 0.3f, 0.6f, 0.9f, 0.6f, 0.3f };
            float[] dst = new float[8];
            int n = VoiceMath.ResampleLinear(src, 48000, dst, 16000);
            Assert.AreEqual(2, n);
            Assert.AreEqual(0f, dst[0], 1e-5f);    // pos 0 → src[0]
            Assert.AreEqual(0.9f, dst[1], 1e-5f);  // pos 3 → src[3]
        }

        [Test]
        public void ResampleLinear_RespectsDestinationCapacity()
        {
            float[] src = { 0f, 1f, 0f, 1f, 0f, 1f };
            float[] dst = new float[2];                 // smaller than the natural output
            int n = VoiceMath.ResampleLinear(src, 16000, dst, 16000);
            Assert.AreEqual(2, n);
        }

        [Test]
        public void ResampleLinear_EmptyOrInvalid_ReturnsZero()
        {
            Assert.AreEqual(0, VoiceMath.ResampleLinear(new float[0], 16000, new float[4], 16000));
            Assert.AreEqual(0, VoiceMath.ResampleLinear(new float[] { 1f }, 0, new float[4], 16000));
            Assert.AreEqual(0, VoiceMath.ResampleLinear(new float[] { 1f }, 16000, new float[4], 0));
        }
    }
}
