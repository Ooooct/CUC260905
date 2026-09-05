using CUC260905.Interaction;
using QFramework;
using UnityEngine;
using UnityEngine.UI;

namespace CUC260905.Network
{
    /// <summary>
    /// 用户节点部署接入期间的世界空间饼状计时器。
    /// 只读取拓扑模型中的剩余接入时间，不维护或修改部署状态。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkNodeRegistrar))]
    public sealed class DeploymentAccessTimerView : MonoBehaviour, IController
    {
        private const int TextureSize = 128;

        private static Sprite sCircleSprite;

        [SerializeField, Min(0f)]
        [Tooltip("饼状计时器的世界半径。")]
        private float mRadius = 0.5f;
        [SerializeField, Range(0f, 1f)]
        [Tooltip("黑色饼状计时器的不透明度。")]
        private float mOpacity = 0.5f;
        [SerializeField]
        [Tooltip("世界空间 Canvas 的排序层。")]
        private string mSortingLayer = "Default";
        [SerializeField]
        [Tooltip("相对节点图标的排序顺序。")]
        private int mSortingOrder = 1;

        private NetworkNodeRegistrar mRegistrar;
        private INetworkTopologyModel mTopologyModel;
        private GameObject mTimerObject;
        private Image mPieImage;

        private void Start()
        {
            mRegistrar = GetComponent<NetworkNodeRegistrar>();
            mTopologyModel = this.GetModel<INetworkTopologyModel>();
        }

        private void Update()
        {
            if (mRegistrar == null || mTopologyModel == null)
            {
                return;
            }

            string nodeId = mRegistrar.NodeId;
            if (string.IsNullOrWhiteSpace(nodeId) || !mTopologyModel.IsRegistered(nodeId))
            {
                return;
            }

            if (!mTopologyModel.TryGetDeploymentAccessRemaining(
                    nodeId,
                    Time.timeAsDouble,
                    out float remainingSeconds) ||
                mTopologyModel.DeploymentAccessTime <= 0f ||
                remainingSeconds <= 0f)
            {
                DestroyTimer();
                enabled = false;
                return;
            }

            EnsureTimer();
            if (mPieImage != null)
            {
                float remainingProgress = remainingSeconds / mTopologyModel.DeploymentAccessTime;
                mPieImage.fillAmount = Mathf.Clamp01(remainingProgress);
            }
        }

        private void EnsureTimer()
        {
            if (mTimerObject != null)
            {
                return;
            }

            mTimerObject = new GameObject(
                "DeploymentAccessTimer",
                typeof(RectTransform),
                typeof(Canvas),
                typeof(CanvasRenderer));
            mTimerObject.transform.SetParent(transform, false);

            Canvas canvas = mTimerObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.overrideSorting = true;
            canvas.sortingLayerName = mSortingLayer;
            canvas.sortingOrder = mSortingOrder;

            RectTransform timerTransform = mTimerObject.GetComponent<RectTransform>();
            float diameter = Mathf.Max(0f, mRadius * 2f);
            timerTransform.anchorMin = new Vector2(0.5f, 0.5f);
            timerTransform.anchorMax = new Vector2(0.5f, 0.5f);
            timerTransform.pivot = new Vector2(0.5f, 0.5f);
            timerTransform.anchoredPosition3D = Vector3.zero;
            timerTransform.sizeDelta = new Vector2(diameter, diameter);

            GameObject pieObject = new GameObject(
                "Pie",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            pieObject.transform.SetParent(mTimerObject.transform, false);

            RectTransform pieTransform = pieObject.GetComponent<RectTransform>();
            pieTransform.anchorMin = Vector2.zero;
            pieTransform.anchorMax = Vector2.one;
            pieTransform.offsetMin = Vector2.zero;
            pieTransform.offsetMax = Vector2.zero;

            mPieImage = pieObject.GetComponent<Image>();
            mPieImage.sprite = GetCircleSprite();
            mPieImage.type = Image.Type.Filled;
            mPieImage.fillMethod = Image.FillMethod.Radial360;
            mPieImage.fillOrigin = (int)Image.Origin360.Top;
            mPieImage.fillClockwise = true;
            mPieImage.raycastTarget = false;
            mPieImage.color = new Color(0f, 0f, 0f, Mathf.Clamp01(mOpacity));
        }

        private void DestroyTimer()
        {
            if (mTimerObject != null)
            {
                Destroy(mTimerObject);
            }

            mTimerObject = null;
            mPieImage = null;
        }

        private void OnDestroy()
        {
            DestroyTimer();
        }

        private static Sprite GetCircleSprite()
        {
            if (sCircleSprite != null)
            {
                return sCircleSprite;
            }

            Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
            {
                name = "DeploymentAccessTimerTexture",
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
                    float alpha = Mathf.Clamp01(radiusPixels - distance + 1f);
                    pixels[y * TextureSize + x] = new Color32(
                        255,
                        255,
                        255,
                        (byte)Mathf.RoundToInt(alpha * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            sCircleSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, TextureSize, TextureSize),
                new Vector2(0.5f, 0.5f),
                TextureSize);
            sCircleSprite.name = "DeploymentAccessTimerSprite";
            return sCircleSprite;
        }

        IArchitecture IBelongToArchitecture.GetArchitecture()
        {
            return GameArchitecture.Interface;
        }
    }
}
