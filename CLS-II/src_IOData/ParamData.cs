// ============================================================================
//  ParamData.cs  —  TcLCS-UDP v1.1 帧定义 & SubID 常量 & 协议结构体 & 全局缓冲区
//
//  严格依据：
//    · CLS-II/_docs/TcLCS-UDP_Protocol_v1.1.docx
//    · UDP-61131-JD/UDP_Com/DUTs/*.TcDUT  (PLC ST 源代码，字段 100% 准确)
//
//  用户确认（2026-05-11）：TcLCS_CtrlIn SIZEOF = 68B（TwinCAT 在线 SIZEOF 一致）
//
//  TwinCAT → C# 类型映射（pack=8, LE）：
//    BOOL       → byte   (1B)
//    BYTE       → byte   (1B)
//    SINT       → sbyte  (1B)
//    INT        → short  (2B LE)
//    UINT       → ushort (2B LE)
//    DINT       → int    (4B LE)
//    UDINT      → uint   (4B LE)
//    REAL       → float  (4B LE, IEEE754)
//    LREAL      → double (8B LE, IEEE754)
//    STRING(15) → byte[16] via MarshalAs ByValArray (15字符+1终止符)
// ============================================================================
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace CLS_II
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
        public TcFrameHeader Header { get; set; } = new TcFrameHeader();
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

        /// <summary>文档自检：0x01 0x03 0x00 0x00 0x00 0x0A → 0xCDC5</summary>
        public static bool SelfTest()
        {
            ReadOnlySpan<byte> v = new byte[] { 0x01, 0x03, 0x00, 0x00, 0x00, 0x0A };
            return Compute(v) == 0xCDC5;
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
        public static TcFrame TryParse(ReadOnlySpan<byte> buf, out TcStatus err)
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
    // =========================================================================
    //  协议结构体定义（来源：UDP-61131-JD PLC DUT 源代码，100% 准确）
    // =========================================================================

    // SubID 0x01  ST_CLSModel  pack=8  22×LREAL = 176B  R/W
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct ST_CLSModel
    {
        public double bA; public double bF; public double bH; public double bS;
        public double kS; public double kH; public double kHV; public double kSV;
        public double kQ; public double kU; public double mA; public double mF;
        public double Vbrk; public double Xbrk; public double FcA; public double FcF;
        public double bFA; public double dzF;
        public double NFC_P; public double NFC_I; public double NFC_D; public double NFC_N;
        // SIZEOF: 22×8 = 176 ✅
    }

    // SubID 0x02  ST_CLSParam  pack=8  BOOL(1)+pad(7)+17×LREAL(136) = 144B  R/W
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct ST_CLSParam
    {
        public byte AutoHoming;
        private byte _p0; private byte _p1; private byte _p2;
        private byte _p3; private byte _p4; private byte _p5; private byte _p6; // 7B pad
        public double VPos; public double Jagment; public double JagmentP; public double JagmentN;
        public double P0Home; public double Foffset; public double Fzero; public double F0Aoff;
        public double Poffset; public double P0Trim; public double L0Trim; public double L0TravA;
        public double L0TravB; public double ShakerA; public double ShakerF;
        public double VT1; public double VT2;
        // SIZEOF: 8+17×8 = 144 ✅
    }

    // SubID 0x03  ST_CLS5K  pack=8  14×LREAL = 112B  R/W
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct ST_CLS5K
    {
        public double K0; public double K1; public double K2;
        public double K3; public double K4; public double K5;
        public double X0; public double X1; public double X2;
        public double X3; public double X4; public double X5;
        public double X6; public double Ke;
        // SIZEOF: 14×8 = 112 ✅
    }

    // SubID 0x04  ST_CLSConsts  pack=8  BOOL+BOOL+UINT+pad(4)+12×LREAL = 104B  R/W
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct ST_CLSConsts
    {
        public byte KUSEDEG;  // BOOL  offset=0
        public byte CCWDIR;   // BOOL  offset=1
        public ushort KFLMT;    // UINT  offset=2
        private byte _p0; private byte _p1; private byte _p2; private byte _p3; // 4B pad → offset=8
        public double KFmax; public double KVmax; public double Larm;
        public double KPR; public double KFR; public double KX2P;
        public double KForceTo; public double KVelTo; public double KPosTo;
        public double KF2N; public double KV2DPS; public double KP2DEG;
        // SIZEOF: 8+12×8 = 104 ✅
    }

    // SubID 0x05  ST_TestMDL  pack=8  11×LREAL = 88B  R/W
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct ST_TestMDL
    {
        public double Km; public double Ks; public double Kp;
        public double Ka; public double Kq; public double bs;
        public double bA; public double Ksf; public double bsf;
        public double DL; public double PL;
        // SIZEOF: 11×8 = 88 ✅
    }

    // SubID 0x06  ST_CLSEnum  pack=8  8×SINT+INT+9×UINT = 28B  R/W
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct ST_CLSEnum
    {
        public sbyte DM_PP; public sbyte DM_PV; public sbyte DM_PT; public sbyte DM_HM;
        public sbyte DM_IP; public sbyte DM_CSP; public sbyte DM_CSV; public sbyte DM_CST;
        public short STU_KQS;     // INT   offset=8
        public ushort STU_FAULT; public ushort STU_STOP; public ushort STU_NORDY;
        public ushort STU_OPR; public ushort STU_HOMED;
        public ushort DRV_SHUTDWN; public ushort DRV_ENABLE; public ushort DRV_RESET;
        public ushort SW_FCW;
        // SIZEOF: 8+2+9×2 = 28 ✅
    }

    // SubID 0x07  ST_XT  pack=8  ARRAY[0..20] OF LREAL = 21×8 = 168B  R/W
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct ST_XT
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 21)]
        public double[] aXT;
        // SIZEOF: 21×8 = 168 ✅
    }

    // SubID 0x08  ST_YT  pack=8  ARRAY[0..20] OF LREAL = 21×8 = 168B  R/W
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct ST_YT
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 21)]
        public double[] aYT;
        // SIZEOF: 21×8 = 168 ✅
    }

    // SubID 0x10  ST_DeviceInfo  pack=8  SIZEOF=16B  R/W
    // 来源：ST_DeviceInfo.TcDUT（含原文 Offset/Size 注释）
    // BYTE(1+3pad)+REAL(4)+REAL(4)+BOOL(1+3pad) = 16B
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct ST_DeviceInfo
    {
        public byte ID;        // BYTE  offset=0   1B
        private byte _p0; private byte _p1; private byte _p2; // 3B pad → offset=4
        public float PosN;      // REAL  offset=4   4B
        public float POSP;      // REAL  offset=8   4B
        public byte TestMode;  // BOOL  offset=12  1B
        private byte _p3; private byte _p4; private byte _p5; // 3B pad → offset=16
        // SIZEOF: 1+3+4+4+1+3 = 16 ✅
    }

    // SubID 0x11  UdpDataCfg  → ST_UDP_Parameter  pack=8  SIZEOF=48B  R/W
    // SubID 0x12  UdpParamCfg → ST_UDP_Parameter  pack=8  SIZEOF=48B  R/W
    // 来源：ST_UDP_Parameter.TcDUT（含原文 Offset/Size 注释）
    // STRING(15)=16B, BOOL(1)+pad(3)+UDINT(4)
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct ST_UDP_Parameter
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] LocalIP;        // STRING(15)  offset=0   16B
        public uint LocalPort;      // UDINT       offset=16   4B
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public byte[] RemoteIP;       // STRING(15)  offset=20  16B
        public uint RemotePort;     // UDINT       offset=36   4B
        public byte bPeriodical;    // BOOL        offset=40   1B
        private byte _p0; private byte _p1; private byte _p2; // 3B pad → offset=44
        public uint Period;         // UDINT       offset=44   4B
        // SIZEOF: 16+4+16+4+1+3+4 = 48 ✅

        public string GetLocalIP() => DecodeIp(LocalIP);
        public string GetRemoteIP() => DecodeIp(RemoteIP);
        public void SetLocalIP(string ip) => EncodeIp(ip, ref LocalIP);
        public void SetRemoteIP(string ip) => EncodeIp(ip, ref RemoteIP);

        private static string DecodeIp(byte[] buf)
        {
            if (buf == null) return string.Empty;
            int len = Array.IndexOf(buf, (byte)0);
            return Encoding.ASCII.GetString(buf, 0, len < 0 ? buf.Length : len);
        }
        private static void EncodeIp(string s, ref byte[] buf)
        {
            if (buf == null) buf = new byte[16];
            Array.Clear(buf, 0, 16);
            if (string.IsNullOrEmpty(s)) return;
            byte[] src = Encoding.ASCII.GetBytes(s);
            Array.Copy(src, buf, Math.Min(src.Length, 15)); // 最多15字符，第16位保持0
        }
    }

    // SubID 0x13  ST_TcLCS_U (CtrlIn)  pack=8  UDINT(4)+15×REAL(60)+UDINT(4) = 68B  R/W
    // 来源：ST_TcLCS_U.TcDUT ✅
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct ST_TcLCS_U
    {
        public uint CtrlCmd;   // UDINT offset=0   控制命令 0=OFF,1=ForceLoopX1,10=Reset
        public float fwdFric;   // REAL  offset=4   FwdFriction [N]
        public float jamPos;    // REAL  offset=8   FwdJamPosition [deg]
        public float TravA;     // REAL  offset=12  FwdPositiveStop [deg]
        public float TravB;     // REAL  offset=16  FwdNegativeStop [deg]
        public float fwdMassD;  // REAL  offset=20  FwdAddedMass [N/(deg/s²)]
        public float fwdDampD;  // REAL  offset=24  FwdAddedDamping [N·sec/deg]
        public float FInput;    // REAL  offset=28  ForceInput [N]
        public float Vap;       // REAL  offset=32  AutopilotVelocity [deg/s]
        public uint FnSwitch;  // UDINT offset=36  功能开关
        public float VTrim;     // REAL  offset=40  TrimVelocity [deg/sec]
        public float FaOffset;  // REAL  offset=44  AeroForceOffset [N]
        public float FaGrad;    // REAL  offset=48  AeroForceGradient [N/deg]
        public float trimInitP; // REAL  offset=52  TrimInitPosition [deg]
        public float Spare1;    // REAL  offset=56  保留1
        public float Spare2;    // REAL  offset=60  保留2
        public float Spare3;    // REAL  offset=64  保留3
        // SIZEOF: 4+15×4+4 = 68 ✅
    }

    // SubID 0x14  ST_TcLCS_Y (CtrlOut)  pack=8  3×DINT(12)+10×REAL(40) = 52B  R only
    // 来源：ST_TcLCS_Y.TcDUT ✅
    [StructLayout(LayoutKind.Sequential, Pack = 8)]
    public struct ST_TcLCS_Y
    {
        public int state;         // DINT  offset=0   通道状态
        public int safety;        // DINT  offset=4
        public int isFading;      // DINT  offset=8   1=Fading active
        public float fwdPosition;   // REAL  offset=12  前端位置 [deg]
        public float fwdVelocity;   // REAL  offset=16  前端速度 [deg/sec]
        public float fwdForce;      // REAL  offset=20  载荷大小 [N]
        public float cableForce;    // REAL  offset=24  钢索力 [N]
        public float trimPosition;  // REAL  offset=28  配平位置 [deg]
        public float aftPosition;   // REAL  offset=32  后端位置 [deg]
        public float motorPosition; // REAL  offset=36  保留字段
        public float motorVelocity; // REAL  offset=40  保留字段
        public float sensorForce;   // REAL  offset=44  保留字段
        public float commandForce;  // REAL  offset=48  保留字段
        // SIZEOF: 3×4+10×4 = 52 ✅
    }

    // =========================================================================
    //  ParamData —— 全局静态缓冲区（真相唯一源）
    // =========================================================================
    public static class ParamData
    {
        public static ST_TcLCS_U CtrlIn = new ST_TcLCS_U();
        public static ST_TcLCS_Y CtrlOut = new ST_TcLCS_Y();
        public static ST_CLSModel CLS_Model = new ST_CLSModel();
        public static ST_CLSParam CLS_Param = new ST_CLSParam();
        public static ST_CLS5K CLS_5K = new ST_CLS5K();
        public static ST_CLSConsts CLS_Consts = new ST_CLSConsts();
        public static ST_TestMDL Test_MDL = new ST_TestMDL();
        public static ST_CLSEnum CLS_Enum = new ST_CLSEnum();
        public static ST_XT Param_XT = new ST_XT();
        public static ST_YT Param_YT = new ST_YT();
        public static ST_DeviceInfo Device_Info = new ST_DeviceInfo();
        public static ST_UDP_Parameter UdpData_Cfg = new ST_UDP_Parameter();
        public static ST_UDP_Parameter UdpParam_Cfg = new ST_UDP_Parameter();

        public static readonly object LockCtrlIn = new object();
        public static readonly object LockCtrlOut = new object();
        public static readonly object LockCLSModel = new object();
        public static readonly object LockCLSParam = new object();
        public static readonly object LockCLS5K = new object();
        public static readonly object LockCLSConsts = new object();
        public static readonly object LockTestMDL = new object();
        public static readonly object LockCLSEnum = new object();
        public static readonly object LockXT = new object();
        public static readonly object LockYT = new object();
        public static readonly object LockDevInfo = new object();
        public static readonly object LockUdpDataCfg = new object();
        public static readonly object LockUdpParamCfg = new object();

        /// <summary>通用序列化：结构体 → byte[]，供 WRITE_REQ 发送。</summary>
        public static byte[] Serialize<T>(T s) where T : struct
            => Struct_Func.StructToBytes(s);

        /// <summary>CtrlIn 周期 Write 快捷方法。</summary>
        public static byte[] SerializeCtrlIn()
        {
            lock (LockCtrlIn)
                return Struct_Func.StructToBytes(CtrlIn);
        }

        /// <summary>
        /// 收到 READ_ACK / 主动上报帧后调用，自动按 SubID 解析 Payload
        /// 并存入对应全局变量。返回 false 表示 SubID 未知或长度不符。
        /// </summary>
        public static bool TryDeserialize(TcFrame frame)
        {
            if (frame?.Payload == null || frame.Payload.Length == 0) return false;
            int expect = TcSubIdSize.Get(frame.Header.SubId);
            if (expect > 0 && frame.Payload.Length != expect)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[ParamData] size mismatch sub={frame.Header.SubId} " +
                    $"expect={expect} got={frame.Payload.Length}");
                return false;
            }
            switch (frame.Header.SubId)
            {
                case TcSubId.TcLCS_CtrlOut:
                    lock (LockCtrlOut)
                        CtrlOut = (ST_TcLCS_Y)Struct_Func.BytesToStruct(frame.Payload, CtrlOut);
                    return true;
                case TcSubId.TcLCS_CtrlIn:
                    lock (LockCtrlIn)
                        CtrlIn = (ST_TcLCS_U)Struct_Func.BytesToStruct(frame.Payload, CtrlIn);
                    return true;
                case TcSubId.CLSModel:
                    lock (LockCLSModel)
                        CLS_Model = (ST_CLSModel)Struct_Func.BytesToStruct(frame.Payload, CLS_Model);
                    return true;
                case TcSubId.CLSParam:
                    lock (LockCLSParam)
                        CLS_Param = (ST_CLSParam)Struct_Func.BytesToStruct(frame.Payload, CLS_Param);
                    return true;
                case TcSubId.CLS5K:
                    lock (LockCLS5K)
                        CLS_5K = (ST_CLS5K)Struct_Func.BytesToStruct(frame.Payload, CLS_5K);
                    return true;
                case TcSubId.CLSConsts:
                    lock (LockCLSConsts)
                        CLS_Consts = (ST_CLSConsts)Struct_Func.BytesToStruct(frame.Payload, CLS_Consts);
                    return true;
                case TcSubId.TestMDL:
                    lock (LockTestMDL)
                        Test_MDL = (ST_TestMDL)Struct_Func.BytesToStruct(frame.Payload, Test_MDL);
                    return true;
                case TcSubId.CLSEnum:
                    lock (LockCLSEnum)
                        CLS_Enum = (ST_CLSEnum)Struct_Func.BytesToStruct(frame.Payload, CLS_Enum);
                    return true;
                case TcSubId.XT:
                    lock (LockXT)
                        Param_XT = (ST_XT)Struct_Func.BytesToStruct(frame.Payload, Param_XT);
                    return true;
                case TcSubId.YT:
                    lock (LockYT)
                        Param_YT = (ST_YT)Struct_Func.BytesToStruct(frame.Payload, Param_YT);
                    return true;
                case TcSubId.DeviceInfo:
                    lock (LockDevInfo)
                        Device_Info = (ST_DeviceInfo)Struct_Func.BytesToStruct(frame.Payload, Device_Info);
                    return true;
                case TcSubId.UdpDataCfg:
                    lock (LockUdpDataCfg)
                        UdpData_Cfg = (ST_UDP_Parameter)Struct_Func.BytesToStruct(frame.Payload, UdpData_Cfg);
                    return true;
                case TcSubId.UdpParamCfg:
                    lock (LockUdpParamCfg)
                        UdpParam_Cfg = (ST_UDP_Parameter)Struct_Func.BytesToStruct(frame.Payload, UdpParam_Cfg);
                    return true;
                case TcSubId.ALL:
                    {
                        // 992B = CLSModel(176) + CLSParam(144) + CLS5K(112) + CLSConsts(104)
                        //      + TestMDL(88)   + CLSEnum(28)   + XT(168)    + YT(168)
                        var p = frame.Payload;
                        int off = 0;

                        lock (LockCLSModel)
                        {
                            CLS_Model = (ST_CLSModel)Struct_Func.BytesToStruct(p, off, CLS_Model);
                            off += 176;
                        }
                        lock (LockCLSParam)
                        {
                            CLS_Param = (ST_CLSParam)Struct_Func.BytesToStruct(p, off, CLS_Param);
                            off += 144;
                        }
                        lock (LockCLS5K)
                        {
                            CLS_5K = (ST_CLS5K)Struct_Func.BytesToStruct(p, off, CLS_5K);
                            off += 112;
                        }
                        lock (LockCLSConsts)
                        {
                            CLS_Consts = (ST_CLSConsts)Struct_Func.BytesToStruct(p, off, CLS_Consts);
                            off += 104;
                        }
                        lock (LockTestMDL)
                        {
                            Test_MDL = (ST_TestMDL)Struct_Func.BytesToStruct(p, off, Test_MDL);
                            off += 88;
                        }
                        lock (LockCLSEnum)
                        {
                            CLS_Enum = (ST_CLSEnum)Struct_Func.BytesToStruct(p, off, CLS_Enum);
                            off += 32;
                        }
                        lock (LockXT)
                        {
                            Param_XT = (ST_XT)Struct_Func.BytesToStruct(p, off, Param_XT);
                            off += 168;
                        }
                        lock (LockYT)
                        {
                            Param_YT = (ST_YT)Struct_Func.BytesToStruct(p, off, Param_YT);
                            // off += 168;  // 最后一块，不再需要
                        }
                        return true;
                    }
                default:
                    System.Diagnostics.Debug.WriteLine(
                        $"[ParamData] TryDeserialize: unhandled SubID={frame.Header.SubId}");
                    return false;
            }
        }
        public static class Snap
        {
            // 写类（差分写监控）
            public static ST_CLSModel CLS_Model = new ST_CLSModel();
            public static ST_CLSParam CLS_Param = new ST_CLSParam();
            public static ST_CLS5K CLS_5K = new ST_CLS5K();
            public static ST_CLSConsts CLS_Consts = new ST_CLSConsts();
            public static ST_TestMDL Test_MDL = new ST_TestMDL();
            public static ST_CLSEnum CLS_Enum = new ST_CLSEnum();
            public static ST_XT Param_XT = new ST_XT();
            public static ST_YT Param_YT = new ST_YT();
            public static ST_DeviceInfo DeviceInfo = new ST_DeviceInfo();
            public static ST_UDP_Parameter UdpDataCfg = new ST_UDP_Parameter();
            public static ST_UDP_Parameter UdpParamCfg = new ST_UDP_Parameter();
            public static ST_TcLCS_Y CtrlOut = new ST_TcLCS_Y();

            // 周期写（每次 tick 都发，不做差分）
            public static ST_TcLCS_U CtrlIn = new ST_TcLCS_U();

            // 只读（轮询读回后同步到源变量和快照，源变量不可写）
            // TcLCS_CtrlOut / DeviceInfo / UdpDataCfg / UdpParamCfg / ALL
            // 这些的快照不需要声明，轮询读后直接覆盖源变量即可
        }
    }
}