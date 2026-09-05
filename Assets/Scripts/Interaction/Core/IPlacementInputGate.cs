using QFramework;

namespace CUC260905.Interaction
{
    /// <summary>
    /// 当 IsBlocked 为 true 时，InteractionInputSystem 整帧跳过目标解析与意图解释
    /// （世界点击/拖拽/悬浮被抑制），但每帧指针数据仍会写入 IPointerFrameSource。
    /// 契约定义在 Interaction 域，实现由放置域（PlacementInputGate）提供。
    /// </summary>
    public interface IPlacementInputGate : IUtility
    {
        bool IsBlocked { get; }
    }
}
