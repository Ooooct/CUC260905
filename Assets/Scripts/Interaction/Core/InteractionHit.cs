using UnityEngine;

namespace CUC260905.Interaction
{
    /// <summary>一次目标解析结果；不向上层暴露 Collider 或 GameObject。</summary>
    public readonly struct InteractionHit
    {
        /// <summary>解析后的逻辑对象根节点。</summary>
        public readonly IInteractionTarget Target;

        /// <summary>由目标 Camera 根据屏幕位置生成的世界射线。</summary>
        public readonly Ray Ray;

        /// <summary>Collider 被射线命中的世界坐标。</summary>
        public readonly Vector3 Point;

        /// <summary>Collider 在命中点处的世界法线。</summary>
        public readonly Vector3 Normal;

        /// <summary>命中点距 Ray 起点的距离。</summary>
        public readonly float Distance;

        public InteractionHit(
            IInteractionTarget target,
            Ray ray,
            Vector3 point,
            Vector3 normal,
            float distance)
        {
            Target = target;
            Ray = ray;
            Point = point;
            Normal = normal;
            Distance = distance;
        }

        /// <summary>当前射线是否解析到仍可交互的逻辑对象。</summary>
        public bool HasTarget
        {
            get { return Target != null && Target.IsAvailable; }
        }
    }
}
