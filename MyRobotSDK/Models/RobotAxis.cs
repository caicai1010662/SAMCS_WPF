namespace MyRobotSDK.Models
{
    /// <summary>
    /// 【功能】强类型轴枚举，映射 SAMCS 六轴机器人。
    /// 【特别说明】底层驱动器使用 "01"~"06" 进行寻址，枚举值对应(R-Y-X-Z-P-I)。
    /// </summary>
    public enum RobotAxis
    {
        /// <summary>
        /// 旋转轴R - 控制器地址 01
        /// </summary>
        Axis1 = 1,

        /// <summary>
        /// 线性轴Y - 控制器地址 02
        /// </summary>
        Axis2 = 2,

        /// <summary>
        /// 线性轴X - 控制器地址 03
        /// </summary>
        Axis3 = 3,

        /// <summary>
        /// 线性轴Z - 控制器地址 04
        /// </summary>
        Axis4 = 4,

        /// <summary>
        /// 俯仰轴P - 控制器地址 05
        /// </summary>
        Axis5 = 5,

        /// <summary>
        /// 植入轴I - 控制器地址 06
        /// </summary>
        Axis6 = 6
    }
}