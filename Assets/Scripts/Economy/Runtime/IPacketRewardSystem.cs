using CUC260905.Network;
using QFramework;

namespace CUC260905.Economy
{
    /// <summary>
    /// 数据包传输奖励规则：每次成功完成一次数据包传输时随机发放 2~3 金币（各 50%）。
    /// 通过事件总线订阅 PacketTransmittedEvent，与 Network 域解耦——本模块不持有网络引用，
    /// 也不在数据包传输路径上注入经济依赖。
    /// </summary>
    public interface IPacketRewardSystem : ISystem
    {
        /// <summary>每次成功传输可能发放的最小金币数。</summary>
        int MinRewardPerTransmission { get; }

        /// <summary>每次成功传输可能发放的最大金币数。</summary>
        int MaxRewardPerTransmission { get; }

        /// <summary>按 2~3 各 50% 的概率掷出一次奖励金额（金币）。</summary>
        int RollRewardPerTransmission();
    }

    /// <summary>
    /// 监听数据包成功传输事件并为经济系统增加收入。
    /// 只对 PacketTransmittedEvent（传输成功）发奖；不可达等失败事件不产生收入。
    /// 余额写入统一经 IEconomySystem 校验，Add 返回 false 时静默忽略，不中断传输流程。
    /// 随机源可注入 System.Random（默认新建），便于测试复现奖励序列。
    /// </summary>
    public sealed class PacketRewardSystem : AbstractSystem, IPacketRewardSystem
    {
        /// <summary>每次成功传输发放的最小奖励（金币）。</summary>
        public const int DefaultMinRewardPerTransmission = 2;

        /// <summary>每次成功传输发放的最大奖励（金币）。</summary>
        public const int DefaultMaxRewardPerTransmission = 3;

        private readonly System.Random mRandom;
        private IUnRegister mTransmittedRegistration;

        public PacketRewardSystem() : this(null)
        {
        }

        public PacketRewardSystem(System.Random random)
        {
            mRandom = random ?? new System.Random();
        }

        public int MinRewardPerTransmission
        {
            get { return DefaultMinRewardPerTransmission; }
        }

        public int MaxRewardPerTransmission
        {
            get { return DefaultMaxRewardPerTransmission; }
        }

        public int RollRewardPerTransmission()
        {
            // Next(2) 返回 0 或 1，各约 50%；映射到最小/最大奖励。
            return mRandom.Next(2) == 0
                ? DefaultMinRewardPerTransmission
                : DefaultMaxRewardPerTransmission;
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
            economySystem.Add(RollRewardPerTransmission());
        }
    }
}
