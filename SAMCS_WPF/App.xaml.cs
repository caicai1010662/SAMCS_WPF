using Microsoft.Extensions.DependencyInjection;
using SAMCS_WPF.ViewModels;
using System;
using System.Windows;

namespace SAMCS_WPF
{
    public partial class App : Application
    {
        /// <summary>
        /// 全局依赖注入容器 (引擎核心)
        /// </summary>
        public new static IServiceProvider Current => ((App)Application.Current).Services;

        public IServiceProvider Services { get; }

        public App()
        {
            Services = ConfigureServices();
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            // 1. 注册 ViewModel (每次请求都创建新实例，或者根据需要设为 Singleton 单例)
            services.AddSingleton<MainViewModel>();

            // 2. TODO: 未来我们会在这里注册全局的 RobotService

            return services.BuildServiceProvider();
        }
    }
}