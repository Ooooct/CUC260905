using CUC260905.Interaction;
using CUC260905.Network;
using CUC260905.Placement;
using QFramework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CUC260905.Game
{
    /// <summary>
    /// 场景内线性教程的表现控制器。
    /// 教程仅消费已经发生的玩法事件，不改变放置、连线、传输或升级规则。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class GuidePanelController : MonoBehaviour, IController
    {
        private const string DeployServerText =
            "先点左下角「新建服务器」，再把服务器放到地图空白处。";

        private const string ConnectUsersText =
            "彩色的是用户节点，它们之间不能直接连线，数据都得经服务器中转。从服务器节点拖到用户节点即可连线；右键点击已建立的连线即可删除；没有可用路径时，传输会失败。";

        private const string ConnectUsersFailureText =
            "没有可用路径，检查一下服务器和用户节点的连线。";

        private const string InspectServerText =
            "点一下服务器，能看到负载、吞吐量和最大连接数。传输成功会赚到 Gold，用它升级服务器。";

        private const string FinishText =
            "按 Space 暂停，按 Esc 退出。祝你好运！";

        [Header("显示")]
        [SerializeField] private TextMeshProUGUI mTutorialText;
        [SerializeField, Min(0f), Tooltip("最后一条教程保持完整显示的时长（秒）。")]
        private float mFinalDisplayDuration = 6f;
        [SerializeField, Min(0.01f), Tooltip("最后一条教程渐隐时长（秒）。")]
        private float mFadeDuration = 0.5f;

        private CanvasGroup mCanvasGroup;
        private IUnRegister mPlacementRegistration;
        private IUnRegister mTransmittedRegistration;
        private IUnRegister mUnreachableRegistration;
        private IUnRegister mServerClickedRegistration;
        private GuideStep mCurrentStep;
        private float mFinalElapsed;

        private enum GuideStep
        {
            DeployServer = 0,
            ConnectUsers = 1,
            InspectServer = 2,
            Finish = 3,
            Completed = 4
        }

        private void Awake()
        {
            mCanvasGroup = GetComponent<CanvasGroup>();
            mCanvasGroup.alpha = 1f;
            mCanvasGroup.interactable = false;
            mCanvasGroup.blocksRaycasts = false;

            if (mTutorialText == null)
            {
                mTutorialText = GetComponentInChildren<TextMeshProUGUI>();
            }

            if (mTutorialText == null)
            {
                Debug.LogError("GuidePanelController 未找到教程文本。", this);
                enabled = false;
                return;
            }

            ShowStep(GuideStep.DeployServer);
        }

        // Start 晚于 InputController.Awake，确保 GameArchitecture 已完成装配。
        private void Start()
        {
            if (!enabled)
            {
                return;
            }

            mPlacementRegistration = this.RegisterEvent<PlacementPlacedEvent>(OnPlacementPlaced);
            mTransmittedRegistration = this.RegisterEvent<PacketTransmittedEvent>(OnPacketTransmitted);
            mUnreachableRegistration = this.RegisterEvent<PacketUnreachableEvent>(OnPacketUnreachable);
            mServerClickedRegistration = this.RegisterEvent<ServerNodeClickedEvent>(OnServerNodeClicked);
        }

        private void Update()
        {
            if (mCurrentStep != GuideStep.Finish)
            {
                return;
            }

            mFinalElapsed += Time.unscaledDeltaTime;
            if (mFinalElapsed <= mFinalDisplayDuration)
            {
                return;
            }

            float fadeProgress = Mathf.Clamp01(
                (mFinalElapsed - mFinalDisplayDuration) / mFadeDuration);
            mCanvasGroup.alpha = 1f - fadeProgress;
            if (fadeProgress >= 1f)
            {
                mCurrentStep = GuideStep.Completed;
            }
        }

        private void OnDestroy()
        {
            mPlacementRegistration?.UnRegister();
            mTransmittedRegistration?.UnRegister();
            mUnreachableRegistration?.UnRegister();
            mServerClickedRegistration?.UnRegister();
        }

        /// <summary>场景加载后将教程控制器落到既有 GuidePanel，避免重复维护场景接线。</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void InstallSceneLoadHandler()
        {
            SceneManager.sceneLoaded -= EnsureGuidePanelController;
            SceneManager.sceneLoaded += EnsureGuidePanelController;
        }

        private static void EnsureGuidePanelController(Scene scene, LoadSceneMode _)
        {
            if (!string.Equals(scene.name, "SampleScene", System.StringComparison.Ordinal))
            {
                return;
            }

            GameObject guidePanel = GameObject.Find("GuidePanel");
            if (guidePanel == null || guidePanel.GetComponent<GuidePanelController>() != null)
            {
                return;
            }

            guidePanel.AddComponent<GuidePanelController>();
        }

        private void OnPlacementPlaced(PlacementPlacedEvent placedEvent)
        {
            if (mCurrentStep != GuideStep.DeployServer || placedEvent.Instance == null)
            {
                return;
            }

            ServerNodeController server = placedEvent.Instance.GetComponent<ServerNodeController>();
            if (server != null)
            {
                ShowStep(GuideStep.ConnectUsers);
            }
        }

        private void OnPacketTransmitted(PacketTransmittedEvent transmittedEvent)
        {
            if (mCurrentStep == GuideStep.ConnectUsers)
            {
                ShowStep(GuideStep.InspectServer);
            }
        }

        private void OnPacketUnreachable(PacketUnreachableEvent unreachableEvent)
        {
            if (mCurrentStep == GuideStep.ConnectUsers && mTutorialText != null)
            {
                mTutorialText.text = ConnectUsersText + "\n\n" + ConnectUsersFailureText;
            }
        }

        private void OnServerNodeClicked(ServerNodeClickedEvent clickedEvent)
        {
            if (mCurrentStep == GuideStep.InspectServer)
            {
                ShowStep(GuideStep.Finish);
            }
        }

        private void ShowStep(GuideStep step)
        {
            mCurrentStep = step;
            if (mTutorialText == null)
            {
                return;
            }

            switch (step)
            {
                case GuideStep.DeployServer:
                    mTutorialText.text = DeployServerText;
                    break;
                case GuideStep.ConnectUsers:
                    mTutorialText.text = ConnectUsersText;
                    break;
                case GuideStep.InspectServer:
                    mTutorialText.text = InspectServerText;
                    break;
                case GuideStep.Finish:
                    mFinalElapsed = 0f;
                    mCanvasGroup.alpha = 1f;
                    mTutorialText.text = FinishText;
                    break;
            }
        }

        IArchitecture IBelongToArchitecture.GetArchitecture()
        {
            return GameArchitecture.Interface;
        }
    }
}
