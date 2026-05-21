using CommunityToolkit.Mvvm.ComponentModel;
using SAMCS_WPF.Services;

namespace SAMCS_WPF.ViewModels
{
    /// <summary>
    /// 主窗口 ViewModel —— 管理三个子页面的导航切换与 Tab 选中状态。
    /// </summary>
    public partial class MainViewModel : ObservableObject
    {
        // ===================== 依赖注入字段 =====================

        private readonly IRobotControlService _robotService;
        private readonly MonitorViewModel _monitorVM;
        private readonly StereotaxicViewModel _stereotaxicVM;
        private readonly ImplantViewModel _implantVM;

        // ===================== 私有状态字段 =====================

        private string _selectedPage = "Monitor";

        // ===================== 导航属性 =====================

        /// <summary>
        /// 当前选中页面标识 ("Monitor" / "Stereotaxic" / "Implant")
        /// </summary>
        public string SelectedPage
        {
            get { return _selectedPage; }
            set
            {
                if (SetProperty(ref _selectedPage, value))
                {
                    // 通知三个 Tab 的 IsChecked 绑定 + 三个 View 的 Visibility 绑定刷新
                    OnPropertyChanged(nameof(IsMonitorSelected));
                    OnPropertyChanged(nameof(IsStereotaxicSelected));
                    OnPropertyChanged(nameof(IsImplantSelected));
                }
            }
        }

        // ===================== 子 ViewModel 暴露 =====================
        //
        // 白话文说明：之前用 DataTemplate 时，WPF 会自动把 ViewModel 赋给 View 的 DataContext。
        // 现在改成三个 View 直接写在 XAML 里，需要手动绑定 DataContext。
        // 这三个属性就是给 XAML 绑定用的：DataContext="{Binding MonitorVM}"。
        //

        /// <summary>
        /// 状态监控页 ViewModel（供 MonitorView 绑定 DataContext）
        /// </summary>
        public MonitorViewModel MonitorVM
        {
            get { return _monitorVM; }
        }

        /// <summary>
        /// 脑立体定向页 ViewModel（供 StereotaxicView 绑定 DataContext）
        /// </summary>
        public StereotaxicViewModel StereotaxicVM
        {
            get { return _stereotaxicVM; }
        }

        /// <summary>
        /// 微电极植入页 ViewModel（供 ImplantView 绑定 DataContext）
        /// </summary>
        public ImplantViewModel ImplantVM
        {
            get { return _implantVM; }
        }

        /// <summary>
        /// "状态监控" Tab 选中状态
        /// </summary>
        public bool IsMonitorSelected
        {
            get => SelectedPage == "Monitor";
            set { if (value) SelectedPage = "Monitor"; }
        }

        /// <summary>
        /// "脑立体定向" Tab 选中状态
        /// </summary>
        public bool IsStereotaxicSelected
        {
            get => SelectedPage == "Stereotaxic";
            set { if (value) SelectedPage = "Stereotaxic"; }
        }

        /// <summary>
        /// "微电极植入" Tab 选中状态
        /// </summary>
        public bool IsImplantSelected
        {
            get => SelectedPage == "Implant";
            set { if (value) SelectedPage = "Implant"; }
        }

        // ===================== 构造函数 =====================

        public MainViewModel(
            IRobotControlService robotService,
            MonitorViewModel monitorVM,
            StereotaxicViewModel stereotaxicVM,
            ImplantViewModel implantVM)
        {
            // TODO: _robotService 预留给主窗口级别的硬件操作（如全局急停）
            _robotService = robotService;
            _monitorVM = monitorVM;
            _stereotaxicVM = stereotaxicVM;
            _implantVM = implantVM;
        }
    }
}
