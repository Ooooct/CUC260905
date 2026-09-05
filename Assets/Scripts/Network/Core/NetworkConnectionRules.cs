using System.Collections.Generic;
using UnityEngine;

namespace CUC260905.Network
{
    /// <summary>一次连线尝试的裁决结果。Success 表示所有规则通过，可以写入拓扑。</summary>
    public enum ConnectionVerdict
    {
        Success = 0,
        InvalidNodeId = 1,
        NodeNotRegistered = 2,
        SameNode = 3,
        UserToUserForbidden = 4,
        AlreadyConnected = 5,
        NodePositionUnavailable = 6,
        CrossingEdge = 7,
        TopologyWriteFailed = 8,
        MaxConnectionsExceeded = 9
    }

    /// <summary>纯几何线段，供交叉检查使用；不持有节点或模型引用。</summary>
    public readonly struct NetworkEdgeSegment
    {
        public readonly Vector2 Start;
        public readonly Vector2 End;

        public NetworkEdgeSegment(Vector2 start, Vector2 end)
        {
            Start = start;
            End = end;
        }

        public NetworkEdgeSegment(Vector3 start, Vector3 end)
            : this(new Vector2(start.x, start.y), new Vector2(end.x, end.y))
        {
        }
    }

    /// <summary>
    /// 连线规则的纯逻辑校验器：不依赖 Unity 对象、模型或系统。
    /// 角色规则：仅禁止 用户↔用户；自连、重复连线、交叉等由调用方（System）组合判定，
    /// 本类只提供可独立测试的几何与角色原子能力。
    /// </summary>
    public static class NetworkConnectionRules
    {
        /// <summary>两条线段是否在内部（不含端点）相交。平行或共线重叠不算交叉。</summary>
        public static bool SegmentsCross(
            in Vector2 firstStart,
            in Vector2 firstEnd,
            in Vector2 secondStart,
            in Vector2 secondEnd)
        {
            Vector2 firstDirection = firstEnd - firstStart;
            Vector2 secondDirection = secondEnd - secondStart;
            float denominator = Cross(firstDirection, secondDirection);
            if (Mathf.Approximately(denominator, 0.0f))
            {
                // 平行（含共线）：不存在唯一的内部交点，按“不交叉”处理。
                return false;
            }

            Vector2 originDelta = secondStart - firstStart;
            float t = Cross(originDelta, secondDirection) / denominator;
            float u = Cross(originDelta, firstDirection) / denominator;
            return IsStrictlyInside(t) && IsStrictlyInside(u);
        }

        /// <summary>
        /// 候选线段与既有边集合的交叉检查。
        /// 既有边已由调用方过滤掉与候选线段共享端点的邻接边（严格内部相交本身也会排除端点重合），
        /// 这里只负责逐条判定并返回首个冲突。
        /// </summary>
        public static ConnectionVerdict CheckCrossing(
            in Vector2 fromPosition,
            in Vector2 toPosition,
            IReadOnlyCollection<NetworkEdgeSegment> existingSegments)
        {
            if (existingSegments == null)
            {
                return ConnectionVerdict.Success;
            }

            foreach (NetworkEdgeSegment segment in existingSegments)
            {
                if (SegmentsCross(fromPosition, toPosition, segment.Start, segment.End))
                {
                    return ConnectionVerdict.CrossingEdge;
                }
            }

            return ConnectionVerdict.Success;
        }

        /// <summary>角色连通规则：仅禁止用户节点互相连接。</summary>
        public static ConnectionVerdict ValidateRoles(NodeDescriptor first, NodeDescriptor second)
        {
            if (first.Role == NetworkNodeRole.User && second.Role == NetworkNodeRole.User)
            {
                return ConnectionVerdict.UserToUserForbidden;
            }

            return ConnectionVerdict.Success;
        }

        /// <summary>
        /// 最大连接数规则：0 或负值视为未配置/无限；
        /// 当前已连接边数 + 1（本次新增）超过上限时拒绝连线。
        /// </summary>
        public static ConnectionVerdict ValidateConnectionCapacity(int currentConnectionCount, int maxConnections)
        {
            if (maxConnections <= 0)
            {
                return ConnectionVerdict.Success;
            }

            return currentConnectionCount + 1 > maxConnections
                ? ConnectionVerdict.MaxConnectionsExceeded
                : ConnectionVerdict.Success;
        }

        /// <summary>参数严格落在开区间 (0, 1)：交点位于线段内部，不含端点。</summary>
        private static bool IsStrictlyInside(float value)
        {
            return value > 0.0f && value < 1.0f;
        }

        private static float Cross(in Vector2 first, in Vector2 second)
        {
            return first.x * second.y - first.y * second.x;
        }
    }
}
