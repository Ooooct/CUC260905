using System;
using QFramework;

namespace CUC260905.Network
{
    /// <summary>
    /// 服务器节点能力档案：每秒数据处理上限 + 最大连接边数 + 两条独立升级轨道的等级。
    /// 仅对 NetworkNodeRole.Server 有意义，随 Register 携带存入拓扑模型。
    /// 每个可变属性以 BindableProperty 存储：Value 变化即通知监听器（Register / RegisterWithInitValue）。
    /// 0 / 0f 表示未配置或无限。
    /// </summary>
    public sealed class ServerNodeCapabilities
    {
        /// <summary>每秒数据处理上限（条/秒，抽象单位）。0 = 未配置/无限。</summary>
        public BindableProperty<float> DataProcessingPerSecond { get; }

        /// <summary>
        /// 最近一秒内已被数据包占用的处理量。它是服务器实例的运行时状态，
        /// 不参与升级数值和能力档案的相等性比较。
        /// </summary>
        public BindableProperty<float> CurrentDataLoadPerSecond { get; }

        /// <summary>最大连接边数。0 = 未配置/无限。</summary>
        public BindableProperty<int> MaxConnections { get; }

        /// <summary>数据吞吐量轨道等级。0 = 基础档。</summary>
        public BindableProperty<int> DataThroughputLevel { get; }

        /// <summary>最大连接数轨道等级。0 = 基础档。</summary>
        public BindableProperty<int> MaxConnectionsLevel { get; }

        /// <summary>
        /// 兼容旧调用的聚合等级：取两条轨道中的较高等级。
        /// 新代码应读取具体轨道等级，不能将此属性作为升级写入目标。
        /// </summary>
        public int Level
        {
            get { return Math.Max(DataThroughputLevel.Value, MaxConnectionsLevel.Value); }
        }

        /// <summary>兼容旧初始化：两条轨道从同一等级起步。</summary>
        public ServerNodeCapabilities(float dataProcessingPerSecond, int maxConnections, int level = 0)
            : this(dataProcessingPerSecond, maxConnections, level, level)
        {
        }

        public ServerNodeCapabilities(
            float dataProcessingPerSecond,
            int maxConnections,
            int dataThroughputLevel,
            int maxConnectionsLevel)
        {
            DataProcessingPerSecond = new BindableProperty<float>(dataProcessingPerSecond);
            CurrentDataLoadPerSecond = new BindableProperty<float>(0f);
            MaxConnections = new BindableProperty<int>(maxConnections);
            DataThroughputLevel = new BindableProperty<int>(dataThroughputLevel);
            MaxConnectionsLevel = new BindableProperty<int>(maxConnectionsLevel);
        }

        /// <summary>任一项为负值即视为非法配置。</summary>
        public bool IsValid
        {
            get
            {
                return !float.IsNaN(DataProcessingPerSecond.Value) &&
                       !float.IsInfinity(DataProcessingPerSecond.Value) &&
                       DataProcessingPerSecond.Value >= 0f &&
                       MaxConnections.Value >= 0 &&
                       DataThroughputLevel.Value >= 0 &&
                       MaxConnectionsLevel.Value >= 0;
            }
        }

        public override bool Equals(object obj)
        {
            return obj is ServerNodeCapabilities other &&
                   DataProcessingPerSecond.Value.Equals(other.DataProcessingPerSecond.Value) &&
                   MaxConnections.Value == other.MaxConnections.Value &&
                   DataThroughputLevel.Value == other.DataThroughputLevel.Value &&
                   MaxConnectionsLevel.Value == other.MaxConnectionsLevel.Value;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                DataProcessingPerSecond.Value,
                MaxConnections.Value,
                DataThroughputLevel.Value,
                MaxConnectionsLevel.Value);
        }

        public override string ToString()
        {
            return $"{{DataProcessingPerSecond={DataProcessingPerSecond.Value}, MaxConnections={MaxConnections.Value}, DataThroughputLevel={DataThroughputLevel.Value}, MaxConnectionsLevel={MaxConnectionsLevel.Value}}}";
        }
    }
}
