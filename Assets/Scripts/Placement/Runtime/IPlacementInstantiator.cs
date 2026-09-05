using QFramework;
using UnityEngine;

namespace CUC260905.Placement
{
    /// <summary>实例化/销毁放置对象的端口，便于测试替身替换。</summary>
    public interface IPlacementInstantiator : IUtility
    {
        GameObject Instantiate(GameObject prefab, Vector3 position, Quaternion rotation);

        void Destroy(GameObject instance);
    }
}
