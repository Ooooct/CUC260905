using System;
using System.Collections.Generic;
using QFramework;

namespace CUC260905.Network
{
    /// <summary>数据包发送结果；只有 Success 会占用服务器最近一秒的吞吐容量。</summary>
    public enum PacketTransmissionResult
    {
        Success = 0,
        SourceNotRegistered = 1,
        SourceNotUserNode = 2,
        DestinationUnavailable = 3,
        DestinationNotUserNode = 4,
        InvalidPacketSize = 5,
        Unreachable = 6,
        SelfSendForbidden = 7,
        SourceNotAccessible = 8,
        DestinationNotAccessible = 9
    }

    /// <summary>一次成功发送的数据包快照；路径包含起点用户节点和终点用户节点，中间经服务器中继。</summary>
    public readonly struct PacketTransmittedEvent : IEvent
    {
        public readonly string SourceNodeId;
        public readonly string DestinationNodeId;
        public readonly float PacketSize;
        public readonly IReadOnlyList<string> PathNodeIds;

        public PacketTransmittedEvent(
            string sourceNodeId,
            string destinationNodeId,
            float packetSize,
            IReadOnlyList<string> pathNodeIds)
        {
            SourceNodeId = sourceNodeId;
            DestinationNodeId = destinationNodeId;
            PacketSize = packetSize;
            PathNodeIds = pathNodeIds == null
                ? Array.Empty<string>()
                : new List<string>(pathNodeIds).AsReadOnly();
        }
    }

    /// <summary>
    /// 一次没有可行路由的数据包快照。除起点外，还会携带在寻路期间因吞吐上限被拒绝的服务器，
    /// 供表现层标记实际阻塞传输的问题节点。
    /// </summary>
    public readonly struct PacketUnreachableEvent : IEvent
    {
        public readonly string SourceNodeId;
        public readonly string DestinationNodeId;
        public readonly float PacketSize;
        public readonly PacketTransmissionResult Result;
        public readonly IReadOnlyList<string> ProblemNodeIds;

        public PacketUnreachableEvent(
            string sourceNodeId,
            string destinationNodeId,
            float packetSize,
            PacketTransmissionResult result,
            IReadOnlyList<string> problemNodeIds = null)
        {
            SourceNodeId = sourceNodeId;
            DestinationNodeId = destinationNodeId;
            PacketSize = packetSize;
            Result = result;
            ProblemNodeIds = problemNodeIds == null
                ? Array.Empty<string>()
                : new List<string>(problemNodeIds).AsReadOnly();
        }
    }

    /// <summary>总体负载首次达到失败阈值时发送；同一局内只会发送一次。</summary>
    public readonly struct GameOverEvent : IEvent
    {
    }
}
