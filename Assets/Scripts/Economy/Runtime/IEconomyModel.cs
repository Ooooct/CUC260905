using QFramework;

namespace CUC260905.Economy
{
    /// <summary>经济系统的只读状态：单一货币余额。所有写操作经 IEconomySystem 进入。</summary>
    public interface IEconomyModel : IModel
    {
        /// <summary>当前货币余额；值变化会通知监听器（Register / RegisterWithInitValue）。</summary>
        IReadonlyBindableProperty<int> Balance { get; }
    }

    /// <summary>
    /// 保存单一货币（金币）余额；不持有 Unity 对象，也不包含业务规则。
    /// 余额以 BindableProperty 存储：Value 变化即通知监听器，
    /// 由 IEconomySystem 通过 internal 写方法统一修改。
    /// </summary>
    public sealed class EconomyModel : AbstractModel, IEconomyModel
    {
        private readonly BindableProperty<int> mBalance;

        public EconomyModel(int startingBalance = 0)
        {
            // 初始余额不允许为负，保证模型不变量始终成立。
            mBalance = new BindableProperty<int>(startingBalance > 0 ? startingBalance : 0);
        }

        public IReadonlyBindableProperty<int> Balance
        {
            get { return mBalance; }
        }

        /// <summary>增加余额；仅由 EconomySystem 在参数校验后调用。</summary>
        internal void Add(int amount)
        {
            mBalance.Value += amount;
        }

        /// <summary>扣除余额；仅由 EconomySystem 在余额足够校验后调用。</summary>
        internal void Consume(int amount)
        {
            mBalance.Value -= amount;
        }

        protected override void OnInit()
        {
        }

        protected override void OnDeinit()
        {
            // 架构销毁时不触发监听器，直接复位。
            mBalance.SetValueWithoutEvent(0);
        }
    }
}
