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
    /// 默认实现：按角色生成"前缀-顺序号-短随机后缀"的可读唯一 ID（如 server-2-4K7Q）。
    /// 顺序号进程内单调递增，保证同一架构实例内不重复；短随机后缀（去易混字符表）
    /// 用于避开场景/预置手填 ID（例如 SampleScene 里的 server-1），同时保留人读辨识度。
    /// 无状态、跨场景唯一，放置系统每次实例化都能拿到不冲突的 NodeId。
    /// </summary>
    public sealed class SequentialNodeIdentitySource : INodeIdentitySource
    {
        /// <summary>短码字符表：去掉易混的 0/O、1/I/l，提升口述与抄录准确度。</summary>
        private const string SuffixAlphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";
        private const int SuffixLength = 4;

        // 顺序号按角色进程内共享：实例路径与静态 Create 兜底共用同一序列，避免两套计数不一致。
        private static int mServerCount;
        private static int mUserCount;
        private static readonly System.Random mRandom = new System.Random();

        public string NextNodeId(NetworkNodeRole role)
        {
            return Create(role);
        }

        /// <summary>供不依赖 Utility 的场景直接生成 ID（例如缺少身份 Utility 时兜底），与实例路径共用同一顺序号序列。</summary>
        public static string Create(NetworkNodeRole role)
        {
            string prefix = role == NetworkNodeRole.Server ? "server" : "user";
            int sequence = role == NetworkNodeRole.Server ? ++mServerCount : ++mUserCount;
            return string.Format("{0}-{1}-{2}", prefix, sequence, CreateSuffix());
        }

        private static string CreateSuffix()
        {
            char[] chars = new char[SuffixLength];
            for (int index = 0; index < SuffixLength; index++)
            {
                chars[index] = SuffixAlphabet[mRandom.Next(SuffixAlphabet.Length)];
            }
            return new string(chars);
        }
    }
}
