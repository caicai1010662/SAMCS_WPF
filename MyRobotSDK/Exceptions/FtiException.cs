using System;

namespace MyRobotSDK.Exceptions
{
    /// <summary>
    /// 【功能】自定义硬件通信异常。
    /// 【特别说明】
    /// （1）用于将底层返回的整型错误码（如 0x8001）统一包装为符合 C# 规范的标准异常对象。
    /// （2）建议在 WPF 上位机的 ViewModel 层级进行全局 try-catch 拦截，并提取 Message 提示给操作人员。
    /// </summary>
    public class FtiException : Exception
    {
        /// <summary>
        /// 原厂底层的 16 进制错误代码，用于查阅硬件故障手册。
        /// </summary>
        public int ErrorCode { get; }

        /// <summary>
        /// 构造函数：初始化带有特定底层错误码和描述的硬件异常。
        /// </summary>
        /// <param name="errorCode">底层返回的整型错误码 (如 32769)</param>
        /// <param name="message">当前业务操作的描述上下文</param>
        public FtiException(int errorCode, string message)
            : base($"[FTI Error: 0x{errorCode:X4}] {message}")
        {
            ErrorCode = errorCode;
        }

        /// <summary>
        /// 构造函数：包含内部异常信息，用于异常链的追踪。
        /// </summary>
        public FtiException(int errorCode, string message, Exception innerException)
            : base($"[FTI Error: 0x{errorCode:X4}] {message}", innerException)
        {
            ErrorCode = errorCode;
        }
    }
}