using QFramework;
using UnityEngine;

namespace CUC260905.Browsing
{
    /// <summary>玩家在空白区域按住左键，开始平移摄像机时发送。</summary>
    public readonly struct CameraBrowsingPanBeganEvent : IEvent
    {
        /// <summary>按下位置的屏幕像素坐标。</summary>
        public readonly Vector2 ScreenPosition;

        public CameraBrowsingPanBeganEvent(Vector2 screenPosition)
        {
            ScreenPosition = screenPosition;
        }
    }

    /// <summary>平移结束（松开左键或被取消）时发送；携带释放时刻的焦点速度，供惯性/表现层使用。</summary>
    public readonly struct CameraBrowsingPanEndedEvent : IEvent
    {
        /// <summary>焦点世界速度（世界单位/秒）。</summary>
        public readonly Vector3 FocalVelocity;

        public CameraBrowsingPanEndedEvent(Vector3 focalVelocity)
        {
            FocalVelocity = focalVelocity;
        }
    }

    /// <summary>缩放值发生变化时发送；Previous/New 为 orthographicSize 或 fieldOfView。</summary>
    public readonly struct CameraBrowsingZoomChangedEvent : IEvent
    {
        /// <summary>变化前的缩放值。</summary>
        public readonly float PreviousZoom;

        /// <summary>变化后的缩放值。</summary>
        public readonly float NewZoom;

        /// <summary>本次缩放发生时指针所在的屏幕坐标（缩放锚点）。</summary>
        public readonly Vector2 ScreenPosition;

        public CameraBrowsingZoomChangedEvent(float previousZoom, float newZoom, Vector2 screenPosition)
        {
            PreviousZoom = previousZoom;
            NewZoom = newZoom;
            ScreenPosition = screenPosition;
        }
    }
}
