using System;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Threading.Tasks;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyRobotSDK.Models;
using SAMCS_WPF.Models;
using SAMCS_WPF.Services;

namespace SAMCS_WPF.ViewModels
{
    /// <summary>
    /// 状态监控页 ViewModel —— 负责串口连接、六轴参数实时刷新与运动控制指令下发。
    /// </summary>
    public partial class MonitorViewModel : ObservableObject
    {
        // ===================== 依赖注入字段 =====================

        private readonly IRobotControlService _robotService;
        private readonly DispatcherTimer _refreshTimer;
        private readonly DispatcherTimer _configTimer;

        // ===================== ObservableProperty =====================

        [ObservableProperty]
        private ObservableCollection<string> _availablePorts = [];

        [ObservableProperty]
        private string? _selectedPort;

        [ObservableProperty]
        private bool _isConnected;

        [ObservableProperty]
        private ObservableCollection<AxisUIModel> _axes = [];

        // ===================== Commands =====================

        /// <summary>
        /// 串口连接/断开切换
        /// </summary>
        [RelayCommand]
        private void ToggleConnection()
        {
            if (IsConnected)
            {
                DisconnectSystem();
            }
            else
            {
                try
                {
                    // 1. 建立物理连接
                    _robotService.Connect(SelectedPort!, 115200);

                    // 2. 电机型号握手校验
                    string[] expectedModels =
                    [
                        "IM42ET_485", "IM42ET_485", "IM42ET_485",
                        "IM42ET_485", "IM35ET_485", "IM28ET_485"
                    ];

                    for (int i = 0; i < Axes.Count; i++)
                    {
                        var axisUI = Axes[i];
                        var axisEnum = (RobotAxis)(i + 1);
                        string realModel = _robotService.GetAxis(axisEnum).GetMotorModel();
                        axisUI.MotorModel = realModel;

                        if (realModel != expectedModels[i])
                        {
                            _robotService.Dispose();
                            System.Windows.MessageBox.Show(
                                $"设备校验失败！\n" +
                                $"轴体 {axisUI.AxisName} 预设为 {expectedModels[i]}，但实际读取到 {realModel}。\n" +
                                $"请检查串口选择是否正确或硬件连线是否匹配。",
                                "连接终止",
                                System.Windows.MessageBoxButton.OK,
                                System.Windows.MessageBoxImage.Error);
                            IsConnected = false;
                            return;
                        }
                    }
                    // 3. 校验通过
                    IsConnected = true;
                    // 4. 校验通过，立即读一次配置 + 启动两个轮询定时器
                    UpdateAxisConfiguration();
                    _refreshTimer.Start();
                    _configTimer.Start();
                    IsConnected = true;
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show($"连接异常: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 六轴同步归位 —— 所有轴同时启动，统一等待全部停止
        /// </summary>
        [RelayCommand]
        private async Task HomeAllAxesAsync()
        {
            if (!IsConnected)
            {
                System.Windows.MessageBox.Show("请先连接设备！");
                return;
            }

            // 预设归位坐标: R, Y, X, Z, P, I
            float[] homePositions = [0f, 100f, 100f, 32.5f, 45f, 10f];

            try
            {
                // 第一步：统一设定所有轴的速度
                for (int i = 0; i < Axes.Count; i++)
                {
                    var axisEnum = (RobotAxis)int.Parse(Axes[i].AxisId);
                    _robotService.GetAxis(axisEnum).SetVelocity(5f);
                }

                // 第二步：对所有轴同时下发运动指令（不等待）
                var axisEnums = new RobotAxis[Axes.Count];
                for (int i = 0; i < Axes.Count; i++)
                {
                    axisEnums[i] = (RobotAxis)int.Parse(Axes[i].AxisId);
                    _robotService.GetAxis(axisEnums[i]).MoveAbsolute(homePositions[i]);
                }

                // 第三步：统一轮询，直到所有轴全部停止
                while (true)
                {
                    bool anyRunning = false;
                    for (int i = 0; i < axisEnums.Length; i++)
                    {
                        if (_robotService.GetAxis(axisEnums[i]).IsRunning())
                        {
                            anyRunning = true;
                            break;
                        }
                    }

                    if (!anyRunning) break;
                    await Task.Delay(50);
                }

                System.Windows.MessageBox.Show(
                    "六轴同步归位完成！", "提示",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"归零异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 六轴限位往返测试 —— 各轴依次：上限位 → 下限位 → 默认位置
        /// </summary>
        [RelayCommand]
        private async Task TestAllAxesAsync()
        {
            if (!IsConnected)
            {
                System.Windows.MessageBox.Show("请先连接设备！");
                return;
            }

            // 默认归位坐标: R, Y, X, Z, P, I
            float[] homePositions = [0f, 100f, 100f, 32.5f, 45f, 10f];

            try
            {
                // 统一设定速度
                for (int i = 0; i < Axes.Count; i++)
                {
                    var axisEnum = (RobotAxis)int.Parse(Axes[i].AxisId);
                    _robotService.GetAxis(axisEnum).SetVelocity(5f);
                }

                for (int i = 0; i < Axes.Count; i++)
                {
                    var axisUI = Axes[i];
                    var axisEnum = (RobotAxis)int.Parse(axisUI.AxisId);
                    var controller = _robotService.GetAxis(axisEnum);

                    // 往：运动到上限位
                    controller.MoveAbsolute(axisUI.SoftLimitMax);
                    while (controller.IsRunning()) await Task.Delay(50);
                    await Task.Delay(3000);

                    // 返：运动到下限位
                    controller.MoveAbsolute(axisUI.SoftLimitMin);
                    while (controller.IsRunning()) await Task.Delay(50);
                    await Task.Delay(3000);

                    // 归：回到默认位置
                    controller.MoveAbsolute(homePositions[i]);
                    while (controller.IsRunning()) await Task.Delay(50);
                    await Task.Delay(3000);
                }

                System.Windows.MessageBox.Show(
                    "六轴限位往返测试完成！", "提示",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"测试异常: {ex.Message}");
            }
        }

        /// <summary>
        /// 紧急停止 —— 向全部轴瞬间群发 Stop 指令
        /// </summary>
        [RelayCommand]
        private void EmergencyStop()
        {
            if (!IsConnected) return;

            try
            {
                foreach (var axisUI in Axes)
                {
                    var axisEnum = (RobotAxis)int.Parse(axisUI.AxisId);
                    _robotService.GetAxis(axisEnum).Stop();
                }
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"急停下发失败: {ex.Message}");
            }
        }

        // ===================== 构造函数 =====================

        public MonitorViewModel(IRobotControlService robotService)
        {
            _robotService = robotService;

            InitializeConnectionPanel();
            InitializeAxes();

            _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            _refreshTimer.Tick += (_, _) => UpdateRealtimeAxisData();

            _configTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
            _configTimer.Tick += (_, _) => UpdateAxisConfiguration();
        }

        // ===================== 私有初始化方法 =====================

        /// <summary>
        /// 扫描系统可用串口列表，默认选中第一个
        /// </summary>
        private void InitializeConnectionPanel()
        {
            string[] ports = SerialPort.GetPortNames();
            foreach (var port in ports)
                AvailablePorts.Add(port);

            if (AvailablePorts.Count > 0)
                SelectedPort = AvailablePorts[0];
        }

        /// <summary>
        /// 按产品规格初始化六轴 UI 模型
        /// </summary>
        private void InitializeAxes()
        {
            Axes.Clear();

            Axes.Add(new AxisUIModel
            {
                AxisName = "R(旋转)", AxisId = "01", MotorModel = "IM42ET_485",
                PosUnit = "°", VelUnit = "°/s",
                SoftLimitMin = -180, SoftLimitMax = 180, CurrentPosition = 0
            });
            Axes.Add(new AxisUIModel
            {
                AxisName = "Y(平移)", AxisId = "02", MotorModel = "IM42ET_485",
                PosUnit = "mm", VelUnit = "mm/s",
                SoftLimitMin = 0, SoftLimitMax = 200, CurrentPosition = 0
            });
            Axes.Add(new AxisUIModel
            {
                AxisName = "X(平移)", AxisId = "03", MotorModel = "IM42ET_485",
                PosUnit = "mm", VelUnit = "mm/s",
                SoftLimitMin = 0, SoftLimitMax = 200, CurrentPosition = 0
            });
            Axes.Add(new AxisUIModel
            {
                AxisName = "Z(平移)", AxisId = "04", MotorModel = "IM42ET_485",
                PosUnit = "mm", VelUnit = "mm/s",
                SoftLimitMin = 0, SoftLimitMax = 75, CurrentPosition = 0
            });
            Axes.Add(new AxisUIModel
            {
                AxisName = "P(俯仰)", AxisId = "05", MotorModel = "IM35ET_485",
                PosUnit = "°", VelUnit = "°/s",
                SoftLimitMin = 0, SoftLimitMax = 90, CurrentPosition = 0
            });
            Axes.Add(new AxisUIModel
            {
                AxisName = "I(植入)", AxisId = "06", MotorModel = "IM28ET_485",
                PosUnit = "mm", VelUnit = "mm/s",
                SoftLimitMin = 0, SoftLimitMax = 50, CurrentPosition = 0
            });
        }

        // ===================== 硬件数据刷新 =====================

        /// <summary>
        /// 高频刷新（100ms） —— 仅读取位置与速度，避免占用通信带宽
        /// </summary>
        private void UpdateRealtimeAxisData()
        {
            if (!IsConnected) return;

            try
            {
                foreach (var axisUI in Axes)
                {
                    var axisEnum = (RobotAxis)int.Parse(axisUI.AxisId);
                    var controller = _robotService.GetAxis(axisEnum);

                    axisUI.CurrentPosition = controller.GetPosition();
                    axisUI.Velocity = controller.GetVelocity();
                    axisUI.IsRunning = controller.IsRunning();
                }
            }
            catch (Exception)
            {
                // 瞬时通信异常静默忽略，下一轮刷新自动重试
            }
        }

        /// <summary>
        /// 低频配置刷新（60s） —— 读取运动参数，连接时立即执行一次 + 之后每 60s 轮询
        /// </summary>
        private void UpdateAxisConfiguration()
        {
            if (!IsConnected) return;

            try
            {
                foreach (var axisUI in Axes)
                {
                    var axisEnum = (RobotAxis)int.Parse(axisUI.AxisId);
                    var controller = _robotService.GetAxis(axisEnum);

                    axisUI.Acceleration = controller.GetAcceleration();
                    axisUI.Deceleration = controller.GetDeceleration();
                    axisUI.Pitch = controller.GetPitch();
                    axisUI.Division = controller.GetDivision();
                }
            }
            catch (Exception)
            {
                // 配置读取失败不影响实时刷新
            }
        }

        // ===================== 断开连接 =====================

        /// <summary>
        /// 断开硬件连接并停止定时刷新
        /// </summary>
        private void DisconnectSystem()
        {
            _refreshTimer.Stop();
            _configTimer.Stop();
            _robotService.Dispose();
            IsConnected = false;

            foreach (var a in Axes)
                a.MotorModel = "Unknown";
        }
    }
}
