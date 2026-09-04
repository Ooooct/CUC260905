/****************************************************************************
 * Copyright (c) 2015 ~ 2024 liangxiegame MIT License
 *
 * QFramework v1.0
 *
 * https://qframework.cn
 * https://github.com/liangxiegame/QFramework
 * https://gitee.com/liangxiegame/QFramework
 *
 * Author:
 *  liangxie        https://github.com/liangxie
 *  soso            https://github.com/so-sos-so
 *
 * Contributor
 *  TastSong        https://github.com/TastSong
 *  京产肠饭         https://gitee.com/JingChanChangFan/hk_-unity-tools
 *  猫叔(一只皮皮虾) https://space.bilibili.com/656352/
 *  misakiMeiii     https://github.com/misakiMeiii
 *  New一天
 *  幽飞冷凝雪～冷
 *
 * Community
 *  QQ Group: 623597263
 * 
 * Latest Update: 2025.3.18 10:21 add InitArchitecture api
 ****************************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace QFramework
{
    #region Architecture

    /// <summary>
    /// QFramework 的架构入口。
    ///
    /// 架构负责统一管理三类长期服务：Model 保存数据，System 承担业务逻辑，
    /// Utility 提供与业务无关的工具能力；Command、Query 和 Event 则是访问这些能力的通信方式。
    /// </summary>
    public interface IArchitecture
    {
        /// <summary>向架构注册一个 System。</summary>
        void RegisterSystem<T>(T system) where T : ISystem;

        /// <summary>向架构注册一个 Model。</summary>
        void RegisterModel<T>(T model) where T : IModel;

        /// <summary>向架构注册一个 Utility。</summary>
        void RegisterUtility<T>(T utility) where T : IUtility;

        /// <summary>按类型获取已注册的 System。</summary>
        T GetSystem<T>() where T : class, ISystem;

        /// <summary>按类型获取已注册的 Model。</summary>
        T GetModel<T>() where T : class, IModel;

        /// <summary>按类型获取已注册的 Utility。</summary>
        T GetUtility<T>() where T : class, IUtility;

        /// <summary>发送一个无返回值的 Command。</summary>
        void SendCommand<T>(T command) where T : ICommand;

        /// <summary>发送一个有返回值的 Command，并返回执行结果。</summary>
        TResult SendCommand<TResult>(ICommand<TResult> command);

        /// <summary>发送一个 Query，并返回查询结果。</summary>
        TResult SendQuery<TResult>(IQuery<TResult> query);

        /// <summary>发送一个无参 Event。Event 类型必须可以用无参构造函数创建。</summary>
        void SendEvent<T>() where T : new();

        /// <summary>发送一个已经创建好的 Event 实例。</summary>
        void SendEvent<T>(T e);

        /// <summary>注册一个类型事件监听器，并返回用于取消注册的句柄。</summary>
        IUnRegister RegisterEvent<T>(Action<T> onEvent);

        /// <summary>移除一个类型事件监听器。</summary>
        void UnRegisterEvent<T>(Action<T> onEvent);

        /// <summary>反初始化架构，释放架构持有的 Model、System 和 IOC 注册。</summary>
        void Deinit();
    }

    /// <summary>
    /// 架构的泛型基类。
    /// 每一种具体架构都通过继承此类获得一个按类型区分的静态实例：
    /// <code>public class GameArchitecture : Architecture&lt;GameArchitecture&gt;</code>。
    /// </summary>
    public abstract class Architecture<T> : IArchitecture where T : Architecture<T>, new()
    {
        // mInited 用来区分“首次初始化阶段”和“运行中动态注册阶段”。
        private bool mInited = false;


        /// <summary>
        /// 在架构创建并完成 <see cref="Init"/> 后调用的补丁回调。
        /// 可用于在不修改架构子类的情况下追加注册逻辑。
        /// </summary>
        public static Action<T> OnRegisterPatch = architecture => { };

        /// <summary>当前具体架构的静态实例。</summary>
        protected static T mArchitecture;

        /// <summary>
        /// 获取当前架构的接口实例。
        /// 第一次访问时会自动调用 <see cref="InitArchitecture"/> 完成懒初始化。
        /// </summary>
        public static IArchitecture Interface
        {
            get
            {
                if (mArchitecture == null) InitArchitecture();
                return mArchitecture;
            }
        }

        /// <summary>
        /// 初始化架构。
        ///
        /// 初始化顺序是：创建架构实例 → 执行子类 <see cref="Init"/> 注册依赖 →
        /// 执行 <see cref="OnRegisterPatch"/> → 初始化 Model → 初始化 System。
        /// 同一架构实例只会初始化一次。
        /// </summary>
        public static void InitArchitecture()
        {
            if (mArchitecture == null)
            {
                mArchitecture = new T();

                // 子类 Init 只负责声明/注册依赖，不建议在这里直接使用尚未完成初始化的 System 或 Model。
                mArchitecture.Init();

                OnRegisterPatch?.Invoke(mArchitecture);

                // Model 和 System 在注册完成后统一初始化；LINQ 查询只筛选尚未初始化的实例。
                foreach (var model in mArchitecture.mContainer.GetInstancesByType<IModel>().Where(m => !m.Initialized))
                {
                    model.Init();
                    model.Initialized = true;
                }

                foreach (var system in mArchitecture.mContainer.GetInstancesByType<ISystem>()
                             .Where(m => !m.Initialized))
                {
                    system.Init();
                    system.Initialized = true;
                }

                mArchitecture.mInited = true;
            }
        }

        /// <summary>由具体架构实现，用于注册 Model、System 和 Utility。</summary>
        protected abstract void Init();

        /// <summary>
        /// 反初始化架构。
        /// 先调用架构自己的清理回调，再反初始化 System 和 Model，最后清空 IOC 容器并释放静态实例。
        /// </summary>
        public void Deinit()
        {
            OnDeinit();
            foreach (var system in mContainer.GetInstancesByType<ISystem>().Where(s => s.Initialized)) system.Deinit();
            foreach (var model in mContainer.GetInstancesByType<IModel>().Where(m => m.Initialized)) model.Deinit();
            mContainer.Clear();
            mArchitecture = null;
        }

        /// <summary>架构反初始化时的扩展点，子类可重写。</summary>
        protected virtual void OnDeinit()
        {
        }

        // IOCContainer 只保存当前架构的依赖；架构销毁时统一 Clear，避免跨架构残留实例。
        private IOCContainer mContainer = new IOCContainer();

        /// <summary>
        /// 注册 System。
        /// 如果架构已经完成初始化，动态注册的 System 会在注册动作内立即初始化。
        /// </summary>
        public void RegisterSystem<TSystem>(TSystem system) where TSystem : ISystem
        {
            system.SetArchitecture(this);
            mContainer.Register<TSystem>(system);

            if (mInited)
            {
                system.Init();
                system.Initialized = true;
            }
        }

        /// <summary>
        /// 注册 Model。
        /// 如果架构已经完成初始化，动态注册的 Model 会在注册动作内立即初始化。
        /// </summary>
        public void RegisterModel<TModel>(TModel model) where TModel : IModel
        {
            model.SetArchitecture(this);
            mContainer.Register<TModel>(model);

            if (mInited)
            {
                model.Init();
                model.Initialized = true;
            }
        }

        /// <summary>注册 Utility。Utility 没有统一的初始化生命周期。</summary>
        public void RegisterUtility<TUtility>(TUtility utility) where TUtility : IUtility =>
            mContainer.Register<TUtility>(utility);

        /// <summary>获取指定类型的 System；未注册时由 IOCContainer 返回 null。</summary>
        public TSystem GetSystem<TSystem>() where TSystem : class, ISystem => mContainer.Get<TSystem>();

        /// <summary>获取指定类型的 Model；未注册时由 IOCContainer 返回 null。</summary>
        public TModel GetModel<TModel>() where TModel : class, IModel => mContainer.Get<TModel>();

        /// <summary>获取指定类型的 Utility；未注册时由 IOCContainer 返回 null。</summary>
        public TUtility GetUtility<TUtility>() where TUtility : class, IUtility => mContainer.Get<TUtility>();

        /// <summary>执行一个有返回值的 Command。</summary>
        public TResult SendCommand<TResult>(ICommand<TResult> command) => ExecuteCommand(command);

        /// <summary>执行一个无返回值的 Command。</summary>
        public void SendCommand<TCommand>(TCommand command) where TCommand : ICommand => ExecuteCommand(command);

        /// <summary>
        /// Command 的实际执行入口。
        /// 架构先把自身注入 Command，再调用 Command.Execute；Command 内部通常通过扩展方法访问 Model/System。
        /// </summary>
        protected virtual TResult ExecuteCommand<TResult>(ICommand<TResult> command)
        {
            command.SetArchitecture(this);
            return command.Execute();
        }

        /// <summary>无返回值 Command 的实际执行入口。</summary>
        protected virtual void ExecuteCommand(ICommand command)
        {
            command.SetArchitecture(this);
            command.Execute();
        }

        /// <summary>执行一个 Query。</summary>
        public TResult SendQuery<TResult>(IQuery<TResult> query) => DoQuery<TResult>(query);

        /// <summary>
        /// Query 的实际执行入口。
        /// Query 同样先获得当前架构，再执行 <see cref="IQuery{TResult}.Do"/>。
        /// </summary>
        protected virtual TResult DoQuery<TResult>(IQuery<TResult> query)
        {
            query.SetArchitecture(this);
            return query.Do();
        }

        // 每个 Architecture 拥有自己的事件系统；Global 则是跨架构的全局事件系统。
        private TypeEventSystem mTypeEventSystem = new TypeEventSystem();

        /// <summary>发送无参类型事件。</summary>
        public void SendEvent<TEvent>() where TEvent : new() => mTypeEventSystem.Send<TEvent>();

        /// <summary>发送指定实例的类型事件。</summary>
        public void SendEvent<TEvent>(TEvent e) => mTypeEventSystem.Send<TEvent>(e);

        /// <summary>注册架构作用域内的类型事件。</summary>
        public IUnRegister RegisterEvent<TEvent>(Action<TEvent> onEvent) => mTypeEventSystem.Register<TEvent>(onEvent);

        /// <summary>移除架构作用域内的类型事件监听。</summary>
        public void UnRegisterEvent<TEvent>(Action<TEvent> onEvent) => mTypeEventSystem.UnRegister<TEvent>(onEvent);
    }

    /// <summary>为指定事件类型提供统一处理入口的接口。</summary>
    public interface IOnEvent<T>
    {
        /// <summary>收到事件时调用。</summary>
        void OnEvent(T e);
    }

    /// <summary>把 IOnEvent&lt;T&gt; 适配到全局 TypeEventSystem 的扩展方法。</summary>
    public static class OnGlobalEventExtension
    {
        /// <summary>注册当前对象的全局事件处理方法。</summary>
        public static IUnRegister RegisterEvent<T>(this IOnEvent<T> self) where T : struct =>
            TypeEventSystem.Global.Register<T>(self.OnEvent);

        /// <summary>移除当前对象的全局事件处理方法。</summary>
        public static void UnRegisterEvent<T>(this IOnEvent<T> self) where T : struct =>
            TypeEventSystem.Global.UnRegister<T>(self.OnEvent);
    }

    #endregion

    #region Controller

    /// <summary>
    /// Controller 的能力集合。
    /// Controller 通常位于表现层，只负责响应输入/生命周期并发出 Command、Query 或 Event，
    /// 不直接持有业务数据。
    /// </summary>
    public interface IController : IBelongToArchitecture, ICanSendCommand, ICanGetSystem, ICanGetModel,
        ICanRegisterEvent, ICanSendQuery, ICanGetUtility
    {
    }

    #endregion

    #region System

    /// <summary>
    /// System 的能力集合。
    /// System 是有生命周期的业务服务，能够访问 Model、Utility、其他 System，并收发事件。
    /// </summary>
    public interface ISystem : IBelongToArchitecture, ICanSetArchitecture, ICanGetModel, ICanGetUtility,
        ICanRegisterEvent, ICanSendEvent, ICanGetSystem, ICanInit
    {
    }

    /// <summary>System 的抽象基类，负责保存架构引用并转发生命周期。</summary>
    public abstract class AbstractSystem : ISystem
    {
        private IArchitecture mArchitecture;

        // 通过显式接口实现隐藏生命周期基础设施，让业务子类只需关注 OnInit/OnDeinit。
        IArchitecture IBelongToArchitecture.GetArchitecture() => mArchitecture;

        void ICanSetArchitecture.SetArchitecture(IArchitecture architecture) => mArchitecture = architecture;

        /// <summary>表示当前 System 是否已经完成初始化。</summary>
        public bool Initialized { get; set; }

        void ICanInit.Init() => OnInit();

        /// <summary>执行 System 的反初始化逻辑。</summary>
        public void Deinit() => OnDeinit();

        /// <summary>反初始化扩展点，子类可重写。</summary>
        protected virtual void OnDeinit()
        {
        }

        /// <summary>初始化扩展点，子类必须实现。</summary>
        protected abstract void OnInit();
    }

    #endregion

    #region Model

    /// <summary>
    /// Model 的能力集合。
    /// Model 保存业务状态，可以访问 Utility 并发送事件，但不应依赖表现层对象。
    /// </summary>
    public interface IModel : IBelongToArchitecture, ICanSetArchitecture, ICanGetUtility, ICanSendEvent, ICanInit
    {
    }

    /// <summary>Model 的抽象基类，负责保存架构引用并转发生命周期。</summary>
    public abstract class AbstractModel : IModel
    {
        private IArchitecture mArchitecturel;

        // Model 与 System 一样属于某个 Architecture；引用由注册过程注入。
        IArchitecture IBelongToArchitecture.GetArchitecture() => mArchitecturel;

        void ICanSetArchitecture.SetArchitecture(IArchitecture architecture) => mArchitecturel = architecture;

        /// <summary>表示当前 Model 是否已经完成初始化。</summary>
        public bool Initialized { get; set; }

        void ICanInit.Init() => OnInit();

        /// <summary>执行 Model 的反初始化逻辑。</summary>
        public void Deinit() => OnDeinit();

        /// <summary>反初始化扩展点，子类可重写。</summary>
        protected virtual void OnDeinit()
        {
        }

        /// <summary>初始化扩展点，子类必须实现。</summary>
        protected abstract void OnInit();
    }

    #endregion

    #region Utility

    /// <summary>
    /// Utility 的标记接口。
    /// Utility 通常是无状态或独立于具体业务的基础能力，例如存档、时间、随机数和配置读取。
    /// </summary>
    public interface IUtility
    {
    }

    #endregion

    #region Command

    /// <summary>无返回值 Command 的接口；Command 表达一次改变业务状态的动作。</summary>
    public interface ICommand : IBelongToArchitecture, ICanSetArchitecture, ICanGetSystem, ICanGetModel, ICanGetUtility,
        ICanSendEvent, ICanSendCommand, ICanSendQuery
    {
        /// <summary>执行 Command。</summary>
        void Execute();
    }

    /// <summary>有返回值 Command 的接口；适合需要在执行动作后立即获得结果的场景。</summary>
    public interface ICommand<TResult> : IBelongToArchitecture, ICanSetArchitecture, ICanGetSystem, ICanGetModel,
        ICanGetUtility,
        ICanSendEvent, ICanSendCommand, ICanSendQuery
    {
        /// <summary>执行 Command 并返回结果。</summary>
        TResult Execute();
    }

    /// <summary>
    /// 无返回值 Command 的抽象基类。
    /// 架构通过显式接口实现注入自身，业务代码只需实现 <see cref="OnExecute"/>。
    /// </summary>
    public abstract class AbstractCommand : ICommand
    {
        private IArchitecture mArchitecture;

        IArchitecture IBelongToArchitecture.GetArchitecture() => mArchitecture;

        void ICanSetArchitecture.SetArchitecture(IArchitecture architecture) => mArchitecture = architecture;

        void ICommand.Execute() => OnExecute();

        /// <summary>实现 Command 的业务动作。</summary>
        protected abstract void OnExecute();
    }

    /// <summary>有返回值 Command 的抽象基类。</summary>
    public abstract class AbstractCommand<TResult> : ICommand<TResult>
    {
        private IArchitecture mArchitecture;

        IArchitecture IBelongToArchitecture.GetArchitecture() => mArchitecture;

        void ICanSetArchitecture.SetArchitecture(IArchitecture architecture) => mArchitecture = architecture;

        TResult ICommand<TResult>.Execute() => OnExecute();

        /// <summary>实现 Command 的业务动作并返回结果。</summary>
        protected abstract TResult OnExecute();
    }

    #endregion

    #region Query

    /// <summary>Query 的接口；Query 只读取状态，不应通过副作用修改业务数据。</summary>
    public interface IQuery<TResult> : IBelongToArchitecture, ICanSetArchitecture, ICanGetModel, ICanGetSystem,
        ICanSendQuery
    {
        /// <summary>执行查询并返回结果。</summary>
        TResult Do();
    }

    /// <summary>Query 的抽象基类，子类通过 <see cref="OnDo"/> 提供查询逻辑。</summary>
    public abstract class AbstractQuery<T> : IQuery<T>
    {
        /// <summary>执行查询。</summary>
        public T Do() => OnDo();

        /// <summary>实现查询逻辑并返回结果。</summary>
        protected abstract T OnDo();


        private IArchitecture mArchitecture;

        /// <summary>获取当前 Query 所属的架构。</summary>
        public IArchitecture GetArchitecture() => mArchitecture;

        /// <summary>由架构注入当前 Query 所属的架构。</summary>
        public void SetArchitecture(IArchitecture architecture) => mArchitecture = architecture;
    }

    #endregion

    #region Rule

    /// <summary>标识对象属于某个 Architecture，并可通过它访问架构服务。</summary>
    public interface IBelongToArchitecture
    {
        /// <summary>获取所属架构。</summary>
        IArchitecture GetArchitecture();
    }

    /// <summary>允许架构在运行时把所属 Architecture 注入对象。</summary>
    public interface ICanSetArchitecture
    {
        /// <summary>设置所属架构。</summary>
        void SetArchitecture(IArchitecture architecture);
    }

    /// <summary>声明对象可以获取 Model。</summary>
    public interface ICanGetModel : IBelongToArchitecture
    {
    }

    /// <summary>把 ICanGetModel 的能力转发为便捷的扩展方法。</summary>
    public static class CanGetModelExtension
    {
        /// <summary>通过所属架构获取指定类型的 Model。</summary>
        public static T GetModel<T>(this ICanGetModel self) where T : class, IModel =>
            self.GetArchitecture().GetModel<T>();
    }

    /// <summary>声明对象可以获取 System。</summary>
    public interface ICanGetSystem : IBelongToArchitecture
    {
    }

    /// <summary>把 ICanGetSystem 的能力转发为便捷的扩展方法。</summary>
    public static class CanGetSystemExtension
    {
        /// <summary>通过所属架构获取指定类型的 System。</summary>
        public static T GetSystem<T>(this ICanGetSystem self) where T : class, ISystem =>
            self.GetArchitecture().GetSystem<T>();
    }

    /// <summary>声明对象可以获取 Utility。</summary>
    public interface ICanGetUtility : IBelongToArchitecture
    {
    }

    /// <summary>把 ICanGetUtility 的能力转发为便捷的扩展方法。</summary>
    public static class CanGetUtilityExtension
    {
        /// <summary>通过所属架构获取指定类型的 Utility。</summary>
        public static T GetUtility<T>(this ICanGetUtility self) where T : class, IUtility =>
            self.GetArchitecture().GetUtility<T>();
    }

    /// <summary>声明对象可以注册和移除架构作用域内的事件。</summary>
    public interface ICanRegisterEvent : IBelongToArchitecture
    {
    }

    /// <summary>把 ICanRegisterEvent 的能力转发为便捷的扩展方法。</summary>
    public static class CanRegisterEventExtension
    {
        /// <summary>注册架构作用域内的事件监听。</summary>
        public static IUnRegister RegisterEvent<T>(this ICanRegisterEvent self, Action<T> onEvent) =>
            self.GetArchitecture().RegisterEvent<T>(onEvent);

        /// <summary>移除架构作用域内的事件监听。</summary>
        public static void UnRegisterEvent<T>(this ICanRegisterEvent self, Action<T> onEvent) =>
            self.GetArchitecture().UnRegisterEvent<T>(onEvent);
    }

    /// <summary>声明对象可以发送 Command。</summary>
    public interface ICanSendCommand : IBelongToArchitecture
    {
    }

    /// <summary>把 ICanSendCommand 的能力转发为便捷的扩展方法。</summary>
    public static class CanSendCommandExtension
    {
        /// <summary>创建并发送一个无参 Command。</summary>
        public static void SendCommand<T>(this ICanSendCommand self) where T : ICommand, new() =>
            self.GetArchitecture().SendCommand<T>(new T());

        /// <summary>发送一个已经创建好的无返回值 Command。</summary>
        public static void SendCommand<T>(this ICanSendCommand self, T command) where T : ICommand =>
            self.GetArchitecture().SendCommand<T>(command);

        /// <summary>发送一个有返回值的 Command。</summary>
        public static TResult SendCommand<TResult>(this ICanSendCommand self, ICommand<TResult> command) =>
            self.GetArchitecture().SendCommand(command);
    }

    /// <summary>声明对象可以发送 Event。</summary>
    public interface ICanSendEvent : IBelongToArchitecture
    {
    }

    /// <summary>把 ICanSendEvent 的能力转发为便捷的扩展方法。</summary>
    public static class CanSendEventExtension
    {
        /// <summary>创建并发送一个无参 Event。</summary>
        public static void SendEvent<T>(this ICanSendEvent self) where T : new() =>
            self.GetArchitecture().SendEvent<T>();

        /// <summary>发送一个已经创建好的 Event。</summary>
        public static void SendEvent<T>(this ICanSendEvent self, T e) => self.GetArchitecture().SendEvent<T>(e);
    }

    /// <summary>声明对象可以发送 Query。</summary>
    public interface ICanSendQuery : IBelongToArchitecture
    {
    }

    /// <summary>把 ICanSendQuery 的能力转发为便捷的扩展方法。</summary>
    public static class CanSendQueryExtension
    {
        /// <summary>发送 Query 并返回查询结果。</summary>
        public static TResult SendQuery<TResult>(this ICanSendQuery self, IQuery<TResult> query) =>
            self.GetArchitecture().SendQuery(query);
    }

    /// <summary>统一描述 Model/System 的初始化状态和生命周期。</summary>
    public interface ICanInit
    {
        /// <summary>标识对象是否已经完成初始化。</summary>
        bool Initialized { get; set; }

        /// <summary>初始化对象。</summary>
        void Init();

        /// <summary>反初始化对象。</summary>
        void Deinit();
    }

    #endregion

    #region TypeEventSystem

    /// <summary>封装一次取消注册动作的句柄。</summary>
    public interface IUnRegister
    {
        /// <summary>执行取消注册动作。</summary>
        void UnRegister();
    }

    /// <summary>保存多个取消注册句柄，便于在对象销毁或状态切换时统一清理。</summary>
    public interface IUnRegisterList
    {
        /// <summary>当前持有的取消注册句柄列表。</summary>
        List<IUnRegister> UnregisterList { get; }
    }

    /// <summary>管理取消注册句柄列表的扩展方法。</summary>
    public static class IUnRegisterListExtension
    {
        /// <summary>把一个取消注册句柄加入列表。</summary>
        public static void AddToUnregisterList(this IUnRegister self, IUnRegisterList unRegisterList) =>
            unRegisterList.UnregisterList.Add(self);

        /// <summary>依次执行列表中的所有取消注册动作，然后清空列表。</summary>
        public static void UnRegisterAll(this IUnRegisterList self)
        {
            foreach (var unRegister in self.UnregisterList)
            {
                unRegister.UnRegister();
            }

            self.UnregisterList.Clear();
        }
    }

    /// <summary>
    /// 基于委托的取消注册句柄。
    /// 调用一次后会把委托置空，因此它适合作为一次性资源清理动作返回给调用者。
    /// </summary>
    public struct CustomUnRegister : IUnRegister
    {
        private Action mOnUnRegister { get; set; }

        /// <summary>创建一个由 <paramref name="onUnRegister"/> 执行清理的句柄。</summary>
        public CustomUnRegister(Action onUnRegister) => mOnUnRegister = onUnRegister;

        /// <summary>执行清理并释放内部委托。</summary>
        public void UnRegister()
        {
            mOnUnRegister.Invoke();
            mOnUnRegister = null;
        }
    }

#if UNITY_5_6_OR_NEWER
    /// <summary>
    /// Unity 生命周期取消注册触发器的基类。
    /// 具体子类在 OnDestroy、OnDisable 或当前场景卸载时调用 <see cref="UnRegister"/>。
    /// </summary>
    public abstract class UnRegisterTrigger : UnityEngine.MonoBehaviour
    {
        private readonly HashSet<IUnRegister> mUnRegisters = new HashSet<IUnRegister>();

        /// <summary>添加一个取消注册句柄，并返回同一个句柄以便链式调用。</summary>
        public IUnRegister AddUnRegister(IUnRegister unRegister)
        {
            mUnRegisters.Add(unRegister);
            return unRegister;
        }

        /// <summary>从触发器中移除一个尚未执行的取消注册句柄。</summary>
        public void RemoveUnRegister(IUnRegister unRegister) => mUnRegisters.Remove(unRegister);

        /// <summary>执行并清空当前触发器持有的全部取消注册动作。</summary>
        public void UnRegister()
        {
            foreach (var unRegister in mUnRegisters)
            {
                unRegister.UnRegister();
            }

            mUnRegisters.Clear();
        }
    }

    /// <summary>当 GameObject 被销毁时自动取消注册。</summary>
    public class UnRegisterOnDestroyTrigger : UnRegisterTrigger
    {
        private void OnDestroy()
        {
            UnRegister();
        }
    }

    /// <summary>当 GameObject 被禁用时自动取消注册。</summary>
    public class UnRegisterOnDisableTrigger : UnRegisterTrigger
    {
        private void OnDisable()
        {
            UnRegister();
        }
    }

    /// <summary>当当前场景卸载时自动取消注册的全局触发器。</summary>
    public class UnRegisterCurrentSceneUnloadedTrigger : UnRegisterTrigger
    {
        private static UnRegisterCurrentSceneUnloadedTrigger mDefault;

        /// <summary>获取场景卸载触发器；首次访问时会创建并隐藏一个常驻 GameObject。</summary>
        public static UnRegisterCurrentSceneUnloadedTrigger Get
        {
            get
            {
                if (!mDefault)
                {
                    mDefault = new GameObject("UnRegisterCurrentSceneUnloadedTrigger")
                        .AddComponent<UnRegisterCurrentSceneUnloadedTrigger>();
                }

                return mDefault;
            }
        }

        private void Awake()
        {
            // 触发器必须跨场景存活，才能监听当前场景的卸载事件。
            DontDestroyOnLoad(this);
            hideFlags = HideFlags.HideInHierarchy;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
        }

        private void OnDestroy() => SceneManager.sceneUnloaded -= OnSceneUnloaded;
        void OnSceneUnloaded(Scene scene) => UnRegister();
    }
#endif

    /// <summary>把取消注册动作绑定到 Unity 或 Godot 的生命周期。</summary>
    public static class UnRegisterExtension
    {
#if UNITY_5_6_OR_NEWER

        // 把取消注册行为挂到 GameObject 的生命周期上，调用方不必手动保存 MonoBehaviour 引用。
        static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
        {
            var trigger = gameObject.GetComponent<T>();

            if (!trigger)
            {
                trigger = gameObject.AddComponent<T>();
            }

            return trigger;
        }

        /// <summary>当 GameObject 销毁时取消注册。</summary>
        public static IUnRegister UnRegisterWhenGameObjectDestroyed(this IUnRegister unRegister,
            UnityEngine.GameObject gameObject) =>
            GetOrAddComponent<UnRegisterOnDestroyTrigger>(gameObject)
                .AddUnRegister(unRegister);

        /// <summary>当组件所在的 GameObject 销毁时取消注册。</summary>
        public static IUnRegister UnRegisterWhenGameObjectDestroyed<T>(this IUnRegister self, T component)
            where T : UnityEngine.Component =>
                self.UnRegisterWhenGameObjectDestroyed(component.gameObject);

        /// <summary>当组件所在的 GameObject 被禁用时取消注册。</summary>
        public static IUnRegister UnRegisterWhenDisabled<T>(this IUnRegister self, T component)
            where T : UnityEngine.Component =>
                self.UnRegisterWhenDisabled(component.gameObject);

        /// <summary>当 GameObject 被禁用时取消注册。</summary>
        public static IUnRegister UnRegisterWhenDisabled(this IUnRegister unRegister,
            UnityEngine.GameObject gameObject) =>
            GetOrAddComponent<UnRegisterOnDisableTrigger>(gameObject)
                .AddUnRegister(unRegister);

        /// <summary>当当前场景卸载时取消注册。</summary>
        public static IUnRegister UnRegisterWhenCurrentSceneUnloaded(this IUnRegister self) =>
            UnRegisterCurrentSceneUnloadedTrigger.Get.AddUnRegister(self);
#endif


#if GODOT
		/// <summary>当 Godot Node 离开场景树时取消注册。</summary>
		public static IUnRegister UnRegisterWhenNodeExitTree(this IUnRegister unRegister, Godot.Node node)
		{
			node.TreeExiting += unRegister.UnRegister;
			return unRegister;
		}
#endif
    }

    /// <summary>
    /// 按事件类型分发事件的系统。
    /// 每个 Architecture 有一个实例；<see cref="Global"/> 提供跨架构的全局实例。
    /// 事件类型本身就是路由键，不需要额外的字符串或枚举。
    /// </summary>
    public class TypeEventSystem
    {
        private readonly EasyEvents mEvents = new EasyEvents();

        /// <summary>跨 Architecture 共享的全局类型事件系统。</summary>
        public static readonly TypeEventSystem Global = new TypeEventSystem();

        /// <summary>创建并发送一个无参事件；没有监听器时不会创建事件容器。</summary>
        public void Send<T>() where T : new() => mEvents.GetEvent<EasyEvent<T>>()?.Trigger(new T());

        /// <summary>发送一个事件实例。</summary>
        public void Send<T>(T e) => mEvents.GetEvent<EasyEvent<T>>()?.Trigger(e);

        /// <summary>注册事件监听器，并返回取消注册句柄。</summary>
        public IUnRegister Register<T>(Action<T> onEvent) => mEvents.GetOrAddEvent<EasyEvent<T>>().Register(onEvent);

        /// <summary>移除指定事件类型的监听器。</summary>
        public void UnRegister<T>(Action<T> onEvent)
        {
            var e = mEvents.GetEvent<EasyEvent<T>>();
            e?.UnRegister(onEvent);
        }
    }

    #endregion

    #region IOC

    /// <summary>
    /// 简单的类型到实例映射容器。
    /// Architecture 用它保存 Model、System 和 Utility；注册同一类型时会覆盖旧实例。
    /// </summary>
    public class IOCContainer
    {
        private Dictionary<Type, object> mInstances = new Dictionary<Type, object>();

        /// <summary>按泛型参数的类型注册实例；同类型重复注册会替换旧实例。</summary>
        public void Register<T>(T instance)
        {
            var key = typeof(T);

            if (mInstances.ContainsKey(key))
            {
                mInstances[key] = instance;
            }
            else
            {
                mInstances.Add(key, instance);
            }
        }

        /// <summary>按精确注册类型获取实例；未找到时返回 null。</summary>
        public T Get<T>() where T : class
        {
            var key = typeof(T);

            if (mInstances.TryGetValue(key, out var retInstance))
            {
                return retInstance as T;
            }

            return null;
        }

        /// <summary>
        /// 获取所有可赋值给 T 的实例。
        /// 这个方法用于架构批量初始化所有 Model 或 System。
        /// </summary>
        public IEnumerable<T> GetInstancesByType<T>()
        {
            var type = typeof(T);
            return mInstances.Values.Where(instance => type.IsInstanceOfType(instance)).Cast<T>();
        }

        /// <summary>清空容器中的全部实例引用。</summary>
        public void Clear() => mInstances.Clear();
    }

    #endregion

    #region BindableProperty

    /// <summary>
    /// 可读写的响应式属性。
    /// Value 发生变化时触发监听器；<see cref="SetValueWithoutEvent"/> 可以只更新值而不通知监听器。
    /// </summary>
    public interface IBindableProperty<T> : IReadonlyBindableProperty<T>
    {
        /// <summary>属性当前值。</summary>
        new T Value { get; set; }

        /// <summary>设置属性值但不触发变更事件。</summary>
        void SetValueWithoutEvent(T newValue);
    }

    /// <summary>只读观察接口；调用者可以监听变化，但不能直接写入 Value。</summary>
    public interface IReadonlyBindableProperty<T> : IEasyEvent
    {
        /// <summary>属性当前值。</summary>
        T Value { get; }

        /// <summary>注册监听器，并在注册完成前立即用当前值调用一次。</summary>
        IUnRegister RegisterWithInitValue(Action<T> action);

        /// <summary>移除属性变更监听器。</summary>
        void UnRegister(Action<T> onValueChanged);

        /// <summary>注册属性变更监听器。</summary>
        IUnRegister Register(Action<T> onValueChanged);
    }

    /// <summary>
    /// BindableProperty 的默认实现。
    /// 属性写入时先通过 <see cref="Comparer"/> 判断是否真的变化，只有变化时才保存并触发事件。
    /// </summary>
    public class BindableProperty<T> : IBindableProperty<T>
    {
        /// <summary>使用初始值创建属性。</summary>
        public BindableProperty(T defaultValue = default) => mValue = defaultValue;

        /// <summary>属性当前保存的原始值；子类可通过扩展点访问它。</summary>
        protected T mValue;

        /// <summary>
        /// 当前 T 类型共用的值比较器。
        /// <see cref="WithComparer"/> 会修改该泛型类型的静态比较器，因此会影响所有 BindableProperty&lt;T&gt; 实例。
        /// </summary>
        public static Func<T, T, bool> Comparer { get; set; } = (a, b) => a.Equals(b);

        /// <summary>设置当前 T 类型使用的比较器，并返回当前属性以支持链式配置。</summary>
        public BindableProperty<T> WithComparer(Func<T, T, bool> comparer)
        {
            Comparer = comparer;
            return this;
        }

        /// <summary>
        /// 读取或写入属性值。
        /// 写入 null/null 或比较器判定相等时不会触发事件；否则先保存新值再通知监听器。
        /// </summary>
        public T Value
        {
            get => GetValue();
            set
            {
                if (value == null && mValue == null) return;
                if (value != null && Comparer(value, mValue)) return;

                SetValue(value);
                mOnValueChanged.Trigger(value);
            }
        }

        /// <summary>保存值的扩展点，子类可以重写以增加存储逻辑。</summary>
        protected virtual void SetValue(T newValue) => mValue = newValue;

        /// <summary>读取值的扩展点，子类可以重写以增加读取逻辑。</summary>
        protected virtual T GetValue() => mValue;

        /// <summary>直接写入值，不触发属性变更事件。</summary>
        public void SetValueWithoutEvent(T newValue) => mValue = newValue;

        private EasyEvent<T> mOnValueChanged = new EasyEvent<T>();

        /// <summary>注册属性变更监听器。</summary>
        public IUnRegister Register(Action<T> onValueChanged)
        {
            return mOnValueChanged.Register(onValueChanged);
        }

        /// <summary>先用当前值调用监听器，再注册后续变化监听。</summary>
        public IUnRegister RegisterWithInitValue(Action<T> onValueChanged)
        {
            onValueChanged(mValue);
            return Register(onValueChanged);
        }

        /// <summary>移除属性变更监听器。</summary>
        public void UnRegister(Action<T> onValueChanged) => mOnValueChanged.UnRegister(onValueChanged);

        // 适配 IEasyEvent 的无参数监听：属性值发生变化时忽略 T，仅通知“发生了变化”。
        IUnRegister IEasyEvent.Register(Action onEvent)
        {
            return Register(Action);
            void Action(T _) => onEvent();
        }

        /// <summary>返回当前值的字符串表示。</summary>
        public override string ToString() => Value.ToString();
    }

    /// <summary>在 Unity 启动前为常用值类型安装更合适的比较器。</summary>
    internal class ComparerAutoRegister
    {
#if UNITY_5_6_OR_NEWER
        /// <summary>注册 Unity 常用值类型和基础类型的相等比较。</summary>
        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void AutoRegister()
        {
            BindableProperty<int>.Comparer = (a, b) => a == b;
            BindableProperty<float>.Comparer = (a, b) => a == b;
            BindableProperty<double>.Comparer = (a, b) => a == b;
            BindableProperty<string>.Comparer = (a, b) => a == b;
            BindableProperty<long>.Comparer = (a, b) => a == b;
            BindableProperty<UnityEngine.Vector2>.Comparer = (a, b) => a == b;
            BindableProperty<UnityEngine.Vector3>.Comparer = (a, b) => a == b;
            BindableProperty<UnityEngine.Vector4>.Comparer = (a, b) => a == b;
            BindableProperty<UnityEngine.Color>.Comparer = (a, b) => a == b;
            BindableProperty<UnityEngine.Color32>.Comparer =
                (a, b) => a.r == b.r && a.g == b.g && a.b == b.b && a.a == b.a;
            BindableProperty<UnityEngine.Bounds>.Comparer = (a, b) => a == b;
            BindableProperty<UnityEngine.Rect>.Comparer = (a, b) => a == b;
            BindableProperty<UnityEngine.Quaternion>.Comparer = (a, b) => a == b;
            BindableProperty<UnityEngine.Vector2Int>.Comparer = (a, b) => a == b;
            BindableProperty<UnityEngine.Vector3Int>.Comparer = (a, b) => a == b;
            BindableProperty<UnityEngine.BoundsInt>.Comparer = (a, b) => a == b;
            BindableProperty<UnityEngine.RangeInt>.Comparer = (a, b) => a.start == b.start && a.length == b.length;
            BindableProperty<UnityEngine.RectInt>.Comparer = (a, b) => a.Equals(b);
        }
#endif
    }

    #endregion

    #region EasyEvent

    /// <summary>
    /// 无参数简单事件的统一监听接口。
    /// Register 返回取消注册句柄，推荐把句柄交给生命周期扩展方法管理。
    /// </summary>
    public interface IEasyEvent
    {
        /// <summary>注册一个无参数监听器。</summary>
        IUnRegister Register(Action onEvent);
    }

    /// <summary>无参数简单事件。</summary>
    public class EasyEvent : IEasyEvent
    {
        private Action mOnEvent = () => { };

        /// <summary>注册监听器并返回取消注册句柄。</summary>
        public IUnRegister Register(Action onEvent)
        {
            mOnEvent += onEvent;
            return new CustomUnRegister(() => { UnRegister(onEvent); });
        }

        /// <summary>立即调用一次监听器，然后注册它以接收后续事件。</summary>
        public IUnRegister RegisterWithACall(Action onEvent)
        {
            onEvent.Invoke();
            return Register(onEvent);
        }

        /// <summary>移除监听器。</summary>
        public void UnRegister(Action onEvent) => mOnEvent -= onEvent;

        /// <summary>触发事件，按注册顺序调用监听器。</summary>
        public void Trigger() => mOnEvent?.Invoke();
    }

    /// <summary>携带一个参数的简单事件。</summary>
    public class EasyEvent<T> : IEasyEvent
    {
        private Action<T> mOnEvent = e => { };

        /// <summary>注册监听器并返回取消注册句柄。</summary>
        public IUnRegister Register(Action<T> onEvent)
        {
            mOnEvent += onEvent;
            return new CustomUnRegister(() => { UnRegister(onEvent); });
        }

        /// <summary>移除监听器。</summary>
        public void UnRegister(Action<T> onEvent) => mOnEvent -= onEvent;


        /// <summary>携带参数触发事件。</summary>
        public void Trigger(T t) => mOnEvent?.Invoke(t);

        // 显式实现 IEasyEvent，使 BindableProperty 等只关心“发生变化”，而不必处理参数。
        IUnRegister IEasyEvent.Register(Action onEvent)
        {
            return Register(Action);
            void Action(T _) => onEvent();
        }
    }

    /// <summary>携带两个参数的简单事件。</summary>
    public class EasyEvent<T, K> : IEasyEvent
    {
        private Action<T, K> mOnEvent = (t, k) => { };

        /// <summary>注册监听器并返回取消注册句柄。</summary>
        public IUnRegister Register(Action<T, K> onEvent)
        {
            mOnEvent += onEvent;
            return new CustomUnRegister(() => { UnRegister(onEvent); });
        }

        /// <summary>移除监听器。</summary>
        public void UnRegister(Action<T, K> onEvent) => mOnEvent -= onEvent;

        /// <summary>携带两个参数触发事件。</summary>
        public void Trigger(T t, K k) => mOnEvent?.Invoke(t, k);

        /// <summary>把带参数事件适配为无参数事件监听。</summary>
        IUnRegister IEasyEvent.Register(Action onEvent)
        {
            return Register(Action);
            void Action(T _, K __) => onEvent();
        }
    }

    /// <summary>携带三个参数的简单事件。</summary>
    public class EasyEvent<T, K, S> : IEasyEvent
    {
        private Action<T, K, S> mOnEvent = (t, k, s) => { };

        /// <summary>注册监听器并返回取消注册句柄。</summary>
        public IUnRegister Register(Action<T, K, S> onEvent)
        {
            mOnEvent += onEvent;
            return new CustomUnRegister(() => { UnRegister(onEvent); });
        }

        /// <summary>移除监听器。</summary>
        public void UnRegister(Action<T, K, S> onEvent) => mOnEvent -= onEvent;

        /// <summary>携带三个参数触发事件。</summary>
        public void Trigger(T t, K k, S s) => mOnEvent?.Invoke(t, k, s);

        /// <summary>把带参数事件适配为无参数事件监听。</summary>
        IUnRegister IEasyEvent.Register(Action onEvent)
        {
            return Register(Action);
            void Action(T _, K __, S ___) => onEvent();
        }
    }

    /// <summary>
    /// 按事件类型保存多个 IEasyEvent 实例。
    /// TypeEventSystem 使用它把具体事件类型映射为 EasyEvent&lt;T&gt;。
    /// </summary>
    public class EasyEvents
    {
        private static readonly EasyEvents mGlobalEvents = new EasyEvents();

        /// <summary>从全局事件表获取指定类型的事件。</summary>
        public static T Get<T>() where T : IEasyEvent => mGlobalEvents.GetEvent<T>();

        /// <summary>向全局事件表注册一个新的事件类型。</summary>
        public static void Register<T>() where T : IEasyEvent, new() => mGlobalEvents.AddEvent<T>();

        private readonly Dictionary<Type, IEasyEvent> mTypeEvents = new Dictionary<Type, IEasyEvent>();

        /// <summary>添加一个新的事件实例；重复类型会抛出 Dictionary 异常。</summary>
        public void AddEvent<T>() where T : IEasyEvent, new() => mTypeEvents.Add(typeof(T), new T());

        /// <summary>获取已注册事件；未注册时返回默认值。</summary>
        public T GetEvent<T>() where T : IEasyEvent
        {
            return mTypeEvents.TryGetValue(typeof(T), out var e) ? (T)e : default;
        }

        /// <summary>获取已注册事件，未注册时创建并添加一个。</summary>
        public T GetOrAddEvent<T>() where T : IEasyEvent, new()
        {
            var eType = typeof(T);
            if (mTypeEvents.TryGetValue(eType, out var e))
            {
                return (T)e;
            }

            var t = new T();
            mTypeEvents.Add(eType, t);
            return t;
        }
    }

    #endregion


    #region Event Extension

    /// <summary>
    /// 将多个 IEasyEvent 合并为一个无参数事件。
    /// 任意源事件触发时都会触发 OrEvent；OrEvent 被取消注册时会释放所有源事件监听。
    /// </summary>
    public class OrEvent : IUnRegisterList
    {
        /// <summary>添加一个源事件；源事件触发时转发为当前 OrEvent 的触发。</summary>
        public OrEvent Or(IEasyEvent easyEvent)
        {
            easyEvent.Register(Trigger).AddToUnregisterList(this);
            return this;
        }

        private Action mOnEvent = () => { };

        /// <summary>注册 OrEvent 监听器。</summary>
        public IUnRegister Register(Action onEvent)
        {
            mOnEvent += onEvent;
            return new CustomUnRegister(() => { UnRegister(onEvent); });
        }

        /// <summary>立即调用一次监听器，然后注册后续监听。</summary>
        public IUnRegister RegisterWithACall(Action onEvent)
        {
            onEvent.Invoke();
            return Register(onEvent);
        }

        /// <summary>移除监听器，并同时取消 OrEvent 对全部源事件的订阅。</summary>
        public void UnRegister(Action onEvent)
        {
            mOnEvent -= onEvent;
            this.UnRegisterAll();
        }

        /// <summary>转发任一源事件的触发。</summary>
        private void Trigger() => mOnEvent?.Invoke();

        /// <summary>OrEvent 持有的源事件取消注册句柄。</summary>
        public List<IUnRegister> UnregisterList { get; } = new List<IUnRegister>();
    }

    /// <summary>创建由两个简单事件组成的 OrEvent。</summary>
    public static class OrEventExtensions
    {
        /// <summary>组合当前事件和另一个事件，任意一个触发都会通知监听器。</summary>
        public static OrEvent Or(this IEasyEvent self, IEasyEvent e) => new OrEvent().Or(self).Or(e);
    }

    #endregion

#if UNITY_EDITOR
    /// <summary>Unity 编辑器菜单入口，用于打开 QFramework 安装页面。</summary>
    internal class EditorMenus
    {
        /// <summary>打开 QFramework 在线安装页面。</summary>
        [UnityEditor.MenuItem("QFramework/Install QFrameworkWithToolKits")]
        public static void InstallPackageKit() => UnityEngine.Application.OpenURL("https://qframework.cn/qf");
    }
#endif
}
