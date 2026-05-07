# CLAUDE.md — SAMCS 项目指引

## 构建命令

```bash
# 编译并运行 WPF 应用
dotnet build SAMCS_WPF/SAMCS_WPF.csproj
dotnet run --project SAMCS_WPF/SAMCS_WPF.csproj

# 或通过解决方案
dotnet restore SAMCS_WPF.slnx
dotnet build SAMCS_WPF.slnx
```

## DLL 引用策略

**MyRobotSDK 已冻结，其源码已从解决方案中移除，仅保留编译产物作为二进制依赖。**

- `SAMCS_WPF/lib/MyRobotSDK.dll` — 托管程序集 (`.csproj` 中以 `<Reference>` + `<HintPath>` 引用)
- `SAMCS_WPF/lib/MyRobotSDK.xml` — IntelliSense 文档，MSBuild 自动随 DLL 复制到输出
- `SAMCS_WPF/lib/MyRobotSDK.pdb` — 调试符号，允许调试时步进 SDK 源码
- `SAMCS_WPF/lib/x64/ftcoreimc.dll` — 原厂非托管驱动 (`<Content>` + `CopyToOutputDirectory=Always`)
- MyRobotSDK 源码保留在仓库中供查阅，但不参与解决方案编译

## 架构分层

```
SAMCS_WPF (WPF, MVVM) ──二进制引用──→ lib/MyRobotSDK.dll
    → ViewModels/MainViewModel.cs       # UI 逻辑，调用 SDK
    → Services/                         # 服务层 (预留)
    → Models/                           # UI 模型 (预留)
    → Views/                            # 视图 (预留)

MyRobotSDK (仅源码保留，不编译)
    → Controllers/RobotController.cs    # 主控：连接、句柄管理、IDisposable
    → Controllers/AxisController.cs     # 单轴：运动、状态、参数读写
    → HAL/FtiMotionController.cs        # P/Invoke 全量声明 (internal)
    → HAL/FtiConstants.cs               # 底层常量 (internal)
    → Models/RobotAxis.cs               # Axis1~Axis6 枚举
    → Models/FtiHandle.cs               # 强类型句柄 struct
    → Exceptions/FtiException.cs        # 硬件异常，含 ErrorCode
```

## 关键约束

- **x64 only** — 原厂 DLL 仅 64 位，所有项目 `PlatformTarget=x64`
- **net8.0** — 全项目统一运行时
- **危险方法隔离** — `AxisController` 中标记 `[Obsolete("危险操作，仅限调试", true)]` 的方法会在编译时报错，物理级屏蔽。包括：`SetEnabled`, `Home`, `SetZero`, `ChangeAddress`, `SaveParamsPermanently`, `SetAcceleration`, `SetDeceleration`, `SetDivision`, `SetPitch`, `SetSoftLimitP1/P2`, `SetRegisterUInt16/32`, `GetRegisterUInt16/32`
- **不安全代码** — MyRobotSDK 开启 `<AllowUnsafeBlocks>True</AllowUnsafeBlocks>`，由 `NativeLibrary.SetDllImportResolver` 使用
- **DLL 加载** — `FtiMotionController` 静态构造函数通过 `ImportResolver` 从 `lib/x64/ftcoreimc.dll` 加载非托管 DLL
- **DLL 复制** — `ftcoreimc.dll` 需在 `.csproj` 中配置 `<CopyToOutputDirectory>Always</CopyToOutputDirectory>`，两个项目各有一份

## MVVM 模式

- 使用 `CommunityToolkit.Mvvm` 源代码生成器
- ViewModel 必须是 `partial class`，继承 `ObservableObject`
- 属性用 `[ObservableProperty]` 标注（字段命名 `_camelCase`，自动生成 `PascalCase`）
- 命令用 `[RelayCommand]` 标注
- DI 容器：`App.Services` (Microsoft.Extensions.DependencyInjection)
- 设计时 DataContext：在 XAML 中用 `d:Window.DataContext` 声明

## 硬件通信

- 连接方式：串口 (Modbus-RTU, `"COM5" + 115200`) 或网口 (Modbus-TCP, `"192.168.0.11" + 10001`)
- `RobotController.Connect` 仅建立连接不通信，需通过 `GetPosition()` 等实际交互来验证连通性
- `RobotController` 实现 `IDisposable`，业务代码须 `using` 或手动 `Dispose`
- 所有非托管调用返回 `int` 错误码，通过 `FtiMotionController.CheckError` 统一转为 `FtiException`

## 编码风格

- C# 命名：类/方法 `PascalCase`，私有字段 `_camelCase`
- XML 文档注释：所有 public API 均附带 `<summary>` 和参数说明
- `internal` 隔离：HAL 层全部 internal，SDK 对外仅暴露 Controllers + Models + Exceptions
- 项目文件使用 `.csproj` + `.slnx`（新格式解决方案文件）

## 当前状态

- UI 层为硬件验证 Demo：连接 → 读取 6 轴参数 → 上报
- `assets/` 下有三个模块目录 (Implantation, Monitor, Stereotactic) 待开发
- `Services/` 层预留 RobotService 待注册
