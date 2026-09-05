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
        SelfSendForbidden = 7
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

    /// <summary>一次没有可行路由的数据包快照，供之后追加专门的提示或特效表现。</summary>
    public readonly struct PacketUnreachableEvent : IEvent
    {
        public readonly string SourceNodeId;
        public readonly string DestinationNodeId;
        public readonly float PacketSize;
        public readonly PacketTransmissionResult Result;

        public PacketUnreachableEvent(
            string sourceNodeId,
            string destinationNodeId,
            float packetSize,
            PacketTransmissionResult result)
        {
            SourceNodeId = sourceNodeId;
            DestinationNodeId = destinationNodeId;
            PacketSize = packetSize;
            Result = result;
        }
    }
}
