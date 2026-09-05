using System.Collections.Generic;
using CUC260905.Message;
using CUC260905.Network;
using NUnit.Framework;
using QFramework;

namespace CUC260905.Tests
{
    public sealed class ConnectionFeedbackTests
    {
        private static readonly ConnectionVerdict[] FailureVerdicts =
        {
            ConnectionVerdict.InvalidNodeId,
            ConnectionVerdict.NodeNotRegistered,
            ConnectionVerdict.SameNode,
            ConnectionVerdict.UserToUserForbidden,
            ConnectionVerdict.AlreadyConnected,
            ConnectionVerdict.NodePositionUnavailable,
            ConnectionVerdict.CrossingEdge,
            ConnectionVerdict.TopologyWriteFailed,
            ConnectionVerdict.MaxConnectionsExceeded
        };

        // ---- GetFailureReason ----

        [Test]
        public void GetFailureReason_EveryFailureVerdict_HasNonEmptyText()
        {
            foreach (ConnectionVerdict verdict in FailureVerdicts)
            {
                string reason = ConnectionFeedback.GetFailureReason(verdict);
                Assert.That(reason, Is.Not.Null, $"裁决 {verdict} 应提供失败原因文本");
                Assert.That(reason, Is.Not.Empty, $"裁决 {verdict} 应提供非空失败原因文本");
            }
        }

        [Test]
        public void GetFailureReason_Success_ReturnsNull()
        {
            Assert.That(ConnectionFeedback.GetFailureReason(ConnectionVerdict.Success), Is.Null);
        }

        [Test]
        public void GetFailureReason_MaxConnectionsExceeded_MentionsLimit()
        {
            Assert.That(ConnectionFeedback.GetFailureReason(ConnectionVerdict.MaxConnectionsExceeded),
                Does.Contain("最大连接数"));
        }

        // ---- TryPublishFailure ----

        [Test]
        public void TryPublishFailure_EveryFailureVerdict_PublishesReasonToTarget()
        {
            foreach (ConnectionVerdict verdict in FailureVerdicts)
            {
                FakeMessageSystem messages = new FakeMessageSystem();
                Assert.That(ConnectionFeedback.TryPublishFailure(messages, "MainTerminal", verdict), Is.True,
                    $"裁决 {verdict} 应发布失败原因");
                Assert.That(messages.LastTargetId, Is.EqualTo("MainTerminal"));
                Assert.That(messages.LastText, Is.EqualTo(ConnectionFeedback.GetFailureReason(verdict)));
            }
        }

        [Test]
        public void TryPublishFailure_Success_DoesNotPublish()
        {
            FakeMessageSystem messages = new FakeMessageSystem();
            Assert.That(ConnectionFeedback.TryPublishFailure(messages, "MainTerminal", ConnectionVerdict.Success), Is.False);
            Assert.That(messages.PublishCount, Is.Zero);
        }

        [Test]
        public void TryPublishFailure_NullMessageSystem_ReturnsFalse()
        {
            Assert.That(ConnectionFeedback.TryPublishFailure(null, "MainTerminal", ConnectionVerdict.CrossingEdge), Is.False);
        }

        [Test]
        public void TryPublishFailure_BlankTargetId_ReturnsFalse()
        {
            FakeMessageSystem messages = new FakeMessageSystem();
            Assert.That(ConnectionFeedback.TryPublishFailure(messages, " ", ConnectionVerdict.CrossingEdge), Is.False);
            Assert.That(messages.PublishCount, Is.Zero);
        }

        private sealed class FakeMessageSystem : IMessageSystem
        {
            public int PublishCount;
            public string LastTargetId;
            public string LastText;
            private readonly List<SystemMessage> mHistory = new List<SystemMessage>();

            public bool Publish(string targetId, string text)
            {
                PublishCount++;
                LastTargetId = targetId;
                LastText = text;
                mHistory.Add(new SystemMessage(targetId, text, mHistory.Count + 1));
                return true;
            }

            public IReadOnlyList<SystemMessage> GetHistory(string targetId)
            {
                return new List<SystemMessage>(mHistory);
            }

            // ---- ISystem 存根：本测试只使用 Publish / GetHistory ----
            public bool Initialized { get; set; }

            public void Init()
            {
                Initialized = true;
            }

            public void Deinit()
            {
                Initialized = false;
            }

            public IArchitecture GetArchitecture()
            {
                return null;
            }

            public void SetArchitecture(IArchitecture architecture)
            {
            }
        }
    }
}
