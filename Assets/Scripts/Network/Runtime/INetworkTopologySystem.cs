using System;
using System.Collections.Generic;
using QFramework;
using UnityEngine;

namespace CUC260905.Network
{
    /// <summary>节点资料和连通关系的唯一写入口。</summary>
    public interface INetworkTopologySystem : ISystem
    {
        /// <summary>
        /// 注册节点。deployedAt 为部署（注册）时刻，用于计算用户节点的部署接入剩余时间；
        /// 0 表示未指定（按无接入门控处理，兼容既有调用方与测试）。
        /// </summary>
        NetworkTopologyResult Register(NodeDescriptor node, ServerNodeCapabilities capabilities = null, double deployedAt = 0d);

        NetworkTopologyResult Unregister(string nodeId);

        NetworkTopologyResult SetConnected(string firstNodeId, string secondNodeId, bool isConnected, float maxTransmissionSpeed = 0f);

        /// <summary>
        /// 只读预检指定服务器的下一档升级，成功时返回精确的等级、数值和花费报价。
        /// 此方法不会修改服务器能力或任何其他模型。
        /// </summary>
        NetworkTopologyResult TryGetNextServerUpgrade(
            string nodeId,
            ServerUpgradeTrack track,
            ServerUpgradeConfig config,
            out ServerUpgradeQuote quote);

        /// <summary>
        /// 将指定服务器的一个能力轨道升至配置中的下一等级。
        /// 这是不含货币语义的拓扑写入口；玩家操作请改用 IServerUpgradeSystem。
        /// 成功后模型内同一份能力档案会更新，并发送 ServerNodeUpgradedEvent。
        /// </summary>
        NetworkTopologyResult UpgradeServer(
            string nodeId,
            ServerUpgradeTrack track,
            ServerUpgradeConfig config,
            out UpgradeLevelData appliedData);
    }

    /// <summary>校验外部请求、写入拓扑，并在状态完成后发布变化事件。</summary>
    public sealed class NetworkTopologySystem : AbstractSystem, INetworkTopologySystem
    {
        private readonly NetworkTopologyModel mModel;

        public NetworkTopologySystem(NetworkTopologyModel model)
        {
            mModel = model;
        }

        public NetworkTopologyResult Register(NodeDescriptor node, ServerNodeCapabilities capabilities = null, double deployedAt = 0d)
        {
            if (!node.HasValidNodeId)
            {
                return NetworkTopologyResult.InvalidNodeId;
            }

            // 能力档案是服务器节点专属属性：仅 Server 角色且数值合法时可携带。
            if (capabilities != null)
            {
                if (node.Role != NetworkNodeRole.Server || !capabilities.IsValid)
                {
                    return NetworkTopologyResult.InvalidCapabilities;
                }
            }

            if (mModel.TryGetNode(node.NodeId, out NodeDescriptor existingNode))
            {
                return existingNode.Equals(node)
                    ? NetworkTopologyResult.NoChange
                    : NetworkTopologyResult.DuplicateNodeId;
            }

            mModel.Register(node, capabilities, deployedAt);
            this.SendEvent(new NodeRegisteredEvent(node));
            return NetworkTopologyResult.Success;
        }

        public NetworkTopologyResult Unregister(string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                return NetworkTopologyResult.InvalidNodeId;
            }

            if (!mModel.IsRegistered(nodeId))
            {
                return NetworkTopologyResult.NodeNotRegistered;
            }

            List<string> disconnectedNodeIds = new List<string>();
            mModel.Unregister(nodeId, disconnectedNodeIds);
            foreach (string disconnectedNodeId in disconnectedNodeIds)
            {
                this.SendEvent(new NodeConnectivityChangedEvent(nodeId, disconnectedNodeId, false));
            }

            this.SendEvent(new NodeUnregisteredEvent(nodeId));
            return NetworkTopologyResult.Success;
        }

        public NetworkTopologyResult SetConnected(string firstNodeId, string secondNodeId, bool isConnected, float maxTransmissionSpeed = 0f)
        {
            if (string.IsNullOrWhiteSpace(firstNodeId) || string.IsNullOrWhiteSpace(secondNodeId))
            {
                return NetworkTopologyResult.InvalidNodeId;
            }

            if (string.Equals(firstNodeId, secondNodeId, StringComparison.Ordinal))
            {
                return NetworkTopologyResult.SameNode;
            }

            if (!mModel.IsRegistered(firstNodeId) || !mModel.IsRegistered(secondNodeId))
            {
                return NetworkTopologyResult.NodeNotRegistered;
            }

            if (!mModel.SetConnected(firstNodeId, secondNodeId, isConnected, maxTransmissionSpeed))
            {
                return NetworkTopologyResult.NoChange;
            }

            this.SendEvent(new NodeConnectivityChangedEvent(firstNodeId, secondNodeId, isConnected));
            return NetworkTopologyResult.Success;
        }

        public NetworkTopologyResult UpgradeServer(
            string nodeId,
            ServerUpgradeTrack track,
            ServerUpgradeConfig config,
            out UpgradeLevelData appliedData)
        {
            appliedData = null;
            NetworkTopologyResult quoteResult = TryGetNextServerUpgrade(
                nodeId,
                track,
                config,
                out ServerUpgradeQuote quote);
            if (quoteResult != NetworkTopologyResult.Success)
            {
                return quoteResult;
            }

            ServerNodeCapabilities capabilities;
            mModel.TryGetServerCapabilities(nodeId, out capabilities);
            ApplyUpgrade(capabilities, track, quote.TargetLevel, quote.TargetData);
            appliedData = quote.TargetData;
            this.SendEvent(new ServerNodeUpgradedEvent(
                nodeId,
                track,
                quote.CurrentLevel,
                quote.TargetLevel,
                appliedData,
                capabilities));
            return NetworkTopologyResult.Success;
        }

        public NetworkTopologyResult TryGetNextServerUpgrade(
            string nodeId,
            ServerUpgradeTrack track,
            ServerUpgradeConfig config,
            out ServerUpgradeQuote quote)
        {
            quote = default;
            UpgradeLevelData appliedData = null;
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                return NetworkTopologyResult.InvalidNodeId;
            }

            if (!mModel.TryGetNode(nodeId, out NodeDescriptor node))
            {
                return NetworkTopologyResult.NodeNotRegistered;
            }

            if (node.Role != NetworkNodeRole.Server)
            {
                return NetworkTopologyResult.NotServerNode;
            }

            if (!mModel.TryGetServerCapabilities(nodeId, out ServerNodeCapabilities capabilities) || capabilities == null)
            {
                return NetworkTopologyResult.ServerCapabilitiesMissing;
            }

            if (config == null)
            {
                return NetworkTopologyResult.UpgradeConfigMissing;
            }

            int currentLevel = GetLevel(capabilities, track);
            if (currentLevel == int.MaxValue || !config.TryGet(track, currentLevel + 1, out appliedData))
            {
                return NetworkTopologyResult.UpgradeLevelUnavailable;
            }

            if (appliedData == null ||
                float.IsNaN(appliedData.AppliedValue) ||
                float.IsInfinity(appliedData.AppliedValue) ||
                appliedData.AppliedValue < 0f ||
                appliedData.MoneyCost < 0)
            {
                appliedData = null;
                return NetworkTopologyResult.InvalidUpgradeData;
            }

            if (!IsNotDowngrade(capabilities, track, appliedData))
            {
                return NetworkTopologyResult.InvalidUpgradeData;
            }

            quote = new ServerUpgradeQuote(
                nodeId,
                track,
                currentLevel,
                currentLevel + 1,
                appliedData);
            return NetworkTopologyResult.Success;
        }

        private static int GetLevel(ServerNodeCapabilities capabilities, ServerUpgradeTrack track)
        {
            return track == ServerUpgradeTrack.DataThroughput
                ? capabilities.DataThroughputLevel.Value
                : capabilities.MaxConnectionsLevel.Value;
        }

        private static bool IsNotDowngrade(
            ServerNodeCapabilities capabilities,
            ServerUpgradeTrack track,
            UpgradeLevelData appliedData)
        {
            return track == ServerUpgradeTrack.DataThroughput
                ? appliedData.AppliedValue >= capabilities.DataProcessingPerSecond.Value
                : Mathf.RoundToInt(appliedData.AppliedValue) >= capabilities.MaxConnections.Value;
        }

        private static void ApplyUpgrade(
            ServerNodeCapabilities capabilities,
            ServerUpgradeTrack track,
            int nextLevel,
            UpgradeLevelData appliedData)
        {
            if (track == ServerUpgradeTrack.DataThroughput)
            {
                capabilities.DataProcessingPerSecond.Value = appliedData.AppliedValue;
                capabilities.DataThroughputLevel.Value = nextLevel;
                return;
            }

            capabilities.MaxConnections.Value = Mathf.RoundToInt(appliedData.AppliedValue);
            capabilities.MaxConnectionsLevel.Value = nextLevel;
        }

        protected override void OnInit()
        {
            if (mModel == null)
            {
                throw new InvalidOperationException(
                    "NetworkTopologySystem 初始化前必须创建 NetworkTopologyModel。");
            }
        }
    }
}
