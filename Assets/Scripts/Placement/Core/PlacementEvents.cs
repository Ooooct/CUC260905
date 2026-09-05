using QFramework;
using UnityEngine;

namespace CUC260905.Placement
{
    /// <summary>进入放置模式（prefab 已选定）。</summary>
    public readonly struct PlacementStartedEvent : IEvent
    {
        public readonly GameObject Prefab;

        public PlacementStartedEvent(GameObject prefab)
        {
            Prefab = prefab;
        }
    }

    /// <summary>放置模式被取消。</summary>
    public readonly struct PlacementCancelledEvent : IEvent
    {
    }

    /// <summary>成功放置一个实例。</summary>
    public readonly struct PlacementPlacedEvent : IEvent
    {
        public readonly GameObject Prefab;
        public readonly Vector3 WorldPosition;
        public readonly GameObject Instance;

        public PlacementPlacedEvent(GameObject prefab, Vector3 worldPosition, GameObject instance)
        {
            Prefab = prefab;
            WorldPosition = worldPosition;
            Instance = instance;
        }
    }
}
