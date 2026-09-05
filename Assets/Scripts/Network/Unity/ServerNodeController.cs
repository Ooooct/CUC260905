using CUC260905.Interaction;
using QFramework;
using UnityEngine;

namespace CUC260905.Network
{
    /// <summary>
    /// 服务器节点控制器（表现层）。
    /// 职责单一：节点被点击时，从拓扑模型读取该节点数据，并发布 ServerNodeClickedEvent，
    /// 供 UI 显示、升级调整以及其他界面刷新使用。
    /// 节点登记仍由同物体上的 NetworkNodeRegistrar 负责，本控制器不持有业务数据。
    ///
    /// 场景装配：与 InteractionTarget、CapabilitySinkAdapter、NetworkNodeRegistrar 同物体，
    /// Collider 可位于子节点（Resolver 按父级查找 InteractionTarget）。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkNodeRegistrar))]
    [RequireComponent(typeof(CapabilitySinkAdapter))]
    public sealed class ServerNodeController : MonoBehaviour, IController, ICanSendEvent, IClickable
    {
        private NetworkNodeRegistrar mRegistrar;
        private INetworkTopologyModel mModel;

        /// <summary>节点 ID，直接取自同物体上的 NetworkNodeRegistrar，避免重复配置。</summary>
        public string NodeId
        {
            get { return mRegistrar != null ? mRegistrar.NodeId : string.Empty; }
        }

        private void Awake()
        {
            mRegistrar = GetComponent<NetworkNodeRegistrar>();
        }

        // Start 晚于 InputController.Awake，确保 GameArchitecture 已装配。
        private void Start()
        {
            mModel = this.GetModel<INetworkTopologyModel>();
        }

        /// <summary>点击入口，由 CapabilitySinkAdapter 经 IClickable 转发。</summary>
        public InteractionResult OnClick(in ClickIntent intent)
        {
            if (mModel == null || mRegistrar == null)
            {
                return new InteractionResult(InteractionResultStatus.SinkUnavailable);
            }

            string nodeId = mRegistrar.NodeId;
            if (!mModel.TryGetNode(nodeId, out NodeDescriptor node))
            {
                Debug.LogWarning($"[{name}] 节点 {nodeId} 未登记到拓扑，忽略点击。", this);
                return new InteractionResult(InteractionResultStatus.Rejected);
            }

            // 能力档案可能未配置（User 节点或未携带注册），此时保持 null，由监听方容错。
            mModel.TryGetServerCapabilities(nodeId, out ServerNodeCapabilities capabilities);

            // 携带该节点在模型中的数据发布事件：UI 显示、升级调整、界面刷新统一听此事件。
            this.SendEvent(new ServerNodeClickedEvent(node, capabilities));

            return new InteractionResult(InteractionResultStatus.Handled);
        }

        IArchitecture IBelongToArchitecture.GetArchitecture()
        {
            return GameArchitecture.Interface;
        }
    }
}
