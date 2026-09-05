using CUC260905.Network;
using NUnit.Framework;

namespace CUC260905.Tests
{
    /// <summary>
    /// INodeIdentitySource：验证系统自动注入的 NodeId 非空、跨调用唯一、带角色前缀。
    /// 放置系统放置的节点无需手工配置 NodeId，注册键由本 Utility 生成。
    /// </summary>
    public sealed class NodeIdentitySourceTests
    {
        [Test]
        public void GuidSource_ProducesNonEmptyUniquePrefixedIds()
        {
            INodeIdentitySource source = new GuidNodeIdentitySource();

            string serverA = source.NextNodeId(NetworkNodeRole.Server);
            string serverB = source.NextNodeId(NetworkNodeRole.Server);
            string userA = source.NextNodeId(NetworkNodeRole.User);

            Assert.That(serverA, Is.Not.Empty);
            Assert.That(serverB, Is.Not.EqualTo(serverA));
            Assert.That(userA, Is.Not.EqualTo(serverA));
            Assert.That(serverA, Does.StartWith("server-"));
            Assert.That(userA, Does.StartWith("user-"));
        }

        [Test]
        public void StaticCreate_MatchesInstanceBehavior()
        {
            string id = GuidNodeIdentitySource.Create(NetworkNodeRole.Server);

            Assert.That(id, Is.Not.Empty);
            Assert.That(id, Does.StartWith("server-"));
        }
    }
}
