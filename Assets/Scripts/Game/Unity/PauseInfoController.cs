using CUC260905.Interaction;
using QFramework;
using TMPro;
using UnityEngine;

namespace CUC260905.Game
{
    /// <summary>
    /// 暂停提示（PauseInfo）的表现控制器：进入暂停时显示，恢复时隐藏。
    /// 显示动画：0.5s 内按 easeOutCubic 从停靠位置 + (0,-100) 的偏移滑入停靠位置，
    /// 同时透明度从 0 淡入到场景配置值；结束时反向播放同一条动画（淡出并滑回偏移位），
    /// 完成后隐藏对象（SetActive(false)）。
    /// 动画使用非缩放时间：进入暂停瞬间 Time.timeScale = 0，若按缩放时间驱动淡入会立刻冻结、
    /// 永远无法推进；恢复时时间缩放虽已还原，这里统一使用非缩放时间保证确定性。
    /// 状态以进度（0 = 隐藏 ~ 1 = 显示）驱动，中途切换方向时从当前进度反向继续，不闪断。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PauseInfoController : MonoBehaviour, IController
    {
        [Header("动画")]
        [SerializeField, Min(0.01f), Tooltip("显示/隐藏动画时长（秒），默认 0.5s。")]
        private float mDuration = 0.5f;
        [SerializeField, Tooltip("显示起始位置相对停靠位置的偏移，默认 (0, -100)。")]
        private Vector2 mHiddenOffset = new Vector2(0f, -100f);

        private RectTransform mRectTransform;
        private TextMeshProUGUI mText;
        private IUnRegister mPausedRegistration;
        private IUnRegister mResumedRegistration;

        private Vector2 mHomePosition;
        private float mFullAlpha;
        private bool mTargetShown;
        private float mProgress;
        private bool mInitialized;

        private void Start()
        {
            EnsureInitialized();
        }

        private void Update()
        {
            EnsureInitialized();
            if (!mInitialized || !Application.isPlaying)
            {
                return;
            }

            float target = mTargetShown ? 1f : 0f;
            if (Mathf.Approximately(mProgress, target))
            {
                return;
            }

            // 暂停期间 timeScale = 0，必须用非缩放时间推进动画。
            mProgress = Mathf.MoveTowards(mProgress, target, Time.unscaledDeltaTime / mDuration);
            ApplyState(mProgress);

            if (mProgress <= 0f)
            {
                gameObject.SetActive(false);
            }
        }

        private void OnGamePaused(GamePausedEvent _)
        {
            // 事件在总线同步分发，即使对象当前被隐藏也能收到回调。
            gameObject.SetActive(true);
            mTargetShown = true;
        }

        private void OnGameResumed(GameResumedEvent _)
        {
            mTargetShown = false;
        }

        private void EnsureInitialized()
        {
            if (mInitialized)
            {
                return;
            }

            mRectTransform = GetComponent<RectTransform>();
            mText = GetComponent<TextMeshProUGUI>();
            if (mRectTransform == null || mText == null)
            {
                Debug.LogError("PauseInfoController 需要挂在包含 RectTransform 与 TextMeshProUGUI 的 UI 对象上。", this);
                enabled = false;
                return;
            }

            // 以场景配置的停靠位置与文字透明度为动画基准（隐藏偏移叠加在停靠位置之上）。
            mHomePosition = mRectTransform.anchoredPosition;
            mFullAlpha = mText.color.a;

            mPausedRegistration = this.RegisterEvent<GamePausedEvent>(OnGamePaused);
            mResumedRegistration = this.RegisterEvent<GameResumedEvent>(OnGameResumed);

            // 若场景加载时已处于暂停（例如直接以暂停态进入），落到显示完成态而非播放入场。
            IGamePauseState pauseState = this.GetModel<IGamePauseState>();
            bool alreadyPaused = pauseState != null && pauseState.IsPaused.Value;
            mTargetShown = alreadyPaused;
            mProgress = alreadyPaused ? 1f : 0f;
            ApplyState(mProgress);
            gameObject.SetActive(alreadyPaused);

            mInitialized = true;
        }

        private void ApplyState(float progress)
        {
            float eased = EaseOutCubic(progress);
            Color color = mText.color;
            color.a = Mathf.Lerp(0f, mFullAlpha, eased);
            mText.color = color;
            mRectTransform.anchoredPosition = Vector2.Lerp(mHomePosition + mHiddenOffset, mHomePosition, eased);
        }

        /// <summary>三次缓出曲线：1 - (1 - t)^3。</summary>
        private static float EaseOutCubic(float value)
        {
            float inverse = 1f - value;
            return 1f - inverse * inverse * inverse;
        }

        private void OnDestroy()
        {
            mPausedRegistration?.UnRegister();
            mResumedRegistration?.UnRegister();
        }

        IArchitecture IBelongToArchitecture.GetArchitecture()
        {
            return GameArchitecture.Interface;
        }
    }
}
