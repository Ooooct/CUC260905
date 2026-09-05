using DG.Tweening;
using UnityEngine;

namespace CUC260905.Feedback
{
    /// <summary>
    /// 单个背景反馈圆：世界空间 SpriteRenderer（默认 sortingOrder = -1），
    /// 初始全不透明，在 Duration 内按 easeOutCubic 淡出至 0，随后销毁自身。
    /// 圆盘纹理由代码生成，无需美术资源；多个圆互不影响，可并发播放。
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class CircleFeedbackView : MonoBehaviour
    {
        private const int TextureSize = 256;

        private static Sprite sCircleSprite;

        private SpriteRenderer mSpriteRenderer;
        private Tween mFadeTween;
        private CircleFeedbackRequest mRequest;

        /// <summary>创建反馈圆并挂到 parent 下；随后调用 <see cref="Play"/> 开始淡出。</summary>
        public static CircleFeedbackView Create(
            Transform parent,
            in CircleFeedbackRequest request,
            string sortingLayer,
            int sortingOrder)
        {
            GameObject gameObject = new GameObject("FeedbackCircle");
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.position = request.Position;

            CircleFeedbackView view = gameObject.AddComponent<CircleFeedbackView>();
            view.mRequest = request;
            // RequireComponent 已自动添加 SpriteRenderer，取回而非再次 AddComponent，避免重复渲染器。
            view.mSpriteRenderer = gameObject.GetComponent<SpriteRenderer>();
            view.mSpriteRenderer.sprite = GetCircleSprite();
            view.mSpriteRenderer.sortingLayerName = sortingLayer;
            view.mSpriteRenderer.sortingOrder = sortingOrder;
            view.mSpriteRenderer.color = request.Color;

            // 精灵的 pixelsPerUnit = 纹理边长，本地尺寸即 1x1 世界单位，按直径缩放得到半径圆。
            float diameter = Mathf.Max(0f, request.Radius * 2f);
            gameObject.transform.localScale = new Vector3(diameter, diameter, 1f);
            return view;
        }

        /// <summary>从全透明度按 easeOutCubic 淡出到 0，结束后销毁自身。</summary>
        public void Play()
        {
            if (mSpriteRenderer == null)
            {
                mSpriteRenderer = GetComponent<SpriteRenderer>();
            }

            Color color = mSpriteRenderer.color;
            mSpriteRenderer.color = new Color(color.r, color.g, color.b, 1f);

            if (mRequest.Duration <= 0f || mRequest.Radius <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            mFadeTween = mSpriteRenderer
                .DOFade(0f, mRequest.Duration)
                .SetEase(Ease.OutCubic)
                .SetLink(gameObject)
                .OnComplete(() => Destroy(gameObject));
        }

        private void OnDestroy()
        {
            mFadeTween?.Kill();
            mFadeTween = null;
        }

        private static Sprite GetCircleSprite()
        {
            if (sCircleSprite != null)
            {
                return sCircleSprite;
            }

            Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                name = "FeedbackCircleTexture",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };

            Vector2 center = new Vector2(TextureSize * 0.5f, TextureSize * 0.5f);
            float radiusPixels = TextureSize * 0.5f;
            Color32[] pixels = new Color32[TextureSize * TextureSize];
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    // 1 像素软边，避免硬锯齿。
                    float alpha = Mathf.Clamp01(radiusPixels - distance + 1f);
                    byte alphaByte = (byte)Mathf.RoundToInt(alpha * 255f);
                    pixels[y * TextureSize + x] = new Color32(255, 255, 255, alphaByte);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();

            sCircleSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, TextureSize, TextureSize),
                new Vector2(0.5f, 0.5f),
                TextureSize);
            sCircleSprite.name = "FeedbackCircleSprite";
            return sCircleSprite;
        }
    }
}
