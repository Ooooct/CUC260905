using QFramework;

namespace CUC260905.Game
{
    /// <summary>
    /// 进入暂停时发送：模拟时间（数据包、生成、负载、淡出动画）冻结，
    /// 相机浏览保留，世界交互（点击/拖拽连线/放置/右键删边）被抑制。
    /// </summary>
    public readonly struct GamePausedEvent : IEvent
    {
    }

    /// <summary>取消暂停时发送：模拟时间恢复，世界交互重新生效。</summary>
    public readonly struct GameResumedEvent : IEvent
    {
    }
}
