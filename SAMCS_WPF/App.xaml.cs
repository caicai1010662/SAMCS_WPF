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
        /// 全局 DI 容器，供非注入场景按需取用服务。
        /// </summary>
        public static IServiceProvider? Services { get; private set; }

        public App()
        {
            Services = ConfigureServices();
        }

        /// <summary>
        /// 注册核心服务、ViewModel 与主窗口到 DI 容器。
        /// </summary>
        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // 核心服务 —— 单例
            services.AddSingleton<IRobotControlService, RobotControlService>();

            // ViewModel —— 单例，保证 Tab 切换时状态不丢失
            services.AddSingleton<MainViewModel>();
            services.AddSingleton<MonitorViewModel>();
            services.AddSingleton<StereotaxicViewModel>();
            services.AddSingleton<ImplantViewModel>();

            // 主窗口 —— 瞬时
            services.AddTransient<MainWindow>();

            return services.BuildServiceProvider();
        }

        /// <summary>
        /// 从容器解析主窗口，注入 MainViewModel 并启动。
        /// </summary>
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var mainWindow = Services!.GetRequiredService<MainWindow>();
            mainWindow.DataContext = Services!.GetRequiredService<MainViewModel>();
            mainWindow.Show();
        }
    }
}
