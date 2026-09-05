using System;
using CUC260905.Game;
using CUC260905.Interaction;
using NUnit.Framework;

namespace CUC260905.Tests
{
    public sealed class IntentDispatcherPauseTests
    {
        [Test]
        public void DispatchWhilePaused_RejectsOrdinaryClick()
        {
            GamePauseState pauseState = new GamePauseState();
            pauseState.IsPaused.Value = true;
            RecordingClickSink sink = new RecordingClickSink();
            IntentDispatcher dispatcher = new IntentDispatcher(
                new FixedSinkResolver(sink, null),
                pauseState);

            InteractionResult result = dispatcher.Dispatch(new TestTarget(), default(ClickIntent));

            Assert.That(result.Status, Is.EqualTo(InteractionResultStatus.Rejected));
            Assert.That(sink.CallCount, Is.Zero);
        }

        [Test]
        public void DispatchWhilePaused_ForwardsPauseAllowedDrag()
        {
            GamePauseState pauseState = new GamePauseState();
            pauseState.IsPaused.Value = true;
            RecordingPauseAllowedDragSink sink = new RecordingPauseAllowedDragSink();
            IntentDispatcher dispatcher = new IntentDispatcher(
                new FixedSinkResolver(null, sink),
                pauseState);

            InteractionResult result = dispatcher.Dispatch(new TestTarget(), default(DragIntent));

            Assert.That(result.Status, Is.EqualTo(InteractionResultStatus.Handled));
            Assert.That(sink.CallCount, Is.EqualTo(1));
        }

        private sealed class TestTarget : IInteractionTarget
        {
            public bool IsAvailable
            {
                get { return true; }
            }
        }

        private sealed class FixedSinkResolver : IIntentSinkResolver
        {
            private readonly IIntentSink<ClickIntent> mClickSink;
            private readonly IIntentSink<DragIntent> mDragSink;

            public FixedSinkResolver(
                IIntentSink<ClickIntent> clickSink,
                IIntentSink<DragIntent> dragSink)
            {
                mClickSink = clickSink;
                mDragSink = dragSink;
            }

            public bool TryResolve<TIntent>(
                IInteractionTarget target,
                out IIntentSink<TIntent> sink)
                where TIntent : struct, IInteractionIntent
            {
                if (typeof(TIntent) == typeof(ClickIntent) && mClickSink != null)
                {
                    sink = (IIntentSink<TIntent>)(object)mClickSink;
                    return true;
                }

                if (typeof(TIntent) == typeof(DragIntent) && mDragSink != null)
                {
                    sink = (IIntentSink<TIntent>)(object)mDragSink;
                    return true;
                }

                sink = null;
                return false;
            }
        }

        private sealed class RecordingClickSink : IIntentSink<ClickIntent>
        {
            public int CallCount { get; private set; }

            public InteractionResult Handle(IInteractionTarget target, in ClickIntent intent)
            {
                CallCount++;
                return new InteractionResult(InteractionResultStatus.Handled);
            }
        }

        private sealed class RecordingPauseAllowedDragSink :
            IIntentSink<DragIntent>, IPauseAllowedIntentSink
        {
            public int CallCount { get; private set; }

            public InteractionResult Handle(IInteractionTarget target, in DragIntent intent)
            {
                CallCount++;
                return new InteractionResult(InteractionResultStatus.Handled);
            }

            public bool CanHandleWhilePaused(Type intentType)
            {
                return intentType == typeof(DragIntent);
            }
        }
    }
}
