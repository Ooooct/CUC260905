using UnityEngine;
using CUC260905.Interaction;

namespace CUC260905.Interaction.Example
{
    /// <summary>仅负责悬浮提示的示例反馈。</summary>
    [RequireComponent(typeof(InteractionExampleVisual))]
    public sealed class HoverExampleFeedback : MonoBehaviour, IHoverable
    {
        [SerializeField] private Color mHoverColor = new Color(1.0f, 0.85f, 0.2f, 1.0f);

        private InteractionExampleVisual mVisual;

        private void Awake()
        {
            mVisual = GetComponent<InteractionExampleVisual>();
        }

        public InteractionResult OnHover(in HoverIntent intent)
        {
            if (intent.Phase == HoverPhase.Enter)
            {
                mVisual.SetColor(mHoverColor);
                Debug.Log($"[Interaction Example] {name} Hover.Enter", this);
            }
            else
            {
                mVisual.ApplyRest();
                Debug.Log($"[Interaction Example] {name} Hover.Exit", this);
            }

            return new InteractionResult(InteractionResultStatus.Handled);
        }
    }
}
