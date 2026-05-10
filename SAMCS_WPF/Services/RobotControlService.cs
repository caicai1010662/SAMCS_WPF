using System;
using MyRobotSDK.Controllers;
using MyRobotSDK.Models;

namespace SAMCS_WPF.Services
{
    /// <summary>
    /// 机器人硬件控制服务 —— 封装 RobotController 生命周期与单轴控制器分配。
    /// </summary>
    public class RobotControlService : IRobotControlService, IDisposable
    {
        private RobotController? _robot;

        /// <summary>
        /// 当前是否与硬件建立有效连接
        /// </summary>
        public bool IsConnected => _robot != null;

        /// <summary>
        /// 建立串口连接，若已有连接则先释放
        /// </summary>
        public void Connect(string port, int baud)
        {
            Dispose();
            _robot = RobotController.Connect(port, baud);
        }

        /// <summary>
        /// 获取指定轴控制器，未连接时抛出异常
        /// </summary>
        public AxisController GetAxis(RobotAxis axis)
        {
            if (_robot == null)
                throw new InvalidOperationException("机器人未连接");

            return _robot.GetAxis(axis);
        }

        /// <summary>
        /// 断开连接并释放 SDK 资源
        /// </summary>
        public void Dispose()
        {
            _robot?.Dispose();
            _robot = null;
        }
    }
}
