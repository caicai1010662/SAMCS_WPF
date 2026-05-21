using MyRobotSDK.Models;

namespace SAMCS_WPF.Models
{
    /// <summary>
    /// ===== 白话文说明 =====
    /// 这个类存储一个轴的"运行时状态"——也就是某一次从 SDK 读取到的实时数据。
    ///
    /// 它是一个快照（Snapshot）：
    ///   每次定时器触发 → 从 SDK 读数据 → 创建一个 AxisRuntimeState 对象 → 返回给 ViewModel
    ///   ViewModel 把数据拷贝到 AxisUIModel → 这个对象就可以丢弃了
    ///
    /// 为什么不直接更新 AxisUIModel：
    ///   因为数据采集在 Service 层，Service 不应该知道 AxisUIModel 这个 UI 类的存在。
    ///   AxisRuntimeState 是一个纯数据中介，Service 填好数据，ViewModel 自己决定怎么用。
    ///
    /// 这个类不操作 SDK，不绑定 UI，只负责"搬运数据"。
    /// </summary>
    public class AxisRuntimeState
    {
        /// <summary>
        /// 物理轴枚举，用来标识这个状态属于哪个轴。
        /// </summary>
        public RobotAxis Axis { get; set; }

        /// <summary>
        /// 当前位置值。
        /// 由 SDK GetPosition() 读取，单位取决于轴类型（° 或 mm）。
        /// </summary>
        public float CurrentPosition { get; set; }

        /// <summary>
        /// 当前设定速度值。
        /// 由 SDK GetVelocity() 读取。
        /// 注意：这是"设定速度"，不是"实际速度"。
        /// 电机实际速度可能因负载等原因略低于设定值。
        /// </summary>
        public float Velocity { get; set; }

        /// <summary>
        /// 电机是否正在运行。
        /// true = 正在转动，false = 已停止。
        /// 由 SDK IsRunning() 读取。
        /// UI 上的状态灯（●）根据这个值变红或变绿。
        /// </summary>
        public bool IsRunning { get; set; }

        /// <summary>
        /// 加速度值，单位 ms（毫秒）。
        /// 表示电机从 0 加速到目标速度需要的时间。
        /// 由 SDK GetAcceleration() 读取。
        /// </summary>
        public ushort Acceleration { get; set; }

        /// <summary>
        /// 减速度值，单位 ms（毫秒）。
        /// 表示电机从目标速度减速到 0 需要的时间。
        /// 由 SDK GetDeceleration() 读取。
        /// </summary>
        public ushort Deceleration { get; set; }

        /// <summary>
        /// 螺距值，单位 mm（毫米）。
        /// 丝杆转动一圈对应的直线位移。
        /// 由 SDK GetPitch() 读取。
        /// </summary>
        public float Pitch { get; set; }

        /// <summary>
        /// 细分值，单位 P/R（脉冲每转）。
        /// 电机驱动器将一个完整的步距角细分为多少微步。
        /// 由 SDK GetDivision() 读取。
        /// </summary>
        public int Division { get; set; }

    }
}
