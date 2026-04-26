namespace MyRobotSDK.Models
{
    /// <summary>
    /// 【功能】强类型轴枚举，映射 SAMCS 六轴机器人的物理结构。
    /// 【特别说明】底层驱动器使用 "01"~"06" 进行寻址，枚举值直接对应其物理轴向 (R-Y-X-Z-P-I)。
    /// </summary>
    public enum RobotAxis
    {
        /// <summary>
        /// 旋转轴 (R - Rotation) - 对应控制器地址 01
        /// </summary>
        Axis1 = 1,

        /// <summary>
        /// Y 轴平移 (Y) - 对应控制器地址 02
        /// </summary>
        Axis2 = 2,

        /// <summary>
        /// X 轴平移 (X) - 对应控制器地址 03
        /// </summary>
        Axis3 = 3,

        /// <summary>
        /// Z 轴平移 (Z) - 对应控制器地址 04
        /// </summary>
        Axis4 = 4,

        /// <summary>
        /// 俯仰轴 (P - Pitch) - 对应控制器地址 05
        /// </summary>
        Axis5 = 5,

        /// <summary>
        /// 植入轴 (I - Implantation) - 对应控制器地址 06
        /// </summary>
        Axis6 = 6
    }
}