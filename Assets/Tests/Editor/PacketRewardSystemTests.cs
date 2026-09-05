using CUC260905.Economy;
using CUC260905.Network;
using NUnit.Framework;
using QFramework;

namespace CUC260905.Tests
{
    /// <summary>
    /// 数据包传输奖励规则测试：仅对成功传输事件发放金币（随机 2~3、各 50%），失败事件不发；
    /// 余额写入失败（如溢出）时奖励逻辑保持静默，不抛异常。
    /// </summary>
    public sealed class PacketRewardSystemTests
    {
        private const int MinReward = 2;
        private const int MaxReward = 3;

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
        public void RewardRange_IsTwoToThreeCoinsPerTransmission()
        {
            Assert.That(mRewardSystem.MinRewardPerTransmission, Is.EqualTo(MinReward));
            Assert.That(mRewardSystem.MaxRewardPerTransmission, Is.EqualTo(MaxReward));
        }

        [Test]
        public void RollReward_OnlyReturnsTwoOrThree()
        {
            PacketRewardSystem system = new PacketRewardSystem(new System.Random(12345));
            for (int i = 0; i < 1000; i++)
            {
                int reward = system.RollRewardPerTransmission();
                Assert.That(reward, Is.InRange(MinReward, MaxReward));
            }
        }

        [Test]
        public void RollReward_DistributionIsApproximatelyFiftyFifty()
        {
            PacketRewardSystem system = new PacketRewardSystem(new System.Random(12345));
            const int sampleCount = 1000;
            int countTwo = 0;
            for (int i = 0; i < sampleCount; i++)
            {
                if (system.RollRewardPerTransmission() == MinReward)
                {
                    countTwo++;
                }
            }

            // 固定种子下结果确定：2 与 3 应大致各占一半（宽松区间，避免断言耦合具体序列）。
            Assert.That(countTwo, Is.InRange(400, 600));
        }

        [Test]
        public void PacketTransmitted_AddsTwoOrThreeCoins()
        {
            PublishTransmitted();

            Assert.That(mModel.Balance.Value, Is.InRange(MinReward, MaxReward));
        }

        [Test]
        public void PacketTransmitted_AccumulatesWithinExpectedRange()
        {
            const int transmissionCount = 10;
            for (int i = 0; i < transmissionCount; i++)
            {
                PublishTransmitted();
            }

            Assert.That(mModel.Balance.Value,
                Is.InRange(MinReward * transmissionCount, MaxReward * transmissionCount));
        }

        [Test]
        public void PacketTransmitted_WithDeterministicRandom_AddsExpectedTotal()
        {
            PacketRewardTestArchitecture.Reset();
            EconomyModel model = new EconomyModel();
            PacketRewardTestArchitecture.Configure(model, new AlternatingRewardRandom());
            mModel = PacketRewardTestArchitecture.Interface.GetModel<IEconomyModel>();
            mRewardSystem = PacketRewardTestArchitecture.Interface.GetSystem<IPacketRewardSystem>();

            // 交替序列 0,1,0,1 → 奖励 2,3,2,3，共 4 次成功传输应为 10。
            PublishTransmitted();
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

            Assert.That(mModel.Balance.Value, Is.InRange(MinReward * 2, MaxReward * 2));
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

        /// <summary>
        /// 固定输出 0、1 交替的随机源：让奖励序列确定（2、3、2、3…），用于精确断言。
        /// </summary>
        private sealed class AlternatingRewardRandom : System.Random
        {
            private int mNext;

            public override int Next(int maxValue)
            {
                if (maxValue <= 0)
                {
                    return 0;
                }

                int result = mNext;
                mNext = 1 - mNext;
                return result;
            }
        }

        private sealed class PacketRewardTestArchitecture : Architecture<PacketRewardTestArchitecture>
        {
            private static EconomyModel sModel;
            private static System.Random sRandom;

            public static void Configure(EconomyModel model)
            {
                Configure(model, null);
            }

            public static void Configure(EconomyModel model, System.Random random)
            {
                sModel = model;
                sRandom = random;
            }

            public static void Reset()
            {
                if (mArchitecture != null)
                {
                    mArchitecture.Deinit();
                }

                sModel = null;
                sRandom = null;
            }

            protected override void Init()
            {
                RegisterModel<IEconomyModel>(sModel);
                RegisterSystem<IEconomySystem>(new EconomySystem(sModel));
                RegisterSystem<IPacketRewardSystem>(new PacketRewardSystem(sRandom));
            }
        }
    }
}
