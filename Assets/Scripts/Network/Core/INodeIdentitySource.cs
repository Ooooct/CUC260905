using QFramework;

namespace CUC260905.Network
{
    /// <summary>
    /// 生成节点唯一 ID 的基础能力（Utility）。
    /// 由系统在放置/创建节点时自动注入 NodeId，替代场景或 prefab 上手工配置。
    /// 作为 Utility 注册，便于测试替换为固定序列。
    /// </summary>
    public interface INodeIdentitySource : IUtility
    {
        string NextNodeId(NetworkNodeRole role);
    }

    /// <summary>
    /// 默认实现：按角色前缀 + GUID 生成唯一 ID。
    /// 无状态、跨场景唯一，放置系统每次实例化都能拿到不冲突的 NodeId。
    /// </summary>
    public sealed class GuidNodeIdentitySource : INodeIdentitySource
    {
        public string NextNodeId(NetworkNodeRole role)
        {
            return Create(role);
        }

        /// <summary>供不依赖 Utility 的场景直接生成 ID（例如缺少身份 Utility 时兜底）。</summary>
        public static string Create(NetworkNodeRole role)
        {
            string prefix = role == NetworkNodeRole.Server ? "server" : "user";
            return $"{prefix}-{System.Guid.NewGuid():N}";
        }
    }
}
