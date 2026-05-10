# SAMCS — Six-axis Motion Control System

## 前置条件

- **Windows 10/11 x64** — 原厂驱动仅提供 x64 DLL
- **[.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)**
- **Visual Studio 2026**

## 硬件通信架构

```
WPF UI (MVVM)
    ↓ 调用
RobotController.Connect("COM5", 115200)
    ↓ P/Invoke
ftcoreimc.dll (C++ 原厂驱动)
    ↓ Modbus-RTU
六轴电机驱动器 (Axis 01~06)
```

物理轴映射：**Axis1(R-旋转) → Axis2(Y轴) → Axis3(X轴) → Axis4(Z轴) → Axis5(P-俯仰) → Axis6(I-植入)**

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