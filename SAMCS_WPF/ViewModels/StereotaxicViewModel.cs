using CommunityToolkit.Mvvm.ComponentModel;
using SAMCS_WPF.Services;

namespace SAMCS_WPF.ViewModels
{
    /// <summary>
    /// 脑立体定向页 ViewModel —— 预留给手术规划与立体定向定位功能。
    /// </summary>
    public partial class StereotaxicViewModel : ObservableObject
    {
        // 预留给子页面硬件操作
        private readonly IRobotControlService _robotService;

        public StereotaxicViewModel(IRobotControlService robotService)
        {
            _robotService = robotService;
        }
    }
}
