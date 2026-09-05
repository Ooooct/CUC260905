using QFramework;
using UnityEngine;

namespace CUC260905.Interaction
{
    /// <summary>将屏幕位置解析为逻辑交互目标的 Unity Adapter。</summary>
    public interface ITargetResolver : IUtility
    {
        bool TryResolve(in PointerSignal signal, out InteractionHit hit);
    }

    /// <summary>
    /// 基于 3D Physics.Raycast 的目标解析 Adapter。
    /// 命中 Collider 后向父节点查找 InteractionTarget，并返回逻辑对象而非 Collider。
    /// </summary>
    public sealed class Physics3DTargetResolver : ITargetResolver
    {
        // Camera 由组合根显式传入，避免内部隐式查询 Camera.main。
        private readonly Camera mCamera;
        private readonly LayerMask mLayerMask;
        private readonly float mMaxDistance;

        public Physics3DTargetResolver(
            Camera camera,
            LayerMask layerMask,
            float maxDistance)
        {
            mCamera = camera;
            mLayerMask = layerMask;
            mMaxDistance = maxDistance;
        }

        public bool TryResolve(in PointerSignal signal, out InteractionHit hit)
        {
            hit = default;

            // 没有可用 Camera 时无法把屏幕坐标转换为世界射线。
            if (mCamera == null)
            {
                return false;
            }

            Ray ray = mCamera.ScreenPointToRay(signal.ScreenPosition);
            RaycastHit physicsHit;

            // 即使未命中，也保留 Ray，供拖拽层投影当前指针到业务平面。
            hit = new InteractionHit(
                null,
                ray,
                Vector3.zero,
                Vector3.zero,
                0.0f);

            if (!Physics.Raycast(
                    ray,
                    out physicsHit,
                    mMaxDistance,
                    mLayerMask,
                    QueryTriggerInteraction.Ignore))
            {
                return false;
            }

            // Collider 可以位于交互对象子节点，逻辑目标始终以根组件为准。
            InteractionTarget target =
                physicsHit.collider.GetComponentInParent<InteractionTarget>();

            if (target == null || !target.IsAvailable)
            {
                return false;
            }

            hit = new InteractionHit(
                target,
                ray,
                physicsHit.point,
                physicsHit.normal,
                physicsHit.distance);
            return true;
        }
    }
}
