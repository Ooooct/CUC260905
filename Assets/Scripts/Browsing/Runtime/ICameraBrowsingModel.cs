using QFramework;
using UnityEngine;

namespace CUC260905.Browsing
{
    /// <summary>
    /// 浏览系统的状态模型：逻辑焦点与缩放。
    /// 只保存数据，不持有任何表现层对象（不引用 Camera、不创建 Tween）。
    /// </summary>
    public interface ICameraBrowsingModel : IModel
    {
        /// <summary>摄像机中心对准的世界焦点（通常位于游戏平面 z = 0）。</summary>
        IBindableProperty<Vector3> FocalPoint { get; }

        /// <summary>当前缩放值：正交相机为 orthographicSize，透视相机为 fieldOfView，越小越放大。</summary>
        IBindableProperty<float> Zoom { get; }

        /// <summary>玩家是否正在空白区域平移摄像机。</summary>
        IBindableProperty<bool> IsPanning { get; }
    }

    /// <summary>浏览状态模型的默认实现。</summary>
    public sealed class CameraBrowsingModel : AbstractModel, ICameraBrowsingModel
    {
        public IBindableProperty<Vector3> FocalPoint { get; }

        public IBindableProperty<float> Zoom { get; }

        public IBindableProperty<bool> IsPanning { get; }

        public CameraBrowsingModel(Vector3 initialFocalPoint, float initialZoom)
        {
            FocalPoint = new BindableProperty<Vector3>(initialFocalPoint);
            Zoom = new BindableProperty<float>(initialZoom);
            IsPanning = new BindableProperty<bool>(false);
        }

        protected override void OnInit()
        {
        }
    }
}
