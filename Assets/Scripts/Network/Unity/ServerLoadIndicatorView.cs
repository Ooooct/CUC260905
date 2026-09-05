using DG.Tweening;
using QFramework;
using UnityEngine;
using UnityEngine.UI;

namespace CUC260905.Network
{
    /// <summary>
    /// 单台服务器的世界空间负载条视图。
    /// 只在能力值变化时刷新 Slider；位置由 ServerLoadIndicatorPool 集中同步。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ServerLoadIndicatorView : MonoBehaviour
    {
        private static Sprite sWhiteSprite;

        private RectTransform mRectTransform;
        private Slider mSlider;
        private Image mFillImage;
        private Transform mTarget;
        private IUnRegister mLoadRegistration;
        private IUnRegister mCapacityRegistration;
        private ServerNodeCapabilities mCapabilities;
        private SpriteRenderer mTargetRenderer;
        private Color mLowLoadColor;
        private Color mHighLoadColor;
        private Vector3 mLastWorldPosition;
        private bool mHasWorldPosition;
        private Tweener mFillTween;
        private float mDisplayedLoad = -1.0f;

        private const float FillTweenDuration = 0.22f;

        public Transform Target
        {
            get { return mTarget; }
        }

        public void Initialize(Vector2 size)
        {
            mRectTransform = transform as RectTransform;
            mRectTransform.sizeDelta = size;
            mRectTransform.pivot = new Vector2(0.5f, 0.5f);

            mFillImage = CreateImage("Fill", transform, Color.green);
            SetStretch(mFillImage.rectTransform, Vector2.zero, new Vector2(0.0f, 1.0f), Vector2.zero, Vector2.zero);

            mSlider = gameObject.AddComponent<Slider>();
            mSlider.transition = Selectable.Transition.None;
            mSlider.navigation = new Navigation { mode = Navigation.Mode.None };
            mSlider.minValue = 0.0f;
            mSlider.maxValue = 1.0f;
            mSlider.wholeNumbers = false;
            mSlider.direction = Slider.Direction.LeftToRight;
            mSlider.fillRect = null;
            mSlider.SetValueWithoutNotify(0.0f);
        }

        public void Bind(
            Transform target,
            ServerNodeCapabilities capabilities,
            Color lowLoadColor,
            Color highLoadColor)
        {
            Unbind();
            mTarget = target;
            mCapabilities = capabilities;
            mTargetRenderer = target != null ? target.GetComponent<SpriteRenderer>() : null;
            mLowLoadColor = lowLoadColor;
            mHighLoadColor = highLoadColor;

            if (mCapabilities == null)
            {
                ApplyLoad(0.0f);
                return;
            }

            mLoadRegistration = mCapabilities.CurrentDataLoadPerSecond.RegisterWithInitValue(OnLoadChanged);
            mCapacityRegistration = mCapabilities.DataProcessingPerSecond.RegisterWithInitValue(OnCapacityChanged);
        }

        public void Unbind()
        {
            mLoadRegistration?.UnRegister();
            mCapacityRegistration?.UnRegister();
            mLoadRegistration = null;
            mCapacityRegistration = null;
            mCapabilities = null;
            mTarget = null;
            mTargetRenderer = null;
            mHasWorldPosition = false;
            mDisplayedLoad = -1.0f;
            mFillTween?.Pause();

            if (mSlider != null)
            {
                mSlider.SetValueWithoutNotify(0.0f);
            }

            if (mFillImage != null)
            {
                mFillImage.rectTransform.anchorMax = new Vector2(0.0f, 1.0f);
            }
        }

        public void UpdateWorldPosition(Vector3 worldOffset)
        {
            if (mTarget == null)
            {
                return;
            }

            Vector3 worldPosition = mTarget.position + worldOffset;
            if (mTargetRenderer != null)
            {
                Bounds bounds = mTargetRenderer.bounds;
                worldPosition = new Vector3(
                    bounds.center.x + worldOffset.x,
                    bounds.min.y + worldOffset.y,
                    bounds.center.z + worldOffset.z);
            }
            if (mHasWorldPosition && (worldPosition - mLastWorldPosition).sqrMagnitude <= 0.000001f)
            {
                return;
            }

            mRectTransform.position = worldPosition;
            mLastWorldPosition = worldPosition;
            mHasWorldPosition = true;
        }

        private void OnDestroy()
        {
            Unbind();
            mFillTween?.Kill();
            mFillTween = null;
        }

        private void OnLoadChanged(float _)
        {
            RefreshLoad();
        }

        private void OnCapacityChanged(float _)
        {
            RefreshLoad();
        }

        private void RefreshLoad()
        {
            if (mCapabilities == null)
            {
                ApplyLoad(0.0f);
                return;
            }

            float capacity = mCapabilities.DataProcessingPerSecond.Value;
            float normalizedLoad = capacity > 0.0f
                ? Mathf.Clamp01(mCapabilities.CurrentDataLoadPerSecond.Value / capacity)
                : 0.0f;
            ApplyLoad(normalizedLoad);
        }

        private void ApplyLoad(float normalizedLoad)
        {
            if (mSlider == null || mFillImage == null)
            {
                return;
            }

            mSlider.SetValueWithoutNotify(normalizedLoad);
            Vector2 targetAnchorMax = new Vector2(normalizedLoad, 1.0f);
            mFillImage.color = Color.Lerp(mLowLoadColor, mHighLoadColor, normalizedLoad);

            if (Mathf.Approximately(mDisplayedLoad, normalizedLoad))
            {
                return;
            }

            mDisplayedLoad = normalizedLoad;
            if (mFillTween == null)
            {
                mFillTween = mFillImage.rectTransform
                    .DOAnchorMax(targetAnchorMax, FillTweenDuration)
                    .SetEase(Ease.OutCubic)
                    .SetAutoKill(false)
                    .Pause();
            }
            else
            {
                mFillTween.ChangeEndValue(targetAnchorMax, true);
            }

            mFillTween.Restart();
        }

        private static Image CreateImage(string objectName, Transform parent, Color color)
        {
            GameObject imageObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            imageObject.transform.SetParent(parent, false);
            Image image = imageObject.GetComponent<Image>();
            image.sprite = GetWhiteSprite();
            image.type = Image.Type.Simple;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static void SetStretch(
            RectTransform rectTransform,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 offsetMin,
            Vector2 offsetMax)
        {
            rectTransform.anchorMin = anchorMin;
            rectTransform.anchorMax = anchorMax;
            rectTransform.offsetMin = offsetMin;
            rectTransform.offsetMax = offsetMax;
        }

        private static Sprite GetWhiteSprite()
        {
            if (sWhiteSprite != null)
            {
                return sWhiteSprite;
            }

            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply(false, true);
            sWhiteSprite = Sprite.Create(
                texture,
                new Rect(0.0f, 0.0f, 1.0f, 1.0f),
                new Vector2(0.5f, 0.5f),
                1.0f);
            return sWhiteSprite;
        }
    }
}
