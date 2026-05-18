using System.Collections.Generic;
using System.Threading.Tasks;
using SAMCS_WPF.Models;

namespace SAMCS_WPF.Services
{
    /// <summary>
    /// ===== 白话文说明：什么是接口（Interface） =====
    /// 接口就像一份"合同"或"菜单"。
    /// 它只规定了"有哪些方法可以调用"，不规定"这些方法怎么实现"。
    ///
    /// 为什么需要接口：
    ///   1. ViewModel 只知道接口，不需要知道实现细节
    ///   2. 如果以后想换一种实现方式（比如用网络代替串口），
    ///      只需要写一个新的实现类，ViewModel 不用改一行代码
    ///   3. DI 容器根据接口自动找到实现类并注入
    ///
    /// 这个接口定义了：轴运动工作流的所有操作。
    /// 具体实现在 AxisWorkflowService.cs 中。
    /// </summary>
    public interface IAxisWorkflowService
    {
        /// <summary>
        /// 当前是否已连接硬件。
        /// true = 已连接，false = 未连接。
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// 获取六轴静态定义列表。
        /// 返回的数据包含每个轴的名称、限位值、归位坐标等产品规格参数。
        /// ViewModel 用这个列表来初始化 UI 显示的轴参数。
        /// </summary>
        IReadOnlyList<AxisDefinition> GetAxisDefinitions();

        /// <summary>
        /// 建立串口连接，并逐轴校验电机型号是否与预设一致。
        /// 校验失败时会自动断开连接并弹出提示框。
        ///
        /// 参数 port：串口号，例如 "COM5"。
        /// 波特率在实现内部固定为 115200（硬件固件要求）。
        /// </summary>
        void ConnectAndValidate(string port);

        /// <summary>
        /// 断开硬件连接，释放 SDK 占用的资源（串口句柄等）。
        /// </summary>
        void Disconnect();

        /// <summary>
        /// 六轴同步归位 —— 所有轴同时启动，统一等待全部停止。
        /// 内部步骤：设速度 → 下发运动指令 → 轮询等待全部停止。
        /// 这是一个异步方法（async Task），执行期间不阻塞 UI。
        /// </summary>
        Task HomeAllAxesAsync();

        /// <summary>
        /// 六轴限位往返测试 —— 每个轴依次执行三段运动：
        ///   1. 运动到上限位 → 等待停止 → 延时 3 秒
        ///   2. 运动到下限位 → 等待停止 → 延时 3 秒
        ///   3. 运动到默认归位 → 等待停止 → 延时 3 秒
        /// 这是一个异步方法（async Task），执行期间不阻塞 UI。
        /// </summary>
        Task TestAllAxesAsync();

        /// <summary>
        /// 紧急停止 —— 向全部六个轴同时发送 Stop 指令。
        /// 不等待、不轮询，瞬间群发。
        /// 注意：这是同步方法，执行极快，不需要 async。
        /// </summary>
        void EmergencyStop();

        /// <summary>
        /// 高频实时数据读取 —— 每 100ms 调用一次。
        /// 读取内容：位置、速度、运行状态（三个最常变化的值）。
        /// 通信异常时返回空列表，调用方应跳过本轮刷新而不是崩溃。
        /// </summary>
        IReadOnlyList<AxisRuntimeState> ReadRealtime();

        /// <summary>
        /// 低频配置数据读取 —— 每 60 秒调用一次。
        /// 读取内容：加速度、减速度、螺距、细分（四个不常变的值）。
        /// 通信异常时返回空列表。
        /// </summary>
        IReadOnlyList<AxisRuntimeState> ReadConfiguration();
    }
}
