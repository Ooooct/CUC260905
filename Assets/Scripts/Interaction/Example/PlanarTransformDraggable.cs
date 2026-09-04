using UnityEngine;
using CUC260905.Interaction;

namespace CUC260905.Interaction.Example
{
    /// <summary>以拖拽开始位置为基准，在固定世界平面移动目标 Transform。</summary>
    [DisallowMultipleComponent]
    public sealed class PlanarTransformDraggable : MonoBehaviour, IDraggable
    {
        [SerializeField] private Transform mTarget;
        [SerializeField] private Vector3 mPlaneNormal = Vector3.up;

        private Plane mDragPlane;
        private Vector3 mOffset;
        private Vector3 mStartPosition;
        private bool mIsDragging;

        private void Awake()
        {
            if (mTarget == null) mTarget = transform;
        }

        public InteractionResult OnDrag(in DragIntent intent)
        {
            if (mTarget == null) return new InteractionResult(InteractionResultStatus.Rejected);

            switch (intent.Phase)
            {
                case DragPhase.Begin: return BeginDrag(intent);
                case DragPhase.Update: return MoveToPointer(intent);
                case DragPhase.End: return EndDrag(intent);
                case DragPhase.Cancel: return CancelDrag();
                default: return new InteractionResult(InteractionResultStatus.Rejected);
            }
        }

        private InteractionResult BeginDrag(in DragIntent intent)
        {
            Vector3 normal = mPlaneNormal.sqrMagnitude > 0.0f ? mPlaneNormal : Vector3.up;
            mStartPosition = mTarget.position;
            mDragPlane = new Plane(normal, mStartPosition);
            if (!TryGetPlanePoint(intent.Pointer.WorldRay, out Vector3 point))
                return new InteractionResult(InteractionResultStatus.Rejected);

            mOffset = mStartPosition - point;
            mIsDragging = true;
            return new InteractionResult(InteractionResultStatus.Handled);
        }

        private InteractionResult MoveToPointer(in DragIntent intent)
        {
            if (!mIsDragging || !TryGetPlanePoint(intent.Pointer.WorldRay, out Vector3 point))
                return new InteractionResult(InteractionResultStatus.Rejected);

            mTarget.position = point + mOffset;
            return new InteractionResult(InteractionResultStatus.Handled);
        }

        private InteractionResult EndDrag(in DragIntent intent)
        {
            InteractionResult result = MoveToPointer(intent);
            mIsDragging = false;
            return result;
        }

        private InteractionResult CancelDrag()
        {
            if (!mIsDragging) return new InteractionResult(InteractionResultStatus.Rejected);

            mTarget.position = mStartPosition;
            mIsDragging = false;
            return new InteractionResult(InteractionResultStatus.Handled);
        }

        private bool TryGetPlanePoint(Ray ray, out Vector3 point)
        {
            if (mDragPlane.Raycast(ray, out float distance))
            {
                point = ray.GetPoint(distance);
                return true;
            }

            point = default;
            return false;
        }
    }
}
