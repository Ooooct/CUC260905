using QFramework;

namespace CUC260905.Feedback
{
    /// <summary>
    /// 圆形背景反馈（-1 层）的唯一写入口。
    /// 其他类只需传入位置、半径、颜色与时长，圆会在该时长内按 easeOutCubic 淡出到 0 后移除。
    /// 典型用法：<c>this.GetSystem&lt;IFeedbackSystem&gt;().ShowCircle(new CircleFeedbackRequest(position, radius, color, duration));</c>
    /// </summary>
    public interface IFeedbackSystem : ISystem
    {
        /// <summary>请求在背景层显示一个反馈圆。</summary>
        void ShowCircle(in CircleFeedbackRequest request);
    }
}
