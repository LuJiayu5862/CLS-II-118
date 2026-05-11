// JdData.cs
// JD-61101 UDP 通信协议 — 帧常量 + 数据结构
// ⚠️ 所有字节偏移、固定值、长度均严格来自 JD-61101-UDP通信协议.docx，禁止自编
// 字节序：JD-61101 位移字段为大端（Big-Endian）；与 TcLCS 的小端不同，切勿混淆
using System;
using System.Buffers.Binary;

namespace CLS_II.src_IOData
{
    /// <summary>
    /// JD-61101 协议常量
    /// 发送帧（PLC→上位机）帧头: 0xA5 0xA5 0xA5
    /// 接收帧（上位机→PLC）帧头: 0x5A 0x5A 0x5A
    /// 帧总长: 20 字节（DATA0~DATA19），DATA19 = 和校验 SUM(DATA3..DATA18) & 0xFF
    /// </summary>
    public static class JdConstants
    {
        // 发送帧（PLC → 上位机）帧头
        public const byte SOF_TX_0 = 0xA5;
        public const byte SOF_TX_1 = 0xA5;
        public const byte SOF_TX_2 = 0xA5;

        // 接收帧（上位机 → PLC）帧头
        public const byte SOF_RX_0 = 0x5A;
        public const byte SOF_RX_1 = 0x5A;
        public const byte SOF_RX_2 = 0x5A;

        public const byte DEVICE_ID    = 0x01; // 设备编号
        public const byte DATA_LEN_VAL = 0x0E; // DATA4 数据长度字段值 = 14
        public const int  FRAME_LEN    = 20;   // 帧总字节数 DATA0~DATA19

        // DATA5（发送帧）故障状态
        public const byte STATUS_OK    = 0x00;

        // DATA5（接收帧）故障复位
        public const byte CLEAR_FAULT_NORMAL = 0x00;
        public const byte CLEAR_FAULT_RESET  = 0xAA;

        // DATA7（接收帧）操纵负荷复位
        public const byte RESET_PEDAL_NORMAL = 0x00;
        public const byte RESET_PEDAL_RESET  = 0xAA;
    }

    /// <summary>
    /// JD-61101 发送帧（PLC → 上位机）解析结果
    /// DATA7-10: 脚蹬位移 int32 大端，范围 [-18000, +18000]
    ///           左脚向前为负，右脚向前为正
    /// </summary>
    public sealed class JdTxFrame
    {
        public byte  Status;       // DATA5: 0x00=正常，其他=故障码
        public int   PedalPos;     // DATA7-10: 脚蹬位移 int32 大端
        public byte  Checksum;     // DATA19: 和校验（已校验通过后填入）

        /// <summary>
        /// 从 20 字节 UDP payload 解析发送帧
        /// </summary>
        public static bool TryParse(ReadOnlySpan<byte> buf, out JdTxFrame frame)
        {
            frame = null;
            if (buf.Length != JdConstants.FRAME_LEN) return false;

            // 校验帧头
            if (buf[0] != JdConstants.SOF_TX_0 ||
                buf[1] != JdConstants.SOF_TX_1 ||
                buf[2] != JdConstants.SOF_TX_2) return false;

            // 校验 DeviceID 与 DataLen
            if (buf[3] != JdConstants.DEVICE_ID)    return false;
            if (buf[4] != JdConstants.DATA_LEN_VAL) return false;

            // 校验和：SUM(DATA3..DATA18) & 0xFF
            byte sum = 0;
            for (int i = 3; i <= 18; i++) sum += buf[i];
            if (sum != buf[19]) return false;

            frame = new JdTxFrame
            {
                Status    = buf[5],
                // DATA7-10: int32 大端（高位在前，低位在后）
                PedalPos  = BinaryPrimitives.ReadInt32BigEndian(buf.Slice(7, 4)),
                Checksum  = buf[19]
            };
            return true;
        }
    }

    /// <summary>
    /// JD-61101 接收帧（上位机 → PLC）构建结果
    /// </summary>
    public sealed class JdRxFrame
    {
        public byte ClearFault;  // DATA5: 0x00=正常，0xAA=清除故障码
        public byte ResetPedal;  // DATA7: 0x00=正常，0xAA=复位到中立位

        /// <summary>
        /// 构建 20 字节接收帧
        /// </summary>
        public byte[] Build()
        {
            var buf = new byte[JdConstants.FRAME_LEN];
            buf[0] = JdConstants.SOF_RX_0;
            buf[1] = JdConstants.SOF_RX_1;
            buf[2] = JdConstants.SOF_RX_2;
            buf[3] = JdConstants.DEVICE_ID;
            buf[4] = JdConstants.DATA_LEN_VAL;
            buf[5] = ClearFault;
            buf[6] = 0x00; // 预留
            buf[7] = ResetPedal;
            // DATA8~DATA18 全部为 0x00（预留）
            // DATA19: 和校验 SUM(DATA3..DATA18)
            byte sum = 0;
            for (int i = 3; i <= 18; i++) sum += buf[i];
            buf[19] = sum;
            return buf;
        }

        /// <summary>构建正常帧（不复位）</summary>
        public static JdRxFrame Normal() =>
            new JdRxFrame { ClearFault = JdConstants.CLEAR_FAULT_NORMAL,
                            ResetPedal = JdConstants.RESET_PEDAL_NORMAL };

        /// <summary>构建故障复位帧</summary>
        public static JdRxFrame ClearFaultFrame() =>
            new JdRxFrame { ClearFault = JdConstants.CLEAR_FAULT_RESET,
                            ResetPedal = JdConstants.RESET_PEDAL_NORMAL };

        /// <summary>构建负荷复位帧</summary>
        public static JdRxFrame ResetPedalFrame() =>
            new JdRxFrame { ClearFault = JdConstants.CLEAR_FAULT_NORMAL,
                            ResetPedal = JdConstants.RESET_PEDAL_RESET };
    }
}
