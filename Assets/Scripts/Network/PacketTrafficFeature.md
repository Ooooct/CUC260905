# 数据包流量与动态路由

用户节点部署并登记成功后，会先等待一段随机部署冷却，之后按随机间隔向另一个随机用户节点发送随机大小的数据包（经服务器中继）。用户节点不会作为中继；一条有效路由只能是“用户节点 → 一个或多个服务器节点 → 目标用户节点”。数据包只在用户节点之间发送，服务器是转发/处理节点，不会作为数据包终点。

## 配置

在 `UserNode` Prefab 或场景内用户节点的 `UserNodeController` Inspector 中配置：

- `Deployment Cooldown Min / Max`：部署后首次发送的随机等待秒数。
- `Send Interval Min / Max`：后续两次发送之间的随机秒数。
- `Packet Size Min / Max`：单包大小范围（Mb）。
- `Load Cost Weight`：路径规避拥堵服务器的强度，0 时只按跳数选路。
- `Message Target Id`：无可行路由时写入的提示终端，默认 `MainTerminal`。

默认值是冷却 `0.5–1.5s`、间隔 `0.8–1.8s`、包大小 `10–30Mb`、负载权重 `4`。服务器容量继续取 `NetworkNodeRegistrar` 注入的 `DataProcessingPerSecond`（及其升级配置），不在本功能中改写。

## 路由与吞吐

每个服务器维护最近 1 秒内所有已通过数据包的大小之和 `CurrentDataLoadPerSecond`。寻找路径时，候选服务器必须满足：

```
最近 1 秒已处理量 + 当前包大小 <= DataProcessingPerSecond
```

其中 `DataProcessingPerSecond = 0` 沿用现有语义，表示不限流。Dijkstra 的进入服务器代价为：

```
1 + Load Cost Weight × 预测利用率²
```

选定路径后，系统会在同一调用中向路径上的所有服务器预留该数据包；因此同一帧中的后续数据包会看到已经更新的负载，不会超卖容量。没有连通路径，或所有路径均被容量筛除时，系统发布 `PacketUnreachableEvent` 并向配置的 `IMessageSystem` 终端写入“数据包不可达”消息。若拓扑中不存在其他用户节点可作目标（例如只有 1 个用户节点），`SendRandomPacket` 直接返回 `DestinationUnavailable`，不发布消息与事件：这属于“暂无发送目标”的静默跳过，不是路由失败，也不会增加总体负载。

服务器信息面板的吞吐字段现在显示为“当前近 1 秒负载 / 处理上限 Mbps”。

## 总体负载 HUD

将 `GlobalLoadBarController` 挂到屏幕空间 `Canvas` 后，它会在编辑态持久化创建
`GlobalLoadHUD`、红色 `GlobalLoadSlider` 与仅含百分比的 `GlobalLoadPercentage`。若场景尚未预先挂载，
`GlobalLoadHudBootstrap` 会在进入场景后自动补齐该组件。
它监听 `PacketUnreachableEvent`：每次不可达默认增加 `20%`，默认每秒降低 `5%`。两个数值都可在
`GlobalLoadBarController` Inspector 中修改。达到 `100%` 时当前版本只输出一次 `Debug.LogError`
作为游戏失败占位；后续正式失败流程应替换这一表现，不应改写数据包事件链。

## 自动化验证

`Assets/Scripts/Network/Tests/Editor/PacketTrafficSystemTests.cs` 覆盖：

- 路径中每台服务器都会占用容量，且恰好在 1 秒后释放；
- 多条可行路径时偏向较低负载路径；
- 所有路径超出上限时，拒绝发送并发布不可达消息与事件；
- 目的端必须是用户节点：`user → server` 被拒（`DestinationNotUserNode`），随机发送只会在其他用户节点之间选取目标（没有其他用户节点时返回 `DestinationUnavailable`，且不发布消息/事件，属静默跳过）；
- 不允许自我发送：显式 `user → user`（同节点）被拒（`SelfSendForbidden`），随机发送在多种子下从不指向源节点；
- 总体负载的不可达惩罚、按秒衰减与首次达到 100% 的失败阈值。
