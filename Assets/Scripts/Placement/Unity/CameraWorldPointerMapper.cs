using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CUC260905.Placement
{
    /// <summary>
    /// 用指定 Camera 把屏幕坐标映射到固定 z 平面（射线与平面求交）。
    /// UI 判定使用 EventSystem 指针命中。
    /// </summary>
    public sealed class CameraWorldPointerMapper : IWorldPointerMapper
    {
        private readonly Camera mCamera;
        private readonly float mPlaneZ;

        public CameraWorldPointerMapper(Camera camera, float planeZ)
        {
            mCamera = camera ?? throw new ArgumentNullException(nameof(camera));
            mPlaneZ = planeZ;
        }

        public bool TryMapScreenToWorld(Vector2 screenPosition, out Vector3 worldPosition)
        {
            worldPosition = default;
            if (mCamera == null)
            {
                return false;
            }

            Ray ray = mCamera.ScreenPointToRay(screenPosition);
            // 视线平行于放置平面时为退化情况，直接取射线原点的平面投影。
            if (Mathf.Approximately(ray.direction.z, 0.0f))
            {
                Vector3 origin = ray.origin;
                worldPosition = new Vector3(origin.x, origin.y, mPlaneZ);
                return true;
            }

            float t = (mPlaneZ - ray.origin.z) / ray.direction.z;
            worldPosition = ray.origin + ray.direction * t;
            return true;
        }

        public bool IsOverUI(Vector2 screenPosition)
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }
    }
}
