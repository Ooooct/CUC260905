using System;
using System.Collections.Generic;
using CUC260905.Interaction;
using CUC260905.Placement;
using QFramework;
using UnityEngine;

namespace CUC260905.Network
{
    /// <summary>
    /// 用户节点自动生成控制器（表现层）。
    /// 启用自动生成时，先等待首个服务器登记并立即生成第一个用户节点；之后每次生成节拍到来时，由 UserNodeScatterGenerator
    /// 在当前"已外扩半径"内逐一采样一个候选点（Best-Candidate，保持平均且随机），
    /// 采样范围随已生成数量从内半径逐步外扩到 RangeRadius；
    /// 过近（MinDistance）分析同时避开已生成用户点与场景内服务器节点
    /// （服务器位置经 INetworkTopologyModel + INodePositionProvider 每次生成时读取）。
    /// 实例化统一走架构注册的 IPlacementInstantiator，便于测试替身替换。
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UserNodeSpawnerController : MonoBehaviour, IController
    {
        [Header("节点模板")]
        [SerializeField, Tooltip("被实例化的用户节点 prefab（应携带 NetworkNodeRegistrar，Role = User）。")]
        private GameObject mUserNodePrefab;
        [SerializeField, Tooltip("实例挂载的父节点；为空时挂在场景根。")]
        private Transform mParent;

        [Header("候选点配置")]
        [SerializeField, Min(0.01f), Tooltip("候选点距原点允许的最大距离（圆盘外半径）。")]
        private float mRangeRadius = 10.0f;
        [SerializeField, Min(0.01f), Tooltip("任意两个候选点之间的最小允许距离；同时约束候选点与服务器节点。")]
        private float mMinDistance = 0.5f;
        [SerializeField, Min(0.0f), Tooltip("候选点距原点允许的最小距离（中心留空，避开原点处的中心对象）。")]
        private float mInnerRadius = 0.5f;
        [SerializeField, Min(0), Tooltip("计划生成数量随机下限（含）。")]
        private int mMinCount = 40;
        [SerializeField, Min(0), Tooltip("计划生成数量随机上限（含）。")]
        private int mMaxCount = 80;
        [SerializeField, Tooltip("随机种子；-1 表示每次进入按实例随机。")]
        private int mSeed = -1;

        [Header("生成节奏")]
        [SerializeField, Min(0.01f), Tooltip("两次生成之间的随机间隔下限（秒）。")]
        private float mSpawnIntervalMin = 0.4f;
        [SerializeField, Min(0.01f), Tooltip("两次生成之间的随机间隔上限（秒）。")]
        private float mSpawnIntervalMax = 0.8f;
        [SerializeField, Tooltip("实例落点所在固定世界 z 平面。")]
        private float mSpawnZ = 0.0f;
        [SerializeField, Tooltip("进入运行时是否自动生成；首个用户节点会在首个服务器登记后立即生成。")]
        private bool mAutoStart = true;

        private UserNodeScatterGenerator mGenerator;
        private UserNodeSpawnScheduler mScheduler;
        private IPlacementInstantiator mInstantiator;
        private IUnRegister mNodeRegisteredRegistration;
        private bool mSpawnStarted;

        /// <summary>已生成（消耗）的用户节点位置，按生成顺序排列，供状态显示或测试读取。</summary>
        public IReadOnlyList<Vector2> GeneratedPoints
        {
            get { return mGenerator != null ? mGenerator.GeneratedPoints : null; }
        }

        /// <summary>计划生成的总数（Reset 时在 [MinCount, MaxCount] 内随机采样）。</summary>
        public int TargetCount
        {
            get { return mGenerator != null ? mGenerator.TargetCount : 0; }
        }

        /// <summary>已生成（消耗）的用户节点数量。</summary>
        public int SpawnedCount
        {
            get { return mGenerator != null ? mGenerator.GeneratedCount : 0; }
        }

        /// <summary>是否已无待生成的用户节点（计划达成或因域饱和停止）。</summary>
        public bool IsExhausted
        {
            get { return mGenerator == null || !mGenerator.CanGenerateMore; }
        }

        // Start 晚于 InputController.Awake，确保 GameArchitecture 已装配（IPlacementInstantiator 可用）。
        private void Start()
        {
            mInstantiator = this.GetUtility<IPlacementInstantiator>();
            if (mInstantiator == null)
            {
                Debug.LogError(
                    "UserNodeSpawnerController 未找到 IPlacementInstantiator，请确认场景存在 InputController。",
                    this);
                enabled = false;
                return;
            }

            Regenerate();
            if (!mAutoStart || mScheduler == null)
            {
                return;
            }

            mNodeRegisteredRegistration = this.RegisterEvent<NodeRegisteredEvent>(OnNodeRegistered);
            if (HasRegisteredServer())
            {
                StartAfterFirstServerRegistered();
            }
        }

        private void OnDestroy()
        {
            mNodeRegisteredRegistration?.UnRegister();
        }

        /// <summary>重建生成器与节奏器；种子固定时结果确定。</summary>
        public void Regenerate()
        {
            System.Random random = new System.Random(mSeed >= 0 ? mSeed : Environment.TickCount ^ GetInstanceID());
            UserNodeScatterConfig config = new UserNodeScatterConfig(
                mRangeRadius, mMinDistance, mInnerRadius, mMinCount, mMaxCount);
            mGenerator = new UserNodeScatterGenerator(config, random);
            mGenerator.Reset();
            mScheduler = new UserNodeSpawnScheduler(mSpawnIntervalMin, mSpawnIntervalMax, random);
            mSpawnStarted = false;
        }

        private void Update()
        {
            if (!mSpawnStarted || mGenerator == null || mScheduler == null ||
                mInstantiator == null || mUserNodePrefab == null)
            {
                return;
            }

            double now = Time.timeAsDouble;
            // while 补足帧率波动 / 卡顿落下的间隔，保持"每段间隔生成一个"的契约。
            while (mGenerator.CanGenerateMore &&
                   mScheduler.TryConsume(now, mGenerator.TargetCount, out _))
            {
                if (!SpawnNextUserNode())
                {
                    break;
                }
            }
        }

        private void OnNodeRegistered(NodeRegisteredEvent registeredEvent)
        {
            if (registeredEvent.Node.Role == NetworkNodeRole.Server)
            {
                StartAfterFirstServerRegistered();
            }
        }

        /// <summary>首个服务器登记后立即生成一个用户节点，后续节点再进入随机节奏。</summary>
        private void StartAfterFirstServerRegistered()
        {
            if (mSpawnStarted || mGenerator == null || mScheduler == null ||
                mInstantiator == null || mUserNodePrefab == null)
            {
                return;
            }

            mSpawnStarted = true;
            if (mScheduler.TryConsumeImmediately(
                    Time.timeAsDouble,
                    mGenerator.TargetCount,
                    out _))
            {
                SpawnNextUserNode();
            }
        }

        private bool SpawnNextUserNode()
        {
            if (!mGenerator.TryGenerateNextPoint(CollectServerPositions(), out Vector2 position))
            {
                return false;
            }

            Vector3 world = new Vector3(position.x, position.y, mSpawnZ);
            GameObject instance = mInstantiator.Instantiate(mUserNodePrefab, world, Quaternion.identity);
            if (instance != null && mParent != null)
            {
                instance.transform.SetParent(mParent, true);
            }

            // 生成节拍以候选点产出为准，与既有行为一致：实例化失败不回滚已消耗的位置，
            // 也不阻断后续节拍。
            return true;
        }

        private bool HasRegisteredServer()
        {
            INetworkTopologyModel topologyModel = this.GetModel<INetworkTopologyModel>();
            if (topologyModel == null)
            {
                return false;
            }

            foreach (NodeDescriptor node in topologyModel.Nodes)
            {
                if (node.Role == NetworkNodeRole.Server)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 收集当前所有服务器节点的世界 x/y 位置，作为生成点的"过近障碍"。
        /// 每次生成时惰性读取，保证玩家后放置/移除的服务器同样生效；
        /// 拓扑或位置源未就绪时返回空集（此时仅保留用户点之间的最小距离约束）。
        /// </summary>
        private List<Vector2> CollectServerPositions()
        {
            List<Vector2> positions = new List<Vector2>();
            INetworkTopologyModel topologyModel = this.GetModel<INetworkTopologyModel>();
            INodePositionProvider positionProvider = this.GetUtility<INodePositionProvider>();
            if (topologyModel == null || positionProvider == null)
            {
                return positions;
            }

            foreach (NodeDescriptor node in topologyModel.Nodes)
            {
                if (node.Role != NetworkNodeRole.Server)
                {
                    continue;
                }

                if (positionProvider.TryGetNodePosition(node.NodeId, out Vector3 world))
                {
                    positions.Add(new Vector2(world.x, world.y));
                }
            }

            return positions;
        }

        IArchitecture IBelongToArchitecture.GetArchitecture()
        {
            return GameArchitecture.Interface;
        }
    }
}
