namespace MyRobotSDK.HAL
{
    /// <summary>
    /// 【功能】底层驱动器状态映射宏常量。
    /// 【特别说明】
    /// （1）对应头文件中的 #define 宏定义。
    /// （2）使用 internal 关键字进行隔离保护，避免将底层的魔法数字 (Magic Numbers) 污染到外层的 WPF 业务逻辑中。
    /// </summary>
    internal static class FtiConstants
    {
        /// <summary>
        /// 逻辑真
        /// </summary>
        public const byte FT_TRUE = 1;

        /// <summary>
        /// 逻辑假
        /// </summary>
        public const byte FT_FALSE = 0;

        /// <summary>
        /// 底层操作成功返回码
        /// </summary>
        public const int FT_SUCCESS = 0;

        /// <summary>
        /// 错误码：无效的连接句柄
        /// </summary>
        public const int FT_ERR_INVALID_HANDLE = 0x8001;

        /// <summary>
        /// 错误码：无效的特性或指令不支持
        /// </summary>
        public const int FT_ERR_INVALID_FEATURE = 0x8002;

        /// <summary>
        /// 错误码：底层方法未实现
        /// </summary>
        public const int FT_ERR_NOTIMPLEMENTED = 0x8003;

        /// <summary>
        /// 错误码：无效的物理轴号
        /// </summary>
        public const int FT_ERR_INVALID_AXIS = 0x8004;
    }
}