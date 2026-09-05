using QFramework;
using UnityEngine;

namespace CUC260905.Browsing
{
    /// <summary>滚轮输入端口；浏览系统只依赖该端口，不直接读 Unity Input。</summary>
    public interface IScrollWheelSource : IUtility
    {
        /// <summary>本帧滚轮位移（y &gt; 0 向上滚＝放大）。</summary>
        Vector2 ScrollDelta { get; }
    }

    /// <summary>旧 Input Manager 的滚轮 Adapter。</summary>
    public sealed class ScrollWheelSource : IScrollWheelSource
    {
        public Vector2 ScrollDelta
        {
            get { return Input.mouseScrollDelta; }
        }
    }
}
