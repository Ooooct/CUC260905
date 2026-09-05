using System;
using QFramework;

namespace CUC260905.Interaction
{
    /// <summary>
    /// 单场景、单输入上下文的 QFramework Architecture。
    /// Configure 必须先于 Interface 首次访问；Controller 是唯一装配入口。
    /// </summary>
    public sealed class InteractionArchitecture : Architecture<InteractionArchitecture>
    {
        private static bool sConfigured;
        private static InputConfig sConfiguration;

        /// <summary>在 Architecture 初始化前写入场景专属的 Camera 与交互参数。</summary>
        public static void Configure(InputConfig configuration)
        {
            if (mArchitecture != null || sConfigured)
            {
                throw new InvalidOperationException(
                    "InteractionArchitecture 已完成或正在等待装配；一个场景只能存在一个 InputController。");
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
                    "必须先调用 InteractionArchitecture.Configure，再访问 InteractionArchitecture.Interface。");
            }

            IIntentSinkResolver sinkResolver =
                new ComponentSinkResolver();
            ITargetResolver targetResolver = CreateTargetResolver();

            // Utility 先注册，随后 Model 和 System 可在各自 OnInit 中按接口获取依赖。
            RegisterUtility<IInputSourceUtility>(new LegacyInputUtility());
            RegisterUtility<IIntentSinkResolver>(sinkResolver);
            RegisterUtility<ITargetResolver>(targetResolver);
            RegisterUtility<IIntentDispatcher>(new IntentDispatcher(sinkResolver));

            RegisterModel<IPointerIntentModel>(
                new PointerIntentModel(sConfiguration.DragThresholdPixels));
            RegisterSystem<IInteractionInputSystem>(new InteractionInputSystem());
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
