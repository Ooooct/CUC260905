using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace CUC260905.Feedback
{
    /// <summary>
    /// 屏幕外反馈指示：在屏幕中心周围的圆环上显示一个朝向目标的临时三角形。
    /// 仅处理呈现，不参与射线检测，也不保存跨请求的状态。
    /// </summary>
    [RequireComponent(typeof(Image))]
    public sealed class OffscreenFeedbackIndicatorView : MonoBehaviour
    {
        private const int TextureSize = 64;

        private static Sprite sTriangleSprite;

        private Image mImage;
        private Tween mFadeTween;
        private float mDuration;

        /// <summary>创建一个尖端朝向 direction 的三角形；随后调用 <see cref="Play"/> 开始淡出。</summary>
        public static OffscreenFeedbackIndicatorView Create(
            Transform parent,
            Vector2 direction,
            Color color,
            float duration,
            float size,
            float centerDistance,
            float screenPadding)
        {
            GameObject gameObject = new GameObject("OffscreenFeedbackIndicator", typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);

            RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            float clampedSize = Mathf.Max(1f, size);
            rectTransform.sizeDelta = new Vector2(clampedSize, clampedSize);

            RectTransform parentRectTransform = parent as RectTransform;
            float availableRadius = centerDistance;
            if (parentRectTransform != null)
            {
                float halfShortestSide = Mathf.Min(
                    parentRectTransform.rect.width,
                    parentRectTransform.rect.height) * 0.5f;
                availableRadius = Mathf.Min(
                    centerDistance,
                    Mathf.Max(0f, halfShortestSide - screenPadding - clampedSize * 0.5f));
            }

            rectTransform.anchoredPosition = direction * availableRadius;
            rectTransform.localRotation = Quaternion.Euler(
                0f,
                0f,
                Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f);

            OffscreenFeedbackIndicatorView view = gameObject.AddComponent<OffscreenFeedbackIndicatorView>();
            view.mDuration = Mathf.Max(0f, duration);
            view.mImage = gameObject.GetComponent<Image>();
            view.mImage.sprite = GetTriangleSprite();
            view.mImage.raycastTarget = false;
            view.mImage.color = new Color(color.r, color.g, color.b, 1f);
            return view;
        }

        /// <summary>从不透明淡出到全透明，结束后自动销毁自身。</summary>
        public void Play()
        {
            if (mImage == null)
            {
                mImage = GetComponent<Image>();
            }

            if (mDuration <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            mFadeTween = mImage
                .DOFade(0f, mDuration)
                .SetEase(Ease.OutCubic)
                .SetLink(gameObject)
                .OnComplete(() => Destroy(gameObject));
        }

        private void OnDestroy()
        {
            mFadeTween?.Kill();
            mFadeTween = null;
        }

        private static Sprite GetTriangleSprite()
        {
            if (sTriangleSprite != null)
            {
                return sTriangleSprite;
            }

            Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                name = "OffscreenFeedbackTriangleTexture",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.DontSave
            };

            Color32[] pixels = new Color32[TextureSize * TextureSize];
            for (int y = 0; y < TextureSize; y++)
            {
                for (int x = 0; x < TextureSize; x++)
                {
                    float normalizedX = (x + 0.5f) / TextureSize;
                    float normalizedY = (y + 0.5f) / TextureSize;
                    float halfWidth = (1f - normalizedY) * 0.5f;
                    float leftBoundary = 0.5f - halfWidth;
                    float rightBoundary = 0.5f + halfWidth;
                    bool isInside = normalizedY >= 0f && normalizedY <= 1f &&
                                    normalizedX >= leftBoundary && normalizedX <= rightBoundary;
                    pixels[y * TextureSize + x] = isInside
                        ? new Color32(255, 255, 255, 255)
                        : new Color32(255, 255, 255, 0);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            sTriangleSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, TextureSize, TextureSize),
                new Vector2(0.5f, 0.5f),
                TextureSize);
            sTriangleSprite.name = "OffscreenFeedbackTriangleSprite";
            return sTriangleSprite;
        }
    }
}
