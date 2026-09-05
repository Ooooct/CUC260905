using UnityEngine;

namespace CUC260905.Network
{
    /// <summary>
    /// 用户节点散点生成的纯数据参数（不引用表现层，保证算法可脱离 Unity 对象做单元测试）。
    /// 语义：生成数量落在 [MinCount, MaxCount] 之间的随机用户节点，
    /// 任意两点（含与服务器节点）世界距离不小于 MinDistance；
    /// 采样范围从 [InnerRadius, InnerRadius] 起始、随已生成数量面积线性外扩到
    /// [InnerRadius, RangeRadius]，实现"从中心逐步外扩"。构造时对所有数值做钳制与归序。
    /// </summary>
    public readonly struct UserNodeScatterConfig
    {
        /// <summary>候选点距原点允许的最大距离（圆盘外半径）。</summary>
        public readonly float RangeRadius;

        /// <summary>任意两个候选点之间的最小允许距离。</summary>
        public readonly float MinDistance;

        /// <summary>候选点距原点允许的最小距离（圆盘内半径，用于避开原点处的中心对象）。</summary>
        public readonly float InnerRadius;

        /// <summary>候选点数量随机下限（含）。</summary>
        public readonly int MinCount;

        /// <summary>候选点数量随机上限（含）。</summary>
        public readonly int MaxCount;

        /// <summary>常用默认参数：半径 10、最小距离 0.5、中心留空 0.5、数量 40–80。</summary>
        public static UserNodeScatterConfig Default
        {
            get
            {
                return new UserNodeScatterConfig(10.0f, 0.5f, 0.5f, 40, 80);
            }
        }

        public UserNodeScatterConfig(
            float rangeRadius,
            float minDistance,
            float innerRadius,
            int minCount,
            int maxCount)
        {
            RangeRadius = Mathf.Max(0.01f, rangeRadius);
            MinDistance = Mathf.Max(0.01f, minDistance);
            InnerRadius = Mathf.Clamp(innerRadius, 0.0f, RangeRadius);
            MinCount = Mathf.Max(0, minCount);
            MaxCount = Mathf.Max(MinCount, maxCount);
        }

        /// <summary>是否能容纳至少一个候选点（内半径严格小于外半径）。</summary>
        public bool CanHoldAnyPoint
        {
            get { return InnerRadius < RangeRadius; }
        }
    }
}
