using System;
using System.Text;
using MyRobotSDK.Models;
using MyRobotSDK.HAL;

namespace MyRobotSDK.Controllers
{
    /// <summary>
    /// 【单轴体控制】负责实现对单个特定轴体进行运动控制与状态查询。
    /// </summary>
    public sealed class AxisController
    {
        private readonly RobotController _robot;

        /// <summary>
        /// 当前控制器绑定的轴体。
        /// </summary>
        public RobotAxis Axis { get; }

        internal AxisController(RobotController robot, RobotAxis axis)
        {
            _robot = robot ?? throw new ArgumentNullException(nameof(robot));
            Axis = axis;
        }

        #region ================= 运动与使能控制 =================

        /// <summary>
        /// 使能
        /// 该函数无需调用
        /// </summary>
        /// <param name="enable">true表示使能，false表示失能</param>
        [Obsolete("危险操作，仅限调试", true)]
        public void SetEnabled(bool enable)
        {
            byte val = enable ? FtiConstants.FT_TRUE : FtiConstants.FT_FALSE;
            FtiMotionController.CheckError(
                FtiMotionController.fti_single_setenabled(_robot.Handle.Value, FtiMotionController.GetAxisStr(Axis), val),
                "SetEnabled", Axis.ToString());
        }

        /// <summary>
        /// 绝对运动，移动到指定的绝对位置坐标。
        /// </summary>
        /// <param name="position">绝对位置坐标</param>
        public void MoveAbsolute(float position)
        {
            FtiMotionController.CheckError(
                FtiMotionController.fti_single_moveabs(_robot.Handle.Value, FtiMotionController.GetAxisStr(Axis), position),
                "MoveAbsolute", Axis.ToString());
        }

        /// <summary>
        /// 相对运动，相对于当前位置移动指定的距离，正值表示正向移动，负值表示负向移动。
        /// </summary>
        /// <param name="distance">相对运动距离</param>
        public void MoveRelative(float distance)
        {
            FtiMotionController.CheckError(
                FtiMotionController.fti_single_move(_robot.Handle.Value, FtiMotionController.GetAxisStr(Axis), distance),
                "MoveRelative", Axis.ToString());
        }

        /// <summary>
        /// 停止运动，立即停止当前轴体的运动，进入停止状态。
        /// </summary>
        public void Stop()
        {
            FtiMotionController.CheckError(
                FtiMotionController.fti_single_stop(_robot.Handle.Value, FtiMotionController.GetAxisStr(Axis)),
                "Stop", Axis.ToString());
        }

        /// <summary>
        /// 向左（负向）连续运动，直到调用 Stop() 停止运动。
        /// </summary>
        public void JogLeft()
        {
            FtiMotionController.CheckError(
                FtiMotionController.fti_single_jogleft(_robot.Handle.Value, FtiMotionController.GetAxisStr(Axis)),
                "JogLeft", Axis.ToString());
        }

        /// <summary>
        /// 向右（正向）连续运动，直到调用 Stop() 停止运动。
        /// </summary>
        public void JogRight()
        {
            FtiMotionController.CheckError(
                FtiMotionController.fti_single_jogright(_robot.Handle.Value, FtiMotionController.GetAxisStr(Axis)),
                "JogRight", Axis.ToString());
        }

        /// <summary>
        /// 搜零，触发内置搜零程序
        /// 注：采用多圈绝对值编码器，不需要搜零，该函数无实际作用。
        /// </summary>
        [Obsolete("危险操作，仅限调试", true)]
        public void Home()
        {
            FtiMotionController.CheckError(
                FtiMotionController.fti_single_home(_robot.Handle.Value, FtiMotionController.GetAxisStr(Axis)),
                "Home", Axis.ToString());
        }

        /// <summary>
        /// 将当前位置设为零，若在运动则会先停止运动。
        /// 注：采用多圈绝对值编码器，不能置零，该函数无实际作用。
        /// </summary>
        [Obsolete("危险操作，仅限调试", true)]
        public void SetZero()
        {
            FtiMotionController.CheckError(
                FtiMotionController.fti_single_zero(_robot.Handle.Value, FtiMotionController.GetAxisStr(Axis)),
                "SetZero", Axis.ToString());
        }

        #endregion

        #region ================= 状态读取与解析 =================

        /// <summary>
        /// 获取当前绝对坐标值，单位为mm或°，表示当前轴体相对于机械零点的位置。
        /// </summary>
        /// <returns>当前位置</returns>
        public float GetPosition()
        {
            FtiMotionController.CheckError(
                FtiMotionController.fti_single_getpos(_robot.Handle.Value, FtiMotionController.GetAxisStr(Axis), out float pos),
                "GetPosition", Axis.ToString());
            return pos;
        }

        /// <summary>
        /// 查询是否正在运动，返回 true 表示正在运动，返回 false 表示停止状态。
        /// 特别说明：从状态值中进行解析，先通过底层拉取状态，再进行位运算。
        /// </summary>
        /// <returns>运行中返回 true，停止返回 false</returns>
        public bool IsRunning()
        {
            FtiMotionController.CheckError(
                FtiMotionController.fti_single_getstatus(_robot.Handle.Value, FtiMotionController.GetAxisStr(Axis), out uint status),
                "GetStatus", Axis.ToString());

            FtiMotionController.CheckError(
                FtiMotionController.fti_single_isrunning(status, out byte isRunning),
                "IsRunning", Axis.ToString());

            return isRunning == FtiConstants.FT_TRUE;
        }

        /// <summary>
        /// 获取限位状态，返回一个元组，包含正限位和负限位的状态，true表示对应限位触发，false表示未触发。
        /// 特别说明：从状态值中进行解析，先通过底层拉取状态，再提取限位信号。
        /// </summary>
        /// <returns>PosLimit: 正限位是否触发, NegLimit: 负限位是否触发</returns>
        public (bool PosLimit, bool NegLimit) GetLimits()
        {
            FtiMotionController.CheckError(
                FtiMotionController.fti_single_getstatus(_robot.Handle.Value, FtiMotionController.GetAxisStr(Axis), out uint status),
                "GetStatus", Axis.ToString());

            byte[] limits = new byte[2];
            FtiMotionController.CheckError(
                FtiMotionController.fti_single_getlimits(status, limits),
                "GetLimits", Axis.ToString());

            return (limits[1] == FtiConstants.FT_TRUE, limits[0] == FtiConstants.FT_TRUE);
        }

        /// <summary>
        /// 获取电机型号，可以用于确认是否正确连接了预期的电机，并进行调试或维护操作。
        /// </summary>
        /// <returns>电机型号字符串</returns>
        public string GetMotorModel()
        {
            byte[] buffer = new byte[256];
            FtiMotionController.CheckError(
                FtiMotionController.fti_get_motor_model(_robot.Handle.Value, FtiMotionController.GetAxisStr(Axis), buffer),
                "GetMotorModel", Axis.ToString());

            int length = 0;
            while (length < buffer.Length && buffer[length] >= 32 && buffer[length] <= 126) length++;
            return Encoding.ASCII.GetString(buffer, 0, length).Trim();
        }

        #endregion

        #region ================= 参数配置与持久化 =================

        /// <summary>
        /// 修改驱动器地址。出厂默认地址为 1。
        /// 不要随意调用该函数修改地址，除非你非常清楚自己在做什么，并且做好了记录新地址的准备。修改地址后，后续的通信都需要使用新地址，否则将无法通信。
        /// 特别说明：调用该函数修改驱动器地址后，后续的通信均需要使用新地址，务必记录好新地址。
        /// </summary>
        /// <param name="newAddress">驱动器的新地址</param>
        [Obsolete("危险操作，仅限调试", true)]
        public void ChangeAddress(ushort newAddress)
        {
            FtiMotionController.CheckError(
                FtiMotionController.fti_change_addr(_robot.Handle.Value, FtiMotionController.GetAxisStr(Axis), newAddress),
                "ChangeAddress", Axis.ToString());
        }

        /// <summary>
        /// 永久保存当前参数，断电重启后保持，但该函数会写入带记忆寄存器，存在擦写寿命限制，请谨慎使用。
        /// 特别说明：
        /// （1）带记忆寄存器的擦写寿命是10万次，请谨慎使用。建议不使用“自动保存参数”类似功能。
        /// （2）部分参数需要调用该函数才能断电保存，否则断电后丢失，如螺距、闭环中的PID参数等。
        /// （3）在未断电情况下，用户可以一次性把需要保存的参数设置好，再调用该函数。
        /// </summary>
        [Obsolete("危险操作，仅限调试", true)]
        public void SaveParamsPermanently()
        {
            FtiMotionController.CheckError(
                FtiMotionController.fti_save_params_permanently(_robot.Handle.Value, FtiMotionController.GetAxisStr(Axis)),
                "SaveParamsPermanently", Axis.ToString());
        }

        /// <summary>
        /// 设置目标轴体的预期运行速度，单位为mm/s或°/s，表示轴体运动的目标速度。
        /// </summary>
        /// <param name="velocity">目标运行速度</param>
        public void SetVelocity(float velocity)
        {
            FtiMotionController.CheckError(
                FtiMotionController.fti_set_vel(_robot.Handle.Value, FtiMotionController.GetAxisStr(Axis), velocity),
                "SetVelocity", Axis.ToString());
        }

        /// <summary>
        /// 获取目标轴体的当前运行速度，单位为mm/s或°/s，表示当前轴体的实际运行速度。
        /// </summary>
        /// <returns>当前运行速度</returns>
        public float GetVelocity()
        {
            FtiMotionController.CheckError(
                FtiMotionController.fti_get_vel(_robot.Handle.Value, FtiMotionController.GetAxisStr(Axis), out float val),
                "GetVelocity", Axis.ToString());
            return val;
        }

        /// <summary>
        /// 设置加速系数，单位为ms，表示电机从启动到达到目标速度所需的时间。
        /// 较小的加速系数会使电机更快地达到目标速度，但可能会增加机械应力和振动；
        /// 较大的加速系数会使电机更平稳地加速，但可能会增加达到目标速度的时间。
        /// </summary>
        /// <param name="accel">加速系数(ms)</param>
        [Obsolete("危险操作，仅限调试", true)]
        public void SetAcceleration(ushort accel)
        {
            FtiMotionController.CheckError(
                FtiMotionController.fti_set_accel(_robot.Handle.Value, FtiMotionController.GetAxisStr(Axis), accel),
                "SetAcceleration", Axis.ToString());
        }

        /// <summary>
        /// 获取加速系数，单位为ms，表示电机从启动到达到目标速度所需的时间。
        /// </summary>
        /// <returns>加速系数(ms)</returns>
        public ushort GetAcceleration()
        {
            FtiMotionController.CheckError(
                FtiMotionController.fti_get_accel(_robot.Handle.Value, FtiMotionController.GetAxisStr(Axis), out ushort val),
                "GetAcceleration", Axis.ToString());
            return val;
        }

        /// <summary>
        /// 设置减速系数，单位为ms，表示电机从目标速度到停止所需的时间。
        /// 较小的加速系数会使电机更快地达到目标速度，但可能会增加机械应力和振动；
        /// 较大的加速系数会使电机更平稳地加速，但可能会增加达到目标速度的时间。
        /// </summary>
        /// <param name="decel">减速系数(ms)</param>
        [Obsolete("危险操作，仅限调试", true)]
        public void SetDeceleration(ushort decel)
        {
            FtiMotionController.CheckError(
                FtiMotionController.fti_set_decel(_robot.Handle.Value, FtiMotionController.GetAxisStr(Axis), decel),
                "SetDeceleration", Axis.ToString());
        }

        /// <summary>
        /// 获取减速系数，单位为ms，表示电机从目标速度到停止所需的时间。
        /// </summary>
        /// <returns>减速系数(ms)</returns>
        public ushort GetDeceleration()
        {
            FtiMotionController.CheckError(
                FtiMotionController.fti_get_decel(_robot.Handle.Value, FtiMotionController.GetAxisStr(Axis), out ushort val),
                "GetDeceleration", Axis.ToString());
            return val;
        }

        /// <summary>
        /// 设置细分，单位为脉冲数，表示电机旋转一周所需的脉冲数。
        /// </summary>
        /// <param name="division">细分值，六个轴体的细分是定值，都是4000</param>
        [Obsolete("危险操作，仅限调试", true)]
        public void SetDivision(int division)
        {
            FtiMotionController.CheckError(
                FtiMotionController.fti_set_div(_robot.Handle.Value, FtiMotionController.GetAxisStr(Axis), division),
                "SetDivision", Axis.ToString());
        }

        /// <summary>
        /// 获取细分，单位为脉冲数，表示电机旋转一周所需的脉冲数。
        /// </summary>
        /// <returns>当前细分值</returns>
        public int GetDivision()
        {
            FtiMotionController.CheckError(
                FtiMotionController.fti_get_div(_robot.Handle.Value, FtiMotionController.GetAxisStr(Axis), out int val),
                "GetDivision", Axis.ToString());
            return val;
        }

        /// <summary>
        /// 设置螺距，单位为mm，表示电机旋转一周实际平移的距离。
        /// 特别说明：对于平移台，螺距是指电机旋转一周实际平移的距离。
        /// </summary>
        /// <param name="pitch">螺距，单位mm</param>
        [Obsolete("危险操作，仅限调试", true)]
        public void SetPitch(float pitch)
        {
            FtiMotionController.CheckError(
                FtiMotionController.fti_set_pitch(_robot.Handle.Value, FtiMotionController.GetAxisStr(Axis), pitch),
                "SetPitch", Axis.ToString());
        }

        /// <summary>
        /// 获取螺距，单位为mm，表示电机旋转一周实际平移的距离。
        /// 特别说明：对于平移台，螺距是指电机旋转一周实际平移的距离。
        /// </summary>
        /// <returns>螺距，单位mm</returns>
        public float GetPitch()
        {
            FtiMotionController.CheckError(
                FtiMotionController.fti_get_pitch(_robot.Handle.Value, FtiMotionController.GetAxisStr(Axis), out float val),
                "GetPitch", Axis.ToString());
            return val;
        }

        /// <summary>
        /// 设置软限位的位置下限，单位为mm或°，表示轴体运动的软限位下限位置。
        /// </summary>
        /// <param name="limit">软限位的位置下限</param>
        [Obsolete("危险操作，仅限调试", true)]
        public void SetSoftLimitP1(float limit)
        {
            FtiMotionController.CheckError(
                FtiMotionController.fti_set_sw_p1(_robot.Handle.Value, FtiMotionController.GetAxisStr(Axis), limit),
                "SetSoftLimitP1", Axis.ToString());
        }

        /// <summary>
        /// 获取软限位的位置下限，单位为mm或°，表示轴体运动的软限位下限位置。
        /// </summary>
        /// <returns>软限位的位置下限</returns>
        public float GetSoftLimitP1()
        {
            FtiMotionController.CheckError(
                FtiMotionController.fti_get_sw_p1(_robot.Handle.Value, FtiMotionController.GetAxisStr(Axis), out float val),
                "GetSoftLimitP1", Axis.ToString());
            return val;
        }

        /// <summary>
        /// 设置软限位的位置上限，单位为mm或°，表示轴体运动的软限位上限位置。
        /// </summary>
        /// <param name="limit">软限位的位置上限</param>
        [Obsolete("危险操作，仅限调试", true)]
        public void SetSoftLimitP2(float limit)
        {
            FtiMotionController.CheckError(
                FtiMotionController.fti_set_sw_p2(_robot.Handle.Value, FtiMotionController.GetAxisStr(Axis), limit),
                "SetSoftLimitP2", Axis.ToString());
        }

        /// <summary>
        /// 获取软限位的位置上限，单位为mm或°，表示轴体运动的软限位上限位置。
        /// </summary>
        /// <returns>软限位的位置上限</returns>
        public float GetSoftLimitP2()
        {
            FtiMotionController.CheckError(
                FtiMotionController.fti_get_sw_p2(_robot.Handle.Value, FtiMotionController.GetAxisStr(Axis), out float val),
                "GetSoftLimitP2", Axis.ToString());
            return val;
        }

        #endregion

        #region ================= 高级底层寄存器操作 =================

        /// <summary>
        /// 设置寄存器的值，UINT32类型。
        /// 特别说明：请谨慎使用，确保地址正确、类型正确。
        /// </summary>
        /// <param name="regAddr">寄存器地址</param>
        /// <param name="value">数值，UINT32</param>
        [Obsolete("危险操作，仅限调试", true)]
        public void SetRegisterUInt32(int regAddr, uint value)
        {
            FtiMotionController.CheckError(
                FtiMotionController.fti_set_uint32(_robot.Handle.Value, FtiMotionController.GetAxisStr(Axis), regAddr, value),
                "SetRegisterUInt32", Axis.ToString());
        }

        /// <summary>
        /// 读取寄存器的值，UINT32类型。
        /// 特别说明：请谨慎使用，确保地址正确、类型正确。
        /// </summary>
        /// <param name="regAddr">寄存器地址</param>
        /// <returns>当前数值</returns>
        [Obsolete("危险操作，仅限调试", true)]
        public uint GetRegisterUInt32(int regAddr)
        {
            FtiMotionController.CheckError(
                FtiMotionController.fti_get_uint32(_robot.Handle.Value, FtiMotionController.GetAxisStr(Axis), regAddr, out uint val),
                "GetRegisterUInt32", Axis.ToString());
            return val;
        }

        /// <summary>
        /// 设置寄存器的值，UINT16类型。
        /// 特别说明：请谨慎使用，确保地址正确、类型正确。
        /// </summary>
        /// <param name="regAddr">寄存器地址</param>
        /// <param name="value">数值，UINT16</param>
        [Obsolete("危险操作，仅限调试", true)]
        public void SetRegisterUInt16(int regAddr, ushort value)
        {
            FtiMotionController.CheckError(
                FtiMotionController.fti_set_uint16(_robot.Handle.Value, FtiMotionController.GetAxisStr(Axis), regAddr, value),
                "SetRegisterUInt16", Axis.ToString());
        }

        /// <summary>
        /// 读取寄存器的值，UINT16类型。
        /// 特别说明：请谨慎使用，确保地址正确、类型正确。
        /// </summary>
        /// <param name="regAddr">寄存器地址</param>
        /// <returns>当前数值，UINT16</returns>
        [Obsolete("危险操作，仅限调试", true)]
        public ushort GetRegisterUInt16(int regAddr)
        {
            FtiMotionController.CheckError(
                FtiMotionController.fti_get_uint16(_robot.Handle.Value, FtiMotionController.GetAxisStr(Axis), regAddr, out ushort val),
                "GetRegisterUInt16", Axis.ToString());
            return val;
        }

        #endregion
    }
}