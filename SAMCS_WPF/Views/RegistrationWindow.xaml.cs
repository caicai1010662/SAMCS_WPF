using System.Windows;

namespace SAMCS_WPF.Views
{
    /// <summary>
    /// 颅骨配准窗口 —— 独立 Window，展示五点采集结果与坐标系配准数据。
    /// </summary>
    public partial class RegistrationWindow : Window
    {
        public RegistrationWindow()
        {
            InitializeComponent();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
