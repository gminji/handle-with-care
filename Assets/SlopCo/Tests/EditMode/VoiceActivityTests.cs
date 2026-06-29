using NUnit.Framework;
using SlopCo.Audio;

namespace SlopCo.Tests.EditMode
{
    /// <summary>
    /// Verifies the pure Voice Activity Indicator math (VoiceActivity) without any UnityEngine / NGO runtime.
    /// </summary>
    public class VoiceActivityTests
    {
        private const float Window = 0.35f;

        [Test]
        public void IsActive_NeverSpoke_IsFalse()
        {
            Assert.IsFalse(VoiceActivity.IsActive(0f, 10f, Window));
            Assert.IsFalse(VoiceActivity.IsActive(-1f, 10f, Window));
        }

        [Test]
        public void IsActive_JustSpoke_IsTrue()
        {
            Assert.IsTrue(VoiceActivity.IsActive(10f, 10f, Window));        // now == lastVoiced
        }

        [Test]
        public void IsActive_JustInsideWindow_IsTrue()
        {
            // Just inside the debounce window → still active. (Exact-boundary float equality is unreliable;
            // the contract is "within the window", so test just-inside and just-outside instead.)
            Assert.IsTrue(VoiceActivity.IsActive(10f, 10f + Window - 0.01f, Window));
        }

        [Test]
        public void IsActive_PastWindow_IsFalse()
        {
            Assert.IsFalse(VoiceActivity.IsActive(10f, 10f + Window + 0.01f, Window));
        }

        [Test]
        public void Intensity_NeverSpoke_IsZero()
        {
            Assert.That(VoiceActivity.Intensity(0f, 5f, Window), Is.EqualTo(0f));
            Assert.That(VoiceActivity.Intensity(-3f, 5f, Window), Is.EqualTo(0f));
        }

        [Test]
        public void Intensity_AtStamp_IsFull()
        {
            Assert.That(VoiceActivity.Intensity(10f, 10f, Window), Is.EqualTo(1f));
        }

        [Test]
        public void Intensity_PastWindow_IsZero()
        {
            Assert.That(VoiceActivity.Intensity(10f, 10f + Window, Window), Is.EqualTo(0f));
            Assert.That(VoiceActivity.Intensity(10f, 10f + Window + 1f, Window), Is.EqualTo(0f));
        }

        [Test]
        public void Intensity_MidWindow_DecaysLinearly()
        {
            // Halfway through the window → ~0.5 (linear ramp down).
            float v = VoiceActivity.Intensity(10f, 10f + Window * 0.5f, Window);
            Assert.That(v, Is.EqualTo(0.5f).Within(1e-4f));
        }

        [Test]
        public void Intensity_IsMonotonicNonIncreasing()
        {
            const float spoke = 0.0001f; // lastVoiced just above 0 so it counts as "spoke"
            float prev = VoiceActivity.Intensity(spoke, 0f, Window); // t<=0 → 1 (full), the true starting value
            for (int i = 1; i <= 10; i++)
            {
                float now = Window * (i / 10f);
                float v = VoiceActivity.Intensity(spoke, now, Window);
                Assert.That(v, Is.LessThanOrEqualTo(prev + 1e-5f), $"step {i}");
                prev = v;
            }
        }

        [Test]
        public void PulseScale_ZeroIntensity_IsExactlyBaseScale()
        {
            const float baseScale = 0.18f;
            // Any 'now' — with intensity 0 the pulse term vanishes.
            Assert.That(VoiceActivity.PulseScale(123.456f, 0f, baseScale, 0.25f, 6f),
                Is.EqualTo(baseScale).Within(1e-6f));
        }

        [Test]
        public void PulseScale_FullIntensity_StaysWithinAmplitudeBand()
        {
            const float baseScale = 0.18f;
            const float amp = 0.25f;
            const float speed = 6f;
            float lo = baseScale * (1f - amp);
            float hi = baseScale * (1f + amp);
            // Sample across a full period; every sample must stay in the band.
            for (int i = 0; i < 200; i++)
            {
                float now = i * 0.01f;
                float s = VoiceActivity.PulseScale(now, 1f, baseScale, amp, speed);
                Assert.That(s, Is.GreaterThanOrEqualTo(lo - 1e-5f).And.LessThanOrEqualTo(hi + 1e-5f), $"sample {i}");
            }
        }

        [Test]
        public void PulseScale_IsDeterministic()
        {
            float a = VoiceActivity.PulseScale(42f, 0.7f, 0.18f, 0.25f, 6f);
            float b = VoiceActivity.PulseScale(42f, 0.7f, 0.18f, 0.25f, 6f);
            Assert.That(a, Is.EqualTo(b));
        }
    }
}
