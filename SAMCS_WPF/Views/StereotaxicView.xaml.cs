using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Web.WebView2.Core;

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

        // ===================== 构造函数 =====================

        public StereotaxicView()
        {
            InitializeComponent();
            // 初始隐藏，页面就绪后再显示，消除白屏闪烁
            WebViewSkull.Visibility = System.Windows.Visibility.Hidden;
            _ = InitializeWebViewAsync();
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
                // Debug 模式：虚拟主机直指源码目录，改 HTML/JS 后 F5 刷新即生效，无需编译
                // bin 输出路径: bin/x64/Debug/net8.0-windows → 上 4 层到项目根
                string projectDir = Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\..\"));
                runtime = Path.Combine(projectDir, RuntimePath);
                modelDir = Path.Combine(projectDir, @"3D\Model");
#else
                // Release 模式：虚拟主机指向 bin 输出目录
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

                // 浏览器快捷键已禁用，刷新通过控制面板按钮触发

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

        /// <summary>
        /// 注册 NavigationCompleted 事件。
        /// 页面加载完成后显示 WebView。
        /// </summary>
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

                // 显示 WebView
                WebViewSkull.Dispatcher.Invoke(() =>
                {
                    WebViewSkull.Visibility = System.Windows.Visibility.Visible;
                });

                Debug.WriteLine("[Skull3D] WebView 已显示");
            };
        }
    }
}
