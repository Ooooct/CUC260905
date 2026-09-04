using UnityEngine;

namespace CUC260905.Interaction.Example
{
    /// <summary>2D 示例对象的纯视觉状态，不承担任何交互能力。</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class InteractionExampleVisual : MonoBehaviour
    {
        [SerializeField] private Color mRestColor = Color.white;

        private SpriteRenderer mSpriteRenderer;

        private void Awake()
        {
            mSpriteRenderer = GetComponent<SpriteRenderer>();
            ApplyRest();
        }

        public void ApplyRest()
        {
            SetColor(mRestColor);
        }

        public void SetColor(Color color)
        {
            if (mSpriteRenderer != null) mSpriteRenderer.color = color;
        }
    }
}
