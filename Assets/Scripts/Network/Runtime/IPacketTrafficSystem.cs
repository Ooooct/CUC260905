using System;
using System.Collections.Generic;
using CUC260905.Message;
using QFramework;

namespace CUC260905.Network
{
    /// <summary>数据包发送、动态路由和服务器近一秒吞吐量账本的唯一入口。</summary>
    public interface IPacketTrafficSystem : ISystem
    {
        /// <summary>清理已超出一秒窗口的处理记录，并同步服务器当前负载。</summary>
        void Tick(double now);

        /// <summary>从用户节点随机选取另一个用户节点作为目标并发送数据包。</summary>
        PacketTransmissionResult SendRandomPacket(
            string sourceNodeId,
            float packetSize,
            float loadCostWeight,
            string messageTargetId,
            double now,
            Random random);

        /// <summary>向指定用户节点发送数据包；主要供规则测试和后续任务目标使用。</summary>
        PacketTransmissionResult SendPacket(
            string sourceNodeId,
            string destinationNodeId,
            float packetSize,
            float loadCostWeight,
            string messageTargetId,
            double now);
    }

    /// <summary>
    /// 以服务器近一秒的累计处理量作为容量账本。寻路采用 Dijkstra：
    /// 边基础代价为 1，进入服务器的额外代价为 loadCostWeight * 预测利用率²；
    /// 预测负载超过服务器上限的节点直接不参与候选路径。
    /// </summary>
    public sealed class PacketTrafficSystem : AbstractSystem, IPacketTrafficSystem
    {
        private const double LoadWindowSeconds = 1d;

        private readonly NetworkTopologyModel mTopologyModel;
        private readonly IMessageSystem mMessageSystem;
        private readonly Dictionary<string, Queue<TimedPacket>> mServerPackets =
            new Dictionary<string, Queue<TimedPacket>>(StringComparer.Ordinal);

        public PacketTrafficSystem(NetworkTopologyModel topologyModel, IMessageSystem messageSystem)
        {
            mTopologyModel = topologyModel;
            mMessageSystem = messageSystem;
        }

        protected override void OnInit()
        {
        }

        protected override void OnDeinit()
        {
            mServerPackets.Clear();
        }

        public void Tick(double now)
        {
            PruneExpiredPackets(now);
        }

        public PacketTransmissionResult SendRandomPacket(
            string sourceNodeId,
            float packetSize,
            float loadCostWeight,
            string messageTargetId,
            double now,
            Random random)
        {
            if (random == null)
            {
                throw new ArgumentNullException(nameof(random));
            }

            List<string> destinationNodeIds = new List<string>();
            foreach (NodeDescriptor node in mTopologyModel.Nodes)
            {
                if (node.Role == NetworkNodeRole.User &&
                    !string.Equals(node.NodeId, sourceNodeId, StringComparison.Ordinal))
                {
                    destinationNodeIds.Add(node.NodeId);
                }
            }

            if (destinationNodeIds.Count == 0)
            {
                // 没有其他用户节点可作目标：这不是路由失败，而是"暂无发送目标"。
                // 静默返回，不发布消息与事件，避免单节点场景反复刷屏、
                // 误触发总体负载惩罚与失败反馈圆。
                PruneExpiredPackets(now);
                return PacketTransmissionResult.DestinationUnavailable;
            }

            string destinationNodeId = destinationNodeIds[random.Next(destinationNodeIds.Count)];
            return SendPacket(
                sourceNodeId,
                destinationNodeId,
                packetSize,
                loadCostWeight,
                messageTargetId,
                now);
        }

        public PacketTransmissionResult SendPacket(
            string sourceNodeId,
            string destinationNodeId,
            float packetSize,
            float loadCostWeight,
            string messageTargetId,
            double now)
        {
            PruneExpiredPackets(now);
            PacketTransmissionResult validationResult = ValidateRequest(sourceNodeId, destinationNodeId, packetSize);
            if (validationResult != PacketTransmissionResult.Success)
            {
                return PublishUnreachable(sourceNodeId, destinationNodeId, packetSize, validationResult, messageTargetId);
            }

            List<string> pathNodeIds = FindLowestCostPath(
                sourceNodeId,
                destinationNodeId,
                packetSize,
                Math.Max(0f, loadCostWeight));
            if (pathNodeIds == null)
            {
                return PublishUnreachable(
                    sourceNodeId,
                    destinationNodeId,
                    packetSize,
                    PacketTransmissionResult.Unreachable,
                    messageTargetId);
            }

            ReserveServerCapacity(pathNodeIds, packetSize, now);
            this.SendEvent(new PacketTransmittedEvent(
                sourceNodeId,
                destinationNodeId,
                packetSize,
                pathNodeIds));
            return PacketTransmissionResult.Success;
        }

        private PacketTransmissionResult ValidateRequest(string sourceNodeId, string destinationNodeId, float packetSize)
        {
            if (float.IsNaN(packetSize) || float.IsInfinity(packetSize) || packetSize <= 0f)
            {
                return PacketTransmissionResult.InvalidPacketSize;
            }

            if (!mTopologyModel.TryGetNode(sourceNodeId, out NodeDescriptor sourceNode))
            {
                return PacketTransmissionResult.SourceNotRegistered;
            }

            if (sourceNode.Role != NetworkNodeRole.User)
            {
                return PacketTransmissionResult.SourceNotUserNode;
            }

            if (!mTopologyModel.TryGetNode(destinationNodeId, out NodeDescriptor destinationNode) ||
                destinationNode.Role != NetworkNodeRole.User)
            {
                return PacketTransmissionResult.DestinationNotUserNode;
            }

            // 用户节点只会向"其他"用户节点发送数据包，不允许自我发送。
            if (string.Equals(sourceNodeId, destinationNodeId, StringComparison.Ordinal))
            {
                return PacketTransmissionResult.SelfSendForbidden;
            }

            return PacketTransmissionResult.Success;
        }

        private List<string> FindLowestCostPath(
            string sourceNodeId,
            string destinationNodeId,
            float packetSize,
            float loadCostWeight)
        {
            Dictionary<string, float> distances = new Dictionary<string, float>(StringComparer.Ordinal)
            {
                { sourceNodeId, 0f }
            };
            Dictionary<string, string> previousNodeIds = new Dictionary<string, string>(StringComparer.Ordinal);
            HashSet<string> visitedNodeIds = new HashSet<string>(StringComparer.Ordinal);

            while (TryGetNearestUnvisitedNode(distances, visitedNodeIds, out string currentNodeId))
            {
                if (string.Equals(currentNodeId, destinationNodeId, StringComparison.Ordinal))
                {
                    return BuildPath(sourceNodeId, destinationNodeId, previousNodeIds);
                }

                visitedNodeIds.Add(currentNodeId);
                foreach (string neighborNodeId in mTopologyModel.GetConnectedNodeIds(currentNodeId))
                {
                    if (visitedNodeIds.Contains(neighborNodeId))
                    {
                        continue;
                    }

                    // 目标用户节点是路径终点：允许进入（进入代价为 1），但不会作为中继。
                    bool isDestination = string.Equals(neighborNodeId, destinationNodeId, StringComparison.Ordinal);
                    float neighborCost;
                    if (isDestination)
                    {
                        neighborCost = 1f;
                    }
                    else if (!CanUseAsRouteNode(neighborNodeId, packetSize, loadCostWeight, out neighborCost))
                    {
                        continue;
                    }

                    float candidateDistance = distances[currentNodeId] + neighborCost;
                    if (!distances.TryGetValue(neighborNodeId, out float existingDistance) ||
                        candidateDistance < existingDistance)
                    {
                        distances[neighborNodeId] = candidateDistance;
                        previousNodeIds[neighborNodeId] = currentNodeId;
                    }
                }
            }

            return null;
        }

        private bool CanUseAsRouteNode(
            string nodeId,
            float packetSize,
            float loadCostWeight,
            out float nodeCost)
        {
            nodeCost = 0f;
            if (!mTopologyModel.TryGetNode(nodeId, out NodeDescriptor node) ||
                node.Role != NetworkNodeRole.Server ||
                !mTopologyModel.TryGetServerCapabilities(nodeId, out ServerNodeCapabilities capabilities))
            {
                return false;
            }

            float capacity = capabilities.DataProcessingPerSecond.Value;
            float currentLoad = capabilities.CurrentDataLoadPerSecond.Value;
            if (capacity > 0f && currentLoad + packetSize > capacity)
            {
                return false;
            }

            float utilization = capacity > 0f
                ? (currentLoad + packetSize) / capacity
                : 0f;
            nodeCost = 1f + loadCostWeight * utilization * utilization;
            return true;
        }

        private static bool TryGetNearestUnvisitedNode(
            Dictionary<string, float> distances,
            HashSet<string> visitedNodeIds,
            out string nodeId)
        {
            nodeId = null;
            float nearestDistance = float.PositiveInfinity;
            foreach (KeyValuePair<string, float> pair in distances)
            {
                if (visitedNodeIds.Contains(pair.Key) || pair.Value >= nearestDistance)
                {
                    continue;
                }

                nodeId = pair.Key;
                nearestDistance = pair.Value;
            }

            return nodeId != null;
        }

        private static List<string> BuildPath(
            string sourceNodeId,
            string destinationNodeId,
            Dictionary<string, string> previousNodeIds)
        {
            List<string> pathNodeIds = new List<string>();
            string currentNodeId = destinationNodeId;
            while (currentNodeId != null)
            {
                pathNodeIds.Add(currentNodeId);
                if (string.Equals(currentNodeId, sourceNodeId, StringComparison.Ordinal))
                {
                    pathNodeIds.Reverse();
                    return pathNodeIds;
                }

                if (!previousNodeIds.TryGetValue(currentNodeId, out currentNodeId))
                {
                    return null;
                }
            }

            return null;
        }

        private void ReserveServerCapacity(IReadOnlyList<string> pathNodeIds, float packetSize, double now)
        {
            for (int index = 0; index < pathNodeIds.Count; index++)
            {
                string nodeId = pathNodeIds[index];
                if (!mTopologyModel.TryGetNode(nodeId, out NodeDescriptor node) || node.Role != NetworkNodeRole.Server)
                {
                    continue;
                }

                if (!mServerPackets.TryGetValue(nodeId, out Queue<TimedPacket> packets))
                {
                    packets = new Queue<TimedPacket>();
                    mServerPackets.Add(nodeId, packets);
                }

                packets.Enqueue(new TimedPacket(now, packetSize));
                UpdateServerCurrentLoad(nodeId, packets);
            }
        }

        private void PruneExpiredPackets(double now)
        {
            foreach (KeyValuePair<string, Queue<TimedPacket>> pair in mServerPackets)
            {
                Queue<TimedPacket> packets = pair.Value;
                while (packets.Count > 0 && now - packets.Peek().SentAt >= LoadWindowSeconds)
                {
                    packets.Dequeue();
                }

                UpdateServerCurrentLoad(pair.Key, packets);
            }
        }

        private void UpdateServerCurrentLoad(string nodeId, Queue<TimedPacket> packets)
        {
            if (!mTopologyModel.TryGetServerCapabilities(nodeId, out ServerNodeCapabilities capabilities))
            {
                return;
            }

            float load = 0f;
            foreach (TimedPacket packet in packets)
            {
                load += packet.Size;
            }

            capabilities.CurrentDataLoadPerSecond.Value = load;
        }

        private PacketTransmissionResult PublishUnreachable(
            string sourceNodeId,
            string destinationNodeId,
            float packetSize,
            PacketTransmissionResult result,
            string messageTargetId)
        {
            string targetText = string.IsNullOrWhiteSpace(destinationNodeId) ? "用户节点" : destinationNodeId;
            if (mMessageSystem != null && !string.IsNullOrWhiteSpace(messageTargetId))
            {
                mMessageSystem.Publish(
                    messageTargetId,
                    $"数据包不可达：{sourceNodeId} 无法向 {targetText} 发送 {packetSize:0.#} Mb（{result}）。");
            }

            this.SendEvent(new PacketUnreachableEvent(sourceNodeId, destinationNodeId, packetSize, result));
            return result;
        }

        private readonly struct TimedPacket
        {
            public readonly double SentAt;
            public readonly float Size;

            public TimedPacket(double sentAt, float size)
            {
                SentAt = sentAt;
                Size = size;
            }
        }
    }
}
