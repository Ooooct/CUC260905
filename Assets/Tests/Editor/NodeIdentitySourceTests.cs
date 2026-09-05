using System.Collections.Generic;
using CUC260905.Network;
using NUnit.Framework;

namespace CUC260905.Tests
{
    /// <summary>
    /// INodeIdentitySource：验证系统自动注入的 NodeId 非空、跨调用唯一、带角色前缀，
    /// 并保持"前缀-顺序号-短码"的可读格式（如 server-2-4K7Q）。
    /// 放置系统放置的节点无需手工配置 NodeId，注册键由本 Utility 生成。
    /// </summary>
    public sealed class NodeIdentitySourceTests
    {
        // 短码只允许数字 2-9 与去掉 I/L/O 的大写字母（不含 0/O/1/I/l 易混字符），与实现字符表一致。
        private const string ServerPattern = "^server-[1-9][0-9]*-[2-9A-HJ-NP-Z]{4}$";
        private const string UserPattern = "^user-[1-9][0-9]*-[2-9A-HJ-NP-Z]{4}$";

        [Test]
        public void DefaultSource_ProducesNonEmptyUniquePrefixedIds()
        {
            INodeIdentitySource source = new SequentialNodeIdentitySource();

            string serverA = source.NextNodeId(NetworkNodeRole.Server);
            string serverB = source.NextNodeId(NetworkNodeRole.Server);
            string userA = source.NextNodeId(NetworkNodeRole.User);

            Assert.That(serverA, Is.Not.Empty);
            Assert.That(serverB, Is.Not.EqualTo(serverA));
            Assert.That(userA, Is.Not.EqualTo(serverA));
            Assert.That(serverA, Does.Match(ServerPattern), "服务器 ID 应为 server-<顺序号>-<短码>");
            Assert.That(userA, Does.Match(UserPattern), "用户 ID 应为 user-<顺序号>-<短码>");
        }

        [Test]
        public void StaticCreate_MatchesInstanceBehavior()
        {
            string id = SequentialNodeIdentitySource.Create(NetworkNodeRole.Server);

            Assert.That(id, Is.Not.Empty);
            Assert.That(id, Does.Match(ServerPattern));
        }

        [Test]
        public void NextNodeId_ManyDraws_AllUniquePerRole()
        {
            HashSet<string> serverIds = new HashSet<string>(System.StringComparer.Ordinal);
            HashSet<string> userIds = new HashSet<string>(System.StringComparer.Ordinal);
            SequentialNodeIdentitySource source = new SequentialNodeIdentitySource();
            for (int index = 0; index < 500; index++)
            {
                string serverId = source.NextNodeId(NetworkNodeRole.Server);
                string userId = source.NextNodeId(NetworkNodeRole.User);
                Assert.That(serverIds.Add(serverId), Is.True, $"服务器 ID 不应重复：{serverId}");
                Assert.That(userIds.Add(userId), Is.True, $"用户 ID 不应重复：{userId}");
            }
        }
    }
}
