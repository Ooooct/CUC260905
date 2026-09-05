using System;
using System.Collections.Generic;
using CUC260905.Game;
using CUC260905.Interaction;
using CUC260905.Placement;
using NUnit.Framework;
using QFramework;
using UnityEngine;

namespace CUC260905.Tests
{
    public sealed class PlacementSystemTests
    {
        private FakeMapper mMapper;
        private FakeInstantiator mInstantiator;
        private PointerFrameSource mFrameSource;
        private IPlacementModel mModel;
        private IPlacementSystem mSystem;
        private IGamePauseState mPauseState;
        private readonly List<GameObject> mCreated = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            PlacementTestArchitecture.Reset();
            mMapper = new FakeMapper();
            mInstantiator = new FakeInstantiator();
            mFrameSource = new PointerFrameSource();
            mModel = new PlacementModel();
            mPauseState = new GamePauseState();

            PlacementTestArchitecture.Configure(mMapper, mInstantiator, mFrameSource, mModel, mPauseState);
            mSystem = PlacementTestArchitecture.Interface.GetSystem<IPlacementSystem>();
        }

        [TearDown]
        public void TearDown()
        {
            PlacementTestArchitecture.Reset();
            foreach (GameObject go in mCreated)
            {
                if (go != null)
                {
                    UnityEngine.Object.DestroyImmediate(go);
                }
            }

            mCreated.Clear();
        }

        [Test]
        public void Begin_EntersPlacingAndSelectsPrefab()
        {
            GameObject prefab = NewObj("prefab");

            mSystem.Begin(prefab);

            Assert.That(mSystem.IsPlacing, Is.True);
            Assert.That(mModel.SelectedPrefab.Value, Is.SameAs(prefab));
        }

        [Test]
        public void LeftClickDown_PlacesOnceAndExits()
        {
            GameObject prefab = NewObj("prefab");
            mSystem.Begin(prefab);

            Vector2 screen = new Vector2(200.0f, 100.0f);
            mFrameSource.Write(new PointerFrameEvent(screen, LeftDown(screen)));
            mSystem.ProcessFrame(0.0f);

            Assert.That(mInstantiator.Instantiated, Is.EqualTo(1));
            Assert.That(mInstantiator.LastPrefab, Is.SameAs(prefab));
            Assert.That(mInstantiator.LastPosition, Is.EqualTo(new Vector3(2.0f, 1.0f, 0.0f)));
            Assert.That(mSystem.IsPlacing, Is.False, "单次放置成功后应退出放置模式");
        }

        [Test]
        public void RightClickDown_CancelsWithoutPlacing()
        {
            GameObject prefab = NewObj("prefab");
            mSystem.Begin(prefab);

            mFrameSource.Write(new PointerFrameEvent(Vector2.zero, RightDown()));
            mSystem.ProcessFrame(0.0f);

            Assert.That(mSystem.IsPlacing, Is.False);
            Assert.That(mInstantiator.Instantiated, Is.Zero);
        }

        [Test]
        public void LeftClickOverUI_DoesNotPlace()
        {
            mMapper.OverUI = _ => true;
            GameObject prefab = NewObj("prefab");
            mSystem.Begin(prefab);

            Vector2 screen = new Vector2(50.0f, 50.0f);
            mFrameSource.Write(new PointerFrameEvent(screen, LeftDown(screen)));
            mSystem.ProcessFrame(0.0f);

            Assert.That(mInstantiator.Instantiated, Is.Zero);
            Assert.That(mSystem.IsPlacing, Is.True, "UI 上的点击不应放置，也不应退出");
        }

        [Test]
        public void BeginWhilePlacing_SwitchesPrefabWithoutExiting()
        {
            GameObject first = NewObj("first");
            GameObject second = NewObj("second");

            mSystem.Begin(first);
            mSystem.Begin(second);

            Assert.That(mSystem.IsPlacing, Is.True);
            Assert.That(mModel.SelectedPrefab.Value, Is.SameAs(second));
        }

        [Test]
        public void ProcessFrame_UpdatesPointerWorldPosition()
        {
            GameObject prefab = NewObj("prefab");
            mSystem.Begin(prefab);

            mFrameSource.Write(new PointerFrameEvent(new Vector2(300.0f, 150.0f), EmptySignals()));
            mSystem.ProcessFrame(0.0f);

            Assert.That(mModel.PointerWorldPosition.Value, Is.EqualTo(new Vector3(3.0f, 1.5f, 0.0f)));
        }

        [Test]
        public void Gate_IsBlockedOnlyWhilePlacing()
        {
            IPlacementInputGate gate = PlacementTestArchitecture.Interface.GetUtility<IPlacementInputGate>();

            Assert.That(gate.IsBlocked, Is.False);

            mSystem.Begin(NewObj("prefab"));
            Assert.That(gate.IsBlocked, Is.True);

            mSystem.Cancel();
            Assert.That(gate.IsBlocked, Is.False);
        }

        [Test]
        public void TryPlace_NullPrefab_DoesNotInstantiate()
        {
            mSystem.Begin(NewObj("prefab"));
            mModel.SelectedPrefab.Value = null;

            mSystem.TryPlace(Vector3.zero);

            Assert.That(mInstantiator.Instantiated, Is.Zero);
            Assert.That(mSystem.IsPlacing, Is.False);
        }

        [Test]
        public void Begin_WhilePaused_DoesNotEnterPlacing()
        {
            mPauseState.IsPaused.Value = true;

            mSystem.Begin(NewObj("prefab"));

            Assert.That(mSystem.IsPlacing, Is.False, "暂停期间不应进入放置模式");
        }

        [Test]
        public void ProcessFrame_WhilePaused_FreezesPlacement()
        {
            mSystem.Begin(NewObj("prefab"));
            mPauseState.IsPaused.Value = true;

            Vector2 screen = new Vector2(200.0f, 100.0f);
            mFrameSource.Write(new PointerFrameEvent(screen, LeftDown(screen)));
            mSystem.ProcessFrame(0.0f);

            Assert.That(mInstantiator.Instantiated, Is.Zero, "暂停期间不应放置");
            Assert.That(mSystem.IsPlacing, Is.True, "暂停期间放置模式应保持冻结，不放置也不退出");
        }

        [Test]
        public void ProcessFrame_AfterResume_PlacesOnce()
        {
            mSystem.Begin(NewObj("prefab"));
            mPauseState.IsPaused.Value = true;
            mPauseState.IsPaused.Value = false;

            Vector2 screen = new Vector2(200.0f, 100.0f);
            mFrameSource.Write(new PointerFrameEvent(screen, LeftDown(screen)));
            mSystem.ProcessFrame(0.0f);

            Assert.That(mInstantiator.Instantiated, Is.EqualTo(1), "恢复后放置应重新生效");
            Assert.That(mSystem.IsPlacing, Is.False);
        }

        private GameObject NewObj(string name)
        {
            GameObject go = new GameObject(name);
            mCreated.Add(go);
            return go;
        }

        private static List<PointerSignal> LeftDown(Vector2 position)
        {
            return new List<PointerSignal> { Signal(PointerPhase.Down, PointerButton.Left, position) };
        }

        private static List<PointerSignal> RightDown()
        {
            return new List<PointerSignal> { Signal(PointerPhase.Down, PointerButton.Right, Vector2.zero) };
        }

        private static List<PointerSignal> EmptySignals()
        {
            return new List<PointerSignal>();
        }

        private static PointerSignal Signal(PointerPhase phase, PointerButton button, Vector2 position)
        {
            return new PointerSignal(0, button, phase, position, Vector2.zero, 0.0f);
        }

        private sealed class FakeMapper : IWorldPointerMapper
        {
            public Func<Vector2, bool> OverUI = _ => false;

            public bool TryMapScreenToWorld(Vector2 screenPosition, out Vector3 worldPosition)
            {
                worldPosition = new Vector3(screenPosition.x / 100.0f, screenPosition.y / 100.0f, 0.0f);
                return true;
            }

            public bool IsOverUI(Vector2 screenPosition)
            {
                return OverUI(screenPosition);
            }
        }

        private sealed class FakeInstantiator : IPlacementInstantiator
        {
            public int Instantiated;
            public GameObject LastPrefab;
            public Vector3 LastPosition;

            public GameObject Instantiate(GameObject prefab, Vector3 position, Quaternion rotation)
            {
                Instantiated++;
                LastPrefab = prefab;
                LastPosition = position;
                return prefab;
            }

            public void Destroy(GameObject instance)
            {
            }
        }

        private sealed class PlacementTestArchitecture : Architecture<PlacementTestArchitecture>
        {
            private static FakeMapper sMapper;
            private static FakeInstantiator sInstantiator;
            private static PointerFrameSource sFrameSource;
            private static IPlacementModel sModel;
            private static IGamePauseState sPauseState;

            public static void Configure(
                FakeMapper mapper,
                FakeInstantiator instantiator,
                PointerFrameSource frameSource,
                IPlacementModel model,
                IGamePauseState pauseState)
            {
                sMapper = mapper;
                sInstantiator = instantiator;
                sFrameSource = frameSource;
                sModel = model;
                sPauseState = pauseState;
            }

            public static void Reset()
            {
                if (mArchitecture != null)
                {
                    mArchitecture.Deinit();
                }

                sMapper = null;
                sInstantiator = null;
                sFrameSource = null;
                sModel = null;
                sPauseState = null;
            }

            protected override void Init()
            {
                RegisterUtility<IWorldPointerMapper>(sMapper);
                RegisterUtility<IPlacementInstantiator>(sInstantiator);
                RegisterUtility<IPointerFrameSink>(sFrameSource);
                RegisterUtility<IPointerFrameSource>(sFrameSource);
                RegisterUtility<IPlacementInputGate>(new PlacementInputGate(sModel));
                RegisterModel<IPlacementModel>(sModel);
                RegisterModel<IGamePauseState>(sPauseState);
                RegisterSystem<IPlacementSystem>(new PlacementSystem());
            }
        }
    }
}
