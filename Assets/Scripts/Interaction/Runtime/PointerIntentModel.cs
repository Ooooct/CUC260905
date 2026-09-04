using System;
using System.Collections.Generic;
using QFramework;
using UnityEngine;

namespace CUC260905.Interaction
{
    /// <summary>将已解释的意图送往下一层，不包含任何业务调度规则。</summary>
    public interface IInteractionIntentEmitter
    {
        InteractionResult Emit<TIntent>(IInteractionTarget target, in TIntent intent)
            where TIntent : struct, IInteractionIntent;
    }

    /// <summary>保存指针会话状态，并将信号解释为交互意图。</summary>
    public interface IPointerIntentModel : IModel
    {
        void Process(in PointerSignal signal, in InteractionHit hit);

        void CancelAll();
    }

    /// <summary>
    /// 输入域 Model。按 PointerId 与按键保存会话、悬浮与拖拽状态。
    /// 不认识 Unity 生命周期或具体业务对象；意图只经 IInteractionIntentEmitter 离开本层。
    /// </summary>
    public sealed class PointerIntentModel : AbstractModel, IPointerIntentModel
    {
        // 由 Architecture 注入的调度 Utility；Model 只依赖其输出端口。
        private IInteractionIntentEmitter mEmitter;
        // 使用平方距离比较，避免每个 Move 信号进行开方。
        private readonly float mDragThresholdSqr;
        // 同一指针可同时持有不同按键；因此会话键必须包含 PointerId 与 Button。
        private readonly Dictionary<PointerSessionKey, PointerSession> mSessions =
            new Dictionary<PointerSessionKey, PointerSession>();
        // 悬浮只与指针关联，不区分按键。
        private readonly Dictionary<int, HoverState> mHoverStates =
            new Dictionary<int, HoverState>();
        // 枚举 Dictionary 时不能删除元素，先复制待处理键。
        private readonly List<PointerSessionKey> mSessionKeys = new List<PointerSessionKey>();
        // CancelAll 同理：先复制悬浮指针，再统一清理。
        private readonly List<int> mHoverPointerIds = new List<int>();

        /// <param name="dragThresholdPixels">按下后达到此屏幕距离才开始拖拽。</param>
        public PointerIntentModel(float dragThresholdPixels)
        {
            float threshold = Mathf.Max(0.0f, dragThresholdPixels);
            mDragThresholdSqr = threshold * threshold;
        }

        /// <summary>处理单个信号；调用方需先完成目标解析。</summary>
        public void Process(
            in PointerSignal signal,
            in InteractionHit hit)
        {
            // 同一输入快照生成唯一上下文，确保本次意图的时间、屏幕位置与射线一致。
            PointerFrame frame = new PointerFrame(signal, hit);
            switch (frame.Signal.Phase)
            {
                case PointerPhase.Down:
                    // 按下也可能首次进入对象，先同步 Hover 再建立按下会话。
                    UpdateHover(frame);
                    BeginSession(frame);
                    break;
                case PointerPhase.Move:
                    // Hover 反映当前位置；拖拽始终交给按下时捕获的目标。
                    UpdateHover(frame);
                    ProcessMove(frame);
                    break;
                case PointerPhase.Up:
                    // 先结束点击或拖拽，再处理本次释放导致的 Hover 离开，保证业务顺序直观。
                    ProcessUp(frame);
                    UpdateHover(frame);
                    break;
                case PointerPhase.Cancel:
                    // Cancel 表示设备序列不完整，不能继续判定 Click 或 Drag.End。
                    CancelPointer(frame);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(signal));
            }
        }

        /// <summary>输入源失效、切场景等边界发生时，取消所有未结束会话并清空悬浮。</summary>
        public void CancelAll()
        {
            // 复制键后再发 Cancel，避免遍历期间修改 mSessions。
            mSessionKeys.Clear();
            foreach (PointerSessionKey key in mSessions.Keys)
            {
                mSessionKeys.Add(key);
            }

            foreach (PointerSessionKey key in mSessionKeys)
            {
                PointerSession session = mSessions[key];
                EmitDragCancel(session);
            }

            // 非拖拽按下无需业务通知，直接丢弃即可。
            mSessions.Clear();

            // 悬浮状态也必须以 Exit 收束，否则提示层可能永久显示。
            mHoverPointerIds.Clear();
            foreach (int pointerId in mHoverStates.Keys)
            {
                mHoverPointerIds.Add(pointerId);
            }

            foreach (int pointerId in mHoverPointerIds)
            {
                HoverState hover = mHoverStates[pointerId];
                HoverIntent intent = new HoverIntent(HoverPhase.Exit, hover.Context, hover.Hit);
                mEmitter.Emit(hover.Target, intent);
            }

            mHoverStates.Clear();
        }

        protected override void OnInit()
        {
            mEmitter = this.GetUtility<IInteractionDispatchUtility>();
            if (mEmitter == null)
            {
                throw new InvalidOperationException(
                    "PointerIntentModel 初始化前必须注册 IInteractionDispatchUtility。");
            }
        }

        protected override void OnDeinit()
        {
            // 架构移除 Model 时收束未结束的拖拽和悬浮状态。
            CancelAll();
        }

        private void BeginSession(PointerFrame frame)
        {
            if (!frame.Hit.HasTarget)
            {
                // 空白处按下没有归属对象，后续 Up 不产生 Click。
                return;
            }

            // 新 Down 覆盖同键旧会话，防御输入源漏发 Up 的异常序列。
            PointerSessionKey key = new PointerSessionKey(frame.Signal.PointerId, frame.Signal.Button);
            mSessions[key] = new PointerSession
            {
                PressTarget = frame.Hit.Target,
                PressHit = frame.Hit,
                PressPosition = frame.Signal.ScreenPosition,
                LastContext = frame.Context,
                LastHit = frame.Hit
            };
        }

        private void ProcessMove(PointerFrame frame)
        {
            // 一个指针的不同按键各自判定拖拽。
            CollectSessionKeys(frame.Signal.PointerId);
            foreach (PointerSessionKey key in mSessionKeys)
            {
                PointerSession session = mSessions[key];
                // 记录最近状态，供之后 Cancel 提供准确上下文。
                session.LastContext = frame.Context;
                session.LastHit = frame.Hit;

                if (!session.IsDragging)
                {
                    Vector2 offset = frame.Signal.ScreenPosition - session.PressPosition;
                    if (offset.sqrMagnitude < mDragThresholdSqr)
                    {
                        // 未越过阈值仍是候选点击。
                        continue;
                    }

                    // 首次越阈值时锁定按下目标；指针移出对象仍继续向它发送拖拽。
                    session.IsDragging = true;
                    session.CapturedTarget = session.PressTarget;
                    DragIntent beginIntent = new DragIntent(
                        DragPhase.Begin,
                        frame.Context,
                        session.PressHit,
                        frame.Hit);
                    mEmitter.Emit(session.CapturedTarget, beginIntent);
                    // Begin 已携带当前位置，不在同一信号再发一次 Update。
                    continue;
                }

                // 拖拽已开始，CurrentHit 允许为空，但 Pointer.WorldRay 仍可用于自由拖动。
                DragIntent updateIntent = new DragIntent(
                    DragPhase.Update,
                    frame.Context,
                    session.PressHit,
                    frame.Hit);
                mEmitter.Emit(session.CapturedTarget, updateIntent);
            }
        }

        private void ProcessUp(PointerFrame frame)
        {
            PointerSessionKey key = new PointerSessionKey(frame.Signal.PointerId, frame.Signal.Button);
            if (!mSessions.TryGetValue(key, out PointerSession session))
            {
                // 未命中过可交互目标的 Down，不会建立会话。
                return;
            }

            // Up 也是当前会话的最后一帧状态，先保存以便逻辑完整。
            session.LastContext = frame.Context;
            session.LastHit = frame.Hit;
            if (session.IsDragging)
            {
                // 拖拽目标来自按下捕获，不受释放时射线命中的对象影响。
                DragIntent endIntent = new DragIntent(
                    DragPhase.End,
                    frame.Context,
                    session.PressHit,
                    frame.Hit);
                mEmitter.Emit(session.CapturedTarget, endIntent);
            }
            else if (frame.Hit.HasTarget && SameTarget(session.PressTarget, frame.Hit.Target))
            {
                // 只有同一可用目标按下、释放，才解释为点击。
                ClickIntent clickIntent = new ClickIntent(frame.Context, session.PressHit, frame.Hit);
                mEmitter.Emit(session.PressTarget, clickIntent);
            }

            // Up 是会话终点；无论是否形成业务意图都要回收。
            mSessions.Remove(key);
        }

        private void CancelPointer(PointerFrame frame)
        {
            // 一个 Cancel 终结此指针全部按键会话，防止多键状态残留。
            CollectSessionKeys(frame.Signal.PointerId);
            foreach (PointerSessionKey key in mSessionKeys)
            {
                PointerSession session = mSessions[key];
                session.LastContext = frame.Context;
                session.LastHit = frame.Hit;
                EmitDragCancel(session);
                mSessions.Remove(key);
            }

            if (!mHoverStates.TryGetValue(frame.Signal.PointerId, out HoverState hover))
            {
                // 此指针从未进入目标，不需要补发 Exit。
                return;
            }

            // Exit 使用上一个有效命中；Cancel 当前命中可能已经无目标。
            HoverIntent exitIntent = new HoverIntent(HoverPhase.Exit, frame.Context, hover.Hit);
            mEmitter.Emit(hover.Target, exitIntent);
            mHoverStates.Remove(frame.Signal.PointerId);
        }

        private void UpdateHover(PointerFrame frame)
        {
            int pointerId = frame.Context.PointerId;
            // 无有效目标等同于指针位于空白区域。
            IInteractionTarget currentTarget = frame.Hit.HasTarget ? frame.Hit.Target : null;
            if (mHoverStates.TryGetValue(pointerId, out HoverState previous))
            {
                if (SameTarget(previous.Target, currentTarget))
                {
                    if (currentTarget != null)
                    {
                        // 不重复发 Enter；仅更新状态，供未来 Exit 或 Cancel 使用。
                        mHoverStates[pointerId] = new HoverState(currentTarget, frame.Hit, frame.Context);
                    }

                    return;
                }

                // 目标切换或移入空白区时，旧对象先收到 Exit。
                HoverIntent exitIntent = new HoverIntent(HoverPhase.Exit, frame.Context, previous.Hit);
                mEmitter.Emit(previous.Target, exitIntent);
                mHoverStates.Remove(pointerId);
            }

            if (currentTarget == null)
            {
                return;
            }

            // 之后再让新对象收到 Enter，避免同一指针同时悬浮两个目标。
            HoverIntent enterIntent = new HoverIntent(HoverPhase.Enter, frame.Context, frame.Hit);
            mEmitter.Emit(currentTarget, enterIntent);
            mHoverStates[pointerId] = new HoverState(currentTarget, frame.Hit, frame.Context);
        }

        private void CollectSessionKeys(int pointerId)
        {
            mSessionKeys.Clear();
            // 只收集当前 PointerId，避免多指针互相影响。
            foreach (PointerSessionKey key in mSessions.Keys)
            {
                if (key.PointerId == pointerId)
                {
                    mSessionKeys.Add(key);
                }
            }
        }

        private void EmitDragCancel(PointerSession session)
        {
            if (!session.IsDragging)
            {
                // 仅按下未移动的会话没有可取消业务状态。
                return;
            }

            // 取消通知仍使用被捕获目标与最近一帧上下文。
            DragIntent cancelIntent = new DragIntent(
                DragPhase.Cancel,
                session.LastContext,
                session.PressHit,
                session.LastHit);
            mEmitter.Emit(session.CapturedTarget, cancelIntent);
        }

        private static bool SameTarget(IInteractionTarget first, IInteractionTarget second)
        {
            // 目标身份按引用判断，避免业务对象重写 Equals 影响输入语义。
            return ReferenceEquals(first, second);
        }

        /// <summary>同一帧的原始信号、解析结果与语义上下文；私有方法只接收此对象。</summary>
        private readonly struct PointerFrame
        {
            public readonly PointerSignal Signal;
            public readonly InteractionHit Hit;
            public readonly PointerContext Context;

            public PointerFrame(PointerSignal signal, InteractionHit hit)
            {
                Signal = signal;
                Hit = hit;
                Context = new PointerContext(signal, hit);
            }
        }

        private readonly struct PointerSessionKey : IEquatable<PointerSessionKey>
        {
            public readonly int PointerId;
            public readonly PointerButton Button;

            public PointerSessionKey(int pointerId, PointerButton button)
            {
                PointerId = pointerId;
                Button = button;
            }

            public bool Equals(PointerSessionKey other)
            {
                return PointerId == other.PointerId && Button == other.Button;
            }

            public override bool Equals(object obj)
            {
                return obj is PointerSessionKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (PointerId * 397) ^ (int)Button;
                }
            }
        }

        private sealed class PointerSession
        {
            // PressTarget 是点击判定与拖拽捕获来源。
            public IInteractionTarget PressTarget;
            // CapturedTarget 在 Drag.Begin 后固定，不随射线命中改变。
            public IInteractionTarget CapturedTarget;
            public InteractionHit PressHit;
            public Vector2 PressPosition;
            public PointerContext LastContext;
            public InteractionHit LastHit;
            public bool IsDragging;
        }

        private readonly struct HoverState
        {
            // Exit 需要回到旧目标，并携带旧命中与最近上下文。
            public readonly IInteractionTarget Target;
            public readonly InteractionHit Hit;
            public readonly PointerContext Context;

            public HoverState(IInteractionTarget target, InteractionHit hit, PointerContext context)
            {
                Target = target;
                Hit = hit;
                Context = context;
            }
        }
    }
}
