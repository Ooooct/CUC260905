using UnityEngine;

namespace CUC260905.Network
{
    /// <summary>
    /// 用户节点生成节奏器（纯逻辑、可单测，不依赖 Unity 生命周期）。
    /// 持有"下一次生成时间"与已生成节拍数：到点后推进一个生成节拍，
    /// 间隔在 [IntervalMin, IntervalMax] 内随机采样；计划数量耗尽后不再消耗。
    /// 生成位置由 UserNodeScatterGenerator 逐点产出，本节奏器只负责"何时生成"。
    /// </summary>
    public sealed class UserNodeSpawnScheduler
    {
        private readonly float mIntervalMin;
        private readonly float mIntervalMax;
        private readonly System.Random mRandom;
        private int mSpawnedCount;
        private double mNextSpawnAt;

        public UserNodeSpawnScheduler(float intervalMin, float intervalMax, System.Random random)
        {
            mIntervalMin = Mathf.Max(0.01f, Mathf.Min(intervalMin, intervalMax));
            mIntervalMax = Mathf.Max(0.01f, Mathf.Max(intervalMin, intervalMax));
            mRandom = random ?? new System.Random();
            mSpawnedCount = 0;
            mNextSpawnAt = 0.0;
        }

        /// <summary>已推进的生成节拍数量。</summary>
        public int SpawnedCount
        {
            get { return mSpawnedCount; }
        }

        /// <summary>下一次允许生成的时间点。</summary>
        public double NextSpawnAt
        {
            get { return mNextSpawnAt; }
        }

        /// <summary>是否已把全部计划节拍消耗完毕。</summary>
        public bool IsExhausted(int candidateCount)
        {
            return mSpawnedCount >= candidateCount;
        }

        /// <summary>重置节奏：从 now 起首个生成节拍落在 [IntervalMin, IntervalMax] 内。</summary>
        public void Reset(double now)
        {
            mSpawnedCount = 0;
            mNextSpawnAt = now + SampleInterval();
        }

        /// <summary>
        /// 立即推进一个生成节拍，并从当前时刻开始安排后续随机间隔。
        /// 用于首个服务器建成后立刻生成首个用户节点；后续节奏仍与普通生成一致。
        /// </summary>
        public bool TryConsumeImmediately(double now, int candidateCount, out int index)
        {
            index = -1;
            if (mSpawnedCount >= candidateCount)
            {
                return false;
            }

            index = mSpawnedCount;
            mSpawnedCount++;
            mNextSpawnAt = now + SampleInterval();
            return true;
        }

        /// <summary>
        /// 到达下一次生成时间且未耗尽时，推进一个生成节拍并返回 true；否则返回 false（index 恒为 -1）。
        /// 输出的 index 为本次节拍的序号（严格按 0、1、2… 递增），
        /// 配合"随序号逐步外扩"的逐点生成器即呈现"从中心逐步外移"。
        /// </summary>
        public bool TryConsume(double now, int candidateCount, out int index)
        {
            index = -1;
            if (mSpawnedCount >= candidateCount)
            {
                return false;
            }

            if (now < mNextSpawnAt)
            {
                return false;
            }

            index = mSpawnedCount;
            mSpawnedCount++;
            mNextSpawnAt = now + SampleInterval();
            return true;
        }

        private double SampleInterval()
        {
            double t = mRandom.NextDouble();
            return mIntervalMin + t * (mIntervalMax - mIntervalMin);
        }
    }
}
