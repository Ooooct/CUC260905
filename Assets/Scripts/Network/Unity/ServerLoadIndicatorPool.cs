using System;
using System.Collections.Generic;
using CUC260905.Interaction;
using QFramework;
using UnityEngine;
using UnityEngine.Pool;

namespace CUC260905.Network
{
    /// <summary>
    /// WorldCanvas 上服务器负载条的集中管理器。
    /// 预热并复用 UGUI 条目，单帧只遍历活动条目同步世界位置；负载重绘由 BindableProperty 事件驱动。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ServerLoadIndicatorPool : MonoBehaviour, IController
    {
        [Header("对象池")]
        [SerializeField, Tooltip("启动时创建的条目数，覆盖同屏 200 台服务器。")]
        private int mPrewarmCount = 200;
        [SerializeField, Tooltip("空闲条目的最大保留数量。")]
        private int mMaxRetainedCount = 256;

        [Header("显示")]
        [SerializeField, Tooltip("负载条相对 Circle 视觉下缘的世界坐标偏移。")]
        private Vector3 mWorldOffset = new Vector3(0.0f, -0.6f, 0.0f);
        [SerializeField, Tooltip("单个负载条的世界空间尺寸。")]
        private Vector2 mIndicatorSize = new Vector2(0.65f, 0.065f);
        [SerializeField, Tooltip("0% 负载的填充颜色。")]
        private Color mLowLoadColor = new Color(0.15f, 0.9f, 0.28f, 1.0f);
        [SerializeField, Tooltip("100% 负载的填充颜色。")]
        private Color mHighLoadColor = new Color(0.95f, 0.12f, 0.12f, 1.0f);

        private readonly Dictionary<string, ServerLoadIndicatorView> mActiveIndicators =
            new Dictionary<string, ServerLoadIndicatorView>(StringComparer.Ordinal);
        private readonly Dictionary<string, NetworkNodeRegistrar> mRegistrars =
            new Dictionary<string, NetworkNodeRegistrar>(StringComparer.Ordinal);

        private IObjectPool<ServerLoadIndicatorView> mPool;
        private INetworkTopologyModel mTopologyModel;
        private IUnRegister mNodeRegisteredRegistration;
        private IUnRegister mNodeUnregisteredRegistration;
        private bool mNeedsRegistrarScan = true;

        private void Awake()
        {
            int prewarmCount = Mathf.Max(200, mPrewarmCount);
            int maxRetainedCount = Mathf.Max(prewarmCount, mMaxRetainedCount);
            mPool = new ObjectPool<ServerLoadIndicatorView>(
                CreateIndicator,
                OnGetIndicator,
                OnReleaseIndicator,
                OnDestroyIndicator,
                false,
                prewarmCount,
                maxRetainedCount);

            List<ServerLoadIndicatorView> prewarmed = new List<ServerLoadIndicatorView>(prewarmCount);
            for (int index = 0; index < prewarmCount; index++)
            {
                prewarmed.Add(mPool.Get());
            }

            for (int index = 0; index < prewarmed.Count; index++)
            {
                mPool.Release(prewarmed[index]);
            }
        }

        private void Start()
        {
            mTopologyModel = this.GetModel<INetworkTopologyModel>();
            if (mTopologyModel == null)
            {
                Debug.LogError("服务器负载条初始化失败：未找到 INetworkTopologyModel。", this);
                enabled = false;
                return;
            }

            mNodeRegisteredRegistration = this.RegisterEvent<NodeRegisteredEvent>(OnNodeRegistered);
            mNodeUnregisteredRegistration = this.RegisterEvent<NodeUnregisteredEvent>(OnNodeUnregistered);
            mNeedsRegistrarScan = true;
        }

        private void LateUpdate()
        {
            if (mNeedsRegistrarScan)
            {
                ScanRegistrarsAndSyncIndicators();
            }

            foreach (KeyValuePair<string, ServerLoadIndicatorView> pair in mActiveIndicators)
            {
                pair.Value.UpdateWorldPosition(mWorldOffset);
            }
        }

        private void OnDestroy()
        {
            mNodeRegisteredRegistration?.UnRegister();
            mNodeUnregisteredRegistration?.UnRegister();
            mNodeRegisteredRegistration = null;
            mNodeUnregisteredRegistration = null;

            foreach (KeyValuePair<string, ServerLoadIndicatorView> pair in mActiveIndicators)
            {
                pair.Value.Unbind();
            }

            mActiveIndicators.Clear();
            mRegistrars.Clear();
            mPool?.Clear();
            mPool = null;
        }

        private void OnNodeRegistered(NodeRegisteredEvent e)
        {
            if (e.Node.Role == NetworkNodeRole.Server)
            {
                mNeedsRegistrarScan = true;
            }
        }

        private void OnNodeUnregistered(NodeUnregisteredEvent e)
        {
            if (!mActiveIndicators.TryGetValue(e.NodeId, out ServerLoadIndicatorView indicator))
            {
                return;
            }

            mActiveIndicators.Remove(e.NodeId);
            mRegistrars.Remove(e.NodeId);
            mPool.Release(indicator);
        }

        private void ScanRegistrarsAndSyncIndicators()
        {
            mNeedsRegistrarScan = false;
            NetworkNodeRegistrar[] registrars = FindObjectsOfType<NetworkNodeRegistrar>();
            for (int index = 0; index < registrars.Length; index++)
            {
                NetworkNodeRegistrar registrar = registrars[index];
                string nodeId = registrar.NodeId;
                if (string.IsNullOrWhiteSpace(nodeId) ||
                    !mTopologyModel.TryGetNode(nodeId, out NodeDescriptor node) ||
                    node.Role != NetworkNodeRole.Server ||
                    !mTopologyModel.TryGetServerCapabilities(nodeId, out ServerNodeCapabilities capabilities))
                {
                    continue;
                }

                mRegistrars[nodeId] = registrar;
                if (mActiveIndicators.ContainsKey(nodeId))
                {
                    continue;
                }

                ServerLoadIndicatorView indicator = mPool.Get();
                indicator.Bind(ResolveVisualTarget(registrar), capabilities, mLowLoadColor, mHighLoadColor);
                indicator.UpdateWorldPosition(mWorldOffset);
                mActiveIndicators.Add(nodeId, indicator);
            }
        }

        private ServerLoadIndicatorView CreateIndicator()
        {
            GameObject indicatorObject = new GameObject(
                "Server Load Indicator",
                typeof(RectTransform),
                typeof(ServerLoadIndicatorView));
            indicatorObject.transform.SetParent(transform, false);
            ServerLoadIndicatorView indicator = indicatorObject.GetComponent<ServerLoadIndicatorView>();
            indicator.Initialize(mIndicatorSize);
            return indicator;
        }

        private static void OnGetIndicator(ServerLoadIndicatorView indicator)
        {
            indicator.gameObject.SetActive(true);
        }

        private static void OnReleaseIndicator(ServerLoadIndicatorView indicator)
        {
            indicator.Unbind();
            indicator.gameObject.SetActive(false);
        }

        private static void OnDestroyIndicator(ServerLoadIndicatorView indicator)
        {
            if (indicator != null)
            {
                Destroy(indicator.gameObject);
            }
        }

        private static Transform ResolveVisualTarget(NetworkNodeRegistrar registrar)
        {
            Transform circle = registrar.transform.Find("Circle");
            return circle != null ? circle : registrar.transform;
        }

        IArchitecture IBelongToArchitecture.GetArchitecture()
        {
            return GameArchitecture.Interface;
        }
    }
}
