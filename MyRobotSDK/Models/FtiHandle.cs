using System;

namespace MyRobotSDK.Models
{
    /// <summary>
    /// 【功能】强类型设备句柄。
    /// 【特别说明】
    /// （1）此结构用于包装底层 64 位无符号长整型 (ulong) 的设备指针，防止开发者将普通数值误传到 P/Invoke 接口中。
    /// （2）提供了值相等性比较，方便后续在集合或字典中管理多台控制器的并发连接状态。
    /// </summary>
    public readonly struct FtiHandle(ulong value) : IEquatable<FtiHandle>
    {
        /// <summary>
        /// 原始底层通信句柄值。
        /// </summary>
        public ulong Value { get; } = value;

        /// <summary>
        /// 判断当前通信句柄是否有效（非 0 即认为可能有效）。
        /// </summary>
        public bool IsValid => Value != 0;

        /// <summary>
        /// 指示当前句柄是否等于另一个同类型句柄。
        /// </summary>
        public bool Equals(FtiHandle other) => Value == other.Value;

        /// <summary>
        /// 指示当前句柄是否等于另一个未知类型对象。
        /// </summary>
        public override bool Equals(object? obj) => obj is FtiHandle other && Equals(other);

        /// <summary>
        /// 获取当前句柄的哈希代码。
        /// </summary>
        public override int GetHashCode() => Value.GetHashCode();

        /// <summary>
        /// 判断两个句柄是否指向同一个底层设备引用。
        /// </summary>
        public static bool operator ==(FtiHandle left, FtiHandle right) => left.Equals(right);

        /// <summary>
        /// 判断两个句柄是否指向不同的底层设备引用。
        /// </summary>
        public static bool operator !=(FtiHandle left, FtiHandle right) => !(left == right);
    }
}