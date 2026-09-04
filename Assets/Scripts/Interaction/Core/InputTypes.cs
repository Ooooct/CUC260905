using UnityEngine;

namespace CUC260905.Interaction
{
    /// <summary>统一描述指针按键。</summary>
    public enum PointerButton
    {
        Left = 0,
        Right = 1,
        Middle = 2
    }

    /// <summary>设备层产生的指针状态变化。</summary>
    public enum PointerPhase
    {
        Down,
        Move,
        Up,
        Cancel
    }

    /// <summary>
    /// 设备无关的原始指针信号。
    /// 它不包含点击、拖拽、悬浮等交互语义。
    /// </summary>
    public readonly struct PointerSignal
    {
        /// <summary>同一输入设备中的稳定指针编号；鼠标固定为 0。</summary>
        public readonly int PointerId;

        /// <summary>触发本次状态变化的按键；Move 表示鼠标指针时固定为 Left。</summary>
        public readonly PointerButton Button;

        /// <summary>本帧发生的原始状态变化。</summary>
        public readonly PointerPhase Phase;

        /// <summary>当前屏幕像素坐标。</summary>
        public readonly Vector2 ScreenPosition;

        /// <summary>相对上一帧的屏幕像素位移。</summary>
        public readonly Vector2 ScreenDelta;

        /// <summary>采集此信号时的非缩放时间。</summary>
        public readonly float Time;

        public PointerSignal(
            int pointerId,
            PointerButton button,
            PointerPhase phase,
            Vector2 screenPosition,
            Vector2 screenDelta,
            float time)
        {
            PointerId = pointerId;
            Button = button;
            Phase = phase;
            ScreenPosition = screenPosition;
            ScreenDelta = screenDelta;
            Time = time;
        }
    }
}
