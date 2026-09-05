# 点选放置系统（QFramework）实现说明

## 目标
在 Unity + QFramework 工程中实现「点选放置」：点击工具按钮进入放置模式 → 半透明预览跟随鼠标 → 左键在世界放置一个指定 Prefab 并**单次退出** → 右键随时取消。

## 已确认的关键决定
1. 放置一个即退出（单次流程，右键取消）。
2. 落点 = 相机屏幕坐标 → 固定 z 平面（默认 0，`InputController.mPlacementZ` 可配）。
3. 输入数据来自现有 Interaction 输入系统的**每帧指针帧数据**（`PointerFrameEvent`），不直接读 `Input.*`。
4. 放置期间**抑制世界点击**（`InteractionInputSystem` 整帧 gate + 进入时 `CancelAll`）。
5. 每帧调度由**被点击的激活按钮**（`PlacementButton`）的 `Update` 调用 `PlacementSystem.ProcessFrame`。
6. 架构升级为**统一 `GameArchitecture`**：Interaction 与 Placement 注册进同一容器，共享事件总线。

## 架构与文件清单

### 新增（Placement 域）
| 文件 | 职责 |
|---|---|
| `Assets/Scripts/Placement/Core/PlacementEvents.cs` | `PlacementStartedEvent` / `PlacementCancelledEvent` / `PlacementPlacedEvent` |
| `Assets/Scripts/Placement/Runtime/IPlacementModel.cs` | `IPlacementModel` + `PlacementModel`（`SelectedPrefab`/`IsPlacing`/`PointerWorldPosition`，BindableProperty） |
| `Assets/Scripts/Placement/Runtime/IPlacementSystem.cs` | `IPlacementSystem` + `PlacementSystem`（Begin/Cancel/TryPlace/ProcessFrame） |
| `Assets/Scripts/Placement/Runtime/IPlacementInstantiator.cs` | 实例化端口（测试替身可替换） |
| `Assets/Scripts/Placement/Runtime/IWorldPointerMapper.cs` | 屏幕→世界端口（测试替身可替换） |
| `Assets/Scripts/Placement/Runtime/PlacementCommands.cs` | `BeginPlacementCommand` / `CancelPlacementCommand` |
| `Assets/Scripts/Placement/Runtime/PlacementInputGate.cs` | 放置激活时阻塞 Interaction 意图解释 |
| `Assets/Scripts/Placement/Unity/CameraWorldPointerMapper.cs` | 相机射线与 z 平面求交 + EventSystem UI 判定 |
| `Assets/Scripts/Placement/Unity/UnityObjectInstantiator.cs` | `Object.Instantiate/Destroy` 适配 |
| `Assets/Scripts/Placement/Unity/PlacementPreviewView.cs` | 半透明幽灵：SpriteRenderer alpha、禁 Collider2D、LateUpdate 跟手、UI 上方隐藏 |
| `Assets/Scripts/Placement/Unity/PlacementButton.cs` | 工具栏按钮：OnClick 进入放置、成为唯一激活驱动者、Update 驱动 ProcessFrame |
| `Assets/Scripts/Placement/Editor/PlacementSceneSetup.cs` | 一键装配场景（菜单 `CUC260905/Placement/Setup Demo Scene`） |
| `Assets/Scripts/Placement/Tests/Editor/PlacementSystemTests.cs` | EditMode 测试（8 个用例） |

### 新增（Interaction 域，3 个文件）
- `Assets/Scripts/Interaction/Core/PointerFrameEvent.cs`
- `Assets/Scripts/Interaction/Core/IPointerFrameSource.cs`（`IPointerFrameSink` + `IPointerFrameSource`）
- `Assets/Scripts/Interaction/Core/IPlacementInputGate.cs`
- `Assets/Scripts/Interaction/Runtime/PointerFrameSource.cs`（内存数据源，写入时拷贝为快照）

### 修改（现有文件）
- `Assets/Scripts/Interaction/Runtime/InteractionArchitecture.cs` → **升级改名 `GameArchitecture`**，迁移原注册并新增放置域注册
- `Assets/Scripts/Interaction/Runtime/InteractionInputSystem.cs` → gate 检查 + 每帧写入 `IPointerFrameSink`
- `Assets/Scripts/Interaction/Unity/InputConfig.cs` → 新增 `PlacementZ`
- `Assets/Scripts/Interaction/Unity/InputController.cs` → 指向 `GameArchitecture`，新增 `mPlacementZ`

### 未改动
`PointerIntentModel` / `IntentDispatcher` / `ComponentSinkResolver` / 目标解析器 / `LegacyInputUtility` —— 现有交互语义原样保留。

## 每帧调度
```
InputController.Update
  └─ InteractionInputSystem.ProcessFrame
       1) LegacyInputUtility 采集信号（唯一消费者，无双读）
       2) 更新最近屏幕坐标
       3) gate 检查：放置激活 → 跳过目标解析与意图解释（抑制世界点击），仍发布帧数据
       4) 发布 PointerFrameEvent 到 PointerFrameSource（拷贝快照）

激活的 PlacementButton.Update（sActiveDriver == this 且 IsPlacing）
  └─ PlacementSystem.ProcessFrame
       - 从 IPointerFrameSource 取最近一帧
       - 屏幕→z 平面 → PointerWorldPosition（预览跟手）
       - Left Down（非 UI）→ TryPlace（放置后退出）
       - Right Down → Cancel
```
说明：放置激活期间输入独占由 `IPlacementInputGate`（`PlacementInputGate.IsBlocked = IsPlacing`）实现；`PlacementButton` 静态 `sActiveDriver` 保证同一时刻只有一个按钮驱动，避免一次左键放置多个。

## 状态机
```
Idle ──点击按钮(BeginPlacementCommand)──▶ Placing（创建半透明预览，gate 生效）
Placing ──左键(非UI)放置成功──▶ Idle（实例化、发 PlacementPlacedEvent、销毁预览、gate 释放）
Placing ──右键──▶ Idle（发 PlacementCancelledEvent）
Placing ──点击其他按钮──▶ Placing（仅切换 SelectedPrefab，预览重建）
```
进入 Placing 时调用 `IInteractionInputSystem.CancelAll()` 收束残留会话。

## 场景接线（二选一）
1. **一键装配**：Unity 菜单 `CUC260905/Placement/Setup Demo Scene`
   - 确保 `PlacementView`（挂 `PlacementPreviewView`）
   - 复用/创建 Canvas 下 Button，挂 `PlacementButton`，onClick 绑 `OnButtonClick`
   - 生成白色方块演示 prefab（`Assets/Placement/Demo/PlacementDemo.prefab`）并赋给按钮
2. **手动接线**：按上述组件手动摆放；`InputController.mPlacementZ` 默认 0。

## 测试
- `Assets/Scripts/Placement/Tests/Editor/PlacementSystemTests.cs`（8 用例）：
  Begin 进入并选中 prefab / 左键放置一次并退出 / 右键取消不放置 / UI 上左键不放置 / 放置中切换 prefab / ProcessFrame 更新世界坐标 / gate 仅放置时阻塞 / 空 prefab 不实例化
- 运行：Unity Test Runner（EditMode）→ `CUC260905.Tests.PlacementSystemTests`。

## 验收对照
| 需求 | 实现 |
|---|---|
| 点击按钮提供 prefab 给放置 System | `PlacementButton.OnButtonClick` → `BeginPlacementCommand` → `PlacementSystem.Begin` |
| 左键在 2D 世界放置一个指定 prefab | `PlacementSystem.ProcessFrame` 消费指针帧，Left Down → `TryPlace`（屏幕→z 平面） |
| 放置后退出流程 | `TryPlace` 放置后 `EndPlacement()`（单次退出） |
| 右键取消流程 | Right Down → `Cancel()` |
| 未放置时显示低透明度预览并跟随鼠标 | `PlacementPreviewView` 监听 BindableProperty：半透明幽灵 + LateUpdate 跟手 |
| 注意现有实现与 Update 调度 | 复用 `InteractionInputSystem` 输入链 + `InputController.Update`；放置调度交给激活按钮 |

## 已知说明 / 注意点
- 放置期间世界点击（IClickable/IDraggable/IHoverable）整体被 gate，退出后恢复。
- 输入存在最多 1 帧处理延迟（按钮 Update 与输入 Update 的顺序差异导致），对放置交互无感。
- 预览用 prefab 副本：仅处理 `SpriteRenderer` alpha 与 `Collider2D` 禁用；粒子/Mesh 类 prefab 需扩展。
- 当前工具链无法在本机直连 8080/编译 Unity，**编译与 EditMode 测试需在你的 Unity 环境（或 Unity MCP）中运行**。
