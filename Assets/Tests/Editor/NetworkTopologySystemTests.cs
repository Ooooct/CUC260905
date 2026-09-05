using System.Collections.Generic;
using CUC260905.Economy;
using CUC260905.Network;
using NUnit.Framework;
using QFramework;
using UnityEngine;

namespace CUC260905.Tests
{
    public sealed class NetworkTopologySystemTests
    {
        private INetworkTopologyModel mModel;
        private INetworkTopologySystem mSystem;
        private IEconomySystem mEconomySystem;
        private IServerUpgradeSystem mUpgradeSystem;

        [SetUp]
        public void SetUp()
        {
            NetworkTopologyTestArchitecture.Reset();
            NetworkTopologyModel model = new NetworkTopologyModel();
            NetworkTopologyTestArchitecture.Configure(model);
            mModel = NetworkTopologyTestArchitecture.Interface.GetModel<INetworkTopologyModel>();
            mSystem = NetworkTopologyTestArchitecture.Interface.GetSystem<INetworkTopologySystem>();
            mEconomySystem = NetworkTopologyTestArchitecture.Interface.GetSystem<IEconomySystem>();
            mUpgradeSystem = NetworkTopologyTestArchitecture.Interface.GetSystem<IServerUpgradeSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            NetworkTopologyTestArchitecture.Reset();
        }

        [Test]
        public void Register_StoresDescriptorAndSendsRegisteredEvent()
        {
            List<NodeRegisteredEvent> events = new List<NodeRegisteredEvent>();
            IUnRegister register = NetworkTopologyTestArchitecture.Interface.RegisterEvent<NodeRegisteredEvent>(
                registeredEvent => events.Add(registeredEvent));
            NodeDescriptor node = Node("user-a", NetworkNodeRole.User);

            NetworkTopologyResult result = mSystem.Register(node);

            Assert.That(result, Is.EqualTo(NetworkTopologyResult.Success));
            Assert.That(mModel.TryGetNode("user-a", out NodeDescriptor stored), Is.True);
            Assert.That(stored, Is.EqualTo(node));
            Assert.That(events, Has.Count.EqualTo(1));
            Assert.That(events[0].Node, Is.EqualTo(node));
            register.UnRegister();
        }

        [Test]
        public void SetConnected_UpdatesBothEndpointsAndSendsOneEvent()
        {
            mSystem.Register(Node("user-a", NetworkNodeRole.User));
            mSystem.Register(Node("server-a", NetworkNodeRole.Server));
            List<NodeConnectivityChangedEvent> events = new List<NodeConnectivityChangedEvent>();
            IUnRegister register = NetworkTopologyTestArchitecture.Interface.RegisterEvent<NodeConnectivityChangedEvent>(
                changedEvent => events.Add(changedEvent));

            NetworkTopologyResult result = mSystem.SetConnected("user-a", "server-a", true);

            Assert.That(result, Is.EqualTo(NetworkTopologyResult.Success));
            Assert.That(mModel.GetConnectedNodeIds("user-a"), Does.Contain("server-a"));
            Assert.That(mModel.GetConnectedNodeIds("server-a"), Does.Contain("user-a"));
            Assert.That(events, Has.Count.EqualTo(1));
            Assert.That(events[0].FirstNodeId, Is.EqualTo("user-a"));
            Assert.That(events[0].SecondNodeId, Is.EqualTo("server-a"));
            Assert.That(events[0].IsConnected, Is.True);
            register.UnRegister();
        }

        [Test]
        public void Unregister_RemovesAllConnectionsBeforeNodeEvent()
        {
            mSystem.Register(Node("user-a", NetworkNodeRole.User));
            mSystem.Register(Node("server-a", NetworkNodeRole.Server));
            mSystem.SetConnected("user-a", "server-a", true);
            List<string> eventOrder = new List<string>();
            IUnRegister connectionRegister = NetworkTopologyTestArchitecture.Interface.RegisterEvent<NodeConnectivityChangedEvent>(
                changedEvent => eventOrder.Add(changedEvent.IsConnected ? "connect" : "disconnect"));
            IUnRegister nodeRegister = NetworkTopologyTestArchitecture.Interface.RegisterEvent<NodeUnregisteredEvent>(
                unregisteredEvent => eventOrder.Add("unregister"));

            NetworkTopologyResult result = mSystem.Unregister("user-a");

            Assert.That(result, Is.EqualTo(NetworkTopologyResult.Success));
            Assert.That(mModel.IsRegistered("user-a"), Is.False);
            Assert.That(mModel.GetConnectedNodeIds("server-a"), Is.Empty);
            Assert.That(eventOrder, Is.EqualTo(new[] { "disconnect", "unregister" }));
            connectionRegister.UnRegister();
            nodeRegister.UnRegister();
        }

        [Test]
        public void InvalidOrRepeatedWrites_DoNotChangeTopologyOrSendEvents()
        {
            NodeDescriptor node = Node("user-a", NetworkNodeRole.User);
            mSystem.Register(node);
            List<NodeConnectivityChangedEvent> events = new List<NodeConnectivityChangedEvent>();
            IUnRegister register = NetworkTopologyTestArchitecture.Interface.RegisterEvent<NodeConnectivityChangedEvent>(
                changedEvent => events.Add(changedEvent));

            NetworkTopologyResult sameRegistration = mSystem.Register(node);
            NetworkTopologyResult duplicateRegistration = mSystem.Register(Node("user-a", NetworkNodeRole.Server));
            NetworkTopologyResult unknownConnection = mSystem.SetConnected("user-a", "server-a", true);
            NetworkTopologyResult selfConnection = mSystem.SetConnected("user-a", "user-a", true);

            Assert.That(sameRegistration, Is.EqualTo(NetworkTopologyResult.NoChange));
            Assert.That(duplicateRegistration, Is.EqualTo(NetworkTopologyResult.DuplicateNodeId));
            Assert.That(unknownConnection, Is.EqualTo(NetworkTopologyResult.NodeNotRegistered));
            Assert.That(selfConnection, Is.EqualTo(NetworkTopologyResult.SameNode));
            Assert.That(events, Is.Empty);
            register.UnRegister();
        }

        [Test]
        public void Register_ServerWithCapabilities_StoresCapabilities()
        {
            ServerNodeCapabilities capabilities = new ServerNodeCapabilities(120f, 8, 3, 1);

            NetworkTopologyResult result = mSystem.Register(
                Node("server-a", NetworkNodeRole.Server), capabilities);

            Assert.That(result, Is.EqualTo(NetworkTopologyResult.Success));
            Assert.That(mModel.TryGetServerCapabilities("server-a", out ServerNodeCapabilities stored), Is.True);
            Assert.That(stored.DataProcessingPerSecond.Value, Is.EqualTo(120f));
            Assert.That(stored.MaxConnections.Value, Is.EqualTo(8));
            Assert.That(stored.DataThroughputLevel.Value, Is.EqualTo(3));
            Assert.That(stored.MaxConnectionsLevel.Value, Is.EqualTo(1));
            Assert.That(stored.Level, Is.EqualTo(3));
            // 值相等：等价的新实例与存储档案相等（含两条轨道等级）。
            Assert.That(stored, Is.EqualTo(new ServerNodeCapabilities(120f, 8, 3, 1)));
            Assert.That(mModel.TryGetServerCapabilities("user-a", out _), Is.False);
        }

        [Test]
        public void Register_InvalidCapabilities_RejectedWithoutSideEffects()
        {
            List<NodeRegisteredEvent> events = new List<NodeRegisteredEvent>();
            IUnRegister register = NetworkTopologyTestArchitecture.Interface.RegisterEvent<NodeRegisteredEvent>(
                registeredEvent => events.Add(registeredEvent));

            NetworkTopologyResult negativeRate = mSystem.Register(
                Node("server-a", NetworkNodeRole.Server), new ServerNodeCapabilities(-1f, 8));
            NetworkTopologyResult negativeEdges = mSystem.Register(
                Node("server-b", NetworkNodeRole.Server), new ServerNodeCapabilities(10f, -1));
            NetworkTopologyResult negativeDataLevel = mSystem.Register(
                Node("server-c", NetworkNodeRole.Server), new ServerNodeCapabilities(10f, 8, -1, 0));
            NetworkTopologyResult negativeConnectionLevel = mSystem.Register(
                Node("server-d", NetworkNodeRole.Server), new ServerNodeCapabilities(10f, 8, 0, -1));

            Assert.That(negativeRate, Is.EqualTo(NetworkTopologyResult.InvalidCapabilities));
            Assert.That(negativeEdges, Is.EqualTo(NetworkTopologyResult.InvalidCapabilities));
            Assert.That(negativeDataLevel, Is.EqualTo(NetworkTopologyResult.InvalidCapabilities));
            Assert.That(negativeConnectionLevel, Is.EqualTo(NetworkTopologyResult.InvalidCapabilities));
            Assert.That(mModel.IsRegistered("server-a"), Is.False);
            Assert.That(mModel.IsRegistered("server-b"), Is.False);
            Assert.That(mModel.IsRegistered("server-c"), Is.False);
            Assert.That(events, Is.Empty);
            register.UnRegister();
        }

        [Test]
        public void Register_CapabilitiesOnUserNode_Rejected()
        {
            NetworkTopologyResult result = mSystem.Register(
                Node("user-a", NetworkNodeRole.User), new ServerNodeCapabilities(120f, 8));

            Assert.That(result, Is.EqualTo(NetworkTopologyResult.InvalidCapabilities));
            Assert.That(mModel.IsRegistered("user-a"), Is.False);
        }

        [Test]
        public void SetConnected_WithSpeed_StoresUndirectedEdge()
        {
            mSystem.Register(Node("user-a", NetworkNodeRole.User));
            mSystem.Register(Node("server-a", NetworkNodeRole.Server));

            NetworkTopologyResult result = mSystem.SetConnected("user-a", "server-a", true, 512f);

            Assert.That(result, Is.EqualTo(NetworkTopologyResult.Success));
            Assert.That(mModel.TryGetEdge("user-a", "server-a", out NetworkEdge edge), Is.True);
            Assert.That(edge.MaxTransmissionSpeed.Value, Is.EqualTo(512f));
            // 无向：反向查询应得到同一条边。
            Assert.That(mModel.TryGetEdge("server-a", "user-a", out NetworkEdge reverse), Is.True);
            Assert.That(reverse, Is.EqualTo(edge));
        }

        [Test]
        public void SetConnected_Disconnect_RemovesEdgeRecord()
        {
            mSystem.Register(Node("user-a", NetworkNodeRole.User));
            mSystem.Register(Node("server-a", NetworkNodeRole.Server));
            mSystem.SetConnected("user-a", "server-a", true, 512f);

            NetworkTopologyResult result = mSystem.SetConnected("user-a", "server-a", false);

            Assert.That(result, Is.EqualTo(NetworkTopologyResult.Success));
            Assert.That(mModel.TryGetEdge("user-a", "server-a", out _), Is.False);
            Assert.That(mModel.GetConnectedNodeIds("user-a"), Is.Empty);
            Assert.That(mModel.GetConnectedNodeIds("server-a"), Is.Empty);
        }

        [Test]
        public void Unregister_RemovesCapabilitiesAndIncidentEdges()
        {
            mSystem.Register(Node("user-a", NetworkNodeRole.User));
            mSystem.Register(Node("server-a", NetworkNodeRole.Server),
                new ServerNodeCapabilities(120f, 8));
            mSystem.SetConnected("user-a", "server-a", true, 512f);

            NetworkTopologyResult result = mSystem.Unregister("server-a");

            Assert.That(result, Is.EqualTo(NetworkTopologyResult.Success));
            Assert.That(mModel.TryGetServerCapabilities("server-a", out _), Is.False);
            Assert.That(mModel.TryGetEdge("user-a", "server-a", out _), Is.False);
            Assert.That(mModel.GetConnectedNodeIds("user-a"), Is.Empty);
        }

        [Test]
        public void BindableProperties_NotifyListenersOnValueChange()
        {
            ServerNodeCapabilities capabilities = new ServerNodeCapabilities(120f, 8, 3, 1);
            mSystem.Register(Node("server-a", NetworkNodeRole.Server), capabilities);
            mSystem.Register(Node("user-a", NetworkNodeRole.User));
            mSystem.SetConnected("user-a", "server-a", true, 512f);

            List<int> dataLevelChanges = new List<int>();
            IUnRegister dataLevelRegister = capabilities.DataThroughputLevel.Register(dataLevelChanges.Add);
            List<int> connectionLevelChanges = new List<int>();
            IUnRegister connectionLevelRegister = capabilities.MaxConnectionsLevel.Register(connectionLevelChanges.Add);
            List<float> speedChanges = new List<float>();
            Assert.That(mModel.TryGetEdge("user-a", "server-a", out NetworkEdge edge), Is.True);
            IUnRegister speedRegister = edge.MaxTransmissionSpeed.Register(speedChanges.Add);

            capabilities.DataThroughputLevel.Value = 5;
            capabilities.MaxConnectionsLevel.Value = 2;
            edge.MaxTransmissionSpeed.Value = 2048f;

            Assert.That(dataLevelChanges, Is.EqualTo(new[] { 5 }));
            Assert.That(connectionLevelChanges, Is.EqualTo(new[] { 2 }));
            Assert.That(speedChanges, Is.EqualTo(new[] { 2048f }));
            // 存储的是同一实例：模型内查询到的档案/边与监听值同步变化。
            Assert.That(mModel.TryGetServerCapabilities("server-a", out ServerNodeCapabilities stored), Is.True);
            Assert.That(stored.DataThroughputLevel.Value, Is.EqualTo(5));
            Assert.That(stored.MaxConnectionsLevel.Value, Is.EqualTo(2));
            dataLevelRegister.UnRegister();
            connectionLevelRegister.UnRegister();
            speedRegister.UnRegister();
        }

        [Test]
        public void UpgradeServer_UpdatesOnlyRequestedTrackAndSendsCompletedEvent()
        {
            ServerNodeCapabilities capabilities = new ServerNodeCapabilities(100f, 4, 0, 0);
            mSystem.Register(Node("server-a", NetworkNodeRole.Server), capabilities);
            ServerUpgradeConfig config = CreateUpgradeConfig();
            List<ServerNodeUpgradedEvent> events = new List<ServerNodeUpgradedEvent>();
            IUnRegister register = NetworkTopologyTestArchitecture.Interface.RegisterEvent<ServerNodeUpgradedEvent>(
                upgradedEvent => events.Add(upgradedEvent));

            NetworkTopologyResult result = mSystem.UpgradeServer(
                "server-a",
                ServerUpgradeTrack.DataThroughput,
                config,
                out UpgradeLevelData appliedData);

            Assert.That(result, Is.EqualTo(NetworkTopologyResult.Success));
            Assert.That(appliedData.AppliedValue, Is.EqualTo(150f));
            Assert.That(appliedData.MoneyCost, Is.EqualTo(100));
            Assert.That(capabilities.DataProcessingPerSecond.Value, Is.EqualTo(150f));
            Assert.That(capabilities.DataThroughputLevel.Value, Is.EqualTo(1));
            Assert.That(capabilities.MaxConnections.Value, Is.EqualTo(4));
            Assert.That(capabilities.MaxConnectionsLevel.Value, Is.EqualTo(0));
            Assert.That(events, Has.Count.EqualTo(1));
            Assert.That(events[0].NodeId, Is.EqualTo("server-a"));
            Assert.That(events[0].Track, Is.EqualTo(ServerUpgradeTrack.DataThroughput));
            Assert.That(events[0].PreviousLevel, Is.EqualTo(0));
            Assert.That(events[0].CurrentLevel, Is.EqualTo(1));
            Assert.That(events[0].Capabilities, Is.SameAs(capabilities));

            register.UnRegister();
            Object.DestroyImmediate(config);
        }

        [Test]
        public void UpgradeServer_RejectsUnavailableOrInvalidRequestsWithoutChangingCapabilities()
        {
            ServerNodeCapabilities capabilities = new ServerNodeCapabilities(100f, 4, 0, 0);
            mSystem.Register(Node("server-a", NetworkNodeRole.Server), capabilities);
            mSystem.Register(Node("user-a", NetworkNodeRole.User));
            ServerUpgradeConfig config = CreateUpgradeConfig();

            NetworkTopologyResult userResult = mSystem.UpgradeServer(
                "user-a", ServerUpgradeTrack.DataThroughput, config, out _);
            NetworkTopologyResult missingConfigResult = mSystem.UpgradeServer(
                "server-a", ServerUpgradeTrack.DataThroughput, null, out _);
            mSystem.UpgradeServer("server-a", ServerUpgradeTrack.DataThroughput, config, out _);
            NetworkTopologyResult maxLevelResult = mSystem.UpgradeServer(
                "server-a", ServerUpgradeTrack.DataThroughput, config, out _);

            Assert.That(userResult, Is.EqualTo(NetworkTopologyResult.NotServerNode));
            Assert.That(missingConfigResult, Is.EqualTo(NetworkTopologyResult.UpgradeConfigMissing));
            Assert.That(maxLevelResult, Is.EqualTo(NetworkTopologyResult.UpgradeLevelUnavailable));
            Assert.That(capabilities.DataProcessingPerSecond.Value, Is.EqualTo(150f));
            Assert.That(capabilities.DataThroughputLevel.Value, Is.EqualTo(1));

            Object.DestroyImmediate(config);
        }

        [Test]
        public void PaidUpgrade_WhenBalanceIsSufficient_DeductsAndUpdatesServerTogether()
        {
            ServerNodeCapabilities capabilities = new ServerNodeCapabilities(100f, 4, 0, 0);
            mSystem.Register(Node("server-a", NetworkNodeRole.Server), capabilities);
            ServerUpgradeConfig config = CreateUpgradeConfig();
            mEconomySystem.Add(100);

            NetworkTopologyResult result = mUpgradeSystem.UpgradeServer(
                "server-a",
                ServerUpgradeTrack.DataThroughput,
                config,
                out ServerUpgradeQuote quote);

            Assert.That(result, Is.EqualTo(NetworkTopologyResult.Success));
            Assert.That(quote.CurrentLevel, Is.EqualTo(0));
            Assert.That(quote.TargetLevel, Is.EqualTo(1));
            Assert.That(quote.TargetData.MoneyCost, Is.EqualTo(100));
            Assert.That(mEconomySystem, Is.Not.Null);
            Assert.That(NetworkTopologyTestArchitecture.Interface.GetModel<IEconomyModel>().Balance.Value, Is.EqualTo(0));
            Assert.That(capabilities.DataProcessingPerSecond.Value, Is.EqualTo(150f));
            Assert.That(capabilities.DataThroughputLevel.Value, Is.EqualTo(1));

            Object.DestroyImmediate(config);
        }

        [Test]
        public void PaidUpgrade_WhenBalanceIsInsufficient_LeavesBalanceAndCapabilitiesUntouched()
        {
            ServerNodeCapabilities capabilities = new ServerNodeCapabilities(100f, 4, 0, 0);
            mSystem.Register(Node("server-a", NetworkNodeRole.Server), capabilities);
            ServerUpgradeConfig config = CreateUpgradeConfig();
            mEconomySystem.Add(99);

            NetworkTopologyResult result = mUpgradeSystem.UpgradeServer(
                "server-a",
                ServerUpgradeTrack.DataThroughput,
                config,
                out ServerUpgradeQuote quote);

            Assert.That(result, Is.EqualTo(NetworkTopologyResult.InsufficientBalance));
            Assert.That(quote.TargetData, Is.Null);
            Assert.That(NetworkTopologyTestArchitecture.Interface.GetModel<IEconomyModel>().Balance.Value, Is.EqualTo(99));
            Assert.That(capabilities.DataProcessingPerSecond.Value, Is.EqualTo(100f));
            Assert.That(capabilities.DataThroughputLevel.Value, Is.EqualTo(0));

            Object.DestroyImmediate(config);
        }

        private static ServerUpgradeConfig CreateUpgradeConfig()
        {
            ServerUpgradeConfig config = ScriptableObject.CreateInstance<ServerUpgradeConfig>();
            config.SetDataThroughputLevels(new[]
            {
                new UpgradeLevelData(100f, 0),
                new UpgradeLevelData(150f, 100)
            });
            config.SetMaxConnectionLevels(new[]
            {
                new UpgradeLevelData(4f, 0),
                new UpgradeLevelData(6f, 80)
            });
            return config;
        }

        private static NodeDescriptor Node(string nodeId, NetworkNodeRole role)
        {
            return new NodeDescriptor(nodeId, role, nodeId);
        }

        private sealed class NetworkTopologyTestArchitecture : Architecture<NetworkTopologyTestArchitecture>
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
                EconomyModel economyModel = new EconomyModel();
                INetworkTopologySystem topologySystem = new NetworkTopologySystem(sModel);
                IEconomySystem economySystem = new EconomySystem(economyModel);
                RegisterModel<INetworkTopologyModel>(sModel);
                RegisterModel<IEconomyModel>(economyModel);
                RegisterSystem<INetworkTopologySystem>(topologySystem);
                RegisterSystem<IEconomySystem>(economySystem);
                RegisterSystem<IServerUpgradeSystem>(
                    new ServerUpgradeSystem(topologySystem, economySystem));
            }
        }
    }
}
