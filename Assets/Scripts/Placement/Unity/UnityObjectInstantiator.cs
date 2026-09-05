using UnityEngine;

namespace CUC260905.Placement
{
    /// <summary>Unity Object.Instantiate / Object.Destroy 适配，隔离放置域对 UnityEngine 对象的直接依赖。</summary>
    public sealed class UnityObjectInstantiator : IPlacementInstantiator
    {
        public GameObject Instantiate(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            return Object.Instantiate(prefab, position, rotation);
        }

        public void Destroy(GameObject instance)
        {
            if (instance != null)
            {
                Object.Destroy(instance);
            }
        }
    }
}
