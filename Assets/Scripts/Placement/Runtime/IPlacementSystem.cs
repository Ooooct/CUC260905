using CUC260905.Game;
using CUC260905.Interaction;
using QFramework;
using UnityEngine;

namespace CUC260905.Placement
{
    /// <summary>放置系统的业务入口与每帧调度入口。</summary>
    public interface IPlacementSystem : ISystem
    {
        bool IsPlacing { get; }

        void Begin(GameObject prefab);

        void Cancel();

        void TryPlace(Vector3 worldPosition);

        void ProcessFrame(float unscaledTime);
    }

    /// <summary>
    /// 放置规则 System。不轮询 Unity 生命周期，也不直接读 Unity Input；
    /// 每帧由激活的 PlacementButton 调用 ProcessFrame，输入数据来自 IPointerFrameSource。
    /// </summary>
    public sealed class PlacementSystem : AbstractSystem, IPlacementSystem
    {
        private IPlacementModel mModel;
        private IWorldPointerMapper mPointerMapper;
        private IPlacementInstantiator mInstantiator;
        private IPointerFrameSource mFrameSource;
        private IInteractionInputSystem mInteractionInput;
        private IGamePauseState mPauseState;
        private Vector3 mLastWorldPosition;

        public bool IsPlacing
        {
            get { return mModel != null && mModel.IsPlacing.Value; }
        }

        protected override void OnInit()
        {
            mModel = this.GetModel<IPlacementModel>();
            mPointerMapper = this.GetUtility<IWorldPointerMapper>();
            mInstantiator = this.GetUtility<IPlacementInstantiator>();
            mFrameSource = this.GetUtility<IPointerFrameSource>();
            mInteractionInput = this.GetSystem<IInteractionInputSystem>();
            mPauseState = this.GetModel<IGamePauseState>();

            if (mModel == null ||
                mPointerMapper == null ||
                mInstantiator == null ||
                mFrameSource == null)
            {
                throw new System.InvalidOperationException(
                    "PlacementSystem 初始化前必须注册 IPlacementModel、IWorldPointerMapper、IPlacementInstantiator、IPointerFrameSource。");
            }
        }

        protected override void OnDeinit()
        {
            Cancel();
        }

        /// <summary>进入放置模式；若已在放置模式则仅切换 prefab。暂停期间拒绝进入。</summary>
        public void Begin(GameObject prefab)
        {
            if (prefab == null || (mPauseState != null && mPauseState.IsPaused.Value))
            {
                return;
            }

            bool wasPlacing = IsPlacing;
            mModel.SelectedPrefab.Value = prefab;
            if (!wasPlacing)
            {
                mModel.IsPlacing.Value = true;
                mInteractionInput?.CancelAll();
                this.SendEvent(new PlacementStartedEvent(prefab));
            }
        }

        public void Cancel()
        {
            if (!IsPlacing)
            {
                return;
            }

            this.SendEvent(new PlacementCancelledEvent());
            EndPlacement();
        }

        /// <summary>在世界坐标放置当前 prefab，单次放置后退出放置模式。</summary>
        public void TryPlace(Vector3 worldPosition)
        {
            if (!IsPlacing)
            {
                return;
            }

            GameObject prefab = mModel.SelectedPrefab.Value;
            if (prefab == null)
            {
                Cancel();
                return;
            }

            GameObject instance = mInstantiator.Instantiate(prefab, worldPosition, prefab.transform.rotation);
            this.SendEvent(new PlacementPlacedEvent(prefab, worldPosition, instance));
            EndPlacement();
        }

        /// <summary>每帧入口：由激活的 PlacementButton 调用，消费最近一帧指针数据。暂停期间冻结放置。</summary>
        public void ProcessFrame(float unscaledTime)
        {
            if (!IsPlacing || (mPauseState != null && mPauseState.IsPaused.Value))
            {
                return;
            }

            PointerFrameEvent? frame = mFrameSource.LatestFrame;
            if (frame == null)
            {
                return;
            }

            PointerFrameEvent f = frame.Value;
            if (mPointerMapper.TryMapScreenToWorld(f.ScreenPosition, out Vector3 worldPosition))
            {
                mLastWorldPosition = worldPosition;
                mModel.PointerWorldPosition.Value = worldPosition;
            }

            if (f.Signals == null)
            {
                return;
            }

            foreach (PointerSignal signal in f.Signals)
            {
                if (signal.Phase != PointerPhase.Down)
                {
                    continue;
                }

                if (signal.Button == PointerButton.Left)
                {
                    // UI 上的点击不触发世界放置（例如点其他工具栏按钮）。
                    if (!mPointerMapper.IsOverUI(signal.ScreenPosition))
                    {
                        TryPlace(mLastWorldPosition);
                    }
                }
                else if (signal.Button == PointerButton.Right)
                {
                    // 右键取消不设 UI 拦截，任何位置都可退出。
                    Cancel();
                }
            }
        }

        private void EndPlacement()
        {
            mModel.IsPlacing.Value = false;
            mModel.SelectedPrefab.Value = null;
        }
    }
}
