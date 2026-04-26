using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using MyRobotSDK.Models;
using MyRobotSDK.Exceptions;

namespace MyRobotSDK.HAL
{
    /// <summary>
    /// 内部底层通信层，不允许外部 WPF 直接调用。
    /// </summary>
    internal static partial class FtiMotionController
    {
        private const string DLL_NAME = "ftcoreimc.dll";

        static FtiMotionController()
        {
            NativeLibrary.SetDllImportResolver(typeof(FtiMotionController).Assembly, ImportResolver);
        }

        private static IntPtr ImportResolver(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
        {
            if (libraryName == DLL_NAME)
            {
                // 全面拥抱 x64，直接写死路径，干掉判断机制
                string libPath = Path.Combine(Environment.CurrentDirectory, "lib", "x64", DLL_NAME);
                if (NativeLibrary.TryLoad(libPath, out IntPtr handle)) return handle;

                throw new DllNotFoundException($"【FTI底层通信库加载失败】请检查 lib/x64/{DLL_NAME} 是否存在。尝试加载路径: {libPath}");
            }
            return IntPtr.Zero;
        }

        // ================= 全量 P/Invoke 声明 =================
        [LibraryImport(DLL_NAME)] internal static partial IntPtr fti_getsdkversion();
        [LibraryImport(DLL_NAME, StringMarshalling = StringMarshalling.Utf8)] internal static partial int fti_open_imc(string comport_or_ip, int baud_or_port, out ulong handle);
        [LibraryImport(DLL_NAME)] internal static partial int fti_close(ulong handle);
        [LibraryImport(DLL_NAME, StringMarshalling = StringMarshalling.Utf8)] internal static partial int fti_change_addr(ulong handle, string axis, ushort value);
        [LibraryImport(DLL_NAME, StringMarshalling = StringMarshalling.Utf8)] internal static partial int fti_save_params_permanently(ulong handle, string axis);
        [LibraryImport(DLL_NAME, StringMarshalling = StringMarshalling.Utf8)] internal static partial int fti_set_accel(ulong handle, string axis, ushort value);
        [LibraryImport(DLL_NAME, StringMarshalling = StringMarshalling.Utf8)] internal static partial int fti_get_accel(ulong handle, string axis, out ushort value);
        [LibraryImport(DLL_NAME, StringMarshalling = StringMarshalling.Utf8)] internal static partial int fti_set_decel(ulong handle, string axis, ushort value);
        [LibraryImport(DLL_NAME, StringMarshalling = StringMarshalling.Utf8)] internal static partial int fti_get_decel(ulong handle, string axis, out ushort value);
        [LibraryImport(DLL_NAME, StringMarshalling = StringMarshalling.Utf8)] internal static partial int fti_set_div(ulong handle, string axis, int value);
        [LibraryImport(DLL_NAME, StringMarshalling = StringMarshalling.Utf8)] internal static partial int fti_get_div(ulong handle, string axis, out int value);
        [LibraryImport(DLL_NAME, StringMarshalling = StringMarshalling.Utf8)] internal static partial int fti_set_pitch(ulong handle, string axis, float value);
        [LibraryImport(DLL_NAME, StringMarshalling = StringMarshalling.Utf8)] internal static partial int fti_get_pitch(ulong handle, string axis, out float value);
        [LibraryImport(DLL_NAME, StringMarshalling = StringMarshalling.Utf8)] internal static partial int fti_set_vel(ulong handle, string axis, float value);
        [LibraryImport(DLL_NAME, StringMarshalling = StringMarshalling.Utf8)] internal static partial int fti_get_vel(ulong handle, string axis, out float value);
        [LibraryImport(DLL_NAME, StringMarshalling = StringMarshalling.Utf8)] internal static partial int fti_set_sw_p1(ulong handle, string axis, float value);
        [LibraryImport(DLL_NAME, StringMarshalling = StringMarshalling.Utf8)] internal static partial int fti_get_sw_p1(ulong handle, string axis, out float value);
        [LibraryImport(DLL_NAME, StringMarshalling = StringMarshalling.Utf8)] internal static partial int fti_set_sw_p2(ulong handle, string axis, float value);
        [LibraryImport(DLL_NAME, StringMarshalling = StringMarshalling.Utf8)] internal static partial int fti_get_sw_p2(ulong handle, string axis, out float value);
        [LibraryImport(DLL_NAME, StringMarshalling = StringMarshalling.Utf8)] internal static partial int fti_single_stop(ulong handle, string axis);
        [LibraryImport(DLL_NAME, StringMarshalling = StringMarshalling.Utf8)] internal static partial int fti_single_zero(ulong handle, string axis);
        [LibraryImport(DLL_NAME, StringMarshalling = StringMarshalling.Utf8)] internal static partial int fti_single_home(ulong handle, string axis);
        [LibraryImport(DLL_NAME, StringMarshalling = StringMarshalling.Utf8)] internal static partial int fti_single_move(ulong handle, string axis, float value);
        [LibraryImport(DLL_NAME, StringMarshalling = StringMarshalling.Utf8)] internal static partial int fti_single_moveabs(ulong handle, string axis, float value);
        [LibraryImport(DLL_NAME, StringMarshalling = StringMarshalling.Utf8)] internal static partial int fti_single_jogleft(ulong handle, string axis);
        [LibraryImport(DLL_NAME, StringMarshalling = StringMarshalling.Utf8)] internal static partial int fti_single_jogright(ulong handle, string axis);
        [LibraryImport(DLL_NAME, StringMarshalling = StringMarshalling.Utf8)] internal static partial int fti_single_getstatus(ulong handle, string axis, out uint value);
        [LibraryImport(DLL_NAME)] internal static partial int fti_single_isrunning(uint status, out byte value);
        [LibraryImport(DLL_NAME)] internal static partial int fti_single_getlimits(uint status, [Out] byte[] value);
        [LibraryImport(DLL_NAME, StringMarshalling = StringMarshalling.Utf8)] internal static partial int fti_single_getpos(ulong handle, string axis, out float value);
        [LibraryImport(DLL_NAME, StringMarshalling = StringMarshalling.Utf8)] internal static partial int fti_single_setenabled(ulong handle, string axis, byte value);
        [LibraryImport(DLL_NAME, StringMarshalling = StringMarshalling.Utf8)] internal static partial int fti_get_motor_model(ulong handle, string axis, [Out] byte[] value);
        [LibraryImport(DLL_NAME, StringMarshalling = StringMarshalling.Utf8)] internal static partial int fti_set_uint32(ulong handle, string axis, int reg_addr, uint value);
        [LibraryImport(DLL_NAME, StringMarshalling = StringMarshalling.Utf8)] internal static partial int fti_get_uint32(ulong handle, string axis, int reg_addr, out uint value);
        [LibraryImport(DLL_NAME, StringMarshalling = StringMarshalling.Utf8)] internal static partial int fti_set_uint16(ulong handle, string axis, int reg_addr, ushort value);
        [LibraryImport(DLL_NAME, StringMarshalling = StringMarshalling.Utf8)] internal static partial int fti_get_uint16(ulong handle, string axis, int reg_addr, out ushort value);

        // ================= C# 内部安全辅助 =================
        internal static void CheckError(int errorCode, string operationName, string? targetAxis = null)
        {
            if (errorCode != FtiConstants.FT_SUCCESS)
            {
                string msg = targetAxis == null ? operationName : $"{operationName} 操作失败，目标轴：{targetAxis}";
                throw new FtiException(errorCode, msg);
            }
        }

        internal static string GetAxisStr(RobotAxis axis)
        {
            return ((int)axis).ToString("D2"); // 转为 01, 02 格式
        }
    }
}