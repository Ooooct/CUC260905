using System.Collections.Generic;
using CUC260905.Network;
using NUnit.Framework;
using UnityEngine;

namespace CUC260905.Tests
{
    public sealed class NetworkConnectionRulesTests
    {
        // ---- SegmentsCross ----

        [Test]
        public void SegmentsCross_CrossingSegments_True()
        {
            Assert.That(NetworkConnectionRules.SegmentsCross(
                new Vector2(0.0f, 0.0f), new Vector2(1.0f, 1.0f),
                new Vector2(0.0f, 1.0f), new Vector2(1.0f, 0.0f)), Is.True);
        }

        [Test]
        public void SegmentsCross_SharedEndpoint_False()
        {
            Assert.That(NetworkConnectionRules.SegmentsCross(
                new Vector2(0.0f, 0.0f), new Vector2(1.0f, 0.0f),
                new Vector2(1.0f, 0.0f), new Vector2(1.0f, 1.0f)), Is.False);
        }

        [Test]
        public void SegmentsCross_EndpointTouchesInterior_False()
        {
            // B 的端点恰好落在 A 的内部：不视为“内部交叉”。
            Assert.That(NetworkConnectionRules.SegmentsCross(
                new Vector2(0.0f, 0.0f), new Vector2(1.0f, 1.0f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1.0f)), Is.False);
        }

        [Test]
        public void SegmentsCross_Disjoint_False()
        {
            Assert.That(NetworkConnectionRules.SegmentsCross(
                new Vector2(0.0f, 0.0f), new Vector2(1.0f, 0.0f),
                new Vector2(2.0f, 2.0f), new Vector2(3.0f, 3.0f)), Is.False);
        }

        [Test]
        public void SegmentsCross_Parallel_False()
        {
            Assert.That(NetworkConnectionRules.SegmentsCross(
                new Vector2(0.0f, 0.0f), new Vector2(1.0f, 0.0f),
                new Vector2(0.0f, 1.0f), new Vector2(1.0f, 1.0f)), Is.False);
        }

        [Test]
        public void SegmentsCross_CollinearOverlap_False()
        {
            Assert.That(NetworkConnectionRules.SegmentsCross(
                new Vector2(0.0f, 0.0f), new Vector2(2.0f, 0.0f),
                new Vector2(1.0f, 0.0f), new Vector2(3.0f, 0.0f)), Is.False);
        }

        // ---- CheckCrossing ----

        [Test]
        public void CheckCrossing_NoExisting_ReturnsSuccess()
        {
            Assert.That(NetworkConnectionRules.CheckCrossing(
                new Vector2(0.0f, 0.0f),
                new Vector2(1.0f, 1.0f),
                new List<NetworkEdgeSegment>()), Is.EqualTo(ConnectionVerdict.Success));
        }

        [Test]
        public void CheckCrossing_CrossesExisting_ReturnsCrossingEdge()
        {
            var existing = new List<NetworkEdgeSegment>
            {
                new NetworkEdgeSegment(new Vector2(0.0f, 1.0f), new Vector2(1.0f, 0.0f))
            };
            Assert.That(NetworkConnectionRules.CheckCrossing(
                new Vector2(0.0f, 0.0f),
                new Vector2(1.0f, 1.0f),
                existing), Is.EqualTo(ConnectionVerdict.CrossingEdge));
        }

        [Test]
        public void CheckCrossing_AdjacentSharedEndpoint_ReturnsSuccess()
        {
            var existing = new List<NetworkEdgeSegment>
            {
                new NetworkEdgeSegment(new Vector2(1.0f, 0.0f), new Vector2(1.0f, 1.0f))
            };
            Assert.That(NetworkConnectionRules.CheckCrossing(
                new Vector2(0.0f, 0.0f),
                new Vector2(1.0f, 0.0f),
                existing), Is.EqualTo(ConnectionVerdict.Success));
        }

        // ---- ValidateRoles ----

        [Test]
        public void ValidateRoles_UserToUser_Forbidden()
        {
            NodeDescriptor first = new NodeDescriptor("u1", NetworkNodeRole.User, "U1");
            NodeDescriptor second = new NodeDescriptor("u2", NetworkNodeRole.User, "U2");
            Assert.That(NetworkConnectionRules.ValidateRoles(first, second),
                Is.EqualTo(ConnectionVerdict.UserToUserForbidden));
        }

        [Test]
        public void ValidateRoles_UserToServer_Allowed()
        {
            NodeDescriptor user = new NodeDescriptor("u1", NetworkNodeRole.User, "U1");
            NodeDescriptor server = new NodeDescriptor("s1", NetworkNodeRole.Server, "S1");
            Assert.That(NetworkConnectionRules.ValidateRoles(user, server),
                Is.EqualTo(ConnectionVerdict.Success));
            Assert.That(NetworkConnectionRules.ValidateRoles(server, user),
                Is.EqualTo(ConnectionVerdict.Success));
        }

        [Test]
        public void ValidateRoles_ServerToServer_Allowed()
        {
            NodeDescriptor first = new NodeDescriptor("s1", NetworkNodeRole.Server, "S1");
            NodeDescriptor second = new NodeDescriptor("s2", NetworkNodeRole.Server, "S2");
            Assert.That(NetworkConnectionRules.ValidateRoles(first, second),
                Is.EqualTo(ConnectionVerdict.Success));
        }

        // ---- ValidateConnectionCapacity ----

        [Test]
        public void ValidateConnectionCapacity_BelowLimit_Success()
        {
            // 当前 0 条边，上限 3：+1 后为 1，未超限。
            Assert.That(NetworkConnectionRules.ValidateConnectionCapacity(0, 3),
                Is.EqualTo(ConnectionVerdict.Success));
        }

        [Test]
        public void ValidateConnectionCapacity_ExactlyAtLimit_Success()
        {
            // 当前 2 条边，上限 3：+1 后恰好等于上限，放行。
            Assert.That(NetworkConnectionRules.ValidateConnectionCapacity(2, 3),
                Is.EqualTo(ConnectionVerdict.Success));
        }

        [Test]
        public void ValidateConnectionCapacity_ExceedsLimit_MaxConnectionsExceeded()
        {
            // 当前 3 条边，上限 3：+1 后为 4，超限拒绝。
            Assert.That(NetworkConnectionRules.ValidateConnectionCapacity(3, 3),
                Is.EqualTo(ConnectionVerdict.MaxConnectionsExceeded));
        }

        [Test]
        public void ValidateConnectionCapacity_NonPositiveMax_Unlimited()
        {
            // 0 与负值均表示未配置/无限。
            Assert.That(NetworkConnectionRules.ValidateConnectionCapacity(10, 0),
                Is.EqualTo(ConnectionVerdict.Success));
            Assert.That(NetworkConnectionRules.ValidateConnectionCapacity(10, -1),
                Is.EqualTo(ConnectionVerdict.Success));
        }
    }
}
