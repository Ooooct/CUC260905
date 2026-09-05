using System;
using System.Collections.Generic;
using QFramework;
using UnityEngine;

namespace CUC260905.Network
{
    /// <summary>
    /// 节点连线的唯一业务入口：组合角色、重复、位置与交叉检查后，
    /// 通过 INetworkTopologySystem 写入无向边，并让模型发布 NodeConnectivityChangedEvent。
    /// 表现层的拖拽工具只负责采集手势，不拼接校验。
    /// </summary>
    public interface INetworkConnectionSystem : ISystem
    {
        /// <summary>尝试连接两个已注册节点；返回裁决结果，Success 时才写入拓扑。</summary>
        ConnectionVerdict TryConnect(string firstNodeId, string secondNodeId);
    }

    /// <summary>
    /// 连线规则 System。交叉检查需要世界坐标，因此通过 INodePositionProvider 读取位置；
    /// 该 Provider 由表现层在场景运行后注册，故本类在 OnInit 中只校验模型依赖，
    /// Provider 在每次 TryConnect 时惰性解析，未就绪时返回 NodePositionUnavailable。
    /// </summary>
    public sealed class NetworkConnectionSystem : AbstractSystem, INetworkConnectionSystem
    {
        private readonly INetworkTopologyModel mModel;
        private readonly INetworkTopologySystem mTopologySystem;

        public NetworkConnectionSystem(
            INetworkTopologyModel model,
            INetworkTopologySystem topologySystem)
        {
            mModel = model;
            mTopologySystem = topologySystem;
        }

        public ConnectionVerdict TryConnect(string firstNodeId, string secondNodeId)
        {
            if (string.IsNullOrWhiteSpace(firstNodeId) || string.IsNullOrWhiteSpace(secondNodeId))
            {
                return ConnectionVerdict.InvalidNodeId;
            }

            if (string.Equals(firstNodeId, secondNodeId, StringComparison.Ordinal))
            {
                return ConnectionVerdict.SameNode;
            }

            if (!mModel.IsRegistered(firstNodeId) || !mModel.IsRegistered(secondNodeId))
            {
                return ConnectionVerdict.NodeNotRegistered;
            }

            if (!mModel.TryGetNode(firstNodeId, out NodeDescriptor first) ||
                !mModel.TryGetNode(secondNodeId, out NodeDescriptor second))
            {
                return ConnectionVerdict.NodeNotRegistered;
            }

            ConnectionVerdict roleVerdict = NetworkConnectionRules.ValidateRoles(first, second);
            if (roleVerdict != ConnectionVerdict.Success)
            {
                return roleVerdict;
            }

            // 无向重复边：任一端已记录另一端即视为已连接。
            // 显式序数比较，避免集合 Contains 绑定到 MemoryExtensions 的 span 重载（CS7036）。
            if (ContainsNodeId(mModel.GetConnectedNodeIds(firstNodeId), secondNodeId))
            {
                return ConnectionVerdict.AlreadyConnected;
            }

            // 最大连接数检查：两个端点各自校验，任一服务器节点
            // “当前已连接边数 + 1”超过其 MaxConnections 即拒绝。
            ConnectionVerdict capacityVerdict = CheckConnectionCapacity(first, second);
            if (capacityVerdict != ConnectionVerdict.Success)
            {
                return capacityVerdict;
            }

            INodePositionProvider positionProvider = this.GetUtility<INodePositionProvider>();
            if (positionProvider == null ||
                !positionProvider.TryGetNodePosition(firstNodeId, out Vector3 firstPosition) ||
                !positionProvider.TryGetNodePosition(secondNodeId, out Vector3 secondPosition))
            {
                return ConnectionVerdict.NodePositionUnavailable;
            }

            ConnectionVerdict crossingVerdict = NetworkConnectionRules.CheckCrossing(
                new Vector2(firstPosition.x, firstPosition.y),
                new Vector2(secondPosition.x, secondPosition.y),
                BuildExistingSegments(firstNodeId, secondNodeId, positionProvider));
            if (crossingVerdict != ConnectionVerdict.Success)
            {
                return crossingVerdict;
            }

            NetworkTopologyResult result = mTopologySystem.SetConnected(firstNodeId, secondNodeId, true);
            switch (result)
            {
                case NetworkTopologyResult.Success:
                    return ConnectionVerdict.Success;
                case NetworkTopologyResult.NoChange:
                    return ConnectionVerdict.AlreadyConnected;
                default:
                    return ConnectionVerdict.TopologyWriteFailed;
            }
        }

        /// <summary>判断一个节点是否已连接另一个节点（无向，序数比较）。</summary>
        private static bool ContainsNodeId(IReadOnlyCollection<string> connectedNodeIds, string nodeId)
        {
            foreach (string connectedNodeId in connectedNodeIds)
            {
                if (string.Equals(connectedNodeId, nodeId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 对连线两端分别做最大连接数校验：用户节点无能力档案直接放行；
        /// 服务器节点无能力档案或 MaxConnections 为 0 视为未配置/无限。
        /// </summary>
        private ConnectionVerdict CheckConnectionCapacity(NodeDescriptor first, NodeDescriptor second)
        {
            ConnectionVerdict firstVerdict = CheckNodeConnectionCapacity(first);
            if (firstVerdict != ConnectionVerdict.Success)
            {
                return firstVerdict;
            }

            return CheckNodeConnectionCapacity(second);
        }

        private ConnectionVerdict CheckNodeConnectionCapacity(NodeDescriptor node)
        {
            if (node.Role != NetworkNodeRole.Server ||
                !mModel.TryGetServerCapabilities(node.NodeId, out ServerNodeCapabilities capabilities) ||
                capabilities == null)
            {
                return ConnectionVerdict.Success;
            }

            return NetworkConnectionRules.ValidateConnectionCapacity(
                mModel.GetConnectedNodeIds(node.NodeId).Count,
                capabilities.MaxConnections.Value);
        }

        /// <summary>
        /// 收集既有边中不与候选边共享端点的线段（邻接边只在一个端点相遇，不可能内部交叉，跳过以省算力且语义正确）。
        /// 端点位置缺失的边（例如正在拆除）跳过，不作为交叉依据。
        /// </summary>
        private List<NetworkEdgeSegment> BuildExistingSegments(
            string firstNodeId,
            string secondNodeId,
            INodePositionProvider positionProvider)
        {
            List<NetworkEdgeSegment> segments = new List<NetworkEdgeSegment>();
            foreach (NetworkEdge edge in mModel.Edges)
            {
                if (edge.FirstNodeId == firstNodeId || edge.FirstNodeId == secondNodeId ||
                    edge.SecondNodeId == firstNodeId || edge.SecondNodeId == secondNodeId)
                {
                    continue;
                }

                if (!positionProvider.TryGetNodePosition(edge.FirstNodeId, out Vector3 start) ||
                    !positionProvider.TryGetNodePosition(edge.SecondNodeId, out Vector3 end))
                {
                    continue;
                }

                segments.Add(new NetworkEdgeSegment(start, end));
            }

            return segments;
        }

        protected override void OnInit()
        {
            if (mModel == null)
            {
                throw new InvalidOperationException(
                    "NetworkConnectionSystem 初始化前必须创建 INetworkTopologyModel。");
            }

            if (mTopologySystem == null)
            {
                throw new InvalidOperationException(
                    "NetworkConnectionSystem 初始化前必须创建 INetworkTopologySystem。");
            }
        }
    }
}
