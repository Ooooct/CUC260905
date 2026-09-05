using CUC260905.Interaction;
using QFramework;
using UnityEngine;

namespace CUC260905.Placement
{
    /// <summary>
    /// 工具栏放置按钮：点击进入放置模式并成为唯一激活驱动者；
    /// 其 Update 在放置期间驱动 PlacementSystem.ProcessFrame。
    /// 静态 sActiveDriver 保证同一时刻只有一个按钮驱动，避免一次左键放置多个。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlacementButton : MonoBehaviour, IController
    {
        [SerializeField] private GameObject mPrefab;

        private IPlacementSystem mSystem;
        private static PlacementButton sActiveDriver;

        /// <summary>uGUI Button.onClick 绑定入口。</summary>
        public void OnButtonClick()
        {
            sActiveDriver = this;
            EnsureSystem();
            this.SendCommand(new BeginPlacementCommand(mPrefab));
        }

        private void Update()
        {
            if (sActiveDriver != this)
            {
                return;
            }

            EnsureSystem();
            if (mSystem != null && mSystem.IsPlacing)
            {
                mSystem.ProcessFrame(Time.unscaledTime);
            }
        }

        // 延迟解析：确保 GameArchitecture 已在 InputController.Awake 中装配完成。
        private void EnsureSystem()
        {
            mSystem ??= this.GetSystem<IPlacementSystem>();
        }

        IArchitecture IBelongToArchitecture.GetArchitecture()
        {
            return GameArchitecture.Interface;
        }
    }
}
