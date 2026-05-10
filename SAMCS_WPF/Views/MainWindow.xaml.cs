using System.Windows;

namespace SAMCS_WPF.Views
{
    /// <summary>
    /// 主窗口 View —— 由 App.OnStartup 实例化并注入 DataContext。
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }
    }
}
