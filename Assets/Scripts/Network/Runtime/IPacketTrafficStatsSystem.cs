using System;
using QFramework;

namespace CUC260905.Network
{
    /// <summary>累计成功传输字节量（Mb）的唯一写入口，并订阅成功传输事件自动累计。</summary>
    public interface IPacketTrafficStatsSystem : ISystem
    {
        /// <summary>当前累计成功传输字节量（Mb）。</summary>
        double TotalMegabits { get; }

        /// <summary>追加累计量；megabits 必须为正且有限，成功返回 true。</summary>
        bool Add(double megabits);
    }

    /// <summary>
    /// 监听数据包成功传输事件并把 PacketSize（Mb）累加到统计模型。
    /// 与 Network 传输路径解耦：本模块不持有传输细节，也不在发送路径上注入统计依赖；
    /// 只对 PacketTransmittedEvent（传输成功）累计，不可达等失败事件不产生统计。
    /// 写入统一经本 System 校验（非正数/非有限/溢出时返回 false，静默忽略，不中断传输流程）。
    /// </summary>
    public sealed class PacketTrafficStatsSystem : AbstractSystem, IPacketTrafficStatsSystem
    {
        private readonly PacketTrafficStatsModel mModel;
        private IUnRegister mTransmittedRegistration;

        public PacketTrafficStatsSystem(PacketTrafficStatsModel model)
        {
            mModel = model;
        }

        public double TotalMegabits
        {
            get { return mModel == null ? 0d : mModel.TotalMegabits.Value; }
        }

        public bool Add(double megabits)
        {
            if (mModel == null)
            {
                return false;
            }

            // 参数校验：只接受正的有限数值，避免 NaN/Infinity 污染累计量。
            if (double.IsNaN(megabits) || double.IsInfinity(megabits) || megabits <= 0d)
            {
                return false;
            }

            double current = mModel.TotalMegabits.Value;
            // double 的极值减法会被舍入吞掉（MaxValue - 1d == MaxValue），
            // 因此用加法结果是否为 +∞ 判断真实溢出；溢出时不写入，避免污染累计量。
            if (double.IsPositiveInfinity(current + megabits))
            {
                return false;
            }

            mModel.Add(megabits);
            return true;
        }

        protected override void OnInit()
        {
            if (mModel == null)
            {
                throw new InvalidOperationException(
                    "PacketTrafficStatsSystem 初始化前必须创建 PacketTrafficStatsModel。");
            }

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

        private void OnPacketTransmitted(PacketTransmittedEvent evt)
        {
            // 成功事件的 PacketSize 已由传输系统校验为正且有限；此处仍走统一写入口，失败静默忽略。
            Add(evt.PacketSize);
        }
    }
}
