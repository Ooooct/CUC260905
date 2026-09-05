using QFramework;

namespace CUC260905.Interaction
{
    /// <summary>指针帧写入端口；由 InteractionInputSystem 每帧写入最新快照。</summary>
    public interface IPointerFrameSink : IUtility
    {
        void Write(in PointerFrameEvent frame);

        void Clear();
    }

    /// <summary>指针帧读取端口；放置等消费方从数据源读取最近一帧，不直接读 Unity Input。</summary>
    public interface IPointerFrameSource : IUtility
    {
        PointerFrameEvent? LatestFrame { get; }
    }
}
