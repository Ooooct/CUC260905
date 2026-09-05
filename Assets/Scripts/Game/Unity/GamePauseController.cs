using CUC260905.Interaction;
using CUC260905.Placement;
using QFramework;
using UnityEngine;

namespace CUC260905.Game
{
    /// <summary>
    /// 空格切换暂停的控制器。
    /// 暂停语义（按需求确认）：模拟时间冻结（Time.timeScale = 0，冻结所有缩放时间驱动的
    /// 数据包/生成/负载/淡出动画），相机浏览保留（相机使用非缩放时间），
    /// 世界交互被抑制（由 InteractionInputSystem / PlacementSystem / NetworkConnectionController
    /// 读取 IGamePauseState 自行门控）。
    /// 进入暂停时收束进行中的交互会话与放置模式，避免跨暂停残留拖拽/幽灵状态。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GamePauseController : MonoBehaviour, IController, ICanSendEvent
    {
        private IGamePauseState mPauseState;
        private IInteractionInputSystem mInputSystem;
        private IPlacementSystem mPlacementSystem;
        private float mTimeScaleBeforePause = 1.0f;
        private bool mInitialized;

        private void Start()
        {
            EnsureInitialized();
        }

        private void Update()
        {
            if (!mInitialized)
            {
                EnsureInitialized();
                if (!mInitialized)
                {
                    return;
                }
            }

            // 旧 Input Manager 的按键读取不受 timeScale 影响，暂停期间仍能响应空格。
            if (Input.GetKeyDown(KeyCode.Space))
            {
                TogglePause();
            }
        }

        /// <summary>按当前状态切换暂停/恢复；可由 UI 按钮等外部入口复用。</summary>
        public void TogglePause()
        {
            if (!mInitialized)
            {
                EnsureInitialized();
                if (!mInitialized)
                {
                    return;
                }
            }

            if (mPauseState.IsPaused.Value)
            {
                Resume();
            }
            else
            {
                Pause();
            }
        }

        /// <summary>进入暂停；已在暂停中时不做任何事。</summary>
        public void Pause()
        {
            if (!mInitialized || mPauseState == null || mPauseState.IsPaused.Value)
            {
                return;
            }

            mPauseState.IsPaused.Value = true;

            // 收束进行中的指针会话（拖拽/悬浮），防止暂停期间漏掉的 Up 让会话残留到恢复后。
            mInputSystem?.CancelAll();

            // 收束放置模式：放置是独占输入，若带着放置模式暂停会继续阻塞相机浏览。
            mPlacementSystem?.Cancel();

            mTimeScaleBeforePause = Time.timeScale;
            Time.timeScale = 0.0f;
            this.SendEvent(new GamePausedEvent());
        }

        /// <summary>取消暂停；未在暂停中时不做任何事。</summary>
        public void Resume()
        {
            if (!mInitialized || mPauseState == null || !mPauseState.IsPaused.Value)
            {
                return;
            }

            Time.timeScale = mTimeScaleBeforePause;
            mPauseState.IsPaused.Value = false;
            this.SendEvent(new GameResumedEvent());
        }

        private void OnDestroy()
        {
            // 场景卸载时若仍处于暂停，恢复时间缩放，避免下一个场景以冻结状态启动。
            if (mPauseState != null && mPauseState.IsPaused.Value)
            {
                Time.timeScale = mTimeScaleBeforePause;
                mPauseState.IsPaused.Value = false;
            }
        }

        private void EnsureInitialized()
        {
            if (mInitialized)
            {
                return;
            }

            // Start 晚于 InputController.Awake，GameArchitecture 已完成装配。
            mPauseState = this.GetModel<IGamePauseState>();
            mInputSystem = this.GetSystem<IInteractionInputSystem>();
            mPlacementSystem = this.GetSystem<IPlacementSystem>();
            if (mPauseState == null)
            {
                Debug.LogError("GamePauseController 未找到 IGamePauseState，请确认场景存在 InputController。", this);
                enabled = false;
                return;
            }

            mInitialized = true;
        }

        IArchitecture IBelongToArchitecture.GetArchitecture()
        {
            return GameArchitecture.Interface;
        }
    }
}
