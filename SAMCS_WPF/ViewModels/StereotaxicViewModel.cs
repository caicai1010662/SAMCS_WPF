using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyRobotSDK.Controllers;
using MyRobotSDK.Models;
using SAMCS_WPF.Models;
using SAMCS_WPF.Services;

namespace SAMCS_WPF.ViewModels
{
    /// <summary>
    /// 控制模式 —— 全局切换，所有 6 轴同时生效
    /// </summary>
    public enum ControlMode
    {
        /// <summary>粗调：mm/s 或 °/s 级速度</summary>
        Coarse,

        /// <summary>细调：μm/s 级速度</summary>
        Fine,

        /// <summary>数值调：输入绝对/相对位置值</summary>
        Numeric
    }

    /// <summary>
    /// 数值调子模式
    /// </summary>
    public enum NumericMode
    {
        /// <summary>绝对位置运动</summary>
        Absolute,

        /// <summary>相对位移运动</summary>
        Relative
    }

    /// <summary>
    /// 脑立体定向页 ViewModel —— 手术规划与立体定向定位。
    ///
    /// 轴运动控制架构：
    ///   - 全局 ControlMode（粗调/细调/数值调）切换
    ///   - 六轴排列：R(旋转) P(俯仰) Y(平移) X(平移) Z(平移) I(植入)
    ///   - 单轴互斥：同一时刻仅允许一个轴运动
    ///   - Jog 点动：Code-Behind 鼠标事件 → ViewModel 方法 → AxisController SDK
    ///   - 50ms DispatcherTimer 刷新实时位置（复用 IAxisWorkflowService.ReadRealtime）
    /// </summary>
    public partial class StereotaxicViewModel : ObservableObject
    {
        // ===================== 依赖注入 =====================

        private readonly IRobotControlService _robotService;
        private readonly IAxisWorkflowService _workflowService;

        // ===================== 定时器 =====================

        // 50ms 刷新定时器 —— 与 MonitorViewModel 保持一致
        // 未连接时不启动，连接后启动，断开后自动停止
        private readonly DispatcherTimer _refreshTimer;

        // ===================== 轴顺序 =====================

        // 用户在 UI 中指定的六轴排列顺序
        private static readonly RobotAxis[] AxisUiOrder =
        {
            RobotAxis.Axis1,  // R(旋转)   —— 角度轴
            RobotAxis.Axis5,  // P(俯仰)   —— 角度轴
            RobotAxis.Axis2,  // Y(平移)   —— 直线轴
            RobotAxis.Axis3,  // X(平移)   —— 直线轴
            RobotAxis.Axis4,  // Z(平移)   —— 直线轴
            RobotAxis.Axis6,  // I(植入)   —— 直线轴
        };

        // ===================== 轴集合 =====================

        /// <summary>
        /// 六轴控制模型集合，顺序：R, P, Y, X, Z, I
        /// </summary>
        public ObservableCollection<AxisControlModel> Axes { get; } = new ObservableCollection<AxisControlModel>();

        // ===================== 控制模式 =====================

        [ObservableProperty]
        private ControlMode _currentMode = ControlMode.Coarse;

        /// <summary>
        /// 是否为点动模式（粗调或细调），控制 +/- 按钮显示
        /// </summary>
        public bool IsJogMode
        {
            get
            {
                return CurrentMode == ControlMode.Coarse || CurrentMode == ControlMode.Fine;
            }
        }

        /// <summary>
        /// 是否为数值调模式，控制输入框 + 执行按钮显示
        /// </summary>
        public bool IsNumericMode
        {
            get
            {
                return CurrentMode == ControlMode.Numeric;
            }
        }

        /// <summary>
        /// 主速度标签文字（根据当前模式切换）
        /// </summary>
        public string SpeedLabel
        {
            get
            {
                if (CurrentMode == ControlMode.Coarse) return "粗调速度:";
                if (CurrentMode == ControlMode.Fine) return "细调速度:";
                return "数值粗调:";
            }
        }

        /// <summary>
        /// 细调行速度标签文字。
        /// Fine 模式 → "细调速度:"，Numeric 模式 → "数值细调:"
        /// </summary>
        public string FineSpeedLabel
        {
            get
            {
                if (CurrentMode == ControlMode.Numeric) return "数值细调:";
                return "细调速度:";
            }
        }

        /// <summary>
        /// 是否显示粗调速度行（粗调模式或数值调模式下可见）
        /// </summary>
        public bool ShowCoarseSpeeds
        {
            get
            {
                return CurrentMode == ControlMode.Coarse || CurrentMode == ControlMode.Numeric;
            }
        }

        /// <summary>
        /// 是否显示细调速度行（细调模式或数值调模式下可见）
        /// </summary>
        public bool ShowFineSpeeds
        {
            get
            {
                return CurrentMode == ControlMode.Fine || CurrentMode == ControlMode.Numeric;
            }
        }

        // ===================== 数值调子模式 =====================

        [ObservableProperty]
        private NumericMode _currentNumericMode = NumericMode.Absolute;

        // ===================== 速度选择 =====================

        // 粗调速度档位 (mm/s 或 °/s)
        public List<double> CoarseSpeeds { get; } = new List<double>
        {
            5.000, 2.000, 1.000, 0.500, 0.200
        };

        // 细调速度档位 (mm/s 或 °/s)
        public List<double> FineSpeeds { get; } = new List<double>
        {
            0.100, 0.050, 0.010, 0.005, 0.001
        };

        /// <summary>
        /// 当前可选速度列表（根据控制模式切换）
        /// </summary>
        public List<double> CurrentSpeeds
        {
            get
            {
                if (CurrentMode == ControlMode.Numeric)
                {
                    // 数值调：合并粗调 + 细调全部档位
                    List<double> combined = new List<double>();
                    combined.AddRange(CoarseSpeeds);
                    combined.AddRange(FineSpeeds);
                    return combined;
                }
                if (CurrentMode == ControlMode.Coarse)
                {
                    return CoarseSpeeds;
                }
                return FineSpeeds;
            }
        }

        /// <summary>
        /// 当前选中的速度值
        /// </summary>
        [ObservableProperty]
        private double _selectedSpeed;

        /// <summary>
        /// 粗调行选中的速度值（可空，null = 未选中）
        /// </summary>
        [ObservableProperty]
        private double? _selectedCoarseSpeed;

        /// <summary>
        /// 细调行选中的速度值（可空，null = 未选中）
        /// </summary>
        [ObservableProperty]
        private double? _selectedFineSpeed;

        /// <summary>
        /// 粗调速度变化时同步到 SelectedSpeed（仅当有选中值时）
        /// </summary>
        partial void OnSelectedCoarseSpeedChanged(double? value)
        {
            if (value.HasValue)
            {
                SelectedSpeed = value.Value;
            }
        }

        /// <summary>
        /// 细调速度变化时同步到 SelectedSpeed（仅当有选中值时）
        /// </summary>
        partial void OnSelectedFineSpeedChanged(double? value)
        {
            if (value.HasValue)
            {
                SelectedSpeed = value.Value;
            }
        }

        // ===================== 运动互斥锁 =====================

        // 当前正在运动的轴（null = 没有轴在运动）
        private RobotAxis? _activeAxis = null;

        [ObservableProperty]
        private bool _isAnyAxisMoving;

        // ===================== 硬件连接状态 =====================

        [ObservableProperty]
        private bool _isConnected;

        // ===================== 日志 =====================

        public ObservableCollection<string> LogMessages { get; } = new ObservableCollection<string>();

        [ObservableProperty]
        private string _logText = "";

        // ===================== 构造函数 =====================

        public StereotaxicViewModel(IRobotControlService robotService, IAxisWorkflowService workflowService)
        {
            _robotService = robotService;
            _workflowService = workflowService;

            InitializeAxesFromDefinitions();
            SelectedCoarseSpeed = CoarseSpeeds[2]; // 默认粗调速度 1.0
            SelectedFineSpeed = FineSpeeds[2];     // 默认细调速度 0.01
            SelectedSpeed = CoarseSpeeds[2];       // 默认速度 1.0

            // 创建 50ms 刷新定时器（不启动，等连接成功后启动）
            _refreshTimer = new DispatcherTimer();
            _refreshTimer.Interval = TimeSpan.FromMilliseconds(50);
            _refreshTimer.Tick += OnRefreshTimerTick;
        }

        // ===================== 初始化轴集合 =====================

        /// <summary>
        /// 从 IAxisWorkflowService 获取轴定义，按 UI 顺序填充 Axes 集合。
        /// 复用已有的 AxisDefinition 数据（轴名、软限位），不硬编码。
        /// </summary>
        private void InitializeAxesFromDefinitions()
        {
            IReadOnlyList<AxisDefinition> definitions = _workflowService.GetAxisDefinitions();

            for (int i = 0; i < AxisUiOrder.Length; i++)
            {
                RobotAxis axisEnum = AxisUiOrder[i];

                // 从 AxisDefinition 列表中查找匹配的轴
                AxisDefinition? def = null;
                for (int j = 0; j < definitions.Count; j++)
                {
                    if (definitions[j].Axis == axisEnum)
                    {
                        def = definitions[j];
                        break;
                    }
                }

                if (def == null)
                {
                    continue;
                }

                bool isAngle = IsAngleAxis(axisEnum);

                AxisControlModel model = new AxisControlModel
                {
                    AxisEnum = axisEnum,
                    AxisName = def.AxisName,
                    Unit = isAngle ? "°" : "mm",
                    IsAngleAxis = isAngle,
                    SoftLimitMin = def.SoftLimitMin,
                    SoftLimitMax = def.SoftLimitMax,
                };

                Axes.Add(model);
            }
        }

        /// <summary>
        /// 判断是否为角度轴（R=Axis1, P=Axis5）
        /// </summary>
        private static bool IsAngleAxis(RobotAxis axis)
        {
            return axis == RobotAxis.Axis1 || axis == RobotAxis.Axis5;
        }

        // ===================== 轮询控制 =====================

        /// <summary>
        /// 尝试启动位置刷新轮询。
        /// 仅在硬件已连接且定时器未运行时启动。
        /// </summary>
        public void TryStartPolling()
        {
#if SIMULATION
            // 仿真模式：无需硬件连接，直接启动定时器
            if (!_refreshTimer.IsEnabled)
            {
                _refreshTimer.Start();
                IsConnected = true;
                AppendLog(">>> SIM: 仿真模式启动");
            }
#else
            if (_workflowService.IsConnected && !_refreshTimer.IsEnabled)
            {
                _refreshTimer.Start();
                IsConnected = true;
                AppendLog("设备已连接，启动位置刷新");
            }
#endif
        }

        /// <summary>
        /// 停止位置刷新轮询并清除连接状态。
        /// </summary>
        public void StopPolling()
        {
            if (_refreshTimer.IsEnabled)
            {
                _refreshTimer.Stop();
            }
            IsConnected = false;
        }

        // ===================== 50ms 定时器回调 =====================

        /// <summary>
        /// 50ms 刷新回调 —— 与 MonitorViewModel 使用相同的数据源。
        /// 自动检测连接断开：如果硬件断开，停止轮询并停止所有运动。
        /// </summary>
        private void OnRefreshTimerTick(object? sender, EventArgs e)
        {
#if SIMULATION
            // 仿真模式：跳过硬件轮询，仅处理运动完成检测（解锁数值调）
            CheckMovementCompletion();
#else
            // 自监测：硬件断开时自动停止轮询
            if (!_workflowService.IsConnected)
            {
                StopAllJogInternal();
                StopPolling();
                return;
            }

            IsConnected = true;
            RefreshAxisPositions();
            CheckMovementCompletion();
#endif
        }

        // 数据刷新失败计数器（连续失败超过阈值时告警）
        private int _refreshFailCount = 0;
        private const int MaxRefreshFailCount = 10; // 连续失败 10 次(500ms)后告警

        /// <summary>
        /// 从 IAxisWorkflowService 读取所有轴实时位置和运行状态。
        /// 复用已有的 ReadRealtime()，不重复轮询 SDK。
        /// </summary>
        private void RefreshAxisPositions()
        {
            IReadOnlyList<AxisRuntimeState> states = _workflowService.ReadRealtime();
            if (states == null || states.Count == 0)
            {
                _refreshFailCount = _refreshFailCount + 1;
                if (_refreshFailCount == MaxRefreshFailCount)
                {
                    AppendLog("警告: 位置数据刷新连续失败 " + MaxRefreshFailCount + " 次");
                }
                return;
            }

            _refreshFailCount = 0; // 成功刷新，清零计数器

            for (int i = 0; i < states.Count; i++)
            {
                AxisRuntimeState state = states[i];
                AxisControlModel? model = FindAxisModel(state.Axis);
                if (model == null)
                {
                    continue;
                }

                model.CurrentPosition = state.CurrentPosition;
                model.IsRunning = state.IsRunning;
            }
        }

        /// <summary>
        /// 检查活动轴是否已完成运动（IsRunning 从 true 变为 false）。
        /// 用于数值调 MoveAbsolute/MoveRelative 完成后的自动解锁。
        /// </summary>
        private void CheckMovementCompletion()
        {
            if (_activeAxis == null)
            {
                return;
            }

            AxisControlModel? model = FindAxisModel(_activeAxis.Value);
            if (model == null)
            {
                // 活动轴在集合中丢失（理论上不应发生），强制解锁防止系统永久锁定
                AppendLog("警告: 活动轴丢失，强制解锁");
                _activeAxis = null;
                IsAnyAxisMoving = false;
                return;
            }

            // 活动轴已停止 → 清除互斥锁
            if (!model.IsRunning)
            {
                _activeAxis = null;
                IsAnyAxisMoving = false;
            }
        }

        /// <summary>
        /// 根据 RobotAxis 在 Axes 集合中查找对应的 AxisControlModel
        /// </summary>
        private AxisControlModel? FindAxisModel(RobotAxis axis)
        {
            for (int i = 0; i < Axes.Count; i++)
            {
                if (Axes[i].AxisEnum == axis)
                {
                    return Axes[i];
                }
            }
            return null;
        }

        /// <summary>
        /// 根据轴序号（UI 顺序 0=R, 1=P, 2=Y, 3=X, 4=Z, 5=I）获取 RobotAxis
        /// </summary>
        public RobotAxis GetAxisByIndex(int index)
        {
            return AxisUiOrder[index];
        }

        // ===================== 模式切换 =====================

        /// <summary>
        /// 切换到指定控制模式。
        /// 运动互斥：有轴运动时忽略切换请求。
        /// </summary>
        [RelayCommand]
        private void SwitchMode(object parameter)
        {
            if (IsAnyAxisMoving)
            {
                return; // 运动互斥：有轴运动时禁止切换模式
            }

            string? modeStr = parameter as string;
            if (string.IsNullOrEmpty(modeStr))
            {
                return;
            }

            ControlMode newMode;
            if (modeStr == "Coarse")
            {
                newMode = ControlMode.Coarse;
            }
            else if (modeStr == "Fine")
            {
                newMode = ControlMode.Fine;
            }
            else if (modeStr == "Numeric")
            {
                newMode = ControlMode.Numeric;
            }
            else
            {
                return;
            }

            if (CurrentMode == newMode)
            {
                return;
            }

            CurrentMode = newMode;

            // 切换速度列表时，选中一个默认档位
            // 只更新当前模式相关的属性，避免 Set 回调相互覆盖
            if (newMode == ControlMode.Coarse)
            {
                int midIndex = CoarseSpeeds.Count / 2;
                SelectedCoarseSpeed = CoarseSpeeds[midIndex];
            }
            else if (newMode == ControlMode.Fine)
            {
                int midIndex = FineSpeeds.Count / 2;
                SelectedFineSpeed = FineSpeeds[midIndex];
            }
            else // Numeric
            {
                int coarseMid = CoarseSpeeds.Count / 2;
                int fineMid = FineSpeeds.Count / 2;
                SelectedCoarseSpeed = CoarseSpeeds[coarseMid];
                SelectedFineSpeed = FineSpeeds[fineMid];
            }

            // 通知 UI 刷新模式相关的绑定属性
            OnPropertyChanged(nameof(CurrentMode));
            OnPropertyChanged(nameof(IsJogMode));
            OnPropertyChanged(nameof(IsNumericMode));
            OnPropertyChanged(nameof(SpeedLabel));
            OnPropertyChanged(nameof(FineSpeedLabel));
            OnPropertyChanged(nameof(ShowCoarseSpeeds));
            OnPropertyChanged(nameof(ShowFineSpeeds));
            OnPropertyChanged(nameof(CurrentSpeeds));

            AppendLog(string.Format("控制模式: {0}", GetModeDisplayName(newMode)));
        }

        /// <summary>
        /// 切换数值调子模式（绝对/相对）
        /// </summary>
        [RelayCommand]
        private void SwitchNumericMode(object parameter)
        {
            if (IsAnyAxisMoving)
            {
                return; // 运动互斥：与 SwitchMode 保持一致
            }

            string? modeStr = parameter as string;
            if (string.IsNullOrEmpty(modeStr))
            {
                return;
            }

            if (modeStr == "Absolute")
            {
                CurrentNumericMode = NumericMode.Absolute;
            }
            else if (modeStr == "Relative")
            {
                CurrentNumericMode = NumericMode.Relative;
            }
        }

        // ===================== Jog 点动（由 Code-Behind 调用） =====================

        /// <summary>
        /// 启动 Jog 连续点动。
        /// 单轴互斥：如果有其他轴在运动，拒绝操作。
        /// </summary>
        /// <param name="axis">目标轴</param>
        /// <param name="positive">true=正向(JogRight), false=负向(JogLeft)</param>
        public void JogStart(RobotAxis axis, bool positive)
        {
            // 运动互斥检查
            if (_activeAxis != null && _activeAxis != axis)
            {
                return;
            }

#if SIMULATION
            // 仿真模式：跳过 SDK 调用
#else
            if (!_robotService.IsConnected)
            {
                AppendLog("错误: 硬件未连接");
                return;
            }
#endif

            try
            {
#if SIMULATION
                AxisControlModel? simModel = FindAxisModel(axis);
                string simSpeedUnit = (simModel != null && simModel.IsAngleAxis) ? "°/s" : "mm/s";
                AppendLog(string.Format(">>> SIM: SetVelocity({0}, {1}{2})",
                    simModel != null ? simModel.AxisName : axis.ToString(), SelectedSpeed, simSpeedUnit));
                AppendLog(string.Format(">>> SIM: {0}({1})",
                    positive ? "JogRight" : "JogLeft",
                    simModel != null ? simModel.AxisName : axis.ToString()));
                if (simModel != null) simModel.IsRunning = true;
#else
                AxisController controller = _robotService.GetAxis(axis);
                controller.SetVelocity((float)SelectedSpeed);

                if (positive)
                {
                    controller.JogRight();
                }
                else
                {
                    controller.JogLeft();
                }
#endif

                _activeAxis = axis;
                IsAnyAxisMoving = true;

                // 日志
                AxisControlModel? model = FindAxisModel(axis);
                string axisName = model != null ? model.AxisName : axis.ToString();
                string direction = positive ? "+" : "-";
                string speedUnit = (model != null && model.IsAngleAxis) ? "°/s" : "mm/s";
                AppendLog(string.Format("{0} Jog{1}  速度: {2}{3}",
                    axisName, direction, SelectedSpeed, speedUnit));
            }
            catch (Exception ex)
            {
                AppendLog(string.Format("Jog 启动失败: {0}", ex.Message));
            }
        }

        /// <summary>
        /// 停止 Jog 点动。
        /// </summary>
        /// <param name="axis">要停止的轴</param>
        public void JogStop(RobotAxis axis)
        {
            if (_activeAxis != axis)
            {
                return;
            }

            try
            {
#if SIMULATION
                AxisControlModel? simModel = FindAxisModel(axis);
                string simAxisName = simModel != null ? simModel.AxisName : axis.ToString();
                AppendLog(string.Format(">>> SIM: Stop({0})", simAxisName));
                if (simModel != null) simModel.IsRunning = false;
#else
                AxisController controller = _robotService.GetAxis(axis);
                controller.Stop();
#endif

                AxisControlModel? model = FindAxisModel(axis);
                string axisName = model != null ? model.AxisName : axis.ToString();
                AppendLog(string.Format("{0} Stop", axisName));
            }
            catch (Exception ex)
            {
                AppendLog(string.Format("Jog 停止失败: {0}", ex.Message));
            }
            finally
            {
                _activeAxis = null;
                IsAnyAxisMoving = false;
            }
        }

        /// <summary>
        /// 停止所有运动（窗口失焦、页面切换、急停等异常中断）。
        /// 遍历全部 6 轴依次调用 Stop()，确保不遗漏。
        /// </summary>
        public void StopAllJog()
        {
            StopAllJogInternal();
        }

        private void StopAllJogInternal()
        {
            if (_activeAxis == null && !IsAnyAxisMoving)
            {
                return;
            }

            for (int i = 0; i < Axes.Count; i++)
            {
                try
                {
#if SIMULATION
                    AppendLog(string.Format(">>> SIM: Stop({0})", Axes[i].AxisName));
                    Axes[i].IsRunning = false;
#else
                    AxisController controller = _robotService.GetAxis(Axes[i].AxisEnum);
                    controller.Stop();
#endif
                }
                catch (Exception ex)
                {
                    // 单个轴 Stop 失败不阻断其他轴，但记录日志
                    Debug.WriteLine("[StereotaxicVM] StopAllJog 单轴停止失败: " + ex.Message);
                }
            }

            if (_activeAxis != null)
            {
                AppendLog("异常中断: 所有轴已停止");
            }

            _activeAxis = null;
            IsAnyAxisMoving = false;
        }

        // ===================== 数值调 =====================

        /// <summary>
        /// 执行数值调运动。
        /// 输入验证 → 限位检查 → 单轴互斥 → 调用 SDK MoveAbsolute/MoveRelative。
        /// </summary>
        /// <param name="parameter">轴序号（0=R, 1=P, 2=Y, 3=X, 4=Z, 5=I）</param>
        [RelayCommand]
        private void ExecuteNumericMove(object parameter)
        {
            if (IsAnyAxisMoving)
            {
                AppendLog("错误: 有轴正在运动，禁止数值调");
                return;
            }

#if SIMULATION
            // 仿真模式：跳过连接检查
#else
            if (!_robotService.IsConnected)
            {
                AppendLog("错误: 硬件未连接");
                return;
            }
#endif

            int axisIndex;
            if (parameter is int idx)
            {
                axisIndex = idx;
            }
            else if (parameter is string str && int.TryParse(str, out int parsed))
            {
                axisIndex = parsed;
            }
            else
            {
                return;
            }

            if (axisIndex < 0 || axisIndex >= Axes.Count)
            {
                return;
            }

            AxisControlModel model = Axes[axisIndex];
            RobotAxis axis = model.AxisEnum;

            // 输入验证
            string input = model.InputText;
            if (string.IsNullOrWhiteSpace(input))
            {
                model.IsInputValid = false;
                AppendLog(string.Format("⚠ {0} 输入为空", model.AxisName));
                return;
            }

            float value;
            if (!float.TryParse(input, out value))
            {
                model.IsInputValid = false;
                AppendLog(string.Format("⚠ {0} 数值格式错误: {1}", model.AxisName, input));
                return;
            }

            float targetPosition;
            if (CurrentNumericMode == NumericMode.Absolute)
            {
                targetPosition = value;
            }
            else
            {
                // 相对位移：目标 = 当前位置 + 输入值
                targetPosition = model.CurrentPosition + value;
            }

            // 软限位检查（仅绝对位置模式需要严格检查目标位置）
            if (targetPosition < model.SoftLimitMin || targetPosition > model.SoftLimitMax)
            {
                model.IsInputValid = false;
                AppendLog(string.Format("⚠ {0} 超限 [{1}, {2}]", model.AxisName, model.SoftLimitMin, model.SoftLimitMax));
                return;
            }

            // 输入合法，清除错误状态
            model.IsInputValid = true;
            // 输入合法，错误已清除

            try
            {
#if SIMULATION
                string simSpeedUnit = model.IsAngleAxis ? "°/s" : "mm/s";
                string simModeName = CurrentNumericMode == NumericMode.Absolute ? "MoveAbsolute" : "MoveRelative";
                AppendLog(string.Format(">>> SIM: SetVelocity({0}, {1}{2})", model.AxisName, SelectedSpeed, simSpeedUnit));
                AppendLog(string.Format(">>> SIM: {0}({1}, target={2:F3}{3})",
                    simModeName, model.AxisName, targetPosition, model.Unit));
                // 仿真模式：数值调瞬时完成，不设 IsRunning（由 CheckMovementCompletion 立即解锁）
#else
                AxisController controller = _robotService.GetAxis(axis);
                controller.SetVelocity((float)SelectedSpeed);

                if (CurrentNumericMode == NumericMode.Absolute)
                {
                    controller.MoveAbsolute(targetPosition);
                }
                else
                {
                    // MoveRelative 接收相对位移量
                    controller.MoveRelative(value);
                }
#endif

                _activeAxis = axis;
                IsAnyAxisMoving = true;

                string modeLabel = CurrentNumericMode == NumericMode.Absolute ? "绝对" : "相对";
                string speedUnit = model.IsAngleAxis ? "°/s" : "mm/s";
                AppendLog(string.Format("{0} Move{1}  目标: {2:F3}{3}  速度: {4}{5}",
                    model.AxisName, modeLabel, targetPosition, model.Unit, SelectedSpeed, speedUnit));
            }
            catch (Exception ex)
            {
                AppendLog(string.Format("数值调失败: {0}", ex.Message));
                model.IsInputValid = false;
                AppendLog(string.Format("⚠ {0} 运动错误: {1}", model.AxisName, ex.Message));
            }
        }

        // ===================== 数值调停止 =====================

        /// <summary>
        /// 数值调模式下停止指定轴。
        /// 与 JogStop 不同：不检查 _activeAxis 是否匹配，直接对目标轴发送 Stop。
        /// 同时清除互斥锁（如果该轴是当前活动轴）。
        /// </summary>
        /// <param name="parameter">轴序号（0=R, 1=P, 2=Y, 3=X, 4=Z, 5=I）</param>
        [RelayCommand]
        private void StopAxis(object parameter)
        {
            int axisIndex;
            if (parameter is int idx)
            {
                axisIndex = idx;
            }
            else if (parameter is string str && int.TryParse(str, out int parsed))
            {
                axisIndex = parsed;
            }
            else
            {
                return;
            }

            if (axisIndex < 0 || axisIndex >= Axes.Count)
            {
                return;
            }

            RobotAxis axis = GetAxisByIndex(axisIndex);
            AxisControlModel model = Axes[axisIndex];

            try
            {
#if SIMULATION
                AppendLog(string.Format(">>> SIM: Stop({0})", model.AxisName));
                model.IsRunning = false;
#else
                AxisController controller = _robotService.GetAxis(axis);
                controller.Stop();
#endif

                // 如果停止的是当前活动轴，释放互斥锁
                if (_activeAxis == axis)
                {
                    _activeAxis = null;
                    IsAnyAxisMoving = false;
                }

                AppendLog(string.Format("{0} Stop (数值调)", model.AxisName));
            }
            catch (Exception ex)
            {
                AppendLog(string.Format("数值调停止失败: {0}", ex.Message));
            }
        }

        // ===================== 五点记录 =====================

        // 六轴绝对位置快照，按 UI 顺序 (R=0, P=1, Y=2, X=3, Z=4, I=5)
        private float[]? _bregmaPosition;
        private float[]? _lambdaPosition;
        private float[]? _midpointPosition;
        private float[]? _midLeftPosition;
        private float[]? _midRightPosition;

        // 前后囟 DV 校验结果（true=已通过, null=未校验）
        private bool? _bregmaLambdaDvPassed;

        // 左右位点 DV 校验结果（暂用于 SetMidRight 内联判定，后续重定位会读取）
#pragma warning disable CS0414
        private bool? _midLeftRightDvPassed;
#pragma warning restore CS0414

        // BV 差值信息文本
        [ObservableProperty]
        private string _bregmaLambdaDvInfo = "";

        [ObservableProperty]
        private string _midLeftRightDvInfo = "";

        // 各点设置状态
        [ObservableProperty]
        private bool _isBregmaSet;

        [ObservableProperty]
        private bool _isLambdaSet;

        [ObservableProperty]
        private bool _isMidpointSet;

        [ObservableProperty]
        private bool _isMidLeftSet;

        [ObservableProperty]
        private bool _isMidRightSet;

        // 移动至中点按钮是否可用（前后囟 DV ≤ 0.03mm）
        public bool CanMoveToMidpoint
        {
            get
            {
                return IsLambdaSet && _bregmaLambdaDvPassed == true;
            }
        }

        // 移动至左位点按钮是否可用
        public bool CanMoveToMidLeft
        {
            get
            {
                return IsMidpointSet;
            }
        }

        // 移动至右位点按钮是否可用
        public bool CanMoveToMidRight
        {
            get
            {
                return IsMidLeftSet;
            }
        }

        // 调平完成（两处 DV 校验均通过）
        [ObservableProperty]
        private bool _isSkullLeveled;

        /// <summary>
        /// 对六轴当前位置做快照（UI 顺序: R=0, P=1, Y=2, X=3, Z=4, I=5）
        /// </summary>
        private float[] SnapshotPositions()
        {
            float[] pos = new float[6];
            for (int i = 0; i < Axes.Count; i++)
            {
                pos[i] = Axes[i].CurrentPosition;
            }
            return pos;
        }

        /// <summary>
        /// 获取轴 Z (DV) 的位置值（UI 索引 4）
        /// </summary>
        private static float GetZPosition(float[] pos)
        {
            return pos[4];
        }

        // ---- 步骤 1: 设置前囟点 ----

        [RelayCommand]
        private void SetBregma()
        {
            _bregmaPosition = SnapshotPositions();
            IsBregmaSet = true;
            AppendLog(string.Format("前囟点已记录  Z={0:F3}mm", GetZPosition(_bregmaPosition)));
        }

        /// <summary>
        /// 删除前囟点 —— 清除已采集位置并重置后续所有步骤
        /// </summary>
        [RelayCommand]
        private void DeleteBregma()
        {
            _bregmaPosition = null;
            IsBregmaSet = false;
            // 清除所有下游数据
            _lambdaPosition = null;
            IsLambdaSet = false;
            _bregmaLambdaDvPassed = null;
            BregmaLambdaDvInfo = "";
            _midpointPosition = null;
            IsMidpointSet = false;
            _midLeftPosition = null;
            IsMidLeftSet = false;
            _midRightPosition = null;
            IsMidRightSet = false;
            _midLeftRightDvPassed = null;
            MidLeftRightDvInfo = "";
            IsSkullLeveled = false;
            OnPropertyChanged(nameof(CanMoveToMidpoint));
            OnPropertyChanged(nameof(CanMoveToMidLeft));
            OnPropertyChanged(nameof(CanMoveToMidRight));
            AppendLog("前囟点已删除，所有后续点位已清除");
        }

        // ---- 步骤 2: 设置后囟点 + DV 校验 ----

        [RelayCommand]
        private void SetLambda()
        {
            if (_bregmaPosition == null)
            {
                return;
            }

            _lambdaPosition = SnapshotPositions();
            IsLambdaSet = true;

            float bregmaZ = GetZPosition(_bregmaPosition);
            float lambdaZ = GetZPosition(_lambdaPosition);
            float diff = Math.Abs(bregmaZ - lambdaZ);
            string highLow = bregmaZ > lambdaZ ? "前囟高于后囟" : "后囟高于前囟";

            if (diff <= 0.03f)
            {
                _bregmaLambdaDvPassed = true;
                BregmaLambdaDvInfo = string.Format("前后囟DV差值 {0:F3}mm ≤ 0.03mm  ✓  {1}", diff, highLow);
                AppendLog(string.Format("后囟点已记录  前后囟DV差值={0:F3}mm  通过", diff));
            }
            else
            {
                _bregmaLambdaDvPassed = false;
                IsSkullLeveled = false;
                BregmaLambdaDvInfo = string.Format("前后囟DV差值 {0:F3}mm > 0.03mm  ✗  {1}", diff, highLow);
                AppendLog(string.Format("后囟点已记录  前后囟DV差值={0:F3}mm  不通过  {1}", diff, highLow));
            }

            // 通知"移动至中点"按钮状态变化
            OnPropertyChanged(nameof(CanMoveToMidpoint));
        }

        /// <summary>
        /// 删除后囟点 —— 清除已采集位置并重置下游步骤
        /// </summary>
        [RelayCommand]
        private void DeleteLambda()
        {
            _lambdaPosition = null;
            IsLambdaSet = false;
            _bregmaLambdaDvPassed = null;
            BregmaLambdaDvInfo = "";
            // 清除下游数据
            _midpointPosition = null;
            IsMidpointSet = false;
            _midLeftPosition = null;
            IsMidLeftSet = false;
            _midRightPosition = null;
            IsMidRightSet = false;
            _midLeftRightDvPassed = null;
            MidLeftRightDvInfo = "";
            IsSkullLeveled = false;
            OnPropertyChanged(nameof(CanMoveToMidpoint));
            OnPropertyChanged(nameof(CanMoveToMidLeft));
            OnPropertyChanged(nameof(CanMoveToMidRight));
            AppendLog("后囟点已删除，后续点位已清除");
        }

        // ---- 步骤 3: 移动至中点 + 设置中点 ----

        [RelayCommand]
        private void MoveToMidpoint()
        {
            if (_bregmaPosition == null || _lambdaPosition == null)
            {
                return;
            }

            // 计算前后囟中点
            float midY = (_bregmaPosition[2] + _lambdaPosition[2]) / 2.0f;
            float midX = (_bregmaPosition[3] + _lambdaPosition[3]) / 2.0f;
            float midZ = (_bregmaPosition[4] + _lambdaPosition[4]) / 2.0f;

            // 中点上方 5mm
            float targetZ = midZ + 5.0f;

            try
            {
                MoveYxzAxes(midY, midX, targetZ);
                AppendLog(string.Format("自动移动至中点+5mm  Y={0:F3} X={1:F3} Z={2:F3}", midY, midX, targetZ));
            }
            catch (Exception ex)
            {
                AppendLog(string.Format("移动至中点失败: {0}", ex.Message));
            }
        }

        [RelayCommand]
        private void SetMidpoint()
        {
            _midpointPosition = SnapshotPositions();
            IsMidpointSet = true;
            AppendLog(string.Format("中点已记录  Z={0:F3}mm", GetZPosition(_midpointPosition)));

            // 解锁"移动至左位点"
            OnPropertyChanged(nameof(CanMoveToMidLeft));
        }

        /// <summary>
        /// 删除中点 —— 清除已采集位置并重置下游步骤
        /// </summary>
        [RelayCommand]
        private void DeleteMidpoint()
        {
            _midpointPosition = null;
            IsMidpointSet = false;
            // 清除下游数据
            _midLeftPosition = null;
            IsMidLeftSet = false;
            _midRightPosition = null;
            IsMidRightSet = false;
            _midLeftRightDvPassed = null;
            MidLeftRightDvInfo = "";
            IsSkullLeveled = false;
            OnPropertyChanged(nameof(CanMoveToMidLeft));
            OnPropertyChanged(nameof(CanMoveToMidRight));
            AppendLog("中点已删除，左右位点已清除");
        }

        // ---- 步骤 4: 移动至左位点 + 设置左位点 ----

        [RelayCommand]
        private void MoveToMidLeft()
        {
            if (_midpointPosition == null)
            {
                return;
            }

            float leftY = _midpointPosition[2];
            float leftX = _midpointPosition[3] - 2.0f;
            float leftZ = _midpointPosition[4] + 5.0f;

            try
            {
                MoveYxzAxes(leftY, leftX, leftZ);
                AppendLog(string.Format("自动移动至左位点+5mm  Y={0:F3} X={1:F3} Z={2:F3}", leftY, leftX, leftZ));
            }
            catch (Exception ex)
            {
                AppendLog(string.Format("移动至左位点失败: {0}", ex.Message));
            }
        }

        [RelayCommand]
        private void SetMidLeft()
        {
            _midLeftPosition = SnapshotPositions();
            IsMidLeftSet = true;
            AppendLog(string.Format("左位点已记录  Z={0:F3}mm", GetZPosition(_midLeftPosition)));

            // 解锁"移动至右位点"
            OnPropertyChanged(nameof(CanMoveToMidRight));
        }

        /// <summary>
        /// 删除左位点 —— 清除已采集位置并重置下游步骤
        /// </summary>
        [RelayCommand]
        private void DeleteMidLeft()
        {
            _midLeftPosition = null;
            IsMidLeftSet = false;
            // 清除下游数据
            _midRightPosition = null;
            IsMidRightSet = false;
            _midLeftRightDvPassed = null;
            MidLeftRightDvInfo = "";
            IsSkullLeveled = false;
            OnPropertyChanged(nameof(CanMoveToMidRight));
            AppendLog("左位点已删除，右位点已清除");
        }

        // ---- 步骤 5: 移动至右位点 + 设置右位点 + DV 校验 ----

        [RelayCommand]
        private void MoveToMidRight()
        {
            if (_midpointPosition == null)
            {
                return;
            }

            float rightY = _midpointPosition[2];
            float rightX = _midpointPosition[3] + 2.0f;
            float rightZ = _midpointPosition[4] + 5.0f;

            try
            {
                MoveYxzAxes(rightY, rightX, rightZ);
                AppendLog(string.Format("自动移动至右位点+5mm  Y={0:F3} X={1:F3} Z={2:F3}", rightY, rightX, rightZ));
            }
            catch (Exception ex)
            {
                AppendLog(string.Format("移动至右位点失败: {0}", ex.Message));
            }
        }

        [RelayCommand]
        private void SetMidRight()
        {
            _midRightPosition = SnapshotPositions();
            IsMidRightSet = true;

            float leftZ = _midLeftPosition != null ? GetZPosition(_midLeftPosition) : 0f;
            float rightZ = GetZPosition(_midRightPosition);
            float diff = Math.Abs(leftZ - rightZ);
            string highLow = leftZ > rightZ ? "左侧高于右侧" : "右侧高于左侧";

            if (diff <= 0.03f)
            {
                _midLeftRightDvPassed = true;
                MidLeftRightDvInfo = string.Format("左右 DV 差值: {0:F3}mm ≤ 0.03mm  ✓  {1}", diff, highLow);
                AppendLog(string.Format("右位点已记录  左右DV差值={0:F3}mm  通过", diff));

                // 两处 DV 校验均通过 → 调平完成
                if (_bregmaLambdaDvPassed == true)
                {
                    IsSkullLeveled = true;
                    AppendLog("颅骨调平完成！前后囟 + 左右位点 DV 差值均 ≤ 0.03mm");
                }
            }
            else
            {
                _midLeftRightDvPassed = false;
                MidLeftRightDvInfo = string.Format("左右 DV 差值: {0:F3}mm > 0.03mm  ✗  {1}", diff, highLow);
                AppendLog(string.Format("右位点已记录  左右DV差值={0:F3}mm  不通过  {1}", diff, highLow));
            }
        }

        /// <summary>
        /// 删除右位点 —— 清除已采集位置，重置调平状态
        /// </summary>
        [RelayCommand]
        private void DeleteMidRight()
        {
            _midRightPosition = null;
            IsMidRightSet = false;
            _midLeftRightDvPassed = null;
            MidLeftRightDvInfo = "";
            IsSkullLeveled = false;
            AppendLog("右位点已删除");
        }

        // ---- 重定位 ----

        [RelayCommand]
        private void Relocate()
        {
            // TODO: 基于已采集五点重建颅骨坐标系，将针尖导航至目标
            AppendLog("重定位功能（待实现）");
        }

        // ---- 打开配准窗口 ----

        /// <summary>
        /// 打开颅骨配准窗口命令（XAML 绑定用）。
        /// </summary>
        [RelayCommand]
        private void OpenRegistration()
        {
            ShowRegistrationWindow();
        }

        /// <summary>
        /// 打开颅骨配准窗口，展示五点采集结果与坐标系配准数据。
        /// 如果五点未全部采集，以当前已有数据展示。
        /// </summary>
        public void ShowRegistrationWindow()
        {
            RegistrationViewModel regVM = new RegistrationViewModel(
                IsSkullLeveled,
                BregmaLambdaDvInfo,
                MidLeftRightDvInfo,
                _bregmaPosition,
                _lambdaPosition,
                _midpointPosition,
                _midLeftPosition,
                _midRightPosition);

            SAMCS_WPF.Views.RegistrationWindow window = new SAMCS_WPF.Views.RegistrationWindow();
            window.DataContext = regVM;
            window.Owner = System.Windows.Application.Current.MainWindow;
            window.Show();
        }

        /// <summary>
        /// 同时移动 Y/X/Z 三轴到指定绝对位置。
        /// MoveAbsolute 非阻塞，三轴并发运动。
        /// </summary>
        private void MoveYxzAxes(float targetY, float targetX, float targetZ)
        {
            RobotAxis yAxis = AxisUiOrder[2]; // Y
            RobotAxis xAxis = AxisUiOrder[3]; // X
            RobotAxis zAxis = AxisUiOrder[4]; // Z

#if SIMULATION
            AppendLog(string.Format(">>> SIM: SetVelocity+MoveAbsolute Y={0:F3} X={1:F3} Z={2:F3}", targetY, targetX, targetZ));
#else
            AxisController yCtrl = _robotService.GetAxis(yAxis);
            AxisController xCtrl = _robotService.GetAxis(xAxis);
            AxisController zCtrl = _robotService.GetAxis(zAxis);

            yCtrl.SetVelocity(5f);
            xCtrl.SetVelocity(5f);
            zCtrl.SetVelocity(5f);

            yCtrl.MoveAbsolute(targetY);
            xCtrl.MoveAbsolute(targetX);
            zCtrl.MoveAbsolute(targetZ);
#endif

            // 设置互斥锁（以最后一轴为代表），运动完成后自动解锁
            _activeAxis = zAxis;
            IsAnyAxisMoving = true;
        }

        // ===================== 输入验证 =====================

        /// <summary>
        /// 验证数值输入是否在软限位范围内。
        /// 由 View 在输入文本变化时调用（通过绑定或事件）。
        /// </summary>
        public void ValidateNumericInput(int axisIndex)
        {
            if (axisIndex < 0 || axisIndex >= Axes.Count)
            {
                return;
            }

            AxisControlModel model = Axes[axisIndex];
            string input = model.InputText;

            if (string.IsNullOrWhiteSpace(input))
            {
                model.IsInputValid = true;
                // 输入合法，错误已清除
                return;
            }

            float value;
            if (!float.TryParse(input, out value))
            {
                model.IsInputValid = false;
                model.InputError = "格式错误";
                return;
            }

            float target;
            if (CurrentNumericMode == NumericMode.Absolute)
            {
                target = value;
            }
            else
            {
                target = model.CurrentPosition + value;
            }

            if (target < model.SoftLimitMin || target > model.SoftLimitMax)
            {
                model.IsInputValid = false;
                AppendLog(string.Format("⚠ {0} 超限 [{1}, {2}]", model.AxisName, model.SoftLimitMin, model.SoftLimitMax));
            }
            else
            {
                model.IsInputValid = true;
                // 输入合法，错误已清除
            }
        }

        // ===================== 急停 =====================

        /// <summary>
        /// 全局急停 —— 停全部轴、清除互斥锁、注销异步任务。
        /// 复用 IAxisWorkflowService.EmergencyStop() 确保与 MonitorView 一致。
        /// </summary>
        [RelayCommand]
        private void EmergencyStop()
        {
#if SIMULATION
            AppendLog(">>> SIM: EmergencyStop() - 所有轴停止");
            for (int i = 0; i < Axes.Count; i++) Axes[i].IsRunning = false;
#else
            _workflowService.EmergencyStop();
#endif
            _activeAxis = null;
            IsAnyAxisMoving = false;
            AppendLog("!!! 急停 !!!");
        }

        // ===================== 日志 =====================

        /// <summary>
        /// 追加一条带时间戳的日志消息。
        /// 同时更新 ObservableCollection（ListBox 绑定）和纯文本（TextBox 绑定）。
        /// </summary>
        public void AppendLog(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            string entry = string.Format("[{0}] {1}", timestamp, message);
            LogMessages.Add(entry);

            // 更新 TextBox 文本绑定（使用生成的属性，自动触发 PropertyChanged）
            LogText = LogText + entry + Environment.NewLine;
        }

        // ===================== 辅助 =====================

        private static string GetModeDisplayName(ControlMode mode)
        {
            if (mode == ControlMode.Coarse)
            {
                return "粗调";
            }
            if (mode == ControlMode.Fine)
            {
                return "细调";
            }
            return "数值调";
        }
    }
}
