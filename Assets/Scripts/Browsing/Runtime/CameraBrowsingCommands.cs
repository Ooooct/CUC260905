using QFramework;
using UnityEngine;

namespace CUC260905.Browsing
{
    /// <summary>命令：把摄像机焦点移动到指定世界坐标（表现层通常用 DOTween 缓动呈现）。</summary>
    public sealed class FocusCameraCommand : AbstractCommand
    {
        private readonly Vector3 mWorldPosition;

        public FocusCameraCommand(Vector3 worldPosition)
        {
            mWorldPosition = worldPosition;
        }

        protected override void OnExecute()
        {
            this.GetSystem<ICameraBrowsingSystem>().MoveTo(mWorldPosition);
        }
    }

    /// <summary>命令：设置摄像机缩放值（正交相机为 orthographicSize，透视为 fieldOfView）。</summary>
    public sealed class SetCameraZoomCommand : AbstractCommand
    {
        private readonly float mZoom;

        public SetCameraZoomCommand(float zoom)
        {
            mZoom = zoom;
        }

        protected override void OnExecute()
        {
            this.GetSystem<ICameraBrowsingSystem>().ZoomTo(mZoom);
        }
    }
}
