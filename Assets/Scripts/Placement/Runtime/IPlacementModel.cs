using QFramework;
using UnityEngine;

namespace CUC260905.Placement
{
    /// <summary>放置域状态。</summary>
    public interface IPlacementModel : IModel
    {
        /// <summary>当前选定的待放置 prefab。</summary>
        IBindableProperty<GameObject> SelectedPrefab { get; }

        /// <summary>是否处于放置模式。</summary>
        IBindableProperty<bool> IsPlacing { get; }

        /// <summary>预览应跟随的世界坐标。</summary>
        IBindableProperty<Vector3> PointerWorldPosition { get; }
    }

    /// <summary>放置状态 Model；只保存数据，不依赖表现层对象。</summary>
    public sealed class PlacementModel : AbstractModel, IPlacementModel
    {
        public IBindableProperty<GameObject> SelectedPrefab { get; } = new BindableProperty<GameObject>();

        public IBindableProperty<bool> IsPlacing { get; } = new BindableProperty<bool>(false);

        public IBindableProperty<Vector3> PointerWorldPosition { get; } = new BindableProperty<Vector3>();

        protected override void OnInit()
        {
        }

        protected override void OnDeinit()
        {
            SelectedPrefab.Value = null;
            IsPlacing.Value = false;
            PointerWorldPosition.Value = default;
        }
    }
}
