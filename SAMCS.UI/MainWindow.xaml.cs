using SAMCS.UI.ViewModels;
using System.Windows;
using Microsoft.Extensions.DependencyInjection; // 需要引入这个

namespace SAMCS.UI
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            // MVVM 的核心：将 UI 的数据上下文指向我们注册的 ViewModel
            this.DataContext = App.Current.GetRequiredService<MainViewModel>();
        }
    }
}