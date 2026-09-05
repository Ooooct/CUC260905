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
        /// <summary>每成功完成一次数据包传输发放的金币数。</summary>
        int RewardPerTransmission { get; }
    }

    /// <summary>
    /// 监听数据包成功传输事件并为经济系统增加收入。
    /// 只对 PacketTransmittedEvent（传输成功）发奖；不可达等失败事件不产生收入。
    /// 余额写入统一经 IEconomySystem 校验，Add 返回 false 时静默忽略，不中断传输流程。
    /// </summary>
    public sealed class PacketRewardSystem : AbstractSystem, IPacketRewardSystem
    {
        /// <summary>未指定时使用的单次奖励数值（数值设计 v1：每次成功传输 2 金币，见 docs/numerical-design.md §5）。</summary>
        public const int DefaultRewardPerTransmission = 2;

        private readonly int mRewardPerTransmission;
        private IUnRegister mTransmittedRegistration;

        public PacketRewardSystem()
            : this(DefaultRewardPerTransmission)
        {
        }

        public PacketRewardSystem(int rewardPerTransmission)
        {
            // 奖励数值不允许为非正数；非法时回退到默认值，保持不变量。
            mRewardPerTransmission = rewardPerTransmission > 0
                ? rewardPerTransmission
                : DefaultRewardPerTransmission;
        }

        public int RewardPerTransmission
        {
            get { return mRewardPerTransmission; }
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

            // 余额写入由 EconomySystem 校验（参数非法或溢出时返回 false），此处不额外处理。
            economySystem.Add(mRewardPerTransmission);
        }
    }
}
