using QFramework;
using UnityEngine;

namespace CUC260905.Placement
{
    /// <summary>把屏幕坐标映射到放置平面世界坐标，并判定指针是否落在 UI 上。</summary>
    public interface IWorldPointerMapper : IUtility
    {
        bool TryMapScreenToWorld(Vector2 screenPosition, out Vector3 worldPosition);

        bool IsOverUI(Vector2 screenPosition);
    }
}
