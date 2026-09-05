namespace CUC260905.Interaction
{
    /// <summary>
    /// 从 Unity 目标的本地 Sink 登记表解析接收器。
    /// Dispatcher 只依赖接口；注册表、配置表等其他 Resolver 可随时替换此实现。
    /// </summary>
    public sealed class ComponentSinkResolver : IIntentSinkResolver
    {
        public bool TryResolve<TIntent>(
            IInteractionTarget target,
            out IIntentSink<TIntent> sink)
            where TIntent : struct, IInteractionIntent
        {
            // Target 不提供本地登记能力时，本 Resolver 不猜测其业务实现。
            if (target is IIntentSinkProvider provider)
            {
                return provider.TryGetIntentSink(out sink);
            }

            sink = null;
            return false;
        }
    }
}
