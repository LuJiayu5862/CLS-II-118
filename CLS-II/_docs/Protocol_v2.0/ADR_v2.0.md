# TcLCS-UDP Protocol v2.0 — 架构决策记录（ADR）

> **文档状态**：持续更新中
> **最后更新**：2026-05-16
> **说明**：本文件记录 v2.0 协议设计过程中的关键议题与冻结决策，供后续开发参考。

---

## 议题 1：主站变量寻址方案 ✅ 已冻结

### 背景
主站（TwinCAT3 ST）需要将 ParamTable 中每个条目与实际 PLC 变量关联，使得上位机发起读写时，主站能快速定位并操作对应变量。

### 候选方案
- **方案 A**：CASE 枚举 — 每个 ParamID 对应一个 CASE 分支，直接操作具体变量
- **方案 B**：PVOID 指针 + MEMCPY — 注册阶段存储变量地址，读写时 MEMCPY
- **方案 C**：OFFSETOF 偏移 — 计算结构体内偏移量，运行时按偏移读写

### 决策：方案 B（PVOID 指针 + MEMCPY）

```pascal
// 上电 bInit 周期注册一次
ParamTable[i].pVar := ADR(stAxis.rTargetVelocity);
ParamTable[i].ByteSize := SIZEOF(REAL);

// READ
MEMCPY(pDest, ParamTable[i].pVar, ParamTable[i].ByteSize);

// WRITE
MEMCPY(ParamTable[i].pVar, pSrc, ParamTable[i].ByteSize);
```

### 理由
- 不使用 CASE 枚举：每增加变量需重编译，维护成本高
- 不使用 OFFSETOF：TwinCAT ST 支持有限，跨结构体时不可靠
- PVOID + MEMCPY：注册一次，运行时 O(1) 定位，无需修改读写逻辑

---

## 议题 2：GroupID 语义边界 ✅ 已冻结

### 决策
- 单 GroupID（BYTE 单值），每变量唯一归属一个 Group
- `GroupID = 0x00` → 不分组，仅支持 READ_BY_ID / WRITE_BY_ID
- `GroupID = 0x01~0xFE` → 有效分组，支持 READ_GROUP / WRITE_GROUP
- `GroupID = 0xFF` → 保留
- 同组变量读取保证原子性（同一 PLC 扫描周期内快照 MEMCPY）

---

## 议题 3：写操作确认机制 ✅ 已冻结

### 决策：按 CycleClass 自动推断默认 ACK 策略 + params.json 局部覆盖

| CycleClass | 值 | 默认 ACK |
|------------|-----|---------|
| CYCLE_2MS | 0x00 | 无 ACK |
| CYCLE_10MS | 0x01 | 无 ACK |
| CYCLE_100MS | 0x02 | 有 ACK |
| CYCLE_1S | 0x03 | 有 ACK |
| CYCLE_MANUAL | 0x04 | 有 ACK |

`params.json` 可对任意 ParamID 单独设置 `"ack": true/false` 覆盖默认值。

**机制说明**：
- `Flags.ACK_REQ` 由上位机在发帧时动态置位，主站被动响应（收到 ACK_REQ=1 则回包，ACK_REQ=0 则静默）
- 适用于 WRITE_BY_ID；WRITE_GROUP 的 ACK_REQ 推断规则见议题 7

**写入错误诊断保留 ParamID（Access=RO，GroupID=0，CycleClass=MANUAL）**：

| ParamID | 名称 | 说明 |
|---------|------|------|
| 0xFFF0 | WriteErrorCount | 累计写入错误次数，上电清零 |
| 0xFFF1 | LastErrorParamID | 最近一次写入错误的 ParamID |
| 0xFFF2 | LastErrorCode | 最近一次写入错误的错误码 |

---

## 议题 4：参数表动态性 + DictHash ✅ 已冻结

### 决策：启动/手动拉取一次，运行时不自动重拉，但必须具备失配检出。

**DictHash = CRC16/MODBUS**，完整计算规则见 `FrameFormat_v2.0.md` §七。

**接口落点**：
- GET_PARAM_DICT ACK（0x90）首字段携带 DictHash
- PING_ACK / PONG（0x83）在 4B 毫秒时间字段后追加 2B DictHash
- HELLO_ACK（0x85）首字段携带 DictHash

---

## 议题 4.5：参数表自描述增强 ✅ 已冻结

### 决策：Entry 新增 Unit（8B 定长）+ Desc（变长 ≤64B）；新增 GET_ENUM_MAP；新增 STRING64/STRING128。

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
Name       : NameLen B     // UTF-8，最长 32B
DescLen    : BYTE  1B
Desc       : DescLen B     // UTF-8，最长 64B
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

## 议题 5：HELLO 握手扩展 ✅ 已冻结

### 决策：HELLO_ACK（CMD=0x85）Payload 扩展：

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

保留区段完整：`0xFFEF`（DeviceName）+ `0xFFF0~0xFFF2`（诊断）= `0xFFEF~0xFFFF`

---

## 议题 6：串口场景封装 ✅ 已冻结

### 决策：UART 场景在原始 v2.0 帧外增加 2B 长度前缀外壳（方案 B1）。

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

## 议题 7：GROUP_ENTRY 注册机制 ✅ 已冻结

### 背景
WRITE_GROUP 的 ACK_REQ 推断不能依赖组内变量 CycleClass 的博弈（组内变量 CycleClass 可能不一致），需要一个写定在协议里、对上位机无额外判断负担的唯一规则。同时上位机需要能够拉取组的元信息（名称、描述、推荐频率）以支持 Watch 窗口、波形引擎等上层功能。

### 决策

**1. 新增 GROUP_ENTRY 结构**

```
GroupID    : BYTE  1B
CycleClass : BYTE  1B   // 组推荐写入频率，ACK_REQ 推断唯一来源
NameLen    : BYTE  1B
Name       : NameLen B  // UTF-8，最长 32B
DescLen    : BYTE  1B
Desc       : DescLen B  // UTF-8，最长 64B
```

**未注册 GroupID 的缺省行为**：
- Name → `"Group_0xXX"`
- Desc → 空字符串
- CycleClass → 取组内所有变量 CycleClass 最保守值（fallback）

**2. 新增指令 GET_GROUP_DICT（0x14 / 0x94）**

REQ Payload：空（PayloadLen=0）

ACK Payload：
```
[EntryCount : BYTE  1B]
[Entry × EntryCount] :
  [GroupID    : BYTE  1B]
  [CycleClass : BYTE  1B]
  [NameLen    : BYTE  1B]
  [Name       : NameLen B]
  [DescLen    : BYTE  1B]
  [Desc       : DescLen B]
```

**3. ACK_REQ 推断规则统一**

| 写指令 | ACK_REQ 默认推断来源 | 可覆盖方式 |
|--------|-------------------|--------|
| WRITE_BY_ID | 目标 ParamID 的 CycleClass | `params.json` ParamID 级 `"ack"` |
| WRITE_GROUP | GROUP_ENTRY.CycleClass | `params.json` GroupID 级 `"ack"` |

两条规则形式完全一致，上位机可共用同一套推断函数。

**4. GROUP_ENTRY 纳入现有 DictHash 计算**

DictHash 计算输入新增（按 GroupID 升序追加在 PARAM_ENTRY 之后）：
```
GroupID(1B) + CycleClass(1B) + NameLen(1B) + Name(NameLen B)
```
- **参与 Hash**：GroupID, CycleClass, NameLen, Name
- **不参与 Hash**：DescLen, Desc
- 与 PARAM_ENTRY 共用同一个 DictHash，握手时一并校验，无需新增 Hash 字段

**5. 使用场景边界**

| 场景 | 推荐指令 | ACK 行为 |
|------|---------|--------|
| 多变量联动控制（Position/Velocity/Torque 等） | WRITE_GROUP | 由 GROUP_ENTRY.CycleClass 推断，高频组默认无 ACK |
| Watch 窗口手动配置参数 | WRITE_BY_ID | 低频，默认有 ACK，可监控写入失败 |
| 单变量波形输出（高频跟随） | WRITE_BY_ID 单条 | params.json 覆盖 `"ack": false`，Fire & Forget |
