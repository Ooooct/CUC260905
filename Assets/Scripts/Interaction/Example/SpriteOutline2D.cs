using UnityEngine;

namespace CUC260905.Interaction.Example
{
    /// <summary>
    /// 为 SpriteRenderer 提供可独立配置的内外轮廓。
    /// Sprite 必须使用 Full Rect 网格；图集帧之间需要留出透明边距。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SpriteOutline2D : MonoBehaviour
    {
        private const string ShaderName = "CUC260905/Sprite Outline 2D";

        private static readonly int OuterOutlineColorId = Shader.PropertyToID("_OuterOutlineColor");
        private static readonly int OuterOutlineWidthId = Shader.PropertyToID("_OuterOutlineWidth");
        private static readonly int InnerOutlineColorId = Shader.PropertyToID("_InnerOutlineColor");
        private static readonly int InnerOutlineWidthId = Shader.PropertyToID("_InnerOutlineWidth");

        private static Material sSharedOutlineMaterial;

        [Header("外轮廓")]
        [SerializeField] private Color mOuterColor = new Color(1.0f, 0.82f, 0.18f, 1.0f);
        [SerializeField, Range(0.0f, 16.0f)] private float mOuterWidth = 1.0f;

        [Header("内轮廓")]
        [SerializeField] private Color mInnerColor = new Color(1.0f, 1.0f, 1.0f, 0.0f);
        [SerializeField, Range(0.0f, 16.0f)] private float mInnerWidth = 0.0f;

        private SpriteRenderer mSpriteRenderer;
        private Material mOriginalMaterial;
        private MaterialPropertyBlock mPropertyBlock;
        private bool mUsingOutlineMaterial;

        public Color OuterColor
        {
            get { return mOuterColor; }
            set
            {
                mOuterColor = value;
                ApplyProperties();
            }
        }

        public float OuterWidth
        {
            get { return mOuterWidth; }
            set
            {
                mOuterWidth = Mathf.Clamp(value, 0.0f, 16.0f);
                ApplyProperties();
            }
        }

        public Color InnerColor
        {
            get { return mInnerColor; }
            set
            {
                mInnerColor = value;
                ApplyProperties();
            }
        }

        public float InnerWidth
        {
            get { return mInnerWidth; }
            set
            {
                mInnerWidth = Mathf.Clamp(value, 0.0f, 16.0f);
                ApplyProperties();
            }
        }

        private void Awake()
        {
            CacheRenderer();
        }

        private void OnEnable()
        {
            CacheRenderer();
            ApplyOutlineMaterial();
            ApplyProperties();
        }

        private void OnValidate()
        {
            mOuterWidth = Mathf.Clamp(mOuterWidth, 0.0f, 16.0f);
            mInnerWidth = Mathf.Clamp(mInnerWidth, 0.0f, 16.0f);

            if (!isActiveAndEnabled)
            {
                return;
            }

            CacheRenderer();
            ApplyOutlineMaterial();
            ApplyProperties();
        }

        private void OnDisable()
        {
            RestoreOriginalMaterial();
        }

        /// <summary>一次性修改外轮廓颜色和粗细。</summary>
        public void SetOuterOutline(Color color, float width)
        {
            mOuterColor = color;
            mOuterWidth = Mathf.Clamp(width, 0.0f, 16.0f);
            ApplyProperties();
        }

        /// <summary>一次性修改内轮廓颜色和粗细。</summary>
        public void SetInnerOutline(Color color, float width)
        {
            mInnerColor = color;
            mInnerWidth = Mathf.Clamp(width, 0.0f, 16.0f);
            ApplyProperties();
        }

        private void CacheRenderer()
        {
            if (mSpriteRenderer == null)
            {
                mSpriteRenderer = GetComponent<SpriteRenderer>();
            }

            if (mPropertyBlock == null)
            {
                mPropertyBlock = new MaterialPropertyBlock();
            }
        }

        private void ApplyOutlineMaterial()
        {
            Material outlineMaterial = GetSharedOutlineMaterial();
            if (mSpriteRenderer == null || outlineMaterial == null)
            {
                return;
            }

            if (!mUsingOutlineMaterial)
            {
                mOriginalMaterial = mSpriteRenderer.sharedMaterial;
                mUsingOutlineMaterial = true;
            }

            mSpriteRenderer.sharedMaterial = outlineMaterial;
        }

        private void RestoreOriginalMaterial()
        {
            if (!mUsingOutlineMaterial || mSpriteRenderer == null)
            {
                return;
            }

            if (mSpriteRenderer.sharedMaterial == sSharedOutlineMaterial)
            {
                mSpriteRenderer.sharedMaterial = mOriginalMaterial;
            }

            mUsingOutlineMaterial = false;
            mOriginalMaterial = null;
        }

        private void ApplyProperties()
        {
            if (mSpriteRenderer == null || !mUsingOutlineMaterial)
            {
                return;
            }

            mSpriteRenderer.GetPropertyBlock(mPropertyBlock);
            mPropertyBlock.SetColor(OuterOutlineColorId, mOuterColor);
            mPropertyBlock.SetFloat(OuterOutlineWidthId, mOuterWidth);
            mPropertyBlock.SetColor(InnerOutlineColorId, mInnerColor);
            mPropertyBlock.SetFloat(InnerOutlineWidthId, mInnerWidth);
            mSpriteRenderer.SetPropertyBlock(mPropertyBlock);
        }

        private static Material GetSharedOutlineMaterial()
        {
            if (sSharedOutlineMaterial != null)
            {
                return sSharedOutlineMaterial;
            }

            Shader outlineShader = Shader.Find(ShaderName);
            if (outlineShader == null)
            {
                Debug.LogError("找不到 Sprite Outline 2D Shader，请确认脚本资源已完成导入。");
                return null;
            }

            sSharedOutlineMaterial = new Material(outlineShader)
            {
                name = "SpriteOutline2D (Runtime Shared)",
                hideFlags = HideFlags.DontSave
            };
            return sSharedOutlineMaterial;
        }
    }
}
