using System.Collections.Generic;
using CUC260905.Economy;
using NUnit.Framework;
using QFramework;

namespace CUC260905.Tests
{
    public sealed class EconomySystemTests
    {
        private IEconomyModel mModel;
        private IEconomySystem mSystem;

        [SetUp]
        public void SetUp()
        {
            EconomyTestArchitecture.Reset();
            EconomyModel model = new EconomyModel();
            EconomyTestArchitecture.Configure(model);
            mModel = EconomyTestArchitecture.Interface.GetModel<IEconomyModel>();
            mSystem = EconomyTestArchitecture.Interface.GetSystem<IEconomySystem>();
        }

        [TearDown]
        public void TearDown()
        {
            EconomyTestArchitecture.Reset();
        }

        [Test]
        public void StartingBalance_IsZero()
        {
            Assert.That(mModel.Balance.Value, Is.EqualTo(0));
        }

        [Test]
        public void Add_IncreasesBalanceAndReturnsTrue()
        {
            bool result = mSystem.Add(100);

            Assert.That(result, Is.True);
            Assert.That(mModel.Balance.Value, Is.EqualTo(100));
        }

        [Test]
        public void Consume_WhenSufficient_DeductsAndReturnsTrue()
        {
            mSystem.Add(100);

            bool result = mSystem.Consume(40);

            Assert.That(result, Is.True);
            Assert.That(mModel.Balance.Value, Is.EqualTo(60));
        }

        [Test]
        public void Consume_WhenExactBalance_ReturnsTrueAndLeavesZero()
        {
            mSystem.Add(50);

            bool result = mSystem.Consume(50);

            Assert.That(result, Is.True);
            Assert.That(mModel.Balance.Value, Is.EqualTo(0));
        }

        [Test]
        public void Consume_WhenInsufficient_DoesNotDeductAndReturnsFalse()
        {
            mSystem.Add(30);

            bool result = mSystem.Consume(100);

            Assert.That(result, Is.False);
            Assert.That(mModel.Balance.Value, Is.EqualTo(30));
        }

        [Test]
        public void Consume_WhenBalanceZero_ReturnsFalse()
        {
            bool result = mSystem.Consume(1);

            Assert.That(result, Is.False);
            Assert.That(mModel.Balance.Value, Is.EqualTo(0));
        }

        [Test]
        public void NonPositiveAmounts_AreRejectedWithoutSideEffects()
        {
            Assert.That(mSystem.Add(0), Is.False);
            Assert.That(mSystem.Add(-5), Is.False);
            Assert.That(mSystem.Consume(0), Is.False);
            Assert.That(mSystem.Consume(-5), Is.False);
            Assert.That(mModel.Balance.Value, Is.EqualTo(0));
        }

        [Test]
        public void Add_WhenResultWouldOverflow_IsRejectedWithoutChangingBalance()
        {
            EconomyTestArchitecture.Reset();
            EconomyModel model = new EconomyModel(int.MaxValue);
            EconomyTestArchitecture.Configure(model);
            mModel = EconomyTestArchitecture.Interface.GetModel<IEconomyModel>();
            mSystem = EconomyTestArchitecture.Interface.GetSystem<IEconomySystem>();

            bool result = mSystem.Add(1);

            Assert.That(result, Is.False);
            Assert.That(mModel.Balance.Value, Is.EqualTo(int.MaxValue));
        }

        [Test]
        public void Balance_NotifiesListenersOnChange()
        {
            List<int> changes = new List<int>();
            IUnRegister register = mModel.Balance.Register(changes.Add);

            mSystem.Add(10);
            mSystem.Consume(4);

            Assert.That(changes, Is.EqualTo(new[] { 10, 6 }));
            register.UnRegister();
        }

        private sealed class EconomyTestArchitecture : Architecture<EconomyTestArchitecture>
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
            }
        }
    }
}
