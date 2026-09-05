using System;

namespace CUC260905.Interaction
{
    /// <summary>
    /// 保存最近一帧指针快照的内存数据源。
    /// 写入与读取分别通过 IPointerFrameSink / IPointerFrameSource 两个端口暴露，
    /// 避免消费方误写。写入时把信号拷贝为快照，与输入系统的复用列表解耦。
    /// </summary>
    public sealed class PointerFrameSource : IPointerFrameSink, IPointerFrameSource
    {
        private PointerFrameEvent? mLatest;

        public PointerFrameEvent? LatestFrame
        {
            get { return mLatest; }
        }

        public void Write(in PointerFrameEvent frame)
        {
            PointerSignal[] signals = frame.Signals != null && frame.Signals.Count > 0
                ? new PointerSignal[frame.Signals.Count]
                : Array.Empty<PointerSignal>();

            for (int i = 0; i < signals.Length; i++)
            {
                signals[i] = frame.Signals[i];
            }

            mLatest = new PointerFrameEvent(frame.ScreenPosition, signals);
        }

        public void Clear()
        {
            mLatest = null;
        }
    }
}
