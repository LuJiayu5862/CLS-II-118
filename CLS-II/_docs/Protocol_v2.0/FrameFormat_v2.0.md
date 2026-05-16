# TcLCS-UDP Protocol v2.0 — 帧格式规范

> **文档状态**：草案（设计讨论中）
> **最后更新**：2026-05-16
> **基于**：TcLCS-UDP Protocol v1.1（Version=0x02）向上扩展

---

## 一、设计原则

- **向后兼容**：v2.0 帧使用 `Version=0x03`，主站可在同一 socket 上通过 Version 字段区分 v1.1 / v2.0 帧，支持双版本共存与渐进迁移。
- **沿用 v1.1 物理层**：SOF/EOF 魔数、CRC-16/MODBUS 算法、最大帧长 1400B 完全不变。
- **新增专用端口**：v2.0 使用端口 `5051`，v1.1 的 `5050`（Param）和 `15000`（Data）不受影响。
- **CMD 编码约定沿用**：REQ = 低字节，ACK = REQ | 0x80。

---

## 二、通用帧结构

### 2.1 Header（12 字节）

```
Offset  Size  字段         类型    说明
───────────────────────────────────────────────────────────────
  0     1B    SOF0         BYTE    固定 0xAA
  1     1B    SOF1         BYTE    固定 0x55
  2     1B    Version      BYTE    固定 0x03（v2.0 标识）
  3     1B    DevID        BYTE    设备ID；0xFF = 广播
  4     1B    CMD          BYTE    指令码（见第三节）
  5     1B    Flags        BYTE    标志位（见 2.2）
  6     2B    SeqNum       UINT    帧序号，上位机自增，回包原样返回
  8     2B    PayloadLen   UINT    Payload 字节数（不含 Header / Trailer）
 10     2B    Reserved     WORD    保留，固定 0x0000（预留 SubCMD / SessionID）
```

> ⚠️ v1.1 Header 为 11B，v2.0 扩展至 **12B**（新增 Reserved 字段）。

### 2.2 Flags 字段（1 字节）

```
Bit 7  ACK_REQ    1 = 请求对端确认（用于写操作）
Bit 6  FRAG       1 = 分片帧（参数表分页传输时使用）
Bit 5  LAST_FRAG  1 = 最后一个分片
Bit 4  TRACE      1 = 此帧携带 Trace Buffer 数据
Bit 3-0 Reserved  填 0
```

### 2.3 Payload（可变长，0~1385 字节）

各 CMD 对应 Payload 格式见第四节。

### 2.4 Trailer（3 字节，与 v1.1 完全一致）

```
Offset(from end)  Size  字段    说明
────────────────────────────────────────────────────────────
       -3         2B    CRC16   CRC-16/MODBUS，覆盖范围：Header + Payload
       -1         1B    EOF     固定 0x55
```

### 2.5 完整帧示意

```
┌──────────────────────────────────────────────────────┐
│  SOF0  SOF1  Ver  DevID  CMD  Flags  SeqNum  PldLen  Reserved │  ← Header 12B
│  0xAA  0x55  0x03  ...   ...  ...    2B      2B       2B      │
├──────────────────────────────────────────────────────┤
│                   Payload (0~1385B)                   │
├──────────────────────────────────────────────────────┤
│              CRC16 (2B)          EOF (0x55)           │  ← Trailer 3B
└──────────────────────────────────────────────────────┘
最大帧长 = 12 + 1385 + 3 = 1400B
```

---

## 三、CMD 指令码总表

### 3.1 v1.1 继承指令（兼容保留，0x01–0x05）

| CMD (REQ) | CMD (ACK) | 名称             | 说明                                      |
|-----------|-----------|------------------|-------------------------------------------|
| `0x01`    | `0x81`    | READ_REQ         | 按 SubID 读（v1.1 旧式，兼容保留）        |
| `0x02`    | `0x82`    | WRITE_REQ        | 按 SubID 写（v1.1 旧式，兼容保留）        |
| `0x03`    | `0x83`    | PING / PONG      | 心跳检测                                  |
| `0x04`    | `0x84`    | SAVE_PERSIST     | 触发持久化保存                            |
| `0x05`    | `0x85`    | HELLO / HELLO_ACK| 握手（v2.0 扩展 Payload，见 4.0b）        |
| `0xEE`    | —         | ERR              | 错误响应                                  |

### 3.2 参数表指令族（0x10–0x1F）

| CMD (REQ) | CMD (ACK) | 名称              | 说明                                      |
|-----------|-----------|-------------------|-------------------------------------------|
| `0x10`    | `0x90`    | GET_PARAM_DICT    | 请求参数表（分页，附 PageIndex）          |
| `0x11`    | `0x91`    | GET_PARAM_BY_ID   | 查询单个 ParamID 的完整元信息             |
| `0x12`    | `0x92`    | PARAM_SUBSCRIBE   | 订阅参数变化通知（预留，暂未实装）        |
| `0x13`    | `0x93`    | GET_ENUM_MAP      | 查询指定 ParamID 的枚举值-文本映射表      |

### 3.3 数据读写指令族（0x20–0x2F）

| CMD (REQ) | CMD (ACK) | 名称              | 说明                                      |
|-----------|-----------|-------------------|-------------------------------------------|
| `0x20`    | `0xA0`    | READ_BY_ID        | 按 ParamID 列表批量读                     |
| `0x21`    | `0xA1`    | WRITE_BY_ID       | 按 ParamID 列表批量写                     |
| `0x22`    | `0xA2`    | READ_GROUP        | 按 GroupID 读整组                         |
| `0x23`    | `0xA3`    | WRITE_GROUP       | 按 GroupID 写整组                         |

### 3.4 Trace Buffer 指令族（0x30–0x3F）

| CMD (REQ) | CMD (ACK) | 名称              | 说明                                      |
|-----------|-----------|-------------------|-------------------------------------------|
| `0x30`    | `0xB0`    | TRACE_CONFIG      | 下发示波器采样配置                        |
| `0x31`    | `0xB1`    | TRACE_START       | 启动采样                                  |
| `0x32`    | `0xB2`    | TRACE_STOP        | 停止采样                                  |
| `0x33`    | `0xB3`    | TRACE_STATUS      | 查询采样状态（是否触发完成）              |
| `0x34`    | `0xB4`    | TRACE_UPLOAD      | 请求上传 Buffer 数据（支持分片）          |

---

## 四、Payload 格式详述

### 4.0 PING 请求（CMD=0x03）/ PONG 应答（CMD=0x83）

**PING 请求 Payload：**

```
[Timestamp_ms : UDINT 4B]   // 上位机发送时刻（毫秒，用于 RTT 计算）
```

**PONG 应答 Payload：**

```
[Timestamp_ms : UDINT 4B]   // 原样回传上位机时间戳
[DictHash     : UINT  2B]   // 当前参数表结构哈希（CRC16/MODBUS）
```

> **设计意图**：上位机每次 PING 可顺带校验 DictHash，无需额外发起 GET_PARAM_DICT，降低心跳开销。若 DictHash 与本地缓存不一致，则触发参数表重拉。

### 4.0b HELLO 请求（CMD=0x05）/ HELLO_ACK 应答（CMD=0x85）

**HELLO 请求 Payload：**

```
（Payload 为空，PayloadLen=0）
```

**HELLO_ACK 应答 Payload：**

```
[DictHash   : UINT  2B]      // 参数表结构哈希，握手即缓存，无需立即拉取参数表
[ParamCount : UINT  2B]      // 参数表总条目数
[FW_Major   : BYTE  1B]      // 固件主版本号（Major 不一致 → 上位机拒绝连接）
[FW_Minor   : BYTE  1B]      // 固件次版本号（上位机 Minor > 主站 → 降级运行+警告）
[FW_Patch   : BYTE  1B]      // 固件补丁版本号（差异静默忽略，始终兼容）
[DevNameLen : BYTE  1B]      // DeviceName 字节数（1~32B）
[DeviceName : DevNameLen B]  // UTF-8，无 null 结尾；来源：ParamID 0xFFEF
```

固定部分 **8B** + 变长 DeviceName（1~32B）。

**FirmwareVersion 兼容性规则：**

| 情形 | 上位机行为 |
|------|-----------|
| Major 不一致 | 拒绝连接，显示版本不兼容错误 |
| Minor：上位机 > 主站 | 降级运行，界面显示固件过旧警告 |
| Minor：上位机 ≤ 主站 | 正常连接 |
| Patch 任意差异 | 静默忽略，始终兼容 |

### 4.1 GET_PARAM_DICT 请求（CMD=0x10）

```
[PageIndex : UINT 2B]   // 页码，从 0 开始，每页最多 12 条
```

### 4.2 GET_PARAM_DICT 应答（CMD=0x90）

```
[DictHash   : UINT  2B]   // 参数表结构哈希（CRC16/MODBUS），便于上位机快速失配检出
[TotalCount : UINT  2B]   // 参数表总条目数
[PageIndex  : UINT  2B]   // 本页页码
[EntryCount : BYTE  1B]   // 本页实际条目数（≤ 12）
[Entry × EntryCount] :
  [ParamID    : UINT  2B]
  [DataType   : BYTE  1B]   // 见 4.3 DataType 枚举
  [ByteSize   : BYTE  1B]   // 该类型占用字节数
  [Access     : BYTE  1B]   // 0=RO, 1=RW
  [GroupID    : BYTE  1B]   // 0=不分组
  [CycleClass : BYTE  1B]   // 0=2ms,1=10ms,2=100ms,3=1s,4=手动
  [Unit       : BYTE[8] 8B] // 定长，UTF-8，不足补0，如 "rpm", "mm/s"
  [NameLen    : BYTE  1B]   // 变量名字节数（UTF-8，最长 32B）
  [Name       : NameLen B]  // 变量名字符串（无 null 结尾）
  [DescLen    : BYTE  1B]   // 描述字节数（UTF-8，最长 64B，0=无描述）
  [Desc       : DescLen B]  // 描述字符串（无 null 结尾）
```

每条 Entry 固定部分 **17B**（原 9B + Unit 8B）+ 变长 Name（1~32B）+ 变长 Desc（0~64B）。
每页最多 **12 条**，最坏情况 (17+1+32+1+64)×12 = 1380B，在 1385B MTU 内安全。

当 `FRAG=1` 且 `LAST_FRAG=1` 时表示参数表传输完毕。

### 4.3 DataType 枚举

| 值     | 类型      | ByteSize | TwinCAT ST 对应  |
|--------|-----------|----------|------------------|
| `0x01` | BOOL      | 1        | BOOL             |
| `0x02` | BYTE      | 1        | BYTE             |
| `0x03` | WORD      | 2        | WORD             |
| `0x04` | INT       | 2        | INT              |
| `0x05` | UINT      | 2        | UINT             |
| `0x06` | DWORD     | 4        | DWORD            |
| `0x07` | DINT      | 4        | DINT             |
| `0x08` | UDINT     | 4        | UDINT            |
| `0x09` | REAL      | 4        | REAL             |
| `0x0A` | LREAL     | 8        | LREAL            |
| `0x0B` | LINT      | 8        | LINT             |
| `0x0C` | ULINT     | 8        | ULINT            |
| `0x0D` | STRING8   | 8        | STRING(7)        |
| `0x0E` | STRING16  | 16       | STRING(15)       |
| `0x0F` | STRING32  | 32       | STRING(31)       |
| `0x10` | STRING64  | 64       | STRING(63)       |
| `0x11` | STRING128 | 128      | STRING(127)      |

### 4.4 GET_PARAM_BY_ID 请求（CMD=0x11）

```
[ParamID : UINT 2B]   // 查询目标 ParamID
```

应答（CMD=0x91）返回单条 Entry，格式同 4.2 中的 Entry 结构（含 Unit 与 Desc）。

### 4.4b GET_ENUM_MAP 请求（CMD=0x13）

```
[ParamID : UINT 2B]   // 查询目标 ParamID（需为枚举类型）
```

应答（CMD=0x93）：

```
[ParamID  : UINT  2B]
[MapCount : BYTE  1B]   // 枚举项数（非枚举类型返回 MapCount=0）
[Entry × MapCount] :
  [EnumValue : INT   2B]   // 枚举数值（有符号，兼容负值枚举）
  [TextLen   : BYTE  1B]
  [Text      : TextLen B]  // UTF-8，最长 32B
```

### 4.5 READ_BY_ID 请求（CMD=0x20）

```
[Count   : BYTE  1B]            // 本次请求变量数（建议 ≤ 50）
[ParamID : UINT  2B] × Count    // 变量 ID 列表
```

### 4.6 READ_BY_ID 应答（CMD=0xA0）

```
[Count   : BYTE  1B]
[Item × Count] :
  [ParamID : UINT  2B]          // 原样返回，方便上位机对照
  [Value   : ByteSize B]        // 按各自 ByteSize 紧排，字节序 Little-Endian
```

### 4.7 WRITE_BY_ID 请求（CMD=0x21）

Flags.ACK_REQ 置 1 时主站返回 WRITE_BY_ID_ACK（CMD=0xA1）。ACK_REQ 默认值由 CycleClass 推断（见 ADR 议题3），可由 `params.json` 覆盖。

```
[Count   : BYTE  1B]
[Item × Count] :
  [ParamID : UINT  2B]
  [Value   : ByteSize B]        // 字节序 Little-Endian
```

### 4.8 WRITE_BY_ID 应答（CMD=0xA1）

```
[Count : BYTE 1B]
[Item × Count] :
  [ParamID : UINT 2B] // 写入的 ParamID，原样返回
  [WriteResult : BYTE 1B] // 0=成功, 非0=ErrCode（见§五错误码表）
```
每条 Item 固定 **3B**，总 Payload = `1 + Count × 3`（B）。

### 4.9 READ_GROUP 请求（CMD=0x22）

```
[GroupID : BYTE 1B]   // 目标分组ID
```

应答（CMD=0xA2）格式同 READ_BY_ID 应答（4.6），包含该组所有变量。

### 4.10 WRITE_GROUP 请求（CMD=0x23）

```
[GroupID : BYTE  1B]
[Count   : BYTE  1B]            // 本次写入的变量数
[Item × Count] :
  [ParamID : UINT  2B]
  [Value   : ByteSize B]
```

### 4.11 TRACE_CONFIG 请求（CMD=0x30）

```
[SampleInterval_us : UDINT  4B]   // 采样间隔（单位 μs，最小 250）
[SampleCount       : UDINT  4B]   // 采样点数（受主站 Buffer 限制）
[TriggerParamID    : UINT   2B]   // 触发变量 ParamID；0x0000=立即触发
[TriggerThreshold  : LREAL  8B]   // 触发阈值（统一用 LREAL 兼容各类型）
[TriggerEdge       : BYTE   1B]   // 0=立即, 1=上升沿, 2=下降沿, 3=电平高, 4=电平低
[ChannelCount      : BYTE   1B]   // 采样通道数（≤ 8）
[ParamID : UINT 2B] × ChannelCount  // 各通道的 ParamID
```

应答（CMD=0xB0）：
```
[Result  : BYTE 1B]   // 0=配置成功, 非0=ErrCode
[BufferCapacity : UDINT 4B]   // 主站可用 Buffer 点数
```

### 4.12 TRACE_STATUS 应答（CMD=0xB3）

```
[Status        : BYTE  1B]   // 0=空闲,1=采样中,2=已触发完成,3=Buffer满
[CollectedCount: UDINT 4B]   // 已采集点数
[Timestamp_us  : ULINT 8B]   // 触发时刻的主站时间戳（μs）
```

### 4.13 TRACE_UPLOAD 请求（CMD=0x34）

```
[FragIndex : UINT  2B]   // 请求的数据分片索引，从 0 开始
[FragSize  : UINT  2B]   // 每片期望的点数（≤ 100）
```

应答（CMD=0xB4），Flags.FRAG=1，Flags.LAST_FRAG 末片置1：

```
[FragIndex     : UINT  2B]
[PointCount    : UINT  2B]   // 本片实际点数
[ChannelCount  : BYTE  1B]
[Data] : 按时间顺序排列，每个时间点：
  [Timestamp_us : UDINT 4B]   // 相对触发时刻的偏移（μs）
  [ChValue × ChannelCount] : 各通道值，按各自 ByteSize 紧排
```

---

## 五、ERR 帧 Payload（扩展自 v1.1）

```
[ErrCode  : BYTE  1B]   // 错误码（见下表）
[OrigCMD  : BYTE  1B]   // 引发错误的原始 CMD
[ErrInfo  : UINT  2B]   // 附加信息（如非法 ParamID 值）
```

| ErrCode | 含义                         |
|---------|------------------------------|
| `0x01`  | 未知 CMD                     |
| `0x02`  | Payload 长度错误             |
| `0x03`  | CRC 校验失败                 |
| `0x04`  | ParamID 不存在               |
| `0x05`  | 变量只读，拒绝写入           |
| `0x06`  | 参数表页码越界               |
| `0x07`  | Trace Buffer 未就绪          |
| `0x08`  | 写入值超出范围（预留）       |
| `0xFF`  | 未定义错误                   |

---

## 六、关键常量速查

```
PROTO_SOF0         = 0xAA
PROTO_SOF1         = 0x55
PROTO_EOF          = 0x55
PROTO_VERSION_V2   = 0x03       // v1.1 = 0x02
PROTO_HEADLEN_V2   = 12         // v1.1 = 11
PROTO_TAILLEN      = 3          // 不变
PROTO_MAXPAYLD_V2  = 1385       // 1400 - 12 - 3
PROTO_MAXFRAME     = 1400       // 不变

PORT_V1_DATA       = 15000      // v1.1 旧周期通道，不变
PORT_V1_PARAM      = 5050       // v1.1 Param 通道，不变
PORT_V2            = 5051       // v2.0 专用端口

PARAM_DICT_PAGE_SIZE = 12       // 每页最多 12 条（原 20，因 Entry 扩展而调整）

DT_BOOL   = 0x01  DT_BYTE   = 0x02  DT_WORD   = 0x03
DT_INT    = 0x04  DT_UINT   = 0x05  DT_DWORD  = 0x06
DT_DINT   = 0x07  DT_UDINT  = 0x08  DT_REAL   = 0x09
DT_LREAL  = 0x0A  DT_LINT   = 0x0B  DT_ULINT  = 0x0C
DT_STR8   = 0x0D  DT_STR16  = 0x0E  DT_STR32  = 0x0F
DT_STR64  = 0x10  DT_STR128 = 0x11

ACCESS_RO = 0x00
ACCESS_RW = 0x01

CYCLE_2MS   = 0x00
CYCLE_10MS  = 0x01
CYCLE_100MS = 0x02
CYCLE_1S    = 0x03
CYCLE_MANUAL= 0x04

// 写入错误诊断（保留 ParamID，只读，GroupID=0，CycleClass=MANUAL）
PARAMID_WRITE_ERR_COUNT    = 0xFFF0   // WriteErrorCount
PARAMID_LAST_ERR_PARAMID   = 0xFFF1   // LastErrorParamID
PARAMID_LAST_ERR_CODE      = 0xFFF2   // LastErrorCode

// 设备标识保留 ParamID（只读，GroupID=0，CycleClass=MANUAL）
PARAMID_DEVICE_NAME        = 0xFFEF   // DeviceName，STRING32，握手时主站读取并填入 HELLO_ACK

// 完整保留区段：0xFFEF ~ 0xFFFF
// 0xFFEF  DeviceName
// 0xFFF0  WriteErrorCount
// 0xFFF1  LastErrorParamID
// 0xFFF2  LastErrorCode
// 0xFFF3 ~ 0xFFFF 保留
```

---

## 七、待确认事项（⚠️ Flag 列表）

| # | 项目 | 当前设计值 | 需确认原因 |
|---|------|-----------|------------|
| 1 | Header Reserved 字段后续用途 | 0x0000 | 是否升级为 SubCMD 或 SessionID？ |
| 2 | 每页参数表最大条目数 | 12 条 | 已按扩展后 Entry（17B+变长）重新验算，MTU 安全 |
| 3 | Trace Buffer 最大点数 | 受主站 RAM 限制 | ST 端 Buffer 数组大小待定 |
| 4 | TRACE_UPLOAD 每片最大点数 | ≤ 100 | 需结合最多 8 通道 × LREAL(8B) × 100 = 6400B 验证不超 MTU |
| 5 | 字节序 | Little-Endian | 与 v1.1 实现保持一致，需核对 |
| 6 | WRITE_BY_ID 无 ACK_REQ 时主站是否静默 | 静默（不回包）| 已通过 ADR 议题3 CycleClass 推断规则确认 |
| 7 | GET_ENUM_MAP 对非枚举类型的行为 | 返回 MapCount=0 | 待确认是否需要返回 ERR 帧 |
| 8 | Unit 字段不足 8B 时补零方式 | 末尾补 0x00 | 待确认解析端是否统一 trimNull |

---

## 八、传输场景说明

### 8.1 UDP 主场景

v2.0 协议的**设计基准场景**为 UDP/IP，端口 `5051`。所有 Payload 上限、分页策略均以 UDP MTU（1400B）为准。帧结构、CMD 集合、Payload 格式均按本文档定义执行，无需任何额外封装。

### 8.2 UART 兼容封装（外壳层）

为支持未来串口点对点部署场景（基准波特率 115200 bps），在原始 v2.0 帧外增加 **2B 长度前缀外壳（方案 B1）**。应用层语义与 UDP 场景**完全一致**，不引入新 CMD，不引入 RS-485 地址仲裁，不引入 SLIP 编码。

**UART 封装结构：**

```
[FrameLen   : UINT  2B]   // 内层 v2.0 完整帧字节数（SOF 到 EOF，含 CRC）
[v2.0 Frame : FrameLen B] // 原封不动的标准 v2.0 帧
// 内层结构：SOF(2B) + Header(12B) + Payload + CRC16(2B) + EOF(1B)
```

**接收端两状态机：**

```
STATE_WAIT_LEN  → 读取 2B FrameLen
STATE_RECV_BODY → 读 FrameLen 字节 → CRC 通过则交应用层 / 失败则回 STATE_WAIT_LEN
```

**UDP vs UART 场景参数对比：**

| 项目 | UDP 场景 | UART 场景 |
|------|----------|-----------|
| 最大总帧长（含外壳） | 1400B | 256B |
| UART 外壳 | 无 | 2B FrameLen |
| 内层最大帧长 | 1400B | 254B |
| 最大 Payload | 1385B | 239B（254 - 12 - 3） |
| 参数表分页建议 | 12 条/页 | 4 条/页 |
| CMD 集合 | v2.0 完整定义 | 完全相同，不新增专用 CMD |
| RS-485 地址仲裁 | 不适用 | **不引入**（点对点） |
| SLIP 编码 | 不适用 | **不引入** |
