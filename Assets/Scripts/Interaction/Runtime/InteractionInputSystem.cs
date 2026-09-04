using System;
using System.Collections.Generic;
using QFramework;

namespace CUC260905.Interaction
{
    /// <summary>QFramework 输入协调 System；由 Controller 在 Unity Update 中驱动。</summary>
    public interface IInteractionInputSystem : ISystem
    {
        void ProcessFrame(float unscaledTime);

        void CancelAll();
    }

    /// <summary>
    /// 协调 Input Utility、目标解析 Utility 与意图 Model。
    /// 本类不自行轮询 Unity Update；Controller 调用 ProcessFrame 才处理一帧。
    /// </summary>
    public sealed class InteractionInputSystem : AbstractSystem, IInteractionInputSystem
    {
        private readonly List<PointerSignal> mSignals = new List<PointerSignal>();

        private IInputSourceUtility mInputUtility;
        private IInteractionTargetResolverUtility mTargetResolverUtility;
        private IPointerIntentModel mPointerIntentModel;

        protected override void OnInit()
        {
            // 依赖由 Architecture 统一注册，System 不创建具体 Unity Adapter。
            mInputUtility = this.GetUtility<IInputSourceUtility>();
            mTargetResolverUtility = this.GetUtility<IInteractionTargetResolverUtility>();
            mPointerIntentModel = this.GetModel<IPointerIntentModel>();

            if (mInputUtility == null ||
                mTargetResolverUtility == null ||
                mPointerIntentModel == null)
            {
                throw new InvalidOperationException(
                    "InteractionInputSystem 初始化前必须注册输入、目标解析 Utility 与 PointerIntentModel。");
            }
        }

        /// <summary>采集本帧信号，逐个解析目标，再交给 Model 解释意图。</summary>
        public void ProcessFrame(float unscaledTime)
        {
            mSignals.Clear();
            mInputUtility.CollectSignals(mSignals, unscaledTime);

            foreach (PointerSignal signal in mSignals)
            {
                // 未命中时 Hit 仍尽量保留 Ray，供拖拽能力处理自由空间位置。
                mTargetResolverUtility.TryResolve(signal, out InteractionHit hit);
                mPointerIntentModel.Process(signal, hit);
            }
        }

        /// <summary>由 Controller 的禁用、切场景等生命周期边界调用。</summary>
        public void CancelAll()
        {
            mPointerIntentModel.CancelAll();
        }

        protected override void OnDeinit()
        {
            CancelAll();
            mSignals.Clear();
        }
    }
}
