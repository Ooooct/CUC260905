using QFramework;
using UnityEngine;

namespace CUC260905.Interaction
{
    /// <summary>
    /// Unity 表现层 Controller。负责把场景参数装配进 Architecture，并在 Update 驱动输入 System。
    /// 不解释意图，不直接执行业务能力。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InteractionInputController : MonoBehaviour, IController
    {
        [Header("Target Resolver")]
        [SerializeField] private Camera mCamera;
        [SerializeField] private InteractionPhysicsMode mPhysicsMode = InteractionPhysicsMode.Physics3D;
        [SerializeField] private LayerMask mLayerMask = ~0;
        [SerializeField] private float mMaxDistance = 100.0f;

        [Header("Intent Model")]
        [SerializeField] private float mDragThresholdPixels = 8.0f;

        private IInteractionInputSystem mInputSystem;
        private bool mOwnsArchitecture;

        private void Awake()
        {
            // 必须在首次访问 Interface 前配置，避免静态 Architecture 绑定错误场景 Camera。
            InteractionArchitecture.Configure(new InteractionInputConfiguration(
                mCamera,
                mPhysicsMode,
                mLayerMask,
                mMaxDistance,
                mDragThresholdPixels));

            mInputSystem = this.GetSystem<IInteractionInputSystem>();
            mOwnsArchitecture = true;
        }

        private void Update()
        {
            mInputSystem.ProcessFrame(Time.unscaledTime);
        }

        private void OnDisable()
        {
            // 焦点或场景切换中断输入时，先让拖拽和悬浮收到正常收束。
            if (mInputSystem != null)
            {
                mInputSystem.CancelAll();
            }
        }

        private void OnDestroy()
        {
            if (mOwnsArchitecture)
            {
                InteractionArchitecture.Deinitialize();
            }
        }

        IArchitecture IBelongToArchitecture.GetArchitecture()
        {
            return InteractionArchitecture.Interface;
        }
    }
}
