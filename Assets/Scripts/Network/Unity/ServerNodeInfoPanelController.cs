using System.Collections.Generic;
using CUC260905.Economy;
using CUC260905.Interaction;
using QFramework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CUC260905.Network
{
    /// <summary>
    /// 服务器信息面板（表现层）。监听服务器点击事件，展示当前能力，
    /// 并将两个升级按钮转发给 IServerUpgradeSystem。
    /// 面板可见性：按下服务器节点时立即读取数据并显示（easeOutBack 位移 + 淡入，0.5s，
    /// 从停靠位置 + (0,-100) 滑入停靠位置），完全不等输入系统"点击/拖拽"判定；
    /// 下一次左键/右键按下且未点击面板、也未按下服务器节点时反向播放动画后隐藏。
    /// 释放仍会收到 ServerNodeClickedEvent 作为幂等确认，避免拖拽画连线时残留无数据面板。
    /// 数据吞吐量分两端显示：DataShowcur 为当前近 1 秒负载，DataShowmax 为处理上限
    /// （0 上限显示 ∞）；数值变化时按 easeOutCubic 单调跳动到新值。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ServerNodeInfoPanelController : MonoBehaviour, IController
    {
        [Header("显示")]
        [SerializeField] private TMP_Text mServerNameText;
        [SerializeField] private TMP_Text mNodeIdText;
        [SerializeField] private TMP_Text mDataShowcurText;
        [SerializeField] private TMP_Text mDataShowmaxText;
        [SerializeField] private TMP_Text mMaxConnectionsText;
        [SerializeField] private TMP_Text mDataUpgradeLabel;
        [SerializeField] private TMP_Text mConnectionUpgradeLabel;

        [Header("操作")]
        [SerializeField] private Button mDataUpgradeButton;
        [SerializeField] private Button mConnectionUpgradeButton;
        [SerializeField] private ServerUpgradeConfig mUpgradeConfig;

        [Header("动画")]
        [SerializeField, Min(0.01f), Tooltip("面板显示/隐藏动画时长（秒），默认 0.5s。")]
        private float mDuration = 0.5f;
        [SerializeField, Tooltip("显示起始位置相对停靠位置的偏移，默认 (0, -100)。")]
        private Vector2 mHiddenOffset = new Vector2(0f, -100f);
        [SerializeField, Min(0.01f), Tooltip("数据吞吐量数字跳动时长（秒），默认 0.5s。")]
        private float mNumberJumpDuration = 0.5f;

        private IServerUpgradeSystem mUpgradeSystem;
        private INetworkTopologySystem mTopologySystem;
        private INetworkTopologyModel mTopologyModel;
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
        private IUnRegister mPointerFrameRegistration;

        private RectTransform mPanelRectTransform;
        private CanvasGroup mPanelCanvasGroup;
        private GraphicRaycaster mPanelRaycaster;
        private ITargetResolver mTargetResolver;
        private readonly List<RaycastResult> mRaycastResults = new List<RaycastResult>();
        private Vector2 mHomePosition;
        private bool mTargetShown;
        private float mProgress;
        private NumberTween mCurrentLoadTween;
        private NumberTween mMaxLoadTween;

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

            mCurrentLoadTween = new NumberTween(mDataShowcurText, "{0:0.0}Mbps", "∞Mbps");
            mMaxLoadTween = new NumberTween(mDataShowmaxText, "/{0:0.0}Mbps", "/∞Mbps");
        }

        // Start 晚于 InputController.Awake，确保 GameArchitecture 已装配。
        private void Start()
        {
            mUpgradeSystem = this.GetSystem<IServerUpgradeSystem>();
            mTopologySystem = this.GetSystem<INetworkTopologySystem>();
            mTopologyModel = this.GetModel<INetworkTopologyModel>();
            mEconomyModel = this.GetModel<IEconomyModel>();
            mTargetResolver = this.GetUtility<ITargetResolver>();
            mNodeClickedRegistration = this.RegisterEvent<ServerNodeClickedEvent>(OnServerNodeClicked);

            if (mEconomyModel != null)
            {
                mBalanceRegistration = mEconomyModel.Balance.Register(OnBalanceChanged);
            }

            ClearSelection();
            EnsurePanelAnimationSetup();
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
            mPointerFrameRegistration?.UnRegister();
        }

        private void OnServerNodeClicked(ServerNodeClickedEvent clickedEvent)
        {
            if (clickedEvent.Capabilities == null || string.IsNullOrWhiteSpace(clickedEvent.Node.NodeId))
            {
                ClearSelection();
                return;
            }

            // 释放时的点击作为幂等确认：数据在按下瞬间已填充，此处仅重新绑定刷新。
            SelectServer(clickedEvent.Node, clickedEvent.Capabilities);
        }

        /// <summary>选定一台服务器并填充面板数据、显示面板；按下与点击两条路径复用。</summary>
        private void SelectServer(NodeDescriptor node, ServerNodeCapabilities capabilities)
        {
            ClearCapabilityBindings();
            mSelectedNodeId = node.NodeId;
            mSelectedCapabilities = capabilities;
            if (mServerNameText != null)
            {
                mServerNameText.text = string.IsNullOrWhiteSpace(node.DisplayName)
                    ? node.NodeId
                    : node.DisplayName;
            }

            if (mNodeIdText != null)
            {
                mNodeIdText.text = string.IsNullOrWhiteSpace(node.NodeId)
                    ? "-"
                    : string.Format("ID: {0}", node.NodeId);
            }

            // 选中新服务器时数字从 0 起跳；RegisterWithInitValue 触发的首次跳动展示真实数据。
            mCurrentLoadTween?.Snap(0f, false);
            mMaxLoadTween?.Snap(0f, false);
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
            Show();
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
            RefreshDataShowMax();
        }

        private void OnCurrentLoadChanged(float value)
        {
            RefreshDataShowCurrent();
        }

        /// <summary>刷新吞吐"当前值"（近 1 秒负载），数字从当前显示值跳动到最新值。</summary>
        private void RefreshDataShowCurrent()
        {
            if (mCurrentLoadTween == null)
            {
                return;
            }

            float currentLoad = mSelectedCapabilities == null
                ? 0f
                : mSelectedCapabilities.CurrentDataLoadPerSecond.Value;
            mCurrentLoadTween.SetTarget(currentLoad, mNumberJumpDuration, false);
        }

        /// <summary>刷新吞吐"最大值"（处理上限）；0 表示不限流，直接显示 ∞。</summary>
        private void RefreshDataShowMax()
        {
            if (mMaxLoadTween == null)
            {
                return;
            }

            float capacity = mSelectedCapabilities == null
                ? 0f
                : mSelectedCapabilities.DataProcessingPerSecond.Value;
            mMaxLoadTween.SetTarget(capacity, mNumberJumpDuration, capacity <= 0f);
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

            mCurrentLoadTween?.Snap(0f, false);
            mMaxLoadTween?.Snap(0f, false);
            if (mDataShowcurText != null)
            {
                mDataShowcurText.text = "-";
            }

            if (mDataShowmaxText != null)
            {
                mDataShowmaxText.text = "-";
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

        /// <summary>显示面板并播放入场动画（easeOutBack 滑入 + 淡入）。</summary>
        private void Show()
        {
            gameObject.SetActive(true);
            if (mPanelCanvasGroup != null)
            {
                mPanelCanvasGroup.blocksRaycasts = true;
            }

            mTargetShown = true;
        }

        /// <summary>请求收起面板：反向播放入场动画，播完后隐藏对象。</summary>
        private void Hide()
        {
            if (mPanelCanvasGroup != null)
            {
                mPanelCanvasGroup.blocksRaycasts = false;
            }

            mTargetShown = false;
        }

        /// <summary>
        /// 每帧指针事件：完全按"按下"驱动面板——显示/数据/收起都不依赖输入系统
        /// 判断"点击还是拖拽"的时机（那要到释放或越阈值移动才确定，正是延迟感的来源）。
        /// </summary>
        private void OnPointerFrame(PointerFrameEvent frame)
        {
            if (frame.Signals == null)
            {
                return;
            }

            for (int index = 0; index < frame.Signals.Count; index++)
            {
                PointerSignal signal = frame.Signals[index];
                if (signal.Button != PointerButton.Left && signal.Button != PointerButton.Right)
                {
                    continue;
                }

                if (signal.Phase == PointerPhase.Down)
                {
                    OnPress(signal);
                }
            }
        }

        /// <summary>按下瞬间的显示/收起判定：立即读取并展示服务器数据，或收起面板。</summary>
        private void OnPress(PointerSignal signal)
        {
            // 点击面板（含升级按钮）不收起。
            if (IsPointerOverPanel(signal.ScreenPosition))
            {
                return;
            }

            // 按下服务器节点：立即读取数据并显示；数据暂不可读时保持面板现状，不收起。
            if (TryResolveServerController(signal.ScreenPosition, out ServerNodeController controller))
            {
                TrySelectServer(controller.NodeId);
                return;
            }

            // 非面板、非服务器节点：立即收起。
            Hide();
        }

        /// <summary>按节点 ID 读取模型数据并选中；数据无效时什么都不做（保持当前面板）。</summary>
        private void TrySelectServer(string nodeId)
        {
            if (mTopologyModel == null ||
                !mTopologyModel.TryGetNode(nodeId, out NodeDescriptor node))
            {
                return;
            }

            mTopologyModel.TryGetServerCapabilities(nodeId, out ServerNodeCapabilities capabilities);
            if (capabilities == null)
            {
                return;
            }

            SelectServer(node, capabilities);
        }

        /// <summary>复用输入系统的目标解析：按下位置是否命中已登记的服务器节点；命中时返回其控制器。</summary>
        private bool TryResolveServerController(Vector2 screenPosition, out ServerNodeController controller)
        {
            controller = null;
            if (mTargetResolver == null)
            {
                return false;
            }

            PointerSignal signal = new PointerSignal(
                0,
                PointerButton.Left,
                PointerPhase.Down,
                screenPosition,
                Vector2.zero,
                Time.unscaledTime);
            if (!mTargetResolver.TryResolve(in signal, out InteractionHit hit) || !hit.HasTarget)
            {
                return false;
            }

            InteractionTarget target = hit.Target as InteractionTarget;
            if (target == null)
            {
                return false;
            }

            controller = target.GetComponent<ServerNodeController>();
            return controller != null && !string.IsNullOrWhiteSpace(controller.NodeId);
        }

        /// <summary>在指定屏幕坐标处做 UI 射线检测，判断是否命中面板或其子元素。</summary>
        private bool IsPointerOverPanel(Vector2 screenPosition)
        {
            if (EventSystem.current == null || mPanelRaycaster == null)
            {
                return false;
            }

            PointerEventData eventData = new PointerEventData(EventSystem.current);
            eventData.position = screenPosition;
            mRaycastResults.Clear();
            mPanelRaycaster.Raycast(eventData, mRaycastResults);
            for (int index = 0; index < mRaycastResults.Count; index++)
            {
                if (mRaycastResults[index].gameObject == gameObject ||
                    mRaycastResults[index].gameObject.transform.IsChildOf(transform))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>准备面板动画所需引用，并把面板设为初始隐藏态。</summary>
        private void EnsurePanelAnimationSetup()
        {
            mPanelRectTransform = GetComponent<RectTransform>();
            mPanelCanvasGroup = GetComponent<CanvasGroup>();
            if (mPanelCanvasGroup == null)
            {
                mPanelCanvasGroup = gameObject.AddComponent<CanvasGroup>();
            }

            // 场景 Canvas 上挂有 GraphicRaycaster，用于判断点击是否落在面板上。
            mPanelRaycaster = GetComponentInParent<GraphicRaycaster>();
            mHomePosition = mPanelRectTransform != null
                ? mPanelRectTransform.anchoredPosition
                : Vector2.zero;

            mPointerFrameRegistration = this.RegisterEvent<PointerFrameEvent>(OnPointerFrame);

            // 初始隐藏：只有收到有效服务器数据才显示。
            mTargetShown = false;
            mProgress = 0f;
            ApplyState(0f);
            mPanelCanvasGroup.blocksRaycasts = false;
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                return;
            }

            float deltaTime = Time.unscaledDeltaTime;
            bool numbersAnimating = TickDataTweens(deltaTime);

            float target = mTargetShown ? 1f : 0f;
            if (Mathf.Approximately(mProgress, target) && !numbersAnimating)
            {
                return;
            }

            // 使用非缩放时间推进：面板收起可能发生在暂停（timeScale=0）期间，
            // 缩放时间会把动画冻结在半透明态。
            mProgress = Mathf.MoveTowards(mProgress, target, deltaTime / mDuration);
            ApplyState(mProgress);

            if (mProgress <= 0f)
            {
                gameObject.SetActive(false);
            }
        }

        /// <summary>按进度应用动画：easeOutBack 作用于透明度与停靠位置位移。</summary>
        private void ApplyState(float progress)
        {
            float eased = EaseOutBack(progress);
            if (mPanelCanvasGroup != null)
            {
                mPanelCanvasGroup.alpha = Mathf.Clamp01(eased);
            }

            if (mPanelRectTransform != null)
            {
                mPanelRectTransform.anchoredPosition = Vector2.Lerp(
                    mHomePosition + mHiddenOffset,
                    mHomePosition,
                    eased);
            }
        }

        /// <summary>三次缓出回弹曲线（easeOutBack）：接近终点时略过冲后回正。</summary>
        private static float EaseOutBack(float value)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = value - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        /// <summary>推进两个吞吐数字的跳动动画；返回是否仍有动画在播放。</summary>
        private bool TickDataTweens(float deltaTime)
        {
            bool animating = false;
            if (mCurrentLoadTween != null)
            {
                animating |= mCurrentLoadTween.Tick(deltaTime);
            }

            if (mMaxLoadTween != null)
            {
                animating |= mMaxLoadTween.Tick(deltaTime);
            }

            return animating;
        }

        /// <summary>单个 TMP 文本的数字跳动动画：从起始显示值按 easeOutCubic 单调缓动到目标值。</summary>
        private sealed class NumberTween
        {
            private readonly TMP_Text mText;
            private readonly string mFiniteFormat;
            private readonly string mUnlimitedText;
            private float mDisplayValue;
            private float mStartValue;
            private float mTargetValue;
            private float mElapsed;
            private float mDuration = 1f;
            private bool mUnlimited;

            public NumberTween(TMP_Text text, string finiteFormat, string unlimitedText)
            {
                mText = text;
                mFiniteFormat = finiteFormat;
                mUnlimitedText = unlimitedText;
            }

            /// <summary>立即跳到目标值（无动画）：用于清空或切换服务器时归零。</summary>
            public void Snap(float value, bool isUnlimited)
            {
                mDisplayValue = value;
                mStartValue = value;
                mTargetValue = value;
                mElapsed = 0f;
                mUnlimited = isUnlimited;
                Apply();
            }

            /// <summary>设置目标值并从当前显示值开始跳动；无限上限直接显示 ∞。</summary>
            public void SetTarget(float target, float duration, bool isUnlimited)
            {
                mStartValue = mDisplayValue;
                mTargetValue = target;
                mElapsed = 0f;
                mDuration = Mathf.Max(duration, 0.01f);
                mUnlimited = isUnlimited;
                if (isUnlimited)
                {
                    Apply();
                }
            }

            /// <summary>每帧推进动画并刷新文本；返回是否仍在播放。</summary>
            public bool Tick(float deltaTime)
            {
                if (mUnlimited)
                {
                    return false;
                }

                mElapsed += deltaTime;
                float normalized = Mathf.Clamp01(mElapsed / mDuration);
                mDisplayValue = Mathf.Lerp(mStartValue, mTargetValue, EaseOutCubic(normalized));
                Apply();
                return normalized < 1f;
            }

            private void Apply()
            {
                if (mText == null)
                {
                    return;
                }

                mText.text = mUnlimited
                    ? mUnlimitedText
                    : string.Format(mFiniteFormat, mDisplayValue);
            }

            /// <summary>三次缓出曲线：加速快、接近终点减速，单调逼近不越过目标值。</summary>
            private static float EaseOutCubic(float value)
            {
                float u = 1f - value;
                return 1f - u * u * u;
            }
        }

        IArchitecture IBelongToArchitecture.GetArchitecture()
        {
            return GameArchitecture.Interface;
        }
    }
}
