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
            PacketRewardTestArchitecture.Configure(model, new SequenceRandom(0, 1, 0));
            mModel = PacketRewardTestArchitecture.Interface.GetModel<IEconomyModel>();
            mRewardSystem = PacketRewardTestArchitecture.Interface.GetSystem<IPacketRewardSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            PacketRewardTestArchitecture.Reset();
        }

        [Test]
        public void DefaultRewardRange_IsThreeToFourCoinsPerTransmission()
        {
            Assert.That(mRewardSystem.MinimumRewardPerTransmission, Is.EqualTo(3));
            Assert.That(mRewardSystem.MaximumRewardPerTransmission, Is.EqualTo(4));
        }

        [Test]
        public void PacketTransmitted_AddsThreeCoinsWhenRandomSelectsLowerReward()
        {
            PublishTransmitted();

            Assert.That(mModel.Balance.Value, Is.EqualTo(3));
        }

        [Test]
        public void PacketTransmitted_AddsFourCoinsWhenRandomSelectsUpperReward()
        {
            PacketRewardTestArchitecture.Reset();
            EconomyModel model = new EconomyModel();
            PacketRewardTestArchitecture.Configure(model, new SequenceRandom(1));
            mModel = PacketRewardTestArchitecture.Interface.GetModel<IEconomyModel>();

            PublishTransmitted();

            Assert.That(mModel.Balance.Value, Is.EqualTo(4));
        }

        [Test]
        public void PacketTransmitted_AccumulatesAcrossTransmissions()
        {
            PublishTransmitted();
            PublishTransmitted();
            PublishTransmitted();

            Assert.That(mModel.Balance.Value, Is.EqualTo(10));
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

            Assert.That(mModel.Balance.Value, Is.EqualTo(7));
        }

        [Test]
        public void PacketTransmitted_WhenAddFails_DoesNotThrowAndKeepsBalance()
        {
            PacketRewardTestArchitecture.Reset();
            EconomyModel model = new EconomyModel(int.MaxValue);
            PacketRewardTestArchitecture.Configure(model, new SequenceRandom(0));
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

            private static System.Random sRewardRandom;

            public static void Configure(EconomyModel model, System.Random rewardRandom)
            {
                sModel = model;
                sRewardRandom = rewardRandom;
            }

            public static void Reset()
            {
                if (mArchitecture != null)
                {
                    mArchitecture.Deinit();
                }

                sModel = null;
                sRewardRandom = null;
            }

            protected override void Init()
            {
                RegisterModel<IEconomyModel>(sModel);
                RegisterSystem<IEconomySystem>(new EconomySystem(sModel));
                RegisterSystem<IPacketRewardSystem>(new PacketRewardSystem(sRewardRandom));
            }
        }

        private sealed class SequenceRandom : System.Random
        {
            private readonly int[] mValues;
            private int mIndex;

            public SequenceRandom(params int[] values)
            {
                mValues = values;
            }

            public override int Next(int minValue, int maxValue)
            {
                int value = mValues[mIndex % mValues.Length];
                mIndex++;
                return minValue + value % (maxValue - minValue);
            }
        }
    }
}
