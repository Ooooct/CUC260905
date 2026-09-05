using NUnit.Framework;

namespace CUC260905.Network.Tests
{
    public sealed class GlobalLoadStateTests
    {
        [Test]
        public void AddUnreachablePenalty_AddsConfiguredAmount()
        {
            GlobalLoadState state = new GlobalLoadState();

            bool reachedGameOver = state.AddUnreachablePenalty(0.2f);

            Assert.That(reachedGameOver, Is.False);
            Assert.That(state.NormalizedLoad, Is.EqualTo(0.2f));
        }

        [Test]
        public void Decay_ReducesByConfiguredRatePerSecond()
        {
            GlobalLoadState state = new GlobalLoadState();
            state.AddUnreachablePenalty(0.6f);

            bool changed = state.Decay(2.0f, 0.05f);

            Assert.That(changed, Is.True);
            Assert.That(state.NormalizedLoad, Is.EqualTo(0.5f));
        }

        [Test]
        public void AddUnreachablePenalty_ReachingFullLoadTriggersGameOverOnlyOnce()
        {
            GlobalLoadState state = new GlobalLoadState();

            Assert.That(state.AddUnreachablePenalty(0.8f), Is.False);
            Assert.That(state.AddUnreachablePenalty(0.2f), Is.True);
            Assert.That(state.IsGameOver, Is.True);
            Assert.That(state.AddUnreachablePenalty(0.2f), Is.False);
        }
    }
}
