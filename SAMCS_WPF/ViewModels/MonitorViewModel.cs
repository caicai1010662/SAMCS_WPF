using System;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SAMCS_WPF.Models;
using SAMCS_WPF.Services;

namespace SAMCS_WPF.ViewModels
{
    /// <summary>
    /// ===== 白话文说明 =====
    /// 这个类是"状态监控页面"的大脑。
    /// 它的工作只有三件事：
    ///   1. 存放 UI 需要的数据（串口列表、轴参数、连接状态）
    ///   2. 响应用户点击按钮（连接、归位、测试、急停）
    ///   3. 用定时器每隔一段时间从硬件读取最新数据，刷新屏幕
    ///
    /// 它不直接操作硬件。所有硬件操作都委托给 AxisWorkflowService 去执行。
    /// 这样做的原因：把"怎么控制电机"的复杂代码集中在 Service 层，
    /// ViewModel 保持简单，只做 UI 相关的事情。
    ///
    /// ===== 数据流向 =====
    /// 用户点击按钮 → Command 方法 → AxisWorkflowService → 硬件 SDK → 电机
    /// 定时器触发 → AxisWorkflowService 读数据 → ViewModel 更新属性 → UI 自动刷新
    /// </summary>
    public partial class MonitorViewModel : ObservableObject
    {
        // ===================== 依赖注入字段 =====================

        /// <summary>
        /// 轴运动工作流服务。
        /// 这个字段由 DI 容器在创建 MonitorViewModel 时自动传入，
        /// 不需要手动 new。DI 容器会根据 App.xaml.cs 中的注册找到对应的实现类。
        /// </summary>
        private readonly IAxisWorkflowService _workflowService;

        /// <summary>
        /// 高频刷新定时器，每 100ms 触发一次。
        /// 用 DispatcherTimer 而不是 System.Timers.Timer 的原因：
        /// DispatcherTimer 的回调在 UI 线程上执行，可以直接修改 UI 绑定的属性，
        /// 不需要 Dispatcher.Invoke，代码更简单，也更容易调试。
        /// </summary>
        private readonly DispatcherTimer _refreshTimer;

        /// <summary>
        /// 低频配置刷新定时器，每 60 秒触发一次。
        /// 读取加速度、减速度、螺距、细分这些不常变化的参数。
        /// 60 秒间隔足够，因为这些参数只在系统初始化时设置，运行中不会变。
        /// </summary>
        private readonly DispatcherTimer _configTimer;

        // ===================== ObservableProperty =====================
        //
        // 白话文说明：[ObservableProperty] 是 CommunityToolkit.Mvvm 提供的功能。
        // 你写一个 private 字段（比如 _selectedPort），
        // 工具会自动生成一个 public 属性（比如 SelectedPort），
        // 并且当属性值变化时自动通知 UI 刷新。
        // 这比手写 get/set + OnPropertyChanged 省很多代码。
        //

        [ObservableProperty]
        private ObservableCollection<string> _availablePorts = new ObservableCollection<string>();

        /// <summary>
        /// 用户在下拉框中选中的串口号。
        /// 初始值是空字符串，等 InitializeConnectionPanel 扫描完串口后自动设为第一个可用串口。
        /// </summary>
        [ObservableProperty]
        private string _selectedPort = "";

        /// <summary>
        /// 当前是否已连接硬件。
        /// true = 已连接，UI 上按钮文字变为"断开连接"，背景变红。
        /// false = 未连接。
        /// </summary>
        [ObservableProperty]
        private bool _isConnected;

        /// <summary>
        /// 【仅前端展示】波特率选项列表，实际通信固定使用 115200。
        /// 这些值只显示在下拉框中给用户看，不会传给 Connect()。
        /// 为什么这样做：硬件固件固定 115200，前端放一个下拉框是为了演示和界面完整性。
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<int> _baudRates = CreateBaudRateList();

        /// <summary>
        /// 【仅前端展示】用户在下拉框中选择的波特率值，默认 115200。
        /// 改变这个值不会影响实际通信波特率。
        /// </summary>
        [ObservableProperty]
        private int _selectedBaudRate = 115200;

        /// <summary>
        /// 六轴 UI 数据集合，绑定到表格的 ItemsControl。
        /// 集合中第 0 个元素是 R 轴，第 1 个是 Y 轴，依次类推。
        /// 这个顺序不能乱，因为右侧数据表格按这个顺序显示列。
        /// </summary>
        [ObservableProperty]
        private ObservableCollection<AxisUIModel> _axes = new ObservableCollection<AxisUIModel>();

        /// <summary>
        /// 创建波特率列表（因为不能用 [] 集合表达式，用传统方式初始化）
        /// </summary>
        private static ObservableCollection<int> CreateBaudRateList()
        {
            ObservableCollection<int> list = new ObservableCollection<int>();
            list.Add(9600);
            list.Add(19200);
            list.Add(38400);
            list.Add(57600);
            list.Add(115200);
            return list;
        }

        // ===================== Commands =====================
        //
        // 白话文说明：[RelayCommand] 也是 CommunityToolkit.Mvvm 提供的。
        // 它会自动生成一个名为 XxxCommand 的公开属性，
        // WPF 按钮通过 Binding 找到这个 Command 并绑定点击事件。
        //

        /// <summary>
        /// 串口连接/断开切换按钮。
        /// 点击后：
        ///   如果已连接 → 断开连接
        ///   如果未连接 → 建立连接 + 校验电机型号 + 启动定时刷新
        ///
        /// 连接的实际工作委托给 AxisWorkflowService.ConnectAndValidate()。
        /// ViewModel 只做：调服务 → 检查结果 → 更新状态 → 启停定时器。
        /// </summary>
        [RelayCommand]
        private void ToggleConnection()
        {
            if (IsConnected)
            {
                // 当前已连接 → 执行断开流程
                DisconnectSystem();
            }
            else
            {
                // 当前未连接 → 执行连接流程
                try
                {
                    // 安全检查：确保用户已选择串口（避免 SelectedPort 为空）
                    string port = SelectedPort;
                    if (port == null || port.Length == 0)
                    {
                        System.Windows.MessageBox.Show("请先选择通讯串口！");
                        return;
                    }

                    // 委托工作流服务执行物理连接与型号校验
                    // 所有连接细节（串口参数、校验逻辑）都在服务内部
                    _workflowService.ConnectAndValidate(port);

                    // 连接完成后检查服务是否真正连上了
                    // 如果型号校验失败，服务内部会断开连接，这里会检测到 false
                    if (!_workflowService.IsConnected)
                    {
                        IsConnected = false;
                        return;
                    }

                    // 校验通过：立即读一次配置参数 + 启动两个轮询定时器
                    UpdateAxisConfiguration();
                    _refreshTimer.Start();
                    _configTimer.Start();
                    IsConnected = true;
                }
                catch (Exception ex)
                {
                    // 任何未预料的异常都弹窗提示，不让程序悄悄崩溃
                    System.Windows.MessageBox.Show("连接异常: " + ex.Message);
                }
            }
        }

        /// <summary>
        /// 六轴同步归位按钮。
        /// ViewModel 只做两件事：前置检查 + 委托服务执行。
        /// 具体的运动步骤（设速→下发→等待）全在 AxisWorkflowService.HomeAllAxesAsync() 里。
        ///
        /// 为什么用 async/await：归位过程需要等待电机停止（轮询 50ms），
        /// 如果用同步方法会卡死 UI。async/await 让等待期间 UI 仍然可以响应。
        /// </summary>
        [RelayCommand]
        private async Task HomeAllAxesAsync()
        {
            if (!IsConnected)
            {
                System.Windows.MessageBox.Show("请先连接设备！");
                return;
            }

            try
            {
                // 所有运动逻辑委托给服务层执行
                await _workflowService.HomeAllAxesAsync();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("归零异常: " + ex.Message);
            }
        }

        /// <summary>
        /// 六轴限位往返测试按钮。
        /// 每个轴依次执行：上限位 → 下限位 → 默认位置 三段运动。
        /// 全部逻辑在 AxisWorkflowService.TestAllAxesAsync() 里。
        /// </summary>
        [RelayCommand]
        private async Task TestAllAxesAsync()
        {
            if (!IsConnected)
            {
                System.Windows.MessageBox.Show("请先连接设备！");
                return;
            }

            try
            {
                // 所有测试逻辑委托给服务层执行
                await _workflowService.TestAllAxesAsync();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("测试异常: " + ex.Message);
            }
        }

        /// <summary>
        /// 紧急停止按钮。
        /// 向全部六个轴同时发送 Stop 指令。
        /// 不等待、不轮询，直接群发。
        /// </summary>
        [RelayCommand]
        private void EmergencyStop()
        {
            if (!IsConnected)
            {
                return;
            }

            try
            {
                _workflowService.EmergencyStop();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("急停下发失败: " + ex.Message);
            }
        }

        // ===================== 构造函数 =====================

        /// <summary>
        /// 构造函数 —— DI 容器创建 MonitorViewModel 时调用。
        /// 做了四件事：
        ///   1. 保存 workflowService 引用
        ///   2. 扫描可用串口列表
        ///   3. 从服务获取轴定义，初始化 UI 绑定的轴集合
        ///   4. 创建两个定时器（不立即启动，等连接成功后才启动）
        /// </summary>
        public MonitorViewModel(IAxisWorkflowService workflowService)
        {
            _workflowService = workflowService;

            // 1. 扫描系统串口
            InitializeConnectionPanel();

            // 2. 从服务获取六轴产品定义，创建 UI 绑定的 AxisUIModel 列表
            InitializeAxesFromDefinitions();

            // 3. 创建 100ms 高频刷新定时器（不立即启动）
            _refreshTimer = new DispatcherTimer();
            _refreshTimer.Interval = TimeSpan.FromMilliseconds(100);
            _refreshTimer.Tick += OnRefreshTimerTick;

            // 4. 创建 60s 低频配置刷新定时器（不立即启动）
            _configTimer = new DispatcherTimer();
            _configTimer.Interval = TimeSpan.FromSeconds(60);
            _configTimer.Tick += OnConfigTimerTick;
        }

        // ===================== 定时器回调 =====================

        /// <summary>
        /// 100ms 定时器回调 —— 读取位置、速度、运行状态
        /// </summary>
        private void OnRefreshTimerTick(object? sender, EventArgs e)
        {
            UpdateRealtimeAxisData();
        }

        /// <summary>
        /// 60s 定时器回调 —— 读取加速度、减速度、螺距、细分
        /// </summary>
        private void OnConfigTimerTick(object? sender, EventArgs e)
        {
            UpdateAxisConfiguration();
        }

        // ===================== 私有初始化方法 =====================

        /// <summary>
        /// 扫描系统可用串口列表，默认选中第一个。
        /// 调用的是 .NET 自带的 SerialPort.GetPortNames()，
        /// 返回类似 ["COM1", "COM3", "COM5"] 的数组。
        /// 这是纯 UI 层操作，不涉及硬件通信。
        /// </summary>
        private void InitializeConnectionPanel()
        {
            string[] ports = SerialPort.GetPortNames();

            // 使用传统 for 循环，方便调试时 F10 单步跟踪
            for (int i = 0; i < ports.Length; i++)
            {
                AvailablePorts.Add(ports[i]);
            }

            // 如果有可用串口，默认选第一个
            if (AvailablePorts.Count > 0)
            {
                SelectedPort = AvailablePorts[0];
            }
        }

        /// <summary>
        /// 从 AxisWorkflowService 获取六轴静态定义，构建 UI 绑定的 AxisUIModel 列表。
        ///
        /// 为什么从服务获取而不是硬编码：
        /// 轴的静态参数（限位值、归位坐标等）属于产品规格，
        /// 应该由服务层统一管理。ViewModel 只负责把数据转成 UI 能绑定的格式。
        ///
        /// AxisUIModel 和 AxisDefinition 的关系：
        ///   AxisDefinition 是纯数据（存在服务层）
        ///   AxisUIModel 是 UI 绑定模型（存在 ViewModel 层，继承了 ObservableObject）
        ///   初始化时把 Definition 的值拷贝到 UIModel 中
        /// </summary>
        private void InitializeAxesFromDefinitions()
        {
            Axes.Clear();

            IReadOnlyList<AxisDefinition> definitions = _workflowService.GetAxisDefinitions();
            for (int i = 0; i < definitions.Count; i++)
            {
                AxisDefinition def = definitions[i];

                AxisUIModel axisUI = new AxisUIModel();
                axisUI.AxisName = def.AxisName;
                axisUI.AxisId = def.AxisId;
                axisUI.MotorModel = def.ExpectedMotorModel;
                axisUI.PosUnit = def.PosUnit;
                axisUI.VelUnit = def.VelUnit;
                axisUI.SoftLimitMin = def.SoftLimitMin;
                axisUI.SoftLimitMax = def.SoftLimitMax;
                axisUI.SoftVelocityLimit = def.SoftVelocityLimit;
                axisUI.CurrentPosition = 0;

                Axes.Add(axisUI);
            }
        }

        // ===================== 硬件数据刷新 =====================

        /// <summary>
        /// 高频刷新（每 100ms 执行一次）。
        /// 从工作流服务读取六轴实时数据，然后逐轴更新到 UI 绑定模型。
        ///
        /// 数据路径：
        ///   硬件 SDK → AxisWorkflowService.ReadRealtime() → AxisRuntimeState 列表
        ///   → 逐字段拷贝到 AxisUIModel → WPF 自动刷新 UI
        ///
        /// 为什么只读三个值（位置、速度、运行状态）：
        ///   串口通信带宽有限，100ms 一次已经很频繁。
        ///   只读必要的数据，不让串口堵塞。
        /// </summary>
        private void UpdateRealtimeAxisData()
        {
            if (!IsConnected)
            {
                return;
            }

            // 从服务获取六轴实时数据快照
            IReadOnlyList<AxisRuntimeState> states = _workflowService.ReadRealtime();

            // 通信异常时服务返回空列表，跳过本轮刷新（保留上次数据）
            if (states.Count == 0)
            {
                return;
            }

            // 逐轴把服务返回的纯数据拷贝到 UI 绑定模型
            for (int i = 0; i < states.Count; i++)
            {
                AxisUIModel axisUI = Axes[i];
                AxisRuntimeState state = states[i];

                axisUI.CurrentPosition = state.CurrentPosition;
                axisUI.Velocity = state.Velocity;
                axisUI.IsRunning = state.IsRunning;
            }
        }

        /// <summary>
        /// 低频配置刷新（每 60 秒执行一次，连接成功时也会立即执行一次）。
        /// 读取加速度、减速度、螺距、细分等运动参数。
        ///
        /// 为什么 60 秒一次而不是 100ms：
        ///   这些参数在系统运行中几乎不会变化，不需要高频读取。
        ///   减少串口通信负担。
        /// </summary>
        private void UpdateAxisConfiguration()
        {
            if (!IsConnected)
            {
                return;
            }

            // 从服务获取六轴配置数据快照
            IReadOnlyList<AxisRuntimeState> states = _workflowService.ReadConfiguration();

            // 通信异常时跳过
            if (states.Count == 0)
            {
                return;
            }

            // 逐轴拷贝配置数据到 UI 绑定模型
            for (int i = 0; i < states.Count; i++)
            {
                AxisUIModel axisUI = Axes[i];
                AxisRuntimeState state = states[i];

                axisUI.Acceleration = state.Acceleration;
                axisUI.Deceleration = state.Deceleration;
                axisUI.Pitch = state.Pitch;
                axisUI.Division = state.Division;
            }
        }

        // ===================== 断开连接 =====================

        /// <summary>
        /// 断开硬件连接的完整流程：
        ///   1. 停掉两个刷新定时器（断开后不需要再读数据了）
        ///   2. 委托服务断开硬件连接、释放 SDK 资源
        ///   3. 更新 UI 连接状态为 false
        ///   4. 把所有轴的电机型号显示重置为 "Unknown"
        ///
        /// 顺序不能乱：必须先停定时器再断开连接，
        /// 否则定时器可能在断开后触发，导致通信异常。
        /// </summary>
        private void DisconnectSystem()
        {
            // 步骤一：停止定时刷新
            _refreshTimer.Stop();
            _configTimer.Stop();

            // 步骤二：断开硬件连接
            _workflowService.Disconnect();

            // 步骤三：更新 UI 状态
            IsConnected = false;

            // 步骤四：重置电机型号显示
            for (int i = 0; i < Axes.Count; i++)
            {
                Axes[i].MotorModel = "Unknown";
            }
        }
    }
}
