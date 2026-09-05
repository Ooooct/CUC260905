using CUC260905.Game;
using CUC260905.Interaction;
using CUC260905.Network;
using QFramework;
using UnityEngine;

namespace CUC260905.Feedback
{
    /// <summary>
    /// 将既有领域事件映射为短音效的场景表现层。
    /// 音频只作二维全局反馈，不参与网络、暂停或升级规则。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(AudioSource))]
    public sealed class AudioFeedbackController : MonoBehaviour, IController
    {
        [Header("音效资源")]
        [SerializeField] private AudioClip mNodeAppearedClip;
        [SerializeField] private AudioClip mNodeConnectedClip;
        [SerializeField] private AudioClip mServerUpgradedClip;
        [SerializeField] private AudioClip mTransmissionFailedClip;
        [SerializeField] private AudioClip mPausedClip;

        [Header("播放设置")]
        [SerializeField, Range(0.0f, 1.0f)]
        [Tooltip("所有短音效的统一音量。")]
        private float mVolume = 0.7f;

        private AudioSource mAudioSource;
        private IUnRegister mNodeRegisteredRegistration;
        private IUnRegister mConnectionRegistration;
        private IUnRegister mUpgradeRegistration;
        private IUnRegister mTransmissionFailedRegistration;
        private IUnRegister mPausedRegistration;
        private bool mAcceptNodeRegistered;

        private void Awake()
        {
            mAudioSource = GetComponent<AudioSource>();
            mAudioSource.playOnAwake = false;
            mAudioSource.loop = false;
            mAudioSource.spatialBlend = 0.0f;
        }

        private void Start()
        {
            mNodeRegisteredRegistration = this.RegisterEvent<NodeRegisteredEvent>(OnNodeRegistered);
            mConnectionRegistration = this.RegisterEvent<NodeConnectivityChangedEvent>(OnConnectivityChanged);
            mUpgradeRegistration = this.RegisterEvent<ServerNodeUpgradedEvent>(OnServerUpgraded);
            mTransmissionFailedRegistration = this.RegisterEvent<PacketUnreachableEvent>(OnPacketUnreachable);
            mPausedRegistration = this.RegisterEvent<GamePausedEvent>(OnGamePaused);
        }

        private void LateUpdate()
        {
            // 所有场景对象的 Start 均结束后，才把后续登记视为“新出现节点”。
            mAcceptNodeRegistered = true;
        }

        private void OnDestroy()
        {
            mNodeRegisteredRegistration?.UnRegister();
            mConnectionRegistration?.UnRegister();
            mUpgradeRegistration?.UnRegister();
            mTransmissionFailedRegistration?.UnRegister();
            mPausedRegistration?.UnRegister();
        }

        private void OnNodeRegistered(NodeRegisteredEvent _)
        {
            if (mAcceptNodeRegistered)
            {
                Play(mNodeAppearedClip);
            }
        }

        private void OnConnectivityChanged(NodeConnectivityChangedEvent changedEvent)
        {
            if (changedEvent.IsConnected)
            {
                Play(mNodeConnectedClip);
            }
        }

        private void OnServerUpgraded(ServerNodeUpgradedEvent _)
        {
            Play(mServerUpgradedClip);
        }

        private void OnPacketUnreachable(PacketUnreachableEvent _)
        {
            Play(mTransmissionFailedClip);
        }

        private void OnGamePaused(GamePausedEvent _)
        {
            Play(mPausedClip);
        }

        private void Play(AudioClip clip)
        {
            if (clip != null && mAudioSource != null)
            {
                mAudioSource.PlayOneShot(clip, Mathf.Clamp01(mVolume));
            }
        }

        IArchitecture IBelongToArchitecture.GetArchitecture()
        {
            return GameArchitecture.Interface;
        }
    }
}
