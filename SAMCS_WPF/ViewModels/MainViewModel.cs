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
            get => _selectedPage;
            set
            {
                if (SetProperty(ref _selectedPage, value))
                {
                    OnPropertyChanged(nameof(CurrentView));
                    OnPropertyChanged(nameof(IsMonitorSelected));
                    OnPropertyChanged(nameof(IsStereotaxicSelected));
                    OnPropertyChanged(nameof(IsImplantSelected));
                }
            }
        }

        /// <summary>
        /// 当前显示的子 ViewModel，由 ContentControl 通过 DataTemplate 自动匹配对应 View
        /// </summary>
        public object CurrentView => SelectedPage switch
        {
            "Monitor" => _monitorVM,
            "Stereotaxic" => _stereotaxicVM,
            "Implant" => _implantVM,
            _ => _monitorVM
        };

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
