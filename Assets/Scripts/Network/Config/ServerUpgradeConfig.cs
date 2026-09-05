using System;
using System.Collections.Generic;
using UnityEngine;

namespace CUC260905.Network
{
    /// <summary>升级系统单个等级的数据：新的应用数值 + 金钱消耗。</summary>
    [Serializable]
    public sealed class UpgradeLevelData
    {
        /// <summary>达到该等级后应用的新数值（绝对值，非相对增量）。</summary>
        [Tooltip("达到该等级后应用的新数值（绝对值，非相对增量）。")]
        public float AppliedValue;

        /// <summary>升到该等级所需的金钱消耗。</summary>
        [Tooltip("升到该等级所需的金钱消耗。")]
        public int MoneyCost;

        public UpgradeLevelData()
        {
        }

        public UpgradeLevelData(float appliedValue, int moneyCost)
        {
            AppliedValue = appliedValue;
            MoneyCost = moneyCost;
        }

        public override string ToString()
        {
            return $"[{nameof(UpgradeLevelData)} appliedValue={AppliedValue}, moneyCost={MoneyCost}]";
        }
    }

    /// <summary>服务器可升级的能力轨道，对应 ServerNodeCapabilities 的两个字段。</summary>
    public enum ServerUpgradeTrack
    {
        DataThroughput = 0,
        MaxConnections = 1
    }

    /// <summary>
    /// 服务器下一档升级的只读报价。由 INetworkTopologySystem 预检后创建，
    /// 供 UI 展示花费和跨域升级协调器执行扣费。
    /// </summary>
    public readonly struct ServerUpgradeQuote
    {
        public readonly string NodeId;
        public readonly ServerUpgradeTrack Track;
        public readonly int CurrentLevel;
        public readonly int TargetLevel;
        public readonly UpgradeLevelData TargetData;

        public ServerUpgradeQuote(
            string nodeId,
            ServerUpgradeTrack track,
            int currentLevel,
            int targetLevel,
            UpgradeLevelData targetData)
        {
            NodeId = nodeId;
            Track = track;
            CurrentLevel = currentLevel;
            TargetLevel = targetLevel;
            TargetData = targetData;
        }
    }

    /// <summary>
    /// 服务器升级系统的开发者配置 ScriptableObject。
    ///
    /// 保存两条升级表（List）：数据吞吐量、最大连接数；
    /// List 的索引即等级（index 0 = level 0，index N = level N），
    /// 每个元素固定包含「新的应用数值 + 金钱消耗」。
    ///
    /// 取值语义：TryGet* 严格按等级取值（越界返回 false）；
    /// Get* 越界自动 clamp 到最近档位；BuildCapabilities 可直接把某等级
    /// 的两条数据合成为 ServerNodeCapabilities。
    /// </summary>
    [CreateAssetMenu(
        fileName = "ServerUpgradeConfig",
        menuName = "CUC260905/Network/Server Upgrade Config",
        order = 0)]
    public sealed class ServerUpgradeConfig : ScriptableObject
    {
        [Header("数据吞吐量升级表（List 索引 = 等级）")]
        [SerializeField]
        private List<UpgradeLevelData> mDataThroughputLevels = new List<UpgradeLevelData>();

        [Header("最大连接数升级表（List 索引 = 等级）")]
        [SerializeField]
        private List<UpgradeLevelData> mMaxConnectionLevels = new List<UpgradeLevelData>();

        public IReadOnlyList<UpgradeLevelData> DataThroughputLevels
        {
            get { return mDataThroughputLevels; }
        }

        public IReadOnlyList<UpgradeLevelData> MaxConnectionLevels
        {
            get { return mMaxConnectionLevels; }
        }

        public int DataThroughputLevelCount
        {
            get { return mDataThroughputLevels.Count; }
        }

        public int MaxConnectionLevelCount
        {
            get { return mMaxConnectionLevels.Count; }
        }

        public int GetLevelCount(ServerUpgradeTrack track)
        {
            return track == ServerUpgradeTrack.DataThroughput
                ? mDataThroughputLevels.Count
                : mMaxConnectionLevels.Count;
        }

        /// <summary>整表替换（工厂预填/测试用）。传入 null 视作清空。</summary>
        public void SetDataThroughputLevels(IEnumerable<UpgradeLevelData> levels)
        {
            ReplaceLevels(mDataThroughputLevels, levels);
        }

        /// <summary>整表替换（工厂预填/测试用）。传入 null 视作清空。</summary>
        public void SetMaxConnectionLevels(IEnumerable<UpgradeLevelData> levels)
        {
            ReplaceLevels(mMaxConnectionLevels, levels);
        }

        /// <summary>严格取数据吞吐量表：越界或空表返回 false，data 为 null。</summary>
        public bool TryGetData(int level, out UpgradeLevelData data)
        {
            return TryGetLevelData(mDataThroughputLevels, level, out data);
        }

        /// <summary>严格取最大连接数表：越界或空表返回 false，data 为 null。</summary>
        public bool TryGetMaxConnections(int level, out UpgradeLevelData data)
        {
            return TryGetLevelData(mMaxConnectionLevels, level, out data);
        }

        public bool TryGet(ServerUpgradeTrack track, int level, out UpgradeLevelData data)
        {
            List<UpgradeLevelData> levels = track == ServerUpgradeTrack.DataThroughput
                ? mDataThroughputLevels
                : mMaxConnectionLevels;
            return TryGetLevelData(levels, level, out data);
        }

        /// <summary>取数据吞吐量，越界 clamp 到最近档位；空表返回 null。</summary>
        public UpgradeLevelData GetData(int level)
        {
            return GetLevelData(mDataThroughputLevels, level);
        }

        /// <summary>取最大连接数，越界 clamp 到最近档位；空表返回 null。</summary>
        public UpgradeLevelData GetMaxConnections(int level)
        {
            return GetLevelData(mMaxConnectionLevels, level);
        }

        public UpgradeLevelData Get(ServerUpgradeTrack track, int level)
        {
            List<UpgradeLevelData> levels = track == ServerUpgradeTrack.DataThroughput
                ? mDataThroughputLevels
                : mMaxConnectionLevels;
            return GetLevelData(levels, level);
        }

        /// <summary>按相同等级合成服务器能力快照，供旧的统一等级配置调用。</summary>
        public ServerNodeCapabilities BuildCapabilities(int level)
        {
            return BuildCapabilities(level, level);
        }

        /// <summary>按两条轨道各自的等级合成服务器能力快照。</summary>
        public ServerNodeCapabilities BuildCapabilities(int dataThroughputLevel, int maxConnectionsLevel)
        {
            UpgradeLevelData throughput = GetLevelData(mDataThroughputLevels, dataThroughputLevel);
            UpgradeLevelData connections = GetLevelData(mMaxConnectionLevels, maxConnectionsLevel);
            return new ServerNodeCapabilities(
                throughput != null ? throughput.AppliedValue : 0f,
                connections != null ? Mathf.RoundToInt(connections.AppliedValue) : 0,
                Mathf.Max(0, dataThroughputLevel),
                Mathf.Max(0, maxConnectionsLevel));
        }

        private static bool TryGetLevelData(
            IReadOnlyList<UpgradeLevelData> levels,
            int level,
            out UpgradeLevelData data)
        {
            data = null;
            if (levels == null || level < 0 || level >= levels.Count)
            {
                return false;
            }

            data = levels[level];
            return true;
        }

        private static UpgradeLevelData GetLevelData(IReadOnlyList<UpgradeLevelData> levels, int level)
        {
            if (levels == null || levels.Count == 0)
            {
                return null;
            }

            return levels[Mathf.Clamp(level, 0, levels.Count - 1)];
        }

        private static void ReplaceLevels(List<UpgradeLevelData> target, IEnumerable<UpgradeLevelData> levels)
        {
            target.Clear();
            if (levels == null)
            {
                return;
            }

            target.AddRange(levels);
        }
    }
}
