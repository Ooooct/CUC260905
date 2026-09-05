using CUC260905.Network;
using NUnit.Framework;

namespace CUC260905.Tests
{
    /// <summary>
    /// UserNodeSpawnScheduler：锁定"间隔随机、按序推进、耗尽停止"的生成节奏契约。
    /// </summary>
    public sealed class UserNodeSpawnSchedulerTests
    {
        private const double Epsilon = 1e-6;

        [Test]
        public void Reset_FirstSpawnAt_IsWithinConfiguredIntervalRange()
        {
            const float IntervalMin = 0.4f;
            const float IntervalMax = 0.8f;
            UserNodeSpawnScheduler scheduler = CreateScheduler(IntervalMin, IntervalMax, 11);
            const double Now = 1000.0;

            scheduler.Reset(Now);

            double delay = scheduler.NextSpawnAt - Now;
            Assert.That(delay, Is.GreaterThanOrEqualTo(IntervalMin - Epsilon));
            Assert.That(delay, Is.LessThanOrEqualTo(IntervalMax + Epsilon));
            Assert.That(scheduler.SpawnedCount, Is.EqualTo(0));
        }

        [Test]
        public void TryConsume_BeforeDueTime_ReturnsFalse()
        {
            UserNodeSpawnScheduler scheduler = CreateScheduler(0.4f, 0.8f, 5);
            const double Now = 0.0;
            scheduler.Reset(Now);

            bool consumed = scheduler.TryConsume(Now + 0.1, 10, out int index);

            Assert.That(consumed, Is.False);
            Assert.That(index, Is.EqualTo(-1));
            Assert.That(scheduler.SpawnedCount, Is.EqualTo(0));
        }

        [Test]
        public void TryConsume_AfterDueTime_ReturnsSequentialIndices()
        {
            UserNodeSpawnScheduler scheduler = CreateScheduler(0.05f, 0.05f, 3);
            const double Now = 0.0;
            scheduler.Reset(Now);

            bool first = scheduler.TryConsume(Now + 1.0, 10, out int firstIndex);
            bool second = scheduler.TryConsume(Now + 2.0, 10, out int secondIndex);
            bool third = scheduler.TryConsume(Now + 3.0, 10, out int thirdIndex);

            Assert.That(first, Is.True);
            Assert.That(second, Is.True);
            Assert.That(third, Is.True);
            Assert.That(firstIndex, Is.EqualTo(0));
            Assert.That(secondIndex, Is.EqualTo(1));
            Assert.That(thirdIndex, Is.EqualTo(2));
            Assert.That(scheduler.SpawnedCount, Is.EqualTo(3));
        }

        [Test]
        public void TryConsume_ExhaustedCandidateCount_ReturnsFalse()
        {
            UserNodeSpawnScheduler scheduler = CreateScheduler(0.05f, 0.05f, 7);
            const double Now = 0.0;
            scheduler.Reset(Now);

            bool first = scheduler.TryConsume(Now + 1.0, 2, out int firstIndex);
            bool second = scheduler.TryConsume(Now + 2.0, 2, out int secondIndex);
            bool third = scheduler.TryConsume(Now + 3.0, 2, out int thirdIndex);

            Assert.That(first, Is.True);
            Assert.That(second, Is.True);
            Assert.That(third, Is.False);
            Assert.That(firstIndex, Is.EqualTo(0));
            Assert.That(secondIndex, Is.EqualTo(1));
            Assert.That(thirdIndex, Is.EqualTo(-1));
        }

        [Test]
        public void Intervals_StayWithinConfiguredRange_AcrossManyConsumes()
        {
            const float IntervalMin = 0.25f;
            const float IntervalMax = 1.0f;
            UserNodeSpawnScheduler scheduler = CreateScheduler(IntervalMin, IntervalMax, 99);
            const int CandidateCount = 500;
            const double Now = 0.0;
            scheduler.Reset(Now);

            double passed = Now;
            int consumed = 0;
            for (int step = 1; step <= CandidateCount; step++)
            {
                passed = Now + step * 10.0;
                bool success = scheduler.TryConsume(passed, CandidateCount, out _);
                Assert.That(success, Is.True, $"第 {step} 次消耗本应成功。");
                // 每次消耗把下一次生成调度在"本次消耗时刻 + 随机间隔"上，
                // 因此间隔 = NextSpawnAt − 本次消耗时刻（不含 now 的跳变）。
                double delay = scheduler.NextSpawnAt - passed;
                Assert.That(delay, Is.GreaterThanOrEqualTo(IntervalMin - Epsilon));
                Assert.That(delay, Is.LessThanOrEqualTo(IntervalMax + Epsilon));
                consumed++;
            }

            Assert.That(consumed, Is.EqualTo(CandidateCount));
        }

        private static UserNodeSpawnScheduler CreateScheduler(float intervalMin, float intervalMax, int seed)
        {
            return new UserNodeSpawnScheduler(intervalMin, intervalMax, new System.Random(seed));
        }
    }
}
