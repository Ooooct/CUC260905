using UnityEngine;

namespace CUC260905.Interaction
{
    /// <summary>
    /// 基于 2D Physics 的目标解析 Adapter。
    /// 使用 Camera 的屏幕射线与 XY 平面上的 Collider2D 相交，返回逻辑对象而非 Collider2D。
    /// Trigger 是否参与命中遵循 Physics2D.queriesHitTriggers 项目设置。
    /// </summary>
    public sealed class Physics2DTargetResolver : ITargetResolver
    {
        // Camera 由组合根显式传入，避免内部隐式查询 Camera.main。
        private readonly Camera mCamera;
        private readonly LayerMask mLayerMask;
        private readonly float mMaxDistance;

        public Physics2DTargetResolver(
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

            if (mCamera == null)
            {
                return false;
            }

            Ray ray = mCamera.ScreenPointToRay(signal.ScreenPosition);

            // 即使未命中，也保留 Ray，供拖拽层投影当前指针到业务平面。
            hit = new InteractionHit(
                null,
                ray,
                Vector3.zero,
                Vector3.zero,
                0.0f);

            RaycastHit2D physicsHit = Physics2D.GetRayIntersection(
                ray,
                mMaxDistance,
                mLayerMask);

            if (physicsHit.collider == null)
            {
                return false;
            }

            // Collider2D 可以位于交互对象子节点，逻辑目标始终以根组件为准。
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
