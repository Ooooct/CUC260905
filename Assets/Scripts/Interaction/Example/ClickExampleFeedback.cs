using UnityEngine;
using CUC260905.Interaction;

namespace CUC260905.Interaction.Example
{
    /// <summary>仅负责点击示例反馈。</summary>
    [RequireComponent(typeof(InteractionExampleVisual))]
    public sealed class ClickExampleFeedback : MonoBehaviour, IClickable
    {
        [SerializeField] private Color mClickedColor = new Color(0.95f, 0.35f, 0.25f, 1.0f);

        private InteractionExampleVisual mVisual;

        private void Awake()
        {
            mVisual = GetComponent<InteractionExampleVisual>();
        }

        public InteractionResult OnClick(in ClickIntent intent)
        {
            mVisual.SetColor(mClickedColor);
            Debug.Log($"[Interaction Example] {name} Click", this);
            return new InteractionResult(InteractionResultStatus.Handled);
        }
    }
}
