using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MyRobotSDK.Controllers;
using MyRobotSDK.Models;
using System;
using System.Data;
using System.Text;
using System.Windows;

namespace SAMCS.UI.ViewModels
{
    // 注意：必须是 partial 类，以便让 Toolkit 自动生成代码
    public partial class MainViewModel : ObservableObject
    {
        // 维持硬件生命周期的底层主控对象
        private RobotController? _robot;

        // 通信端口
        [ObservableProperty]
        private string _comPort = "COM5";

        // 界面上显示的文本 (使用 [ObservableProperty] 自动生成带通知的 ConnectionStatus 属性)
        [ObservableProperty]
        private string _connectionStatus = "系统就绪，等待连接硬件...";

        // 六个轴的位置信息
        [ObservableProperty]
        private string _axisPositions = "";

        // 连接状态标志，用于控制按钮是否可以点击
        [ObservableProperty]
        [NotifyCanExecuteChangedFor(nameof(ConnectHardwareCommand))]
        [NotifyCanExecuteChangedFor(nameof(DisconnectHardwareCommand))]
        private bool _isConnected;

        // 按钮的可点击条件
        public bool CanConnect => !IsConnected;
        public bool CanDisconnect => IsConnected;

        /// <summary>
        /// 连接硬件命令 (绑定到界面按钮)
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanConnect))]
        private void ConnectHardware()
        {
            try
            {
                ConnectionStatus = "正在建立通信链路...";

                // 1. 开启端口连接
                _robot = RobotController.Connect(ComPort, 115200);

                ConnectionStatus = "正在进行 6 轴典型参数验证...";

                // 预准备 6 个轴的控制器实例
                var controllers = new AxisController[6];
                for (int i = 0; i < 6; i++)
                {
                    controllers[i] = _robot.GetAxis((RobotAxis)(i + 1));
                }

                // --- 第一阶段：读取 6 个轴的核心物理参数 ---
                // 目的：验证硬件是否记住了“细分”、“螺距”等决定单位换算的参数
                StringBuilder paramReport = new StringBuilder();
                paramReport.AppendLine("=== 硬件物理参数同步结果 ===");

                foreach (var axisCtrl in controllers)
                {
                    // 分别读取四项关键参数
                    int div = axisCtrl.GetDivision();
                    float pitch = axisCtrl.GetPitch();
                    ushort accel = axisCtrl.GetAcceleration();
                    ushort decel = axisCtrl.GetDeceleration();

                    paramReport.AppendLine($"轴 {axisCtrl.Axis}: 细分={div}, 螺距={pitch}, 加速={accel}ms, 减速={decel}ms");

                    // 输出到调试窗口，方便在 VS 2026 的输出栏直接比照
                    System.Diagnostics.Debug.WriteLine($"[SDK验证] {axisCtrl.Axis} -> Div:{div}, Pitch:{pitch}, Acc:{accel}, Dec:{decel}");
                }

                // --- 第二阶段：在所有参数确认后，获取 6 轴实时坐标 ---
                ConnectionStatus = "参数同步完成，正在获取 6 轴实时坐标...";
                paramReport.AppendLine("\n=== 实时坐标获取结果 ===");

                foreach (var axisCtrl in controllers)
                {
                    float currentPos = axisCtrl.GetPosition();
                    paramReport.AppendLine($"轴 {axisCtrl.Axis}: 当前位置 = {currentPos}");

                    System.Diagnostics.Debug.WriteLine($"[SDK验证] {axisCtrl.Axis} -> CurrentPosition: {currentPos}");
                }

                // 3. 验证通过
                IsConnected = true;
                ConnectionStatus = $"✅ SDK 验证成功：已同步 6 轴数据 (版本: {RobotController.SdkVersion})";

                // 弹出汇总报告，方便你直接与既定结果做比照
                MessageBox.Show(paramReport.ToString(), "SAMCS 硬件验证报告", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                // 异常处理：释放资源并报错
                _robot?.Dispose();
                _robot = null;
                IsConnected = false;
                ConnectionStatus = "❌ 硬件验证流程中断";

                MessageBox.Show($"SDK 验证失败！\n\n异常信息：{ex.Message}\n\n请检查硬件是否上电及通信链路。",
                                "验证异常", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// 断开连接命令 (绑定到界面按钮)
        /// </summary>
        [RelayCommand(CanExecute = nameof(CanDisconnect))]
        private void DisconnectHardware()
        {
            _robot?.Dispose();
            _robot = null;
            IsConnected = false;
            AxisPositions = "";
            ConnectionStatus = "⚠️ 硬件已安全断开";
        }
    }
}
