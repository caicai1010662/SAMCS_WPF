using CommunityToolkit.Mvvm.ComponentModel;
using MyRobotSDK.Models;

namespace SAMCS_WPF.Models
{
    /// <summary>
    /// 轴控制面板中每个轴的 UI 绑定模型。
    /// 精简单轴控制所需属性，与 AxisUIModel（状态监控用）职责分离。
    /// </summary>
    public partial class AxisControlModel : ObservableObject
    {
        // === 静态属性（初始化后不变） ===

        /// <summary>
        /// SDK 轴枚举
        /// </summary>
        public RobotAxis AxisEnum { get; init; }

        /// <summary>
        /// 轴显示名称，如 "R(旋转)"、"P(俯仰)"
        /// </summary>
        public string AxisName { get; init; } = "";

        /// <summary>
        /// 单位字符串，"mm" 或 "°"
        /// </summary>
        public string Unit { get; init; } = "";

        /// <summary>
        /// 是否为角度轴（R/P 为 true，Y/X/Z/I 为 false）
        /// </summary>
        public bool IsAngleAxis { get; init; }

        /// <summary>
        /// 软件限位下限（mm 或 °）
        /// </summary>
        public float SoftLimitMin { get; init; }

        /// <summary>
        /// 软件限位上限（mm 或 °）
        /// </summary>
        public float SoftLimitMax { get; init; }

        // === 运行时属性（由 50ms 定时器刷新） ===

        /// <summary>
        /// 当前绝对位置（mm 或 °）
        /// </summary>
        [ObservableProperty]
        private float _currentPosition;

        /// <summary>
        /// 电机是否正在运动
        /// </summary>
        [ObservableProperty]
        private bool _isRunning;

        // === 数值调输入（仅在 Numeric 模式下使用） ===

        /// <summary>
        /// 用户输入的数值文本
        /// </summary>
        [ObservableProperty]
        private string _inputText = "";

        /// <summary>
        /// 输入值是否合法（在软限位范围内）
        /// </summary>
        [ObservableProperty]
        private bool _isInputValid = true;

        /// <summary>
        /// 输入验证错误信息
        /// </summary>
        [ObservableProperty]
        private string _inputError = "";
    }
}
