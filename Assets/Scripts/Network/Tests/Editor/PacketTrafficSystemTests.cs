using System.Collections.Generic;
using CUC260905.Message;
using NUnit.Framework;
using QFramework;

namespace CUC260905.Network.Tests
{
    public sealed class PacketTrafficSystemTests
    {
        private NetworkTopologyModel mModel;
        private INetworkTopologySystem mTopologySystem;
        private IMessageSystem mMessageSystem;
        private IPacketTrafficSystem mTrafficSystem;

        [SetUp]
        public void SetUp()
        {
            mModel = new NetworkTopologyModel();
            TrafficTestArchitecture.Configure(mModel);
            mTopologySystem = TrafficTestArchitecture.Interface.GetSystem<INetworkTopologySystem>();
            mMessageSystem = TrafficTestArchitecture.Interface.GetSystem<IMessageSystem>();
            mTrafficSystem = TrafficTestArchitecture.Interface.GetSystem<IPacketTrafficSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            TrafficTestArchitecture.Reset();
        }

        [Test]
        public void SendPacket_ReservesEveryServerOnPath_AndExpiresAfterOneSecond()
        {
            RegisterUser("user-a");
            RegisterUser("user-b");
            RegisterServer("server-a", 10f);
            RegisterServer("server-b", 10f);
            Connect("user-a", "server-a");
            Connect("server-a", "server-b");
            Connect("server-b", "user-b");

            PacketTransmissionResult result = mTrafficSystem.SendPacket(
                "user-a", "user-b", 6f, 2f, "MainTerminal", 10d);

            Assert.That(result, Is.EqualTo(PacketTransmissionResult.Success));
            Assert.That(GetLoad("server-a"), Is.EqualTo(6f));
            Assert.That(GetLoad("server-b"), Is.EqualTo(6f));

            mTrafficSystem.Tick(10.999d);
            Assert.That(GetLoad("server-a"), Is.EqualTo(6f));
            mTrafficSystem.Tick(11d);
            Assert.That(GetLoad("server-a"), Is.EqualTo(0f));
            Assert.That(GetLoad("server-b"), Is.EqualTo(0f));
        }

        [Test]
        public void SendPacket_PrefersLessLoadedValidPath()
        {
            RegisterUser("user-a");
            RegisterUser("user-b");
            RegisterUser("user-c");
            RegisterServer("server-a", 10f);
            RegisterServer("server-b", 10f);
            RegisterServer("server-c", 10f);
            Connect("user-a", "server-a");
            Connect("user-a", "server-b");
            Connect("server-a", "server-c");
            Connect("server-b", "server-c");
            Connect("server-a", "user-b");
            Connect("server-c", "user-c");
            mTrafficSystem.SendPacket("user-a", "user-b", 7f, 0f, "MainTerminal", 5d);

            List<PacketTransmittedEvent> sent = new List<PacketTransmittedEvent>();
            IUnRegister registration = TrafficTestArchitecture.Interface.RegisterEvent<PacketTransmittedEvent>(sent.Add);
            PacketTransmissionResult result = mTrafficSystem.SendPacket(
                "user-a", "user-c", 2f, 8f, "MainTerminal", 5.1d);

            Assert.That(result, Is.EqualTo(PacketTransmissionResult.Success));
            Assert.That(sent, Has.Count.EqualTo(1));
            Assert.That(sent[0].PathNodeIds, Is.EqualTo(new[] { "user-a", "server-b", "server-c", "user-c" }));
            registration.UnRegister();
        }

        [Test]
        public void SendPacket_WhenAllRoutesExceedThroughput_PublishesUnreachableMessage()
        {
            RegisterUser("user-a");
            RegisterUser("user-b");
            RegisterServer("server-a", 5f);
            Connect("user-a", "server-a");
            Connect("server-a", "user-b");
            List<PacketUnreachableEvent> unreachable = new List<PacketUnreachableEvent>();
            IUnRegister registration = TrafficTestArchitecture.Interface.RegisterEvent<PacketUnreachableEvent>(unreachable.Add);

            PacketTransmissionResult result = mTrafficSystem.SendPacket(
                "user-a", "user-b", 6f, 1f, "MainTerminal", 1d);

            Assert.That(result, Is.EqualTo(PacketTransmissionResult.Unreachable));
            Assert.That(mMessageSystem.GetHistory("MainTerminal"), Has.Count.EqualTo(1));
            Assert.That(unreachable, Has.Count.EqualTo(1));
            Assert.That(unreachable[0].Result, Is.EqualTo(PacketTransmissionResult.Unreachable));
            Assert.That(unreachable[0].ProblemNodeIds, Is.EqualTo(new[] { "server-a" }));
            registration.UnRegister();
        }

        [Test]
        public void SendPacket_ToServerNode_IsRejected()
        {
            RegisterUser("user-a");
            RegisterServer("server-a", 10f);
            Connect("user-a", "server-a");

            PacketTransmissionResult result = mTrafficSystem.SendPacket(
                "user-a", "server-a", 4f, 1f, "MainTerminal", 1d);

            Assert.That(result, Is.EqualTo(PacketTransmissionResult.DestinationNotUserNode));
            Assert.That(GetLoad("server-a"), Is.EqualTo(0f));
        }

        [Test]
        public void SendRandomPacket_WhenOnlyServersExist_ReturnsDestinationUnavailable()
        {
            RegisterUser("user-a");
            RegisterServer("server-a", 10f);
            Connect("user-a", "server-a");
            List<PacketUnreachableEvent> unreachable = new List<PacketUnreachableEvent>();
            IUnRegister registration = TrafficTestArchitecture.Interface.RegisterEvent<PacketUnreachableEvent>(unreachable.Add);

            PacketTransmissionResult result = mTrafficSystem.SendRandomPacket(
                "user-a", 4f, 1f, "MainTerminal", 1d, new System.Random(123));

            // 没有其他用户节点时属于"暂无发送目标"：返回结果但不发布消息与事件
            // （否则单节点场景会反复报不可达并累计总体负载惩罚）。
            Assert.That(result, Is.EqualTo(PacketTransmissionResult.DestinationUnavailable));
            Assert.That(mMessageSystem.GetHistory("MainTerminal"), Is.Empty);
            Assert.That(unreachable, Is.Empty);
            registration.UnRegister();
        }

        [Test]
        public void SendRandomPacket_TargetsAnotherUserNode()
        {
            RegisterUser("user-a");
            RegisterUser("user-b");
            RegisterServer("server-a", 100f);
            Connect("user-a", "server-a");
            Connect("server-a", "user-b");
            List<PacketTransmittedEvent> sent = new List<PacketTransmittedEvent>();
            IUnRegister registration = TrafficTestArchitecture.Interface.RegisterEvent<PacketTransmittedEvent>(sent.Add);

            PacketTransmissionResult result = mTrafficSystem.SendRandomPacket(
                "user-a", 4f, 1f, "MainTerminal", 1d, new System.Random(7));

            Assert.That(result, Is.EqualTo(PacketTransmissionResult.Success));
            Assert.That(sent, Has.Count.EqualTo(1));
            Assert.That(sent[0].SourceNodeId, Is.EqualTo("user-a"));
            Assert.That(sent[0].DestinationNodeId, Is.EqualTo("user-b"));
            registration.UnRegister();
        }

        [Test]
        public void SendPacket_ToSelf_IsRejected()
        {
            RegisterUser("user-a");
            RegisterServer("server-a", 10f);
            Connect("user-a", "server-a");

            PacketTransmissionResult result = mTrafficSystem.SendPacket(
                "user-a", "user-a", 4f, 1f, "MainTerminal", 1d);

            Assert.That(result, Is.EqualTo(PacketTransmissionResult.SelfSendForbidden));
            Assert.That(GetLoad("server-a"), Is.EqualTo(0f));
        }

        [Test]
        public void SendRandomPacket_NeverTargetsSourceNode()
        {
            RegisterUser("user-a");
            RegisterUser("user-b");
            RegisterUser("user-c");
            // 既有修复：容量 100 装不下 64×4=256 的 1 秒窗口负载，会先触发容量拒绝（Unreachable），
            // 偏离本测试"随机发包不命中源节点"的单一意图；抬高到 1000 让路由始终可行。
            RegisterServer("server-a", 1000f);
            Connect("user-a", "server-a");
            Connect("server-a", "user-b");
            Connect("server-a", "user-c");
            List<PacketTransmittedEvent> sent = new List<PacketTransmittedEvent>();
            IUnRegister registration = TrafficTestArchitecture.Interface.RegisterEvent<PacketTransmittedEvent>(sent.Add);

            for (int seed = 0; seed < 64; seed++)
            {
                PacketTransmissionResult result = mTrafficSystem.SendRandomPacket(
                    "user-a", 4f, 1f, "MainTerminal", 1d, new System.Random(seed));
                Assert.That(result, Is.EqualTo(PacketTransmissionResult.Success));
            }

            registration.UnRegister();
            Assert.That(sent, Has.Count.EqualTo(64));
            foreach (PacketTransmittedEvent packet in sent)
            {
                Assert.That(packet.SourceNodeId, Is.EqualTo("user-a"));
                Assert.That(packet.DestinationNodeId, Is.Not.EqualTo("user-a"));
            }
        }

        [Test]
        public void SendRandomPacket_SelectsTargetUniformly()
        {
            // 目标选取为均匀随机（已移除距离加权）：对所有已接入的其他用户节点等概率抽样，
            // 与位置无关；多种子下各候选被选中的次数应接近相等。
            RegisterUser("user-a");
            RegisterUser("user-b");
            RegisterUser("user-c");
            RegisterServer("server-a", 5000f);
            Connect("user-a", "server-a");
            Connect("server-a", "user-b");
            Connect("server-a", "user-c");
            List<PacketTransmittedEvent> sent = new List<PacketTransmittedEvent>();
            IUnRegister registration =
                TrafficTestArchitecture.Interface.RegisterEvent<PacketTransmittedEvent>(sent.Add);

            for (int seed = 0; seed < 1000; seed++)
            {
                PacketTransmissionResult result = mTrafficSystem.SendRandomPacket(
                    "user-a", 4f, 1f, "MainTerminal", 1d, new System.Random(seed));
                Assert.That(result, Is.EqualTo(PacketTransmissionResult.Success));
            }

            registration.UnRegister();

            int countUserB = 0;
            int countUserC = 0;
            foreach (PacketTransmittedEvent packet in sent)
            {
                if (string.Equals(packet.DestinationNodeId, "user-b", System.StringComparison.Ordinal))
                {
                    countUserB = countUserB + 1;
                }
                else if (string.Equals(packet.DestinationNodeId, "user-c", System.StringComparison.Ordinal))
                {
                    countUserC = countUserC + 1;
                }
            }

            Assert.That(sent, Has.Count.EqualTo(1000));
            // 两候选均匀各占约一半（500±N）：宽松对称区间仅验证分布不再按距离偏斜。
            Assert.That(countUserB, Is.InRange(400, 600));
            Assert.That(countUserC, Is.InRange(400, 600));
        }

        private void RegisterUser(string nodeId)
        {
            Assert.That(
                mTopologySystem.Register(new NodeDescriptor(nodeId, NetworkNodeRole.User, nodeId)),
                Is.EqualTo(NetworkTopologyResult.Success));
        }

        private void RegisterServer(string nodeId, float capacity)
        {
            Assert.That(
                mTopologySystem.Register(
                    new NodeDescriptor(nodeId, NetworkNodeRole.Server, nodeId),
                    new ServerNodeCapabilities(capacity, 0)),
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

        private sealed class TrafficTestArchitecture : Architecture<TrafficTestArchitecture>
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
