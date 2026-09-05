using System;
using System.Collections.Generic;
using CUC260905.Feedback;
using CUC260905.Game;
using CUC260905.Interaction;
using CUC260905.Message;
using CUC260905.Placement;
using QFramework;
using UnityEngine;

namespace CUC260905.Network
{
    /// <summary>
    /// 拖拽连线的表现层工具接口。节点上的 NodeConnectionDragger 只采集手势，
    /// 预览的显隐、端点投影与最终的规则校验都由实现方（NetworkConnectionController）完成。
    /// </summary>
    public interface INetworkConnectionTool : IUtility
    {
        /// <summary>开始从指定节点拖出预览线；返回是否成功开始。</summary>
        bool BeginPreview(string fromNodeId, Ray pointerRay);

        /// <summary>拖拽过程中更新预览线自由端。</summary>
        void UpdatePreview(Ray pointerRay);

        /// <summary>释放：完成或取消当前预览线，返回连线裁决结果。</summary>
        ConnectionVerdict EndPreview(string fromNodeId, in InteractionHit releaseHit);

        /// <summary>取消当前预览线（拖拽被 Cancel 时调用）。</summary>
        void CancelPreview(string fromNodeId);
    }

    /// <summary>
    /// 节点连线的表现层 Controller：
    /// · 维护 NodeId → Transform 位置表并实现 INodePositionProvider（供交叉检查读取坐标）；
    /// · 监听拓扑事件，为每条无向边生成/销毁拉伸 Sprite 线段视图（世界单位粗细）；
    /// · 监听节点与数据包结果事件，复用 IFeedbackSystem 在对应节点显示背景反馈圆；
    /// · 持有拖拽预览线，并作为 INetworkConnectionTool 把手势转成 INetworkConnectionSystem 调用；
    /// · 连线（含预览）的粗细、颜色、材质、排序均为可配置项。
    ///
    /// 节点上的 NodeConnectionDragger 在节点登记时由本控制器自动注入
    /// （对场景既有节点与放置系统新建节点统一生效），无需手工编辑 prefab。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetworkConnectionController : MonoBehaviour,
        IController,
        INodePositionProvider,
        INetworkConnectionTool
    {
        [Header("Edge Visual")]
        [Tooltip("已建立连线的线宽（世界单位，随相机缩放；默认取旧像素值的 1/10）。")]
        [SerializeField] private float mEdgeWidth = 0.6f;
        [SerializeField] private Color mEdgeColor = new Color(0.62f, 0.78f, 1.0f, 1.0f);
        [Tooltip("连线材质；留空时使用 Sprites/Default 自动创建的材质。")]
        [SerializeField] private Material mEdgeMaterial;
        [SerializeField] private int mEdgeSortingOrder = -1;

        [Header("Preview Visual")]
        [Tooltip("拖拽预览线的线宽（世界单位，随相机缩放；默认取旧像素值的 1/10）。")]
        [SerializeField] private float mPreviewWidth = 0.4f;
        [SerializeField] private Color mPreviewColor = new Color(0.55f, 0.55f, 0.55f, 0.6f);
        [Tooltip("预览线材质；留空时使用 Sprites/Default 自动创建的材质。")]
        [SerializeField] private Material mPreviewMaterial;
        [SerializeField] private int mPreviewSortingOrder = 1;

        [Header("Collision")]
        [Tooltip("已建立连线的命中碰撞体厚度（世界单位）。右键点击连线取消该连线。")]
        [SerializeField] private float mLineHitThickness = 0.35f;

        // 位置表：NodeId → 节点根 Transform。由登记事件与初始扫描填充。
        private readonly Dictionary<string, Transform> mNodeTransforms =
            new Dictionary<string, Transform>(StringComparer.Ordinal);

        // 边视图表：规范化键 → 视图。
        private readonly Dictionary<NetworkEdgeKey, EdgeLineView> mEdgeViews =
            new Dictionary<NetworkEdgeKey, EdgeLineView>();

        // 碰撞体 → 边视图：右键命中连线时据此定位要取消的边。
        private readonly Dictionary<Collider2D, EdgeLineView> mColliderViews =
            new Dictionary<Collider2D, EdgeLineView>();

        private INetworkConnectionSystem mConnectionSystem;
        private INetworkTopologySystem mTopologySystem;
        private INetworkTopologyModel mModel;
        private IWorldPointerMapper mPointerMapper;
        private IPlacementInputGate mPlacementGate;
        private IGamePauseState mPauseState;
        private SpriteRenderer mPreviewRenderer;
        private Vector3 mPreviewAnchor;
        private Vector3 mPreviewFreeEnd;
        private Material mResolvedEdgeMaterial;
        private Material mResolvedPreviewMaterial;
        private bool mInitialized;
        private string mPreviewFromNodeId;

        [SerializeField]
        [Tooltip("连线失败原因写入的提示终端标识。")]
        private string mMessageTargetId = "MainTerminal";

        [Header("网络反馈圆")]
        [SerializeField, Min(0f)]
        [Tooltip("新节点登记时显示的灰色背景圆半径（世界单位）。")]
        private float mNodeAppearedRadius = 2.25f;
        [SerializeField, Min(0f)]
        [Tooltip("数据包成功送达时显示的灰色背景圆半径（世界单位）。")]
        private float mPacketSuccessRadius = 1f;
        [SerializeField, Min(0f)]
        [Tooltip("数据包传输失败时显示的红色背景圆半径（世界单位）。")]
        private float mPacketFailureRadius = 2.25f;
        [SerializeField, Min(0f)]
        [Tooltip("网络反馈圆的淡出时长（秒）。")]
        private float mFeedbackDuration = 0.8f;
        [SerializeField]
        [Tooltip("新节点与数据包成功共用的灰色反馈圆颜色。")]
        private Color mSuccessColor = Color.gray;
        [SerializeField]
        [Tooltip("数据包传输失败时的反馈圆颜色。")]
        private Color mFailureColor = Color.red;

        private IMessageSystem mMessageSystem;
        private IFeedbackSystem mFeedbackSystem;

        private static readonly Plane sNodePlane = new Plane(Vector3.forward, Vector3.zero);

        // 共享白色 1×1 Sprite：拉伸成线段时 scale=(长度, 粗细) 即世界单位粗细。
        private static Sprite sWhiteSprite;

        // Start 晚于 InputController.Awake，确保 GameArchitecture 已装配。
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

            mModel = this.GetModel<INetworkTopologyModel>();
            mConnectionSystem = this.GetSystem<INetworkConnectionSystem>();
            mTopologySystem = this.GetSystem<INetworkTopologySystem>();
            mPointerMapper = this.GetUtility<IWorldPointerMapper>();
            mPlacementGate = this.GetUtility<IPlacementInputGate>();
            mPauseState = this.GetModel<IGamePauseState>();
            mMessageSystem = this.GetSystem<IMessageSystem>();
            mFeedbackSystem = this.GetSystem<IFeedbackSystem>();
            if (mModel == null || mConnectionSystem == null || mTopologySystem == null)
            {
                Debug.LogError(
                    "NetworkConnectionController 需要 INetworkTopologyModel、INetworkConnectionSystem 与 INetworkTopologySystem，" +
                    "请确认场景存在 InputController。",
                    this);
                return;
            }

            // 幂等注册：本控制器同时是位置来源与连线工具。
            IArchitecture architecture = GameArchitecture.Interface;
            if (architecture.GetUtility<INodePositionProvider>() == null)
            {
                architecture.RegisterUtility<INodePositionProvider>(this);
            }

            if (architecture.GetUtility<INetworkConnectionTool>() == null)
            {
                architecture.RegisterUtility<INetworkConnectionTool>(this);
            }

            this.RegisterEvent<NodeRegisteredEvent>(OnNodeRegistered)
                .UnRegisterWhenGameObjectDestroyed(this);
            this.RegisterEvent<NodeUnregisteredEvent>(OnNodeUnregistered)
                .UnRegisterWhenGameObjectDestroyed(this);
            this.RegisterEvent<NodeConnectivityChangedEvent>(OnConnectivityChanged)
                .UnRegisterWhenGameObjectDestroyed(this);
            this.RegisterEvent<PacketTransmittedEvent>(OnPacketTransmitted)
                .UnRegisterWhenGameObjectDestroyed(this);
            this.RegisterEvent<PacketUnreachableEvent>(OnPacketUnreachable)
                .UnRegisterWhenGameObjectDestroyed(this);
            this.RegisterEvent<PointerFrameEvent>(OnPointerFrame)
                .UnRegisterWhenGameObjectDestroyed(this);

            CreatePreviewLine();

            // 场景既有节点：登记在 Start；若其 Start 晚于本控制器，后续 NodeRegisteredEvent 会兜底。
            RegisterSceneNodes();
            mInitialized = true;
        }

        // ---- INodePositionProvider ----

        public bool TryGetNodePosition(string nodeId, out Vector3 position)
        {
            position = default;
            if (this == null || !mNodeTransforms.TryGetValue(nodeId, out Transform nodeTransform) ||
                nodeTransform == null)
            {
                return false;
            }

            position = nodeTransform.position;
            return true;
        }

        // ---- INetworkConnectionTool ----

        public bool BeginPreview(string fromNodeId, Ray pointerRay)
        {
            if (!mInitialized || mPreviewRenderer == null || string.IsNullOrWhiteSpace(fromNodeId) ||
                !mNodeTransforms.TryGetValue(fromNodeId, out Transform fromTransform) ||
                fromTransform == null)
            {
                return false;
            }

            mPreviewFromNodeId = fromNodeId;
            mPreviewAnchor = fromTransform.position;
            mPreviewFreeEnd = ProjectToNodePlane(pointerRay, mPreviewAnchor);
            mPreviewRenderer.enabled = true;
            UpdatePreviewGeometry();
            return true;
        }

        public void UpdatePreview(Ray pointerRay)
        {
            if (mPreviewRenderer == null || !mPreviewRenderer.enabled ||
                string.IsNullOrEmpty(mPreviewFromNodeId))
            {
                return;
            }

            if (mNodeTransforms.TryGetValue(mPreviewFromNodeId, out Transform fromTransform) &&
                fromTransform != null)
            {
                mPreviewAnchor = fromTransform.position;
            }

            mPreviewFreeEnd = ProjectToNodePlane(pointerRay, mPreviewAnchor);
            UpdatePreviewGeometry();
        }

        public ConnectionVerdict EndPreview(string fromNodeId, in InteractionHit releaseHit)
        {
            if (!string.Equals(mPreviewFromNodeId, fromNodeId, StringComparison.Ordinal))
            {
                HidePreview();
                return ConnectionVerdict.InvalidNodeId;
            }

            HidePreview();
            if (!mInitialized || mConnectionSystem == null)
            {
                return ConnectionVerdict.TopologyWriteFailed;
            }

            if (!releaseHit.HasTarget)
            {
                // 松手不在任何可交互对象上（空白、UI、被禁用对象）：取消。
                return ConnectionVerdict.NodeNotRegistered;
            }

            if (!TryGetNodeId(releaseHit.Target, out string toNodeId))
            {
                // 命中的对象不是网络节点。
                return ConnectionVerdict.NodeNotRegistered;
            }

            ConnectionVerdict verdict = mConnectionSystem.TryConnect(fromNodeId, toNodeId);
            ConnectionFeedback.TryPublishFailure(mMessageSystem, mMessageTargetId, verdict);
            return verdict;
        }

        public void CancelPreview(string fromNodeId)
        {
            if (!string.Equals(mPreviewFromNodeId, fromNodeId, StringComparison.Ordinal))
            {
                return;
            }

            HidePreview();
        }

        // ---- 右键取消连线 ----

        /// <summary>每帧指针帧：只在右键按下时处理“取消连线”。</summary>
        private void OnPointerFrame(PointerFrameEvent frame)
        {
            if (!mInitialized || frame.Signals == null)
            {
                return;
            }

            // 放置模式独占输入（右键用于取消放置）或暂停期间：不处理连线右键删边。
            bool suppressed = (mPlacementGate != null && mPlacementGate.IsBlocked) ||
                              (mPauseState != null && mPauseState.IsPaused.Value);
            if (suppressed)
            {
                return;
            }

            foreach (PointerSignal signal in frame.Signals)
            {
                if (signal.Phase == PointerPhase.Down && signal.Button == PointerButton.Right)
                {
                    HandleRightClick(signal.ScreenPosition);
                    return;
                }
            }
        }

        private void HandleRightClick(Vector2 screenPosition)
        {
            // 拖拽连线进行中：右键直接取消本次连线。
            if (!string.IsNullOrEmpty(mPreviewFromNodeId))
            {
                CancelPreview(mPreviewFromNodeId);
                return;
            }

            // UI 上的右键不删除世界连线，避免与界面操作冲突。
            if (mPointerMapper != null && mPointerMapper.IsOverUI(screenPosition))
            {
                return;
            }

            if (mPointerMapper == null ||
                !mPointerMapper.TryMapScreenToWorld(screenPosition, out Vector3 worldPosition))
            {
                return;
            }

            Collider2D[] hits = Physics2D.OverlapPointAll(worldPosition, ~0);
            if (hits == null || hits.Length == 0)
            {
                return;
            }

            // 节点优先：命中点上只要存在非连线碰撞体（节点等），就不删除连线，避免误删。
            foreach (Collider2D hit in hits)
            {
                if (hit == null || !hit.enabled)
                {
                    continue;
                }

                if (!mColliderViews.ContainsKey(hit))
                {
                    return;
                }
            }

            foreach (Collider2D hit in hits)
            {
                if (hit == null || !hit.enabled)
                {
                    continue;
                }

                if (mColliderViews.TryGetValue(hit, out EdgeLineView view))
                {
                    // 取消（删除）连线：模型移除边并发布事件，视图随后销毁。
                    mTopologySystem.SetConnected(view.FirstNodeId, view.SecondNodeId, false);
                    return;
                }
            }
        }

        // ---- 拓扑事件 ----

        private void OnNodeRegistered(NodeRegisteredEvent e)
        {
            NetworkNodeRegistrar registrar = FindRegistrar(e.Node.NodeId);
            if (registrar != null)
            {
                RegisterNode(registrar);
                ShowFeedbackCircle(registrar.transform.position, mNodeAppearedRadius, mSuccessColor);
            }
        }

        private void OnNodeUnregistered(NodeUnregisteredEvent e)
        {
            // 边视图由模型拆除时发布的 NodeConnectivityChangedEvent(false) 逐个销毁。
            mNodeTransforms.Remove(e.NodeId);
        }

        private void OnConnectivityChanged(NodeConnectivityChangedEvent e)
        {
            NetworkEdgeKey key = NetworkEdgeKey.Create(e.FirstNodeId, e.SecondNodeId);
            if (e.IsConnected)
            {
                EnsureEdgeView(key, e.FirstNodeId, e.SecondNodeId);
            }
            else
            {
                RemoveEdgeView(key);
            }
        }

        private void OnPacketTransmitted(PacketTransmittedEvent e)
        {
            // 成功反馈落在接收节点，直观表示该数据包已经送达。
            ShowFeedbackAtNode(e.DestinationNodeId, mPacketSuccessRadius, mSuccessColor);
        }

        private void OnPacketUnreachable(PacketUnreachableEvent e)
        {
            // 失败时目标可能不存在，因此在仍可定位的发送节点提示失败。
            ShowFeedbackAtNode(e.SourceNodeId, mPacketFailureRadius, mFailureColor);
        }

        private void ShowFeedbackAtNode(string nodeId, float radius, Color color)
        {
            if (!TryGetNodePosition(nodeId, out Vector3 position))
            {
                return;
            }

            ShowFeedbackCircle(position, radius, color);
        }

        private void ShowFeedbackCircle(Vector3 position, float radius, Color color)
        {
            if (mFeedbackSystem == null)
            {
                return;
            }

            CircleFeedbackRequest request = new CircleFeedbackRequest(
                position,
                Mathf.Max(0f, radius),
                color,
                Mathf.Max(0f, mFeedbackDuration));
            mFeedbackSystem.ShowCircle(request);
        }

        // ---- 节点位置表与拖拽器注入 ----

        private void RegisterSceneNodes()
        {
            NetworkNodeRegistrar[] registrars = FindObjectsOfType<NetworkNodeRegistrar>();
            foreach (NetworkNodeRegistrar registrar in registrars)
            {
                if (string.IsNullOrWhiteSpace(registrar.NodeId))
                {
                    // 身份注入尚未完成；其 Start 会随后发布 NodeRegisteredEvent。
                    continue;
                }

                RegisterNode(registrar);
            }
        }

        private void RegisterNode(NetworkNodeRegistrar registrar)
        {
            mNodeTransforms[registrar.NodeId] = registrar.transform;
            EnsureDragger(registrar.gameObject);
        }

        private static NetworkNodeRegistrar FindRegistrar(string nodeId)
        {
            NetworkNodeRegistrar[] registrars = FindObjectsOfType<NetworkNodeRegistrar>();
            foreach (NetworkNodeRegistrar registrar in registrars)
            {
                if (string.Equals(registrar.NodeId, nodeId, StringComparison.Ordinal))
                {
                    return registrar;
                }
            }

            return null;
        }

        /// <summary>
        /// 为节点注入拖拽连线能力并刷新能力缓存。
        /// 同一 GameObject 上已有 CapabilitySinkAdapter 且其 DragIntent Sink 登记自 Awake，
        /// 因此只需重建适配器内部的能力引用即可生效。
        /// </summary>
        private static void EnsureDragger(GameObject nodeRoot)
        {
            if (nodeRoot.GetComponent<NodeConnectionDragger>() != null)
            {
                return;
            }

            nodeRoot.AddComponent<NodeConnectionDragger>();
            CapabilitySinkAdapter adapter = nodeRoot.GetComponent<CapabilitySinkAdapter>();
            if (adapter != null)
            {
                adapter.RebuildCapabilities();
            }
        }

        private static bool TryGetNodeId(IInteractionTarget target, out string nodeId)
        {
            nodeId = null;
            if (target is InteractionTarget interactionTarget && interactionTarget != null)
            {
                NetworkNodeRegistrar registrar = interactionTarget.GetComponent<NetworkNodeRegistrar>();
                if (registrar != null && !string.IsNullOrWhiteSpace(registrar.NodeId))
                {
                    nodeId = registrar.NodeId;
                    return true;
                }
            }

            return false;
        }

        // ---- 边视图 ----

        private void EnsureEdgeView(NetworkEdgeKey key, string firstNodeId, string secondNodeId)
        {
            if (mEdgeViews.TryGetValue(key, out EdgeLineView existing))
            {
                RefreshEdgeView(existing);
                return;
            }

            GameObject lineObject = new GameObject($"Edge {key}");
            lineObject.transform.SetParent(transform, false);
            // 命中碰撞体放在 Ignore Raycast 层：不参与节点解析（mask 55 不含该层），
            // 也不挡节点点击/拖拽；右键取消时用 OverlapPointAll(~0) 单独检测。
            lineObject.layer = 3;
            SpriteRenderer line = lineObject.AddComponent<SpriteRenderer>();
            ApplySpriteConfig(
                line,
                mEdgeColor,
                ResolveMaterial(ref mResolvedEdgeMaterial, mEdgeMaterial),
                mEdgeSortingOrder);
            BoxCollider2D hitCollider = lineObject.AddComponent<BoxCollider2D>();
            // 世界尺寸 = localSize × localScale；RefreshEdgeView 按命中厚度/视觉粗细换算。
            hitCollider.size = new Vector2(1.0f, 1.0f);

            EdgeLineView view = new EdgeLineView(key, firstNodeId, secondNodeId, line, hitCollider);
            mEdgeViews.Add(key, view);
            mColliderViews.Add(hitCollider, view);
            RefreshEdgeView(view);
        }

        private void RemoveEdgeView(NetworkEdgeKey key)
        {
            if (!mEdgeViews.TryGetValue(key, out EdgeLineView view))
            {
                return;
            }

            mEdgeViews.Remove(key);
            if (view.Collider != null)
            {
                mColliderViews.Remove(view.Collider);
            }

            if (view.Renderer != null)
            {
                Destroy(view.Renderer.gameObject);
            }
        }

        // ---- 预览线 ----

        private void CreatePreviewLine()
        {
            GameObject previewObject = new GameObject("ConnectionPreview");
            previewObject.transform.SetParent(transform, false);
            mPreviewRenderer = previewObject.AddComponent<SpriteRenderer>();
            ApplySpriteConfig(
                mPreviewRenderer,
                mPreviewColor,
                ResolveMaterial(ref mResolvedPreviewMaterial, mPreviewMaterial),
                mPreviewSortingOrder);
            mPreviewRenderer.enabled = false;
        }

        private void HidePreview()
        {
            mPreviewFromNodeId = null;
            if (mPreviewRenderer != null)
            {
                mPreviewRenderer.enabled = false;
            }
        }

        /// <summary>按 锚点→自由端 更新预览线 Sprite 的位置/旋转/缩放（粗细=世界单位）。</summary>
        private void UpdatePreviewGeometry()
        {
            if (mPreviewRenderer == null)
            {
                return;
            }

            Vector3 delta = mPreviewFreeEnd - mPreviewAnchor;
            float length = delta.magnitude;
            if (length <= 0.001f)
            {
                mPreviewRenderer.enabled = false;
                return;
            }

            Transform previewTransform = mPreviewRenderer.transform;
            previewTransform.position = (mPreviewAnchor + mPreviewFreeEnd) * 0.5f;
            previewTransform.rotation = Quaternion.Euler(
                0.0f,
                0.0f,
                Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            previewTransform.localScale = new Vector3(length, Mathf.Max(0.01f, mPreviewWidth), 1.0f);
            mPreviewRenderer.enabled = true;
        }

        // ---- 每帧刷新 ----

        private void LateUpdate()
        {
            if (!mInitialized)
            {
                return;
            }

            // 节点当前不移动，但此处保持每帧同步，为后续节点拖拽移动预留正确性。
            foreach (EdgeLineView view in mEdgeViews.Values)
            {
                RefreshEdgeView(view);
            }
        }

        private void RefreshEdgeView(EdgeLineView view)
        {
            if (view.Renderer == null)
            {
                return;
            }

            if (TryGetNodePosition(view.FirstNodeId, out Vector3 first) &&
                TryGetNodePosition(view.SecondNodeId, out Vector3 second))
            {
                Vector3 delta = second - first;
                float length = delta.magnitude;
                if (length <= 0.001f)
                {
                    // 两端重合：无向量的线段无法定位，保持隐藏。
                    view.Renderer.enabled = false;
                    return;
                }

                // 拉伸 Sprite：scale=(长度, 粗细, 1) → 粗细为世界单位，随相机缩放（场景对象）。
                Transform lineTransform = view.Renderer.transform;
                lineTransform.position = (first + second) * 0.5f;
                lineTransform.rotation = Quaternion.Euler(
                    0.0f,
                    0.0f,
                    Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
                float visualWidth = Mathf.Max(0.01f, mEdgeWidth);
                lineTransform.localScale = new Vector3(length, visualWidth, 1.0f);

                if (view.Collider != null)
                {
                    // localSize × localScale = 世界尺寸；命中厚度独立可配。
                    view.Collider.size = new Vector2(
                        1.0f,
                        Mathf.Max(0.01f, mLineHitThickness) / visualWidth);
                }

                view.Renderer.enabled = true;
            }
            else
            {
                // 端点尚未登记时保持隐藏，避免在原点闪现。
                view.Renderer.enabled = false;
            }
        }

        // ---- 工具 ----

        private static void ApplySpriteConfig(
            SpriteRenderer renderer,
            Color color,
            Material material,
            int sortingOrder)
        {
            renderer.sprite = GetWhiteSprite();
            renderer.color = color;
            // 使用 sharedMaterial：所有线共享同一份可配置材质，改动一处即全局生效。
            renderer.sharedMaterial = material;
            renderer.sortingOrder = sortingOrder;
            renderer.drawMode = SpriteDrawMode.Simple;
        }

        /// <summary>
        /// 共享白色 1×1 Sprite（pixelsPerUnit=1 → 原生尺寸 1 世界单位）。
        /// 拉伸为线段时 scale=(长度, 粗细, 1)，粗细直接是世界单位。
        /// </summary>
        private static Sprite GetWhiteSprite()
        {
            if (sWhiteSprite != null)
            {
                return sWhiteSprite;
            }

            Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            sWhiteSprite = Sprite.Create(
                texture,
                new Rect(0.0f, 0.0f, 1.0f, 1.0f),
                new Vector2(0.5f, 0.5f),
                1.0f);
            return sWhiteSprite;
        }

        private static Material ResolveMaterial(ref Material cached, Material assigned)
        {
            if (cached != null)
            {
                return cached;
            }

            if (assigned != null)
            {
                cached = assigned;
                return cached;
            }

            Shader shader = Shader.Find("Sprites/Default");
            if (shader != null)
            {
                cached = new Material(shader);
            }

            return cached;
        }

        private static Vector3 ProjectToNodePlane(Ray ray, Vector3 fallback)
        {
            if (sNodePlane.Raycast(ray, out float distance))
            {
                return ray.GetPoint(distance);
            }

            return fallback;
        }

        IArchitecture IBelongToArchitecture.GetArchitecture()
        {
            return GameArchitecture.Interface;
        }

        /// <summary>一条已建立连线的渲染视图（拉伸 Sprite + 命中碰撞体）。</summary>
        private sealed class EdgeLineView
        {
            public readonly NetworkEdgeKey Key;
            public readonly string FirstNodeId;
            public readonly string SecondNodeId;
            public readonly SpriteRenderer Renderer;
            public readonly BoxCollider2D Collider;

            public EdgeLineView(
                NetworkEdgeKey key,
                string firstNodeId,
                string secondNodeId,
                SpriteRenderer renderer,
                BoxCollider2D collider)
            {
                Key = key;
                FirstNodeId = firstNodeId;
                SecondNodeId = secondNodeId;
                Renderer = renderer;
                Collider = collider;
            }
        }
    }
}
