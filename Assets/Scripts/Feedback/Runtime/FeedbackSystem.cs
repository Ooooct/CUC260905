using QFramework;
using UnityEngine;

namespace CUC260905.Feedback
{
    /// <summary>
    /// 校验并中转圆形反馈请求：负半径/负时长被收敛为 0，
    /// 合法请求以事件广播给表现层（FeedbackPresenter），本系统不直接持有渲染对象。
    /// </summary>
    public sealed class FeedbackSystem : AbstractSystem, IFeedbackSystem
    {
        protected override void OnInit()
        {
        }

        public void ShowCircle(in CircleFeedbackRequest request)
        {
            float radius = Mathf.Max(0f, request.Radius);
            float duration = Mathf.Max(0f, request.Duration);
            var validRequest = new CircleFeedbackRequest(
                request.Position,
                radius,
                request.Color,
                duration,
                request.ShowOffscreenIndicator);

            this.SendEvent(new CircleFeedbackRequestedEvent(validRequest));
        }
    }
}
