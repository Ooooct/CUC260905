using CUC260905.Economy;
using CUC260905.Network;
using NUnit.Framework;
using QFramework;

namespace CUC260905.Tests
{
    /// <summary>
    /// 数据包传输奖励规则测试：仅对成功传输事件发放金币，失败事件不发；
    /// 余额写入失败（如溢出）时奖励逻辑保持静默，不抛异常。
    /// </summary>
    public sealed class PacketRewardSystemTests
    {
        private IEconomyModel mModel;
        private IPacketRewardSystem mRewardSystem;

        [SetUp]
        public void SetUp()
        {
            PacketRewardTestArchitecture.Reset();
            EconomyModel model = new EconomyModel();
            PacketRewardTestArchitecture.Configure(model);
            mModel = PacketRewardTestArchitecture.Interface.GetModel<IEconomyModel>();
            mRewardSystem = PacketRewardTestArchitecture.Interface.GetSystem<IPacketRewardSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            PacketRewardTestArchitecture.Reset();
        }

        [Test]
        public void DefaultReward_IsTwoCoinsPerTransmission()
        {
            // 数值设计 v1（docs/numerical-design.md §5）：每成功传输 2 金币。
            Assert.That(mRewardSystem.RewardPerTransmission, Is.EqualTo(2));
        }

        [Test]
        public void PacketTransmitted_AddsTwoCoins()
        {
            PublishTransmitted();

            Assert.That(mModel.Balance.Value, Is.EqualTo(2));
        }

        [Test]
        public void PacketTransmitted_AccumulatesAcrossTransmissions()
        {
            PublishTransmitted();
            PublishTransmitted();
            PublishTransmitted();

            Assert.That(mModel.Balance.Value, Is.EqualTo(6));
        }

        [Test]
        public void PacketUnreachable_DoesNotAddCoin()
        {
            PublishUnreachable();

            Assert.That(mModel.Balance.Value, Is.EqualTo(0));
        }

        [Test]
        public void MixedTransmissions_OnlySuccessfulOnesAddCoin()
        {
            PublishUnreachable();
            PublishTransmitted();
            PublishTransmitted();

            Assert.That(mModel.Balance.Value, Is.EqualTo(4));
        }

        [Test]
        public void PacketTransmitted_WhenAddFails_DoesNotThrowAndKeepsBalance()
        {
            PacketRewardTestArchitecture.Reset();
            EconomyModel model = new EconomyModel(int.MaxValue);
            PacketRewardTestArchitecture.Configure(model);
            mModel = PacketRewardTestArchitecture.Interface.GetModel<IEconomyModel>();
            mRewardSystem = PacketRewardTestArchitecture.Interface.GetSystem<IPacketRewardSystem>();

            Assert.DoesNotThrow(PublishTransmitted);
            Assert.That(mModel.Balance.Value, Is.EqualTo(int.MaxValue));
        }

        private void PublishTransmitted()
        {
            PacketRewardTestArchitecture.Interface.SendEvent(new PacketTransmittedEvent(
                "user-a",
                "user-b",
                1f,
                new[] { "user-a", "server-a", "user-b" }));
        }

        private void PublishUnreachable()
        {
            PacketRewardTestArchitecture.Interface.SendEvent(new PacketUnreachableEvent(
                "user-a",
                "user-b",
                1f,
                PacketTransmissionResult.Unreachable));
        }

        private sealed class PacketRewardTestArchitecture : Architecture<PacketRewardTestArchitecture>
        {
            private static EconomyModel sModel;

            public static void Configure(EconomyModel model)
            {
                sModel = model;
            }

            public static void Reset()
            {
                if (mArchitecture != null)
                {
                    mArchitecture.Deinit();
                }

                sModel = null;
            }

            protected override void Init()
            {
                RegisterModel<IEconomyModel>(sModel);
                RegisterSystem<IEconomySystem>(new EconomySystem(sModel));
                RegisterSystem<IPacketRewardSystem>(new PacketRewardSystem());
            }
        }
    }
}
