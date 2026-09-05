using System;
using UnityEngine;

namespace CUC260905.Network
{
    /// <summary>
    /// 基于发送次数的类对数增长曲线（纯逻辑，可独立单元测试）。
    /// 归一化进度 t(n) = ln(1 + k·n) / ln(1 + k·N)，n 为该用户节点累计已发送次数：
    /// n=0 时为 0，前期随发送次数快速上升、后期趋缓，n≥N 后钳位为 1。
    /// 当前只驱动单包数据量增长；频率增长（发送间隔斜坡）后续接入时复用同一曲线。
    /// </summary>
    public static class SendPaceCurve
    {
        /// <summary>
        /// 归一化增长进度 t(n) ∈ [0, 1]。参数非法（曲率 ≤ 0 或饱和次数 < 1）时返回 0；
        /// 发送次数非正时返回 0，避免对数在 0 处发散。
        /// </summary>
        public static float GrowthT(int sendCount, float curvature, int saturationCount)
        {
            if (curvature <= 0f || saturationCount < 1 || sendCount <= 0)
            {
                return 0f;
            }

            float denominator = Mathf.Log(1f + curvature * saturationCount);
            if (denominator <= 0f)
            {
                return 0f;
            }

            float progress = Mathf.Log(1f + curvature * sendCount) / denominator;
            return Mathf.Clamp01(progress);
        }

        /// <summary>
        /// 发送次数 n 处的平均单包大小（Mb）：n=0 等于 baseMean，n≥N 后趋近 ceilingMean，
        /// 全程随 n 单调不减（ceilingMean ≥ baseMean 时）。
        /// </summary>
        public static float MeanPacketSize(
            int sendCount,
            float baseMean,
            float ceilingMean,
            float curvature,
            int saturationCount)
        {
            float progress = GrowthT(sendCount, curvature, saturationCount);
            return baseMean + (ceilingMean - baseMean) * progress;
        }

        /// <summary>
        /// 按曲线均值叠加固定比例随机抖动后采样单包大小（Mb），结果钳位到 [minAbs, maxAbs]。
        /// 抖动为乘性：实际值 = mean × (1 + jitter × U(−1, 1))，保证恒正且随机带随曲线缩放。
        /// jitter ≤ 0 时直接返回钳位后的均值。
        /// </summary>
        public static float SamplePacketSize(
            System.Random random,
            int sendCount,
            float baseMean,
            float ceilingMean,
            float curvature,
            int saturationCount,
            float jitter,
            float minAbs,
            float maxAbs)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            float min = Mathf.Min(minAbs, maxAbs);
            float max = Mathf.Max(minAbs, maxAbs);
            float mean = MeanPacketSize(sendCount, baseMean, ceilingMean, curvature, saturationCount);
            if (jitter <= 0f)
            {
                return Mathf.Clamp(mean, min, max);
            }

            float offset = jitter * (float)(random.NextDouble() * 2.0 - 1.0);
            float sampled = mean * (1f + offset);
            return Mathf.Clamp(sampled, min, max);
        }
    }
}
