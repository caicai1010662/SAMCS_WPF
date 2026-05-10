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
        private string _axisName = string.Empty;

        /// <summary>
        /// 轴硬件 ID，如 "01" ~ "06"
        /// </summary>
        [ObservableProperty]
        private string _axisId = string.Empty;

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

        // ===================== 单位标识 =====================

        /// <summary>
        /// 位置单位（° 或 mm）
        /// </summary>
        [ObservableProperty]
        private string _posUnit = string.Empty;

        /// <summary>
        /// 速度单位（°/s 或 mm/s）
        /// </summary>
        [ObservableProperty]
        private string _velUnit = string.Empty;

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
    }
}
