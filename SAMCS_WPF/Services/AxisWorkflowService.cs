using System;
using System.Collections.Generic;
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
            _axisDefinitions[0].SoftVelocityLimit = 5;
            _axisDefinitions[0].HomePosition = 0f;
            _axisDefinitions[0].PosUnit = "°";
            _axisDefinitions[0].VelUnit = "°/s";

            // 轴 2: Y 前后平移轴
            _axisDefinitions[1] = new AxisDefinition();
            _axisDefinitions[1].Axis = RobotAxis.Axis2;
            _axisDefinitions[1].AxisName = "Y(平移)";
            _axisDefinitions[1].AxisId = "02";
            _axisDefinitions[1].ExpectedMotorModel = "IM42ET_485";
            _axisDefinitions[1].SoftLimitMin = 0.000f;
            _axisDefinitions[1].SoftLimitMax = 200.000f;
            _axisDefinitions[1].SoftVelocityLimit = 5;
            _axisDefinitions[1].HomePosition = 100f;
            _axisDefinitions[1].PosUnit = "mm";
            _axisDefinitions[1].VelUnit = "mm/s";

            // 轴 3: X 左右平移轴
            _axisDefinitions[2] = new AxisDefinition();
            _axisDefinitions[2].Axis = RobotAxis.Axis3;
            _axisDefinitions[2].AxisName = "X(平移)";
            _axisDefinitions[2].AxisId = "03";
            _axisDefinitions[2].ExpectedMotorModel = "IM42ET_485";
            _axisDefinitions[2].SoftLimitMin = 0.000f;
            _axisDefinitions[2].SoftLimitMax = 200.000f;
            _axisDefinitions[2].SoftVelocityLimit = 5;
            _axisDefinitions[2].HomePosition = 100f;
            _axisDefinitions[2].PosUnit = "mm";
            _axisDefinitions[2].VelUnit = "mm/s";

            // 轴 4: Z 上下平移轴
            _axisDefinitions[3] = new AxisDefinition();
            _axisDefinitions[3].Axis = RobotAxis.Axis4;
            _axisDefinitions[3].AxisName = "Z(平移)";
            _axisDefinitions[3].AxisId = "04";
            _axisDefinitions[3].ExpectedMotorModel = "IM42ET_485";
            _axisDefinitions[3].SoftLimitMin = 0.000f;
            _axisDefinitions[3].SoftLimitMax = 75.000f;
            _axisDefinitions[3].SoftVelocityLimit = 5;
            _axisDefinitions[3].HomePosition = 32.5f;
            _axisDefinitions[3].PosUnit = "mm";
            _axisDefinitions[3].VelUnit = "mm/s";

            // 轴 5: P 俯仰轴
            _axisDefinitions[4] = new AxisDefinition();
            _axisDefinitions[4].Axis = RobotAxis.Axis5;
            _axisDefinitions[4].AxisName = "P(俯仰)";
            _axisDefinitions[4].AxisId = "05";
            _axisDefinitions[4].ExpectedMotorModel = "IM35ET_485";
            _axisDefinitions[4].SoftLimitMin = 0.000f;
            _axisDefinitions[4].SoftLimitMax = 90.000f;
            _axisDefinitions[4].SoftVelocityLimit = 5;
            _axisDefinitions[4].HomePosition = 45f;
            _axisDefinitions[4].PosUnit = "°";
            _axisDefinitions[4].VelUnit = "°/s";

            // 轴 6: I 植入轴
            _axisDefinitions[5] = new AxisDefinition();
            _axisDefinitions[5].Axis = RobotAxis.Axis6;
            _axisDefinitions[5].AxisName = "I(植入)";
            _axisDefinitions[5].AxisId = "06";
            _axisDefinitions[5].ExpectedMotorModel = "IM28ET_485";
            _axisDefinitions[5].SoftLimitMin = 0;
            _axisDefinitions[5].SoftLimitMax = 50;
            _axisDefinitions[5].SoftVelocityLimit = 5;
            _axisDefinitions[5].HomePosition = 10f;
            _axisDefinitions[5].PosUnit = "mm";
            _axisDefinitions[5].VelUnit = "mm/s";
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
        /// async/await 说明：
        ///   这是一个 async Task 方法。
        ///   第三步的轮询中用 await Task.Delay(50) 每 50ms 检查一次。
        ///   每次 await 都会把控制权还给 UI 线程，所以 UI 不会卡住。
        /// </summary>
        public async Task HomeAllAxesAsync()
        {
            // ===== 第一步：统一设定所有轴的速度 =====
            for (int i = 0; i < _axisDefinitions.Length; i++)
            {
                AxisDefinition def = _axisDefinitions[i];
                float targetSpeed = 5f;

                // 【安全限位】速度不能超过 SoftVelocityLimit（硬限制，超了就禁止下发）
                if (targetSpeed > def.SoftVelocityLimit)
                {
                    System.Windows.MessageBox.Show(
                        def.AxisName + " 速度超过安全限位！\n" +
                        "允许范围：[0, " + def.SoftVelocityLimit + "]\n" +
                        "目标速度：" + targetSpeed,
                        "安全限位保护",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return;
                }

                // 通过安全检查，下发速度设定指令
                _robotService.GetAxis(def.Axis).SetVelocity(targetSpeed);
            }

            // ===== 第二步：同时下发所有轴的运动指令 =====
            // axisEnums 数组用于第三步的轮询等待
            RobotAxis[] axisEnums = new RobotAxis[_axisDefinitions.Length];

            for (int i = 0; i < _axisDefinitions.Length; i++)
            {
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
            // 每 50ms 检查一次所有轴是否都已停止
            // 使用 while(true) + break 的写法，方便在循环中加调试断点
            while (true)
            {
                bool anyRunning = false;

                // 检查所有轴是否有任何一个还在运行
                for (int i = 0; i < axisEnums.Length; i++)
                {
                    if (_robotService.GetAxis(axisEnums[i]).IsRunning())
                    {
                        anyRunning = true;
                        break;  // 只要有一个还在运行，就不用继续检查了
                    }
                }

                // 全部停止 → 退出循环
                if (!anyRunning)
                {
                    break;
                }

                // 还有轴在运行 → 等 50ms 再检查
                // await 让出 UI 线程，50ms 后自动回到这里继续执行
                await Task.Delay(50);
            }

            // 归位完成，通知操作者
            System.Windows.MessageBox.Show(
                "六轴同步归位完成！", "提示",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
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
        /// async/await 说明：
        ///   每段运动的等待（while + await Task.Delay）都不阻塞 UI。
        ///   每段间的 3 秒延时也用了 await Task.Delay(3000)，
        ///   这 3 秒内 UI 仍然可以响应（比如点紧急停止按钮）。
        /// </summary>
        public async Task TestAllAxesAsync()
        {
            // ===== 统一设定所有轴的速度 =====
            for (int i = 0; i < _axisDefinitions.Length; i++)
            {
                AxisDefinition def = _axisDefinitions[i];
                float targetSpeed = 5f;

                // 【安全限位】速度硬限制检查
                if (targetSpeed > def.SoftVelocityLimit)
                {
                    System.Windows.MessageBox.Show(
                        def.AxisName + " 速度超过安全限位！\n" +
                        "允许范围：[0, " + def.SoftVelocityLimit + "]\n" +
                        "目标速度：" + targetSpeed,
                        "安全限位保护",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return;
                }

                _robotService.GetAxis(def.Axis).SetVelocity(targetSpeed);
            }

            // ===== 逐轴执行三段往返运动 =====
            for (int i = 0; i < _axisDefinitions.Length; i++)
            {
                AxisDefinition def = _axisDefinitions[i];
                AxisController controller = _robotService.GetAxis(def.Axis);

                // ------ 往：运动到上限位 ------
                float targetMax = def.SoftLimitMax;

                // 【安全限位】上限位值本身也应该在有效范围内
                if (targetMax < def.SoftLimitMin || targetMax > def.SoftLimitMax)
                {
                    System.Windows.MessageBox.Show(
                        def.AxisName + " 目标位置超过安全限位！\n" +
                        "允许范围：[" + def.SoftLimitMin + ", " + def.SoftLimitMax + "]\n" +
                        "目标位置：" + targetMax,
                        "安全限位保护",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return;
                }

                // 下发运动指令
                controller.MoveAbsolute(targetMax);

                // 等待运动完成 —— 轮询 IsRunning()，每 50ms 检查一次
                // await 让每次等待都不阻塞 UI 线程
                while (controller.IsRunning())
                {
                    await Task.Delay(50);
                }

                // 到位后延时 3 秒，让操作者有时间观察机械位置
                await Task.Delay(3000);

                // ------ 返：运动到下限位 ------
                float targetMin = def.SoftLimitMin;

                // 【安全限位】下限位值也应该在有效范围内
                if (targetMin < def.SoftLimitMin || targetMin > def.SoftLimitMax)
                {
                    System.Windows.MessageBox.Show(
                        def.AxisName + " 目标位置超过安全限位！\n" +
                        "允许范围：[" + def.SoftLimitMin + ", " + def.SoftLimitMax + "]\n" +
                        "目标位置：" + targetMin,
                        "安全限位保护",
                        System.Windows.MessageBoxButton.OK,
                        System.Windows.MessageBoxImage.Warning);
                    return;
                }

                controller.MoveAbsolute(targetMin);

                // 等待运动完成
                while (controller.IsRunning())
                {
                    await Task.Delay(50);
                }

                // 到位后延时 3 秒
                await Task.Delay(3000);

                // ------ 归：回到默认归位 ------
                float homePos = def.HomePosition;

                // 【安全限位】归位坐标必须在限位范围内
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

                controller.MoveAbsolute(homePos);

                // 等待运动完成
                while (controller.IsRunning())
                {
                    await Task.Delay(50);
                }

                // 到位后延时 3 秒
                await Task.Delay(3000);
            }

            // 六轴测试全部完成
            System.Windows.MessageBox.Show(
                "六轴限位往返测试完成！", "提示",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Information);
        }

        // ===================== 紧急停止 =====================

        /// <summary>
        /// 紧急停止 —— 向全部轴瞬间群发 Stop 指令。
        /// 不等待、不轮询、不检查。
        /// 六个轴的 Stop 指令在同一个循环中连续发出，间隔极短。
        ///
        /// 这是同步方法（不是 async Task），因为 Stop 指令下发极快，
        /// 不需要等待，也不应该被 await 打断。
        /// </summary>
        public void EmergencyStop()
        {
            for (int i = 0; i < _axisDefinitions.Length; i++)
            {
                // 向每个轴发送停止指令
                _robotService.GetAxis(_axisDefinitions[i].Axis).Stop();
            }
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
