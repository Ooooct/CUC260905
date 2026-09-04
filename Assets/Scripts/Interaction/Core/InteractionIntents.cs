using UnityEngine;

namespace CUC260905.Interaction
{
    /// <summary>拖拽交互的阶段。</summary>
    public enum DragPhase
    {
        Begin,
        Update,
        End,
        Cancel
    }

    /// <summary>悬浮交互的阶段。</summary>
    public enum HoverPhase
    {
        Enter,
        Exit
    }

    /// <summary>交互对象收到的设备无关指针上下文。</summary>
    public readonly struct PointerContext
    {
        public readonly int PointerId;
        public readonly PointerButton Button;
        public readonly Vector2 ScreenPosition;
        public readonly Vector2 ScreenDelta;
        public readonly Ray WorldRay;
        public readonly float Time;

        public PointerContext(in PointerSignal signal, in InteractionHit hit)
        {
            PointerId = signal.PointerId;
            Button = signal.Button;
            ScreenPosition = signal.ScreenPosition;
            ScreenDelta = signal.ScreenDelta;
            WorldRay = hit.Ray;
            Time = signal.Time;
        }
    }

    /// <summary>同一目标按下并释放后产生的点击意图。</summary>
    public readonly struct ClickIntent : IInteractionIntent
    {
        public readonly PointerContext Pointer;
        public readonly InteractionHit PressHit;
        public readonly InteractionHit ReleaseHit;

        public ClickIntent(
            PointerContext pointer,
            InteractionHit pressHit,
            InteractionHit releaseHit)
        {
            Pointer = pointer;
            PressHit = pressHit;
            ReleaseHit = releaseHit;
        }
    }

    /// <summary>拖拽生命周期产生的意图；CurrentHit 可以不含目标但始终尽量携带当前 Ray。</summary>
    public readonly struct DragIntent : IInteractionIntent
    {
        public readonly DragPhase Phase;
        public readonly PointerContext Pointer;
        public readonly InteractionHit PressHit;
        public readonly InteractionHit CurrentHit;

        public DragIntent(
            DragPhase phase,
            PointerContext pointer,
            InteractionHit pressHit,
            InteractionHit currentHit)
        {
            Phase = phase;
            Pointer = pointer;
            PressHit = pressHit;
            CurrentHit = currentHit;
        }
    }

    /// <summary>逻辑对象进入或离开悬浮状态时产生的意图。</summary>
    public readonly struct HoverIntent : IInteractionIntent
    {
        public readonly HoverPhase Phase;
        public readonly PointerContext Pointer;
        public readonly InteractionHit Hit;

        public HoverIntent(
            HoverPhase phase,
            PointerContext pointer,
            InteractionHit hit)
        {
            Phase = phase;
            Pointer = pointer;
            Hit = hit;
        }
    }
}
