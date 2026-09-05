using System.Collections.Generic;
using CUC260905.Interaction;
using CUC260905.Visual;
using QFramework;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CUC260905.Placement
{
    /// <summary>
    /// 表现层：响应 IPlacementModel 的变化，管理半透明幽灵预览。
    /// 不持有业务规则；位置在 LateUpdate 应用，避免与相机/交互更新顺序冲突。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlacementPreviewView : MonoBehaviour, IController
    {
        [SerializeField, Range(0.0f, 1.0f)] private float mPreviewAlpha = 0.3f;
        [SerializeField] private Transform mPreviewRoot;
        [SerializeField] private bool mHideOverUI = true;

        private IPlacementModel mModel;
        private GameObject mGhost;
        private readonly List<SpriteRenderer> mGhostRenderers = new List<SpriteRenderer>();
        private readonly List<Collider2D> mGhostColliders = new List<Collider2D>();
        private Vector3 mGhostPosition;
        private bool mHasPosition;
        private bool mInitialized;

        private IUnRegister mPlacingRegister;
        private IUnRegister mPrefabRegister;
        private IUnRegister mPositionRegister;

        // Start 在场景内所有 Awake 之后执行，保证 GameArchitecture 已被 InputController 装配。
        private void Start()
        {
            EnsureInitialized();
        }

        private void EnsureInitialized()
        {
            if (mInitialized)
            {
                return;
            }

            mModel = this.GetModel<IPlacementModel>();
            mPlacingRegister = mModel.IsPlacing.RegisterWithInitValue(OnPlacingChanged);
            mPrefabRegister = mModel.SelectedPrefab.Register(OnSelectedPrefabChanged);
            mPositionRegister = mModel.PointerWorldPosition.Register(OnPointerPositionChanged);
            mInitialized = true;
        }

        private void OnDestroy()
        {
            mPlacingRegister?.UnRegister();
            mPrefabRegister?.UnRegister();
            mPositionRegister?.UnRegister();
            DestroyGhost();
        }

        private void OnPlacingChanged(bool placing)
        {
            if (placing)
            {
                CreateGhost();
            }
            else
            {
                DestroyGhost();
            }
        }

        private void OnSelectedPrefabChanged(GameObject prefab)
        {
            // 放置期间切换 prefab：重建幽灵。
            if (mModel.IsPlacing.Value)
            {
                CreateGhost();
            }
        }

        private void OnPointerPositionChanged(Vector3 worldPosition)
        {
            mGhostPosition = worldPosition;
            mHasPosition = true;
        }

        private void LateUpdate()
        {
            if (mGhost == null)
            {
                return;
            }

            mGhost.transform.position = mGhostPosition;

            if (mHideOverUI &&
                EventSystem.current != null &&
                EventSystem.current.IsPointerOverGameObject())
            {
                mGhost.SetActive(false);
            }
            else
            {
                // 尚未获得首个有效坐标前保持隐藏，避免在原点闪现。
                mGhost.SetActive(mHasPosition);
            }
        }

        private void CreateGhost()
        {
            DestroyGhost();

            GameObject prefab = mModel.SelectedPrefab.Value;
            if (prefab == null)
            {
                return;
            }

            Transform parent = mPreviewRoot != null ? mPreviewRoot : transform;
            mGhost = Instantiate(prefab, parent);
            mGhost.name = "PlacementPreview";
            mGhost.transform.localPosition = Vector3.zero;

            // 幽灵仅作视觉预览：禁用全部领域控制器（IController），
            // 避免其 Start 执行业务逻辑——用户节点随机配色、节点登记、数据包调度等。
            MonoBehaviour[] behaviours = mGhost.GetComponentsInChildren<MonoBehaviour>(true);
            foreach (MonoBehaviour behaviour in behaviours)
            {
                if (behaviour is IController)
                {
                    behaviour.enabled = false;
                }
            }

            // 幽灵不播放入场动画：入场动画属于"节点放置/出现"的表现，幽灵应保持完整尺寸静止预览。
            NodeEntranceAnimation[] entranceAnimations = mGhost.GetComponentsInChildren<NodeEntranceAnimation>(true);
            foreach (NodeEntranceAnimation entranceAnimation in entranceAnimations)
            {
                entranceAnimation.enabled = false;
            }

            mGhostRenderers.Clear();
            mGhost.GetComponentsInChildren(true, mGhostRenderers);
            foreach (SpriteRenderer renderer in mGhostRenderers)
            {
                Color color = renderer.color;
                color.a = mPreviewAlpha;
                renderer.color = color;
            }

            // 幽灵不参与物理拾取，避免干扰世界点击解析。
            mGhostColliders.Clear();
            mGhost.GetComponentsInChildren(true, mGhostColliders);
            foreach (Collider2D collider in mGhostColliders)
            {
                collider.enabled = false;
            }

            mHasPosition = false;
            mGhost.SetActive(false);
        }

        private void DestroyGhost()
        {
            if (mGhost != null)
            {
                Destroy(mGhost);
                mGhost = null;
            }

            mGhostRenderers.Clear();
            mGhostColliders.Clear();
        }

        IArchitecture IBelongToArchitecture.GetArchitecture()
        {
            return GameArchitecture.Interface;
        }
    }
}
