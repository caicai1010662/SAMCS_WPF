using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyRobotSDK.Controllers;
using MyRobotSDK.Models;
using SAMCS_WPF.Models;

namespace SAMCS_WPF.Services
{
    /// <summary>
    /// ===== 白话文说明 =====
    /// 这个类是"轴运动工作流服务"的具体实现。
    /// 它包含了所有与硬件交互的逻辑：连接校验、归位、测试、急停、数据采集。
    ///
    /// 为什么要把这些逻辑从 ViewModel 搬到这里：
    ///   ViewModel 的职责是管理 UI 状态，不是控制电机。
    ///   把几百行的运动控制代码放在 ViewModel 里，会让 ViewModel 又大又难改。
    ///   搬到 Service 层后，ViewModel 变得很薄，运动控制逻辑集中在 Service 中，
    ///   找 bug 时不用在 UI 代码和硬件代码之间来回跳。
    ///
    /// 六轴产品规格（轴名、限位、归位坐标）也存在这里，
    /// 因为这些数据属于"硬件是什么"，不属于"UI 怎么显示"。
    ///
    /// ===== 关于 async/await =====
    /// 运动命令需要等电机停止，等待期间如果用同步循环会卡死 UI。
    /// async/await 的作用：让等待变成"非阻塞"的。
    ///   当代码执行到 await Task.Delay(50) 时，
    ///   当前方法会暂停，把控制权还给 UI 线程，
    ///   50ms 后自动从暂停点继续执行。
    /// 这样 UI 在等待期间仍然可以响应点击和刷新。
    /// </summary>
    public class AxisWorkflowService : IAxisWorkflowService
    {
        // ===================== 依赖字段 =====================

        /// <summary>
        /// 底层机器人控制服务。
        /// 这个服务封装了 RobotController 的生命周期和单轴控制器分配。
        /// AxisWorkflowService 通过它来操作电机，不直接碰 SDK 的 RobotController。
        /// </summary>
        private readonly IRobotControlService _robotService;

        // ===================== 运动任务取消与急停状态 =====================

        /// <summary>
        /// 取消令牌源 —— 串联"急停按钮"和"运动任务"的桥梁。
        /// 当 TestAllAxesAsync 或 HomeAllAxesAsync 启动时，创建新的 CTS。
        /// 当 EmergencyStop 被调用时，触发 Cancel，通知运动任务立即终止。
        /// 为什么需要这个字段：
        ///   没有 CTS 的话，运动任务内部的 for 循环和 Task.Delay
        ///   完全不知道外面已经按了急停，会继续执行后续轴的运动。
        /// </summary>
        private CancellationTokenSource? _cts;

        /// <summary>
        /// 急停锁定状态标记。
        /// true = 急停已触发，拒绝一切新运动指令。
        /// false = 正常状态，允许运动。
        /// 为什么用独立的 bool 而不是只依赖 CTS：
        ///   CTS 是"这一次任务"的取消信号，任务结束后 CTS 就失效了。
        ///   急停状态是"系统级"的锁定，即使任务已经终止，
        ///   也必须阻止新任务启动，直到操作者手动复位。
        /// </summary>
        private bool _isEmergencyStopped;

        // ===================== 六轴产品规格（静态配置） =====================

        /// <summary>
        /// 六轴静态定义数组。
        /// 顺序：R(旋转) → Y(平移) → X(平移) → Z(平移) → P(俯仰) → I(植入)
        /// 这个顺序是固定的，和硬件地址 01~06 一一对应。
        /// 数组中每个元素存储了对应轴的完整产品规格。
        /// </summary>
        private readonly AxisDefinition[] _axisDefinitions;

        // ===================== 构造函数 =====================

        /// <summary>
        /// 构造函数 —— 初始化六轴产品规格定义。
        /// 这些值来自产品硬件规格书，运行时不会改变。
        ///
        /// 六轴列表：
        ///   轴1 (01): R(旋转)    IM42ET_485  限位[-180, 180]   归位 0°
        ///   轴2 (02): Y(平移)    IM42ET_485  限位[0, 200]      归位 100mm
        ///   轴3 (03): X(平移)    IM42ET_485  限位[0, 200]      归位 100mm
        ///   轴4 (04): Z(平移)    IM42ET_485  限位[0, 75]       归位 32.5mm
        ///   轴5 (05): P(俯仰)    IM35ET_485  限位[0, 90]       归位 45°
        ///   轴6 (06): I(植入)    IM28ET_485  限位[0, 50]       归位 10mm
        /// </summary>
        public AxisWorkflowService(IRobotControlService robotService)
        {
            _robotService = robotService;

            _axisDefinitions = new AxisDefinition[6];

            // 轴 1: R 旋转轴
            _axisDefinitions[0] = new AxisDefinition();
            _axisDefinitions[0].Axis = RobotAxis.Axis1;
            _axisDefinitions[0].AxisName = "R(旋转)";
            _axisDefinitions[0].AxisId = "01";
            _axisDefinitions[0].ExpectedMotorModel = "IM42ET_485";
            _axisDefinitions[0].SoftLimitMin = -180.000f;
            _axisDefinitions[0].SoftLimitMax = 180.000f;
            _axisDefinitions[0].SoftVelocityLimit = 5.000f;
            _axisDefinitions[0].HomePosition = 0.000f;

            // 轴 2: Y 前后平移轴
            _axisDefinitions[1] = new AxisDefinition();
            _axisDefinitions[1].Axis = RobotAxis.Axis2;
            _axisDefinitions[1].AxisName = "Y(平移)";
            _axisDefinitions[1].AxisId = "02";
            _axisDefinitions[1].ExpectedMotorModel = "IM42ET_485";
            _axisDefinitions[1].SoftLimitMin = 0.000f;
            _axisDefinitions[1].SoftLimitMax = 200.000f;
            _axisDefinitions[1].SoftVelocityLimit = 5.000f;
            _axisDefinitions[1].HomePosition = 100.000f;

            // 轴 3: X 左右平移轴
            _axisDefinitions[2] = new AxisDefinition();
            _axisDefinitions[2].Axis = RobotAxis.Axis3;
            _axisDefinitions[2].AxisName = "X(平移)";
            _axisDefinitions[2].AxisId = "03";
            _axisDefinitions[2].ExpectedMotorModel = "IM42ET_485";
            _axisDefinitions[2].SoftLimitMin = 0.000f;
            _axisDefinitions[2].SoftLimitMax = 200.000f;
            _axisDefinitions[2].SoftVelocityLimit = 5.000f;
            _axisDefinitions[2].HomePosition = 100.000f;

            // 轴 4: Z 上下平移轴
            _axisDefinitions[3] = new AxisDefinition();
            _axisDefinitions[3].Axis = RobotAxis.Axis4;
            _axisDefinitions[3].AxisName = "Z(平移)";
            _axisDefinitions[3].AxisId = "04";
            _axisDefinitions[3].ExpectedMotorModel = "IM42ET_485";
            _axisDefinitions[3].SoftLimitMin = 0.000f;
            _axisDefinitions[3].SoftLimitMax = 75.000f;
            _axisDefinitions[3].SoftVelocityLimit = 5.000f;
            _axisDefinitions[3].HomePosition = 32.500f;

            // 轴 5: P 俯仰轴
            _axisDefinitions[4] = new AxisDefinition();
            _axisDefinitions[4].Axis = RobotAxis.Axis5;
            _axisDefinitions[4].AxisName = "P(俯仰)";
            _axisDefinitions[4].AxisId = "05";
            _axisDefinitions[4].ExpectedMotorModel = "IM35ET_485";
            _axisDefinitions[4].SoftLimitMin = 0.000f;
            _axisDefinitions[4].SoftLimitMax = 90.000f;
            _axisDefinitions[4].SoftVelocityLimit = 5.000f;
            _axisDefinitions[4].HomePosition = 45.000f;

            // 轴 6: I 植入轴
            _axisDefinitions[5] = new AxisDefinition();
            _axisDefinitions[5].Axis = RobotAxis.Axis6;
            _axisDefinitions[5].AxisName = "I(植入)";
            _axisDefinitions[5].AxisId = "06";
            _axisDefinitions[5].ExpectedMotorModel = "IM28ET_485";
            _axisDefinitions[5].SoftLimitMin = 0.000f;
            _axisDefinitions[5].SoftLimitMax = 50.000f;
            _axisDefinitions[5].SoftVelocityLimit = 5.000f;
            _axisDefinitions[5].HomePosition = 10.000f;
        }

        // ===================== 属性 =====================

        /// <summary>
        /// 当前是否与硬件建立有效连接。
        /// 直接委托给底层 IRobotControlService 查询。
        /// </summary>
        public bool IsConnected
        {
            get { return _robotService.IsConnected; }
        }

        // ===================== 轴定义查询 =====================

        /// <summary>
        /// 返回六轴静态定义的只读副本，供 ViewModel 初始化 UI 模型。
        /// 返回 IReadOnlyList 是为了防止 ViewModel 意外修改产品规格。
        /// </summary>
        public IReadOnlyList<AxisDefinition> GetAxisDefinitions()
        {
            return Array.AsReadOnly(_axisDefinitions);
        }

        // ===================== 连接与校验 =====================

        /// <summary>
        /// 建立物理连接并执行六轴电机型号握手校验。
        ///
        /// 流程：
        ///   1. 调用底层 SDK 建立串口连接（波特率固定 115200）
        ///   2. 逐轴读取实际电机型号
        ///   3. 与预设型号逐一比对
        ///   4. 任一轴不匹配 → 断开连接 + 弹窗提示
        ///   5. 全部匹配 → 连接成功
        ///
        /// 为什么需要型号校验：
        ///   实际硬件可能接线错误（比如把 Y 轴的电机插到了 X 轴接口上）。
        ///   如果不校验，上位机会用错误的参数去控制电机，可能导致机械碰撞。
        ///   校验可以提前发现接线问题。
        /// </summary>
        public void ConnectAndValidate(string port)
        {
            // 步骤 1：建立物理连接
            // 波特率固定 115200，这是硬件固件决定的，不允许配置
            _robotService.Connect(port, 115200);

            // 步骤 2：逐轴校验电机型号
            for (int i = 0; i < _axisDefinitions.Length; i++)
            {
                AxisDefinition def = _axisDefinitions[i];

                // 从 SDK 读取该轴实际安装的电机型号
                string realModel = _robotService.GetAxis(def.Axis).GetMotorModel();

                // 比对预设型号和实际型号
                if (realModel != def.ExpectedMotorModel)
                {
                    // 型号不匹配 → 断开连接，提示操作者检查硬件
                    _robotService.Dispose();

                    System.Windows.MessageBox.Show(
                        "设备校验失败！\n" +
                        "轴体 " + def.AxisName + " 预设为 " + def.ExpectedMotorModel +
                        "，但实际读取到 " + realModel + "。\n" +
                        "请检查串口选择是否正确或硬件连线是否匹配。",
                        "连接终止",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Error);
                    return;
                }
            }
        }

        /// <summary>
        /// 断开硬件连接，释放 SDK 资源（串口句柄等）。
        /// </summary>
        public void Disconnect()
        {
            _robotService.Dispose();
        }

        // ===================== 急停状态管理 =====================

        /// <summary>
        /// 当前是否处于紧急停止锁定状态。
        /// </summary>
        public bool IsEmergencyStopped
        {
            get { return _isEmergencyStopped; }
        }

        /// <summary>
        /// 解除紧急停止锁定状态。
        /// 调用后 IsEmergencyStopped 恢复为 false，允许下发新的运动指令。
        /// 不恢复电机使能，不自动运动——仅解除软件层面的指令封锁。
        ///
        /// 为什么解除操作是独立方法而不是自动恢复：
        ///   工业安全规范要求急停后必须由操作者明确确认"危险已解除"，
        ///   不能由软件自动判断恢复时机。
        /// </summary>
        public void ResetEmergencyStop()
        {
            _isEmergencyStopped = false;
        }

        // ===================== 六轴同步归位 =====================

        /// <summary>
        /// 六轴同步归位 —— 所有轴同时启动，统一等待全部停止。
        ///
        /// 为什么是"同步"而不是"逐个"：
        ///   手术机器人归位时，六个轴需要同时回到安全位置。
        ///   如果逐个归位，先动的轴可能和后动的轴发生机械干涉。
        ///   同时启动可以减少这种风险。
        ///
        /// 流程分三步：
        ///   第一步：统一设定所有轴速度为 5（带安全限位检查）
        ///   第二步：对所有轴同时下发归位运动指令（不等待单轴完成）
        ///   第三步：轮询等待，直到所有轴全部停止
        ///
        /// CancellationToken 说明：
        ///   方法内部创建新的 CTS，通过 _cts 字段暴露给 EmergencyStop。
        ///   每一步等待都传入 token，确保急停时能瞬间中断。
        ///   方法结束时在 finally 中清理 _cts。
        /// </summary>
        public async Task HomeAllAxesAsync()
        {
            // ===== 入口急停状态检查 =====
            if (_isEmergencyStopped)
            {
                System.Windows.MessageBox.Show(
                    "系统处于紧急停止锁定状态！\n请先解除急停锁定后再执行归位操作。",
                    "操作被拒绝",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            // 创建新的取消令牌源，同时保存到字段中
            // 为什么每次调用都要新建 CTS：
            //   CTS 只能 Cancel 一次，Cancel 后就废了。
            //   下次运动任务需要一个全新的 CTS。
            CancellationTokenSource cts = new CancellationTokenSource();
            _cts = cts;
            CancellationToken token = cts.Token;

            try
            {
                // ===== 第一步：统一设定所有轴的速度 =====
                for (int i = 0; i < _axisDefinitions.Length; i++)
                {
                    // 每一次循环迭代前检查是否已被取消
                    token.ThrowIfCancellationRequested();

                    AxisDefinition def = _axisDefinitions[i];
                    float targetSpeed = 5f;

                    _robotService.GetAxis(def.Axis).SetVelocity(targetSpeed);
                }

                // 设速之后再次检查取消信号（急停可能在设速期间触发）
                token.ThrowIfCancellationRequested();

                // ===== 第二步：同时下发所有轴的运动指令 =====
                RobotAxis[] axisEnums = new RobotAxis[_axisDefinitions.Length];

                for (int i = 0; i < _axisDefinitions.Length; i++)
                {
                    token.ThrowIfCancellationRequested();

                    AxisDefinition def = _axisDefinitions[i];
                    float targetPos = def.HomePosition;

                    // 【安全限位】目标位置必须在 [SoftLimitMin, SoftLimitMax] 内
                    if (targetPos < def.SoftLimitMin || targetPos > def.SoftLimitMax)
                    {
                        System.Windows.MessageBox.Show(
                            def.AxisName + " 目标位置超过安全限位！\n" +
                            "允许范围：[" + def.SoftLimitMin + ", " + def.SoftLimitMax + "]\n" +
                            "目标位置：" + targetPos,
                            "安全限位保护",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Warning);
                        return;
                    }

                    axisEnums[i] = def.Axis;
                    _robotService.GetAxis(def.Axis).MoveAbsolute(targetPos);
                }

                // ===== 第三步：轮询等待所有轴停止 =====
                // 每 50ms 检查一次，每次等待都传入 token
                while (true)
                {
                    // 检查取消信号（急停可能在等待期间触发）
                    token.ThrowIfCancellationRequested();

                    bool anyRunning = false;

                    for (int i = 0; i < axisEnums.Length; i++)
                    {
                        if (_robotService.GetAxis(axisEnums[i]).IsRunning())
                        {
                            anyRunning = true;
                            break;
                        }
                    }

                    if (!anyRunning)
                    {
                        break;
                    }

                    // 传入 token：急停时这个 Delay 会立即抛出异常退出，
                    // 不需要等完 50ms
                    await Task.Delay(50, token);
                }

                // 归位完成
                System.Windows.MessageBox.Show(
                    "六轴同步归位完成！", "提示",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (OperationCanceledException)
            {
                // 急停触发导致的任务取消，这是正常流程，不弹窗不报错
                // 急停处理已在 EmergencyStop() 中完成（Stop 指令 + 状态锁定）
            }
            finally
            {
                // 无论正常完成还是被急停打断，都要清理 CTS
                // Dispose 前先检查是否就是当前这个 CTS，
                // 防止覆盖其他任务新创建的 CTS
                if (_cts == cts)
                {
                    _cts = null;
                }
                try
                {
                    cts.Dispose();
                }
                catch
                {
                    // Dispose 异常不影响流程
                }
            }
        }

        // ===================== 六轴限位往返测试 =====================

        /// <summary>
        /// 六轴限位往返测试 —— 每个轴依次执行三段运动。
        ///
        /// 测试目的：
        ///   1. 验证每个轴能否正常运动到极限位置
        ///   2. 验证限位参数设置是否正确
        ///   3. 观察机械运行是否平稳、有无异常声音
        ///
        /// 每个轴的三段运动：
        ///   往：运动到上限位 → 等待停止 → 停留 3 秒（让操作者观察）
        ///   返：运动到下限位 → 等待停止 → 停留 3 秒
        ///   归：回到默认归位 → 等待停止 → 停留 3 秒
        ///
        /// 为什么逐轴而不是同时：
        ///   测试时需要单独观察每个轴的运动情况。
        ///   同时运动会让人看不清哪个轴出了问题。
        ///
        /// CancellationToken 说明：
        ///   每个轴的每段运动前都检查 token，急停下瞬间跳出循环。
        ///   所有 await Task.Delay 都传入 token，3 秒延时能被瞬间打断。
        ///   方法结束时在 finally 中清理 _cts。
        /// </summary>
        public async Task TestAllAxesAsync()
        {
            // ===== 入口急停状态检查 =====
            // 如果系统已处于急停锁定，直接拒绝，不创建 CTS
            if (_isEmergencyStopped)
            {
                System.Windows.MessageBox.Show(
                    "系统处于紧急停止锁定状态！\n请先解除急停锁定后再执行测试。",
                    "操作被拒绝",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Warning);
                return;
            }

            // 创建新的取消令牌源
            CancellationTokenSource cts = new CancellationTokenSource();
            _cts = cts;
            CancellationToken token = cts.Token;

            try
            {
                // ===== 统一设定所有轴的速度 =====
                for (int i = 0; i < _axisDefinitions.Length; i++)
                {
                    token.ThrowIfCancellationRequested();

                    AxisDefinition def = _axisDefinitions[i];
                    float targetSpeed = 5f;

                    _robotService.GetAxis(def.Axis).SetVelocity(targetSpeed);
                }

                // 设速完成后再次检查取消信号
                token.ThrowIfCancellationRequested();

                // ===== 逐轴执行三段往返运动 =====
                for (int i = 0; i < _axisDefinitions.Length; i++)
                {
                    // 每个轴开始前检查是否被取消
                    // 这是最关键的一行：急停后不会再进入下一个轴的循环
                    token.ThrowIfCancellationRequested();

                    AxisDefinition def = _axisDefinitions[i];
                    AxisController controller = _robotService.GetAxis(def.Axis);

                    // ------ 往：运动到上限位 ------
                    float targetMax = def.SoftLimitMax;

                    // 下发运动指令
                    controller.MoveAbsolute(targetMax);

                    // 等待运动完成 —— 轮询 IsRunning()
                    // 每 50ms 检查一次，同时检查取消信号
                    while (controller.IsRunning())
                    {
                        token.ThrowIfCancellationRequested();
                        await Task.Delay(50, token);
                    }

                    // 到位后延时 3 秒，让操作者有时间观察机械位置
                    // 传入 token：急停时这个 3 秒等待被瞬间打断，不需要死等
                    await Task.Delay(3000, token);

                    // ------ 返：运动到下限位 ------
                    float targetMin = def.SoftLimitMin;

                    // 急停可能在上面的安全检查期间触发
                    token.ThrowIfCancellationRequested();

                    controller.MoveAbsolute(targetMin);

                    // 等待运动完成，每次检查取消信号
                    while (controller.IsRunning())
                    {
                        token.ThrowIfCancellationRequested();
                        await Task.Delay(50, token);
                    }

                    // 到位后延时 3 秒，带 token
                    await Task.Delay(3000, token);

                    // ------ 归：回到默认归位 ------
                    float homePos = def.HomePosition;

                    if (homePos < def.SoftLimitMin || homePos > def.SoftLimitMax)
                    {
                        System.Windows.MessageBox.Show(
                            def.AxisName + " 目标位置超过安全限位！\n" +
                            "允许范围：[" + def.SoftLimitMin + ", " + def.SoftLimitMax + "]\n" +
                            "目标位置：" + homePos,
                            "安全限位保护",
                            System.Windows.MessageBoxButton.OK,
                            System.Windows.MessageBoxImage.Warning);
                        return;
                    }

                    token.ThrowIfCancellationRequested();

                    controller.MoveAbsolute(homePos);

                    // 等待运动完成
                    while (controller.IsRunning())
                    {
                        token.ThrowIfCancellationRequested();
                        await Task.Delay(50, token);
                    }

                    // 到位后延时 3 秒，带 token
                    await Task.Delay(3000, token);
                }

                // 六轴测试全部完成
                System.Windows.MessageBox.Show(
                    "六轴限位往返测试完成！", "提示",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (OperationCanceledException)
            {
                // 急停触发导致的任务取消，正常流程，不弹窗
            }
            finally
            {
                // 清理 CTS
                if (_cts == cts)
                {
                    _cts = null;
                }
                try
                {
                    cts.Dispose();
                }
                catch
                {
                    // Dispose 异常不影响流程
                }
            }
        }

        // ===================== 紧急停止 =====================

        /// <summary>
        /// 紧急停止 —— 三件事必须同时做：
        ///   1. 取消所有正在运行的异步运动任务（TestAllAxesAsync / HomeAllAxesAsync）
        ///   2. 向全部六个轴瞬间群发 Stop 指令
        ///   3. 将系统锁定为急停状态，拒绝一切新运动指令
        ///
        /// 三件事的顺序不能乱：
        ///   先 Cancel（通知任务退出循环），再发 Stop（硬件停止），最后锁状态（防止新任务）。
        ///   如果先发 Stop 再 Cancel，任务可能刚好在 Stop 后、Cancel 前
        ///   进入下一个轴的循环，导致后续轴继续运动。
        ///
        /// try-catch 包围 _cts?.Cancel() 的原因：
        ///   Cancel 会触发所有注册的回调和等待中的 Task.Delay，
        ///   极端情况下可能抛出 AggregateException 或 ObjectDisposedException。
        ///   这些异常不应该阻止后续的 Stop 指令下发。
        /// </summary>
        public void EmergencyStop()
        {
            // ===== 步骤 1：取消所有正在运行的运动任务 =====
            // 这会让 TestAllAxesAsync / HomeAllAxesAsync 中的
            // token.ThrowIfCancellationRequested() 和 await Task.Delay(..., token)
            // 立即抛出 OperationCanceledException，终止整个运动序列
            try
            {
                if (_cts != null)
                {
                    _cts.Cancel();
                }
            }
            catch (AggregateException)
            {
                // Cancel 触发的 Task.Delay 取消异常，正常现象，吞掉
            }
            catch (ObjectDisposedException)
            {
                // 极端情况：CTS 已被 Dispose，说明任务早已结束，无需处理
            }

            // ===== 步骤 2：群发 Stop 指令到全部六个轴 =====
            for (int i = 0; i < _axisDefinitions.Length; i++)
            {
                try
                {
                    _robotService.GetAxis(_axisDefinitions[i].Axis).Stop();
                }
                catch
                {
                    // 单个轴 Stop 失败不阻断其他轴的 Stop 下发
                    // 原因：急停时硬件可能已处于异常状态，
                    // 不能因为一个轴通信失败就放弃其他轴的停止
                }
            }

            // ===== 步骤 3：锁定系统为急停状态 =====
            // 此后任何运动指令（归位、测试）在入口处直接拒绝
            _isEmergencyStopped = true;
        }

        // ===================== 高频实时数据读取（100ms） =====================

        /// <summary>
        /// 高频实时数据读取 —— 每 100ms 调用一次。
        ///
        /// 只读取三个最常变化的值：
        ///   - CurrentPosition（当前位置）
        ///   - Velocity（当前速度）
        ///   - IsRunning（是否正在运行）
        ///
        /// 不读取配置参数（加速度、螺距等），因为那些值不常变，
        /// 每次读会增加串口通信负担。
        ///
        /// 通信异常处理策略：
        ///   100ms 一次非常频繁，偶尔一两次通信失败是正常的（电磁干扰等）。
        ///   所以异常时直接返回空列表，不弹窗、不崩溃。
        ///   下一轮 100ms 后自动重试。
        ///   如果硬件真的断了，连接状态会被其他机制检测到。
        /// </summary>
        public IReadOnlyList<AxisRuntimeState> ReadRealtime()
        {
            List<AxisRuntimeState> result = new List<AxisRuntimeState>(6);

            try
            {
                for (int i = 0; i < _axisDefinitions.Length; i++)
                {
                    AxisDefinition def = _axisDefinitions[i];
                    AxisController controller = _robotService.GetAxis(def.Axis);

                    // 为每个轴创建一个状态快照，填入从 SDK 读取的值
                    AxisRuntimeState state = new AxisRuntimeState();
                    state.Axis = def.Axis;
                    state.CurrentPosition = controller.GetPosition();
                    state.Velocity = controller.GetVelocity();
                    state.IsRunning = controller.IsRunning();

                    result.Add(state);
                }
            }
            catch (Exception)
            {
                // 高频轮询的瞬时通信异常，静默忽略
                // 原因：不能每 100ms 弹一次窗，会干扰操作者
                result.Clear();
            }

            return result;
        }

        // ===================== 低频配置数据读取（60s） =====================

        /// <summary>
        /// 低频配置数据读取 —— 每 60 秒调用一次，连接时也会立即调用一次。
        ///
        /// 读取四个不常变化的运动参数：
        ///   - Acceleration（加速度，单位 ms）
        ///   - Deceleration（减速度，单位 ms）
        ///   - Pitch（螺距，单位 mm）
        ///   - Division（细分，单位 P/R）
        ///
        /// 为什么 60 秒一次：
        ///   这些参数是系统初始化时写入的，运行中几乎不会变化。
        ///   偶尔读取确认一下即可，不需要 100ms 高频轮询。
        /// </summary>
        public IReadOnlyList<AxisRuntimeState> ReadConfiguration()
        {
            List<AxisRuntimeState> result = new List<AxisRuntimeState>(6);

            try
            {
                for (int i = 0; i < _axisDefinitions.Length; i++)
                {
                    AxisDefinition def = _axisDefinitions[i];
                    AxisController controller = _robotService.GetAxis(def.Axis);

                    // 为每个轴创建状态快照，只填配置字段（位置速度不填）
                    AxisRuntimeState state = new AxisRuntimeState();
                    state.Axis = def.Axis;
                    state.Acceleration = controller.GetAcceleration();
                    state.Deceleration = controller.GetDeceleration();
                    state.Pitch = controller.GetPitch();
                    state.Division = controller.GetDivision();

                    result.Add(state);
                }
            }
            catch (Exception)
            {
                // 配置读取失败不影响实时刷新，静默跳过
                result.Clear();
            }

            return result;
        }
    }
}
