using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;
using MyRobotSDK.Models;

namespace SAMCS_WPF.Views
{
    /// <summary>
    /// 脑立体定向页 View —— 左侧嵌入 WebView2 显示 3D 小鼠颅骨模型。
    ///
    /// 架构：
    ///   WebView2 加载 SkullViewer.html（Three.js + GLTFLoader），
    ///   页面自动加载 MouseSkull.glb，提供 OrbitControls 旋转/缩放。
    ///
    /// 位姿控制条在 HTML 内部实现，C# 只需通过 ExecuteScriptAsync
    /// 调用 setNeedlePose(ap, ml, dv, rotation, pitch) 传入数值即可。
    ///
    /// Debug 模式热重载：
    ///   #if DEBUG 下，虚拟主机映射到源码目录（而非 bin 输出目录），
    ///   修改 SkullViewer.html 后在 WebView2 内按 F5 即可刷新，无需重新编译。
    /// </summary>
    public partial class StereotaxicView : UserControl
    {
        // ===================== 常量 =====================

        private const string SkullViewerHost = "skull-viewer.local";
        private const string ModelHost = "samcs-model.local";
        private const string RuntimePath = @"3D\Runtime";

        // ===================== 状态 =====================

        public bool IsWebViewReady { get; private set; }

        // 当前是否有 Jog 按钮被按下（用于 MouseLeave 判断）
        private bool _isJogPressed = false;

        // 防止 Loaded 事件重复注册
        private bool _isLoadedRegistered = false;

        // ===================== 便捷 VM 访问 =====================

        private ViewModels.StereotaxicViewModel? VM
        {
            get
            {
                return DataContext as ViewModels.StereotaxicViewModel;
            }
        }

        // ===================== 构造函数 =====================

        public StereotaxicView()
        {
            InitializeComponent();
            // 初始隐藏 WebView，页面就绪后再显示，消除白屏闪烁
            WebViewSkull.Visibility = System.Windows.Visibility.Hidden;
            _ = InitializeWebViewAsync();

            // 启动 3D 模型后，注册生命周期事件
            this.Loaded += OnLoaded;
            this.IsVisibleChanged += OnIsVisibleChanged;
        }

        // ===================== WebView2 初始化 =====================

        /// <summary>
        /// 初始化 WebView2 环境并导航到颅骨 3D 页面。
        /// 虚拟主机映射：
        ///   skull-viewer.local → 3D/Runtime/（SkullViewer.html + JS 文件）
        ///   samcs-model.local  → 3D/Model/（MouseSkull.glb）
        ///
        /// Debug 模式下映射到项目源码目录，Release 模式下映射到 bin 输出目录。
        /// </summary>
        private async Task InitializeWebViewAsync()
        {
            try
            {
                string baseDir = AppContext.BaseDirectory;
                string runtime;
                string modelDir;

#if DEBUG
                // Debug 模式：优先映射到源码目录（改 HTML/JS 后 F5 刷新即生效）
                // 如果源码目录不存在（exe 被复制到 U 盘等场景），回退到 bin 输出目录
                string projectDir = Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\..\"));
                string srcRuntime = Path.Combine(projectDir, RuntimePath);
                if (Directory.Exists(srcRuntime))
                {
                    runtime = srcRuntime;
                    modelDir = Path.Combine(projectDir, @"3D\Model");
                }
                else
                {
                    runtime = Path.GetFullPath(Path.Combine(baseDir, RuntimePath));
                    modelDir = Path.GetFullPath(Path.Combine(baseDir, @"3D\Model"));
                }
#else
                runtime = Path.GetFullPath(Path.Combine(baseDir, RuntimePath));
                modelDir = Path.GetFullPath(Path.Combine(baseDir, @"3D\Model"));
#endif
                runtime = runtime.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                modelDir = modelDir.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

                string userData = Path.Combine(Path.GetTempPath(), "SAMCS_SkullViewer");
                Directory.CreateDirectory(userData);
                CoreWebView2Environment env = await CoreWebView2Environment.CreateAsync(
                    userDataFolder: userData);

                await WebViewSkull.EnsureCoreWebView2Async(env);

                // 禁用所有浏览器快捷键（F5刷新、Ctrl+R等）
                WebViewSkull.CoreWebView2.Settings.AreBrowserAcceleratorKeysEnabled = false;

                // WebView2 背景透明化
                WebViewSkull.DefaultBackgroundColor = System.Drawing.Color.Transparent;

                // 映射虚拟主机：JS/HTML 文件目录
                WebViewSkull.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    SkullViewerHost, runtime, CoreWebView2HostResourceAccessKind.Allow);

                // 映射虚拟主机：3D 模型文件目录
                WebViewSkull.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    ModelHost, modelDir, CoreWebView2HostResourceAccessKind.Allow);

                // JS 控制台日志中继到 C# Debug 输出
                WebViewSkull.CoreWebView2.WebMessageReceived += (sender, args) =>
                {
                    Debug.WriteLine("[Skull3D] " + args.WebMessageAsJson);
                };

                RegisterNavigationEvent();

                // 导航到颅骨 3D 查看器
                WebViewSkull.CoreWebView2.Navigate(
                    "https://" + SkullViewerHost + "/SkullViewer.html");

                IsWebViewReady = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[Skull3D] 初始化失败: " + ex.Message);
            }
        }

        // ===================== 导航事件 =====================

        private void RegisterNavigationEvent()
        {
            var core = WebViewSkull.CoreWebView2;

            core.NavigationStarting += (_, args) =>
                Debug.WriteLine("[Skull3D] 导航: " + args.Uri);

            core.NavigationCompleted += (_, args) =>
            {
                if (!args.IsSuccess)
                {
                    Debug.WriteLine("[Skull3D] 导航失败: " + args.WebErrorStatus);
                    return;
                }

                Debug.WriteLine("[Skull3D] 页面加载完成");

                WebViewSkull.Dispatcher.Invoke(() =>
                {
                    WebViewSkull.Visibility = System.Windows.Visibility.Visible;
                });

                Debug.WriteLine("[Skull3D] WebView 已显示");
            };
        }

        // ===================== 生命周期 =====================

        /// <summary>
        /// 控件加载完成后：
        ///   1. 注册父窗口的 Deactivated 事件（窗口失焦时停止所有运动）
        ///   2. 尝试启动位置刷新轮询
        /// </summary>
        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            if (_isLoadedRegistered)
            {
                return; // 防止 Loaded 事件重复触发导致重复订阅
            }
            _isLoadedRegistered = true;

            // 获取父窗口
            Window window = Window.GetWindow(this);
            if (window != null)
            {
                window.Deactivated += OnWindowDeactivated;
            }

            // 尝试启动轮询（如果硬件已连接则开始 50ms 刷新）
            ViewModels.StereotaxicViewModel? vm = VM;
            if (vm != null)
            {
                vm.TryStartPolling();
            }
        }

        /// <summary>
        /// 窗口失焦时（Alt+Tab 等），自动停止所有 Jog 运动。
        /// 防止电机在用户切换窗口时持续运行。
        /// </summary>
        private void OnWindowDeactivated(object? sender, EventArgs e)
        {
            ViewModels.StereotaxicViewModel? vm = VM;
            if (vm != null)
            {
                vm.StopAllJog();
            }
        }

        /// <summary>
        /// 页面切换（Tab 切换导致 Visibility 变化）时，
        /// 如果页面被隐藏，停止所有 Jog 运动。
        /// </summary>
        private void OnIsVisibleChanged(object? sender, DependencyPropertyChangedEventArgs e)
        {
            bool isVisible = (bool)e.NewValue;
            if (!isVisible)
            {
                ViewModels.StereotaxicViewModel? vm = VM;
                if (vm != null)
                {
                    vm.StopAllJog();
                }
            }
            else
            {
                // 页面重新显示时，尝试启动轮询
                ViewModels.StereotaxicViewModel? vm = VM;
                if (vm != null)
                {
                    vm.TryStartPolling();
                }
            }
        }

        // ===================== Jog 按钮事件 =====================

        /// <summary>
        /// Jog 按钮 MouseDown —— 启动连续点动。
        ///
        /// Tag 格式："序号+方向"
        ///   序号: 0=R, 1=P, 2=Y, 3=X, 4=Z, 5=I
        ///   方向: + 正向, - 负向
        ///
        /// 示例: "0+" = R轴正向, "5-" = I轴负向
        /// </summary>
        private void JogButton_MouseDown(object? sender, MouseButtonEventArgs e)
        {
            ViewModels.StereotaxicViewModel? vm = VM;
            if (vm == null)
            {
                return;
            }

            Button? button = sender as Button;
            if (button == null)
            {
                return;
            }

            string? tag = button.Tag as string;
            if (string.IsNullOrEmpty(tag) || tag.Length < 2)
            {
                return;
            }

            // 解析 Tag：最后一个字符是方向，前面是序号
            char direction = tag[tag.Length - 1];
            string indexStr = tag.Substring(0, tag.Length - 1);

            int axisIndex;
            if (!int.TryParse(indexStr, out axisIndex))
            {
                return;
            }

            RobotAxis axis = vm.GetAxisByIndex(axisIndex);
            bool positive = direction == '+';

            _isJogPressed = true;

            // 捕获鼠标：确保 MouseUp 即使鼠标移出按钮也能收到
            button.CaptureMouse();

            vm.JogStart(axis, positive);
        }

        /// <summary>
        /// Jog 按钮 MouseUp —— 停止点动。
        /// </summary>
        private void JogButton_MouseUp(object? sender, MouseButtonEventArgs e)
        {
            ViewModels.StereotaxicViewModel? vm = VM;
            if (vm == null)
            {
                return;
            }

            Button? button = sender as Button;
            if (button == null)
            {
                return;
            }

            string? tag = button.Tag as string;
            if (string.IsNullOrEmpty(tag) || tag.Length < 2)
            {
                return;
            }

            char direction = tag[tag.Length - 1];
            string indexStr = tag.Substring(0, tag.Length - 1);

            int axisIndex;
            if (!int.TryParse(indexStr, out axisIndex))
            {
                return;
            }

            RobotAxis axis = vm.GetAxisByIndex(axisIndex);

            _isJogPressed = false;

            // 释放鼠标捕获
            button.ReleaseMouseCapture();

            vm.JogStop(axis);
        }

        /// <summary>
        /// Jog 按钮 MouseLeave —— 鼠标离开按钮时自动停止。
        /// 仅在鼠标左键仍然按下的情况下停止（防止"拖拽离开按钮"导致电机持续运动）。
        /// </summary>
        private void JogButton_MouseLeave(object? sender, MouseEventArgs e)
        {
            if (!_isJogPressed)
            {
                return;
            }

            // 检查鼠标左键是否仍然按下
            if (e.LeftButton != MouseButtonState.Pressed)
            {
                return;
            }

            ViewModels.StereotaxicViewModel? vm = VM;
            if (vm == null)
            {
                return;
            }

            Button? button = sender as Button;
            if (button == null)
            {
                return;
            }

            string? tag = button.Tag as string;
            if (string.IsNullOrEmpty(tag) || tag.Length < 2)
            {
                return;
            }

            string indexStr = tag.Substring(0, tag.Length - 1);

            int axisIndex;
            if (!int.TryParse(indexStr, out axisIndex))
            {
                return;
            }

            RobotAxis axis = vm.GetAxisByIndex(axisIndex);

            _isJogPressed = false;
            button.ReleaseMouseCapture();
            vm.JogStop(axis);
        }

        // ===================== 数值输入过滤与校验 =====================

        /// <summary>
        /// 数值调输入框字符过滤：
        /// 1. 只允许数字、小数点、负号
        /// 2. 限制小数位最多 3 位
        /// </summary>
        private void NumericInput_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // 第一步：字符白名单过滤
            for (int i = 0; i < e.Text.Length; i++)
            {
                char c = e.Text[i];
                if (!char.IsDigit(c) && c != '.' && c != '-')
                {
                    e.Handled = true;
                    return;
                }
            }

            // 第二步：限制小数位不超过 3 位
            TextBox? textBox = sender as TextBox;
            if (textBox == null) return;

            string currentText = textBox.Text;
            int dotIndex = currentText.IndexOf('.');

            // 没有小数点，无需限制
            if (dotIndex < 0) return;

            // 光标在 decimal point 之前，不限制
            if (textBox.CaretIndex <= dotIndex) return;

            // 有选中文本（将被替换），不限制
            if (textBox.SelectionLength > 0) return;

            // 统计小数点后的数字位数
            int digitsAfterDot = 0;
            for (int i = dotIndex + 1; i < currentText.Length; i++)
            {
                if (char.IsDigit(currentText[i]))
                {
                    digitsAfterDot = digitsAfterDot + 1;
                }
                else
                {
                    break;
                }
            }

            // 仅当输入的是数字时才限制（允许退格等操作）
            bool isDigit = e.Text.Length > 0 && char.IsDigit(e.Text[0]);

            // 小数点后已有 3 位 → 拒绝继续输入数字
            if (digitsAfterDot >= 3 && isDigit)
            {
                e.Handled = true;
            }
        }

        /// <summary>
        /// 数值调输入框文本变化时触发实时校验。
        /// 通过 TextBox.Tag（轴序号字符串）定位轴并调用 VM 验证。
        /// </summary>
        private void NumericInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            ViewModels.StereotaxicViewModel? vm = VM;
            if (vm == null)
            {
                return;
            }

            TextBox? textBox = sender as TextBox;
            if (textBox == null)
            {
                return;
            }

            string? tag = textBox.Tag as string;
            if (string.IsNullOrEmpty(tag))
            {
                return;
            }

            int axisIndex;
            if (int.TryParse(tag, out axisIndex))
            {
                vm.ValidateNumericInput(axisIndex);
            }
        }

        // ===================== 日志自动滚动 =====================

        /// <summary>
        /// 日志文本变化时自动滚动到底部。
        /// </summary>
        private void LogTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            LogTextBox.ScrollToEnd();
        }
    }
}
