using System;
using QFramework;

namespace CUC260905.Economy
{
    /// <summary>货币余额的唯一写入口：增加与消耗。</summary>
    public interface IEconomySystem : ISystem
    {
        /// <summary>增加余额；amount 必须为正数，成功返回 true。</summary>
        bool Add(int amount);

        /// <summary>
        /// 尝试消耗余额：余额足够则扣除并返回 true；余额不足或参数非法则不扣任何金额并返回 false。
        /// </summary>
        bool Consume(int amount);
    }

    /// <summary>
    /// 校验外部请求并写入余额；余额变化经由模型的 BindableProperty 通知监听方。
    /// 消耗是原子操作：先判断是否足够，不足时不做任何修改。
    /// </summary>
    public sealed class EconomySystem : AbstractSystem, IEconomySystem
    {
        private readonly EconomyModel mModel;

        public EconomySystem(EconomyModel model)
        {
            mModel = model;
        }

        public bool Add(int amount)
        {
            if (amount <= 0 || mModel.Balance.Value > int.MaxValue - amount)
            {
                return false;
            }

            mModel.Add(amount);
            return true;
        }

        public bool Consume(int amount)
        {
            if (amount <= 0 || mModel.Balance.Value < amount)
            {
                return false;
            }

            mModel.Consume(amount);
            return true;
        }

        protected override void OnInit()
        {
            if (mModel == null)
            {
                throw new InvalidOperationException(
                    "EconomySystem 初始化前必须创建 EconomyModel。");
            }
        }
    }
}
