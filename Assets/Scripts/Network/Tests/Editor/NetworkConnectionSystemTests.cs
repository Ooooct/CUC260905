using System;
using System.Collections.Generic;
using CUC260905.Network;
using NUnit.Framework;
using QFramework;
using UnityEngine;

namespace CUC260905.Tests
{
    public sealed class NetworkConnectionSystemTests
    {
        private NetworkTopologyModel mModel;
        private FakePositionProvider mPositions;
        private INetworkTopologySystem mTopologySystem;
        private INetworkConnectionSystem mConnectionSystem;

        [SetUp]
        public void SetUp()
        {
            NetworkConnectionTestArchitecture.Reset();
            mModel = new NetworkTopologyModel();
            mPositions = new FakePositionProvider();
            NetworkConnectionTestArchitecture.Configure(mModel, mPositions);

            mTopologySystem = NetworkConnectionTestArchitecture.Interface.GetSystem<INetworkTopologySystem>();
            mConnectionSystem = NetworkConnectionTestArchitecture.Interface.GetSystem<INetworkConnectionSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            NetworkConnectionTestArchitecture.Reset();
        }

        [Test]
        public void TryConnect_Servers_SuccessAndWritesEdge()
        {
            RegisterServer("s1", "S1", new Vector3(0.0f, 0.0f, 0.0f));
            RegisterServer("s2", "S2", new Vector3(2.0f, 0.0f, 0.0f));

            Assert.That(mConnectionSystem.TryConnect("s1", "s2"), Is.EqualTo(ConnectionVerdict.Success));
            Assert.That(mModel.TryGetEdge("s1", "s2", out NetworkEdge edge), Is.True);
            Assert.That(mModel.GetConnectedNodeIds("s1"), Does.Contain("s2"));
            Assert.That(mModel.GetConnectedNodeIds("s2"), Does.Contain("s1"));
        }

        [Test]
        public void TryConnect_UserToServer_Success()
        {
            RegisterUser("u1", "U1", new Vector3(0.0f, 0.0f, 0.0f));
            RegisterServer("s1", "S1", new Vector3(2.0f, 0.0f, 0.0f));

            Assert.That(mConnectionSystem.TryConnect("u1", "s1"), Is.EqualTo(ConnectionVerdict.Success));
            Assert.That(mModel.TryGetEdge("u1", "s1", out _), Is.True);
        }

        [Test]
        public void TryConnect_UserToUser_Forbidden()
        {
            RegisterUser("u1", "U1", new Vector3(0.0f, 0.0f, 0.0f));
            RegisterUser("u2", "U2", new Vector3(2.0f, 0.0f, 0.0f));

            Assert.That(mConnectionSystem.TryConnect("u1", "u2"), Is.EqualTo(ConnectionVerdict.UserToUserForbidden));
            Assert.That(mModel.TryGetEdge("u1", "u2", out _), Is.False);
        }

        [Test]
        public void TryConnect_SameNode_SameNode()
        {
            RegisterServer("s1", "S1", new Vector3(0.0f, 0.0f, 0.0f));

            Assert.That(mConnectionSystem.TryConnect("s1", "s1"), Is.EqualTo(ConnectionVerdict.SameNode));
            Assert.That(mModel.GetConnectedNodeIds("s1"), Is.Empty);
        }

        [Test]
        public void TryConnect_UnregisteredNode_NodeNotRegistered()
        {
            RegisterServer("s1", "S1", new Vector3(0.0f, 0.0f, 0.0f));

            Assert.That(mConnectionSystem.TryConnect("s1", "ghost"), Is.EqualTo(ConnectionVerdict.NodeNotRegistered));
            Assert.That(mConnectionSystem.TryConnect("ghost", "s1"), Is.EqualTo(ConnectionVerdict.NodeNotRegistered));
        }

        [Test]
        public void TryConnect_AlreadyConnected_AlreadyConnected()
        {
            RegisterServer("s1", "S1", new Vector3(0.0f, 0.0f, 0.0f));
            RegisterServer("s2", "S2", new Vector3(2.0f, 0.0f, 0.0f));

            Assert.That(mConnectionSystem.TryConnect("s1", "s2"), Is.EqualTo(ConnectionVerdict.Success));
            Assert.That(mConnectionSystem.TryConnect("s1", "s2"), Is.EqualTo(ConnectionVerdict.AlreadyConnected));
            Assert.That(mConnectionSystem.TryConnect("s2", "s1"), Is.EqualTo(ConnectionVerdict.AlreadyConnected));
        }

        [Test]
        public void TryConnect_CrossingExistingEdge_CrossingEdge()
        {
            // 对角 A-B 已存在；另一条对角 C-D 与之内部相交。
            RegisterServer("a", "A", new Vector3(0.0f, 0.0f, 0.0f));
            RegisterServer("b", "B", new Vector3(2.0f, 2.0f, 0.0f));
            RegisterServer("c", "C", new Vector3(0.0f, 2.0f, 0.0f));
            RegisterServer("d", "D", new Vector3(2.0f, 0.0f, 0.0f));

            Assert.That(mConnectionSystem.TryConnect("a", "b"), Is.EqualTo(ConnectionVerdict.Success));
            Assert.That(mConnectionSystem.TryConnect("c", "d"), Is.EqualTo(ConnectionVerdict.CrossingEdge));
            Assert.That(mModel.TryGetEdge("c", "d", out _), Is.False);
        }

        [Test]
        public void TryConnect_AdjacentSharedEndpoint_Allowed()
        {
            // 新边 A-C 与既有边 A-B 共享端点 A，不属于交叉。
            RegisterServer("a", "A", new Vector3(0.0f, 0.0f, 0.0f));
            RegisterServer("b", "B", new Vector3(2.0f, 0.0f, 0.0f));
            RegisterServer("c", "C", new Vector3(0.0f, 2.0f, 0.0f));

            Assert.That(mConnectionSystem.TryConnect("a", "b"), Is.EqualTo(ConnectionVerdict.Success));
            Assert.That(mConnectionSystem.TryConnect("a", "c"), Is.EqualTo(ConnectionVerdict.Success));
            Assert.That(mModel.TryGetEdge("a", "c", out _), Is.True);
        }

        [Test]
        public void TryConnect_MissingPosition_NodePositionUnavailable()
        {
            // 节点已登记但位置来源缺失：无法做交叉检查，返回不可用而非写入。
            RegisterServer("s1", "S1", new Vector3(0.0f, 0.0f, 0.0f));
            mTopologySystem.Register(new NodeDescriptor("s2", NetworkNodeRole.Server, "S2"));

            Assert.That(mConnectionSystem.TryConnect("s1", "s2"), Is.EqualTo(ConnectionVerdict.NodePositionUnavailable));
            Assert.That(mModel.TryGetEdge("s1", "s2", out _), Is.False);
        }

        [Test]
        public void Disconnect_RemovesEdgeAndAllowsReconnect()
        {
            // 右键取消连线走 SetConnected(false)：契约是删除边并可重新连接。
            RegisterServer("s1", "S1", new Vector3(0.0f, 0.0f, 0.0f));
            RegisterServer("s2", "S2", new Vector3(2.0f, 0.0f, 0.0f));
            Assert.That(mConnectionSystem.TryConnect("s1", "s2"), Is.EqualTo(ConnectionVerdict.Success));

            Assert.That(mTopologySystem.SetConnected("s1", "s2", false),
                Is.EqualTo(NetworkTopologyResult.Success));
            Assert.That(mModel.TryGetEdge("s1", "s2", out _), Is.False);
            Assert.That(mModel.GetConnectedNodeIds("s1"), Is.Empty);
            Assert.That(mModel.GetConnectedNodeIds("s2"), Is.Empty);

            Assert.That(mConnectionSystem.TryConnect("s1", "s2"), Is.EqualTo(ConnectionVerdict.Success));
            Assert.That(mModel.TryGetEdge("s1", "s2", out _), Is.True);
        }

        [Test]
        public void TryConnect_MaxConnections_SaturatedEndpointRejected()
        {
            // s1 上限 1：第一条连线成功，第二条（再连一个端点）被拒绝。
            RegisterServer("s1", "S1", new Vector3(0.0f, 0.0f, 0.0f), 1);
            RegisterServer("s2", "S2", new Vector3(2.0f, 0.0f, 0.0f));
            RegisterServer("s3", "S3", new Vector3(0.0f, 2.0f, 0.0f));

            Assert.That(mConnectionSystem.TryConnect("s1", "s2"), Is.EqualTo(ConnectionVerdict.Success));
            Assert.That(mConnectionSystem.TryConnect("s1", "s3"), Is.EqualTo(ConnectionVerdict.MaxConnectionsExceeded));
            Assert.That(mModel.TryGetEdge("s1", "s3", out _), Is.False);

            // 无上限端点（s2）继续连线不受影响。
            Assert.That(mConnectionSystem.TryConnect("s2", "s3"), Is.EqualTo(ConnectionVerdict.Success));
        }

        [Test]
        public void TryConnect_MaxConnections_SecondEndpointAlsoChecked()
        {
            // s2 上限 1 且已连 s3；再连 s1 时，s2 端点超限被拒绝。
            RegisterServer("s1", "S1", new Vector3(0.0f, 0.0f, 0.0f));
            RegisterServer("s2", "S2", new Vector3(2.0f, 0.0f, 0.0f), 1);
            RegisterServer("s3", "S3", new Vector3(0.0f, 2.0f, 0.0f));

            Assert.That(mConnectionSystem.TryConnect("s2", "s3"), Is.EqualTo(ConnectionVerdict.Success));
            Assert.That(mConnectionSystem.TryConnect("s1", "s2"), Is.EqualTo(ConnectionVerdict.MaxConnectionsExceeded));
            Assert.That(mModel.TryGetEdge("s1", "s2", out _), Is.False);
        }

        [Test]
        public void TryConnect_MaxConnections_AllowsUpToLimit()
        {
            // s1 上限 2：前两条成功，第三条被拒绝。
            RegisterServer("s1", "S1", new Vector3(0.0f, 0.0f, 0.0f), 2);
            RegisterServer("s2", "S2", new Vector3(2.0f, 0.0f, 0.0f));
            RegisterServer("s3", "S3", new Vector3(0.0f, 2.0f, 0.0f));
            RegisterServer("s4", "S4", new Vector3(-2.0f, 0.0f, 0.0f));

            Assert.That(mConnectionSystem.TryConnect("s1", "s2"), Is.EqualTo(ConnectionVerdict.Success));
            Assert.That(mConnectionSystem.TryConnect("s1", "s3"), Is.EqualTo(ConnectionVerdict.Success));
            Assert.That(mConnectionSystem.TryConnect("s1", "s4"), Is.EqualTo(ConnectionVerdict.MaxConnectionsExceeded));
            Assert.That(mModel.TryGetEdge("s1", "s2", out _), Is.True);
            Assert.That(mModel.TryGetEdge("s1", "s3", out _), Is.True);
            Assert.That(mModel.TryGetEdge("s1", "s4", out _), Is.False);
        }

        [Test]
        public void TryConnect_MaxConnections_ZeroMeansUnlimited()
        {
            // MaxConnections = 0 表示未配置/无限，可任意连线。
            RegisterServer("s1", "S1", new Vector3(0.0f, 0.0f, 0.0f), 0);
            RegisterServer("s2", "S2", new Vector3(2.0f, 0.0f, 0.0f));
            RegisterServer("s3", "S3", new Vector3(0.0f, 2.0f, 0.0f));
            RegisterServer("s4", "S4", new Vector3(-2.0f, 0.0f, 0.0f));

            Assert.That(mConnectionSystem.TryConnect("s1", "s2"), Is.EqualTo(ConnectionVerdict.Success));
            Assert.That(mConnectionSystem.TryConnect("s1", "s3"), Is.EqualTo(ConnectionVerdict.Success));
            Assert.That(mConnectionSystem.TryConnect("s1", "s4"), Is.EqualTo(ConnectionVerdict.Success));
        }

        [Test]
        public void TryConnect_MaxConnections_ServerWithoutCapabilities_Unlimited()
        {
            // 未携带能力档案的服务器：视为无上限，可反复连线。
            RegisterServer("s1", "S1", new Vector3(0.0f, 0.0f, 0.0f));
            RegisterServer("s2", "S2", new Vector3(2.0f, 0.0f, 0.0f));
            RegisterServer("s3", "S3", new Vector3(0.0f, 2.0f, 0.0f));
            RegisterServer("s4", "S4", new Vector3(-2.0f, 0.0f, 0.0f));

            Assert.That(mConnectionSystem.TryConnect("s1", "s2"), Is.EqualTo(ConnectionVerdict.Success));
            Assert.That(mConnectionSystem.TryConnect("s1", "s3"), Is.EqualTo(ConnectionVerdict.Success));
            Assert.That(mConnectionSystem.TryConnect("s1", "s4"), Is.EqualTo(ConnectionVerdict.Success));
        }

        private void RegisterServer(string nodeId, string displayName, Vector3 position)
        {
            Assert.That(mTopologySystem.Register(new NodeDescriptor(nodeId, NetworkNodeRole.Server, displayName)),
                Is.EqualTo(NetworkTopologyResult.Success));
            mPositions.Positions[nodeId] = position;
        }

        private void RegisterServer(string nodeId, string displayName, Vector3 position, int maxConnections)
        {
            Assert.That(mTopologySystem.Register(
                new NodeDescriptor(nodeId, NetworkNodeRole.Server, displayName),
                new ServerNodeCapabilities(0f, maxConnections)),
                Is.EqualTo(NetworkTopologyResult.Success));
            mPositions.Positions[nodeId] = position;
        }

        private void RegisterUser(string nodeId, string displayName, Vector3 position)
        {
            Assert.That(mTopologySystem.Register(new NodeDescriptor(nodeId, NetworkNodeRole.User, displayName)),
                Is.EqualTo(NetworkTopologyResult.Success));
            mPositions.Positions[nodeId] = position;
        }

        private sealed class FakePositionProvider : INodePositionProvider
        {
            public readonly Dictionary<string, Vector3> Positions =
                new Dictionary<string, Vector3>(StringComparer.Ordinal);

            public bool TryGetNodePosition(string nodeId, out Vector3 position)
            {
                return Positions.TryGetValue(nodeId, out position);
            }
        }

        private sealed class NetworkConnectionTestArchitecture : Architecture<NetworkConnectionTestArchitecture>
        {
            private static NetworkTopologyModel sModel;
            private static FakePositionProvider sPositions;

            public static void Configure(NetworkTopologyModel model, FakePositionProvider positions)
            {
                sModel = model;
                sPositions = positions;
            }

            public static void Reset()
            {
                if (mArchitecture != null)
                {
                    mArchitecture.Deinit();
                }

                sModel = null;
                sPositions = null;
            }

            protected override void Init()
            {
                RegisterModel<INetworkTopologyModel>(sModel);
                RegisterUtility<INodePositionProvider>(sPositions);

                NetworkTopologySystem topologySystem = new NetworkTopologySystem(sModel);
                RegisterSystem<INetworkTopologySystem>(topologySystem);
                RegisterSystem<INetworkConnectionSystem>(
                    new NetworkConnectionSystem(sModel, topologySystem));
            }
        }
    }
}
