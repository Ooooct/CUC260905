using System;
using NUnit.Framework;

namespace CUC260905.Network.Tests
{
    /// <summary>
    /// SendPaceCurve（基于发送次数的线性增长曲线）的纯逻辑 EditMode 测试：
    /// 归一化进度、均值公式、单调性、随机抖动带、绝对钳位与可复现性。
    /// </summary>
    public sealed class SendPaceCurveTests
    {
        private const float Tolerance = 0.0005f;

        [Test]
        public void GrowthT_AtZeroSendCount_IsZero()
        {
            Assert.That(SendPaceCurve.GrowthT(0, 150), Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void GrowthT_ReferenceValues_MatchLinearCurve()
        {
            Assert.That(SendPaceCurve.GrowthT(1, 150), Is.EqualTo(1f / 150f).Within(Tolerance));
            Assert.That(SendPaceCurve.GrowthT(15, 150), Is.EqualTo(0.1f).Within(Tolerance));
            Assert.That(SendPaceCurve.GrowthT(75, 150), Is.EqualTo(0.5f).Within(Tolerance));
            Assert.That(SendPaceCurve.GrowthT(100, 150), Is.EqualTo(2f / 3f).Within(Tolerance));
        }

        [Test]
        public void GrowthT_AtSaturationCount_IsOne()
        {
            Assert.That(SendPaceCurve.GrowthT(150, 150), Is.EqualTo(1f).Within(Tolerance));
        }

        [Test]
        public void GrowthT_BeyondSaturationCount_IsClampedToOne()
        {
            Assert.That(SendPaceCurve.GrowthT(150, 150), Is.EqualTo(1f));
            Assert.That(SendPaceCurve.GrowthT(300, 150), Is.EqualTo(1f));
            Assert.That(SendPaceCurve.GrowthT(10000, 150), Is.EqualTo(1f));
        }

        [Test]
        public void GrowthT_IsMonotonicNonDecreasingUpToSaturation()
        {
            float previous = SendPaceCurve.GrowthT(0, 150);
            for (int count = 1; count <= 150; count++)
            {
                float progress = SendPaceCurve.GrowthT(count, 150);
                Assert.That(progress, Is.GreaterThanOrEqualTo(previous));
                previous = progress;
            }
        }

        [Test]
        public void GrowthT_InvalidParameters_ReturnsZero()
        {
            Assert.That(SendPaceCurve.GrowthT(10, 0), Is.EqualTo(0f));
            Assert.That(SendPaceCurve.GrowthT(10, -1), Is.EqualTo(0f));
        }

        [Test]
        public void MeanPacketSize_AtZero_IsBaseMean()
        {
            Assert.That(SendPaceCurve.MeanPacketSize(0, 20f, 40f, 150), Is.EqualTo(20f).Within(Tolerance));
        }

        [Test]
        public void MeanPacketSize_AtSaturation_IsCeilingMean()
        {
            Assert.That(SendPaceCurve.MeanPacketSize(150, 20f, 40f, 150), Is.EqualTo(40f).Within(Tolerance));
            Assert.That(SendPaceCurve.MeanPacketSize(300, 20f, 40f, 150), Is.EqualTo(40f).Within(Tolerance));
        }

        [Test]
        public void MeanPacketSize_IsMonotonicNonDecreasing()
        {
            float previous = SendPaceCurve.MeanPacketSize(0, 20f, 40f, 150);
            for (int count = 1; count <= 300; count++)
            {
                float mean = SendPaceCurve.MeanPacketSize(count, 20f, 40f, 150);
                Assert.That(mean, Is.GreaterThanOrEqualTo(previous));
                previous = mean;
            }
        }

        [Test]
        public void MeanPacketSize_HalfSaturation_IsMidpoint()
        {
            Assert.That(SendPaceCurve.MeanPacketSize(75, 20f, 40f, 150), Is.EqualTo(30f).Within(Tolerance));
        }

        [Test]
        public void SamplePacketSize_ZeroJitter_ReturnsClampedMean()
        {
            // jitter=0 时无随机波动，返回曲线均值（默认参数下无需钳位）。
            float sample = SendPaceCurve.SamplePacketSize(
                new System.Random(1), 100, 20f, 40f, 150, 0f, 5f, 60f);
            float mean = SendPaceCurve.MeanPacketSize(100, 20f, 40f, 150);
            Assert.That(sample, Is.EqualTo(mean).Within(Tolerance));
        }

        [Test]
        public void SamplePacketSize_WithJitter_StaysWithinRelativeBand()
        {
            // count=100 → 线性均值 ≈ 33.333；jitter=0.25 → 乘性带 [均值×0.75, 均值×1.25]，
            // 该带远在绝对钳位 [5, 60] 之内，应全部落带。
            float mean = SendPaceCurve.MeanPacketSize(100, 20f, 40f, 150);
            float bandMin = mean * 0.75f;
            float bandMax = mean * 1.25f;

            for (int seed = 0; seed < 2000; seed++)
            {
                float sample = SendPaceCurve.SamplePacketSize(
                    new System.Random(seed), 100, 20f, 40f, 150, 0.25f, 5f, 60f);
                Assert.That(sample, Is.InRange(bandMin - 0.001f, bandMax + 0.001f));
            }
        }

        [Test]
        public void SamplePacketSize_ClampsToAbsoluteMax()
        {
            // 均值 200 × 抖动带 [100, 300] 整体超出绝对上限 60：所有样本都应钳到 60。
            for (int seed = 0; seed < 200; seed++)
            {
                float sample = SendPaceCurve.SamplePacketSize(
                    new System.Random(seed), 10, 200f, 200f, 150, 0.5f, 0f, 60f);
                Assert.That(sample, Is.EqualTo(60f));
            }
        }

        [Test]
        public void SamplePacketSize_DefaultParameters_NeverExceedSeventyFive()
        {
            // 默认参数（base 15、ceiling 50、jitter 0.25、max 75）下任何发送次数都不超 75Mb。
            for (int count = 0; count <= 600; count++)
            {
                for (int seed = 0; seed < 20; seed++)
                {
                    float sample = SendPaceCurve.SamplePacketSize(
                        new System.Random(seed), count, 15f, 50f, 300, 0.25f, 5f, 75f);
                    Assert.That(sample, Is.LessThanOrEqualTo(75f));
                }
            }
        }

        [Test]
        public void SamplePacketSize_SameSeed_IsDeterministic()
        {
            float first = SendPaceCurve.SamplePacketSize(
                new System.Random(42), 40, 20f, 40f, 150, 0.25f, 5f, 60f);
            float second = SendPaceCurve.SamplePacketSize(
                new System.Random(42), 40, 20f, 40f, 150, 0.25f, 5f, 60f);
            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void SamplePacketSize_NullRandom_Throws()
        {
            Assert.That(
                () => SendPaceCurve.SamplePacketSize(null, 1, 20f, 40f, 150, 0.25f, 5f, 60f),
                Throws.TypeOf<ArgumentNullException>());
        }
    }
}
