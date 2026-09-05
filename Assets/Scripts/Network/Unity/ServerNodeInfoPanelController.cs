using CUC260905.Economy;
using CUC260905.Interaction;
using QFramework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CUC260905.Network
{
    /// <summary>
    /// 服务器信息面板（表现层）。监听服务器点击事件，展示当前能力，
    /// 并将两个升级按钮转发给 IServerUpgradeSystem。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ServerNodeInfoPanelController : MonoBehaviour, IController
    {
        [Header("显示")]
        [SerializeField] private TMP_Text mServerNameText;
        [SerializeField] private TMP_Text mNodeIdText;
        [SerializeField] private TMP_Text mDataProcessingText;
        [SerializeField] private TMP_Text mMaxConnectionsText;
        [SerializeField] private TMP_Text mDataUpgradeLabel;
        [SerializeField] private TMP_Text mConnectionUpgradeLabel;

        [Header("操作")]
        [SerializeField] private Button mDataUpgradeButton;
        [SerializeField] private Button mConnectionUpgradeButton;
        [SerializeField] private ServerUpgradeConfig mUpgradeConfig;

        private IServerUpgradeSystem mUpgradeSystem;
        private INetworkTopologySystem mTopologySystem;
        private IEconomyModel mEconomyModel;
        private ServerNodeCapabilities mSelectedCapabilities;
        private string mSelectedNodeId;
        private IUnRegister mDataValueRegistration;
        private IUnRegister mCurrentLoadRegistration;
        private IUnRegister mConnectionValueRegistration;
        private IUnRegister mDataLevelRegistration;
        private IUnRegister mConnectionLevelRegistration;
        private IUnRegister mBalanceRegistration;
        private IUnRegister mNodeClickedRegistration;

        private void Awake()
        {
            if (mDataUpgradeButton != null)
            {
                mDataUpgradeButton.onClick.AddListener(OnDataUpgradeClicked);
            }

            if (mConnectionUpgradeButton != null)
            {
                mConnectionUpgradeButton.onClick.AddListener(OnConnectionUpgradeClicked);
            }
        }

        // Start 晚于 InputController.Awake，确保 GameArchitecture 已装配。
        private void Start()
        {
            mUpgradeSystem = this.GetSystem<IServerUpgradeSystem>();
            mTopologySystem = this.GetSystem<INetworkTopologySystem>();
            mEconomyModel = this.GetModel<IEconomyModel>();
            mNodeClickedRegistration = this.RegisterEvent<ServerNodeClickedEvent>(OnServerNodeClicked);

            if (mEconomyModel != null)
            {
                mBalanceRegistration = mEconomyModel.Balance.Register(OnBalanceChanged);
            }

            ClearSelection();
        }

        private void OnDestroy()
        {
            if (mDataUpgradeButton != null)
            {
                mDataUpgradeButton.onClick.RemoveListener(OnDataUpgradeClicked);
            }

            if (mConnectionUpgradeButton != null)
            {
                mConnectionUpgradeButton.onClick.RemoveListener(OnConnectionUpgradeClicked);
            }

            ClearCapabilityBindings();
            mBalanceRegistration?.UnRegister();
            mNodeClickedRegistration?.UnRegister();
        }

        private void OnServerNodeClicked(ServerNodeClickedEvent clickedEvent)
        {
            if (clickedEvent.Capabilities == null || string.IsNullOrWhiteSpace(clickedEvent.Node.NodeId))
            {
                ClearSelection();
                return;
            }

            ClearCapabilityBindings();
            mSelectedNodeId = clickedEvent.Node.NodeId;
            mSelectedCapabilities = clickedEvent.Capabilities;
            if (mServerNameText != null)
            {
                mServerNameText.text = string.IsNullOrWhiteSpace(clickedEvent.Node.DisplayName)
                    ? clickedEvent.Node.NodeId
                    : clickedEvent.Node.DisplayName;
            }

            if (mNodeIdText != null)
            {
                mNodeIdText.text = string.IsNullOrWhiteSpace(clickedEvent.Node.NodeId)
                    ? "-"
                    : string.Format("ID: {0}", clickedEvent.Node.NodeId);
            }

            mDataValueRegistration = mSelectedCapabilities.DataProcessingPerSecond
                .RegisterWithInitValue(OnDataProcessingChanged);
            mCurrentLoadRegistration = mSelectedCapabilities.CurrentDataLoadPerSecond
                .RegisterWithInitValue(OnCurrentLoadChanged);
            mConnectionValueRegistration = mSelectedCapabilities.MaxConnections
                .RegisterWithInitValue(OnMaxConnectionsChanged);
            mDataLevelRegistration = mSelectedCapabilities.DataThroughputLevel
                .Register(_ => RefreshUpgradeButtons());
            mConnectionLevelRegistration = mSelectedCapabilities.MaxConnectionsLevel
                .Register(_ => RefreshUpgradeButtons());
            RefreshUpgradeButtons();
        }

        private void OnDataUpgradeClicked()
        {
            Upgrade(ServerUpgradeTrack.DataThroughput);
        }

        private void OnConnectionUpgradeClicked()
        {
            Upgrade(ServerUpgradeTrack.MaxConnections);
        }

        private void Upgrade(ServerUpgradeTrack track)
        {
            if (mUpgradeSystem == null || string.IsNullOrWhiteSpace(mSelectedNodeId) || mUpgradeConfig == null)
            {
                RefreshUpgradeButtons();
                return;
            }

            NetworkTopologyResult result = mUpgradeSystem.UpgradeServer(
                mSelectedNodeId,
                track,
                mUpgradeConfig,
                out _);
            if (result != NetworkTopologyResult.Success)
            {
                Debug.Log($"服务器 {mSelectedNodeId} 的 {track} 升级失败：{result}。", this);
            }

            RefreshUpgradeButtons();
        }

        private void OnDataProcessingChanged(float value)
        {
            RefreshDataProcessingText();
        }

        private void OnCurrentLoadChanged(float value)
        {
            RefreshDataProcessingText();
        }

        private void RefreshDataProcessingText()
        {
            if (mDataProcessingText != null)
            {
                float capacity = mSelectedCapabilities == null
                    ? 0f
                    : mSelectedCapabilities.DataProcessingPerSecond.Value;
                float currentLoad = mSelectedCapabilities == null
                    ? 0f
                    : mSelectedCapabilities.CurrentDataLoadPerSecond.Value;
                mDataProcessingText.text = capacity > 0f
                    ? $"{currentLoad:0.#} / {capacity:0.#} Mbps"
                    : $"{currentLoad:0.#} / ∞ Mbps";
            }
        }

        private void OnMaxConnectionsChanged(int value)
        {
            if (mMaxConnectionsText != null)
            {
                mMaxConnectionsText.text = value > 0 ? $"{value} Node" : "∞ Node";
            }
        }

        private void OnBalanceChanged(int _)
        {
            RefreshUpgradeButtons();
        }

        private void RefreshUpgradeButtons()
        {
            RefreshUpgradeButton(
                ServerUpgradeTrack.DataThroughput,
                mDataUpgradeButton,
                mDataUpgradeLabel);
            RefreshUpgradeButton(
                ServerUpgradeTrack.MaxConnections,
                mConnectionUpgradeButton,
                mConnectionUpgradeLabel);
        }

        private void RefreshUpgradeButton(ServerUpgradeTrack track, Button button, TMP_Text label)
        {
            bool hasQuote = TryGetNextQuote(track, out ServerUpgradeQuote quote);
            int cost = hasQuote ? quote.TargetData.MoneyCost : 0;
            if (label != null)
            {
                label.text = hasQuote
                    ? $"升级\n{cost}"
                    : mSelectedCapabilities == null ? "升级" : "已满级";
            }

            if (button != null)
            {
                button.interactable = hasQuote && mEconomyModel != null && mEconomyModel.Balance.Value >= cost;
            }
        }

        private bool TryGetNextQuote(ServerUpgradeTrack track, out ServerUpgradeQuote quote)
        {
            quote = default;
            return mTopologySystem != null &&
                   !string.IsNullOrWhiteSpace(mSelectedNodeId) &&
                   mUpgradeConfig != null &&
                   mTopologySystem.TryGetNextServerUpgrade(
                       mSelectedNodeId,
                       track,
                       mUpgradeConfig,
                       out quote) == NetworkTopologyResult.Success;
        }

        private void ClearSelection()
        {
            ClearCapabilityBindings();
            mSelectedNodeId = null;
            mSelectedCapabilities = null;
            if (mServerNameText != null)
            {
                mServerNameText.text = "未选择服务器";
            }

            if (mNodeIdText != null)
            {
                mNodeIdText.text = "-";
            }

            if (mDataProcessingText != null)
            {
                mDataProcessingText.text = "-";
            }

            if (mMaxConnectionsText != null)
            {
                mMaxConnectionsText.text = "-";
            }

            RefreshUpgradeButtons();
        }

        private void ClearCapabilityBindings()
        {
            mDataValueRegistration?.UnRegister();
            mCurrentLoadRegistration?.UnRegister();
            mConnectionValueRegistration?.UnRegister();
            mDataLevelRegistration?.UnRegister();
            mConnectionLevelRegistration?.UnRegister();
            mDataValueRegistration = null;
            mCurrentLoadRegistration = null;
            mConnectionValueRegistration = null;
            mDataLevelRegistration = null;
            mConnectionLevelRegistration = null;
        }

        IArchitecture IBelongToArchitecture.GetArchitecture()
        {
            return GameArchitecture.Interface;
        }
    }
}
