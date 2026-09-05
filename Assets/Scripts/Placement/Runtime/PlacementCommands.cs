using QFramework;
using UnityEngine;

namespace CUC260905.Placement
{
    /// <summary>点击工具按钮后发送：进入放置模式并选定 prefab。</summary>
    public sealed class BeginPlacementCommand : AbstractCommand
    {
        private readonly GameObject mPrefab;

        public BeginPlacementCommand(GameObject prefab)
        {
            mPrefab = prefab;
        }

        protected override void OnExecute()
        {
            if (mPrefab != null)
            {
                this.GetSystem<IPlacementSystem>().Begin(mPrefab);
            }
        }
    }

    /// <summary>取消当前放置流程。</summary>
    public sealed class CancelPlacementCommand : AbstractCommand
    {
        protected override void OnExecute()
        {
            this.GetSystem<IPlacementSystem>().Cancel();
        }
    }
}
