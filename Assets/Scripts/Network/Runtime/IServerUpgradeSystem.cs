using System;
using CUC260905.Economy;
using QFramework;

namespace CUC260905.Network
{
    /// <summary>
    /// 服务器付费升级的唯一业务入口：协调拓扑预检、余额扣除与能力写入。
    /// UI 及其他表现层应使用此接口，不能自行拼接 Consume 与 UpgradeServer。
    /// </summary>
    public interface IServerUpgradeSystem : ISystem
    {
        NetworkTopologyResult UpgradeServer(
            string nodeId,
            ServerUpgradeTrack track,
            ServerUpgradeConfig config,
            out ServerUpgradeQuote appliedQuote);
    }

    /// <summary>
    /// 付费升级协调器。所有拓扑校验在扣费前完成；升级写入异常失败时会退款，
    /// 使余额与服务器能力在单帧业务流程内保持一致。
    /// </summary>
    public sealed class ServerUpgradeSystem : AbstractSystem, IServerUpgradeSystem
    {
        private readonly INetworkTopologySystem mTopologySystem;
        private readonly IEconomySystem mEconomySystem;

        public ServerUpgradeSystem(
            INetworkTopologySystem topologySystem,
            IEconomySystem economySystem)
        {
            mTopologySystem = topologySystem;
            mEconomySystem = economySystem;
        }

        public NetworkTopologyResult UpgradeServer(
            string nodeId,
            ServerUpgradeTrack track,
            ServerUpgradeConfig config,
            out ServerUpgradeQuote appliedQuote)
        {
            appliedQuote = default;
            NetworkTopologyResult result = mTopologySystem.TryGetNextServerUpgrade(
                nodeId,
                track,
                config,
                out ServerUpgradeQuote quote);
            if (result != NetworkTopologyResult.Success)
            {
                return result;
            }

            int cost = quote.TargetData.MoneyCost;
            if (cost > 0 && !mEconomySystem.Consume(cost))
            {
                return NetworkTopologyResult.InsufficientBalance;
            }

            result = mTopologySystem.UpgradeServer(nodeId, track, config, out _);
            if (result != NetworkTopologyResult.Success)
            {
                if (cost > 0)
                {
                    mEconomySystem.Add(cost);
                }

                return result;
            }

            appliedQuote = quote;
            return NetworkTopologyResult.Success;
        }

        protected override void OnInit()
        {
            if (mTopologySystem == null)
            {
                throw new InvalidOperationException(
                    "ServerUpgradeSystem 初始化前必须创建 INetworkTopologySystem。");
            }

            if (mEconomySystem == null)
            {
                throw new InvalidOperationException(
                    "ServerUpgradeSystem 初始化前必须创建 IEconomySystem。");
            }
        }
    }
}
