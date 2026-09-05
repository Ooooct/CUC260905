using CUC260905.Interaction;
using QFramework;
using UnityEngine;

namespace CUC260905.Network
{
    /// <summary>
    /// 将场景节点登记到逻辑拓扑；不决定节点创建或连通规则。
    /// 运行时身份自动注入：NodeId 留空时由 INodeIdentitySource 生成唯一 ID，
    /// DisplayName 留空时由 INodeDisplayNameSource 按角色分配唯一友好名（无来源时取 GameObject 名称）。
    /// 服务器初始能力优先从 ServerUpgradeConfig level 0 注入（与升级系统共用数值源），
    /// 未配置 config 时回退到模板手填字段。
    /// 放置系统放置的 prefab 实例同样在 Start 走此路径，无需为每个实例手工配置 NodeId。
    /// Role 是节点类型模板：由节点 prefab 携带，放置系统按选中的 prefab 决定放置哪种节点。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class NetworkNodeRegistrar : MonoBehaviour, IController
    {
        [SerializeField, Tooltip("节点 ID；留空时由系统自动生成唯一 ID。")]
        private string mNodeId;
        [SerializeField, Tooltip("节点角色：决定使用哪个节点类型模板。")]
        private NetworkNodeRole mRole;
        [SerializeField, Tooltip("显示名；留空时取 GameObject 名称。")]
        private string mDisplayName;
        [SerializeField, Tooltip("服务器升级配置；配置后初始能力取 level 0 档，下方手填数值忽略。")]
        private ServerUpgradeConfig mUpgradeConfig;
        [SerializeField, Tooltip("服务器节点初始数据处理上限（条/秒）；未配置 ServerUpgradeConfig 时使用。")]
        private float mDataProcessingPerSecond;
        [SerializeField, Tooltip("服务器节点初始最大连接边数；未配置 ServerUpgradeConfig 时使用。")]
        private int mMaxConnections;
        [SerializeField, Tooltip("服务器节点初始数据吞吐量轨道等级；未配置 ServerUpgradeConfig 时使用。")]
        private int mDataThroughputLevel;
        [SerializeField, Tooltip("服务器节点初始最大连接数轨道等级；未配置 ServerUpgradeConfig 时使用。")]
        private int mMaxConnectionsLevel;

        private INetworkTopologySystem mSystem;
        private bool mRegistered;

        /// <summary>节点 ID，与拓扑模型中的注册键一致；供同物体上的控制器读取。</summary>
        public string NodeId
        {
            get { return mNodeId; }
        }

        // Start 晚于 InputController.Awake，确保 GameArchitecture 已装配。
        private void Start()
        {
            mSystem = this.GetSystem<INetworkTopologySystem>();
            ResolveRuntimeIdentity();
            NodeDescriptor node = new NodeDescriptor(mNodeId, mRole, mDisplayName);
            NetworkTopologyResult result = mRole == NetworkNodeRole.Server
                ? mSystem.Register(node, BuildInitialCapabilities())
                : mSystem.Register(node);
            mRegistered = result == NetworkTopologyResult.Success;

            if (!mRegistered)
            {
                Debug.LogWarning($"节点 {name} 注册失败：{result}。", this);
            }
        }

        /// <summary>
        /// 运行时身份注入：NodeId 留空时由身份 Utility 生成唯一 ID；
        /// DisplayName 留空时分配按角色编号的友好名（无显示名来源时回退对象名）。
        /// 放置系统放置的实例同样自动走此路径。
        /// </summary>
        private void ResolveRuntimeIdentity()
        {
            if (string.IsNullOrWhiteSpace(mNodeId))
            {
                INodeIdentitySource identitySource = this.GetUtility<INodeIdentitySource>();
                mNodeId = identitySource != null
                    ? identitySource.NextNodeId(mRole)
                    : GuidNodeIdentitySource.Create(mRole);
            }

            if (string.IsNullOrWhiteSpace(mDisplayName))
            {
                INodeDisplayNameSource displayNameSource = this.GetUtility<INodeDisplayNameSource>();
                mDisplayName = displayNameSource != null
                    ? displayNameSource.NextDisplayName(mRole)
                    : StripCloneSuffix(gameObject.name);
            }
        }

        /// <summary>
        /// 服务器初始能力注入：优先取 ServerUpgradeConfig 的 level 0 档
        /// （与升级系统共用同一数值源，放置出的节点天然从基础档起步）；
        /// 未配置 config 时回退到模板手填字段。
        /// </summary>
        private ServerNodeCapabilities BuildInitialCapabilities()
        {
            if (mUpgradeConfig != null)
            {
                return mUpgradeConfig.BuildCapabilities(0);
            }

            return new ServerNodeCapabilities(
                mDataProcessingPerSecond,
                mMaxConnections,
                mDataThroughputLevel,
                mMaxConnectionsLevel);
        }

        private static string StripCloneSuffix(string name)
        {
            const string CloneSuffix = "(Clone)";
            return name != null && name.EndsWith(CloneSuffix, System.StringComparison.Ordinal)
                ? name.Substring(0, name.Length - CloneSuffix.Length)
                : name;
        }

        private void OnDestroy()
        {
            if (!mRegistered || mSystem == null)
            {
                return;
            }

            mSystem.Unregister(mNodeId);
            mRegistered = false;
        }

        IArchitecture IBelongToArchitecture.GetArchitecture()
        {
            return GameArchitecture.Interface;
        }
    }
}
