# MyRobotSDK (底层驱动库)

本项目是为 SAMCS (Six-axis Motion Control System) 六轴机器人专门封装的 C# 强类型、面向对象的 SDK。
它在原厂 C++ 语言动态链接库 (`ftcoreimc.dll`) 的基础上，提供了安全的生命周期管理以及友好的面向对象调用接口。

## 📁 解决方案目录架构

```text
📦 MyRobotSDK
├── 📂 Controllers (业务控制器层 - 对外核心 API)
│   ├── 📄 RobotController.cs         # 核心入口：负责建立连接、管理设备句柄与生命周期 (IDisposable)
│   └── 📄 AxisController.cs          # 单轴控制：封装轴体的运动指令、使能控制、实时状态读取等
│
├── 📂 Exceptions (异常处理机制)
│   └── 📄 FtiException.cs            # 硬件通信异常类：将底层返回的错误码包装为标准 C# 异常
│
├── 📂 HAL (硬件抽象层 - 内部隐藏实现)
│   ├── 📄 FtiConstants.cs            # 底层常量字典与状态宏定义
│   └── 📄 FtiMotionController.cs     # P/Invoke 声明："翻译官"
│
├── 📂 lib (非托管依赖环境)
│   └── 📂 x64                        # 64 位原厂驱动库
│      └── ⚙️ ftcoreimc.dll
│   
│
└── 📂 Models (数据模型与契约)
    ├── 📄 FtiHandle.cs               # 强类型句柄结构体：提供相等性比较，防止内存指针误传
    └── 📄 RobotAxis.cs               # 严格映射厂商六轴体定义 Axis1 ~ Axis6 分别对应 R、Y、X、Z、P、I
```

## 🚀 快速上手示例

```text
// 1. 建立控制器连接
using (var robot = RobotController.Connect("COM5", 115200))
{
    // 2. 获取目标轴的控制实例
    var axis1 = robot.GetAxis(RobotAxis.Axis1);  //对应 旋转轴R
    
    // 3. 执行业务指令
    axis1.SetVelocity(3);                        //下发指令，旋转轴 R 运动速度为 3 °/s
    axis1.MoveAbsolute(30f);                     //下发指令，旋转轴 R 运动至 30° 位置
    
    // 4. 读取状态
    bool isRunning = axis1.IsRunning();          //下发指令，查询旋转轴 R 是否处于运动状态，返回值为布尔类型，0表示停止，1表示运动
    float currentPos = axis1.GetPosition();      //下发指令，查询旋转轴 R 位于何处，返回值为单精度浮点数类型，表示停止该电机当前的位置
}
```

## 🎛️ RobotController 核心 API 参考

`RobotController` 是整个 SDK 的入口类，负责设备连接、生命周期管理以及轴控制器实例的获取。

### 静态方法

| 方法名 | 参数 | 返回值 | 功能描述 |
| :--- | :--- | :--- | :--- |
| `Connect` | `string comportOrIp, int baudOrPort` | `RobotController` | 建立与底层控制器的通信连接。串口模式传 `"COM5" + 115200`（Modbus-RTU）；网络模式传 `"192.168.0.11" + 10001`（Modbus-TCP）。成功后返回控制器实例，需用 `using` 包裹以确保资源释放。 |
| `SdkVersion` | 无 (静态属性) | `string?` | 获取底层 `ftcoreimc.dll` 的版本号字符串（如 `"2.2.2.0"`）。 |

### 实例成员

| 成员名 | 类型 / 返回值 | 功能描述 |
| :--- | :--- | :--- |
| `GetAxis(RobotAxis axis)` | `AxisController` | 传入目标轴枚举，返回该轴的单轴控制器实例。后续所有运动与配置操作均通过 `AxisController` 完成。 |
| `Dispose()` | `void` | 关闭与底层设备的通信连接，释放非托管句柄。推荐使用 `using` 块自动调用。 |

### 连接示例

```csharp
// 串口连接（Modbus-RTU）
using (var robot = RobotController.Connect("COM5", 115200))
{
    var axis1 = robot.GetAxis(RobotAxis.Axis1);
    // ...
}

// 网络连接（Modbus-TCP）
using (var robot = RobotController.Connect("192.168.0.11", 10001))
{
    var axis1 = robot.GetAxis(RobotAxis.Axis1);
    // ...
}
```

---

## 🕹️ AxisController 核心 API 参考

`AxisController` 是 SAMCS 六轴系统的单轴控制核心。
出于设备的安全考量，所有涉及底层寄存器覆写、系统细分/螺距修改及软限位篡改的危险方法已被物理级屏蔽（`[Obsolete]` 编译阻断）。

**WPF 业务层仅允许调用以下经过严格测试的安全 API。**

### 1. 基础属性

| 属性名 | 类型 | 描述 |
| :--- | :--- | :--- |
| `Axis` | `RobotAxis` | 获取当前控制器绑定的目标轴体（枚举：`Axis1` ~ `Axis6`，对应物理 R-Y-X-Z-P-I 轴）。 |

### 2. 运动控制 (Motion Control)

所有运动指令均为**触发即返回（非阻塞）**，不会卡死 WPF 的 UI 线程。

| 方法名 | 参数 | 功能描述 |
| :--- | :--- | :--- |
| `MoveAbsolute` | `float position` | **绝对运动**：控制轴运行到指定的绝对位置坐标。 |
| `MoveRelative` | `float distance` | **相对运动**：以当前位置为起点，执行设定距离的增量运动（正负值决定方向）。 |
| `JogLeft` | 无 | **负向点动**：控制轴向左（负向）连续运行，直到调用 `Stop()`或运行到限位。 |
| `JogRight` | 无 | **正向点动**：控制轴向右（正向）连续运行，直到调用 `Stop()`或运行到限位。 |
| `Stop` | 无 | **紧急停止**：立即截断当前轴的任何运动指令。 |

### 3. 状态读取与遥测 (Telemetry & Status)

用于 WPF 实时界面刷新与状态机判定。

| 方法名 | 返回值 | 功能描述 |
| :--- | :--- | :--- |
| `GetPosition` | `float` | 获取当前轴的绝对坐标位置。 |
| `IsRunning` | `bool` | 查询电机是否处于运动状态（`true` 为运行中，`false` 为已停止）。 |
| `GetLimits` | `(bool PosLimit, bool NegLimit)` | 获取物理限位传感器状态。返回元组，包含正限位和负限位的触发情况。 |
| `GetMotorModel` | `string` | 获取底层绑定的硬件电机型号（如 "IM28"）。 |

### 4. 速度与加减速配置

| 方法名 | 参数 / 返回值 | 功能描述 |
| :--- | :--- | :--- |
| `SetVelocity` | `float velocity` | 设定该轴下一次运动的目标指令速度。 |
| `GetVelocity` | `float` | 读取当前设定的目标运行速度。 |
| `GetAcceleration` | `ushort` | 获取电机加速时间（从启动到目标速度所需时间，单位 ms）。 |
| `GetDeceleration` | `ushort` | 获取电机减速时间（从目标速度到停止所需时间，单位 ms）。 |

### 5. 机械参数（只读）

以下参数由原厂出厂校准，业务层只允许读取，禁止写入。

| 方法名 | 返回值 | 功能描述 |
| :--- | :--- | :--- |
| `GetDivision` | `int` | 获取当前轴的细分值（电机旋转一周所需脉冲数）。六轴均为出厂定值。 |
| `GetPitch` | `float` | 获取当前轴的螺距（电机旋转一周对应平移台实际位移，单位 mm）。 |

### 6. 软限位（只读）

| 方法名 | 返回值 | 功能描述 |
| :--- | :--- | :--- |
| `GetSoftLimitP1` | `float` | 获取当前轴软限位的位置下限。 |
| `GetSoftLimitP2` | `float` | 获取当前轴软限位的位置上限。 |

---

## ⚠️ FtiException 异常类

| 成员 | 类型 | 描述 |
| :--- | :--- | :--- |
| `ErrorCode` | `int` | 原厂底层返回的 16 进制错误码（如 `0x8001` 表示无效句柄），用于查阅硬件故障手册。 |
| `Message` | `string` | 继承自 `Exception`，包含错误码和当前操作上下文描述，适合直接展示给操作人员。 |

所有 Controller 方法在底层通信失败时均会抛出 `FtiException`，建议在 WPF ViewModel 层统一 `try-catch` 拦截并提取 `Message` 展示。

---

## 📐 RobotAxis 轴体枚举

| 枚举值 | 物理含义 | 控制器地址 |
| :--- | :--- | :--- |
| `RobotAxis.Axis1` | 旋转轴 (R - Rotation) | 01 |
| `RobotAxis.Axis2` | Y 轴平移 | 02 |
| `RobotAxis.Axis3` | X 轴平移 | 03 |
| `RobotAxis.Axis4` | Z 轴平移 | 04 |
| `RobotAxis.Axis5` | 俯仰轴 (P - Pitch) | 05 |
| `RobotAxis.Axis6` | 植入轴 (I - Implantation) | 06 |

---
