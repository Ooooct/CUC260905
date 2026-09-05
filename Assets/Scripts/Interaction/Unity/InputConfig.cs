using UnityEngine;

namespace CUC260905.Interaction
{
    /// <summary>目标解析使用的 Unity Physics 域。</summary>
    public enum InteractionPhysicsMode
    {
        Physics3D,
        Physics2D
    }

    /// <summary>Controller 传给 GameArchitecture 的单场景输入装配参数。</summary>
    public readonly struct InputConfig
    {
        public readonly Camera Camera;
        public readonly InteractionPhysicsMode PhysicsMode;
        public readonly LayerMask LayerMask;
        public readonly float MaxDistance;
        public readonly float DragThresholdPixels;

        /// <summary>放置预览与实例落点所在的固定世界 z 平面。</summary>
        public readonly float PlacementZ;

        public InputConfig(
            Camera camera,
            InteractionPhysicsMode physicsMode,
            LayerMask layerMask,
            float maxDistance,
            float dragThresholdPixels,
            float placementZ = 0.0f)
        {
            Camera = camera;
            PhysicsMode = physicsMode;
            LayerMask = layerMask;
            MaxDistance = maxDistance;
            DragThresholdPixels = dragThresholdPixels;
            PlacementZ = placementZ;
        }
    }
}
