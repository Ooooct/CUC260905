# 节点连线功能（Node Connection）

本次迭代在 `Network` 模块加入“拖拽连线”能力：从任一节点拖出预览线，松手落在另一节点上即完成连线；用户↔用户、自连、重复连线、与既有连线交叉均被拒绝。连线与预览的粗细、颜色、材质可配置；边作为逻辑数据持久化在拓扑模型中，同时每条连线带命中碰撞体（世界单位粗细，随相机缩放），右键可取消/删除连线。

## 使用方式

1. 打开 `Assets/Scenes/SampleScene.unity`，场景已包含 `Network Connections` 对象（挂 `NetworkConnectionController`）。
2. 运行场景：通过 `UserNodeButton` / `ServerNodeButton` 放置节点（或使用场景内既有服务器节点）。
3. 左键按住任一节点拖出预览线 → 拖到另一节点上松开 → 连线建立；拖到空白处/UI 或非法目标则取消。
4. **右键取消连线**：拖拽预览进行中按右键 → 取消本次连线；非拖拽时右键点击一条已建立的连线 → 删除该连线（节点优先：指针落在节点上时不删除其下方连线；UI 上方不触发）。
5. 也可用菜单 `CUC260905/Network/Setup Connection Scene` 一键把控制器接入任意场景。

## 连通规则

| 场景 | 结果 |
| --- | --- |
| 用户 ↔ 用户 | ❌ `UserToUserForbidden` |
| 用户 ↔ 服务器 | ✅ |
| 服务器 ↔ 服务器 | ✅ |
| 自连（同一节点） | ❌ `SameNode` |
| 已连接（无向重复） | ❌ `AlreadyConnected` |
| 节点未登记 / 位置缺失 | ❌ `NodeNotRegistered` / `NodePositionUnavailable` |
| 新边与既有边内部相交 | ❌ `CrossingEdge`（共享端点不算交叉） |
| 全部通过 | ✅ 写入拓扑并发布 `NodeConnectivityChangedEvent` |

交叉判定为纯几何：两条线段在**内部（不含端点）**相交才算交叉；平行/共线重叠不算。

## 实现分层

```
Core    NetworkConnectionRules         纯逻辑：ConnectionVerdict、SegmentsCross、CheckCrossing、ValidateRoles
Runtime INodePositionProvider          NodeId → 世界坐标（交叉检查需要）
Runtime INetworkConnectionSystem       唯一业务入口：组合角色/重复/位置/交叉校验 → 写拓扑
Unity   NetworkConnectionController    位置表、边线/预览拉伸 Sprite 线段（世界单位粗细）、碰撞体、配置项、事件订阅、右键取消、拖拽器注入
Unity   NodeConnectionDragger          IDraggable：把手势转发给 INetworkConnectionTool（控制器自动注入节点）
Editor  NetworkConnectionSceneSetup    菜单一键接线
```

- 模型新增只读出口 `INetworkTopologyModel.Edges`（既有边快照，供交叉/路径等只读规则）。
- `GameArchitecture` 注册 `INetworkConnectionSystem`。
- 拖拽器在节点登记时由控制器 `AddComponent` 注入并 `RebuildCapabilities()`，对场景既有节点与放置新建节点统一生效，**无需手工编辑 prefab**。
- **碰撞体**：每条已建立连线挂 `BoxCollider2D`（可配置厚度），置于 Ignore Raycast 层（不在节点解析 mask 内），不参与/不阻挡节点交互；右键删除用 `Physics2D.OverlapPointAll(~0)` 单独命中，节点优先。
- **右键检测**：`InteractionInputSystem.PublishFrame` 在写帧快照的同时广播 `PointerFrameEvent`，控制器订阅它处理右键取消（预览取消优先于连线删除）。

## 配置项（`NetworkConnectionController` Inspector）

- 连线：`mEdgeWidth`（世界单位粗细，随相机缩放；默认 0.6 = 旧值的 1/10）、`mEdgeColor`、`mEdgeMaterial`（留空用 Sprites/Default 自建）、`mEdgeSortingOrder`（默认 -1，置于节点之下）。
- 预览：`mPreviewWidth`（世界单位；默认 0.4 = 旧值的 1/10）、`mPreviewColor`（默认灰色）、`mPreviewMaterial`、`mPreviewSortingOrder`（默认 1，置于节点之上）。
- 碰撞：`mLineHitThickness`（世界单位，连线右键命中区厚度，默认 0.35）。

## 后续“连线检查”扩展点

边以 `NetworkEdge` 持久化于模型，坐标经 `INodePositionProvider` 提供；后续点击选线/删线、路径查找、流量检查等可直接几何查询：
- 全部边：`INetworkTopologyModel.Edges` / `TryGetEdge(a, b)`。
- 点选线段：当前已通过连线碰撞体（`BoxCollider2D`）实现右键命中；若需更细粒度的几何判定，也可将指针投影到 z=0 平面后与各边段计算距离。

## 测试

- `Assets/Scripts/Network/Tests/Editor/NetworkConnectionRulesTests.cs`：线段交叉/共点/平行/共线、CheckCrossing、角色规则。
- `Assets/Scripts/Network/Tests/Editor/NetworkConnectionSystemTests.cs`：成功连线、用户↔用户拒绝、自连、未登记、重复、交叉、共享端点邻接、位置缺失、删除后重连（右键取消的模型契约）。
- 需在 Unity Test Runner（Edit Mode）中运行。
- **编译验证**：已在 Unity 2022.3.62f3 引用集上通过 `dotnet build` 验证 `Assembly-CSharp` 与 `Assembly-CSharp-Editor` 均 **0 错误 0 警告**（修复了集合 `.Contains` 误绑定到 `MemoryExtensions.Contains(ReadOnlySpan<char>,…,StringComparison)` 的 CS7036，改为显式序数比较）。

## 相关修改文件

- 新增：`Assets/Scripts/Network/Core/NetworkConnectionRules.cs`
- 新增：`Assets/Scripts/Network/Runtime/INodePositionProvider.cs`
- 新增：`Assets/Scripts/Network/Runtime/INetworkConnectionSystem.cs`
- 新增：`Assets/Scripts/Network/Unity/NetworkConnectionController.cs`
- 新增：`Assets/Scripts/Network/Unity/NodeConnectionDragger.cs`
- 新增：`Assets/Scripts/Network/Editor/NetworkConnectionSceneSetup.cs`
- 新增：`Assets/Scripts/Network/Tests/Editor/NetworkConnectionRulesTests.cs`
- 新增：`Assets/Scripts/Network/Tests/Editor/NetworkConnectionSystemTests.cs`
- 修改：`Assets/Scripts/Network/Runtime/INetworkTopologyModel.cs`（新增 `Edges`）
- 修改：`Assets/Scripts/Interaction/Runtime/InteractionArchitecture.cs`（注册连线 System）
- 修改：`Assets/Scripts/Interaction/Runtime/InteractionInputSystem.cs`（广播 `PointerFrameEvent` 供右键取消）
- 修改：`Assets/Scenes/SampleScene.unity`（新增 `Network Connections` 根对象）
- 修改：`Assets/Resources/Prefabs/ServerNode.prefab`（清空硬编码 `server-1`，放置多台服务器时自动生成唯一 ID）
