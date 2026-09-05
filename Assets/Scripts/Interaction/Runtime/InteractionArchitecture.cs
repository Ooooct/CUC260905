using System;
using CUC260905.Economy;
using CUC260905.Feedback;
using CUC260905.Game;
using CUC260905.Message;
using CUC260905.Network;
using CUC260905.Placement;
using QFramework;

namespace CUC260905.Interaction
{
    /// <summary>
    /// 统一游戏架构：Interaction（输入/交互）与 Placement（放置）注册在同一容器，共享事件总线。
    /// Configure 必须先于 Interface 首次访问；InputController 是唯一装配入口。
    /// </summary>
    public sealed class GameArchitecture : Architecture<GameArchitecture>
    {
        private static bool sConfigured;
        private static InputConfig sConfiguration;

        /// <summary>在 Architecture 初始化前写入场景专属的 Camera 与交互参数。</summary>
        public static void Configure(InputConfig configuration)
        {
            if (mArchitecture != null || sConfigured)
            {
                throw new InvalidOperationException(
                    "GameArchitecture 已完成或正在等待装配；一个场景只能存在一个 InputController。");
            }

            if (configuration.Camera == null)
            {
                throw new ArgumentNullException(nameof(configuration), "InputController 必须指定 Camera。");
            }

            sConfiguration = configuration;
            sConfigured = true;
        }

        /// <summary>由拥有此单例的 Controller 在销毁时调用，释放场景专属引用。</summary>
        public static void Deinitialize()
        {
            if (mArchitecture == null)
            {
                sConfigured = false;
                sConfiguration = default;
                return;
            }

            mArchitecture.Deinit();
        }

        protected override void Init()
        {
            if (!sConfigured)
            {
                throw new InvalidOperationException(
                    "必须先调用 GameArchitecture.Configure，再访问 GameArchitecture.Interface。");
            }

            IIntentSinkResolver sinkResolver =
                new ComponentSinkResolver();
            ITargetResolver targetResolver = CreateTargetResolver();
            PointerFrameSource frameSource = new PointerFrameSource();
            PlacementModel placementModel = new PlacementModel();
            NetworkTopologyModel networkTopologyModel = new NetworkTopologyModel();
            EconomyModel economyModel = new EconomyModel();

            // Utility 先注册，随后 Model 和 System 可在各自 OnInit 中按接口获取依赖。
            RegisterUtility<IInputSourceUtility>(new LegacyInputUtility());
            RegisterUtility<IIntentSinkResolver>(sinkResolver);
            RegisterUtility<ITargetResolver>(targetResolver);
            RegisterUtility<IIntentDispatcher>(new IntentDispatcher(sinkResolver));
            RegisterUtility<INodeIdentitySource>(new GuidNodeIdentitySource());
            RegisterUtility<INodeDisplayNameSource>(new SequentialNodeDisplayNameSource());

            // 每帧指针帧数据源：Interaction 写入，Placement 读取（同一实例双端口）。
            RegisterUtility<IPointerFrameSink>(frameSource);
            RegisterUtility<IPointerFrameSource>(frameSource);

            // 放置域适配器与输入门控。
            RegisterUtility<IWorldPointerMapper>(
                new CameraWorldPointerMapper(sConfiguration.Camera, sConfiguration.PlacementZ));
            RegisterUtility<IPlacementInstantiator>(new UnityObjectInstantiator());
            RegisterUtility<IPlacementInputGate>(new PlacementInputGate(placementModel));

            RegisterModel<IPointerIntentModel>(
                new PointerIntentModel(sConfiguration.DragThresholdPixels));
            RegisterModel<IPlacementModel>(placementModel);
            RegisterModel<IGamePauseState>(new GamePauseState());
            RegisterModel<INetworkTopologyModel>(networkTopologyModel);
            RegisterModel<IEconomyModel>(economyModel);
            RegisterSystem<IInteractionInputSystem>(new InteractionInputSystem());
            RegisterSystem<IPlacementSystem>(new PlacementSystem());
            IMessageSystem messageSystem = new MessageSystem();
            RegisterSystem<IMessageSystem>(messageSystem);
            RegisterSystem<IFeedbackSystem>(new FeedbackSystem());
            INetworkTopologySystem networkTopologySystem = new NetworkTopologySystem(networkTopologyModel);
            IEconomySystem economySystem = new EconomySystem(economyModel);
            RegisterSystem<INetworkTopologySystem>(networkTopologySystem);
            RegisterSystem<IEconomySystem>(economySystem);
            RegisterSystem<INetworkConnectionSystem>(
                new NetworkConnectionSystem(networkTopologyModel, networkTopologySystem));
            RegisterSystem<IServerUpgradeSystem>(
                new ServerUpgradeSystem(networkTopologySystem, economySystem));
            RegisterSystem<IPacketTrafficSystem>(
                new PacketTrafficSystem(networkTopologyModel, messageSystem));
            // 数据包传输奖励：监听成功传输事件，为经济系统增加收入（不依赖网络域反向注入）。
            RegisterSystem<IPacketRewardSystem>(
                new PacketRewardSystem());
            // 数据包传输统计：累计成功传输字节量（Mb），供左上角 HUD 展示（不依赖网络域反向注入）。
            PacketTrafficStatsModel trafficStatsModel = new PacketTrafficStatsModel();
            RegisterModel<IPacketTrafficStatsModel>(trafficStatsModel);
            RegisterSystem<IPacketTrafficStatsSystem>(
                new PacketTrafficStatsSystem(trafficStatsModel));
        }

        protected override void OnDeinit()
        {
            // 释放静态配置，下一场景可以用新的 Camera 重新装配。
            sConfigured = false;
            sConfiguration = default;
        }

        private static ITargetResolver CreateTargetResolver()
        {
            if (sConfiguration.PhysicsMode == InteractionPhysicsMode.Physics2D)
            {
                return new Physics2DTargetResolver(
                    sConfiguration.Camera,
                    sConfiguration.LayerMask,
                    sConfiguration.MaxDistance);
            }

            return new Physics3DTargetResolver(
                sConfiguration.Camera,
                sConfiguration.LayerMask,
                sConfiguration.MaxDistance);
        }
    }
}
