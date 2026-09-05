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
    }

    /// <summary>
    /// 保存节点、无向连线、服务器能力档案与边属性；
    /// 不保存 Unity 对象或业务连通规则。
    /// </summary>
    public sealed class NetworkTopologyModel : AbstractModel, INetworkTopologyModel
    {
        private readonly Dictionary<string, NodeDescriptor> mNodes =
            new Dictionary<string, NodeDescriptor>(StringComparer.Ordinal);
        private readonly Dictionary<string, HashSet<string>> mConnectedNodeIds =
            new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        private readonly Dictionary<string, ServerNodeCapabilities> mServerCapabilities =
            new Dictionary<string, ServerNodeCapabilities>(StringComparer.Ordinal);
        private readonly Dictionary<NetworkEdgeKey, NetworkEdge> mEdges =
            new Dictionary<NetworkEdgeKey, NetworkEdge>();

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

        internal void Register(NodeDescriptor node, ServerNodeCapabilities capabilities = null)
        {
            mNodes.Add(node.NodeId, node);
            mConnectedNodeIds.Add(node.NodeId, new HashSet<string>(StringComparer.Ordinal));
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
