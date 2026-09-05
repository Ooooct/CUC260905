namespace CUC260905.Message
{
    /// <summary>发送给指定终端的不可变消息记录。</summary>
    public readonly struct SystemMessage
    {
        public readonly string TargetId;
        public readonly string Text;
        public readonly int Sequence;

        public SystemMessage(string targetId, string text, int sequence)
        {
            TargetId = targetId;
            Text = text;
            Sequence = sequence;
        }
    }
}
