using System.Collections.Generic;
using NUnit.Framework;
using QFramework;
using UnityEngine;
using CUC260905.Interaction;

namespace CUC260905.Tests
{
    public sealed class PointerIntentModelTests
    {
        private RecordingDispatchUtility mDispatchUtility;
        private IPointerIntentModel mModel;
        private TestTarget mTarget;

        [SetUp]
        public void SetUp()
        {
            InteractionTestArchitecture.Reset();
            mDispatchUtility = new RecordingDispatchUtility();
            mModel = new PointerIntentModel(5.0f);
            mTarget = new TestTarget();

            InteractionTestArchitecture.Configure(mDispatchUtility, mModel);
            InteractionTestArchitecture.Interface.GetModel<IPointerIntentModel>();
        }

        [TearDown]
        public void TearDown()
        {
            InteractionTestArchitecture.Reset();
        }

        [Test]
        public void SameTargetPressAndRelease_EmitsClick()
        {
            mModel.Process(Signal(PointerPhase.Down, Vector2.zero), Hit(mTarget));
            mModel.Process(Signal(PointerPhase.Up, Vector2.zero), Hit(mTarget));

            Assert.That(mDispatchUtility.Events, Is.EqualTo(new[]
            {
                "Hover.Enter",
                "Click"
            }));
        }

        [Test]
        public void DragPastThreshold_EmitsBeginUpdateAndEnd()
        {
            mModel.Process(Signal(PointerPhase.Down, Vector2.zero), Hit(mTarget));
            mModel.Process(Signal(PointerPhase.Move, new Vector2(5.0f, 0.0f)), Hit(mTarget));
            mModel.Process(Signal(PointerPhase.Move, new Vector2(8.0f, 0.0f)), Hit(mTarget));
            mModel.Process(Signal(PointerPhase.Up, new Vector2(8.0f, 0.0f)), Hit(mTarget));

            Assert.That(mDispatchUtility.Events, Is.EqualTo(new[]
            {
                "Hover.Enter",
                "Drag.Begin",
                "Drag.Update",
                "Drag.End"
            }));
        }

        [Test]
        public void MovingOutOfTarget_EmitsHoverExit()
        {
            mModel.Process(Signal(PointerPhase.Move, Vector2.zero), Hit(mTarget));
            mModel.Process(Signal(PointerPhase.Move, Vector2.right), EmptyHit());

            Assert.That(mDispatchUtility.Events, Is.EqualTo(new[]
            {
                "Hover.Enter",
                "Hover.Exit"
            }));
        }

        private static PointerSignal Signal(PointerPhase phase, Vector2 position)
        {
            return new PointerSignal(
                0,
                PointerButton.Left,
                phase,
                position,
                Vector2.zero,
                0.0f);
        }

        private static InteractionHit Hit(IInteractionTarget target)
        {
            return new InteractionHit(
                target,
                new Ray(Vector3.back, Vector3.forward),
                Vector3.zero,
                Vector3.back,
                10.0f);
        }

        private static InteractionHit EmptyHit()
        {
            return new InteractionHit(
                null,
                new Ray(Vector3.back, Vector3.forward),
                Vector3.zero,
                Vector3.zero,
                0.0f);
        }

        private sealed class TestTarget : IInteractionTarget
        {
            public bool IsAvailable
            {
                get { return true; }
            }
        }

        private sealed class RecordingDispatchUtility : IInteractionDispatchUtility
        {
            public readonly List<string> Events = new List<string>();

            public InteractionResult Emit<TIntent>(IInteractionTarget target, in TIntent intent)
                where TIntent : struct, IInteractionIntent
            {
                if (intent is ClickIntent)
                {
                    Events.Add("Click");
                }
                else if (intent is DragIntent dragIntent)
                {
                    Events.Add($"Drag.{dragIntent.Phase}");
                }
                else if (intent is HoverIntent hoverIntent)
                {
                    Events.Add($"Hover.{hoverIntent.Phase}");
                }

                return new InteractionResult(InteractionResultStatus.Handled);
            }

            public InteractionResult Dispatch<TIntent>(IInteractionTarget target, in TIntent intent)
                where TIntent : struct, IInteractionIntent
            {
                return Emit(target, intent);
            }
        }

        private sealed class InteractionTestArchitecture : Architecture<InteractionTestArchitecture>
        {
            private static IInteractionDispatchUtility sDispatchUtility;
            private static IPointerIntentModel sModel;

            public InteractionTestArchitecture()
            {
            }

            public static void Configure(
                IInteractionDispatchUtility dispatchUtility,
                IPointerIntentModel model)
            {
                sDispatchUtility = dispatchUtility;
                sModel = model;
            }

            public static void Reset()
            {
                if (mArchitecture != null)
                {
                    mArchitecture.Deinit();
                }

                sDispatchUtility = null;
                sModel = null;
            }

            protected override void Init()
            {
                RegisterUtility<IInteractionDispatchUtility>(sDispatchUtility);
                RegisterModel<IPointerIntentModel>(sModel);
            }
        }
    }
}
