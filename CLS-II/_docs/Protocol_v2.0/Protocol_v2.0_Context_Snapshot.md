# CLS-II Protocol v2.0 — 设计上下文快照

> **生成时间**：2026-05-16
> **仓库**：LuJiayu5862/CLS-II-118
> **分支**：dev/protocol-v2.0
> **文档路径**：`CLS-II/_docs/Protocol_v2.0/`
> **用途**：跨 Thread 上下文恢复，直接粘贴到新 Thread 开头即可继续工作。

---

## 当前文档状态

| 文件 | 状态 |
|------|------|
| `ADR_v2.0.md` | ✅ 已全量更新，议题 1~6 全部冻结 |
| `FrameFormat_v2.0.md` | ✅ 已全量更新（含 PING DictHash、HELLO_ACK、UART 章节） |
| `README.md` | ✅ 已更新（含 UART 场景说明、保留 ParamID 表、文档列表） |
| `Protocol_v2.0_Context_Snapshot.md` | ✅ 本文件 |
| `ParamEntry_DataStructure.md` | 🔲 待编写 |
| `TraceBuffer_Design.md` | 🔲 待编写 |
| `CSharp_ParamDictionary.md` | 🔲 待编写 |
| `Migration_v1.1_to_v2.0.md` | 🔲 待编写 |

---

## 已冻结议题全量摘要

### 议题 1：主站变量寻址方案 ✅
**决策**：PVOID 指针 + MEMCPY。
- 上电 bInit 周期注册一次：`ParamTable[i].pVar = ADR(变量)`
- READ → `MEMCPY(pDest, pVar, ByteSize)`
- WRITE → `MEMCPY(pVar, pSrc, ByteSize)`
- 不使用 CASE 枚举（每增变量需重编译）
- 不使用 OFFSETOF（TwinCAT ST 支持有限）

---

### 议题 2：GroupID 语义边界 ✅
**决策**：
- 单 GroupID（BYTE 单值），每变量唯一归属一个 Group
- `GroupID = 0x00` → 不分组，仅支持 READ_BY_ID / WRITE_BY_ID
- `GroupID = 0x01~0xFE` → 有效分组，支持 READ_GROUP / WRITE_GROUP
- `GroupID = 0xFF` → 保留
- 同组变量读取保证原子性（同一 PLC 扫描周期内快照 MEMCPY）

---

### 议题 3：写操作确认机制 ✅
**决策**：按 CycleClass 自动推断默认 ACK 策略 + params.json 局部覆盖。

| CycleClass | 值 | 默认 ACK |
|------------|-----|---------|
| CYCLE_2MS | 0x00 | 无 ACK |
| CYCLE_10MS | 0x01 | 无 ACK |
| CYCLE_100MS | 0x02 | 有 ACK |
| CYCLE_1S | 0x03 | 有 ACK |
| CYCLE_MANUAL | 0x04 | 有 ACK |

`params.json` 可对任意 ParamID 单独设置 `"ack": true/false` 覆盖默认值。

**写入错误诊断保留 ParamID（Access=RO，GroupID=0，CycleClass=MANUAL）**：

| ParamID | 名称 | 说明 |
|---------|------|------|
| 0xFFF0 | WriteErrorCount | 累计写入错误次数，上电清零 |
| 0xFFF1 | LastErrorParamID | 最近一次写入错误的 ParamID |
| 0xFFF2 | LastErrorCode | 最近一次写入错误的错误码 |

---

### 议题 4：参数表动态性 + DictHash ✅
**决策**：启动/手动拉取一次，运行时不自动重拉，但必须具备失配检出。

**DictHash = CRC16/MODBUS**，计算输入（按 ParamID 升序拼接）：
```
ParamID(2B) + DataType(1B) + ByteSize(1B) + Access(1B) +
GroupID(1B) + NameLen(1B) + Name(NameLen B)
```

**参与哈希**：ParamID, DataType, ByteSize, Access, GroupID, NameLen, Name  
**不参与哈希**：CycleClass, Unit, DescLen, Desc

**接口落点**：
- GET_PARAM_DICT_ACK（0x90）首字段携带 DictHash
- PING_ACK / PONG（0x83）在 4B 毫秒时间戳后追加 2B DictHash

---

### 议题 4.5：参数表自描述增强 ✅
**决策**：Entry 新增 Unit（8B 定长）+ Desc（变长 ≤64B）；新增 GET_ENUM_MAP（0x13/0x93）；新增 STRING64(0x10) / STRING128(0x11)。

**新 PARAM_ENTRY 结构**：
```
ParamID    : UINT  2B
DataType   : BYTE  1B
ByteSize   : BYTE  1B
Access     : BYTE  1B
GroupID    : BYTE  1B
CycleClass : BYTE  1B
Unit       : BYTE  8B   // 定长，UTF-8，不足补0
NameLen    : BYTE  1B
Name       : NameLen B  // UTF-8，最长 32B
DescLen    : BYTE  1B
Desc       : DescLen B  // UTF-8，最长 64B
```
固定部分 17B（原 9B + Unit 8B），变长 Name+Desc 上限 96B。  
**UDP 场景每页最大条目数 = 12**（最坏 115B×12=1380B ≤ MTU 1385B）

**GET_ENUM_MAP（CMD 0x13 / ACK 0x93）**：
- REQ: `[ParamID: UINT 2B]`
- ACK: `[ParamID 2B][MapCount 1B][{EnumValue:INT 2B, TextLen 1B, Text≤32B} × MapCount]`

**新增 DataType**：

| 值 | 类型 | ByteSize | TwinCAT ST |
|----|------|----------|-----------|
| 0x10 | STRING64 | 64 | STRING(63) |
| 0x11 | STRING128 | 128 | STRING(127) |

---

### 议题 5：HELLO 握手扩展 ✅
**决策**：HELLO_ACK（CMD=0x85）Payload 扩展：

```
[DictHash   : UINT  2B]      // 参数表结构哈希，握手即缓存
[ParamCount : UINT  2B]      // 参数表总条目数
[FW_Major   : BYTE  1B]      // 固件主版本号
[FW_Minor   : BYTE  1B]      // 固件次版本号
[FW_Patch   : BYTE  1B]      // 固件补丁版本号
[DevNameLen : BYTE  1B]      // DeviceName 字节数（最长 32B）
[DeviceName : DevNameLen B]  // UTF-8，无 null 结尾，来源：ParamID 0xFFEF
```
固定部分 8B + 变长 DeviceName（1~32B）。

**FirmwareVersion SemVer 语义**：

| 段 | 触发条件 | 上位机行为 |
|----|----------|-----------|
| Major 不一致 | 帧结构/指令码根本重构 | 拒绝连接 |
| Minor 上位机 > 主站 | 固件版本较旧 | 降级运行，提示警告 |
| Minor 上位机 ≤ 主站 | 正常 | 正常连接 |
| Patch 差异 | 仅缺陷修复 | 静默忽略，始终兼容 |

**新增保留 ParamID**：

| ParamID | 名称 | DataType | Access | 说明 |
|---------|------|----------|--------|------|
| 0xFFEF | DeviceName | STRING32 | RO | 握手时主站读取并填入 HELLO_ACK |

完整保留区段：`0xFFEF`（DeviceName）+ `0xFFF0~0xFFF2`（诊断）= `0xFFEF~0xFFFF`

---

### 议题 6：串口场景封装 ✅
**决策**：UART 场景在原始 v2.0 帧外增加 2B 长度前缀外壳（方案 B1）。

**场景边界**：点对点，未来兼容性目标，基准波特率 115200 bps。

**UART 封装结构**：
```
[FrameLen   : UINT  2B]   // 内层 v2.0 完整帧字节数
[v2.0 Frame : FrameLen B] // 原封不动的标准 v2.0 帧
// SOF(2B)+Header(12B)+Payload+CRC16(2B)+EOF(1B)
```

**接收端两状态机**：
```
STATE_WAIT_LEN  → 读取 2B FrameLen
STATE_RECV_BODY → 读 FrameLen 字节 → CRC 通过交应用层 / 失败回 WAIT_LEN
```

**UART 专用约束**：

| 项目 | UDP 场景 | UART 场景 |
|------|----------|-----------|
| 最大总帧长（含外壳） | 1400B | 256B |
| UART 外壳 | 无 | 2B FrameLen |
| 内层最大 | 1400B | 254B |
| 最大 Payload | 1385B | 239B（254-12-3） |
| 参数表分页 | 12 条/页 | 推荐 4 条/页 |
| CMD 集合 | v2.0 定义 | 完全相同，不新增 |

**不引入**：RS-485 地址仲裁、SLIP 编码、串口专用 CMD。

---

## 技术栈备忘

- **主站**：TwinCAT 3 XAE，IEC 61131-3 ST 语言
- **上位机**：C# .NET 4.7.2 WinForms，Visual Studio
- **通信**：UDP（主场景，端口 5051），UART 115200（未来兼容）
- **仓库**：https://github.com/LuJiayu5862/CLS-II-118/tree/dev/protocol-v2.0/CLS-II/_docs/Protocol_v2.0

---

## 下一步待办（供新 Thread 参考）

| 文件 | 任务 |
|------|------|
| `ParamEntry_DataStructure.md` | 编写 ST 端 PARAM_ENTRY 结构体定义与注册宏 |
| `TraceBuffer_Design.md` | 设计 Trace Buffer 功能块（FB_TraceBuffer）ST 实现 |
| `CSharp_ParamDictionary.md` | C# 端 ParamDictionary 类设计，含 DictHash 校验逻辑 |
| `Migration_v1.1_to_v2.0.md` | v1.1 → v2.0 迁移指南，含端口/版本号切换步骤 |
