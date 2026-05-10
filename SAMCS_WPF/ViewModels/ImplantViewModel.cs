using CommunityToolkit.Mvvm.ComponentModel;
using SAMCS_WPF.Services;

namespace SAMCS_WPF.ViewModels
{
    /// <summary>
    /// 微电极植入页 ViewModel —— 预留给电极植入与信号采集功能。
    /// </summary>
    public partial class ImplantViewModel : ObservableObject
    {
        // 预留给子页面硬件操作
        private readonly IRobotControlService _robotService;

        public ImplantViewModel(IRobotControlService robotService)
        {
            _robotService = robotService;
        }
    }
}
