using CUC260905.Interaction;
using CUC260905.Network;
using QFramework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using DG.Tweening;

namespace CUC260905.Game
{
    /// <summary>
    /// 游戏结束面板的表现控制器：首次收到结束事件后冻结模拟时间，
    /// 用非缩放时间从左侧弹入，并在一秒内展示本局累计总传输量。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameOverPanelController : MonoBehaviour, IController
    {
        [Header("界面引用")]
        [SerializeField] private TMP_Text mScoreText;
        [SerializeField] private Button mReturnToMenuButton;

        [Header("入场动画")]
        [SerializeField, Min(0.01f)] private float mSlideDuration = 0.25f;
        [SerializeField] private Ease mSlideEase = Ease.OutBack;
        [SerializeField, Min(0f)] private float mHiddenPadding = 20f;

        [Header("分数动画")]
        [SerializeField, Min(0.01f)] private float mScoreDuration = 1f;

        private RectTransform mPanelTransform;
        private IPacketTrafficStatsModel mTrafficStats;
        private IUnRegister mGameOverRegistration;
        private Vector2 mShownPosition;
        private double mTargetMegabits;
        private float mScoreElapsed;
        private bool mHasGameOver;
        private bool mIsScoreAnimating;
        private Tweener mSlideTween;

        private void Awake()
        {
            mPanelTransform = transform as RectTransform;
            if (mPanelTransform == null)
            {
                Debug.LogError("GameOverPanelController 必须挂在 RectTransform 上。", this);
                enabled = false;
                return;
            }

            mShownPosition = mPanelTransform.anchoredPosition;
            SetHiddenPosition();
            SetScoreText(0d);
        }

        private void Start()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            mTrafficStats = this.GetModel<IPacketTrafficStatsModel>();
            mGameOverRegistration = this.RegisterEvent<GameOverEvent>(OnGameOver);
            if (mReturnToMenuButton != null)
            {
                mReturnToMenuButton.onClick.AddListener(ReturnToMainMenu);
            }
        }

        private void Update()
        {
            if (!mHasGameOver)
            {
                return;
            }

            // 结束态比暂停输入优先，任何后续全局快捷键都不能恢复模拟时间。
            Time.timeScale = 0f;

            if (!mIsScoreAnimating)
            {
                return;
            }

            mScoreElapsed = Mathf.Min(mScoreElapsed + Time.unscaledDeltaTime, mScoreDuration);
            float progress = mScoreElapsed / mScoreDuration;
            SetScoreText(mTargetMegabits * progress);
            if (progress >= 1f)
            {
                mIsScoreAnimating = false;
                SetScoreText(mTargetMegabits);
            }
        }

        /// <summary>供结束页按钮调用；场景切换前恢复时间，避免主菜单保持冻结。</summary>
        public void ReturnToMainMenu()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("MainMenu");
        }

        private void OnGameOver(GameOverEvent _)
        {
            if (mHasGameOver)
            {
                return;
            }

            mHasGameOver = true;
            Time.timeScale = 0f;
            mTargetMegabits = mTrafficStats == null ? 0d : mTrafficStats.TotalMegabits.Value;
            mScoreElapsed = 0f;
            mIsScoreAnimating = true;
            SetScoreText(0d);

            mSlideTween?.Kill();
            mSlideTween = mPanelTransform.DOAnchorPos(mShownPosition, mSlideDuration)
                .SetEase(mSlideEase)
                .SetUpdate(true);
        }

        private void SetHiddenPosition()
        {
            float hiddenX = -(mPanelTransform.rect.width * mPanelTransform.pivot.x + mHiddenPadding);
            mPanelTransform.anchoredPosition = new Vector2(hiddenX, mShownPosition.y);
        }

        private void SetScoreText(double megabits)
        {
            if (mScoreText == null)
            {
                return;
            }

            mScoreText.SetText("{0:0.0} Mb", (float)megabits);
        }

        private void OnDestroy()
        {
            mGameOverRegistration?.UnRegister();
            mGameOverRegistration = null;
            mSlideTween?.Kill();
            mSlideTween = null;
            if (mReturnToMenuButton != null)
            {
                mReturnToMenuButton.onClick.RemoveListener(ReturnToMainMenu);
            }
        }

        IArchitecture IBelongToArchitecture.GetArchitecture()
        {
            return GameArchitecture.Interface;
        }
    }
}
