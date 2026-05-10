using MyRobotSDK.Controllers;
using MyRobotSDK.Models;
using System;

namespace SAMCS_WPF.Services
{
    /// <summary>
    /// 机器人硬件全局控制服务接口
    /// 负责规范所有涉及底层硬件生命周期和分配的行为
    /// </summary>
    public interface IRobotControlService : IDisposable
    {
        /// <summary>
        /// 查询当前系统是否已成功与底层硬件建立连接
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// 建立硬件连接
        /// </summary>
        /// <param name="port">系统串口号，例如 "COM5"</param>
        /// <param name="baud">通信波特率，例如 115200</param>
        void Connect(string port, int baud);

        /// <summary>
        /// 获取单个目标轴的独立控制权
        /// 拿到该实例后，方可进行运动、速度设定与状态查询
        /// </summary>
        /// <param name="axis">物理轴向枚举 (Axis1 ~ Axis6)</param>
        /// <returns>单轴控制器执行实例</returns>
        AxisController GetAxis(RobotAxis axis);
    }
}