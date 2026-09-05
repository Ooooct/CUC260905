using System;
using System.Collections.Generic;
using QFramework;

namespace CUC260905.Network
{
    /// <summary>逻辑拓扑的只读状态。所有写操作经 INetworkTopologySystem 进入。</summary>
    public interface INetworkTopologyModel : IModel
    {
        bool IsRegistered(string nodeId);

        bool TryGetNode(string nodeId, out NodeDescriptor node);

        /// <summary>当前全部节点的只读快照，供流量生成等只读规则筛选目标。</summary>
        IReadOnlyCollection<NodeDescriptor> Nodes { get; }

        IReadOnlyCollection<string> GetConnectedNodeIds(string nodeId);

        bool TryGetServerCapabilities(string nodeId, out ServerNodeCapabilities capabilities);

        bool TryGetEdge(string firstNodeId, string secondNodeId, out NetworkEdge edge);

        /// <summary>当前全部无向边的只读快照，供交叉检查、路径查找等只读规则使用。</summary>
        IReadOnlyCollection<NetworkEdge> Edges { get; }

        /// <summary>
        /// 全局统一的部署接入时间（秒）：用户节点注册部署后须等待该时长才能发送与接收数据包。
        /// 0 表示关闭接入门控。仅用户节点受此限制，服务器节点始终可用。
        /// </summary>
        float DeploymentAccessTime { get; }

        /// <summary>
        /// 用户节点是否已完成部署接入（now >= 部署时刻 + 部署接入时间）。
        /// 服务器节点恒为 true；未注册节点为 false。
        /// </summary>
        bool IsDeploymentAccessComplete(string nodeId, double now);

        /// <summary>
        /// 查询节点剩余的部署接入时间（秒，不小于 0）；未注册节点返回 false。
        /// 服务器节点视为无接入门控（返回 true，剩余 0）。
        /// </summary>
        bool TryGetDeploymentAccessRemaining(string nodeId, double now, out float remainingSeconds);
    }

    /// <summary>
    /// 保存节点、无向连线、服务器能力档案、边属性与用户节点部署接入状态；
    /// 不保存 Unity 对象或业务连通规则。
    /// </summary>
    public sealed class NetworkTopologyModel : AbstractModel, INetworkTopologyModel
    {
        /// <summary>用户节点默认部署接入时间（秒）；0 表示关闭接入门控。生产装配经 GameArchitecture 显式传入。</summary>
        public const float DefaultDeploymentAccessTime = 10f;

        private readonly Dictionary<string, NodeDescriptor> mNodes =
            new Dictionary<string, NodeDescriptor>(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<string>> mConnectedNodeIds =
            new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, ServerNodeCapabilities> mServerCapabilities =
            new Dictionary<string, ServerNodeCapabilities>(StringComparer.Ordinal);
        private readonly Dictionary<NetworkEdgeKey, NetworkEdge> mEdges =
            new Dictionary<NetworkEdgeKey, NetworkEdge>();
        private readonly Dictionary<string, double> mDeployedAt =
            new Dictionary<string, double>(StringComparer.Ordinal);

        /// <summary>以全局统一的部署接入时间构造模型；0 表示无接入门控（既有测试保持立即可用）。</summary>
        public NetworkTopologyModel(float deploymentAccessTime = 0f)
        {
            DeploymentAccessTime = deploymentAccessTime >= 0f ? deploymentAccessTime : 0f;
        }

        public float DeploymentAccessTime { get; }

        public bool IsDeploymentAccessComplete(string nodeId, double now)
        {
            if (!TryGetDeploymentAccessRemaining(nodeId, now, out float remainingSeconds))
            {
                return false;
            }

            return remainingSeconds <= 0f;
        }

        public bool TryGetDeploymentAccessRemaining(string nodeId, double now, out float remainingSeconds)
        {
            remainingSeconds = 0f;
            if (string.IsNullOrWhiteSpace(nodeId) || !mNodes.TryGetValue(nodeId, out NodeDescriptor node))
            {
                return false;
            }

            // 服务器节点没有部署接入门控，始终可用。
            if (node.Role != NetworkNodeRole.User)
            {
                return true;
            }

            // 用户节点未记录部署时刻（防御性兜底）：视为已完成接入。
            if (!mDeployedAt.TryGetValue(nodeId, out double deployedAt))
            {
                return true;
            }

            double remaining = deployedAt + DeploymentAccessTime - now;
            remainingSeconds = remaining > 0d ? (float)remaining : 0f;
            return true;
        }

        public bool IsRegistered(string nodeId)
        {
            return !string.IsNullOrWhiteSpace(nodeId) && mNodes.ContainsKey(nodeId);
        }

        public bool TryGetNode(string nodeId, out NodeDescriptor node)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                node = default;
                return false;
            }

            return mNodes.TryGetValue(nodeId, out node);
        }

        public IReadOnlyCollection<string> GetConnectedNodeIds(string nodeId)
        {
            if (!mConnectedNodeIds.TryGetValue(nodeId, out HashSet<string> connectedNodeIds))
            {
                return Array.Empty<string>();
            }

            string[] snapshot = new string[connectedNodeIds.Count];
            connectedNodeIds.CopyTo(snapshot);
            return snapshot;
        }

        public bool TryGetServerCapabilities(string nodeId, out ServerNodeCapabilities capabilities)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                capabilities = default;
                return false;
            }

            return mServerCapabilities.TryGetValue(nodeId, out capabilities);
        }

        public bool TryGetEdge(string firstNodeId, string secondNodeId, out NetworkEdge edge)
        {
            NetworkEdgeKey key = NetworkEdgeKey.Create(firstNodeId, secondNodeId);
            return mEdges.TryGetValue(key, out edge);
        }

        public IReadOnlyCollection<NetworkEdge> Edges
        {
            get { return new List<NetworkEdge>(mEdges.Values); }
        }

        public IReadOnlyCollection<NodeDescriptor> Nodes
        {
            get { return new List<NodeDescriptor>(mNodes.Values); }
        }

        internal void Register(NodeDescriptor node, ServerNodeCapabilities capabilities = null, double deployedAt = 0d)
        {
            mNodes.Add(node.NodeId, node);
            mConnectedNodeIds.Add(node.NodeId, new HashSet<string>(StringComparer.Ordinal));
            if (node.Role == NetworkNodeRole.User)
            {
                mDeployedAt.Add(node.NodeId, deployedAt);
            }

            if (capabilities != null)
            {
                mServerCapabilities.Add(node.NodeId, capabilities);
            }
        }

        internal void Unregister(string nodeId, List<string> disconnectedNodeIds)
        {
            HashSet<string> connectedNodeIds = mConnectedNodeIds[nodeId];
            foreach (string connectedNodeId in connectedNodeIds)
            {
                disconnectedNodeIds.Add(connectedNodeId);
                mEdges.Remove(NetworkEdgeKey.Create(nodeId, connectedNodeId));
            }

            foreach (string connectedNodeId in disconnectedNodeIds)
            {
                mConnectedNodeIds[connectedNodeId].Remove(nodeId);
            }

            mServerCapabilities.Remove(nodeId);
            mDeployedAt.Remove(nodeId);
            mConnectedNodeIds.Remove(nodeId);
            mNodes.Remove(nodeId);
        }

        internal bool SetConnected(string firstNodeId, string secondNodeId, bool isConnected, float maxTransmissionSpeed)
        {
            HashSet<string> firstConnections = mConnectedNodeIds[firstNodeId];
            HashSet<string> secondConnections = mConnectedNodeIds[secondNodeId];
            NetworkEdgeKey edgeKey = NetworkEdgeKey.Create(firstNodeId, secondNodeId);
            if (isConnected)
            {
                if (!firstConnections.Add(secondNodeId))
                {
                    return false;
                }

                secondConnections.Add(firstNodeId);
                mEdges[edgeKey] = new NetworkEdge(edgeKey, maxTransmissionSpeed);
                return true;
            }

            if (!firstConnections.Remove(secondNodeId))
            {
                return false;
            }

            secondConnections.Remove(firstNodeId);
            mEdges.Remove(edgeKey);
            return true;
        }

        internal void Clear()
        {
            mNodes.Clear();
            mConnectedNodeIds.Clear();
            mServerCapabilities.Clear();
            mEdges.Clear();
            mDeployedAt.Clear();
        }

        protected override void OnInit()
        {
        }

        protected override void OnDeinit()
        {
            Clear();
        }
    }
}
