using System;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using SAMCS_WPF.Services;
using SAMCS_WPF.ViewModels;
using SAMCS_WPF.Views;

namespace SAMCS_WPF
{
    /// <summary>
    /// 应用程序入口 —— 配置 DI 容器并启动主窗口。
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// 全局 DI 容器。
        /// 白话文说明：DI（Dependency Injection，依赖注入）是一个"对象管理器"。
        /// 你不用自己 new 对象，而是告诉 DI 容器"我需要一个 MonitorViewModel"，
        /// 容器会自动帮你创建，并且自动填好它的所有依赖项。
        /// 这避免了手动传递层层依赖的麻烦。
        /// </summary>
        public static IServiceProvider Services { get; private set; } = null!;

        public App()
        {
            Services = ConfigureServices();
        }

        /// <summary>
        /// 注册核心服务、ViewModel 与主窗口到 DI 容器。
        ///
        /// AddSingleton = 整个程序只创建一个实例，所有人共享。
        /// AddTransient = 每次请求都创建新实例。
        ///
        /// 注册顺序：
        ///   1. IRobotControlService  → RobotControlService   （底层硬件通信）
        ///   2. IAxisWorkflowService  → AxisWorkflowService    （运动流程编排，依赖第 1 项）
        ///   3. 四个 ViewModel                                 （UI 逻辑，依赖第 2 项）
        ///   4. MainWindow                                     （主窗口，依赖第 3 项）
        /// </summary>
        private static IServiceProvider ConfigureServices()
        {
            ServiceCollection services = new ServiceCollection();

            // 核心服务 —— 单例（整个程序生命周期只有一个实例）
            services.AddSingleton<IRobotControlService, RobotControlService>();

            // 轴运动工作流服务 —— 单例，依赖 IRobotControlService
            services.AddSingleton<IAxisWorkflowService, AxisWorkflowService>();

            // ViewModel —— 单例，保证 Tab 切换时状态不丢失
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<MonitorViewModel>();
            services.AddSingleton<StereotaxicViewModel>();
            services.AddSingleton<ImplantViewModel>();

            // 主窗口 —— 瞬时（每次请求创建新实例，实际只请求一次）
            services.AddTransient<MainWindow>();

            return services.BuildServiceProvider();
        }

        /// <summary>
        /// 从容器解析主窗口，注入 MainViewModel 并启动。
        ///
        /// 白话文说明：GetRequiredService 的意思是"从容器中取出已创建好的对象"。
        /// 容器会根据注册的类型自动找到对应的实现类，并自动注入构造函数参数。
        /// </summary>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            MainWindow mainWindow = Services.GetRequiredService<MainWindow>();
            mainWindow.DataContext = Services.GetRequiredService<MainViewModel>();
            mainWindow.Show();
        }
    }
}
