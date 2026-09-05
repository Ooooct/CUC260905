using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace CUC260905.Network.Tests
{
    /// <summary>
    /// DistanceWeightedTargetSelector 的纯逻辑 EditMode 测试：
    /// 对数正态权重公式、单调递减左尾、距离钳制、加权抽样与均匀回退。
    /// </summary>
    public sealed class DistanceWeightedTargetSelectorTests
    {
        private const float Tolerance = 0.003f;

        [Test]
        public void Weight_MatchesLogNormalDensityAtReferenceDistances()
        {
            // 参考值按 f(d; μ=0.3, σ=1.0) = exp(−(ln d − μ)²/2σ²)/(d·σ·√(2π)) 手算。
            Assert.That(DistanceWeightedTargetSelector.Weight(0.5f), Is.EqualTo(0.4873f).Within(Tolerance));
            Assert.That(DistanceWeightedTargetSelector.Weight(1f), Is.EqualTo(0.3814f).Within(Tolerance));
            Assert.That(DistanceWeightedTargetSelector.Weight(2f), Is.EqualTo(0.1846f).Within(Tolerance));
            Assert.That(DistanceWeightedTargetSelector.Weight(4f), Is.EqualTo(0.0553f).Within(Tolerance));
            Assert.That(DistanceWeightedTargetSelector.Weight(8f), Is.EqualTo(0.0102f).Within(Tolerance));
        }

        [Test]
        public void Weight_IsPositiveAndMonotonicallyDecreasingWithinGameRange()
        {
            float[] distances = { 0.5f, 1f, 2f, 4f, 8f, 16f };
            float previousWeight = DistanceWeightedTargetSelector.Weight(distances[0]);
            Assert.That(previousWeight, Is.GreaterThan(0f));

            for (int index = 1; index < distances.Length; index++)
            {
                float weight = DistanceWeightedTargetSelector.Weight(distances[index]);
                Assert.That(weight, Is.GreaterThan(0f));
                Assert.That(weight, Is.LessThanOrEqualTo(previousWeight));
                previousWeight = weight;
            }
        }

        [Test]
        public void Weight_ClampsZeroOrNegativeDistance_ToMinimumDistance()
        {
            float atMin = DistanceWeightedTargetSelector.Weight(DistanceWeightedTargetSelector.MinDistance);
            Assert.That(DistanceWeightedTargetSelector.Weight(0f), Is.EqualTo(atMin));
            Assert.That(DistanceWeightedTargetSelector.Weight(-1f), Is.EqualTo(atMin));
        }

        [Test]
        public void Select_EmptyCandidates_ReturnsNull()
        {
            List<string> empty = new List<string>();
            string selected = DistanceWeightedTargetSelector.Select(
                new System.Random(1),
                empty,
                Vector3.zero,
                _ => Vector3.zero);
            Assert.That(selected, Is.Null);
        }

        [Test]
        public void Select_UnknownPosition_FallsBackToUniform()
        {
            List<string> nodeIds = new List<string> { "a", "b", "c" };
            Dictionary<string, int> counts = new Dictionary<string, int> { { "a", 0 }, { "b", 0 }, { "c", 0 } };

            for (int seed = 0; seed < 600; seed++)
            {
                string selected = DistanceWeightedTargetSelector.Select(
                    new System.Random(seed),
                    nodeIds,
                    Vector3.zero,
                    _ => (Vector3?)null);
                counts[selected] = counts[selected] + 1;
            }

            Assert.That(counts["a"], Is.InRange(150, 250));
            Assert.That(counts["b"], Is.InRange(150, 250));
            Assert.That(counts["c"], Is.InRange(150, 250));
        }

        [Test]
        public void Select_EqualDistances_AreNearUniform()
        {
            List<string> nodeIds = new List<string> { "a", "b", "c" };
            Dictionary<string, int> counts = new Dictionary<string, int> { { "a", 0 }, { "b", 0 }, { "c", 0 } };

            for (int seed = 0; seed < 600; seed++)
            {
                string selected = DistanceWeightedTargetSelector.Select(
                    new System.Random(seed),
                    nodeIds,
                    Vector3.zero,
                    _ => new Vector3(1f, 0f, 0f));
                counts[selected] = counts[selected] + 1;
            }

            Assert.That(counts["a"], Is.InRange(150, 250));
            Assert.That(counts["b"], Is.InRange(150, 250));
            Assert.That(counts["c"], Is.InRange(150, 250));
        }

        [Test]
        public void Select_PrefersClosestTarget_AndSkewsByDistanceWeight()
        {
            // 源在原点；三候选距离均在对数正态密度高于下限 0.05 的区间内，
            // 以便纯密度权重主导：a=0.5（权重≈0.487），b=2（≈0.185），c=4（≈0.055）。
            // 理论占比约 a:b:c = 67% : 25% : 8%，最近者应明显主导。
            List<string> nodeIds = new List<string> { "a", "b", "c" };
            Dictionary<string, int> counts = new Dictionary<string, int> { { "a", 0 }, { "b", 0 }, { "c", 0 } };

            for (int seed = 0; seed < 2000; seed++)
            {
                string selected = DistanceWeightedTargetSelector.Select(
                    new System.Random(seed),
                    nodeIds,
                    Vector3.zero,
                    id => id == "a"
                        ? new Vector3(0.5f, 0f, 0f)
                        : id == "b"
                            ? new Vector3(2f, 0f, 0f)
                            : new Vector3(4f, 0f, 0f));
                counts[selected] = counts[selected] + 1;
            }

            Assert.That(counts["a"], Is.GreaterThan(counts["b"] + counts["c"]));
            Assert.That(counts["b"], Is.GreaterThan(counts["c"]));
            Assert.That(counts["b"], Is.GreaterThan(0));
            Assert.That(counts["c"], Is.GreaterThan(0));
        }

        [Test]
        public void SelectionWeight_FarDistances_AreFlooredAtMinWeight()
        {
            // 密度跌破 0.05 的远处目标（d=8 起）固定为下限；近处不受影响。
            Assert.That(DistanceWeightedTargetSelector.SelectionWeight(8f),
                Is.EqualTo(DistanceWeightedTargetSelector.MinWeight));
            Assert.That(DistanceWeightedTargetSelector.SelectionWeight(16f),
                Is.EqualTo(DistanceWeightedTargetSelector.MinWeight));
            Assert.That(DistanceWeightedTargetSelector.SelectionWeight(100f),
                Is.EqualTo(DistanceWeightedTargetSelector.MinWeight));
            Assert.That(DistanceWeightedTargetSelector.SelectionWeight(0.5f),
                Is.GreaterThan(DistanceWeightedTargetSelector.MinWeight));
        }

        [Test]
        public void SelectionWeight_IsNonIncreasingWithinGameRange()
        {
            // 有效权重 = max(下限, 单调递减密度) → 整体仍非增：近处递减，远处持平于下限。
            float[] distances = { 0.5f, 1f, 2f, 4f, 8f, 16f };
            float previousWeight = DistanceWeightedTargetSelector.SelectionWeight(distances[0]);
            for (int index = 1; index < distances.Length; index++)
            {
                float weight = DistanceWeightedTargetSelector.SelectionWeight(distances[index]);
                Assert.That(weight, Is.LessThanOrEqualTo(previousWeight));
                previousWeight = weight;
            }
        }

        [Test]
        public void Select_WithFloor_FarTargetStillHasFairChance()
        {
            // 权重下限的效果：a=0.5（0.487）对 c=16（密度 0.001，有效 0.05）。
            // 无下限时远端约 2000 次仅选中 4 次；有下限后续约 186 次（≈9.3%）。
            List<string> nodeIds = new List<string> { "a", "c" };
            Dictionary<string, int> counts = new Dictionary<string, int> { { "a", 0 }, { "c", 0 } };

            for (int seed = 0; seed < 2000; seed++)
            {
                string selected = DistanceWeightedTargetSelector.Select(
                    new System.Random(seed),
                    nodeIds,
                    Vector3.zero,
                    id => id == "a" ? new Vector3(0.5f, 0f, 0f) : new Vector3(16f, 0f, 0f));
                counts[selected] = counts[selected] + 1;
            }

            Assert.That(counts["a"], Is.GreaterThan(counts["c"]));
            Assert.That(counts["c"], Is.GreaterThan(50));
            Assert.That(counts["c"], Is.InRange(120, 260));
        }

        [Test]
        public void SelectIndex_AllZeroWeights_FallsBackToUniform()
        {
            float[] zeroWeights = { 0f, 0f, 0f };
            Dictionary<int, int> counts = new Dictionary<int, int> { { 0, 0 }, { 1, 0 }, { 2, 0 } };

            for (int seed = 0; seed < 600; seed++)
            {
                int index = DistanceWeightedTargetSelector.SelectIndex(new System.Random(seed), zeroWeights);
                counts[index] = counts[index] + 1;
            }

            Assert.That(counts[0], Is.InRange(150, 250));
            Assert.That(counts[1], Is.InRange(150, 250));
            Assert.That(counts[2], Is.InRange(150, 250));
        }
    }
}
