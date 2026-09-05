using CUC260905.Network;
using QFramework;

namespace CUC260905.Economy
{
    /// <summary>
    /// 数据包传输奖励规则：每次成功完成一次数据包传输时发放固定金币。
    /// 通过事件总线订阅 PacketTransmittedEvent，与 Network 域解耦——本模块不持有网络引用，
    /// 也不在数据包传输路径上注入经济依赖。
    /// </summary>
    public interface IPacketRewardSystem : ISystem
    {
        /// <summary>每成功完成一次数据包传输可获得的最小金币数。</summary>
        int MinimumRewardPerTransmission { get; }

        /// <summary>每成功完成一次数据包传输可获得的最大奖励金币数。</summary>
        int MaximumRewardPerTransmission { get; }
    }

    /// <summary>
    /// 监听数据包成功传输事件并为经济系统增加收入。
    /// 只对 PacketTransmittedEvent（传输成功）发奖；不可达等失败事件不产生收入。
    /// 余额写入统一经 IEconomySystem 校验，Add 返回 false 时静默忽略，不中断传输流程。
    /// </summary>
    public sealed class PacketRewardSystem : AbstractSystem, IPacketRewardSystem
    {
        /// <summary>每次成功传输奖励的下限（金币）。</summary>
        public const int DefaultMinimumRewardPerTransmission = 3;

        /// <summary>每次成功传输奖励的上限（金币）。</summary>
        public const int DefaultMaximumRewardPerTransmission = 4;

        private readonly System.Random mRewardRandom;
        private IUnRegister mTransmittedRegistration;

        public PacketRewardSystem()
            : this(new System.Random())
        {
        }

        /// <summary>
        /// 传入随机源以便测试复现奖励序列；运行时默认使用独立随机源。
        /// </summary>
        public PacketRewardSystem(System.Random rewardRandom)
        {
            mRewardRandom = rewardRandom ?? new System.Random();
        }

        public int MinimumRewardPerTransmission
        {
            get { return DefaultMinimumRewardPerTransmission; }
        }

        public int MaximumRewardPerTransmission
        {
            get { return DefaultMaximumRewardPerTransmission; }
        }

        protected override void OnInit()
        {
            mTransmittedRegistration = this.RegisterEvent<PacketTransmittedEvent>(OnPacketTransmitted);
        }

        protected override void OnDeinit()
        {
            if (mTransmittedRegistration != null)
            {
                mTransmittedRegistration.UnRegister();
                mTransmittedRegistration = null;
            }
        }

        private void OnPacketTransmitted(PacketTransmittedEvent _)
        {
            IEconomySystem economySystem = this.GetSystem<IEconomySystem>();
            if (economySystem == null)
            {
                return;
            }

            int reward = mRewardRandom.Next(0, 2) == 0
                ? DefaultMinimumRewardPerTransmission
                : DefaultMaximumRewardPerTransmission;

            // 余额写入由 EconomySystem 校验（参数非法或溢出时返回 false），此处不额外处理。
            economySystem.Add(reward);
        }
    }
}
