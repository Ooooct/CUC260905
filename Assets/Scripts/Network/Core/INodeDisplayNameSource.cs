using QFramework;

namespace CUC260905.Network
{
    /// <summary>
    /// 生成节点唯一显示名的基础能力（Utility）。
    /// 节点注册时 DisplayName 留空则由系统自动分配按角色编号的友好名，
    /// 使场景中每个节点都有可辨识的名字，而不是所有实例共享同一显示名。
    /// 作为 Utility 注册，便于测试替换为固定序列。
    /// </summary>
    public interface INodeDisplayNameSource : IUtility
    {
        string NextDisplayName(NetworkNodeRole role);
    }

    /// <summary>
    /// 默认实现：按角色维护递增计数，产出"服务器 #N"/"用户 #N"。
    /// 计数随架构实例存活（会话内不重复）；节点销毁后不复用编号，避免新旧节点重名。
    /// </summary>
    public sealed class SequentialNodeDisplayNameSource : INodeDisplayNameSource
    {
        private int mServerCount;
        private int mUserCount;

        public string NextDisplayName(NetworkNodeRole role)
        {
            if (role == NetworkNodeRole.Server)
            {
                mServerCount++;
                return string.Format("服务器 #{0}", mServerCount);
            }

            mUserCount++;
            return string.Format("用户 #{0}", mUserCount);
        }
    }
}
