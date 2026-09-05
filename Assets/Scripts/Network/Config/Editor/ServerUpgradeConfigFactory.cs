using System.Collections.Generic;
using CUC260905.Network;
using UnityEditor;
using UnityEngine;

namespace CUC260905.Network.EditorTools
{
    /// <summary>
    /// 服务器升级配置 SO 的落地工具：一键生成资产并预填示例等级数据，
    /// 或对选中的配置资产做基础数据校验。
    /// 预填数值仅作演示占位，请按实际数值体系在 Inspector 中调整。
    /// </summary>
    public static class ServerUpgradeConfigFactory
    {
        private const string DefaultAssetPath = "Assets/Configs/ServerUpgradeConfig.asset";

        [MenuItem("CUC260905/Network/Create Server Upgrade Config (Prefilled)")]
        [MenuItem("Assets/CUC260905/Network/Create Server Upgrade Config (Prefilled)")]
        public static void CreateConfig()
        {
            ServerUpgradeConfig config = ScriptableObject.CreateInstance<ServerUpgradeConfig>();
            PrefillSampleLevels(config);

            string path = AssetDatabase.GenerateUniqueAssetPath(DefaultAssetPath);
            AssetDatabase.CreateAsset(config, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = config;
            EditorGUIUtility.PingObject(config);
            Debug.Log($"[ServerUpgradeConfigFactory] 已创建升级配置资产：{path}", config);
        }

        [MenuItem("Assets/CUC260905/Network/Validate Server Upgrade Config", true)]
        public static bool ValidateSelectedConfigEnabled()
        {
            return Selection.activeObject is ServerUpgradeConfig;
        }

        [MenuItem("Assets/CUC260905/Network/Validate Server Upgrade Config")]
        public static void ValidateSelectedConfig()
        {
            ServerUpgradeConfig config = Selection.activeObject as ServerUpgradeConfig;
            if (config == null)
            {
                return;
            }

            List<string> issues = Validate(config);
            if (issues.Count == 0)
            {
                Debug.Log("[ServerUpgradeConfigFactory] 校验通过：所有等级数据均为非负。", config);
                return;
            }

            foreach (string issue in issues)
            {
                Debug.LogWarning($"[ServerUpgradeConfigFactory] {issue}", config);
            }
        }

        /// <summary>预填两表各 13 档（等级 0~12），数值与 docs/numerical-design.md §6 对齐（整十/整五、首档降门槛）。</summary>
        public static void PrefillSampleLevels(ServerUpgradeConfig config)
        {
            config.SetDataThroughputLevels(new List<UpgradeLevelData>
            {
                new UpgradeLevelData(100f, 0),
                new UpgradeLevelData(220f, 50),
                new UpgradeLevelData(340f, 100),
                new UpgradeLevelData(460f, 200),
                new UpgradeLevelData(580f, 300),
                new UpgradeLevelData(700f, 450),
                new UpgradeLevelData(820f, 650),
                new UpgradeLevelData(940f, 900),
                new UpgradeLevelData(1060f, 1300),
                new UpgradeLevelData(1180f, 1800),
                new UpgradeLevelData(1300f, 2500),
                new UpgradeLevelData(1420f, 3500),
                new UpgradeLevelData(1540f, 5000)
            });

            config.SetMaxConnectionLevels(new List<UpgradeLevelData>
            {
                new UpgradeLevelData(5f, 0),
                new UpgradeLevelData(10f, 30),
                new UpgradeLevelData(15f, 50),
                new UpgradeLevelData(20f, 100),
                new UpgradeLevelData(25f, 150),
                new UpgradeLevelData(30f, 250),
                new UpgradeLevelData(35f, 350),
                new UpgradeLevelData(40f, 450),
                new UpgradeLevelData(45f, 650),
                new UpgradeLevelData(50f, 900),
                new UpgradeLevelData(55f, 1300),
                new UpgradeLevelData(60f, 1800),
                new UpgradeLevelData(65f, 2500)
            });
        }

        /// <summary>基础数据校验：空表、空元素或负值都会进入 issues。</summary>
        public static List<string> Validate(ServerUpgradeConfig config)
        {
            List<string> issues = new List<string>();
            if (config == null)
            {
                issues.Add("配置为空。");
                return issues;
            }

            CollectIssues("数据吞吐量", config.DataThroughputLevels, issues);
            CollectIssues("最大连接数", config.MaxConnectionLevels, issues);
            return issues;
        }

        private static void CollectIssues(
            string trackName,
            IReadOnlyList<UpgradeLevelData> levels,
            List<string> issues)
        {
            if (levels.Count == 0)
            {
                issues.Add($"[{trackName}] 等级表为空，请至少配置 level 0。");
            }

            for (int level = 0; level < levels.Count; level++)
            {
                UpgradeLevelData data = levels[level];
                if (data == null)
                {
                    issues.Add($"[{trackName}] level {level} 为空元素。");
                    continue;
                }

                if (data.AppliedValue < 0f)
                {
                    issues.Add($"[{trackName}] level {level} 应用数值为负（{data.AppliedValue}）。");
                }

                if (data.MoneyCost < 0)
                {
                    issues.Add($"[{trackName}] level {level} 金钱消耗为负（{data.MoneyCost}）。");
                }
            }
        }
    }
}
