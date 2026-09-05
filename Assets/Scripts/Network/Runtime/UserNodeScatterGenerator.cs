using System;
using System.Collections.Generic;
using UnityEngine;

namespace CUC260905.Network
{
    /// <summary>
    /// 用户节点散点增量生成器（纯逻辑、有状态，可单测，不依赖 Unity 生命周期）。
    /// 与旧版"启动时一次性生成全部候选点"不同：本生成器逐点产出——
    /// Reset 时只采样目标数量（一个随机数，不预生成任何位置），
    /// 之后每次 TryGenerateNextPoint 在当前"已外扩到的半径"内采样一个点：
    ///   1. 采样圆盘为 [InnerRadius, 当前外半径]；当前外半径按半径平方 = t^GrowthExponent 增长（指数 >1 使中心环带更密、整体更紧凑）；
    ///      t 为“已生成数量占比”，随生成逐点从 InnerRadius 外扩到 RangeRadius（最后一点恰好落在 RangeRadius），从中心逐步外扩；InnerRadius 默认 0，允许填充圆心空余；
    ///      中心更密集、边缘更稀疏的紧凑布局，同时保持用户点之间的 Best-Candidate 均匀性；
    ///   2. 每点采用 Best-Candidate（贪心最大最小距离）采样多个候选，
    ///      先淘汰与已生成用户点距离小于 MinDistance、或落入服务器节点 MinDistance 排斥区的候选
    ///      （服务器节点只做"不进入其 0.5 单位内"的硬性排斥，不参与分布平均性的择优），
    ///      再选"距已生成用户点最远"的候选（蓝噪声级均匀）；
    ///   3. 当前增长圆盘已饱和（找不到合法候选）时，回退到全量圆盘 [InnerRadius, RangeRadius]
    ///      再补救一次，仍失败则判定域饱和，停止后续生成（返回已达成的数量）。
    /// 同一随机种子下生成序列完全确定。
    /// </summary>
    public sealed class UserNodeScatterGenerator
    {
        /// <summary>每点常规候选采样数；越大越平均，计算量越高。</summary>
        private const int CandidateAttemptsPerPoint = 30;

        /// <summary>外扩生长指数：半径平方随已生成数量占比按 t^GrowthExponent 增长；>1 使中心更密集紧凑，1 为面积均匀（旧行为）。</summary>
        private const float GrowthExponent = 1.5f;

        /// <summary>常规采样未命中时，饱和前的加大扫描次数，减少域将满时的过早停止。</summary>
        private const int FinalSweepAttempts = 128;

        private readonly UserNodeScatterConfig mConfig;
        private readonly System.Random mRandom;
        private readonly List<Vector2> mGeneratedPoints = new List<Vector2>();
        private int mTargetCount;
        private bool mSaturated;
        private bool mInitialized;

        public UserNodeScatterGenerator(UserNodeScatterConfig config, System.Random random)
        {
            mConfig = config;
            mRandom = random ?? throw new ArgumentNullException(nameof(random));
        }

        /// <summary>计划生成的总点数，Reset 时在 [MinCount, MaxCount] 内随机采样（含）。</summary>
        public int TargetCount
        {
            get { return mTargetCount; }
        }

        /// <summary>已经生成（产出）的用户节点数量。</summary>
        public int GeneratedCount
        {
            get { return mGeneratedPoints.Count; }
        }

        /// <summary>是否还可以继续生成（未达计划数量且域未饱和）。</summary>
        public bool CanGenerateMore
        {
            get { return mInitialized && !mSaturated && mGeneratedPoints.Count < mTargetCount; }
        }

        /// <summary>已生成点的只读快照（按生成顺序），供状态显示或测试读取。</summary>
        public IReadOnlyList<Vector2> GeneratedPoints
        {
            get { return mGeneratedPoints; }
        }

        /// <summary>重置生成状态：清空已生成点，并重新随机采样计划总数。</summary>
        public void Reset()
        {
            mGeneratedPoints.Clear();
            mSaturated = false;
            mTargetCount = mConfig.CanHoldAnyPoint
                ? mRandom.Next(mConfig.MinCount, mConfig.MaxCount + 1)
                : 0;
            mInitialized = true;
        }

        /// <summary>
        /// 生成下一个用户节点位置；服务器节点位置作为硬性排斥区传入
        /// （仅保证新点不进入服务器 MinDistance 范围，不参与分布择优）。
        /// 成功时返回 true 并输出 position；计划数量已达成、域饱和或尚未 Reset 时返回 false。
        /// </summary>
        public bool TryGenerateNextPoint(IReadOnlyList<Vector2> serverPositions, out Vector2 point)
        {
            point = default;
            if (!CanGenerateMore)
            {
                return false;
            }

            float outerRadius = CurrentOuterRadius();
            if (!TryPickBestCandidate(outerRadius, serverPositions, out point))
            {
                // 当前增长圆盘已饱和：放宽到全量圆盘补救一次，减少域将满时的过早停止；
                // 仍失败则整个域饱和，停止后续生成。
                if (!TryPickBestCandidate(mConfig.RangeRadius, serverPositions, out point))
                {
                    mSaturated = true;
                    return false;
                }
            }

            mGeneratedPoints.Add(point);
            return true;
        }

        /// <summary>
        /// 当前增长圆盘的外半径：半径平方随已生成数量占比 t 按 t^GrowthExponent 增长
        /// （指数 >1 → 中心环带更密、整体更紧凑；指数 1 为面积均匀的旧行为）。
        /// </summary>
        private float CurrentOuterRadius()
        {
            float innerSqr = mConfig.InnerRadius * mConfig.InnerRadius;
            float rangeSqr = mConfig.RangeRadius * mConfig.RangeRadius;
            float t = (float)(mGeneratedPoints.Count + 1) / mTargetCount;
            float tt = Mathf.Pow(Mathf.Clamp01(t), GrowthExponent);
            return Mathf.Sqrt(innerSqr + (rangeSqr - innerSqr) * tt);
        }

        /// <summary>
        /// 在 [InnerRadius, outerRadius] 圆盘内采样多个候选，
        /// 淘汰"距已生成用户点小于 MinDistance"或"落入服务器排斥区"的候选，
        /// 返回"距已生成用户点最远"且满足约束的一个；找不到任何合法候选时返回 false。
        /// 服务器节点只做 MinDistance 硬性排斥（不进入其 0.5 单位内），不参与"最远"择优，
        /// 避免在服务器周围产生比最小距离更大的空洞。
        /// </summary>
        private bool TryPickBestCandidate(
            float outerRadius,
            IReadOnlyList<Vector2> serverPositions,
            out Vector2 best)
        {
            best = default;
            bool found = false;
            float bestMinSqr = -1.0f;
            float minDistanceSqr = mConfig.MinDistance * mConfig.MinDistance;

            int attempts = 0;
            while (attempts < CandidateAttemptsPerPoint + FinalSweepAttempts)
            {
                attempts++;
                Vector2 candidate = RandomAnnulusPoint(mConfig.InnerRadius, outerRadius, mRandom);
                if (IsTooCloseToServers(candidate, serverPositions, minDistanceSqr))
                {
                    continue;
                }

                float minSqrToUsers = MinDistanceSqrToGenerated(candidate);
                if (minSqrToUsers < minDistanceSqr)
                {
                    continue;
                }

                if (minSqrToUsers > bestMinSqr)
                {
                    bestMinSqr = minSqrToUsers;
                    best = candidate;
                    found = true;
                }

                // 常规采样一旦命中即提前结束；仅当常规采样全灭时才进入加大扫描。
                if (attempts == CandidateAttemptsPerPoint && found)
                {
                    break;
                }
            }

            return found;
        }

        /// <summary>候选点到已生成用户点的最小距离平方（分布择优的依据，O(n)，n 为已生成点数）。</summary>
        private float MinDistanceSqrToGenerated(Vector2 candidate)
        {
            float minSqr = float.MaxValue;
            for (int i = 0; i < mGeneratedPoints.Count; i++)
            {
                float squared = (candidate - mGeneratedPoints[i]).sqrMagnitude;
                if (squared < minSqr)
                {
                    minSqr = squared;
                }
            }

            return minSqr;
        }

        /// <summary>候选点是否落入任一服务器节点的 MinDistance 排斥区（硬性约束，不参与择优）。</summary>
        private static bool IsTooCloseToServers(
            Vector2 candidate,
            IReadOnlyList<Vector2> serverPositions,
            float minDistanceSqr)
        {
            for (int i = 0; i < serverPositions.Count; i++)
            {
                if ((candidate - serverPositions[i]).sqrMagnitude < minDistanceSqr)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>在 [inner, radius] 圆环内按面积均匀采样一点。</summary>
        private static Vector2 RandomAnnulusPoint(float inner, float radius, System.Random random)
        {
            float angle = (float)random.NextDouble() * Mathf.PI * 2.0f;
            float innerSqr = inner * inner;
            float radiusSqr = radius * radius;
            float squared = innerSqr + (float)random.NextDouble() * (radiusSqr - innerSqr);
            float length = Mathf.Sqrt(squared);
            return new Vector2(Mathf.Cos(angle) * length, Mathf.Sin(angle) * length);
        }
    }
}
