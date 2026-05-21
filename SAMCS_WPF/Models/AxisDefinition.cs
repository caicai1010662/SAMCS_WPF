using MyRobotSDK.Models;

namespace SAMCS_WPF.Models
{
    /// <summary>
    /// ===== 白话文说明 =====
    /// 这个类存储一个轴的"静态定义"——也就是出厂时就已经确定的参数。
    /// 这些值在程序运行期间不会改变。
    ///
    /// 和 AxisRuntimeState 的区别：
    ///   AxisDefinition  = 产品规格书上的参数（不变）
    ///   AxisRuntimeState = 从硬件实时读取的数据（100ms 刷新一次）
    ///
    /// 和 AxisUIModel 的区别：
    ///   AxisDefinition = 纯数据，不绑定 UI
    ///   AxisUIModel = UI 绑定模型，继承 ObservableObject，属性变化时自动刷新界面
    ///
    /// 这个类不操作 SDK，不绑定 UI，只负责"记住参数"。
    /// </summary>
    public class AxisDefinition
    {
        /// <summary>
        /// 物理轴枚举值。
        /// 比如 RobotAxis.Axis1 代表 R 旋转轴，在 SDK 中的通讯地址是 01。
        /// 这个值直接传给 _robotService.GetAxis() 来获取对应的轴控制器。
        /// </summary>
        public RobotAxis Axis { get; set; }

        /// <summary>
        /// UI 上显示的轴名称，比如 "R(旋转)"、"Y(平移)"。
        /// 括号里的中文帮助操作者理解轴的物理含义。
        /// </summary>
        public string AxisName { get; set; } = "";

        /// <summary>
        /// 轴的通讯地址字符串，比如 "01"、"02" ... "06"。
        /// 这个值显示在 UI 表格的"轴号"栏。
        /// 注意：这是给用户看的字符串，实际 SDK 通讯用的是 RobotAxis 枚举。
        /// </summary>
        public string AxisId { get; set; } = "";

        /// <summary>
        /// 预设的电机型号。
        /// 连接时用这个值和 SDK 实际读取的型号做比对。
        /// 如果不一致，说明硬件接线可能有问题。
        /// </summary>
        public string ExpectedMotorModel { get; set; } = "";

        /// <summary>
        /// 软下限位 —— 轴允许运动的最小位置值（° 或 mm）。
        /// 任何 MoveAbsolute 的目标位置如果小于这个值，会被安全限位检查拦截。
        /// </summary>
        public float SoftLimitMin { get; set; }

        /// <summary>
        /// 软上限位 —— 轴允许运动的最大位置值（° 或 mm）。
        /// </summary>
        public float SoftLimitMax { get; set; }

        /// <summary>
        /// 速度软限位 —— 所有轴统一的最大允许速度（°/s 或 mm/s）。
        /// 当前为 5，由硬件安全规范决定。
        /// SetVelocity() 时如果传入的值超过这个限制，会硬性阻止。
        /// </summary>
        public float SoftVelocityLimit { get; set; } = 5f;

        /// <summary>
        /// 默认归位坐标 —— 点击"复位原点"按钮时，轴会运动到这个位置。
        /// 这个值必须在 [SoftLimitMin, SoftLimitMax] 范围内。
        /// </summary>
        public float HomePosition { get; set; }
    }
}
