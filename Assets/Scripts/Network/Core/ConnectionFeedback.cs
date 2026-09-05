using CUC260905.Message;

namespace CUC260905.Network
{
    /// <summary>
    /// 把连线裁决结果转成用户可见的失败原因，并通过 IMessageSystem 投递到消息终端
    /// （MessageTerminalController 监听同 targetId 的 SystemMessagePublishedEvent 后显示）。
    /// 纯逻辑、可独立测试；Success 不产生任何提示。
    /// </summary>
    public static class ConnectionFeedback
    {
        /// <summary>失败裁决对应的终端提示文本；Success 或未知裁决返回 null。</summary>
        public static string GetFailureReason(ConnectionVerdict verdict)
        {
            switch (verdict)
            {
                case ConnectionVerdict.InvalidNodeId:
                    return "连线失败：无效的节点标识。";
                case ConnectionVerdict.NodeNotRegistered:
                    return "连线失败：目标不是已部署的节点。";
                case ConnectionVerdict.SameNode:
                    return "连线失败：不能连接节点自身。";
                case ConnectionVerdict.UserToUserForbidden:
                    return "连线失败：用户节点之间不能互相连接。";
                case ConnectionVerdict.AlreadyConnected:
                    return "连线失败：这两个节点已经连接。";
                case ConnectionVerdict.NodePositionUnavailable:
                    return "连线失败：节点位置不可用，无法连线。";
                case ConnectionVerdict.CrossingEdge:
                    return "连线失败：会与既有连线交叉。";
                case ConnectionVerdict.TopologyWriteFailed:
                    return "连线失败：拓扑写入失败，请重试。";
                case ConnectionVerdict.MaxConnectionsExceeded:
                    return "连线失败：服务器节点已达到最大连接数上限。";
                default:
                    return null;
            }
        }

        /// <summary>
        /// 失败时把原因发布到指定消息终端。Success、未知裁决、缺少消息系统或目标标识时不发布并返回 false。
        /// </summary>
        public static bool TryPublishFailure(IMessageSystem messageSystem, string targetId, ConnectionVerdict verdict)
        {
            if (verdict == ConnectionVerdict.Success)
            {
                return false;
            }

            string reason = GetFailureReason(verdict);
            if (string.IsNullOrWhiteSpace(reason) ||
                messageSystem == null ||
                string.IsNullOrWhiteSpace(targetId))
            {
                return false;
            }

            return messageSystem.Publish(targetId, reason);
        }
    }
}
