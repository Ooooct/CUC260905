using System;
using System.Collections.Generic;
using QFramework;
using UnityEngine;

namespace CUC260905.Interaction
{
    /// <summary>输入信息的解释器</summary>
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
        private ITargetResolver mTargetResolver;
        private IPointerIntentModel mPointerIntentModel;
        private IPointerFrameSink mFrameSink;
        private IPlacementInputGate mPlacementGate;
        private Vector2 mLastScreenPosition;

        protected override void OnInit()
        {
            // 依赖由 Architecture 统一注册，System 不创建具体 Unity Adapter。
            mInputUtility = this.GetUtility<IInputSourceUtility>();
            mTargetResolver = this.GetUtility<ITargetResolver>();
            mPointerIntentModel = this.GetModel<IPointerIntentModel>();

            if (mInputUtility == null ||
                mTargetResolver == null ||
                mPointerIntentModel == null)
            {
                throw new InvalidOperationException(
                    "InteractionInputSystem 初始化前必须注册输入、目标解析 Utility 与 PointerIntentModel。");
            }

            // 指针帧数据源与输入门控为可选依赖：未注册放置域时行为与旧版一致。
            mFrameSink = this.GetUtility<IPointerFrameSink>();
            mPlacementGate = this.GetUtility<IPlacementInputGate>();
        }

        /// <summary>采集本帧信号，逐个解析目标，再交给 Model 解释意图。</summary>
        public void ProcessFrame(float unscaledTime)
        {
            mSignals.Clear();
            mInputUtility.CollectSignals(mSignals, unscaledTime);

            foreach (PointerSignal signal in mSignals)
            {
                mLastScreenPosition = signal.ScreenPosition;
            }

            // 放置模式独占世界输入，但仍发布本帧数据，供放置与相机浏览等消费者使用。
            // 暂停时的能力筛选由 IntentDispatcher 完成：仅显式声明可暂停拖拽的能力会收到意图。
            bool suppressed = mPlacementGate != null && mPlacementGate.IsBlocked;
            if (suppressed)
            {
                PublishFrame();
                return;
            }

            foreach (PointerSignal signal in mSignals)
            {
                // 未命中时 Hit 仍尽量保留 Ray，供拖拽能力处理自由空间位置。
                mTargetResolver.TryResolve(signal, out InteractionHit hit);
                mPointerIntentModel.Process(signal, hit);
            }

            PublishFrame();
        }

        private void PublishFrame()
        {
            PointerFrameEvent frame = new PointerFrameEvent(mLastScreenPosition, mSignals);
            mFrameSink?.Write(frame);
            // 同时广播为事件：需要观察原始指针（如右键取消连线）的表现层可直接订阅。
            this.SendEvent(frame);
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
