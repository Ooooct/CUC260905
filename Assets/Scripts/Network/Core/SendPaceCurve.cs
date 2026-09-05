using System;
using UnityEngine;

namespace CUC260905.Network
{
    /// <summary>
    /// 基于发送次数的线性增长曲线（纯逻辑，可独立单元测试）。
    /// 归一化进度 t(n) = clamp(n / N)，n 为该用户节点累计已发送次数：
    /// n=0 时为 0，n=N 时为 1，n≥N 后钳位为 1。
    /// 当前只驱动单包数据量增长。
    /// </summary>
    public static class SendPaceCurve
    {
        /// <summary>
        /// 归一化线性增长进度 t(n) ∈ [0, 1]。饱和次数小于 1 或发送次数非正时返回 0。
        /// </summary>
        public static float GrowthT(int sendCount, int saturationCount)
        {
            if (saturationCount < 1 || sendCount <= 0)
            {
                return 0f;
            }

            return Mathf.Clamp01((float)sendCount / saturationCount);
        }

        /// <summary>
        /// 发送次数 n 处的平均单包大小（Mb）：n=0 等于 baseMean，n≥N 后等于 ceilingMean，
        /// 全程随 n 单调不减（ceilingMean ≥ baseMean 时）。
        /// </summary>
        public static float MeanPacketSize(
            int sendCount,
            float baseMean,
            float ceilingMean,
            int saturationCount)
        {
            float progress = GrowthT(sendCount, saturationCount);
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
            float mean = MeanPacketSize(sendCount, baseMean, ceilingMean, saturationCount);
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
