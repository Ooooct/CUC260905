using UnityEngine;

namespace CUC260905.Interaction.Example
{
    /// <summary>
    /// 为 SpriteRenderer 提供可独立配置的内外轮廓。
    /// 支持 Tight 和 Full Rect 网格；图集帧之间需要留出透明边距以保证内轮廓采样正确。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class SpriteOutline2D : MonoBehaviour
    {
        private const string ShaderName = "CUC260905/Sprite Outline 2D";
        private const float MaxOutlineWidth = 64.0f;

        private static readonly int OuterOutlineColorId = Shader.PropertyToID("_OuterOutlineColor");
        private static readonly int OuterOutlineWidthId = Shader.PropertyToID("_OuterOutlineWidth");
        private static readonly int OuterMeshCenterId = Shader.PropertyToID("_OuterMeshCenter");
        private static readonly int OuterMeshScaleId = Shader.PropertyToID("_OuterMeshScale");
        private static readonly int InnerOutlineColorId = Shader.PropertyToID("_InnerOutlineColor");
        private static readonly int InnerOutlineWidthId = Shader.PropertyToID("_InnerOutlineWidth");

        private static Material sSharedOutlineMaterial;

        [Header("外轮廓")]
        [SerializeField] private Color mOuterColor = new Color(1.0f, 0.82f, 0.18f, 1.0f);
        [SerializeField, Range(0.0f, MaxOutlineWidth)] private float mOuterWidth = 1.0f;

        [Header("内轮廓")]
        [SerializeField] private Color mInnerColor = new Color(1.0f, 1.0f, 1.0f, 0.0f);
        [SerializeField, Range(0.0f, MaxOutlineWidth)] private float mInnerWidth = 0.0f;

        [SerializeField, HideInInspector] private Material mOriginalMaterial;

        private SpriteRenderer mSpriteRenderer;
        private MaterialPropertyBlock mPropertyBlock;
        private Sprite mLastSprite;
        private bool mNeedsRefresh;

        public Color OuterColor
        {
            get { return mOuterColor; }
            set
            {
                mOuterColor = value;
                RequestRefresh();
            }
        }

        public float OuterWidth
        {
            get { return mOuterWidth; }
            set
            {
                mOuterWidth = Mathf.Clamp(value, 0.0f, MaxOutlineWidth);
                RequestRefresh();
            }
        }

        public Color InnerColor
        {
            get { return mInnerColor; }
            set
            {
                mInnerColor = value;
                RequestRefresh();
            }
        }

        public float InnerWidth
        {
            get { return mInnerWidth; }
            set
            {
                mInnerWidth = Mathf.Clamp(value, 0.0f, MaxOutlineWidth);
                RequestRefresh();
            }
        }

        private void Awake()
        {
            CacheRenderer();
        }

        private void OnEnable()
        {
            CacheRenderer();
            RefreshOutline();
        }

        private void OnValidate()
        {
            mOuterWidth = Mathf.Clamp(mOuterWidth, 0.0f, MaxOutlineWidth);
            mInnerWidth = Mathf.Clamp(mInnerWidth, 0.0f, MaxOutlineWidth);

            // OnValidate 可能不在主线程调用，只标记数据变化，由 Update 完成 Unity API 操作。
            mNeedsRefresh = true;
        }

        private void OnDisable()
        {
            RestoreOriginalMaterial();
        }

        private void Update()
        {
            if (mSpriteRenderer != null && mSpriteRenderer.sprite != mLastSprite)
            {
                mNeedsRefresh = true;
            }

            if (!mNeedsRefresh)
            {
                return;
            }

            RefreshOutline();
        }

        /// <summary>一次性修改外轮廓颜色和粗细。</summary>
        public void SetOuterOutline(Color color, float width)
        {
            mOuterColor = color;
            mOuterWidth = Mathf.Clamp(width, 0.0f, MaxOutlineWidth);
            RequestRefresh();
        }

        /// <summary>一次性修改内轮廓颜色和粗细。</summary>
        public void SetInnerOutline(Color color, float width)
        {
            mInnerColor = color;
            mInnerWidth = Mathf.Clamp(width, 0.0f, MaxOutlineWidth);
            RequestRefresh();
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

        private void RequestRefresh()
        {
            mNeedsRefresh = true;
            if (Application.isPlaying)
            {
                RefreshOutline();
            }
        }

        private void RefreshOutline()
        {
            CacheRenderer();

            Material outlineMaterial = GetSharedOutlineMaterial();
            if (mSpriteRenderer == null || outlineMaterial == null)
            {
                return;
            }

            Material currentMaterial = mSpriteRenderer.sharedMaterial;
            if (!IsOutlineMaterial(currentMaterial))
            {
                mOriginalMaterial = currentMaterial;
            }

            mSpriteRenderer.sharedMaterial = outlineMaterial;
            ApplyProperties();
            mLastSprite = mSpriteRenderer.sprite;
            mNeedsRefresh = false;
        }

        private void RestoreOriginalMaterial()
        {
            if (mSpriteRenderer == null || !IsOutlineMaterial(mSpriteRenderer.sharedMaterial))
            {
                return;
            }

            mSpriteRenderer.sharedMaterial = mOriginalMaterial;
            mOriginalMaterial = null;
            mNeedsRefresh = false;
        }

        private void ApplyProperties()
        {
            if (mSpriteRenderer == null || !IsOutlineMaterial(mSpriteRenderer.sharedMaterial))
            {
                return;
            }

            mSpriteRenderer.GetPropertyBlock(mPropertyBlock);
            mPropertyBlock.SetColor(OuterOutlineColorId, mOuterColor);
            mPropertyBlock.SetFloat(OuterOutlineWidthId, mOuterWidth);
            ApplyOuterMeshProperties();
            mPropertyBlock.SetColor(InnerOutlineColorId, mInnerColor);
            mPropertyBlock.SetFloat(InnerOutlineWidthId, mInnerWidth);
            mSpriteRenderer.SetPropertyBlock(mPropertyBlock);
        }

        private void ApplyOuterMeshProperties()
        {
            Sprite sprite = mSpriteRenderer.sprite;
            Vector2 center = Vector2.zero;
            Vector2 scale = Vector2.one;

            if (sprite != null && mOuterWidth > 0.0f)
            {
                Bounds bounds = sprite.bounds;
                float outlineSize = mOuterWidth / sprite.pixelsPerUnit;
                center = bounds.center;
                scale = new Vector2(
                    CalculateOuterMeshScale(bounds.size.x, outlineSize),
                    CalculateOuterMeshScale(bounds.size.y, outlineSize));
            }

            mPropertyBlock.SetVector(OuterMeshCenterId, center);
            mPropertyBlock.SetVector(OuterMeshScaleId, scale);
        }

        private static float CalculateOuterMeshScale(float spriteSize, float outlineSize)
        {
            if (spriteSize <= 0.0f)
            {
                return 1.0f;
            }

            return 1.0f + outlineSize * 2.0f / spriteSize;
        }

        private static bool IsOutlineMaterial(Material material)
        {
            return material != null && material.shader != null && material.shader.name == ShaderName;
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
                name = "SpriteOutline2D (Shared)",
                hideFlags = HideFlags.DontSave
            };
            return sSharedOutlineMaterial;
        }
    }
}
