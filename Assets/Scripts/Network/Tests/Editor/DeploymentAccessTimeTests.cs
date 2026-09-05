using System.Collections.Generic;
using CUC260905.Message;
using NUnit.Framework;
using QFramework;

namespace CUC260905.Network.Tests
{
    /// <summary>
    /// 部署接入时间：用户节点在全局统一的接入时长内既不能发送（作为数据包源）、
    /// 也不能被作为接收目标（数据包目的地）。服务器节点没有接入门控，始终可用。
    /// 覆盖 INetworkTopologyModel 暴露的查询 API 与 PacketTrafficSystem 的门控行为。
    /// </summary>
    public sealed class DeploymentAccessTimeTests
    {
        private const float AccessTimeSeconds = 2f;

        private NetworkTopologyModel mModel;
        private INetworkTopologySystem mTopologySystem;
        private IMessageSystem mMessageSystem;
        private IPacketTrafficSystem mTrafficSystem;

        [SetUp]
        public void SetUp()
        {
            mModel = new NetworkTopologyModel(AccessTimeSeconds);
            AccessTimeTestArchitecture.Configure(mModel);
            mTopologySystem = AccessTimeTestArchitecture.Interface.GetSystem<INetworkTopologySystem>();
            mMessageSystem = AccessTimeTestArchitecture.Interface.GetSystem<IMessageSystem>();
            mTrafficSystem = AccessTimeTestArchitecture.Interface.GetSystem<IPacketTrafficSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            AccessTimeTestArchitecture.Reset();
        }

        [Test]
        public void Model_ExposesConfiguredDeploymentAccessTime()
        {
            Assert.That(mModel.DeploymentAccessTime, Is.EqualTo(AccessTimeSeconds));
        }

        [Test]
        public void DefaultDeploymentAccessTime_IsTenSeconds()
        {
            Assert.That(NetworkTopologyModel.DefaultDeploymentAccessTime, Is.EqualTo(10f));
        }

        [Test]
        public void IsDeploymentAccessComplete_FlipsAfterAccessDurationElapsed()
        {
            RegisterUser("user-a", deployedAt: 10d);

            Assert.That(mModel.IsDeploymentAccessComplete("user-a", 11.9d), Is.False);
            Assert.That(mModel.IsDeploymentAccessComplete("user-a", 12d), Is.True);
            Assert.That(mModel.IsDeploymentAccessComplete("user-a", 20d), Is.True);
        }

        [Test]
        public void TryGetDeploymentAccessRemaining_ReportsCountdown()
        {
            RegisterUser("user-a", deployedAt: 10d);

            Assert.That(mModel.TryGetDeploymentAccessRemaining("user-a", 10d, out float remainingAtStart), Is.True);
            Assert.That(remainingAtStart, Is.EqualTo(AccessTimeSeconds));

            Assert.That(mModel.TryGetDeploymentAccessRemaining("user-a", 11d, out float remainingHalfway), Is.True);
            Assert.That(remainingHalfway, Is.EqualTo(1f));

            Assert.That(mModel.TryGetDeploymentAccessRemaining("user-a", 12d, out float remainingDone), Is.True);
            Assert.That(remainingDone, Is.EqualTo(0f));

            Assert.That(mModel.TryGetDeploymentAccessRemaining("user-a", 30d, out float remainingLater), Is.True);
            Assert.That(remainingLater, Is.EqualTo(0f));
        }

        [Test]
        public void ServerNode_HasNoDeploymentAccessGate()
        {
            RegisterServer("server-a", 10f, deployedAt: 50d);

            Assert.That(mModel.IsDeploymentAccessComplete("server-a", 0d), Is.True);
            Assert.That(mModel.TryGetDeploymentAccessRemaining("server-a", 0d, out float remaining), Is.True);
            Assert.That(remaining, Is.EqualTo(0f));
        }

        [Test]
        public void UnregisteredNode_IsNotDeploymentAccessComplete()
        {
            Assert.That(mModel.IsDeploymentAccessComplete("missing", 0d), Is.False);
            Assert.That(mModel.TryGetDeploymentAccessRemaining("missing", 0d, out _), Is.False);
        }

        [Test]
        public void SendPacket_BeforeSourceAccessible_ReturnsSourceNotAccessible()
        {
            RegisterUser("user-a", deployedAt: 10d);
            RegisterUser("user-b", deployedAt: 0d);
            RegisterServer("server-a", 100f, deployedAt: 0d);
            Connect("user-a", "server-a");
            Connect("server-a", "user-b");

            PacketTransmissionResult result = mTrafficSystem.SendPacket(
                "user-a", "user-b", 4f, 1f, "MainTerminal", 10.5d);

            Assert.That(result, Is.EqualTo(PacketTransmissionResult.SourceNotAccessible));
            Assert.That(GetLoad("server-a"), Is.EqualTo(0f));
        }

        [Test]
        public void SendPacket_BeforeDestinationAccessible_ReturnsDestinationNotAccessible()
        {
            RegisterUser("user-a", deployedAt: 0d);
            RegisterUser("user-b", deployedAt: 10d);
            RegisterServer("server-a", 100f, deployedAt: 0d);
            Connect("user-a", "server-a");
            Connect("server-a", "user-b");

            PacketTransmissionResult result = mTrafficSystem.SendPacket(
                "user-a", "user-b", 4f, 1f, "MainTerminal", 10.5d);

            Assert.That(result, Is.EqualTo(PacketTransmissionResult.DestinationNotAccessible));
            Assert.That(GetLoad("server-a"), Is.EqualTo(0f));
        }

        [Test]
        public void SendPacket_AfterBothAccessible_Succeeds()
        {
            RegisterUser("user-a", deployedAt: 0d);
            RegisterUser("user-b", deployedAt: 0d);
            RegisterServer("server-a", 100f, deployedAt: 0d);
            Connect("user-a", "server-a");
            Connect("server-a", "user-b");

            PacketTransmissionResult result = mTrafficSystem.SendPacket(
                "user-a", "user-b", 4f, 1f, "MainTerminal", 10d);

            Assert.That(result, Is.EqualTo(PacketTransmissionResult.Success));
            Assert.That(GetLoad("server-a"), Is.EqualTo(4f));
        }

        [Test]
        public void SendRandomPacket_OnlyTargetsAccessibleNodes()
        {
            RegisterUser("user-a", deployedAt: 0d);
            RegisterUser("user-b", deployedAt: 0d);
            RegisterUser("user-c", deployedAt: 100d);
            RegisterServer("server-a", 1000f, deployedAt: 0d);
            Connect("user-a", "server-a");
            Connect("server-a", "user-b");
            Connect("server-a", "user-c");

            List<PacketTransmittedEvent> sent = new List<PacketTransmittedEvent>();
            IUnRegister registration = AccessTimeTestArchitecture.Interface.RegisterEvent<PacketTransmittedEvent>(sent.Add);

            for (int seed = 0; seed < 32; seed++)
            {
                PacketTransmissionResult result = mTrafficSystem.SendRandomPacket(
                    "user-a", 4f, 1f, "MainTerminal", 50d, new System.Random(seed));
                Assert.That(result, Is.EqualTo(PacketTransmissionResult.Success));
            }

            registration.UnRegister();
            Assert.That(sent, Has.Count.EqualTo(32));
            foreach (PacketTransmittedEvent packet in sent)
            {
                Assert.That(packet.DestinationNodeId, Is.EqualTo("user-b"));
            }
        }

        [Test]
        public void SendRandomPacket_WhenOnlyInaccessibleTargets_ReturnsDestinationUnavailableSilently()
        {
            RegisterUser("user-a", deployedAt: 0d);
            RegisterUser("user-b", deployedAt: 100d);
            RegisterServer("server-a", 1000f, deployedAt: 0d);
            Connect("user-a", "server-a");
            Connect("server-a", "user-b");
            List<PacketUnreachableEvent> unreachable = new List<PacketUnreachableEvent>();
            IUnRegister registration = AccessTimeTestArchitecture.Interface.RegisterEvent<PacketUnreachableEvent>(unreachable.Add);

            PacketTransmissionResult result = mTrafficSystem.SendRandomPacket(
                "user-a", 4f, 1f, "MainTerminal", 50d, new System.Random(7));

            Assert.That(result, Is.EqualTo(PacketTransmissionResult.DestinationUnavailable));
            Assert.That(mMessageSystem.GetHistory("MainTerminal"), Is.Empty);
            Assert.That(unreachable, Is.Empty);
            registration.UnRegister();
        }

        private void RegisterUser(string nodeId, double deployedAt)
        {
            Assert.That(
                mTopologySystem.Register(
                    new NodeDescriptor(nodeId, NetworkNodeRole.User, nodeId),
                    deployedAt: deployedAt),
                Is.EqualTo(NetworkTopologyResult.Success));
        }

        private void RegisterServer(string nodeId, float capacity, double deployedAt)
        {
            Assert.That(
                mTopologySystem.Register(
                    new NodeDescriptor(nodeId, NetworkNodeRole.Server, nodeId),
                    new ServerNodeCapabilities(capacity, 0),
                    deployedAt),
                Is.EqualTo(NetworkTopologyResult.Success));
        }

        private void Connect(string firstNodeId, string secondNodeId)
        {
            Assert.That(
                mTopologySystem.SetConnected(firstNodeId, secondNodeId, true),
                Is.EqualTo(NetworkTopologyResult.Success));
        }

        private float GetLoad(string nodeId)
        {
            Assert.That(mModel.TryGetServerCapabilities(nodeId, out ServerNodeCapabilities capabilities), Is.True);
            return capabilities.CurrentDataLoadPerSecond.Value;
        }

        private sealed class AccessTimeTestArchitecture : Architecture<AccessTimeTestArchitecture>
        {
            private static NetworkTopologyModel sModel;

            public static void Configure(NetworkTopologyModel model)
            {
                sModel = model;
            }

            public static void Reset()
            {
                if (mArchitecture != null)
                {
                    mArchitecture.Deinit();
                }

                sModel = null;
            }

            protected override void Init()
            {
                MessageSystem messageSystem = new MessageSystem();
                INetworkTopologySystem topologySystem = new NetworkTopologySystem(sModel);
                RegisterModel<INetworkTopologyModel>(sModel);
                RegisterSystem<IMessageSystem>(messageSystem);
                RegisterSystem<INetworkTopologySystem>(topologySystem);
                RegisterSystem<IPacketTrafficSystem>(new PacketTrafficSystem(sModel, messageSystem));
            }
        }
    }
}
