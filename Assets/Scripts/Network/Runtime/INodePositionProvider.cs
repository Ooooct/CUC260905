using QFramework;
using UnityEngine;

namespace CUC260905.Network
{
    /// <summary>
    /// 提供 NodeId → 世界坐标的只读映射，供需要几何信息的规则（如连线交叉检查）使用。
    /// 由表现层（NetworkConnectionController）注册并维护，测试时可替换为固定表。
    /// </summary>
    public interface INodePositionProvider : IUtility
    {
        bool TryGetNodePosition(string nodeId, out Vector3 position);
    }
}
