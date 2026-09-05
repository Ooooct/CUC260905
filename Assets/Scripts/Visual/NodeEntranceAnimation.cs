using DG.Tweening;
using UnityEngine;

namespace CUC260905.Visual
{
    /// <summary>
    /// 节点入场动画：节点放置/出现时，根节点整体 scale 从约 0.5 倍缓动到 1.0 倍。
    /// 缓动曲线使用 easeOutBack，0.5s 播放完；挂在节点根上，Start 时触发一次。
    /// 以 Awake 时刻的 localScale 为基准（支持 prefab 原始缩放非 1 的情况），
    /// 动画在其上乘系数：start = base * mStartScaleFactor，end = base。
    /// 使用 DOTween 默认缩放时间（尊重 Time.timeScale），与工程"暂停冻结模拟时间"一致。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NodeEntranceAnimation : MonoBehaviour
    {
        [SerializeField, Min(0.01f), Tooltip("入场起始整体 scale 倍数（相对基准），默认约 0.5。")]
        private float mStartScaleFactor = 0.5f;
        [SerializeField, Min(0.01f), Tooltip("入场动画时长（秒），默认 0.5s。")]
        private float mDuration = 0.5f;
        [SerializeField, Tooltip("入场缓动曲线，默认 easeOutBack。")]
        private Ease mEase = Ease.OutBack;

        private Vector3 mBaseScale;
        private Tweener mTweener;

        private void Awake()
        {
            // 基准取 prefab/场景设定的根 scale，动画在其上乘系数。
            mBaseScale = transform.localScale;
        }

        // Start 晚于 Awake；场景预置节点与实例化节点都会在出现后第一时间播放一次。
        private void Start()
        {
            Play();
        }

        /// <summary>播放入场动画（scale 从基准 * 起始系数 → 基准，easeOutBack）。可手动再次触发。</summary>
        public void Play()
        {
            mTweener?.Kill();
            transform.localScale = mBaseScale * mStartScaleFactor;
            mTweener = transform.DOScale(mBaseScale, mDuration).SetEase(mEase);
        }

        /// <summary>打断当前入场动画；由更优先的 scale 动画（如悬浮反馈）接管根缩放时调用。</summary>
        public void KillTween()
        {
            mTweener?.Kill();
            mTweener = null;
        }

        private void OnDestroy()
        {
            mTweener?.Kill();
            mTweener = null;
        }
    }
}
