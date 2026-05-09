using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace CLS_II
{
    // ============================================================
    //  TcLCS v1.1  参数通道数据仓
    //  对应文档  TcLCS-UDP_Protocol_v1.1.docx
    // ============================================================

    /// <summary>
    /// v1.1 协议报文头（16 bytes，Pack=1）
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct _ParamHeader
    {
        public UInt32 Magic;       // 0x544C4353 = 'TLCS'
        public UInt16 Version;     // 0x0101
        public UInt16 MsgType;     // 见 ParamMsgType
        public UInt32 SeqId;       // 流水号（自增）
        public UInt32 PayloadLen;  // Payload 字节数
    }

    public static class ParamMsgType
    {
        public const UInt16 GET_REQ = 0x0001;
        public const UInt16 GET_RSP = 0x0002;
        public const UInt16 SET_REQ = 0x0003;
        public const UInt16 SET_RSP = 0x0004;
        public const UInt16 ACK     = 0x0005;
        public const UInt16 NACK    = 0x0006;
    }

    /// <summary>
    /// SubID 枚举（对应 AppendixC，按需扩充）
    /// </summary>
    public enum ParamSubId : UInt16
    {
        ChannelEnable = 0x0001,
        SafetyLimit   = 0x0010,
        GainForward   = 0x0020,
        GainAft       = 0x0021,
        DampCoeff     = 0x0030,
        TrimOffset    = 0x0040,
        BrakeLevel    = 0x0050,
    }

    /// <summary>
    /// 单条参数记录（用于 ParamData.Cache 字典）
    /// </summary>
    public class ParamEntry
    {
        public ParamSubId SubId        { get; set; }
        public int        ChannelId    { get; set; }  // 0-based；-1 表示全局
        public string     Name         { get; set; }
        public float      Value        { get; set; }
        public float      PendingValue { get; set; }
        public bool       IsDirty      { get; set; }  // true = 待发送
        public DateTime   LastUpdated  { get; set; } = DateTime.MinValue;
    }

    /// <summary>
    /// TcLCS 参数通道全局数据仓
    /// Key: (SubId, ChannelId)
    /// </summary>
    static class ParamData
    {
        // 参数缓存，线程安全用 lock(Cache)
        public static readonly Dictionary<(ParamSubId, int), ParamEntry> Cache
            = new Dictionary<(ParamSubId, int), ParamEntry>();

        // 最近一次收到的响应报文头（调试用）
        public static _ParamHeader LastRspHeader;

        // 最近一次发送的流水号
        public static UInt32 LastSeqId { get; internal set; } = 0;

        /// <summary>
        /// 初始化缓存（为每个已知 SubId × 10通道 预分配条目）
        /// 在 MainForm_Load 中调用一次
        /// </summary>
        public static void Init()
        {
            lock (Cache)
            {
                if (Cache.Count > 0) return;
                foreach (ParamSubId sid in Enum.GetValues(typeof(ParamSubId)))
                {
                    for (int ch = 0; ch < CLSConsts.TotalChannels; ch++)
                        Cache[(sid, ch)] = new ParamEntry
                        {
                            SubId = sid, ChannelId = ch,
                            Name  = $"{sid}_CH{ch + 1}", Value = 0f,
                        };
                    // 全局条目
                    Cache[(sid, -1)] = new ParamEntry
                    {
                        SubId = sid, ChannelId = -1,
                        Name  = $"{sid}_Global", Value = 0f,
                    };
                }
            }
        }

        /// <summary>标记某条目为待发送</summary>
        public static void SetDirty(ParamSubId sid, int ch, float newValue)
        {
            lock (Cache)
            {
                if (Cache.TryGetValue((sid, ch), out var entry))
                { entry.PendingValue = newValue; entry.IsDirty = true; }
            }
        }

        /// <summary>从响应帧更新缓存</summary>
        public static void UpdateFromResponse(ParamSubId sid, int ch, float value)
        {
            lock (Cache)
            {
                if (Cache.TryGetValue((sid, ch), out var entry))
                { entry.Value = value; entry.IsDirty = false; entry.LastUpdated = DateTime.Now; }
            }
        }
    }
}
