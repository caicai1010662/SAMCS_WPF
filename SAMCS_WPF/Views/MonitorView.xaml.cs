using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Threading;
using Microsoft.Web.WebView2.Core;
using SAMCS_WPF.ViewModels;

namespace SAMCS_WPF.Views
{
    /// <summary>
    /// 状态监控页 View —— 左侧嵌入 WebView2 显示 3D 六轴机器人模型。
    ///
    /// 架构（最短路径）：
    ///   WebView2 加载 index.html（极简 Three.js + URDFLoader），
    ///   页面自动加载 URDF 和 STL 网格，暴露 window.updateJoint / window.getJointNames。
    ///   C# 侧用 100ms DispatcherTimer 从 MonitorViewModel 读真实位置，
    ///   转换单位后调 ExecuteScriptAsync("updateJoint('joint2', rad)") 推给 JS。
    ///
    /// 无 robot_viewer、无 CSS 隐藏、无 JS 注入、无中间抽象层。
    /// </summary>
    public partial class MonitorView : UserControl
    {
        // ===================== 常量 =====================

        private const string RobotViewerHost = "robot-viewer.local";
        private const string ModelHost = "samcs-model.local";
        private const string DistPath = @"3D\Runtime";
        private const string ModelPath = @"3D\Model";

        // ===================== 状态字段 =====================

        public bool IsWebViewReady { get; private set; }

        /// <summary>
        /// 100ms 同步定时器 —— 将真实电机位置实时推送到 3D 模型。
        /// 为什么用 DispatcherTimer：回调在 UI 线程，可以直接读 DataContext 和调 WebView2。
        /// </summary>
        private DispatcherTimer _syncTimer = null!;

        /// <summary>
        /// 同步计数器，用于降低日志输出频率（每 30 次 ≈ 每 3 秒输出一条）
        /// </summary>
        private int _syncTickCount = 0;

        // ===================== 构造函数 =====================

        public MonitorView()
        {
            InitializeComponent();
            // 初始不可见，等页面就绪后再显示，消除白屏闪烁
            WebView3D.Visibility = System.Windows.Visibility.Hidden;
            _ = InitializeWebViewAsync();
        }

        // ===================== WebView2 初始化 =====================

        /// <summary>
        /// 初始化 WebView2 环境并导航到 3D 页面。
        /// 虚拟主机映射到 exe 所在目录下的 3D 子目录：
        ///   robot-viewer.local → 3D/Runtime/（index.html + JS 文件）
        ///   samcs-model.local  → 3D/Model/（SAMCS.urdf + STL 网格）
        /// 3D 文件由 csproj 的 Content + CopyToOutputDirectory 跟随输出。
        /// </summary>
        private async Task InitializeWebViewAsync()
        {
            try
            {
                string baseDir = AppContext.BaseDirectory;
                string dist = Path.GetFullPath(Path.Combine(baseDir, DistPath))
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string model = Path.GetFullPath(Path.Combine(baseDir, ModelPath))
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                string userData = Path.Combine(Path.GetTempPath(), "SAMCS_WebView3D");
                Directory.CreateDirectory(userData);
                CoreWebView2Environment env = await CoreWebView2Environment.CreateAsync(
                    userDataFolder: userData);

                await WebView3D.EnsureCoreWebView2Async(env);

                // WebView2 背景透明化 —— 让 HTML 的 border-radius 圆角外侧不露出白色直角框
                WebView3D.DefaultBackgroundColor = System.Drawing.Color.Transparent;

                WebView3D.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    RobotViewerHost, dist, CoreWebView2HostResourceAccessKind.Allow);
                WebView3D.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    ModelHost, model, CoreWebView2HostResourceAccessKind.Allow);

                // 中继 JS 控制台日志到 C# Debug 输出
                // JS 端 console.log/error 会被拦截并通过 postMessage 发过来
                WebView3D.CoreWebView2.WebMessageReceived += (sender, args) =>
                {
                    Debug.WriteLine("[JS] " + args.WebMessageAsJson);
                };

                RegisterNavigationEvent();

                // 加载极简 3D 页面（index.html 自动加载 URDF 模型）
                WebView3D.CoreWebView2.Navigate(
                    "https://" + RobotViewerHost + "/index.html");

                IsWebViewReady = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[SAMCS3D] 初始化失败: " + ex.Message);
            }
        }

        // ===================== 导航事件 =====================

        /// <summary>
        /// 注册 NavigationCompleted 事件。
        /// 页面加载完成后：显示 WebView → 启动同步定时器。
        /// 不需要 CSS 隐藏、JS 注入、模型加载脚本——
        /// index.html 自带场景搭建、灯光、URDF 加载、updateJoint/getJointNames 接口。
        /// </summary>
        private void RegisterNavigationEvent()
        {
            var core = WebView3D.CoreWebView2;

            core.NavigationStarting += (_, args) =>
                Debug.WriteLine("[SAMCS3D] 导航: " + args.Uri);

            core.NavigationCompleted += async (_, args) =>
            {
                if (!args.IsSuccess)
                {
                    Debug.WriteLine("[SAMCS3D] 导航失败: " + args.WebErrorStatus);
                    return;
                }

                Debug.WriteLine("[SAMCS3D] 页面加载完成");

                // 显示 WebView（模型由 index.html 异步加载，就绪后自动显示）
                WebView3D.Dispatcher.Invoke(() =>
                {
                    WebView3D.Visibility = System.Windows.Visibility.Visible;
                });

                // 启动数字孪生同步定时器
                StartSyncTimer();

                Debug.WriteLine("[SAMCS3D] WebView 已显示，同步定时器已启动");
            };
        }

        // ===================== 数字孪生同步 =====================
        //
        // 六轴 → URDF 关节映射表（顺序和 Axes[0..5] 严格对应）：
        //   索引 0: R 旋转轴 → joint2 (continuous, 度→弧度)
        //   索引 1: Y 平移轴 → joint3 (prismatic, mm→m)
        //   索引 2: X 平移轴 → joint4 (prismatic, mm→m)
        //   索引 3: Z 平移轴 → joint5 (prismatic, mm→m)
        //   索引 4: P 俯仰轴 → joint6 (revolute,  度→弧度)
        //   索引 5: I 植入轴 → joint7 (prismatic, mm→m)
        //

        // 六轴 URDF 关节名数组，索引和 Axes[0..5] 严格一一对应
        private static readonly string[] JointNames =
            { "joint2", "joint3", "joint4", "joint5", "joint6", "joint7" };

        // 每个轴是否为旋转轴（true=旋转 false=平移）
        private static readonly bool[] IsRotation =
            { true, false, false, false, true, false };

        /// <summary>
        /// 启动 3D 模型同步定时器。
        /// </summary>
        private void StartSyncTimer()
        {
            _syncTimer = new DispatcherTimer();
            _syncTimer.Interval = TimeSpan.FromMilliseconds(50);
            _syncTimer.Tick += OnSyncTimerTick;
            _syncTimer.Start();

            Debug.WriteLine("[SAMCS3D] 同步定时器已启动 (50ms, 六轴批量)");
        }

        /// <summary>
        /// 同步定时器回调 —— 每 50ms 执行一次。
        /// 组装六轴数据 → 单位转换 → 打包 JSON → 一次 ExecuteScriptAsync 发送。
        ///
        /// 为什么批量打包而不逐个关节发 ExecuteScriptAsync：
        ///   每次调用都是一次跨进程序列化，6 次单独调用 = 6 倍开销。
        ///   打包成 1 次调用 = 每秒仅 20 次调用，完全不卡 UI。
        /// </summary>
        private async void OnSyncTimerTick(object? sender, EventArgs e)
        {
            try
            {
                if (!IsWebViewReady) return;

                MonitorViewModel? vm = this.DataContext as MonitorViewModel;
                if (vm == null) return;

                // ===== 步骤 1：组装六轴数据 =====
                // 每个轴表示为 { "n": 关节名, "v": URDF单位值 }
                object[] jointData = new object[6];

                // 从 ViewModel 读六轴实时位置
                if (!vm.IsConnected) return;
                if (vm.Axes.Count < 6) return;

                for (int i = 0; i < 6; i++)
                {
                    float motorValue = vm.Axes[i].CurrentPosition;
                    double urdfValue;

                    if (IsRotation[i])
                    {
                        // 旋转轴（R、P）：度 → 弧度
                        urdfValue = motorValue * Math.PI / 180.0;
                    }
                    else
                    {
                        // 平移轴（Y、X、Z、I）：mm → 米
                        urdfValue = motorValue / 1000.0;
                    }

                    jointData[i] = new { n = JointNames[i], v = urdfValue };
                }

                // ===== 步骤 2：序列化 JSON =====
                // System.Text.Json 将 new { n, v } 序列化为 {"n":"joint2","v":1.57}
                string json = System.Text.Json.JsonSerializer.Serialize(jointData);

                // ===== 步骤 3：一次性发送给 JS =====
                // 调 window.updateAllJoints(json) 批量更新 6 个关节
                string script = "updateAllJoints(" + json + ")";
                string result = await WebView3D.CoreWebView2.ExecuteScriptAsync(script);

                // ===== 步骤 4：每 3 秒输出一次日志 =====
                _syncTickCount++;
                if (_syncTickCount % 30 == 0)
                {
                    Debug.WriteLine(string.Format(
                        "[SAMCS3D-SYNC] 六轴批量 长度={0} JS返回={1}",
                        json.Length, result));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[SAMCS3D-SYNC] 异常: " + ex.Message);
            }
        }

    }
}
