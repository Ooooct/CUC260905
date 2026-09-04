namespace CUC260905.Interaction
{
    /// <summary>一次意图调度的终态。</summary>
    public enum InteractionResultStatus
    {
        Handled,
        TargetUnavailable,
        SinkUnavailable,
        Rejected
    }

    /// <summary>调度层或业务接收器返回的轻量结果。</summary>
    public readonly struct InteractionResult
    {
        public readonly InteractionResultStatus Status;

        public InteractionResult(InteractionResultStatus status)
        {
            Status = status;
        }

        public bool IsHandled
        {
            get { return Status == InteractionResultStatus.Handled; }
        }
    }
}
