using System;

namespace CUC260905.Network
{
    /// <summary>节点在逻辑拓扑中的角色；角色不决定连通规则。</summary>
    public enum NetworkNodeRole
    {
        User = 0,
        Server = 1
    }

    /// <summary>节点注册资料。NodeId 在当前架构实例内必须唯一。</summary>
    public readonly struct NodeDescriptor : IEquatable<NodeDescriptor>
    {
        public readonly string NodeId;
        public readonly NetworkNodeRole Role;
        public readonly string DisplayName;

        public NodeDescriptor(string nodeId, NetworkNodeRole role, string displayName)
        {
            NodeId = nodeId;
            Role = role;
            DisplayName = displayName ?? string.Empty;
        }

        public bool HasValidNodeId
        {
            get { return !string.IsNullOrWhiteSpace(NodeId); }
        }

        public bool Equals(NodeDescriptor other)
        {
            return string.Equals(NodeId, other.NodeId, StringComparison.Ordinal) &&
                   Role == other.Role &&
                   string.Equals(DisplayName, other.DisplayName, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is NodeDescriptor other && Equals(other);
        }

        public override int GetHashCode()
        {
            int nodeIdHash = NodeId == null ? 0 : StringComparer.Ordinal.GetHashCode(NodeId);
            int displayNameHash = DisplayName == null ? 0 : StringComparer.Ordinal.GetHashCode(DisplayName);
            return HashCode.Combine(nodeIdHash, (int)Role, displayNameHash);
        }
    }

    /// <summary>拓扑写操作结果，供外部规则系统决定后续行为。</summary>
    public enum NetworkTopologyResult
    {
        Success = 0,
        NoChange = 1,
        InvalidNodeId = 2,
        NodeNotRegistered = 3,
        DuplicateNodeId = 4,
        SameNode = 5,
        InvalidCapabilities = 6,
        NotServerNode = 7,
        ServerCapabilitiesMissing = 8,
        UpgradeConfigMissing = 9,
        UpgradeLevelUnavailable = 10,
        InvalidUpgradeData = 11,
        InsufficientBalance = 12
    }
}
