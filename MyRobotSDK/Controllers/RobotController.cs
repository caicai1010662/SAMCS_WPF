using System;
using System.Threading;
using MyRobotSDK.Models;
using MyRobotSDK.HAL;

namespace MyRobotSDK.Controllers
{
    /// <summary>
    /// 【核心类】六轴机器人主控制器。
    /// 负责管理与底层硬件的通信生命周期。
    /// </summary>
    public sealed class RobotController : IDisposable
    {
        internal FtiHandle Handle { get; }
        private volatile int _disposed = 0;

        private RobotController(FtiHandle handle)
        {
            Handle = handle;
        }

        /// <summary>
        /// 获取底层驱动库的 SDK 版本号，如“2.2.2.0”。
        /// </summary>
        public static string? SdkVersion => FtiMotionController.fti_getsdkversion() != IntPtr.Zero
            ? System.Runtime.InteropServices.Marshal.PtrToStringAnsi(FtiMotionController.fti_getsdkversion())
            : null;

        /// <summary>
        /// 连接控制器。
        /// （1）串口连接时，使用 Modbus-RTU，“COM口+波特率”，COM5 + 115200。
        /// （2）网络连接时，使用 Modbus-TCP，“IP地址+端口号”，192.168.0.11 + 10001。
        /// （3）modbus 通信中的地址通过轴号体现，即与轴 01 通信时地址使用 01。
        ///  特别说明：Connect 函数仅仅建立连接，并未实际通信，此函数执行成功并不能判定与控制器的通信连接正常，可通过读取位置，进行实际通信以判定与控制器的通信是否正常。
        /// </summary>
        /// <param name="comportOrIp">串口设备标识或IP地址</param>
        /// <param name="baudOrPort">串口通信波特率或网络通信端口号</param>
        /// <returns>实例化后的主控制器对象</returns>
        public static RobotController Connect(string comportOrIp, int baudOrPort)
        {
            FtiMotionController.CheckError(
                FtiMotionController.fti_open_imc(comportOrIp, baudOrPort, out ulong handleVal),
                "Connect");
            return new RobotController(new FtiHandle(handleVal));
        }

        /// <summary>
        /// 获取特定轴体的控制器对象。
        /// 通过此对象可以下发针对单轴的运动和配置指令。
        /// </summary>
        /// <param name="axis">目标轴体</param>
        /// <returns>单轴控制器实例</returns>
        public AxisController GetAxis(RobotAxis axis)
        {
            ObjectDisposedException.ThrowIf(_disposed == 1, this);
            return new AxisController(this, axis);
        }

        /// <summary>
        /// 关闭连接并释放底层通信句柄。
        /// </summary>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
            {
                if (Handle.IsValid)
                {
                    // 接收错误码
                    int result = FtiMotionController.fti_close(Handle.Value);

                    if (result != HAL.FtiConstants.FT_SUCCESS)
                    {
                        System.Diagnostics.Debug.WriteLine($"[警告] 释放机器人连接句柄时发生错误。错误码: 0x{result:X4}");
                    }
                }
            }
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 析构函数，作为资源释放的最后防线。
        /// </summary>
        ~RobotController() => Dispose();
    }
}