using CommunityToolkit.Mvvm.ComponentModel;

namespace SAMCS_WPF.Models
{
    /// <summary>
    /// 单轴 UI 数据模型 —— 封装轴参数绑定、单位显示与越界报警逻辑。
    /// </summary>
    public partial class AxisUIModel : ObservableObject
    {
        // ===================== 轴基本标识 =====================

        /// <summary>
        /// 轴名称，如 "R(旋转)"、"Y(平移)"
        /// </summary>
        [ObservableProperty]
        private string _axisName = "";

        /// <summary>
        /// 轴硬件 ID，如 "01" ~ "06"
        /// </summary>
        [ObservableProperty]
        private string _axisId = "";

        /// <summary>
        /// 电机型号，连接时由 SDK 读取
        /// </summary>
        [ObservableProperty]
        private string _motorModel = "Unknown";

        // ===================== 位置与速度 =====================

        /// <summary>
        /// 当前位置值（单位由 PosUnit 决定）
        /// </summary>
        [ObservableProperty]
        private float _currentPosition;

        /// <summary>
        /// 当前设定速度值（单位由 VelUnit 决定）
        /// </summary>
        [ObservableProperty]
        private float _velocity;

        /// <summary>
        /// 电机是否正在运行中（由 100ms 定时器从 SDK 读取）
        /// true = 运行中（红色灯），false = 已停止（绿色灯）
        /// </summary>
        [ObservableProperty]
        private bool _isRunning;

        // ===================== 单位标识 =====================

        /// <summary>
        /// 位置单位（° 或 mm）
        /// </summary>
        [ObservableProperty]
        private string _posUnit = "";

        /// <summary>
        /// 速度单位（°/s 或 mm/s）
        /// </summary>
        [ObservableProperty]
        private string _velUnit = "";

        // ===================== 软限位 =====================

        /// <summary>
        /// 软上限位
        /// </summary>
        [ObservableProperty]
        private float _softLimitMax;

        /// <summary>
        /// 软下限位
        /// </summary>
        [ObservableProperty]
        private float _softLimitMin;

        /// <summary>
        /// 限位显示文本，格式：[下限位, 上限位]，例如 [-180, 180]
        /// 当 SoftLimitMin 或 SoftLimitMax 变化时自动刷新
        /// </summary>
        public string LimitDisplay
        {
            get
            {
                return string.Format("[{0}, {1}]", SoftLimitMin, SoftLimitMax);
            }
        }

        /// <summary>
        /// 速度软限位，所有轴统一最大允许速度 5 mm/s 或 °/s
        /// SetVelocity() 时强制检查，超过此值禁止下发
        /// </summary>
        [ObservableProperty]
        private float _softVelocityLimit = 5f;

        /// <summary>
        /// 速度限位显示文本，格式：[0, 最大速度]，例如 [0, 5]
        /// 当 SoftVelocityLimit 变化时自动刷新
        /// </summary>
        public string VelocityLimitDisplay
        {
            get
            {
                return string.Format("[0, {0}]", SoftVelocityLimit);
            }
        }

        // ===================== 运动参数 =====================

        /// <summary>
        /// 加速度 (ms)
        /// </summary>
        [ObservableProperty]
        private ushort _acceleration;

        /// <summary>
        /// 减速度 (ms)
        /// </summary>
        [ObservableProperty]
        private ushort _deceleration;

        /// <summary>
        /// 螺距 (mm)
        /// </summary>
        [ObservableProperty]
        private float _pitch;

        /// <summary>
        /// 细分 (P/R)
        /// </summary>
        [ObservableProperty]
        private int _division;

        // ===================== 越界报警 =====================

        /// <summary>
        /// 当前位置是否接近软限位（距上下限 5% 范围内触发报警）
        /// </summary>
        public bool IsNearLimit
        {
            get
            {
                float threshold = (SoftLimitMax - SoftLimitMin) * 0.05f;
                return CurrentPosition <= (SoftLimitMin + threshold)
                    || CurrentPosition >= (SoftLimitMax - threshold);
            }
        }

        /// <summary>
        /// 位置变化时同步刷新越界报警状态
        /// </summary>
        partial void OnCurrentPositionChanged(float value)
        {
            OnPropertyChanged(nameof(IsNearLimit));
        }

        /// <summary>
        /// 软限位值变化时，同步刷新限位显示文本
        /// </summary>
        partial void OnSoftLimitMinChanged(float value)
        {
            OnPropertyChanged(nameof(LimitDisplay));
        }

        /// <summary>
        /// 软限位值变化时，同步刷新限位显示文本
        /// </summary>
        partial void OnSoftLimitMaxChanged(float value)
        {
            OnPropertyChanged(nameof(LimitDisplay));
        }

        /// <summary>
        /// 速度限位值变化时，同步刷新速度限位显示文本
        /// </summary>
        partial void OnSoftVelocityLimitChanged(float value)
        {
            OnPropertyChanged(nameof(VelocityLimitDisplay));
        }
    }
}
