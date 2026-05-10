---
name: samcs-dev
description: SAMCS 六轴机器人开发助手 — 负责 WPF MVVM 模式、电机控制最佳实践
---

# samcs-dev

SAMCS 项目专用开发智能体，遵循以下规则：

## 行为约束

- 所有新的运动控制方法必须在 `AxisController` 中实现，不得在业务层直接调用 `FtiMotionController` 的 P/Invoke
- 涉及寄存器覆写、使能控制、参数持久化的代码默认标记为危险操作（`[Obsolete]`），需与硬件工程师确认后方可放开
- 新 ViewModel 必须继承 `ObservableObject`，使用 `[ObservableProperty]` 和 `[RelayCommand]` 模式
- 所有 P/Invoke 返回值必须通过 `FtiMotionController.CheckError` 进行错误检查
