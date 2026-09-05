using CUC260905.Interaction;
using DG.Tweening;
using UnityEngine;

namespace CUC260905.Visual
{
    /// <summary>
    /// 节点悬浮反馈：指针进入节点根时把节点整体放大（默认 20%），离开时恢复，
    /// 缓动曲线 easeOutBack，动画时长 0.2s。
    /// 挂在节点根上，实现 IHoverable，由同物体的 CapabilitySinkAdapter 转发悬浮意图。
    /// 与 NodeEntranceAnimation 同物体配合：悬浮接管根 scale 前先打断入场动画，
    /// 避免两个缓动同时驱动同一 Transform 相互覆盖。
    /// 使用 DOTween 默认缩放时间（尊重 Time.timeScale），与工程"暂停冻结模拟时间"一致。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NodeHoverScaleFeedback : MonoBehaviour, IHoverable
    {
        [SerializeField, Min(1.0f), Tooltip("悬浮时相对基准的整体 scale 倍数，默认 1.2（放大 20%）。")]
        private float mHoverScaleFactor = 1.2f;
        [SerializeField, Min(0.01f), Tooltip("悬浮放大/离开恢复动画时长（秒），默认 0.2s。")]
        private float mDuration = 0.2f;
        [SerializeField, Tooltip("悬浮/恢复缓动曲线，默认 easeOutBack。")]
        private Ease mEase = Ease.OutBack;

        private Vector3 mBaseScale;
        private NodeEntranceAnimation mEntranceAnimation;
        private Tweener mTweener;

        private void Awake()
        {
            // 基准取 prefab/场景设定的根 scale，悬浮在其上乘系数（支持原始缩放非 1 的节点）。
            mBaseScale = transform.localScale;
            mEntranceAnimation = GetComponent<NodeEntranceAnimation>();
        }

        /// <summary>悬浮入口：进入放大到基准 * 系数，离开恢复基准。</summary>
        public InteractionResult OnHover(in HoverIntent intent)
        {
            Vector3 target = intent.Phase == HoverPhase.Enter
                ? mBaseScale * mHoverScaleFactor
                : mBaseScale;
            AnimateTo(target);
            return new InteractionResult(InteractionResultStatus.Handled);
        }

        private void AnimateTo(Vector3 target)
        {
            mTweener?.Kill();
            // 悬浮接管根 scale 时先打断入场动画，避免两个缓动争夺同一属性。
            mEntranceAnimation?.KillTween();
            mTweener = transform.DOScale(target, mDuration).SetEase(mEase);
        }

        private void OnDestroy()
        {
            mTweener?.Kill();
            mTweener = null;
        }
    }
}
