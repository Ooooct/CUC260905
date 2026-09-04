using System.Collections.Generic;
using QFramework;
using UnityEngine;

namespace CUC260905.Interaction
{
    /// <summary>
    /// 输入设备 Adapter。实现向调用方提供的列表追加本帧信号，
    /// 不负责清空列表，也不解释交互语义。
    /// </summary>
    public interface IInputSourceUtility : IUtility
    {
        void CollectSignals(List<PointerSignal> destination, float unscaledTime);
    }

    /// <summary>
    /// 旧 Input Manager 的鼠标 Adapter。
    /// 只读取 Unity 输入并产生 PointerSignal，不解释点击、拖拽或悬浮。
    /// </summary>
    public sealed class LegacyInputUtility : IInputSourceUtility
    {
        private const int MousePointerId = 0;

        private bool mHasLastPosition;
        private Vector2 mLastPosition;

        /// <summary>
        /// 将本帧鼠标变化追加到 destination；调用方负责在每帧开始时清空列表。
        /// 移动信号固定使用 Left，表示鼠标指针本身，而非左键保持状态。
        /// </summary>
        public void CollectSignals(List<PointerSignal> destination, float unscaledTime)
        {
            Vector3 mousePosition = Input.mousePosition;
            Vector2 currentPosition = new Vector2(mousePosition.x, mousePosition.y);
            Vector2 screenDelta = mHasLastPosition
                ? currentPosition - mLastPosition
                : Vector2.zero;

            CollectButtonDown(destination, PointerButton.Left, 0, currentPosition, unscaledTime);
            CollectButtonDown(destination, PointerButton.Right, 1, currentPosition, unscaledTime);
            CollectButtonDown(destination, PointerButton.Middle, 2, currentPosition, unscaledTime);

            // 首帧的零位移 Move 用于建立初始 Hover 目标；后续只在坐标改变时发送。
            if (!mHasLastPosition || screenDelta.sqrMagnitude > 0.0f)
            {
                destination.Add(new PointerSignal(
                    MousePointerId,
                    PointerButton.Left,
                    PointerPhase.Move,
                    currentPosition,
                    screenDelta,
                    unscaledTime));
            }

            // Down、Move、Up 的固定顺序保证拖拽层可先接收最后位移，再结束会话。
            CollectButtonUp(destination, PointerButton.Left, 0, currentPosition, unscaledTime);
            CollectButtonUp(destination, PointerButton.Right, 1, currentPosition, unscaledTime);
            CollectButtonUp(destination, PointerButton.Middle, 2, currentPosition, unscaledTime);

            mLastPosition = currentPosition;
            mHasLastPosition = true;
        }

        private static void CollectButtonDown(
            List<PointerSignal> destination,
            PointerButton button,
            int mouseButton,
            Vector2 screenPosition,
            float unscaledTime)
        {
            if (!Input.GetMouseButtonDown(mouseButton))
            {
                return;
            }

            // 按下没有可归属的上一帧位移，因此 Delta 固定为零。
            destination.Add(new PointerSignal(
                MousePointerId,
                button,
                PointerPhase.Down,
                screenPosition,
                Vector2.zero,
                unscaledTime));
        }

        private static void CollectButtonUp(
            List<PointerSignal> destination,
            PointerButton button,
            int mouseButton,
            Vector2 screenPosition,
            float unscaledTime)
        {
            if (!Input.GetMouseButtonUp(mouseButton))
            {
                return;
            }

            // 释放位移已由本帧 Move 传递，避免拖拽层重复累计 Delta。
            destination.Add(new PointerSignal(
                MousePointerId,
                button,
                PointerPhase.Up,
                screenPosition,
                Vector2.zero,
                unscaledTime));
        }
    }
}
