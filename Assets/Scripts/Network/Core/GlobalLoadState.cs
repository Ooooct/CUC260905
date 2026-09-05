using UnityEngine;

namespace CUC260905.Network
{
    /// <summary>总体负载的纯运行时状态；数值使用 0 到 1 的归一化范围。</summary>
    public sealed class GlobalLoadState
    {
        private float mNormalizedLoad;
        private bool mGameOver;

        public float NormalizedLoad
        {
            get { return mNormalizedLoad; }
        }

        public bool IsGameOver
        {
            get { return mGameOver; }
        }

        /// <summary>记录一次不可达惩罚；返回值表示本次是否首次达到失败阈值。</summary>
        public bool AddUnreachablePenalty(float normalizedPenalty)
        {
            float penalty = Mathf.Max(0.0f, normalizedPenalty);
            mNormalizedLoad = Mathf.Clamp01(mNormalizedLoad + penalty);
            if (mGameOver || mNormalizedLoad < 1.0f)
            {
                return false;
            }

            mGameOver = true;
            return true;
        }

        /// <summary>按每秒降低量衰减负载；游戏失败标记不会因视觉回落而复位。</summary>
        public bool Decay(float deltaTime, float normalizedDecreasePerSecond)
        {
            float decrease = Mathf.Max(0.0f, deltaTime) * Mathf.Max(0.0f, normalizedDecreasePerSecond);
            float nextLoad = Mathf.Clamp01(mNormalizedLoad - decrease);
            if (Mathf.Approximately(nextLoad, mNormalizedLoad))
            {
                return false;
            }

            mNormalizedLoad = nextLoad;
            return true;
        }
    }
}
