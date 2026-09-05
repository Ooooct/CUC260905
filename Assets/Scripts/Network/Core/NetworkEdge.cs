using System;
using QFramework;

namespace CUC260905.Network
{
    /// <summary>
    /// 无向边的规范化键。构造时按 Ordinal 字典序排序两端 ID，
    /// 使 (a, b) 与 (b, a) 得到同一个键，作为平行边表的字典键。
    /// </summary>
    public readonly struct NetworkEdgeKey : IEquatable<NetworkEdgeKey>
    {
        /// <summary>字典序较小的端点 ID。</summary>
        public readonly string FirstNodeId;

        /// <summary>字典序较大的端点 ID。</summary>
        public readonly string SecondNodeId;

        public NetworkEdgeKey(string nodeA, string nodeB)
        {
            int comparison = string.CompareOrdinal(nodeA, nodeB);
            if (comparison <= 0)
            {
                FirstNodeId = nodeA;
                SecondNodeId = nodeB;
            }
            else
            {
                FirstNodeId = nodeB;
                SecondNodeId = nodeA;
            }
        }

        public static NetworkEdgeKey Create(string nodeA, string nodeB)
        {
            return new NetworkEdgeKey(nodeA, nodeB);
        }

        public bool Equals(NetworkEdgeKey other)
        {
            return string.Equals(FirstNodeId, other.FirstNodeId, StringComparison.Ordinal) &&
                   string.Equals(SecondNodeId, other.SecondNodeId, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is NetworkEdgeKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            int firstHash = FirstNodeId == null ? 0 : StringComparer.Ordinal.GetHashCode(FirstNodeId);
            int secondHash = SecondNodeId == null ? 0 : StringComparer.Ordinal.GetHashCode(SecondNodeId);
            return HashCode.Combine(firstHash, secondHash);
        }

        public override string ToString()
        {
            return $"{FirstNodeId}--{SecondNodeId}";
        }
    }

    /// <summary>
    /// 无向边记录：规范化端点键 + 最大传输速度。
    /// 速度以 BindableProperty 存储：Value 变化即通知监听器（Register / RegisterWithInitValue）。
    /// 与邻接表平行存储，只描述边属性，不决定连通规则。
    /// </summary>
    public sealed class NetworkEdge
    {
        public NetworkEdgeKey Key { get; }

        /// <summary>最大传输速度（抽象单位/秒）。0 = 未配置。</summary>
        public BindableProperty<float> MaxTransmissionSpeed { get; }

        public NetworkEdge(NetworkEdgeKey key, float maxTransmissionSpeed)
        {
            Key = key;
            MaxTransmissionSpeed = new BindableProperty<float>(maxTransmissionSpeed);
        }

        public NetworkEdge(string firstNodeId, string secondNodeId, float maxTransmissionSpeed)
            : this(NetworkEdgeKey.Create(firstNodeId, secondNodeId), maxTransmissionSpeed)
        {
        }

        /// <summary>字典序较小的端点 ID。</summary>
        public string FirstNodeId
        {
            get { return Key.FirstNodeId; }
        }

        /// <summary>字典序较大的端点 ID。</summary>
        public string SecondNodeId
        {
            get { return Key.SecondNodeId; }
        }

        public override bool Equals(object obj)
        {
            return obj is NetworkEdge other &&
                   Key.Equals(other.Key) &&
                   MaxTransmissionSpeed.Value.Equals(other.MaxTransmissionSpeed.Value);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Key, MaxTransmissionSpeed.Value);
        }

        public override string ToString()
        {
            return $"{Key} [speed={MaxTransmissionSpeed.Value}]";
        }
    }
}
