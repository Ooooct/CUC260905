using UnityEngine;

namespace CUC260905.Interaction
{
    /// <summary>目标解析使用的 Unity Physics 域。</summary>
    public enum InteractionPhysicsMode
    {
        Physics3D,
        Physics2D
    }

    /// <summary>Controller 传给 InteractionArchitecture 的单场景输入装配参数。</summary>
    public readonly struct InputConfig
    {
        public readonly Camera Camera;
        public readonly InteractionPhysicsMode PhysicsMode;
        public readonly LayerMask LayerMask;
        public readonly float MaxDistance;
        public readonly float DragThresholdPixels;

        public InputConfig(
            Camera camera,
            InteractionPhysicsMode physicsMode,
            LayerMask layerMask,
            float maxDistance,
            float dragThresholdPixels)
        {
            Camera = camera;
            PhysicsMode = physicsMode;
            LayerMask = layerMask;
            MaxDistance = maxDistance;
            DragThresholdPixels = dragThresholdPixels;
        }
    }
}
