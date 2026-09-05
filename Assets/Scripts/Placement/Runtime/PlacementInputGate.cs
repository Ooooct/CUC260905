using CUC260905.Interaction;
using QFramework;

namespace CUC260905.Placement
{
    /// <summary>放置激活时阻塞 Interaction 的意图解释，实现"放置期间抑制世界点击"。</summary>
    public sealed class PlacementInputGate : IPlacementInputGate
    {
        private readonly IPlacementModel mModel;

        public PlacementInputGate(IPlacementModel model)
        {
            mModel = model;
        }

        public bool IsBlocked
        {
            get { return mModel != null && mModel.IsPlacing.Value; }
        }
    }
}
