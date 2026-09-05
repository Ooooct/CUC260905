using CUC260905.Interaction;
using QFramework;
using TMPro;
using UnityEngine;

namespace CUC260905.Economy
{
    /// <summary>
    /// 货币显示控制器（表现层）。
    /// 职责单一：订阅模型余额变化并刷新 TMP 文本。
    /// 不持有业务数据，也不直接修改余额——增加/消耗统一走 IEconomySystem。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class EconomyController : MonoBehaviour, IController
    {
        [Header("Display")]
        [SerializeField]
        [Tooltip("显示货币余额的 TextMeshPro 文本对象。")]
        private TMP_Text mBalanceText;

        [SerializeField]
        [Tooltip("余额文本格式，{0} 为余额占位符。")]
        private string mBalanceFormat = "{0}";

        private IEconomyModel mModel;

        // Start 晚于 InputController.Awake，确保 GameArchitecture 已装配。
        private void Start()
        {
            mModel = this.GetModel<IEconomyModel>();
            if (mModel == null)
            {
                Debug.LogWarning($"[{name}] 未找到 IEconomyModel，跳过余额订阅。", this);
                return;
            }

            // RegisterWithInitValue：先以当前余额刷新一次，之后每次变化都刷新；
            // 句柄随物体销毁自动取消注册，避免泄漏。
            mModel.Balance.RegisterWithInitValue(OnBalanceChanged)
                .UnRegisterWhenGameObjectDestroyed(this);
        }

        private void OnBalanceChanged(int balance)
        {
            if (mBalanceText == null)
            {
                return;
            }

            string format = string.IsNullOrEmpty(mBalanceFormat) ? "{0}" : mBalanceFormat;
            mBalanceText.text = string.Format(format, balance);
        }

        IArchitecture IBelongToArchitecture.GetArchitecture()
        {
            return GameArchitecture.Interface;
        }
    }
}
