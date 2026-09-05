using CUC260905.Economy;
using CUC260905.Interaction;
using QFramework;
using UnityEngine;
using UnityEngine.UI;

namespace CUC260905.Placement
{
    /// <summary>
    /// “新建服务器”工具栏按钮（表现层 + 轻量协调）。
    /// 职责：余额不足时禁用按钮（Button.interactable）；服务器节点放置成功后经
    /// IEconomySystem 扣除建设费用；取消放置不进入放置完成事件，因此不扣费。
    /// 不直接写余额，扣费统一走 IEconomySystem。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NewServerButtonController : MonoBehaviour, IController
    {
        [SerializeField, Tooltip("被门控的 uGUI 按钮；留空时取本物体上的 Button。")]
        private Button mButton;

        [SerializeField, Tooltip("新建服务器对应的节点 prefab；只有放置该 prefab 成功才扣费。")]
        private GameObject mServerPrefab;

        [SerializeField, Min(1), Tooltip("新建服务器的建设费用（金币）。")]
        private int mCost = 30;

        private IEconomyModel mEconomyModel;
        private IEconomySystem mEconomySystem;
        private IUnRegister mBalanceRegistration;
        private IUnRegister mPlacedRegistration;

        private void Awake()
        {
            if (mButton == null)
            {
                mButton = GetComponent<Button>();
            }
        }

        // Start 晚于 InputController.Awake，确保 GameArchitecture 已装配。
        private void Start()
        {
            mEconomyModel = this.GetModel<IEconomyModel>();
            mEconomySystem = this.GetSystem<IEconomySystem>();
            if (mEconomyModel != null)
            {
                mBalanceRegistration = mEconomyModel.Balance.Register(OnBalanceChanged);
            }

            if (mServerPrefab != null)
            {
                mPlacedRegistration = this.RegisterEvent<PlacementPlacedEvent>(OnPlacementPlaced);
            }

            RefreshInteractable();
        }

        private void OnDestroy()
        {
            mBalanceRegistration?.UnRegister();
            mPlacedRegistration?.UnRegister();
        }

        private void OnBalanceChanged(int _)
        {
            RefreshInteractable();
        }

        private void RefreshInteractable()
        {
            if (mButton == null)
            {
                return;
            }

            mButton.interactable = mEconomyModel != null && mEconomyModel.Balance.Value >= mCost;
        }

        private void OnPlacementPlaced(PlacementPlacedEvent placedEvent)
        {
            // 仅对“本按钮发起的新建服务器”扣费：放置的 prefab 必须与按钮配置一致。
            if (mServerPrefab == null || !ReferenceEquals(placedEvent.Prefab, mServerPrefab))
            {
                return;
            }

            // 放置成功视为完成新建服务器，此时才扣费；取消流程不会到达此处。
            if (mEconomySystem != null && !mEconomySystem.Consume(mCost))
            {
                Debug.LogWarning(
                    $"新建服务器扣费失败：余额不足 {mCost}G，服务器已放置但未扣费。", this);
            }

            RefreshInteractable();
        }

        IArchitecture IBelongToArchitecture.GetArchitecture()
        {
            return GameArchitecture.Interface;
        }
    }
}
