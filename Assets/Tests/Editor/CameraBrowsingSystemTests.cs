using CUC260905.Browsing;
using CUC260905.Interaction;
using CUC260905.Placement;
using NUnit.Framework;
using QFramework;
using UnityEngine;

namespace CUC260905.Tests
{
    /// <summary>
    /// CameraBrowsingSystem 的运行时逻辑测试：纯逻辑、无 Camera、无 DOTween。
    /// 屏幕到世界的映射用简化恒等映射（屏幕像素 == 世界单位），便于断言。
    /// </summary>
    public sealed class CameraBrowsingSystemTests
    {
        private StubFrameSource mFrameSource;
        private StubScrollSource mScrollSource;
        private StubTargetResolver mTargetResolver;
        private StubWorldPointerMapper mPointerMapper;
        private StubPlacementGate mPlacementGate;
        private ICameraBrowsingModel mModel;
        private ICameraBrowsingSystem mSystem;

        private static CameraBrowsingConfig DefaultConfig()
        {
            return new CameraBrowsingConfig(
                zoomRange: new Vector2(1.0f, 40.0f),
                zoomStep: 1.12f,
                zoomToCursor: true,
                maxPanSpeed: 60.0f,
                inertiaEnabled: true,
                inertiaDuration: 0.45f,
                panOnEmptyArea: true,
                clampToBounds: false,
                bounds: new Vector4(0.0f, 0.0f, 0.0f, 0.0f));
        }

        [SetUp]
        public void SetUp()
        {
            BrowsingTestArchitecture.Reset();

            mFrameSource = new StubFrameSource();
            mScrollSource = new StubScrollSource();
            mTargetResolver = new StubTargetResolver();
            mPointerMapper = new StubWorldPointerMapper();
            mPlacementGate = new StubPlacementGate();

            BrowsingTestArchitecture.Configure(
                DefaultConfig(),
                mFrameSource,
                mScrollSource,
                mTargetResolver,
                mPointerMapper,
                mPlacementGate,
                Vector3.zero,
                10.0f);

            mModel = BrowsingTestArchitecture.Interface.GetModel<ICameraBrowsingModel>();
            mSystem = BrowsingTestArchitecture.Interface.GetSystem<ICameraBrowsingSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            BrowsingTestArchitecture.Reset();
        }

        [Test]
        public void DragOnEmptyArea_MovesFocalOppositeToDrag()
        {
            PressAndDrag();

            Assert.That(mModel.FocalPoint.Value.x, Is.EqualTo(-40.0f).Within(0.001f));
            Assert.That(mModel.FocalPoint.Value.y, Is.EqualTo(0.0f).Within(0.001f));
            Assert.That(mSystem.IsPanning, Is.True);
        }

        [Test]
        public void DragStartingOnTarget_DoesNotPan()
        {
            mTargetResolver.HasTarget = true;
            PressAndDrag();

            Assert.That(mModel.FocalPoint.Value, Is.EqualTo(Vector3.zero));
            Assert.That(mSystem.IsPanning, Is.False);
        }

        [Test]
        public void DragOverUI_DoesNotPan()
        {
            mPointerMapper.OverUI = true;
            PressAndDrag();

            Assert.That(mModel.FocalPoint.Value, Is.EqualTo(Vector3.zero));
            Assert.That(mSystem.IsPanning, Is.False);
        }

        [Test]
        public void PlacementBlocked_IgnoresPanAndZoom()
        {
            mPlacementGate.IsBlocked = true;

            mScrollSource.ScrollDelta = new Vector2(0.0f, 1.0f);
            mFrameSource.LatestFrame = Frame(Signal(PointerPhase.Move, new Vector2(50, 50), Vector2.zero, 0.0f));
            mSystem.ProcessFrame(0.0f);

            Assert.That(mModel.Zoom.Value, Is.EqualTo(10.0f).Within(0.001f));

            PressAndDrag();
            Assert.That(mModel.FocalPoint.Value, Is.EqualTo(Vector3.zero));
            Assert.That(mSystem.IsPanning, Is.False);
        }

        [Test]
        public void ReleaseWithoutDrag_DoesNotGlide()
        {
            mFrameSource.LatestFrame = Frame(Signal(PointerPhase.Down, new Vector2(100, 100), Vector2.zero, 1.0f));
            mSystem.ProcessFrame(1.0f);

            mFrameSource.LatestFrame = Frame(Signal(PointerPhase.Up, new Vector2(100, 100), Vector2.zero, 1.1f));
            mSystem.ProcessFrame(1.1f);

            // 无位移：不应产生惯性滑动。
            mFrameSource.LatestFrame = Frame();
            mSystem.ProcessFrame(1.2f);
            Assert.That(mModel.FocalPoint.Value, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void ReleaseWithVelocity_GlidesFocalInDragDirection()
        {
            // 快速向左拖（focal 向右减小）后松手，焦点应继续沿拖拽方向滑动并衰减。
            mFrameSource.LatestFrame = Frame(Signal(PointerPhase.Down, new Vector2(0, 0), Vector2.zero, 0.0f));
            mSystem.ProcessFrame(0.0f);

            mFrameSource.LatestFrame = Frame(Signal(PointerPhase.Move, new Vector2(10, 0), new Vector2(10, 0), 0.1f));
            mSystem.ProcessFrame(0.1f);

            mFrameSource.LatestFrame = Frame(Signal(PointerPhase.Move, new Vector2(30, 0), new Vector2(20, 0), 0.2f));
            mSystem.ProcessFrame(0.2f);

            mFrameSource.LatestFrame = Frame(Signal(PointerPhase.Up, new Vector2(30, 0), Vector2.zero, 0.25f));
            mSystem.ProcessFrame(0.25f);

            float releaseX = mModel.FocalPoint.Value.x;

            mFrameSource.LatestFrame = Frame();
            mSystem.ProcessFrame(0.3f);
            mSystem.ProcessFrame(0.4f);

            // 释放时焦点已为 -30，惯性应继续向 -x 移动。
            Assert.That(mModel.FocalPoint.Value.x, Is.LessThan(releaseX));
            Assert.That(mModel.FocalPoint.Value.x, Is.GreaterThan(-45.0f));
        }

        [Test]
        public void ScrollUp_ZoomIn_ReducesZoomValue()
        {
            mScrollSource.ScrollDelta = new Vector2(0.0f, 1.0f);
            mFrameSource.LatestFrame = Frame(Signal(PointerPhase.Move, new Vector2(50, 50), Vector2.zero, 0.0f));
            mSystem.ProcessFrame(0.0f);

            // 10 / 1.12 ≈ 8.9286
            Assert.That(mModel.Zoom.Value, Is.EqualTo(10.0f / 1.12f).Within(0.001f));
        }

        [Test]
        public void ScrollDown_ZoomOut_IncreasesZoomValue()
        {
            mScrollSource.ScrollDelta = new Vector2(0.0f, -1.0f);
            mFrameSource.LatestFrame = Frame(Signal(PointerPhase.Move, new Vector2(50, 50), Vector2.zero, 0.0f));
            mSystem.ProcessFrame(0.0f);

            // 10 * 1.12 = 11.2
            Assert.That(mModel.Zoom.Value, Is.EqualTo(11.2f).Within(0.001f));
        }

        [Test]
        public void ScrollUp_WithCursorAnchor_ShiftsFocalTowardCursor()
        {
            // 初始焦点 (0,0,0)，光标屏幕 (50,50) → 世界 (50,50,0)。
            mScrollSource.ScrollDelta = new Vector2(0.0f, 1.0f);
            mFrameSource.LatestFrame = Frame(Signal(PointerPhase.Move, new Vector2(50, 50), Vector2.zero, 0.0f));
            mSystem.ProcessFrame(0.0f);

            float ratio = (10.0f / 1.12f) / 10.0f; // ≈ 0.8929
            // F' = P + (F - P) * r，P = 光标世界点 (50,0)，F = (0,0) → F' ≈ 5.357（焦点向光标靠拢）。
            float expectedX = 50.0f + (0.0f - 50.0f) * ratio;
            Assert.That(mModel.FocalPoint.Value.x, Is.EqualTo(expectedX).Within(0.01f));
            Assert.That(mModel.FocalPoint.Value.x, Is.GreaterThan(0.0f));
        }

        [Test]
        public void Scroll_WithoutCursorAnchor_KeepsFocal()
        {
            BrowsingTestArchitecture.Reset();
            mFrameSource = new StubFrameSource();
            mScrollSource = new StubScrollSource();
            mTargetResolver = new StubTargetResolver();
            mPointerMapper = new StubWorldPointerMapper();
            mPlacementGate = new StubPlacementGate();

            CameraBrowsingConfig config = new CameraBrowsingConfig(
                zoomRange: new Vector2(1.0f, 40.0f),
                zoomStep: 1.12f,
                zoomToCursor: false,
                maxPanSpeed: 60.0f,
                inertiaEnabled: false,
                inertiaDuration: 0.45f,
                panOnEmptyArea: true,
                clampToBounds: false,
                bounds: new Vector4(0.0f, 0.0f, 0.0f, 0.0f));

            BrowsingTestArchitecture.Configure(
                config, mFrameSource, mScrollSource, mTargetResolver, mPointerMapper, mPlacementGate,
                Vector3.zero, 10.0f);
            mModel = BrowsingTestArchitecture.Interface.GetModel<ICameraBrowsingModel>();
            mSystem = BrowsingTestArchitecture.Interface.GetSystem<ICameraBrowsingSystem>();

            mScrollSource.ScrollDelta = new Vector2(0.0f, 1.0f);
            mFrameSource.LatestFrame = Frame(Signal(PointerPhase.Move, new Vector2(50, 50), Vector2.zero, 0.0f));
            mSystem.ProcessFrame(0.0f);

            Assert.That(mModel.FocalPoint.Value, Is.EqualTo(Vector3.zero));
            Assert.That(mModel.Zoom.Value, Is.EqualTo(10.0f / 1.12f).Within(0.001f));
        }

        [Test]
        public void ScrollUp_ClampedToZoomRangeMin()
        {
            mScrollSource.ScrollDelta = new Vector2(0.0f, 30.0f);
            mFrameSource.LatestFrame = Frame(Signal(PointerPhase.Move, new Vector2(50, 50), Vector2.zero, 0.0f));
            mSystem.ProcessFrame(0.0f);

            Assert.That(mModel.Zoom.Value, Is.EqualTo(1.0f).Within(0.001f));
        }

        [Test]
        public void ZoomRangeFromFactors_ComputesOrthoRange()
        {
            // base=5：2x 放大 → orthoSize 2.5（最小）；0.25x 缩小 → orthoSize 20（最大）。
            Vector2 range = CameraBrowsingConfig.ZoomRangeFromFactors(5.0f, 0.25f, 2.0f);

            Assert.That(range.x, Is.EqualTo(2.5f).Within(0.001f));
            Assert.That(range.y, Is.EqualTo(20.0f).Within(0.001f));
        }

        [Test]
        public void ZoomFromFactorRange_ScrollClampsAtFactorLimits()
        {
            BrowsingTestArchitecture.Reset();
            mFrameSource = new StubFrameSource();
            mScrollSource = new StubScrollSource();
            mTargetResolver = new StubTargetResolver();
            mPointerMapper = new StubWorldPointerMapper();
            mPlacementGate = new StubPlacementGate();

            CameraBrowsingConfig config = new CameraBrowsingConfig(
                zoomRange: CameraBrowsingConfig.ZoomRangeFromFactors(5.0f, 0.25f, 2.0f),
                zoomStep: 1.12f,
                zoomToCursor: false,
                maxPanSpeed: 60.0f,
                inertiaEnabled: false,
                inertiaDuration: 0.45f,
                panOnEmptyArea: true,
                clampToBounds: true,
                bounds: new Vector4(-100.0f, 100.0f, -100.0f, 100.0f));

            BrowsingTestArchitecture.Configure(
                config, mFrameSource, mScrollSource, mTargetResolver, mPointerMapper, mPlacementGate,
                Vector3.zero, 5.0f);
            mModel = BrowsingTestArchitecture.Interface.GetModel<ICameraBrowsingModel>();
            mSystem = BrowsingTestArchitecture.Interface.GetSystem<ICameraBrowsingSystem>();

            // 一路放大 → 2x（orthoSize 2.5）。
            mScrollSource.ScrollDelta = new Vector2(0.0f, 30.0f);
            mFrameSource.LatestFrame = Frame(Signal(PointerPhase.Move, new Vector2(50, 50), Vector2.zero, 0.0f));
            mSystem.ProcessFrame(0.0f);
            Assert.That(mModel.Zoom.Value, Is.EqualTo(2.5f).Within(0.001f));

            // 一路缩小 → 0.25x（orthoSize 20）。
            mScrollSource.ScrollDelta = new Vector2(0.0f, -30.0f);
            mSystem.ProcessFrame(0.0f);
            Assert.That(mModel.Zoom.Value, Is.EqualTo(20.0f).Within(0.001f));
        }

        [Test]
        public void MoveTo_ClampsToBounds()
        {
            BrowsingTestArchitecture.Reset();
            mFrameSource = new StubFrameSource();
            mScrollSource = new StubScrollSource();
            mTargetResolver = new StubTargetResolver();
            mPointerMapper = new StubWorldPointerMapper();
            mPlacementGate = new StubPlacementGate();

            CameraBrowsingConfig config = new CameraBrowsingConfig(
                zoomRange: new Vector2(1.0f, 40.0f),
                zoomStep: 1.12f,
                zoomToCursor: true,
                maxPanSpeed: 60.0f,
                inertiaEnabled: true,
                inertiaDuration: 0.45f,
                panOnEmptyArea: true,
                clampToBounds: true,
                bounds: new Vector4(-10.0f, 10.0f, -10.0f, 10.0f));

            BrowsingTestArchitecture.Configure(
                config, mFrameSource, mScrollSource, mTargetResolver, mPointerMapper, mPlacementGate,
                Vector3.zero, 10.0f);
            mModel = BrowsingTestArchitecture.Interface.GetModel<ICameraBrowsingModel>();
            mSystem = BrowsingTestArchitecture.Interface.GetSystem<ICameraBrowsingSystem>();

            mSystem.MoveTo(new Vector3(50.0f, -50.0f, 0.0f));

            Assert.That(mModel.FocalPoint.Value.x, Is.EqualTo(10.0f).Within(0.001f));
            Assert.That(mModel.FocalPoint.Value.y, Is.EqualTo(-10.0f).Within(0.001f));
        }

        [Test]
        public void FocusCommand_MovesFocalToTarget()
        {
            BrowsingTestArchitecture.Interface.SendCommand(new FocusCameraCommand(new Vector3(5.0f, 7.0f, 0.0f)));

            Assert.That(mModel.FocalPoint.Value.x, Is.EqualTo(5.0f).Within(0.001f));
            Assert.That(mModel.FocalPoint.Value.y, Is.EqualTo(7.0f).Within(0.001f));
        }

        [Test]
        public void SetCameraZoomCommand_ClampsToRange()
        {
            BrowsingTestArchitecture.Interface.SendCommand(new SetCameraZoomCommand(1000.0f));

            Assert.That(mModel.Zoom.Value, Is.EqualTo(40.0f).Within(0.001f));
        }

        private void PressAndDrag()
        {
            mFrameSource.LatestFrame = Frame(Signal(PointerPhase.Down, new Vector2(100, 100), Vector2.zero, 1.0f));
            mSystem.ProcessFrame(1.0f);

            mFrameSource.LatestFrame = Frame(
                Signal(PointerPhase.Move, new Vector2(120, 100), new Vector2(20, 0), 1.1f));
            mSystem.ProcessFrame(1.1f);

            mFrameSource.LatestFrame = Frame(
                Signal(PointerPhase.Move, new Vector2(140, 100), new Vector2(20, 0), 1.2f));
            mSystem.ProcessFrame(1.2f);
        }

        private static PointerSignal Signal(
            PointerPhase phase,
            Vector2 position,
            Vector2 delta,
            float time)
        {
            return new PointerSignal(0, PointerButton.Left, phase, position, delta, time);
        }

        private static PointerFrameEvent Frame(params PointerSignal[] signals)
        {
            Vector2 position = signals != null && signals.Length > 0
                ? signals[0].ScreenPosition
                : Vector2.zero;
            return new PointerFrameEvent(position, signals ?? new PointerSignal[0]);
        }

        private sealed class StubFrameSource : IPointerFrameSource
        {
            public PointerFrameEvent? LatestFrame { get; set; }
        }

        private sealed class StubScrollSource : IScrollWheelSource
        {
            public Vector2 ScrollDelta { get; set; }
        }

        private sealed class StubTargetResolver : ITargetResolver
        {
            public bool HasTarget;

            public bool TryResolve(in PointerSignal signal, out InteractionHit hit)
            {
                hit = new InteractionHit(
                    HasTarget ? (IInteractionTarget)new TestTarget() : null,
                    new Ray(),
                    Vector3.zero,
                    Vector3.zero,
                    0.0f);
                return HasTarget;
            }
        }

        private sealed class TestTarget : IInteractionTarget
        {
            public bool IsAvailable
            {
                get { return true; }
            }
        }

        private sealed class StubWorldPointerMapper : IWorldPointerMapper
        {
            public bool OverUI;

            public bool TryMapScreenToWorld(Vector2 screenPosition, out Vector3 worldPosition)
            {
                // 简化恒等映射：屏幕像素 == 世界单位。
                worldPosition = new Vector3(screenPosition.x, screenPosition.y, 0.0f);
                return true;
            }

            public bool IsOverUI(Vector2 screenPosition)
            {
                return OverUI;
            }
        }

        private sealed class StubPlacementGate : IPlacementInputGate
        {
            public bool IsBlocked { get; set; }
        }

        /// <summary>测试专用架构：注册浏览域所需的全部依赖与真实 Model / System。</summary>
        private sealed class BrowsingTestArchitecture : Architecture<BrowsingTestArchitecture>
        {
            private static CameraBrowsingConfig sConfig;
            private static StubFrameSource sFrameSource;
            private static StubScrollSource sScrollSource;
            private static StubTargetResolver sTargetResolver;
            private static StubWorldPointerMapper sPointerMapper;
            private static StubPlacementGate sPlacementGate;
            private static Vector3 sInitialFocal;
            private static float sInitialZoom;

            public static void Configure(
                CameraBrowsingConfig config,
                StubFrameSource frameSource,
                StubScrollSource scrollSource,
                StubTargetResolver targetResolver,
                StubWorldPointerMapper pointerMapper,
                StubPlacementGate placementGate,
                Vector3 initialFocal,
                float initialZoom)
            {
                sConfig = config;
                sFrameSource = frameSource;
                sScrollSource = scrollSource;
                sTargetResolver = targetResolver;
                sPointerMapper = pointerMapper;
                sPlacementGate = placementGate;
                sInitialFocal = initialFocal;
                sInitialZoom = initialZoom;
            }

            public static void Reset()
            {
                if (mArchitecture != null)
                {
                    mArchitecture.Deinit();
                }

                sFrameSource = null;
                sScrollSource = null;
                sTargetResolver = null;
                sPointerMapper = null;
                sPlacementGate = null;
                sConfig = default;
                sInitialFocal = Vector3.zero;
                sInitialZoom = 0.0f;
            }

            protected override void Init()
            {
                RegisterUtility<IPointerFrameSource>(sFrameSource);
                RegisterUtility<IScrollWheelSource>(sScrollSource);
                RegisterUtility<ITargetResolver>(sTargetResolver);
                RegisterUtility<IWorldPointerMapper>(sPointerMapper);
                RegisterUtility<IPlacementInputGate>(sPlacementGate);
                RegisterModel<ICameraBrowsingModel>(new CameraBrowsingModel(sInitialFocal, sInitialZoom));
                RegisterSystem<ICameraBrowsingSystem>(new CameraBrowsingSystem(sConfig));
            }
        }
    }
}
