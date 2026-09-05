using UnityEngine;

namespace CUC260905.Interaction
{
    /// <summary>标记允许在模拟暂停时继续执行的拖拽能力。</summary>
    public interface IPauseAllowedDrag
    {
    }

    /// <summary>对象响应点击意图的能力。</summary>
    public interface IClickable
    {
        InteractionResult OnClick(in ClickIntent intent);
    }

    /// <summary>对象响应拖拽生命周期意图的能力。</summary>
    public interface IDraggable
    {
        InteractionResult OnDrag(in DragIntent intent);
    }

    /// <summary>对象响应悬浮进入、离开意图的能力。</summary>
    public interface IHoverable
    {
        InteractionResult OnHover(in HoverIntent intent);
    }

    /// <summary>
    /// 将三种基础意图适配为对象能力调用。
    /// 业务组件实现 IClickable、IDraggable、IHoverable 即可，无需认识 Dispatcher 或 Sink Resolver。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(InteractionTarget))]
    public sealed class CapabilitySinkAdapter : MonoBehaviour,
        IIntentSink<ClickIntent>,
        IIntentSink<DragIntent>,
        IIntentSink<HoverIntent>,
        IPauseAllowedIntentSink
    {
        private IClickable mClickable;
        private IDraggable mDraggable;
        private IHoverable mHoverable;

        private void Awake()
        {
            RebuildCapabilities();
        }

        /// <summary>运行时替换能力组件后，由装配代码显式刷新缓存。</summary>
        public void RebuildCapabilities()
        {
            mClickable = null;
            mDraggable = null;
            mHoverable = null;

            // 同一对象可同时具备三类能力；每类取组件顺序中的第一个实现。
            MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour == this)
                {
                    continue;
                }

                if (behaviour is IClickable clickable)
                {
                    RegisterClickable(clickable);
                }

                if (behaviour is IDraggable draggable)
                {
                    RegisterDraggable(draggable);
                }

                if (behaviour is IHoverable hoverable)
                {
                    RegisterHoverable(hoverable);
                }
            }
        }

        /// <summary>Click 只委托给 IClickable，不在适配层决定具体操作。</summary>
        public InteractionResult Handle(IInteractionTarget target, in ClickIntent intent)
        {
            if (!isActiveAndEnabled || mClickable == null)
            {
                return new InteractionResult(InteractionResultStatus.SinkUnavailable);
            }

            return mClickable.OnClick(intent);
        }

        /// <summary>Drag 的 Begin、Update、End、Cancel 原样交由 IDraggable 处理。</summary>
        public InteractionResult Handle(IInteractionTarget target, in DragIntent intent)
        {
            if (!isActiveAndEnabled || mDraggable == null)
            {
                return new InteractionResult(InteractionResultStatus.SinkUnavailable);
            }

            return mDraggable.OnDrag(intent);
        }

        /// <summary>Hover 的 Enter、Exit 原样交由 IHoverable 处理。</summary>
        public InteractionResult Handle(IInteractionTarget target, in HoverIntent intent)
        {
            if (!isActiveAndEnabled || mHoverable == null)
            {
                return new InteractionResult(InteractionResultStatus.SinkUnavailable);
            }

            return mHoverable.OnHover(intent);
        }

        public bool CanHandleWhilePaused(System.Type intentType)
        {
            return intentType == typeof(DragIntent) && mDraggable is IPauseAllowedDrag;
        }

        private void RegisterClickable(IClickable clickable)
        {
            if (mClickable == null)
            {
                mClickable = clickable;
                return;
            }

            Debug.LogError($"{name} 上存在多个 IClickable。请在一个能力组件内显式组合点击行为。", this);
        }

        private void RegisterDraggable(IDraggable draggable)
        {
            if (mDraggable == null)
            {
                mDraggable = draggable;
                return;
            }

            Debug.LogError($"{name} 上存在多个 IDraggable。请在一个能力组件内显式组合拖拽行为。", this);
        }

        private void RegisterHoverable(IHoverable hoverable)
        {
            if (mHoverable == null)
            {
                mHoverable = hoverable;
                return;
            }

            Debug.LogError($"{name} 上存在多个 IHoverable。请在一个能力组件内显式组合悬浮行为。", this);
        }
    }
}
