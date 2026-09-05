using System;
using System.Collections.Generic;
using NUnit.Framework;
using QFramework;

namespace CUC260905.Network.Tests
{
    /// <summary>
    /// 数据包传输量统计测试：仅对成功传输事件累计 PacketSize，失败事件不计；
    /// 非正数/非有限/溢出写入被拒绝且不改变累计值。
    /// </summary>
    public sealed class PacketTrafficStatsSystemTests
    {
        private PacketTrafficStatsModel mModel;
        private IPacketTrafficStatsSystem mSystem;

        [SetUp]
        public void SetUp()
        {
            mModel = new PacketTrafficStatsModel();
            TrafficStatsTestArchitecture.Configure(mModel);
            mSystem = TrafficStatsTestArchitecture.Interface.GetSystem<IPacketTrafficStatsSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            TrafficStatsTestArchitecture.Reset();
        }

        [Test]
        public void StartingTotal_IsZero()
        {
            Assert.That(mModel.TotalMegabits.Value, Is.EqualTo(0d));
            Assert.That(mSystem.TotalMegabits, Is.EqualTo(0d));
        }

        [Test]
        public void PacketTransmitted_AddsPacketSize()
        {
            PublishTransmitted(10f);

            Assert.That(mModel.TotalMegabits.Value, Is.EqualTo(10d));
        }

        [Test]
        public void PacketTransmitted_AccumulatesAcrossTransmissions()
        {
            PublishTransmitted(10f);
            PublishTransmitted(15f);
            PublishTransmitted(20f);

            Assert.That(mModel.TotalMegabits.Value, Is.EqualTo(45d));
        }

        [Test]
        public void PacketUnreachable_DoesNotAdd()
        {
            PublishUnreachable();

            Assert.That(mModel.TotalMegabits.Value, Is.EqualTo(0d));
        }

        [Test]
        public void MixedTransmissions_OnlySuccessfulOnesAdd()
        {
            PublishUnreachable();
            PublishTransmitted(12f);
            PublishTransmitted(3f);
            PublishUnreachable();

            Assert.That(mModel.TotalMegabits.Value, Is.EqualTo(15d));
        }

        [Test]
        public void NonPositiveOrNonFinite_AreRejectedWithoutChangingTotal()
        {
            Assert.That(mSystem.Add(0d), Is.False);
            Assert.That(mSystem.Add(-1d), Is.False);
            Assert.That(mSystem.Add(double.NaN), Is.False);
            Assert.That(mSystem.Add(double.PositiveInfinity), Is.False);

            Assert.That(mModel.TotalMegabits.Value, Is.EqualTo(0d));
        }

        [Test]
        public void Add_WhenResultWouldOverflow_IsRejectedWithoutChangingTotal()
        {
            TrafficStatsTestArchitecture.Reset();
            mModel = new PacketTrafficStatsModel(double.MaxValue);
            TrafficStatsTestArchitecture.Configure(mModel);
            mSystem = TrafficStatsTestArchitecture.Interface.GetSystem<IPacketTrafficStatsSystem>();

            bool result = mSystem.Add(double.MaxValue);

            Assert.That(result, Is.False);
            Assert.That(mModel.TotalMegabits.Value, Is.EqualTo(double.MaxValue));
        }

        [Test]
        public void TotalMegabits_NotifiesListenersOnChange()
        {
            List<double> changes = new List<double>();
            IUnRegister register = mModel.TotalMegabits.Register(changes.Add);

            mSystem.Add(10.5d);
            mSystem.Add(2.25d);

            Assert.That(changes, Is.EqualTo(new[] { 10.5d, 12.75d }));
            register.UnRegister();
        }

        [Test]
        public void OnInit_WithoutModel_ThrowsInvalidOperationException()
        {
            TrafficStatsTestArchitecture.Reset();
            TrafficStatsTestArchitecture.Configure(null);

            Assert.Throws<InvalidOperationException>(() =>
            {
                IPacketTrafficStatsSystem _ = TrafficStatsTestArchitecture.Interface
                    .GetSystem<IPacketTrafficStatsSystem>();
            });
        }

        private static void PublishTransmitted(float packetSize)
        {
            TrafficStatsTestArchitecture.Interface.SendEvent(new PacketTransmittedEvent(
                "user-a",
                "user-b",
                packetSize,
                new[] { "user-a", "server-a", "user-b" }));
        }

        private static void PublishUnreachable()
        {
            TrafficStatsTestArchitecture.Interface.SendEvent(new PacketUnreachableEvent(
                "user-a",
                "user-b",
                1f,
                PacketTransmissionResult.Unreachable));
        }

        private sealed class TrafficStatsTestArchitecture : Architecture<TrafficStatsTestArchitecture>
        {
            private static PacketTrafficStatsModel sModel;

            public static void Configure(PacketTrafficStatsModel model)
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
                // 系统通过构造函数持有统计模型，测试直接读取本地 mModel 字段，无需在架构内再注册模型。
                RegisterSystem<IPacketTrafficStatsSystem>(new PacketTrafficStatsSystem(sModel));
            }
        }
    }
}
