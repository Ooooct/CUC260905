using QFramework;

namespace CUC260905.Network
{
    /// <summary>节点已登记到当前拓扑。</summary>
    public readonly struct NodeRegisteredEvent : IEvent
    {
        public readonly NodeDescriptor Node;

        public NodeRegisteredEvent(NodeDescriptor node)
        {
            Node = node;
        }
    }

    /// <summary>节点已从当前拓扑移除。</summary>
    public readonly struct NodeUnregisteredEvent : IEvent
    {
        public readonly string NodeId;

        public NodeUnregisteredEvent(string nodeId)
        {
            NodeId = nodeId;
        }
    }

    /// <summary>两个节点间连通状态已更新；关系无方向。</summary>
    public readonly struct NodeConnectivityChangedEvent : IEvent
    {
        public readonly string FirstNodeId;
        public readonly string SecondNodeId;
        public readonly bool IsConnected;

        public NodeConnectivityChangedEvent(string firstNodeId, string secondNodeId, bool isConnected)
        {
            FirstNodeId = firstNodeId;
            SecondNodeId = secondNodeId;
            IsConnected = isConnected;
        }
    }

    /// <summary>
    /// 服务器节点被点击后由 ServerNodeController 发布。
    /// 携带该节点在拓扑模型中的数据（注册资料 + 服务器能力档案），
    /// 供 UI 显示、升级调整以及其他界面刷新使用。
    /// Capabilities 为模型内同一实例（属性为 BindableProperty），
    /// 监听方可直接订阅其值变化以同步升级后的显示。
    /// </summary>
    public readonly struct ServerNodeClickedEvent : IEvent
    {
        public readonly NodeDescriptor Node;
        public readonly ServerNodeCapabilities Capabilities;

        public ServerNodeClickedEvent(NodeDescriptor node, ServerNodeCapabilities capabilities)
        {
            Node = node;
            Capabilities = capabilities;
        }
    }

    /// <summary>
    /// 服务器指定能力轨道已升级。能力档案已写入模型后才发布，
    /// 资金、任务和 UI 等外部系统可据此响应。
    /// </summary>
    public readonly struct ServerNodeUpgradedEvent : IEvent
    {
        public readonly string NodeId;
        public readonly ServerUpgradeTrack Track;
        public readonly int PreviousLevel;
        public readonly int CurrentLevel;
        public readonly UpgradeLevelData AppliedData;
        public readonly ServerNodeCapabilities Capabilities;

        public ServerNodeUpgradedEvent(
            string nodeId,
            ServerUpgradeTrack track,
            int previousLevel,
            int currentLevel,
            UpgradeLevelData appliedData,
            ServerNodeCapabilities capabilities)
        {
            NodeId = nodeId;
            Track = track;
            PreviousLevel = previousLevel;
            CurrentLevel = currentLevel;
            AppliedData = appliedData;
            Capabilities = capabilities;
        }
    }
}
