using UnityEngine;
using UnityEngine.Events;
using CUC260905.Interaction;

namespace CUC260905.Interaction.Example
{
    /// <summary>把点击意图适配为 Inspector 可配置的 UnityEvent。</summary>
    [DisallowMultipleComponent]
    public sealed class UnityEventClickable : MonoBehaviour, IClickable
    {
        [SerializeField] private UnityEvent mOnClick;

        public InteractionResult OnClick(in ClickIntent intent)
        {
            mOnClick?.Invoke();
            return new InteractionResult(InteractionResultStatus.Handled);
        }
    }

    /// <summary>把 Hover.Enter 与 Hover.Exit 分别适配为 Inspector 可配置事件。</summary>
    [DisallowMultipleComponent]
    public sealed class UnityEventHoverable : MonoBehaviour, IHoverable
    {
        [SerializeField] private UnityEvent mOnEnter;
        [SerializeField] private UnityEvent mOnExit;

        public InteractionResult OnHover(in HoverIntent intent)
        {
            if (intent.Phase == HoverPhase.Enter)
            {
                mOnEnter?.Invoke();
                return new InteractionResult(InteractionResultStatus.Handled);
            }

            mOnExit?.Invoke();
            return new InteractionResult(InteractionResultStatus.Handled);
        }
    }

}
