# 数据包流量与动态路由

用户节点部署并登记成功后，须先等待**部署接入时间**（全局统一固定秒数）才能开始发送与接收数据包：接入完成前，该节点既不能作为数据包源（发送），也不能被其他用户节点作为接收目标（目的地）。接入完成后按随机间隔向另一个随机用户节点发送随机大小的数据包（经服务器中继）。服务器节点没有接入门控，注册后立即可用。用户节点不会作为中继；一条有效路由只能是“用户节点 → 一个或多个服务器节点 → 目标用户节点”。数据包只在用户节点之间发送，服务器是转发/处理节点，不会作为数据包终点。

## 配置

部署接入时间是全局统一固定值：默认 `10 秒`，即常量 `NetworkTopologyModel.DefaultDeploymentAccessTime`（`0` 表示关闭接入门控），由 `GameArchitecture` 装配时传入 `NetworkTopologyModel`；调整时直接改该常量即可。

在 `UserNode` Prefab 或场景内用户节点的 `UserNodeController` Inspector 中配置数据包节奏：

- `Send Interval Min / Max`：两次发送之间的随机秒数（当前不随发送次数增长）。
- `Packet Size Base Mean / Ceiling Mean`：单包平均大小随发送次数增长的下限与饱和上限（Mb）。
- `Saturation Send Count`：单包大小增长达到饱和所需的发送次数。
- `Packet Size Jitter`：单包大小在曲线均值上的乘性随机抖动比例（±jitter）。
- `Packet Size Min / Max`：单包大小的绝对钳位范围（Mb）。
- `Load Cost Weight`：路径规避拥堵服务器的强度，0 时只按跳数选路。
- `Message Target Id`：无可行路由时写入的提示终端，默认 `MainTerminal`。

默认值是间隔 `2–4s`（中心 3s，±1s 随机偏移）、包大小基准均值 `15Mb`、饱和均值 `50Mb`、饱和次数 `300`、
曲率 `1`、抖动 `0.25`、钳位 `5–75Mb`、负载权重 `4`。单包大小最大不超过 `75Mb`。
服务器容量继续取 `NetworkNodeRegistrar` 注入的 `DataProcessingPerSecond`（及其升级配置），不在本功能中改写。

## 单包大小增长（按发送次数）

每个用户节点累计已发送次数 n 驱动单包平均大小线性增长：

```
t(n) = clamp(n / N)
平均大小(n) = BaseMean + (CeilingMean − BaseMean) · t(n)
实际大小 = clamp(平均大小(n) · (1 + Jitter·U(−1,1)), Min, Max)
```

n=0 时平均大小等于基准均值，随发送次数线性增长，n≥N 后钳位到饱和均值。
实际单包在曲线均值附近按乘性比例随机抖动（保留随机波动），并受绝对钳位约束，最大不超过 `75Mb`。
实现为纯逻辑 `SendPaceCurve`（`Assets/Scripts/Network/Core/`）；`UserNodeController` 按 `mSendCount` 采样后递增。
发送间隔固定为随机 `2–4s`（3s ±1s），不随发送次数增长；频率增长曲线后续接入时复用同一 `SendPaceCurve`。

部署接入时间通过 `INetworkTopologyModel` 暴露：

- `DeploymentAccessTime`：全局统一的接入时长（秒）。
- `IsDeploymentAccessComplete(nodeId, now)`：用户节点是否已完成接入（服务器恒为 `true`，未注册节点为 `false`）。
- `TryGetDeploymentAccessRemaining(nodeId, now, out remainingSeconds)`：剩余接入秒数（不小于 0）。

## 路由与吞吐

每个服务器维护最近 1 秒内所有已通过数据包的大小之和 `CurrentDataLoadPerSecond`。寻找路径时，候选服务器必须满足：

```
最近 1 秒已处理量 + 当前包大小 <= DataProcessingPerSecond
```

其中 `DataProcessingPerSecond = 0` 沿用现有语义，表示不限流。Dijkstra 的进入服务器代价为：

```
1 + Load Cost Weight × 预测利用率²
```

选定路径后，系统会在同一调用中向路径上的所有服务器预留该数据包；因此同一帧中的后续数据包会看到已经更新的负载，不会超卖容量。没有连通路径，或所有路径均被容量筛除时，系统发布 `PacketUnreachableEvent` 并向配置的 `IMessageSystem` 终端写入“数据包不可达”消息。若候选服务器因吞吐上限被拒绝，事件的 `ProblemNodeIds` 会包含这些服务器；场景表现层据此与起始传输点一同显示红色反馈圆。随机发送只在**已接入**的用户节点中选取目标（未接入节点不会被选为目标）。若拓扑中不存在其他已接入的用户节点可作目标（例如只有 1 个用户节点，或其余用户节点均未完成接入），`SendRandomPacket` 直接返回 `DestinationUnavailable`，不发布消息与事件：这属于“暂无发送目标”的静默跳过，不是路由失败，也不会增加总体负载。

服务器信息面板把吞吐量分两端显示：`DataShowcur` 显示当前近 1 秒负载，`DataShowmax` 显示处理上限（`0` 上限显示 ∞）。数值变化时数字按 easeOutCubic 跳动到新值。

## 总体负载 HUD

将 `GlobalLoadBarController` 挂到屏幕空间 `Canvas` 后，它会在编辑态持久化创建
`GlobalLoadHUD`、红色 `GlobalLoadSlider` 与仅含百分比的 `GlobalLoadPercentage`。若场景尚未预先挂载，
`GlobalLoadHudBootstrap` 会在进入场景后自动补齐该组件。
它监听 `PacketUnreachableEvent`：每次不可达默认增加 `5%`，默认每秒降低 `2%`。两个数值都可在
`GlobalLoadBarController` Inspector 中修改。达到 `100%` 时当前版本只输出一次 `Debug.LogError`
作为游戏失败占位；后续正式失败流程应替换这一表现，不应改写数据包事件链。

## 自动化验证

`Assets/Scripts/Network/Tests/Editor/PacketTrafficSystemTests.cs` 覆盖：

- 路径中每台服务器都会占用容量，且恰好在 1 秒后释放；
- 多条可行路径时偏向较低负载路径；
- 所有路径超出上限时，拒绝发送并发布不可达消息与事件，事件包含被吞吐上限拒绝的服务器；
- 目的端必须是用户节点：`user → server` 被拒（`DestinationNotUserNode`），随机发送只会在其他用户节点之间选取目标（没有其他用户节点时返回 `DestinationUnavailable`，且不发布消息/事件，属静默跳过）；
- 不允许自我发送：显式 `user → user`（同节点）被拒（`SelfSendForbidden`），随机发送在多种子下从不指向源节点；
- 目标选取为均匀随机：`SendRandomPacket` 对全部已接入的其他用户节点等概率抽样（与位置无关），多种子下各候选被选次数接近相等；
- 总体负载的不可达惩罚、按秒衰减与首次达到 100% 的失败阈值。

`Assets/Scripts/Network/Tests/Editor/SendPaceCurveTests.cs` 覆盖单包大小的对数增长：
归一化进度参考值与单调性、饱和与越界钳位、均值边界、乘性抖动带、默认参数下不超过 75Mb、同种子可复现、非法参数防御。

`Assets/Scripts/Network/Tests/Editor/DeploymentAccessTimeTests.cs` 覆盖部署接入时间：

- 暴露 API：`DeploymentAccessTime` 返回全局配置值；`TryGetDeploymentAccessRemaining` 返回从完整时长到 0 的倒计时；`IsDeploymentAccessComplete` 在接入时长结束后翻转；
- 服务器节点恒为已接入（无门控），未注册节点返回未接入；
- 显式发送时源未接入返回 `SourceNotAccessible`、目标未接入返回 `DestinationNotAccessible`，且不占用服务器容量；
- 随机发送只选择已接入的目标；所有其他用户节点均未接入时返回 `DestinationUnavailable` 且静默（不发布消息/事件）。
