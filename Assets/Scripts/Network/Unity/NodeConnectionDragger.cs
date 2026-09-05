using CUC260905.Interaction;
using QFramework;
using UnityEngine;

namespace CUC260905.Network
{
    /// <summary>
    /// 节点拖拽连线手势（表现层）。
    /// 由 NetworkConnectionController 在节点登记时自动注入到节点根物体，
    /// 因此场景既有节点与放置系统新建节点无需手工配置即可连线。
    /// 职责单一：把 DragIntent 生命周期转发给 INetworkConnectionTool，
    /// 不持有任何连线规则；点击行为仍由 IClickable（用户/服务器节点控制器）处理。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(NetworkNodeRegistrar))]
    public sealed class NodeConnectionDragger : MonoBehaviour, IController, IDraggable
    {
        private NetworkNodeRegistrar mRegistrar;
        private INetworkConnectionTool mTool;
        private bool mDragging;

        private void Awake()
        {
            mRegistrar = GetComponent<NetworkNodeRegistrar>();
        }

        public InteractionResult OnDrag(in DragIntent intent)
        {
            EnsureTool();
            if (mTool == null || mRegistrar == null || string.IsNullOrWhiteSpace(mRegistrar.NodeId))
            {
                return new InteractionResult(InteractionResultStatus.Rejected);
            }

            string nodeId = mRegistrar.NodeId;
            switch (intent.Phase)
            {
                case DragPhase.Begin:
                    mDragging = mTool.BeginPreview(nodeId, intent.Pointer.WorldRay);
                    break;
                case DragPhase.Update:
                    if (mDragging)
                    {
                        mTool.UpdatePreview(intent.Pointer.WorldRay);
                    }

                    break;
                case DragPhase.End:
                    if (mDragging)
                    {
                        mTool.EndPreview(nodeId, intent.CurrentHit);
                    }

                    mDragging = false;
                    break;
                case DragPhase.Cancel:
                    if (mDragging)
                    {
                        mTool.CancelPreview(nodeId);
                    }

                    mDragging = false;
                    break;
                default:
                    return new InteractionResult(InteractionResultStatus.Rejected);
            }

            return new InteractionResult(InteractionResultStatus.Handled);
        }

        // 工具惰性解析：NetworkConnectionController 的 Start 可能在之后才注册自身。
        private void EnsureTool()
        {
            mTool ??= this.GetUtility<INetworkConnectionTool>();
        }

        IArchitecture IBelongToArchitecture.GetArchitecture()
        {
            return GameArchitecture.Interface;
        }
    }
}
