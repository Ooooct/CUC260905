using CUC260905.Network;
using CUC260905.Network.EditorTools;
using NUnit.Framework;
using UnityEngine;

namespace CUC260905.Tests
{
    /// <summary>
    /// ServerUpgradeConfig：验证「List 索引 = 等级」的取值语义、
    /// 越界 clamp、与 ServerNodeCapabilities 的合成，以及工厂预填结果。
    /// </summary>
    public sealed class ServerUpgradeConfigTests
    {
        private ServerUpgradeConfig mConfig;

        [SetUp]
        public void SetUp()
        {
            mConfig = ScriptableObject.CreateInstance<ServerUpgradeConfig>();
            mConfig.SetDataThroughputLevels(new[]
            {
                new UpgradeLevelData(100f, 0),
                new UpgradeLevelData(150f, 100),
                new UpgradeLevelData(220f, 250)
            });
            mConfig.SetMaxConnectionLevels(new[]
            {
                new UpgradeLevelData(4f, 0),
                new UpgradeLevelData(6f, 80)
            });
        }

        [TearDown]
        public void TearDown()
        {
            if (mConfig != null)
            {
                Object.DestroyImmediate(mConfig);
            }
        }

        [Test]
        public void TryGetData_IndexIsLevel()
        {
            Assert.That(mConfig.TryGetData(1, out UpgradeLevelData data), Is.True);
            Assert.That(data.AppliedValue, Is.EqualTo(150f));
            Assert.That(data.MoneyCost, Is.EqualTo(100));
        }

        [Test]
        public void TryGetMaxConnections_IndexIsLevel()
        {
            Assert.That(mConfig.TryGetMaxConnections(0, out UpgradeLevelData data), Is.True);
            Assert.That(data.AppliedValue, Is.EqualTo(4f));
            Assert.That(data.MoneyCost, Is.EqualTo(0));
        }

        [Test]
        public void TryGet_OutOfRange_ReturnsFalse()
        {
            Assert.That(mConfig.TryGetData(3, out _), Is.False);
            Assert.That(mConfig.TryGetData(-1, out _), Is.False);
            Assert.That(mConfig.TryGetMaxConnections(2, out _), Is.False);
        }

        [Test]
        public void Get_ClampsToNearestLevel()
        {
            Assert.That(mConfig.GetData(99).AppliedValue, Is.EqualTo(220f));
            Assert.That(mConfig.GetData(-5).AppliedValue, Is.EqualTo(100f));
        }

        [Test]
        public void BuildCapabilities_AppliesBothTracksAtLevel()
        {
            ServerNodeCapabilities capabilities = mConfig.BuildCapabilities(1);

            Assert.That(capabilities.DataProcessingPerSecond.Value, Is.EqualTo(150f));
            Assert.That(capabilities.MaxConnections.Value, Is.EqualTo(6));
            Assert.That(capabilities.DataThroughputLevel.Value, Is.EqualTo(1));
            Assert.That(capabilities.MaxConnectionsLevel.Value, Is.EqualTo(1));
        }

        [Test]
        public void BuildCapabilities_AppliesIndependentTrackLevels()
        {
            ServerNodeCapabilities capabilities = mConfig.BuildCapabilities(2, 0);

            Assert.That(capabilities.DataProcessingPerSecond.Value, Is.EqualTo(220f));
            Assert.That(capabilities.MaxConnections.Value, Is.EqualTo(4));
            Assert.That(capabilities.DataThroughputLevel.Value, Is.EqualTo(2));
            Assert.That(capabilities.MaxConnectionsLevel.Value, Is.EqualTo(0));
        }

        [Test]
        public void FactoryPrefill_ProducesValidNonEmptyLevelTables()
        {
            ServerUpgradeConfig config = ScriptableObject.CreateInstance<ServerUpgradeConfig>();
            ServerUpgradeConfigFactory.PrefillSampleLevels(config);

            // 工厂预填为数值设计 v1 的 13 档（等级 0~12，见 docs/numerical-design.md §6）。
            Assert.That(config.DataThroughputLevelCount, Is.EqualTo(13));
            Assert.That(config.MaxConnectionLevelCount, Is.EqualTo(13));
            Assert.That(ServerUpgradeConfigFactory.Validate(config), Is.Empty);

            Object.DestroyImmediate(config);
        }
    }
}
