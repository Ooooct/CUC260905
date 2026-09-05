using QFramework;

namespace CUC260905.Message
{
    /// <summary>消息写入历史后发布，供对应终端立即追加显示。</summary>
    public readonly struct SystemMessagePublishedEvent : IEvent
    {
        public readonly SystemMessage Message;

        public SystemMessagePublishedEvent(SystemMessage message)
        {
            Message = message;
        }
    }
}
