# CUC260905 代理开发约定

## 语言

- 默认使用中文回答用户、编写交付文档和源码注释。
- 仅在用户明确指定、外部 API/协议要求，或既有代码命名规范要求时使用其他语言；代码标识符、命令和专有名词保持原样。

## 范围与权威来源

- 以用户最新需求确定任务边界。
- 本文件是仓库内代理的执行约定。存在且与任务相关时，遵循 `docs/adr/`、`Wiki/`、`HANDOFF.md`、`CONTEXT.md` 和模块文档；文档过期时，以源码和序列化资产为实现事实，并报告差异。
- 当前工程使用 Unity `2022.3.62f3`。从 `ProjectSettings/` 与 `Packages/` 确认配置和依赖，不能把其他 Unity 项目的功能当作本工程事实。
- 保留既有工作区改动。未经用户明确要求，不执行 reset、restore、clean、commit、push、批量删除等操作。

## 每项任务的开始流程

1. 阅读本文件及适用的本地指引（`CLAUDE.md`、`HANDOFF.md`、`CONTEXT.md`、ADR、Wiki）后，再广泛扫描源码。
2. 执行 `git status --short` 和 `git log -1 --oneline`；将已有改动视为用户所有。
3. 在动手前明确所属模块、公开契约、调用链、写入范围和适当的验证方式。
4. 采用最小且完整的改动，不让无关文件或 Unity 生成目录进入补丁。

## 架构与代码

- 工程包含 QFramework。职责保持清晰：Controller 连接 View 与逻辑；Command 执行写操作；Model 保存共享状态；System 提供业务服务；Query 负责读取；事件用于传递状态变化。
- 通过既有 QFramework 访问方式获取已注册依赖（`GetModel<T>()`、`GetSystem<T>()`、`GetUtility<T>()`）。保持既有模块边界，避免新增轮询或隐式跨模块耦合。
- 事件订阅必须匹配生命周期：保留每个注册句柄，并在所有者合适的销毁或清理时机注销。
- 局部变量使用明确的 C# 类型，不引入 `var`。
- 注释简洁说明当前职责或行为，不记录冗长历史叙述。

## Unity 数据、资源与场景

- ScriptableObject 是共享配置模板。按所有者变化的运行时状态存放在运行时组件、Controller、Model 或 System 中。
- 改动场景、Prefab 或资产前，先在 Unity Editor 中核对现状。序列化 Unity 内容优先通过编辑器/MCP 操作；未经核实，不能手写覆盖 Inspector 配置。
- 资产与 `.meta` 文件成对维护并保留 GUID。不得手动修改 `Library/`、`Temp/`、`Logs/` 等生成目录。
- 沿用项目已有的输入、时间、UI 和渲染模式。新增管线专属代码或资产前，先确认当前渲染管线和包支持情况。

## 设计与文档门禁

- 修改共享 API、生命周期/状态归属、跨模块架构或序列化数值/配置前，先给出影响文件、调用链、风险、精确值（如有）和验证计划，等待评审方向后再写入。
- 公开的 System、Model、Utility、Command、Behavior 或同等 API 发生变化时，更新对应项目文档（如存在）。新增或调整模块边界时，同步 `CONTEXT.md`/领域文档（如存在）；已接受的架构取舍写入 ADR，不把临时决定表述为既定规范。
- 新增可玩对象或行为资产时，先询问是否执行项目的冒烟测试流程。

## 验证与汇报

- 文档：重读成品并执行 `git diff --check`。
- C#：等待 Unity 编译完成，确认 Console 没有新增错误；存在时运行最窄的相关自动化检查。
- 场景、Prefab、资产、输入或玩法：静态检查之外，执行对应的 Play Mode 流程；编译通过不等于玩法验收。
- 最终汇报包含：改动路径、理由、实际完成的验证、跳过项及原因、剩余风险或阻塞项。

## 协作

- 主代理负责实现、集成和最终验证。仅当至少两项实质、独立任务确有并行收益时才委派；探索任务保持范围小且以证据为基础。
- 代理设置、hooks、workflow 和 memory 默认是本地配置；只有用户明确授权时才作为团队共享内容改动。

## 工程总览

当前工程是一个使用 Unity `2022.3.62f3` 和本地 QFramework 的游戏原型，世界坐标以 2D 为主。玩法围绕网络节点展开：玩家在场景中注册用户节点和服务器节点，建立不交叉且受容量限制的网络边，再沿拓扑发送数据包。服务器能力、处理负载、金币、系统消息和视觉反馈共同呈现游戏状态。

领域词汇和概念边界见根目录 `CONTEXT.md`。本节只记录实现结构、入口和调用链。源码、序列化场景与文档不一致时，以源码和场景实际内容为准，并在汇报中指出差异。

### 目录地图

| 路径 | 职责 |
|---|---|
| `Assets/Scripts/Framework/` | 当前工程内的 QFramework 基础实现和架构容器。 |
| `Assets/Scripts/Game/` | 跨域暂停：`GamePauseState`（共享暂停状态）、`GamePauseController`（空格切换、`Time.timeScale` 冻结/恢复）、`GamePauseBootstrap`（运行时自动挂载）。暂停时模拟时间冻结、相机浏览保留、世界交互被输入/放置/连线各层门控。 |
| `Assets/Scripts/Interaction/` | 采集原始指针、解析物理目标、维护指针会话、解释 Click/Drag/Hover 意图并路由到对象能力。 |
| `Assets/Scripts/Browsing/` | 相机平移、缩放、焦点移动、边界约束和惯性；Unity Controller 负责把逻辑状态应用到 Camera。 |
| `Assets/Scripts/Placement/` | 选择 Prefab、进入单次放置、生成预览、屏幕坐标映射到固定 z 平面和放置期间的输入门控。 |
| `Assets/Scripts/Network/` | 节点注册、无向拓扑、连线裁决、服务器升级、数据包寻路和近一秒处理负载；也包含网络场景控制器与 EditMode 测试。 |
| `Assets/Scripts/Economy/` | 单一金币余额及其增加/消耗写入口；数据包成功传输时发放金币奖励。 |
| `Assets/Scripts/Message/` | 面向消息终端的系统消息、有限历史和发布事件。 |
| `Assets/Scripts/Feedback/` | 圆形背景反馈请求及其表现层转发。 |
| `Assets/Scripts/Visual/` | 与领域规则相对独立的场景视觉组件，目前包含虚线网格背景。 |
| `Assets/Tests/Editor/` | 跨领域或基础 Model/System 的 EditMode 测试。网络专属测试位于 `Assets/Scripts/Network/Tests/Editor/`，放置测试位于 `Assets/Scripts/Placement/Tests/Editor/`。 |
| `Assets/Scenes/SampleScene.unity` | 当前主要集成场景，包含输入、相机浏览、网络节点、升级、消息、全局负载、放置和反馈等接线。 |
| `Assets/Resources/Prefabs/` | 当前节点 Prefab（`UserNode.prefab`、`ServerNode.prefab`）。 |
| `Assets/Configs/` | 运行时配置资产，目前包含服务器升级配置。 |
| `docs/` | 架构可视化和本地设计资料；仓库当前 `.gitignore` 忽略整个目录，使用前先确认文件是否只存在于本地。 |

领域目录通常按以下职责分层。`Core/` 放领域数据、结果、事件和规则；`Runtime/` 放 QFramework 的 Model/System/Command 及运行时协调；`Unity/` 放 MonoBehaviour、物理、相机、Prefab、UGUI 适配和表现层；`Editor/` 放场景或配置装配菜单；`Tests/Editor/` 放 EditMode 测试。并非每个领域都具备全部子目录。

### 统一架构入口与调用链

- `Assets/Scripts/Interaction/Runtime/InteractionArchitecture.cs` 中的类名是 `GameArchitecture`。它是当前架构容器。Interaction、Placement、Network、Economy、Message、Feedback 注册在同一事件总线上；Browsing 由 `CameraBrowsingController` 在 `InputController` 完成装配后按需追加。
- 场景中的 `InputController.Awake` 必须先调用 `GameArchitecture.Configure`，再首次访问 Architecture；`InputController.Update` 每帧调用 `IInteractionInputSystem.ProcessFrame(Time.unscaledTime)`，`OnDisable` 收束未结束指针状态，`OnDestroy` 释放架构。
- 常规指针链为：`LegacyInputUtility` 采集 `PointerSignal` → `InteractionInputSystem` 解析 `InteractionHit` → `PointerIntentModel` 维护按指针/按键的会话并产生 Click/Drag/Hover 意图 → `IntentDispatcher` 按目标和意图类型找到 Sink → `IClickable`、`IDraggable`、`IHoverable` 等对象能力执行行为。
- `PointerFrameSource` 同时作为写入端和读取端。Interaction 每帧发布不可变指针帧；Placement、连线预览等需要原始指针的功能读取最近帧，不直接读取 Unity `Input`。放置模式通过 `IPlacementInputGate` 暂停世界意图解释，但仍发布指针帧。
- 网络拓扑链为：节点 Controller/Registrar 注册 `NodeDescriptor` → `NetworkTopologySystem` 维护节点、能力档案和无向边 → `NetworkConnectionSystem` 在写入前执行角色、重复、位置、容量和交叉检查 → `PacketTrafficSystem` 基于拓扑寻找路径并记录服务器负载。`ServerUpgradeSystem` 处理升级配置、拓扑能力写入和 Economy 消费；`PacketRewardSystem` 订阅成功传输事件为 Economy 增加奖励收入；Message、Feedback 及各类 UI Controller 通过事件接收结果。
- 相机浏览链为：`CameraBrowsingController` 注册浏览 Model/System/Utility → `CameraBrowsingSystem` 消费指针帧和滚轮源维护焦点、缩放、平移与惯性 → Controller 在 `LateUpdate` 应用相机位置和缩放。程序化聚焦使用 Command/System 入口，玩家开始平移时应打断聚焦动画。
- 放置链为：工具栏 `PlacementButton` 发送 `BeginPlacementCommand` → `PlacementSystem` 维护当前 Prefab 和放置状态 → `PlacementPreviewView` 显示幽灵预览 → 左键非 UI 区域通过世界坐标映射实例化并退出，右键取消。一个时刻只能有一个按钮驱动放置帧处理。

### 本项目中的 QFramework 角色

- `Model` 保存跨帧、可观察的领域状态，例如指针会话、放置状态、相机浏览状态、网络拓扑和金币余额。共享配置与 Prefab 不属于 Model 的运行时状态。
- `System` 是领域写入和规则协调的主要入口：外部 Controller 或 Command 不应复制拓扑、连线、升级、经济、输入解释等规则。
- `Utility` 是可替换的输入源、目标解析、事件路由、坐标映射、节点身份或实例化端口；能替换为测试替身的边界优先放在这里。
- `Controller` 连接 Unity 生命周期、序列化引用、物理、UGUI、Camera 与架构接口。它负责采集和呈现，不应成为领域规则的第二个写入口。
- `Command` 表达一次明确的操作请求，例如开始放置、取消放置、聚焦相机或设置缩放。`Event` 表达已经发生的状态变化或表现请求，不承担回滚或隐式业务写入。

### 开发时的文档索引

1. 先读 `AGENT.md` 与 `CONTEXT.md`，再按任务读取相关 `Core/Runtime/Unity` 源码。
2. 放置系统的已确认流程、场景接线和测试清单见 `PLACEMENT_IMPLEMENTATION.md`；它是当前放置实现说明，不替代源码事实。
3. 输入架构可视化资料位于 `docs/input-system.architecture.json`、`docs/input-system-architecture.html` 等本地文件；它们用于理解结构，不能替代当前源码核对。
4. 若任务涉及场景、Prefab 或 ScriptableObject，先在 Unity Editor 中确认实际对象和引用，再修改序列化内容。
5. 若任务涉及跨领域调用，先画清“输入/事件来源 → Model/System 写入口 → 表现层订阅者”的链路；不要仅按文件名猜测职责。
