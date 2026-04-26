# MyRobotSDK 2.0 (SAMCS 底层驱动库)

本项目是为 SAMCS (Six-axis Motion Control System) 六轴机器人专门封装的 C# 强类型、面向对象 SDK。
它在原厂 C++ 语言动态链接库 (`ftcoreimc.dll`) 的基础上，提供了安全的生命周期管理以及友好的面向对象调用接口。

## 📁 解决方案目录架构

```text
📦 MyRobotSDK 2.0
├── 📂 Controllers (业务控制器层 - 对外核心 API)
│   ├── 📄 RobotController.cs  # 核心入口：负责建立连接、管理设备句柄与生命周期 (IDisposable)
│   └── 📄 AxisController.cs   # 单轴控制：封装轴体的运动指令、使能控制、实时状态读取等
│
├── 📂 Exceptions (异常处理机制)
│   └── 📄 FtiException.cs     # 硬件通信异常类：将底层返回的错误码包装为标准 C# 异常
│
├── 📂 HAL (硬件抽象层 - 内部隐藏实现)
│   ├── 📄 FtiConstants.cs     # 底层常量字典与状态宏定义
│   └── 📄 FtiMotionController.cs # P/Invoke 声明："翻译官"
│
├── 📂 lib (非托管依赖环境)
│   └── 📂 x64                 # 64 位原厂驱动库 (注：在 VS 中需将 dll 属性设置为"如果较新则复制")
│      └── ⚙️ ftcoreimc.dll
│   
│
└── 📂 Models (数据模型与契约)
    ├── 📄 FtiHandle.cs        # 强类型句柄结构体：提供相等性比较，防止内存指针误传
    └── 📄 RobotAxis.cs        # 严格映射厂商六轴体定义 (Axis1 ~ Axis6)
```
## 🚀 快速上手示例
```text
// 1. 建立控制器连接
using (var robot = RobotController.Connect("COM5", 115200))
{
    // 2. 获取目标轴的控制实例
    var axis1 = robot.GetAxis(RobotAxis.Axis1);
    
    // 3. 执行业务指令
    axis1.MoveAbsolute(100.5f);
    
    // 4. 读取状态
    bool isRunning = axis1.IsRunning();
    float currentPos = axis1.GetPosition();
}
```
## 🕹️ AxisController 核心 API 参考

`AxisController` 是 SAMCS 六轴系统的单轴控制核心。
出于设备的安全考量，所有涉及底层寄存器覆写、系统细分/螺距修改及软限位篡改的危险方法已被物理级屏蔽。
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
| `JogLeft` | 无 | **负向点动**：控制轴向左（负向）连续运行，直到调用 `Stop()`。 |
| `JogRight` | 无 | **正向点动**：控制轴向右（正向）连续运行，直到调用 `Stop()`。 |
| `Stop` | 无 | **紧急停止**：立即截断当前轴的任何运动指令。 |

### 3. 状态读取与遥测 (Telemetry & Status)
用于 WPF 实时界面刷新与状态机判定。

| 方法名 | 返回值 | 功能描述 |
| :--- | :--- | :--- |
| `GetPosition` | `float` | 获取当前轴的绝对坐标位置。 |
| `IsRunning` | `bool` | 查询电机是否处于运动状态（`true` 为运行中，`false` 为已停止）。 |
| `GetLimits` | `(bool PosLimit, bool NegLimit)` | 获取物理限位传感器状态。返回元组，包含正限位和负限位的触发情况。 |
| `GetMotorModel` | `string` | 获取底层绑定的硬件电机型号（如 "IM28"）。 |

### 4. 运行参数配置 (Configuration)

| 方法名 | 参数 / 返回值 | 功能描述 |
| :--- | :--- | :--- |
| `SetVelocity` | `float velocity` | 设定该轴下一次运动的目标指令速度。 |
| `GetVelocity` | `float` | 读取当前设定的目标运行速度。 |

---

### 📖 典型业务调用示例

以下展示了在 WPF 的 ViewModel 中，如何安全地进行单轴调度：

```csharp
// 假设已经通过 robot = RobotController.Connect(...) 建立了连接
var zAxis = robot.GetAxis(RobotAxis.Axis4); // 获取 Z 轴 (进针轴)

// 1. 设定进针速度
zAxis.SetVelocity(10.5f);

// 2. 检查限位安全后，执行相对进针
var limits = zAxis.GetLimits();
if (!limits.PosLimit && !limits.NegLimit)
{
    zAxis.MoveRelative(5.0f);
}

// 3. 轮询状态 (配合异步避免阻塞)
while(zAxis.IsRunning())
{
    float currentPos = zAxis.GetPosition();
    // 更新 WPF UI 进度条...
    await Task.Delay(50); 
}