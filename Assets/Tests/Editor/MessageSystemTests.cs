using System.Collections.Generic;
using CUC260905.Message;
using NUnit.Framework;
using QFramework;

namespace CUC260905.Tests
{
    public sealed class MessageSystemTests
    {
        private IMessageSystem mSystem;

        [SetUp]
        public void SetUp()
        {
            MessageTestArchitecture.Reset();
            mSystem = MessageTestArchitecture.Interface.GetSystem<IMessageSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            MessageTestArchitecture.Reset();
        }

        [Test]
        public void Publish_StoresMessageAndRaisesEvent()
        {
            List<SystemMessage> published = new List<SystemMessage>();
            IUnRegister registration = MessageTestArchitecture.Interface
                .RegisterEvent<SystemMessagePublishedEvent>(messageEvent => published.Add(messageEvent.Message));

            bool result = mSystem.Publish("MainTerminal", "连接成功。");
            IReadOnlyList<SystemMessage> history = mSystem.GetHistory("MainTerminal");

            Assert.That(result, Is.True);
            Assert.That(history, Has.Count.EqualTo(1));
            Assert.That(history[0].Text, Is.EqualTo("连接成功。"));
            Assert.That(published, Has.Count.EqualTo(1));
            Assert.That(published[0].Sequence, Is.EqualTo(history[0].Sequence));
            registration.UnRegister();
        }

        [Test]
        public void Publish_KeepsHistoriesSeparatedByTarget()
        {
            mSystem.Publish("MainTerminal", "主终端消息");
            mSystem.Publish("SecondaryTerminal", "副终端消息");

            IReadOnlyList<SystemMessage> mainHistory = mSystem.GetHistory("MainTerminal");
            IReadOnlyList<SystemMessage> secondaryHistory = mSystem.GetHistory("SecondaryTerminal");

            Assert.That(mainHistory, Has.Count.EqualTo(1));
            Assert.That(mainHistory[0].Text, Is.EqualTo("主终端消息"));
            Assert.That(secondaryHistory, Has.Count.EqualTo(1));
            Assert.That(secondaryHistory[0].Text, Is.EqualTo("副终端消息"));
        }

        [Test]
        public void Publish_WhenHistoryExceedsCapacity_RemovesOldestMessage()
        {
            for (int index = 0; index <= 200; index++)
            {
                mSystem.Publish("MainTerminal", index.ToString());
            }

            IReadOnlyList<SystemMessage> history = mSystem.GetHistory("MainTerminal");

            Assert.That(history, Has.Count.EqualTo(200));
            Assert.That(history[0].Text, Is.EqualTo("1"));
            Assert.That(history[199].Text, Is.EqualTo("200"));
        }

        [Test]
        public void Publish_WhenTargetOrTextIsBlank_RejectsWithoutHistory()
        {
            Assert.That(mSystem.Publish("", "内容"), Is.False);
            Assert.That(mSystem.Publish("MainTerminal", " "), Is.False);
            Assert.That(mSystem.GetHistory("MainTerminal"), Is.Empty);
        }

        private sealed class MessageTestArchitecture : Architecture<MessageTestArchitecture>
        {
            public static void Reset()
            {
                if (mArchitecture != null)
                {
                    mArchitecture.Deinit();
                }
            }

            protected override void Init()
            {
                RegisterSystem<IMessageSystem>(new MessageSystem());
            }
        }
    }
}
