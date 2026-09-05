using CUC260905.Interaction;
using QFramework;
using UnityEngine;

namespace CUC260905.Game
{
    /// <summary>
    /// 空格切换暂停的控制器。
    /// 暂停语义（按需求确认）：模拟时间冻结（Time.timeScale = 0，冻结所有缩放时间驱动的
    /// 数据包/生成/负载/淡出动画），相机浏览保留（相机使用非缩放时间），
    /// 相机浏览、服务器部署和节点连线可继续操作；其他世界交互由输入路由按能力抑制。
    /// 进入暂停时收束已有指针手势，但保留尚未完成的服务器部署模式。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GamePauseController : MonoBehaviour, IController, ICanSendEvent
    {
        private IGamePauseState mPauseState;
        private IInteractionInputSystem mInputSystem;
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

            // 旧 Input Manager 的按键读取不受 timeScale 影响，暂停期间仍能响应全局快捷键。
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                QuitGame();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Space))
            {
                TogglePause();
            }
        }

        /// <summary>退出独立运行程序；编辑器内结束 Play Mode，便于直接验证 Esc 行为。</summary>
        private static void QuitGame()
        {
            Application.Quit();

#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
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

            // 清理暂停瞬间已经开始的手势，避免它们跨状态继续；暂停后新建的连线仍可正常处理。
            mInputSystem?.CancelAll();

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
