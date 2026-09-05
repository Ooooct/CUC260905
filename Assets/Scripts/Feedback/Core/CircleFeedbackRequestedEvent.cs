using QFramework;

namespace CUC260905.Feedback
{
    /// <summary>反馈系统校验通过后发布，表现层据此生成并淡出背景圆。</summary>
    public readonly struct CircleFeedbackRequestedEvent : IEvent
    {
        public readonly CircleFeedbackRequest Request;

        public CircleFeedbackRequestedEvent(CircleFeedbackRequest request)
        {
            Request = request;
        }
    }
}
