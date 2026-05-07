# SAMCS — Six-axis Motion Control System

六轴机器人运动控制上位机系统，用于神经外科手术机器人的精确导航与操作。

## 项目概述

SAMCS 由两层构成：

| 层 | 项目 | 描述 |
|:---|:---|:---|
| **SDK 层** | `MyRobotSDK` | C# 强类型 SDK，封装原厂 C++ 驱动 `ftcoreimc.dll`，提供安全的 P/Invoke 互操作与面向对象 API |
| **UI 层** | `SAMCS_WPF` | WPF 桌面应用，基于 MVVM 模式，负责人机交互与手术流程编排 |

## 前置条件

- **Windows 10/11 x64** — 原厂驱动仅提供 x64 DLL
- **[.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)**
- **Visual Studio 2022+**（推荐）或任意支持 .NET 8 的编辑器

## 快速开始

```bash
# 1. 克隆仓库
git clone <repo-url> && cd SAMCS_WPF

# 2. 还原依赖并编译
dotnet restore SAMCS_WPF.slnx
dotnet build SAMCS_WPF.slnx -c Release

# 3. 运行 WPF 上位机
dotnet run --project SAMCS_WPF/SAMCS_WPF.csproj
```

## 解决方案架构

```text
SAMCS_WPF/
├── MyRobotSDK/                    # 底层驱动 SDK
│   ├── Controllers/               # RobotController (连接) + AxisController (单轴)
│   ├── HAL/                       # P/Invoke 声明与常量 (内部隐藏)
│   ├── Models/                    # FtiHandle, RobotAxis
│   ├── Exceptions/                # FtiException
│   └── lib/x64/ftcoreimc.dll     # 原厂非托管驱动
│
├── SAMCS_WPF/                      # WPF 桌面应用
│   ├── ViewModels/                # MVVM ViewModel 层 (MainViewModel)
│   ├── Views/                     # 视图目录 (预留)
│   ├── Models/                    # UI 模型 (预留)
│   ├── Services/                  # 服务层 (预留)
│   └── assets/                    # 手术模块资源
│       ├── Implantation/          # 植入模块
│       ├── Monitor/               # 监测模块
│       └── Stereotactic/          # 立体定向模块
│
└── SAMCS_WPF.slnx                  # 解决方案文件
```

## 硬件通信架构

```
WPF UI (MVVM)
    ↓ 调用
RobotController.Connect("COM5", 115200)
    ↓ P/Invoke
ftcoreimc.dll (C++ 原厂驱动)
    ↓ Modbus-RTU / Modbus-TCP
六轴电机驱动器 (Axis 01~06)
```

物理轴映射：**Axis1(R-旋转) → Axis2(Y轴) → Axis3(X轴) → Axis4(Z轴/进针) → Axis5(P-俯仰) → Axis6(I-植入)**

## 技术栈

| 技术 | 版本 | 用途 |
|:---|:---|:---|
| .NET | 8.0 | 运行时与 SDK |
| WPF | — | 桌面 UI 框架 |
| CommunityToolkit.Mvvm | 8.4.2 | MVVM 源代码生成器 |
| Microsoft.Extensions.DependencyInjection | 10.0.7 | 依赖注入容器 |
| System.IO.Ports | 10.0.7 | 串口通信支持 |

## 开发约定

- **x64 only** — 原厂 DLL 仅有 64 位版本，所有项目强制 `PlatformTarget=x64`
- **危险方法隔离** — 涉及寄存器覆写、使能控制、软限位修改、参数持久化的方法均标记 `[Obsolete("危险操作，仅限调试", true)]`，编译即报错，物理级屏蔽
- **异常处理** — 所有 P/Invoke 返回值通过 `FtiMotionController.CheckError` 统一转为 `FtiException`
- **生命周期** — `RobotController` 实现 `IDisposable`，务必使用 `using` 语句或手动释放
- **非阻塞运动** — 所有运动指令均为触发即返回，不会阻塞 UI 线程
