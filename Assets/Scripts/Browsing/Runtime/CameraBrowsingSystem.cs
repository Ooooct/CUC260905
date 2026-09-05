using CUC260905.Interaction;
using CUC260905.Placement;
using QFramework;
using UnityEngine;

namespace CUC260905.Browsing
{
    /// <summary>
    /// 摄像机浏览系统的业务入口。
    /// 不轮询 Unity 生命周期，也不直接读 Unity Input；每帧由 Controller 调用 <see cref="ProcessFrame"/>，
    /// 输入数据来自 Interaction 域已发布的 IPointerFrameSource，滚轮来自 IScrollWheelSource。
    /// </summary>
    public interface ICameraBrowsingSystem : ISystem
    {
        /// <summary>玩家当前是否正在空白区域平移。</summary>
        bool IsPanning { get; }

        /// <summary>每帧调度入口：处理缩放、平移与惯性。</summary>
        void ProcessFrame(float unscaledTime);

        /// <summary>把焦点移动到世界坐标（程序化聚焦，表现层负责缓动呈现）。</summary>
        void MoveTo(Vector3 worldPosition);

        /// <summary>设置缩放值（正交相机为 orthographicSize，透视为 fieldOfView），自动夹取到范围。</summary>
        void ZoomTo(float zoom);

        /// <summary>取消正在进行的平移与惯性滑动。</summary>
        void Cancel();
    }

    /// <summary>
    /// 浏览规则 System：纯逻辑，可脱离 Camera / DOTween 做单元测试。
    /// 只修改 Model 的焦点/缩放并发送事件，不触碰表现层对象。
    /// </summary>
    public sealed class CameraBrowsingSystem : AbstractSystem, ICameraBrowsingSystem
    {
        private const float DefaultFrameTime = 1.0f / 60.0f;

        private readonly CameraBrowsingConfig mConfig;

        private ICameraBrowsingModel mModel;
        private IPointerFrameSource mFrameSource;
        private IScrollWheelSource mScrollSource;
        private ITargetResolver mTargetResolver;
        private IWorldPointerMapper mPointerMapper;
        private IPlacementInputGate mPlacementGate;

        // 平移会话
        private bool mPanSessionActive;
        private float mLastMoveTime;
        private Vector3 mFocalVelocity;

        // 惯性滑动：焦点速度随时间指数衰减
        private bool mGliding;
        private Vector3 mGlideVelocity;
        private float mGlideRemaining;
        private float mLastProcessTime;

        public CameraBrowsingSystem(CameraBrowsingConfig config)
        {
            mConfig = config;
        }

        public bool IsPanning
        {
            get { return mPanSessionActive; }
        }

        protected override void OnInit()
        {
            mModel = this.GetModel<ICameraBrowsingModel>();
            mFrameSource = this.GetUtility<IPointerFrameSource>();
            mScrollSource = this.GetUtility<IScrollWheelSource>();
            mTargetResolver = this.GetUtility<ITargetResolver>();
            mPointerMapper = this.GetUtility<IWorldPointerMapper>();
            mPlacementGate = this.GetUtility<IPlacementInputGate>();

            if (mModel == null ||
                mFrameSource == null ||
                mScrollSource == null ||
                mTargetResolver == null ||
                mPointerMapper == null)
            {
                throw new System.InvalidOperationException(
                    "CameraBrowsingSystem 初始化前必须注册 ICameraBrowsingModel、IPointerFrameSource、" +
                    "IScrollWheelSource、ITargetResolver、IWorldPointerMapper。");
            }

            mLastProcessTime = Time.unscaledTime;
        }

        protected override void OnDeinit()
        {
            Cancel();
        }

        public void ProcessFrame(float unscaledTime)
        {
            // 放置等独占输入期间：完全抑制平移与缩放，避免与放置点击/拖拽冲突。
            if (mPlacementGate != null && mPlacementGate.IsBlocked)
            {
                Cancel();
                return;
            }

            HandleZoom();
            HandlePointer();
            HandleInertia(unscaledTime);

            mLastProcessTime = unscaledTime;
        }

        public void MoveTo(Vector3 worldPosition)
        {
            if (mModel == null)
            {
                return;
            }

            mModel.FocalPoint.Value = mConfig.ClampFocal(worldPosition);
        }

        public void ZoomTo(float zoom)
        {
            if (mModel == null)
            {
                return;
            }

            mModel.Zoom.Value = Mathf.Clamp(zoom, mConfig.ZoomRange.x, mConfig.ZoomRange.y);
        }

        public void Cancel()
        {
            if (mModel != null && mPanSessionActive)
            {
                mPanSessionActive = false;
                mModel.IsPanning.Value = false;
                this.SendEvent(new CameraBrowsingPanEndedEvent(mFocalVelocity));
            }

            mPanSessionActive = false;
            mFocalVelocity = Vector3.zero;
            mGliding = false;
            mGlideVelocity = Vector3.zero;
            mGlideRemaining = 0.0f;
        }

        private void HandleZoom()
        {
            if (mScrollSource == null)
            {
                return;
            }

            Vector2 scroll = mScrollSource.ScrollDelta;
            if (Mathf.Approximately(scroll.y, 0.0f))
            {
                return;
            }

            Vector2 screenPosition = mFrameSource != null && mFrameSource.LatestFrame.HasValue
                ? mFrameSource.LatestFrame.Value.ScreenPosition
                : Vector2.zero;

            // 指针悬浮在 UI 上时滚轮不缩放场景。
            if (mPointerMapper != null && mPointerMapper.IsOverUI(screenPosition))
            {
                return;
            }

            float current = mModel.Zoom.Value;
            // 向上滚（y > 0）＝放大：缩放值（orthoSize/fov）按倍率缩小。
            float target = current / Mathf.Pow(mConfig.ZoomStep, scroll.y);
            target = Mathf.Clamp(target, mConfig.ZoomRange.x, mConfig.ZoomRange.y);
            if (Mathf.Approximately(target, current))
            {
                return;
            }

            float previous = current;
            Vector3 focal = mModel.FocalPoint.Value;

            if (mConfig.ZoomToCursor &&
                mPointerMapper != null &&
                mPointerMapper.TryMapScreenToWorld(screenPosition, out Vector3 cursorWorld))
            {
                // 缩放锚点保持在光标下：F' = P + (F - P) * (target / current)。
                float ratio = target / current;
                focal += (focal - cursorWorld) * (ratio - 1.0f);
                focal = mConfig.ClampFocal(focal);
            }

            mModel.Zoom.Value = target;
            mModel.FocalPoint.Value = focal;
            this.SendEvent(new CameraBrowsingZoomChangedEvent(previous, target, screenPosition));
        }

        private void HandlePointer()
        {
            if (mFrameSource == null || !mFrameSource.LatestFrame.HasValue)
            {
                return;
            }

            PointerFrameEvent frame = mFrameSource.LatestFrame.Value;
            if (frame.Signals == null)
            {
                return;
            }

            foreach (PointerSignal signal in frame.Signals)
            {
                if (signal.Button != PointerButton.Left)
                {
                    continue;
                }

                switch (signal.Phase)
                {
                    case PointerPhase.Down:
                        TryBeginPan(signal);
                        break;
                    case PointerPhase.Move:
                        if (mPanSessionActive)
                        {
                            UpdatePan(signal);
                        }

                        break;
                    case PointerPhase.Up:
                        if (mPanSessionActive)
                        {
                            EndPan(signal);
                        }

                        break;
                    case PointerPhase.Cancel:
                        if (mPanSessionActive)
                        {
                            Cancel();
                        }

                        break;
                }
            }
        }

        private void TryBeginPan(PointerSignal signal)
        {
            if (!mConfig.PanOnEmptyArea)
            {
                return;
            }

            // 指针落在 UI 上时不发起世界平移。
            if (mPointerMapper != null && mPointerMapper.IsOverUI(signal.ScreenPosition))
            {
                return;
            }

            // 只有空白区域（未命中可交互对象）的按下才属于摄像机平移。
            InteractionHit hit;
            mTargetResolver.TryResolve(signal, out hit);
            if (hit.HasTarget)
            {
                return;
            }

            // 新会话打断进行中的惯性滑动。
            mPanSessionActive = true;
            mLastMoveTime = signal.Time;
            mGliding = false;
            mGlideVelocity = Vector3.zero;
            mModel.IsPanning.Value = true;
            this.SendEvent(new CameraBrowsingPanBeganEvent(signal.ScreenPosition));
        }

        private void UpdatePan(PointerSignal signal)
        {
            Vector2 currentScreen = signal.ScreenPosition;
            Vector2 previousScreen = currentScreen - signal.ScreenDelta;

            Vector3 currentWorld = ScreenToWorld(currentScreen);
            Vector3 previousWorld = ScreenToWorld(previousScreen);
            // 光标在游戏平面上拖动的世界位移。
            Vector3 worldDelta = currentWorld - previousWorld;

            // 抓取语义：焦点向光标拖动的反方向移动，使世界内容跟随指针。
            mModel.FocalPoint.Value = mConfig.ClampFocal(mModel.FocalPoint.Value - worldDelta);

            // 按两帧 Move 信号的时间差归一化估算焦点速度（供惯性使用）。
            float dt = signal.Time - mLastMoveTime;
            if (dt <= 0.0f)
            {
                dt = DefaultFrameTime;
            }

            mLastMoveTime = signal.Time;
            mFocalVelocity = -worldDelta / dt;
        }

        private void EndPan(PointerSignal signal)
        {
            mPanSessionActive = false;
            mModel.IsPanning.Value = false;
            Vector3 velocity = mFocalVelocity;

            this.SendEvent(new CameraBrowsingPanEndedEvent(velocity));

            if (mConfig.InertiaEnabled && velocity.sqrMagnitude > 0.0001f)
            {
                float speed = velocity.magnitude;
                if (speed > mConfig.MaxPanSpeed)
                {
                    velocity *= mConfig.MaxPanSpeed / speed;
                }

                mGlideVelocity = velocity;
                mGlideRemaining = mConfig.InertiaDuration;
                mGliding = true;
                mLastProcessTime = signal.Time;
            }
        }

        private void HandleInertia(float unscaledTime)
        {
            if (!mGliding)
            {
                return;
            }

            float dt = unscaledTime - mLastProcessTime;
            if (dt <= 0.0f)
            {
                dt = DefaultFrameTime;
            }

            mGlideRemaining -= dt;
            mGlideVelocity *= Mathf.Exp(-dt / (mConfig.InertiaDuration * 0.5f));

            if (mGlideRemaining <= 0.0f || mGlideVelocity.sqrMagnitude < 0.0004f)
            {
                mGliding = false;
                mGlideVelocity = Vector3.zero;
                return;
            }

            Vector3 focal = mConfig.ClampFocal(mModel.FocalPoint.Value + mGlideVelocity * dt);
            mModel.FocalPoint.Value = focal;
        }

        private Vector3 ScreenToWorld(Vector2 screenPosition)
        {
            if (mPointerMapper != null &&
                mPointerMapper.TryMapScreenToWorld(screenPosition, out Vector3 worldPosition))
            {
                return worldPosition;
            }

            return mModel.FocalPoint.Value;
        }
    }
}
