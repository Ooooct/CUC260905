using CUC260905.Interaction;
using QFramework;
using UnityEngine;

namespace CUC260905.Feedback
{
    /// <summary>
    /// 圆形背景反馈的表现层：监听 <see cref="CircleFeedbackRequestedEvent"/>，
    /// 在 mRoot 下生成反馈圆；目标位于镜头外时，额外在屏幕中心周围显示朝向目标的三角指示。
    /// 需挂在场景中（建议放在 Managers 下），可运行 CUC260905/Feedback/Setup Scene 一键装配。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FeedbackPresenter : MonoBehaviour, IController
    {
        [Header("反馈圆渲染")]
        [Tooltip("反馈圆所在排序层名。")]
        [SerializeField] private string mSortingLayer = "Default";
        [Tooltip("-1 层（背景）：位于网格（-100）之上、游戏内容（0）之下。")]
        [SerializeField] private int mSortingOrder = -1;
        [Tooltip("反馈圆的父节点；为空时挂在当前对象下。")]
        [SerializeField] private Transform mRoot;

        [Header("屏幕外方向提示")]
        [Tooltip("关闭后仍显示世界空间反馈圆，但不创建屏幕外方向三角。")]
        [SerializeField] private bool mShowOffscreenIndicator = true;
        [Tooltip("用于判断目标是否在视野内、计算方向的摄像机；为空时使用 MainCamera。")]
        [SerializeField] private Camera mIndicatorCamera;
        [Tooltip("承载方向三角的屏幕空间 Canvas；为空时自动寻找第一个 Screen Space - Overlay Canvas。")]
        [SerializeField] private Canvas mIndicatorCanvas;
        [SerializeField, Min(1f)]
        [Tooltip("三角形的边长（像素）。")]
        private float mIndicatorSize = 48f;
        [SerializeField, Min(0f)]
        [Tooltip("三角形中心距屏幕中心的距离（像素）。")]
        private float mIndicatorCenterDistance = 120f;
        [SerializeField, Min(0f)]
        [Tooltip("方向环最大半径与屏幕边缘保留的最小距离（像素）。")]
        private float mIndicatorScreenPadding = 24f;

        private IUnRegister mRequestRegistration;

        private void Start()
        {
            // InputController.Awake 已装配 Architecture，此处订阅事件总线是安全的。
            mRequestRegistration = this.RegisterEvent<CircleFeedbackRequestedEvent>(OnCircleRequested);
        }

        private void OnDestroy()
        {
            mRequestRegistration?.UnRegister();
            mRequestRegistration = null;
        }

        private void OnCircleRequested(CircleFeedbackRequestedEvent requestedEvent)
        {
            Transform root = mRoot != null ? mRoot : transform;
            CircleFeedbackView view = CircleFeedbackView.Create(
                root,
                requestedEvent.Request,
                mSortingLayer,
                mSortingOrder);
            view.Play();

            TryShowOffscreenIndicator(requestedEvent.Request);
        }

        private void TryShowOffscreenIndicator(in CircleFeedbackRequest request)
        {
            if (!mShowOffscreenIndicator || !request.ShowOffscreenIndicator || request.Duration <= 0f)
            {
                return;
            }

            Camera camera = ResolveIndicatorCamera();
            Canvas canvas = ResolveIndicatorCanvas();
            if (camera == null || canvas == null)
            {
                return;
            }

            Vector3 viewportPosition = camera.WorldToViewportPoint(request.Position);
            bool isVisible = viewportPosition.z > 0f &&
                             viewportPosition.x >= 0f && viewportPosition.x <= 1f &&
                             viewportPosition.y >= 0f && viewportPosition.y <= 1f;
            if (isVisible)
            {
                return;
            }

            Vector3 cameraLocalPosition = camera.transform.InverseTransformPoint(request.Position);
            Vector2 direction = new Vector2(cameraLocalPosition.x, cameraLocalPosition.y);
            if (viewportPosition.z <= 0f)
            {
                direction = -direction;
            }

            if (direction.sqrMagnitude <= Mathf.Epsilon)
            {
                direction = Vector2.up;
            }

            direction.Normalize();
            OffscreenFeedbackIndicatorView view = OffscreenFeedbackIndicatorView.Create(
                canvas.transform,
                direction,
                request.Color,
                request.Duration,
                mIndicatorSize,
                mIndicatorCenterDistance,
                mIndicatorScreenPadding);
            view.Play();
        }

        private Camera ResolveIndicatorCamera()
        {
            if (mIndicatorCamera != null && mIndicatorCamera.isActiveAndEnabled)
            {
                return mIndicatorCamera;
            }

            Camera mainCamera = Camera.main;
            return mainCamera != null && mainCamera.isActiveAndEnabled ? mainCamera : null;
        }

        private Canvas ResolveIndicatorCanvas()
        {
            if (mIndicatorCanvas != null && mIndicatorCanvas.isActiveAndEnabled &&
                mIndicatorCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                return mIndicatorCanvas;
            }

            Canvas[] canvases = FindObjectsOfType<Canvas>();
            foreach (Canvas canvas in canvases)
            {
                if (canvas.isActiveAndEnabled && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                {
                    return canvas;
                }
            }

            return null;
        }

        IArchitecture IBelongToArchitecture.GetArchitecture()
        {
            return GameArchitecture.Interface;
        }
    }
}
