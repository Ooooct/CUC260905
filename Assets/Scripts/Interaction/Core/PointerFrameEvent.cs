using System.Collections.Generic;
using QFramework;
using UnityEngine;

namespace CUC260905.Interaction
{
    /// <summary>
    /// Interaction 输入系统每帧发布的指针帧快照。
    /// Signals 是该帧的信号集合；写入 PointerFrameSource 时会拷贝为快照，可安全长期持有。
    /// </summary>
    public readonly struct PointerFrameEvent : IEvent
    {
        /// <summary>本帧最近一次已知的屏幕像素坐标（Move/Down/Up 都会刷新）。</summary>
        public readonly Vector2 ScreenPosition;

        /// <summary>本帧采集到的原始信号（可能为空）。</summary>
        public readonly IReadOnlyList<PointerSignal> Signals;

        public PointerFrameEvent(Vector2 screenPosition, IReadOnlyList<PointerSignal> signals)
        {
            ScreenPosition = screenPosition;
            Signals = signals;
        }
    }
}
