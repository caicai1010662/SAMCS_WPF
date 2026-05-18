# REFACTOR_REPORT.md

## 重构概要

**日期：** 2026-05-17  
**分支：** PlainLanguage  
**目标：** 将 MonitorViewModel 中的硬件流程编排下沉到 Service 层，保留现有 UI 行为与功能不变。

---

## 文件清单

### 新增文件（4 个）

| 文件 | 路径 | 说明 |
|------|------|------|
| `AxisDefinition.cs` | `SAMCS_WPF/Models/` | 单轴静态产品规格定义，纯数据类 |
| `AxisRuntimeState.cs` | `SAMCS_WPF/Models/` | 单轴运行时状态快照，纯数据类 |
| `IAxisWorkflowService.cs` | `SAMCS_WPF/Services/` | 轴运动工作流服务接口 |
| `AxisWorkflowService.cs` | `SAMCS_WPF/Services/` | 轴运动工作流服务实现（核心） |

### 修改文件（2 个）

| 文件 | 说明 |
|------|------|
| `App.xaml.cs` | 新增 `IAxisWorkflowService` → `AxisWorkflowService` 的 DI 注册 |
| `MonitorViewModel.cs` | 瘦身：移除所有直接 SDK 调用，改为委托 `IAxisWorkflowService` |

### 未修改文件

| 文件 | 原因 |
|------|------|
| `MonitorView.xaml` | 所有 Binding / Command / 属性名未变，无需修改 |
| `AxisUIModel.cs` | UI 绑定模型保持不变 |
| `IRobotControlService.cs` | 底层硬件服务接口不变 |
| `RobotControlService.cs` | 底层硬件服务实现不变 |
| `MyRobotSDK/` 全部文件 | SDK 层完全不动 |

---

## 每个文件改动目的

### AxisDefinition.cs（新增）
- **目的：** 原来六轴产品规格（轴名、限位、电机型号、归位坐标）硬编码在 `MonitorViewModel.InitializeAxes()` 中，散落在 46 行初始化代码里。抽取为独立数据类，由 `AxisWorkflowService` 统一管理，一处定义、多处使用。
- **改动：** 无（新增文件）

### AxisRuntimeState.cs（新增）
- **目的：** 原来 `UpdateRealtimeAxisData()` 和 `UpdateAxisConfiguration()` 直接在 ViewModel 中读取 SDK 并赋值给 `AxisUIModel`。现在数据采集在 Service 层完成，需要一个纯数据载体把结果传回 ViewModel。
- **改动：** 无（新增文件）

### IAxisWorkflowService.cs（新增）
- **目的：** 定义 ViewModel 与 Service 之间的契约。ViewModel 只知道这个接口，不知道内部实现。
- **改动：** 无（新增文件）

### AxisWorkflowService.cs（新增）
- **目的：** 集中管理六轴产品规格，封装所有硬件流程逻辑。从 `MonitorViewModel` 迁移了以下逻辑：
  - `ConnectAndValidate()` ← 原 `ToggleConnection` 中的连接+校验逻辑
  - `HomeAllAxesAsync()` ← 原 `MonitorViewModel.HomeAllAxesAsync()`
  - `TestAllAxesAsync()` ← 原 `MonitorViewModel.TestAllAxesAsync()`
  - `EmergencyStop()` ← 原 `MonitorViewModel.EmergencyStop()`
  - `ReadRealtime()` ← 原 `MonitorViewModel.UpdateRealtimeAxisData()`
  - `ReadConfiguration()` ← 原 `MonitorViewModel.UpdateAxisConfiguration()`
- **改动：** 所有安全限位检查、运动步骤、等待延时、注释全部保留。

### App.xaml.cs（修改）
- **目的：** DI 容器新增 `IAxisWorkflowService` → `AxisWorkflowService` 的单例注册，与 `IRobotControlService` 生命周期一致。
- **改动：** 新增 1 行注册代码。

### MonitorViewModel.cs（修改）
- **目的：** 瘦身。从 493 行缩减为约 280 行。移除所有 `_robotService.GetAxis().MoveAbsolute/SetVelocity/...` 调用，改为委托 `_workflowService`。
- **改动：**
  - 构造函数参数：`IRobotControlService` → `IAxisWorkflowService`
  - `InitializeAxes()` → `InitializeAxesFromDefinitions()`：从 Service 获取轴定义
  - `ToggleConnection`：连接逻辑委托 `ConnectAndValidate()`
  - `HomeAllAxesAsync`：只做前置检查 + 委托
  - `TestAllAxesAsync`：只做前置检查 + 委托
  - `EmergencyStop`：只做前置检查 + 委托
  - `UpdateRealtimeAxisData`：读 Service → 映射到 AxisUIModel
  - `UpdateAxisConfiguration`：读 Service → 映射到 AxisUIModel
  - `DisconnectSystem`：委托 `_workflowService.Disconnect()`

---

## 重构前后职责对比

| 职责 | 重构前 | 重构后 |
|------|--------|--------|
| UI 绑定属性（端口、连接状态、Axes） | MonitorViewModel | MonitorViewModel（不变） |
| 串口扫描 | MonitorViewModel | MonitorViewModel（不变，纯 UI 层） |
| 六轴产品规格 | MonitorViewModel.InitializeAxes() 硬编码 | AxisWorkflowService 构造函数 + AxisDefinition |
| 连接 + 型号校验 | MonitorViewModel.ToggleConnection | AxisWorkflowService.ConnectAndValidate |
| 六轴同步归位 | MonitorViewModel.HomeAllAxesAsync | AxisWorkflowService.HomeAllAxesAsync |
| 六轴限位往返测试 | MonitorViewModel.TestAllAxesAsync | AxisWorkflowService.TestAllAxesAsync |
| 紧急停止 | MonitorViewModel.EmergencyStop | AxisWorkflowService.EmergencyStop |
| 100ms 实时数据采集 | MonitorViewModel 直接调用 SDK | AxisWorkflowService.ReadRealtime → VM 映射 |
| 60s 配置数据采集 | MonitorViewModel 直接调用 SDK | AxisWorkflowService.ReadConfiguration → VM 映射 |
| 安全限位检查 | MonitorViewModel 内联 | AxisWorkflowService 内联（不变） |
| SDK 底层通信 | RobotControlService | RobotControlService（不变） |

---

## 保持不变的功能清单

1. ✅ 串口扫描与选择
2. ✅ 连接 + 六轴电机型号握手校验（失败弹窗）
3. ✅ 六轴同步归位（统一设速 → 统一下发 → 统一等待）
4. ✅ 六轴限位往返测试（设速 → 逐轴三段运动 → 延时）
5. ✅ 紧急停止（群发 Stop）
6. ✅ 100ms 实时刷新（位置、速度、运行状态）
7. ✅ 60s 配置刷新（加速、减速、螺距、细分）
8. ✅ 速度安全限位检查（超过 SoftVelocityLimit 硬阻止）
9. ✅ 位置安全限位检查（超出 [SoftLimitMin, SoftLimitMax] 硬阻止）
10. ✅ 所有 MessageBox 提示语义不变
11. ✅ 波特率 ComboBox（仅前端展示）
12. ✅ XAML 完全未修改，所有 Binding 不变

---

## 风险点与后续建议

### 风险点

1. **数据采集增加了一次 List 分配：** `ReadRealtime()` 和 `ReadConfiguration()` 每次返回新建的 `List<AxisRuntimeState>`。100ms 周期下每秒创建 10 个 List，内存压力极小（每个 List 6 个元素），GC 可承受。若后续发现 GC 压力，可改为复用数组。

2. **IsConnected 状态同步：** ViewModel 同时维护了自己的 `_isConnected` 字段和通过 `_workflowService.IsConnected` 查询。正常流程下两者一致。若未来有人在 Service 层直接断开而不通知 VM，可能出现不一致。当前代码路径单一，风险极低。

3. **MessageBox 在 Service 中调用：** Service 层直接调用 `System.Windows.MessageBox`，这在严格的 Clean Architecture 中算违规（Service 不应依赖 UI 框架）。但本项目是工业控制上位机，不是分布式系统。保持 MessageBox 在 Service 中可以避免额外的异常传递层，符合工业控制风格的可调试性要求。

### 后续建议

1. **安全限位值可配置化：** 当前 `SoftLimitMin/Max` 和 `HomePosition` 硬编码在 `AxisWorkflowService` 构造函数中。若后续需要根据手术方案调整，可从 JSON 配置文件加载。

2. **高频轮询异常降噪：** 当前 `ReadRealtime()` 在通信异常时静默返回空列表。若长时间通信故障，操作者可能不知道。建议后续加一个连续失败计数器，连续失败 N 次后在 UI 上显示"通信中断"警告。

3. **AxisRuntimeState 池化（可选）：** 若后续轴数量增加或刷新频率提高，可考虑对象池复用，减少 GC 压力。当前六轴 100ms 场景不需要。

4. **IMonitorViewModel 接口（可选）：** 若后续需要单元测试 ViewModel 行为，可为 MonitorViewModel 提取接口。当前阶段不需要。

---

## 构建与验证结果

```
dotnet build SAMCS_WPF.slnx
结果：已成功生成。0 个警告，0 个错误。

dotnet build SAMCS_WPF/SAMCS_WPF.csproj
结果：已成功生成。0 个警告，0 个错误。
```

**MonitorViewModel 行数变化：** 493 行 → ~280 行（缩减约 43%）

**MonitorViewModel 中不再出现的 SDK 调用：**
- `GetAxis()` — 已迁移
- `MoveAbsolute()` — 已迁移
- `SetVelocity()` — 已迁移
- `IsRunning()` — 已迁移
- `GetMotorModel()` — 已迁移
- `Stop()` — 已迁移
- `GetPosition()` — 已迁移
- `GetVelocity()` — 已迁移
- `GetAcceleration()` — 已迁移
- `GetDeceleration()` — 已迁移
- `GetPitch()` — 已迁移
- `GetDivision()` — 已迁移
