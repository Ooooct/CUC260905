using System.Collections.Generic;
using CUC260905.Interaction;
using CUC260905.Network;
using NUnit.Framework;
using QFramework;
using UnityEngine;

namespace CUC260905.Tests
{
    /// <summary>
    /// UserNodeController：用户节点与服务器节点的关键差异——点击不发布任何事件。
    /// 两个用例分别锁定行为契约（点击被消费、不发 ServerNodeClickedEvent）
    /// 与类型契约（不实现 ICanSendEvent，编译期即保证"点击不发布"）。
    /// </summary>
    public sealed class UserNodeControllerTests
    {
        private UserNodeController mController;

        [SetUp]
        public void SetUp()
        {
            mController = new GameObject("UserNodeTest").AddComponent<UserNodeController>();
        }

        [TearDown]
        public void TearDown()
        {
            if (mController != null)
            {
                Object.DestroyImmediate(mController.gameObject);
            }

            UserNodeTestArchitecture.Reset();
        }

        [Test]
        public void OnClick_ConsumesClickWithoutPublishingServerNodeClickedEvent()
        {
            List<ServerNodeClickedEvent> clickedEvents = new List<ServerNodeClickedEvent>();
            IUnRegister register = UserNodeTestArchitecture.Interface.RegisterEvent<ServerNodeClickedEvent>(
                clickedEvent => clickedEvents.Add(clickedEvent));

            InteractionResult result = mController.OnClick(default);

            Assert.That(result.IsHandled, Is.True);
            Assert.That(clickedEvents, Is.Empty);
            register.UnRegister();
        }

        [Test]
        public void Controller_IsNotAnEventSender()
        {
            // 与 ServerNodeController（IController, ICanSendEvent, IClickable）不同：
            // 用户节点不实现 ICanSendEvent，从类型层面保证点击不发布事件。
            Assert.That(mController, Is.Not.AssignableTo<ICanSendEvent>());
        }

        private sealed class UserNodeTestArchitecture : Architecture<UserNodeTestArchitecture>
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
            }
        }
    }
}
