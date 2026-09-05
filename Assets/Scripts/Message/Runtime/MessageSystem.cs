using System;
using System.Collections.Generic;
using QFramework;

namespace CUC260905.Message
{
    /// <summary>
    /// 维护各终端独立的有限消息历史，并通过事件通知表现层。
    /// 历史属于运行时 System，随当前 GameArchitecture 生命周期结束而释放。
    /// </summary>
    public sealed class MessageSystem : AbstractSystem, IMessageSystem
    {
        private const int HistoryCapacityPerTarget = 200;

        private readonly Dictionary<string, List<SystemMessage>> mHistories =
            new Dictionary<string, List<SystemMessage>>(StringComparer.Ordinal);

        private int mNextSequence;

        protected override void OnInit()
        {
        }

        public bool Publish(string targetId, string text)
        {
            if (string.IsNullOrWhiteSpace(targetId) || string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            List<SystemMessage> history = GetOrCreateHistory(targetId);
            SystemMessage message = new SystemMessage(targetId, text, ++mNextSequence);
            history.Add(message);
            if (history.Count > HistoryCapacityPerTarget)
            {
                history.RemoveAt(0);
            }

            this.SendEvent(new SystemMessagePublishedEvent(message));
            return true;
        }

        public IReadOnlyList<SystemMessage> GetHistory(string targetId)
        {
            if (string.IsNullOrWhiteSpace(targetId) || !mHistories.TryGetValue(targetId, out List<SystemMessage> history))
            {
                return Array.Empty<SystemMessage>();
            }

            return new List<SystemMessage>(history);
        }

        private List<SystemMessage> GetOrCreateHistory(string targetId)
        {
            if (!mHistories.TryGetValue(targetId, out List<SystemMessage> history))
            {
                history = new List<SystemMessage>(HistoryCapacityPerTarget);
                mHistories.Add(targetId, history);
            }

            return history;
        }
    }
}
