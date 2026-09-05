using QFramework;

namespace CUC260905.Network
{
    /// <summary>数据包传输统计的只读状态：累计成功传输的字节量（以 Mb 计）。所有写操作经 IPacketTrafficStatsSystem 进入。</summary>
    public interface IPacketTrafficStatsModel : IModel
    {
        /// <summary>累计成功传输数据包的字节量（Mb）；值变化会通知监听器（Register / RegisterWithInitValue）。</summary>
        IReadonlyBindableProperty<double> TotalMegabits { get; }
    }

    /// <summary>
    /// 保存累计成功传输数据包的字节量（Mb）；不持有 Unity 对象，也不包含业务规则。
    /// 数值以 BindableProperty 存储：Value 变化即通知监听器，
    /// 由 IPacketTrafficStatsSystem 通过 internal 写方法统一修改。
    /// </summary>
    public sealed class PacketTrafficStatsModel : AbstractModel, IPacketTrafficStatsModel
    {
        private readonly BindableProperty<double> mTotalMegabits;

        public PacketTrafficStatsModel(double initialMegabits = 0d)
        {
            // 初始值不允许为负，保证模型不变量始终成立。
            mTotalMegabits = new BindableProperty<double>(initialMegabits > 0d ? initialMegabits : 0d);
        }

        public IReadonlyBindableProperty<double> TotalMegabits
        {
            get { return mTotalMegabits; }
        }

        /// <summary>追加累计量；仅由 PacketTrafficStatsSystem 在参数校验后调用。</summary>
        internal void Add(double megabits)
        {
            mTotalMegabits.Value += megabits;
        }

        protected override void OnInit()
        {
        }

        protected override void OnDeinit()
        {
            // 架构销毁时不触发监听器，直接复位。
            mTotalMegabits.SetValueWithoutEvent(0d);
        }
    }
}
