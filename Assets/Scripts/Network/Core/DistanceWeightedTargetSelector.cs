using System;
using System.Collections.Generic;
using UnityEngine;

namespace CUC260905.Network
{
    /// <summary>
    /// 随机发送目标的距离加权选取（纯逻辑，不依赖模型与表现层，可独立单元测试）。
    ///
    /// 权重曲线为对数正态分布密度 f(d; μ, σ) = exp(−(ln d − μ)² / 2σ²) / (d·σ·√(2π))，
    /// 参数取 μ=0.3、σ=1.0，使峰值（众数 e^(μ−σ²) ≈ 0.497）贴近节点最小间距 0.5：
    /// 在游戏的典型距离区间（0.5–16）上权重随距离增大而单调递减，即
    /// "距离越近的目标权重越大、被选中概率越高"的对数正态左尾曲线。
    ///
    /// 有效选取权重 = max(MinWeight, 密度)：对数正态密度低于 0.05 的远处目标按 0.05 计，
    /// 以平坦下限保留其真实选中机会，避免最近目标完全主导、远端目标被边缘化。
    /// 选取时按权重比例做随机抽样（累计权重 + 均匀随机）。任一候选节点位置缺失
    /// （例如测试环境未注册 INodePositionProvider）时回退为历史均匀随机，保证兼容。
    /// </summary>
    public static class DistanceWeightedTargetSelector
    {
        /// <summary>对数正态分布的对数均值 μ；与 σ 共同决定峰值位置与衰减快慢。</summary>
        public const float LogMean = 0.3f;

        /// <summary>对数正态分布的对数标准差 σ；数值越小，近距离目标越占优。</summary>
        public const float LogSigma = 1.0f;

        /// <summary>
        /// 参与权重计算的最近距离下限，与 UserNodeScatterConfig.Default 的最小间距一致；
        /// 距离小于该值（含两点重合的 d=0）时按该值取权重，避免对数在 0 处发散。
        /// </summary>
        public const float MinDistance = 0.5f;

        /// <summary>
        /// 有效权重下限：对数正态密度低于该值的远处目标按该值计权重（平坦尾部），
        /// 保证远端用户节点仍保有可感知的选中概率。
        /// </summary>
        public const float MinWeight = 0.05f;

        /// <summary>对数正态密度权重：距离越小权重越大，且恒为正、有限。</summary>
        public static float Weight(float distance)
        {
            float clampedDistance = Mathf.Max(MinDistance, distance);
            float logDistance = Mathf.Log(clampedDistance);
            float squaredDeviation = (logDistance - LogMean) * (logDistance - LogMean);
            float exponent = -squaredDeviation / (2f * LogSigma * LogSigma);
            return Mathf.Exp(exponent) / (clampedDistance * LogSigma * Mathf.Sqrt(2f * Mathf.PI));
        }

        /// <summary>
        /// 实际用于抽样的有效权重：对数正态密度与最低权重的较大值。
        /// 距离较近时等于密度；距离较远、密度跌破 0.05 时固定为 0.05。
        /// </summary>
        public static float SelectionWeight(float distance)
        {
            return Mathf.Max(MinWeight, Weight(distance));
        }

        /// <summary>
        /// 在候选节点间按"源点到候选点的距离权重"做加权随机抽样。
        /// 候选列表为空返回 null；任一候选位置未知（positionOf 返回 null）或权重全零时
        /// 回退均匀随机，与旧实现语义一致。
        /// </summary>
        public static string Select(
            System.Random random,
            IReadOnlyList<string> nodeIds,
            Vector3 sourcePosition,
            Func<string, Vector3?> positionOf)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            if (nodeIds == null)
            {
                throw new ArgumentNullException(nameof(nodeIds));
            }

            if (positionOf == null)
            {
                throw new ArgumentNullException(nameof(positionOf));
            }

            if (nodeIds.Count == 0)
            {
                return null;
            }

            List<KeyValuePair<string, float>> weightedCandidates =
                new List<KeyValuePair<string, float>>(nodeIds.Count);
            for (int index = 0; index < nodeIds.Count; index++)
            {
                string nodeId = nodeIds[index];
                Vector3? candidatePosition = positionOf(nodeId);
                if (candidatePosition == null)
                {
                    // 位置不可用：无法计算距离权重，整体回退均匀随机。
                    return nodeIds[random.Next(nodeIds.Count)];
                }

                float distance = Vector2.Distance(
                    new Vector2(sourcePosition.x, sourcePosition.y),
                    new Vector2(candidatePosition.Value.x, candidatePosition.Value.y));
                weightedCandidates.Add(
                    new KeyValuePair<string, float>(nodeId, SelectionWeight(distance)));
            }

            float[] weights = new float[weightedCandidates.Count];
            for (int index = 0; index < weightedCandidates.Count; index++)
            {
                weights[index] = weightedCandidates[index].Value;
            }

            int selectedIndex = SelectIndex(random, weights);
            return selectedIndex < 0 ? null : weightedCandidates[selectedIndex].Key;
        }

        /// <summary>
        /// 按权重比例抽取下标：权重非负（负值钳为 0），总和非正时回退均匀；
        /// 权重列表为空返回 -1。
        /// </summary>
        public static int SelectIndex(System.Random random, IReadOnlyList<float> weights)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            if (weights == null)
            {
                throw new ArgumentNullException(nameof(weights));
            }

            if (weights.Count == 0)
            {
                return -1;
            }

            float totalWeight = 0f;
            for (int index = 0; index < weights.Count; index++)
            {
                totalWeight += Mathf.Max(0f, weights[index]);
            }

            if (totalWeight <= 0f)
            {
                return random.Next(weights.Count);
            }

            double sample = random.NextDouble() * totalWeight;
            float cumulativeWeight = 0f;
            for (int index = 0; index < weights.Count; index++)
            {
                cumulativeWeight += Mathf.Max(0f, weights[index]);
                if (sample < cumulativeWeight)
                {
                    return index;
                }
            }

            return weights.Count - 1;
        }
    }
}
