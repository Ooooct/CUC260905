using System;
using QFramework;

namespace CUC260905.Interaction
{
    /// <summary>处理一种意图的业务接收器；接收器不持有 Dispatcher。</summary>
    public interface IIntentSink<TIntent>
        where TIntent : struct, IInteractionIntent
    {
        InteractionResult Handle(IInteractionTarget target, in TIntent intent);
    }

    /// <summary>按目标与意图类型查找业务接收器，是 Dispatcher 的反向依赖边界。</summary>
    public interface IIntentSinkResolver : IUtility
    {
        bool TryResolve<TIntent>(
            IInteractionTarget target,
            out IIntentSink<TIntent> sink)
            where TIntent : struct, IInteractionIntent;
    }

    /// <summary>对解释层暴露统一意图出口，对外提供可观察的调度结果。</summary>
    public interface IIntentDispatcher : IIntentEmitter, IUtility
    {
        InteractionResult Dispatch<TIntent>(IInteractionTarget target, in TIntent intent)
            where TIntent : struct, IInteractionIntent;
    }

    /// <summary>
    /// 只完成目标可用性检查与接收器路由，不解释 Click、Drag、Hover 的业务含义。
    /// Resolver 可在下一层替换为组件查找、注册表、配置表或测试替身。
    /// </summary>
    public sealed class IntentDispatcher : IIntentDispatcher
    {
        private readonly IIntentSinkResolver mSinkResolver;

        public IntentDispatcher(IIntentSinkResolver sinkResolver)
        {
            mSinkResolver = sinkResolver ?? throw new ArgumentNullException(nameof(sinkResolver));
        }

        /// <summary>满足解释层输出端口；结果由调用方选择是否观察。</summary>
        public InteractionResult Emit<TIntent>(IInteractionTarget target, in TIntent intent)
            where TIntent : struct, IInteractionIntent
        {
            return Dispatch(target, intent);
        }

        /// <summary>先验证目标，再委托 Resolver 获取与当前意图类型匹配的业务接收器。</summary>
        public InteractionResult Dispatch<TIntent>(IInteractionTarget target, in TIntent intent)
            where TIntent : struct, IInteractionIntent
        {
            if (target == null || !target.IsAvailable)
            {
                return new InteractionResult(InteractionResultStatus.TargetUnavailable);
            }

            if (!mSinkResolver.TryResolve(target, out IIntentSink<TIntent> sink) || sink == null)
            {
                return new InteractionResult(InteractionResultStatus.SinkUnavailable);
            }

            return sink.Handle(target, intent);
        }
    }
}
