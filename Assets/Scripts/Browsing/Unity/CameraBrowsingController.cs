using CUC260905.Interaction;
using DG.Tweening;
using QFramework;
using UnityEngine;

namespace CUC260905.Browsing
{
    /// <summary>
    /// 浏览系统的表现层 Controller。
    /// 负责把场景参数装配进 GameArchitecture（动态注册浏览 Model / System / Utility），
    /// 每帧驱动 ICameraBrowsingSystem，并在 LateUpdate 把逻辑状态平滑应用到 Camera：
    /// 位置使用指数平滑（拖拽跟手、惯性跟手），缩放使用 DOTween（OutQuad 等缓动曲线），
    /// 程序化聚焦使用 DOTween（InOutCubic）。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraBrowsingController : MonoBehaviour, IController
    {
        [Header("Target")]
        [SerializeField] private Camera mCamera;
        [Tooltip("缩放 1x 基准（orthographicSize / fieldOfView）；<=0 时取 Camera 当前值。")]
        [SerializeField] private float mBaseZoom = -1.0f;

        [Header("Browsing")]
        [Tooltip("最小放大倍率（最远）：0.25x。")]
        [SerializeField, Range(0.05f, 1.0f)] private float mMinZoomFactor = 0.25f;
        [Tooltip("最大放大倍率（最近）：2x。")]
        [SerializeField, Range(1.0f, 100.0f)] private float mMaxZoomFactor = 2.0f;
        [SerializeField] private float mZoomStep = 1.12f;
        [SerializeField] private bool mZoomToCursor = true;
        [SerializeField, Range(1.0f, 60.0f)] private float mFollowSmoothing = 24.0f;
        [SerializeField] private bool mInertiaEnabled = true;
        [SerializeField] private float mInertiaDuration = 0.45f;
        [SerializeField] private float mMaxPanSpeed = 60.0f;
        [SerializeField] private bool mClampToBounds = true;
        [Tooltip("相机移动/焦点世界范围：x=minX, y=maxX, z=minY, w=maxY（默认 -100..100）。")]
        [SerializeField] private Vector4 mBounds = new Vector4(-100.0f, 100.0f, -100.0f, 100.0f);

        [Header("DOTween Easing")]
        [SerializeField] private float mZoomDuration = 0.12f;
        [SerializeField] private Ease mZoomEase = Ease.OutQuad;
        [SerializeField] private float mFocusDuration = 0.6f;
        [SerializeField] private Ease mFocusEase = Ease.InOutCubic;

        private ICameraBrowsingSystem mSystem;
        private ICameraBrowsingModel mModel;
        private bool mInitialized;
        private float mResolvedBaseZoom;

        private Vector3 mSmoothedFocal;
        private float mAppliedZoom;
        private Tweener mZoomTweener;
        private Tweener mFocusTweener;
        private bool mFocusOverride;
        private Vector3 mFocusOverrideValue;

        // Start 晚于 InputController.Awake，确保 GameArchitecture 已装配完成。
        private void Start()
        {
            EnsureInitialized();
        }

        private void Update()
        {
            if (!mInitialized)
            {
                return;
            }

            mSystem.ProcessFrame(Time.unscaledTime);
        }

        private void LateUpdate()
        {
            if (!mInitialized || mModel == null || mCamera == null)
            {
                return;
            }

            ApplyPosition(mModel.FocalPoint.Value);
            ApplyZoom(mModel.Zoom.Value);
        }

        private void OnDisable()
        {
            // 焦点或场景切换中断输入时，先让平移与惯性收束。
            if (mSystem != null)
            {
                mSystem.Cancel();
            }
        }

        private void OnDestroy()
        {
            mZoomTweener?.Kill();
            mFocusTweener?.Kill();
            mZoomTweener = null;
            mFocusTweener = null;
        }

        /// <summary>程序化聚焦：把镜头平滑移到指定世界点（DOTween InOutCubic 缓动）。</summary>
        public void FocusAt(Vector3 worldPosition, float duration = -1.0f)
        {
            EnsureInitialized();
            if (mSystem == null)
            {
                return;
            }

            mSystem.MoveTo(worldPosition);

            float d = duration > 0.0f ? duration : mFocusDuration;
            mFocusTweener?.Kill();
            mFocusOverride = true;
            mFocusTweener = DOVirtual.Vector3(
                    mSmoothedFocal,
                    worldPosition,
                    d,
                    v => mFocusOverrideValue = v)
                .SetEase(mFocusEase)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    mFocusOverride = false;
                    if (mModel != null)
                    {
                        mSmoothedFocal = mModel.FocalPoint.Value;
                    }

                    mFocusTweener = null;
                });
        }

        private void EnsureInitialized()
        {
            if (mInitialized)
            {
                return;
            }

            if (mCamera == null)
            {
                mCamera = Camera.main;
            }

            if (mCamera == null)
            {
                Debug.LogError("CameraBrowsingController 需要指定 Camera。", this);
                return;
            }

            // 1x 基准缩放：默认取 Camera 当前 orthographicSize / fieldOfView。
            mResolvedBaseZoom = mBaseZoom > 0.0f
                ? mBaseZoom
                : (mCamera.orthographic ? mCamera.orthographicSize : mCamera.fieldOfView);

            RegisterIntoArchitecture(mResolvedBaseZoom);

            mModel = this.GetModel<ICameraBrowsingModel>();
            mSystem = this.GetSystem<ICameraBrowsingSystem>();
            if (mModel == null || mSystem == null)
            {
                Debug.LogError("CameraBrowsingController 注册浏览 Model/System 失败，请确认场景存在 InputController。", this);
                return;
            }

            // 以 Camera 当前位置与 1x 基准缩放作为平滑起点，首帧直接落到目标，避免闪现。
            mSmoothedFocal = mModel.FocalPoint.Value;
            mAppliedZoom = mResolvedBaseZoom;
            Vector3 p = mCamera.transform.position;
            mCamera.transform.position = new Vector3(mSmoothedFocal.x, mSmoothedFocal.y, p.z);
            ApplyCameraZoom(mResolvedBaseZoom);

            // 玩家开始手动平移时，立即打断程序化聚焦，避免镜头与玩家争夺控制权。
            this.RegisterEvent<CameraBrowsingPanBeganEvent>(OnPanBegan)
                .UnRegisterWhenGameObjectDestroyed(this);

            mInitialized = true;
        }

        private void OnPanBegan(CameraBrowsingPanBeganEvent e)
        {
            mFocusTweener?.Kill();
            mFocusTweener = null;
            mFocusOverride = false;
        }

        private void RegisterIntoArchitecture(float baseZoom)
        {
            // GameArchitecture 由 InputController 装配；此处按需追加浏览域依赖（幂等，避免重复注册）。
            IArchitecture architecture = GameArchitecture.Interface;

            if (architecture.GetUtility<IScrollWheelSource>() == null)
            {
                architecture.RegisterUtility<IScrollWheelSource>(new ScrollWheelSource());
            }

            if (architecture.GetModel<ICameraBrowsingModel>() == null)
            {
                Vector3 initialFocal = new Vector3(mCamera.transform.position.x, mCamera.transform.position.y, 0.0f);
                architecture.RegisterModel<ICameraBrowsingModel>(
                    new CameraBrowsingModel(initialFocal, baseZoom));
            }

            if (architecture.GetSystem<ICameraBrowsingSystem>() == null)
            {
                CameraBrowsingConfig config = new CameraBrowsingConfig(
                    zoomRange: CameraBrowsingConfig.ZoomRangeFromFactors(
                        baseZoom, mMinZoomFactor, mMaxZoomFactor),
                    zoomStep: mZoomStep,
                    zoomToCursor: mZoomToCursor,
                    maxPanSpeed: mMaxPanSpeed,
                    inertiaEnabled: mInertiaEnabled,
                    inertiaDuration: mInertiaDuration,
                    panOnEmptyArea: true,
                    clampToBounds: mClampToBounds,
                    bounds: mBounds);
                architecture.RegisterSystem<ICameraBrowsingSystem>(new CameraBrowsingSystem(config));
            }
        }

        private void ApplyPosition(Vector3 targetFocal)
        {
            if (mFocusOverride)
            {
                mSmoothedFocal = mFocusOverrideValue;
            }
            else
            {
                // 指数平滑：k 越大越跟手；k=0 时完全不跟随。
                float k = mFollowSmoothing;
                float t = 1.0f - Mathf.Exp(-k * Time.unscaledDeltaTime);
                mSmoothedFocal = Vector3.Lerp(mSmoothedFocal, targetFocal, t);
            }

            Vector3 p = mCamera.transform.position;
            mCamera.transform.position = new Vector3(mSmoothedFocal.x, mSmoothedFocal.y, p.z);
        }

        private void ApplyZoom(float targetZoom)
        {
            if (Mathf.Approximately(targetZoom, mAppliedZoom))
            {
                return;
            }

            // 每次缩放目标变化都重新从当前值缓动到新目标，连续滚动也能保持平滑。
            mZoomTweener?.Kill();
            float from = mAppliedZoom;
            mZoomTweener = DOVirtual.Float(from, targetZoom, mZoomDuration, v =>
                {
                    mAppliedZoom = v;
                    ApplyCameraZoom(v);
                })
                .SetEase(mZoomEase)
                .SetUpdate(true)
                .OnComplete(() =>
                {
                    mAppliedZoom = targetZoom;
                    ApplyCameraZoom(targetZoom);
                    mZoomTweener = null;
                });
        }

        private void ApplyCameraZoom(float zoom)
        {
            if (mCamera == null)
            {
                return;
            }

            if (mCamera.orthographic)
            {
                mCamera.orthographicSize = zoom;
            }
            else
            {
                mCamera.fieldOfView = zoom;
            }
        }

        IArchitecture IBelongToArchitecture.GetArchitecture()
        {
            return GameArchitecture.Interface;
        }
    }
}
