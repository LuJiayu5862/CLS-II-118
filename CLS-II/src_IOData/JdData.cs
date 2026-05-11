// ============================================================================
//  JdData.cs  —  JD-61101 UDP Data Channel Frame Definitions
//
//  严格依据：CLS-II/_docs/JD-61101-UDP通信协议.docx
//  用户确认（2026-05-11）：
//    - 脚蹬位移字段 = DATA7~DATA10，int32，大端（高位在前）
//    - 范围 [-18000, +18000]，左脚向前负，右脚向前正
//
//  帧长固定 20 字节（DATA0 ~ DATA19）
//  PLC → 上位机（RX）帧头：0xA5 0xA5 0xA5
//  上位机 → PLC（TX）帧头：0x5A 0x5A 0x5A
//  校验：DATA19 = SUM(DATA3..DATA18) & 0xFF
// ============================================================================
using System;
using System.Buffers.Binary;

namespace CLS_II
{
    /// <summary>JD-61101 协议常量（来自协议文档原文）</summary>
    public static class JdConstants
    {
        // ---- 帧头 ----
        public const byte SOF_RX_FROM_PLC = 0xA5; // PLC → 上位机 (DATA0..DATA2)
        public const byte SOF_TX_TO_PLC = 0x5A; // 上位机 → PLC (DATA0..DATA2)

        // ---- 固定字段 ----
        public const byte DEVICE_NO = 0x01; // DATA3
        public const byte DATA_LEN = 0x0E; // DATA4 = 14

        // ---- 长度 ----
        public const int FRAME_LEN = 20; // DATA0..DATA19
        public const int SOF_LEN = 3;  // DATA0..DATA2
        public const int CHECKSUM_IDX = 19; // DATA19
        public const int CHECKSUM_RANGE_START = 3;  // DATA3
        public const int CHECKSUM_RANGE_END = 18; // DATA18 (含)

        // ---- 位移字段（int32 BE，DATA7..DATA10）----
        public const int POS_OFFSET = 7;
        public const int POS_SIZE = 4;
        public const int POS_MIN = -18000;
        public const int POS_MAX = 18000;

        // ---- 状态字节 ----
        public const byte STATUS_OK = 0x00; // DATA5 正常
        public const byte CMD_RESET_FAULT = 0xAA; // 上位机→PLC DATA5：清除故障码
        public const byte CMD_RESET_PEDAL_ZERO = 0xAA; // 上位机→PLC DATA7：操纵负荷复位回中立位
    }

    /// <summary>PLC → 上位机 发送帧（脚蹬状态+位移）</summary>
    public sealed class JdRxFrame
    {
        public byte DeviceNo { get; set; } = JdConstants.DEVICE_NO;
        public byte DataLen { get; set; } = JdConstants.DATA_LEN;
        /// <summary>DATA5: 0x00=正常；其他=故障码</summary>
        public byte Status { get; set; }
        /// <summary>DATA7..DATA10: int32 大端，[-18000, +18000]；左脚向前负，右脚向前正</summary>
        public int PedalPosition { get; set; }
        public byte Checksum { get; set; }

        public bool IsFault => Status != JdConstants.STATUS_OK;
    }

    /// <summary>上位机 → PLC 发送帧（复位控制）</summary>
    public sealed class JdTxFrame
    {
        public byte DeviceNo { get; set; } = JdConstants.DEVICE_NO;
        public byte DataLen { get; set; } = JdConstants.DATA_LEN;
        /// <summary>DATA5: 0x00=正常；0xAA=清除故障码</summary>
        public byte ClearFault { get; set; } = 0x00;
        /// <summary>DATA7: 0x00=正常；0xAA=操纵负荷复位并回中立位（零位）</summary>
        public byte ResetPedal { get; set; } = 0x00;
    }

    /// <summary>JD-61101 和校验 / 编解码工具</summary>
    public static class JdCodec
    {
        /// <summary>SUM(DATA3..DATA18) & 0xFF</summary>
        public static byte Checksum(ReadOnlySpan<byte> frame20)
        {
            int sum = 0;
            for (int i = JdConstants.CHECKSUM_RANGE_START; i <= JdConstants.CHECKSUM_RANGE_END; i++)
                sum += frame20[i];
            return (byte)(sum & 0xFF);
        }

        /// <summary>构建 上位机→PLC 20B 帧（0x5A 头）</summary>
        public static byte[] BuildTx(JdTxFrame f)
        {
            var buf = new byte[JdConstants.FRAME_LEN];
            buf[0] = buf[1] = buf[2] = JdConstants.SOF_TX_TO_PLC;
            buf[3] = f.DeviceNo;
            buf[4] = f.DataLen;
            buf[5] = f.ClearFault;
            buf[6] = 0x00;
            buf[7] = f.ResetPedal;
            // DATA8..DATA18 全 0（已由 new byte[] 初始化）
            buf[JdConstants.CHECKSUM_IDX] = Checksum(buf);
            return buf;
        }

        /// <summary>解析 PLC→上位机 20B 帧（0xA5 头）；返回 null 表示帧结构错误</summary>
        public static JdRxFrame? TryParseRx(ReadOnlySpan<byte> buf, out string? error)
        {
            error = null;
            if (buf.Length != JdConstants.FRAME_LEN) { error = $"BAD_LEN: {buf.Length}"; return null; }
            if (buf[0] != JdConstants.SOF_RX_FROM_PLC ||
                buf[1] != JdConstants.SOF_RX_FROM_PLC ||
                buf[2] != JdConstants.SOF_RX_FROM_PLC) { error = "BAD_SOF"; return null; }
            if (buf[3] != JdConstants.DEVICE_NO) { error = $"BAD_DEV:{buf[3]:X2}"; return null; }
            if (buf[4] != JdConstants.DATA_LEN) { error = $"BAD_DLEN:{buf[4]:X2}"; return null; }

            byte cs = Checksum(buf);
            if (cs != buf[JdConstants.CHECKSUM_IDX]) { error = $"BAD_CHK:calc={cs:X2} got={buf[JdConstants.CHECKSUM_IDX]:X2}"; return null; }

            // 位移：int32 大端，DATA7..DATA10
            int pos = BinaryPrimitives.ReadInt32BigEndian(buf.Slice(JdConstants.POS_OFFSET, JdConstants.POS_SIZE));

            return new JdRxFrame
            {
                DeviceNo = buf[3],
                DataLen = buf[4],
                Status = buf[5],
                PedalPosition = pos,
                Checksum = buf[JdConstants.CHECKSUM_IDX],
            };
        }
    }
}