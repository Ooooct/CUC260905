using CUC260905.Interaction;
using CUC260905.Interaction.Example;
using QFramework;
using UnityEngine;

namespace CUC260905.Network
{
    /// <summary>
    /// 用户节点控制器（表现层）。
    /// 与 ServerNodeController 不同：用户节点被点击时不发布任何事件——
    /// 不发布 ServerNodeClickedEvent，也不引入新的点击事件类型，仅消费点击并返回 Handled。
    /// 本控制器刻意不实现 ICanSendEvent，从类型层面保证"点击不发布"。
    ///
    /// 节点登记由同物体上的 NetworkNodeRegistrar 负责（Role = User，mNodeId 留空时自动生成
    /// "user-" 前缀 ID），注册键与服务器节点（"server-" 前缀）天然区分，
    /// 供后续业务逻辑按角色分派（例如：连线、选中、用户设备管理）。
    /// 同时负责该用户节点的数据包生成调度：部署后等待随机冷却，随后以随机间隔
    /// 向随机用户节点发送随机大小的数据包（经服务器中继）。真正的路由与吞吐记账均由 IPacketTrafficSystem 完成。
    ///
    /// 场景装配：与 InteractionTarget、CapabilitySinkAdapter、NetworkNodeRegistrar 同物体，
    /// Collider 可位于子节点（Resolver 按父级查找 InteractionTarget）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkNodeRegistrar))]
    [RequireComponent(typeof(CapabilitySinkAdapter))]
    public sealed class UserNodeController : MonoBehaviour, IController, IClickable
    {
        [Header("数据包配置")]
        [SerializeField, Min(0f)]
        [Tooltip("节点部署后第一次发送前的随机冷却下限（秒）。")]
        private float mDeploymentCooldownMin = 0.5f;
        [SerializeField, Min(0f)]
        [Tooltip("节点部署后第一次发送前的随机冷却上限（秒）。")]
        private float mDeploymentCooldownMax = 1.5f;
        [SerializeField, Min(0.01f)]
        [Tooltip("两次数据包发送之间的随机间隔下限（秒）。")]
        private float mSendIntervalMin = 0.8f;
        [SerializeField, Min(0.01f)]
        [Tooltip("两次数据包发送之间的随机间隔上限（秒）。")]
        private float mSendIntervalMax = 1.8f;
        [SerializeField, Min(0.01f)]
        [Tooltip("单个数据包的随机大小下限（Mb）。")]
        private float mPacketSizeMin = 10f;
        [SerializeField, Min(0.01f)]
        [Tooltip("单个数据包的随机大小上限（Mb）。")]
        private float mPacketSizeMax = 30f;
        [SerializeField, Min(0f)]
        [Tooltip("路由对服务器预测利用率的偏好权重；越高越主动绕开拥堵服务器。")]
        private float mLoadCostWeight = 4f;
        [SerializeField]
        [Tooltip("无法找到可行路径时写入的提示终端标识。")]
        private string mMessageTargetId = "MainTerminal";

        [Header("节点配色")]
        [SerializeField]
        [Tooltip("用户节点底色候选池；节点生成时随机取其一初始化。")]
        private Color[] mBaseColors =
        {
            new Color(0.106f, 0.624f, 0.839f, 1f), // #1B9FD6
            new Color(0.086f, 0.733f, 0.475f, 1f), // #16BB79
            new Color(0.937f, 0.157f, 0.278f, 1f), // #EF2847
            new Color(1f, 0.8f, 0f, 1f)            // #FFCC00
        };
        [SerializeField, Min(0f)]
        [Tooltip("边缘轮廓的 HSL 明度固定加深量（保持色相不变），默认 0.15。")]
        private float mOutlineLightnessStep = UserNodePalette.DefaultOutlineLightnessStep;

        private static readonly System.Random sPaletteRandom = new System.Random();

        private NetworkNodeRegistrar mRegistrar;
        private INetworkTopologyModel mTopologyModel;
        private IPacketTrafficSystem mTrafficSystem;
        private System.Random mRandom;
        private bool mScheduleStarted;
        private double mNextSendAt;

        // Start 晚于 InputController.Awake；节点登记的先后顺序不固定，Update 会等待登记成功。
        private void Start()
        {
            mRegistrar = GetComponent<NetworkNodeRegistrar>();
            mTopologyModel = this.GetModel<INetworkTopologyModel>();
            mTrafficSystem = this.GetSystem<IPacketTrafficSystem>();
            mRandom = new System.Random(GetInstanceID());
            ApplyPalette();
        }

        /// <summary>
        /// 生成时配色初始化：从底色候选池随机取一套作底色，
        /// 边缘轮廓在 HSL 空间按固定量加深（保持色相不变，默认 L − 0.15，可在 Inspector 调整）。
        /// 候选池未配置时回退到 UserNodePalette 的默认四套底色。
        /// 随机源用共享静态 Random：运行时克隆的 GetInstanceID 是连续整数，
        /// 直接作种子在 Mono 下会产出相同首个随机值，导致所有节点同色。
        /// </summary>
        private void ApplyPalette()
        {
            Color[] palette = (mBaseColors != null && mBaseColors.Length > 0)
                ? mBaseColors
                : UserNodePalette.BaseColors;
            float step = mOutlineLightnessStep > 0f
                ? mOutlineLightnessStep
                : UserNodePalette.DefaultOutlineLightnessStep;

            Color baseColor = palette[sPaletteRandom.Next(palette.Length)];
            Color outlineColor = UserNodePalette.DarkenOutline(baseColor, step);

            SpriteOutline2D outline = GetComponentInChildren<SpriteOutline2D>();
            if (outline != null)
            {
                outline.SetOuterOutline(outlineColor, outline.OuterWidth);
            }

            // 最后设置底色，避免 SpriteOutline2D 的 PropertyBlock 写入覆盖底色。
            SpriteRenderer renderer = GetComponentInChildren<SpriteRenderer>();
            if (renderer != null)
            {
                renderer.color = baseColor;
            }
        }

        private void Update()
        {
            if (mTrafficSystem == null || mTopologyModel == null || mRegistrar == null)
            {
                return;
            }

            double now = Time.timeAsDouble;
            mTrafficSystem.Tick(now);
            if (!mTopologyModel.IsRegistered(mRegistrar.NodeId))
            {
                return;
            }

            if (!mScheduleStarted)
            {
                mScheduleStarted = true;
                mNextSendAt = now + SampleRange(mDeploymentCooldownMin, mDeploymentCooldownMax);
                return;
            }

            if (now < mNextSendAt)
            {
                return;
            }

            float packetSize = SampleRange(mPacketSizeMin, mPacketSizeMax);
            mTrafficSystem.SendRandomPacket(
                mRegistrar.NodeId,
                packetSize,
                Mathf.Max(0f, mLoadCostWeight),
                mMessageTargetId,
                now,
                mRandom);
            mNextSendAt = now + Mathf.Max(0.01f, SampleRange(mSendIntervalMin, mSendIntervalMax));
        }

        private float SampleRange(float first, float second)
        {
            float min = Mathf.Min(first, second);
            float max = Mathf.Max(first, second);
            return min + (float)mRandom.NextDouble() * (max - min);
        }

        /// <summary>点击入口，由 CapabilitySinkAdapter 经 IClickable 转发。</summary>
        public InteractionResult OnClick(in ClickIntent intent)
        {
            // 用户节点点击不发布任何事件；返回 Handled 以消费本次点击，避免落到其他交互。
            return new InteractionResult(InteractionResultStatus.Handled);
        }

        IArchitecture IBelongToArchitecture.GetArchitecture()
        {
            return GameArchitecture.Interface;
        }
    }
}
