// ============================================================================
//  ParamData.cs  —  TcLCS-UDP v1.1 帧定义 & SubID 常量
//
//  严格依据：CLS-II/_docs/TcLCS-UDP_Protocol_v1.1.docx
//  用户确认（2026-05-11）：TcLCS_CtrlIn SIZEOF = 68B（TwinCAT 在线 SIZEOF 一致）
//
//  帧结构（总长 14..1400 B）：
//    Header (11 B, pack_mode=1, LE) + Payload (0..1386 B) + Trailer (3 B)
//    Header: SOF0 SOF1 Ver DevID CMD SubID SeqNo(2LE) PayloadLen(2LE) FragInfo
//    Trailer: CRC16(2LE, MODBUS, 覆盖 Header+Payload) + EOF(0x55)
// ============================================================================
using System;
using System.Buffers.Binary;

namespace CLS_II.src_IOData
{
    public static class TcLcsConstants
    {
        // ---- 同步字/版本/EOF ----
        public const byte SOF0 = 0xAA;
        public const byte SOF1 = 0x55;
        public const byte VERSION = 0x02; // v1.1
        public const byte EOF = 0x55;

        // ---- 长度 ----
        public const int HEADER_LEN = 11;
        public const int TRAILER_LEN = 3;   // CRC16(2) + EOF(1)
        public const int MIN_FRAME = 14;  // 空 Payload
        public const int MAX_FRAME = 1400;
        public const int MAX_PAYLOAD = 1386;

        // ---- Header 偏移 ----
        public const int OFF_SOF0 = 0;
        public const int OFF_SOF1 = 1;
        public const int OFF_VERSION = 2;
        public const int OFF_DEVICE_ID = 3;
        public const int OFF_CMD = 4;
        public const int OFF_SUBID = 5;
        public const int OFF_SEQNO = 6;  // UINT16 LE
        public const int OFF_PAYLOADLEN = 8;  // UINT16 LE
        public const int OFF_FRAGINFO = 10;

        // ---- Device/广播 ----
        public const byte DEV_BROADCAST = 0xFF;
    }

    /// <summary>CMD 命令码（§3）</summary>
    public enum TcCmd : byte
    {
        READ_REQ = 0x01, READ_ACK = 0x81,
        WRITE_REQ = 0x02, WRITE_ACK = 0x82,
        PING = 0x03, PONG = 0x83,
        SAVE_PERSIST = 0x04, SAVE_ACK = 0x84,
        HELLO = 0x05, HELLO_ACK = 0x85,
        ERR = 0xEE,
    }

    /// <summary>SubID（§4），大小以协议文档为准</summary>
    public enum TcSubId : byte
    {
        ALL = 0x00,  // 992 B,  R only
        CLSModel = 0x01,  // 176 B,  R/W
        CLSParam = 0x02,  // 144 B,  R/W
        CLS5K = 0x03,  // 112 B,  R/W
        CLSConsts = 0x04,  // 104 B,  R/W
        TestMDL = 0x05,  //  88 B,  R/W
        CLSEnum = 0x06,  //  28 B,  R/W
        XT = 0x07,  // 168 B,  R/W
        YT = 0x08,  // 168 B,  R/W
        DeviceInfo = 0x10,  //  16 B,  R/W
        UdpDataCfg = 0x11,  //  48 B,  R/W
        UdpParamCfg = 0x12,  //  48 B,  R/W
        TcLCS_CtrlIn = 0x13,  //  68 B,  R/W   ← 用户 2026-05-11 确认 TwinCAT SIZEOF=68
        TcLCS_CtrlOut = 0x14,  //  52 B,  R only
        Bulk = 0xFF,  // ≤1386 B
    }

    /// <summary>错误/状态码（§6）</summary>
    public enum TcStatus : byte
    {
        OK = 0x00,
        BAD_SOF = 0x01,
        BAD_VERSION = 0x02,
        BAD_CRC = 0x03,
        BAD_LEN = 0x04,
        UNKNOWN_CMD = 0x05,
        UNKNOWN_SUBID = 0x06,
        READONLY = 0x07,
        SIZE_MISMATCH = 0x08,
        BUSY = 0x09,
        DEVICE_ID_MISMATCH = 0x0A,
        INTERNAL = 0xFF,
    }

    /// <summary>SubID → Payload 大小（字节）。ALL/Bulk 为特殊情况</summary>
    public static class TcSubIdSize
    {
        public static int Get(TcSubId sub) => sub switch
        {
            TcSubId.ALL => 992,
            TcSubId.CLSModel => 176,
            TcSubId.CLSParam => 144,
            TcSubId.CLS5K => 112,
            TcSubId.CLSConsts => 104,
            TcSubId.TestMDL => 88,
            TcSubId.CLSEnum => 28,
            TcSubId.XT => 168,
            TcSubId.YT => 168,
            TcSubId.DeviceInfo => 16,
            TcSubId.UdpDataCfg => 48,
            TcSubId.UdpParamCfg => 48,
            TcSubId.TcLCS_CtrlIn => 68,
            TcSubId.TcLCS_CtrlOut => 52,
            TcSubId.Bulk => -1, // 变长
            _ => -1,
        };

        public static bool IsReadOnly(TcSubId sub) =>
            sub == TcSubId.ALL || sub == TcSubId.TcLCS_CtrlOut;
    }

    public sealed class TcFrameHeader
    {
        public byte DeviceId;
        public TcCmd Cmd;
        public TcSubId SubId;
        public ushort SeqNo;
        public ushort PayloadLen;
        public byte FragInfo; // 当前固定 0x00
    }

    /// <summary>完整帧（Header + Payload）— Trailer 由 Codec 构建/校验</summary>
    public sealed class TcFrame
    {
        public TcFrameHeader Header { get; set; } = new();
        public byte[] Payload { get; set; } = Array.Empty<byte>();
    }

    /// <summary>CRC-16/MODBUS（§7）
    /// 参数：Poly=0xA001(反向), Init=0xFFFF, RefIn=F, RefOut=F, XorOut=0x0000
    /// 范围：覆盖 Header(11) + Payload(N) = 11+N 字节；存储 Little-Endian
    /// 自检：0x01 0x03 0x00 0x00 0x00 0x0A → 0x0CD5
    /// </summary>
    public static class Crc16Modbus
    {
        public static ushort Compute(ReadOnlySpan<byte> data)
        {
            ushort crc = 0xFFFF;
            for (int i = 0; i < data.Length; i++)
            {
                crc ^= data[i];
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 0x0001) != 0) crc = (ushort)((crc >> 1) ^ 0xA001);
                    else crc = (ushort)(crc >> 1);
                }
            }
            return crc;
        }

        /// <summary>文档自检：0x01 0x03 0x00 0x00 0x00 0x0A → 0x0CD5</summary>
        public static bool SelfTest()
        {
            ReadOnlySpan<byte> v = new byte[] { 0x01, 0x03, 0x00, 0x00, 0x00, 0x0A };
            return Compute(v) == 0x0CD5;
        }
    }

    /// <summary>TcLCS 帧编解码</summary>
    public static class TcCodec
    {
        /// <summary>构建一帧完整字节流（Header + Payload + CRC LE + EOF）</summary>
        public static byte[] Build(byte deviceId, TcCmd cmd, TcSubId subId,
                                   ushort seqNo, ReadOnlySpan<byte> payload,
                                   byte fragInfo = 0x00)
        {
            if (payload.Length > TcLcsConstants.MAX_PAYLOAD)
                throw new ArgumentException($"payload too large: {payload.Length}");

            int total = TcLcsConstants.HEADER_LEN + payload.Length + TcLcsConstants.TRAILER_LEN;
            var buf = new byte[total];

            // Header
            buf[TcLcsConstants.OFF_SOF0] = TcLcsConstants.SOF0;
            buf[TcLcsConstants.OFF_SOF1] = TcLcsConstants.SOF1;
            buf[TcLcsConstants.OFF_VERSION] = TcLcsConstants.VERSION;
            buf[TcLcsConstants.OFF_DEVICE_ID] = deviceId;
            buf[TcLcsConstants.OFF_CMD] = (byte)cmd;
            buf[TcLcsConstants.OFF_SUBID] = (byte)subId;
            BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(TcLcsConstants.OFF_SEQNO, 2), seqNo);
            BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(TcLcsConstants.OFF_PAYLOADLEN, 2), (ushort)payload.Length);
            buf[TcLcsConstants.OFF_FRAGINFO] = fragInfo;

            // Payload
            payload.CopyTo(buf.AsSpan(TcLcsConstants.HEADER_LEN, payload.Length));

            // CRC 覆盖 Header + Payload
            ushort crc = Crc16Modbus.Compute(buf.AsSpan(0, TcLcsConstants.HEADER_LEN + payload.Length));
            int crcOff = TcLcsConstants.HEADER_LEN + payload.Length;
            BinaryPrimitives.WriteUInt16LittleEndian(buf.AsSpan(crcOff, 2), crc);
            buf[crcOff + 2] = TcLcsConstants.EOF;
            return buf;
        }

        /// <summary>解析一帧；返回 null 表示失败，err 给出原因（§6 TcStatus 名）</summary>
        public static TcFrame? TryParse(ReadOnlySpan<byte> buf, out TcStatus err)
        {
            err = TcStatus.OK;
            if (buf.Length < TcLcsConstants.MIN_FRAME || buf.Length > TcLcsConstants.MAX_FRAME)
            { err = TcStatus.BAD_LEN; return null; }

            if (buf[TcLcsConstants.OFF_SOF0] != TcLcsConstants.SOF0 ||
                buf[TcLcsConstants.OFF_SOF1] != TcLcsConstants.SOF1)
            { err = TcStatus.BAD_SOF; return null; }

            if (buf[TcLcsConstants.OFF_VERSION] != TcLcsConstants.VERSION)
            { err = TcStatus.BAD_VERSION; return null; }

            ushort payloadLen = BinaryPrimitives.ReadUInt16LittleEndian(buf.Slice(TcLcsConstants.OFF_PAYLOADLEN, 2));
            int expected = TcLcsConstants.HEADER_LEN + payloadLen + TcLcsConstants.TRAILER_LEN;
            if (expected != buf.Length || payloadLen > TcLcsConstants.MAX_PAYLOAD)
            { err = TcStatus.BAD_LEN; return null; }

            int crcOff = TcLcsConstants.HEADER_LEN + payloadLen;
            ushort crcRecv = BinaryPrimitives.ReadUInt16LittleEndian(buf.Slice(crcOff, 2));
            ushort crcCalc = Crc16Modbus.Compute(buf.Slice(0, crcOff));
            if (crcRecv != crcCalc) { err = TcStatus.BAD_CRC; return null; }

            if (buf[crcOff + 2] != TcLcsConstants.EOF) { err = TcStatus.BAD_LEN; return null; }

            var hdr = new TcFrameHeader
            {
                DeviceId = buf[TcLcsConstants.OFF_DEVICE_ID],
                Cmd = (TcCmd)buf[TcLcsConstants.OFF_CMD],
                SubId = (TcSubId)buf[TcLcsConstants.OFF_SUBID],
                SeqNo = BinaryPrimitives.ReadUInt16LittleEndian(buf.Slice(TcLcsConstants.OFF_SEQNO, 2)),
                PayloadLen = payloadLen,
                FragInfo = buf[TcLcsConstants.OFF_FRAGINFO],
            };
            var payload = payloadLen == 0 ? Array.Empty<byte>() : buf.Slice(TcLcsConstants.HEADER_LEN, payloadLen).ToArray();
            return new TcFrame { Header = hdr, Payload = payload };
        }
    }
}