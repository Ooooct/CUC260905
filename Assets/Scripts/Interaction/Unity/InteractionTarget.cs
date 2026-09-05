using System;
using System.Collections.Generic;
using UnityEngine;

namespace CUC260905.Interaction
{
    /// <summary>逻辑交互对象的抽象入口。</summary>
    public interface IInteractionTarget
    {
        bool IsAvailable { get; }
    }

    /// <summary>向 Resolver 提供目标本地登记的意图接收器。</summary>
    public interface IIntentSinkProvider
    {
        bool TryGetIntentSink<TIntent>(out IIntentSink<TIntent> sink)
            where TIntent : struct, IInteractionIntent;
    }

    /// <summary>
    /// 逻辑交互对象的根组件。子 Collider 命中时，Resolver 会向父节点查找此组件。
    /// 它不解释任何输入，也不执行业务行为；只登记同一 GameObject 上的 Sink。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InteractionTarget : MonoBehaviour, IInteractionTarget, IIntentSinkProvider
    {
        // Key 是意图类型，Value 是实现该类型 Sink 的同物体组件。
        private readonly Dictionary<Type, object> mIntentSinks = new Dictionary<Type, object>();

        private void Awake()
        {
            // Sink 的接口类型在 Awake 前已经可反射，不依赖其自身初始化顺序。
            RebuildIntentSinks();
        }

        /// <summary>目标与其 GameObject 同时启用时才允许被 Resolver 返回。</summary>
        public bool IsAvailable
        {
            get { return isActiveAndEnabled; }
        }

        /// <summary>运行时新增或移除 Sink 后，由装配代码显式重建登记表。</summary>
        public void RebuildIntentSinks()
        {
            mIntentSinks.Clear();
            MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
            foreach (MonoBehaviour behaviour in behaviours)
            {
                // Unity 反序列化阶段可能暂存丢失脚本；登记层忽略它，不让输入链崩溃。
                if (behaviour == null)
                {
                    continue;
                }

                Type[] interfaces = behaviour.GetType().GetInterfaces();
                foreach (Type interfaceType in interfaces)
                {
                    if (!IsIntentSinkInterface(interfaceType))
                    {
                        continue;
                    }

                    Type intentType = interfaceType.GetGenericArguments()[0];
                    if (mIntentSinks.ContainsKey(intentType))
                    {
                        // 同类型多 Sink 会产生不透明的执行顺序，因此拒绝后注册项。
                        Debug.LogError(
                            $"{name} 上存在多个 {intentType.Name} 接收器。每种意图只能登记一个 Sink。",
                            this);
                        continue;
                    }

                    mIntentSinks.Add(intentType, behaviour);
                }
            }
        }

        /// <summary>只按意图类型返回已登记 Sink，不执行任何查找策略或业务判断。</summary>
        public bool TryGetIntentSink<TIntent>(out IIntentSink<TIntent> sink)
            where TIntent : struct, IInteractionIntent
        {
            if (mIntentSinks.TryGetValue(typeof(TIntent), out object value) &&
                value is IIntentSink<TIntent> typedSink)
            {
                sink = typedSink;
                return true;
            }

            sink = null;
            return false;
        }

        private static bool IsIntentSinkInterface(Type interfaceType)
        {
            return interfaceType.IsGenericType &&
                   interfaceType.GetGenericTypeDefinition() == typeof(IIntentSink<>);
        }
    }
}
