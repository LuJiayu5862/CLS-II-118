using System;
using System.Runtime.InteropServices;

namespace CLS_II
{
    // ============================================================
    //  JD-61101  接收数据仓
    //  PLC → PC，20字节固定帧，端口 JdConsts.nJdPortRecv
    // ============================================================

    /// <summary>
    /// JD-61101 上报帧（20 bytes，Pack=4）
    /// 字段顺序与 PLC 内存布局严格对应
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct _JdFeedback
    {
        public UInt32 frameId;      // 帧序号
        public Single pedalPos;     // 踏板位置 [mm]
        public Single pedalForce;   // 踏板力   [N]
        public Single pedalVel;     // 踏板速度 [mm/s]
        public UInt32 jdStatus;     // 状态字：bit0=fault, bit1=enable, bit2=homed
    }

    /// <summary>
    /// JD-61101 发送帧（12 bytes，Pack=4）
    /// PC → PLC，端口 JdConsts.nJdPortSend
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct _JdCommand
    {
        public UInt32 cmdId;        // 命令序号（自增）
        public UInt32 cmdCode;      // 0=Idle, 1=Reset, 2=Enable, 3=Disable
        public Single setPoint;     // 目标位置指令 [mm]（如不用则保持0）
    }

    /// <summary>
    /// JD 通道全局数据仓（线程安全用 lock(JdData.Feedback)）
    /// </summary>
    static class JdData
    {
        public static _JdFeedback Feedback = new _JdFeedback();
        public static _JdCommand  Command  = new _JdCommand();

        /// <summary>收到新帧时由 MainForm.JdUDP 写入，外部只读</summary>
        public static DateTime LastReceivedTime { get; internal set; } = DateTime.MinValue;

        /// <summary>连续未收到帧超过此毫秒数视为超时</summary>
        public const int TimeoutMs = 500;

        public static bool IsTimeout =>
            LastReceivedTime == DateTime.MinValue ||
            (DateTime.Now - LastReceivedTime).TotalMilliseconds > TimeoutMs;
    }
}
