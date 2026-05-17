# TcLCS-UDP Protocol v2.0 — 架构决策记录（ADR）

> **文档状态**：持续更新中
> **最后更新**：2026-05-17
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

## 议题 3：写操作确认机制 ✅ 已冻结（2026-05-17 修订）

### 修订说明

原设计（按 CycleClass 自动推断默认 ACK 策略）已废除。修订原因：CycleClass 描述的是变量的物理更新周期，与上位机的操作意图（周期自动写 vs 用户手动写）没有必然关联，基于 CycleClass 的推断在两个典型场景均给出错误答案：

- 一个 `CYCLE_2MS` 的变量，手动写入时需要确认是否写入成功 → 应有 ACK；但原规则推断为无 ACK
- 一个 `CYCLE_1S` 的变量，周期写时 fire & forget → 不需要 ACK；但原规则推断为有 ACK

### 决策：ACK_REQ 由上位机根据操作类型直接决定，主站无条件响应

| 操作类型 | Flags.ACK_REQ | 决定方 |
|----------|--------------|--------|
| 周期写（periodic_write） | 0 — 无 ACK（fire & forget） | 上位机操作类型 |
| 手动写入（用户触发） | 1 — 有 ACK | 上位机操作类型 |
| 主站行为 | 无条件响应 ACK_REQ 标志 | 协议层 |

**CycleClass 完全退出 ACK 推断**，`Flags.ACK_REQ` 由上位机业务层在发帧时直接置位，不再依赖参数表中任何字段推断。

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

### 决策：Entry 新增 Unit（8B 定长）+ Desc（变长 ≤64B）；新增 GET_ENUM_MAP；新增 STRING64(0x10) / STRING128(0x11)。

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

**GET_ENUM_MAP（CMD 0x12 / ACK 0x92）**：
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
| 最大 Payload | 1385B | 239B（254-12-3）|
| 参数表分页 | 12 条/页 | 推荐 4 条/页 |
| CMD 集合 | v2.0 完整 | 完全相同，不新增 |

**不引入**：RS-485 地址仲裁、SLIP 编码、串口专用 CMD。

---

## 议题 7：GROUP_ENTRY 注册机制 ✅ 已冻结（2026-05-17 修订）

### 背景
WRITE_GROUP 的 ACK_REQ 不再依赖 CycleClass 推断（见议题 3 修订），同时上位机需要能够拉取组的元信息（名称、描述、推荐频率）以支持 Watch 窗口、波形引擎等上层功能。

### 决策

**1. 新增 GROUP_ENTRY 结构**

```
GroupID    : BYTE  1B
CycleClass : BYTE  1B   // 组推荐操作周期，纯建议值，不参与 ACK 推断
NameLen    : BYTE  1B
Name       : NameLen B  // UTF-8，最长 32B
DescLen    : BYTE  1B
Desc       : DescLen B  // UTF-8，最长 64B
```

**未注册 GroupID 的主站缺省行为**（不出现在 GET_GROUP_DICT 应答中）：
- CycleClass → 取组内所有变量 CycleClass 最保守值（最大编号，fallback）
- Name → `"Group_0xXX"`（上位机展示用，主站不存储）
- Desc → 空字符串

**2. 新增指令 GET_GROUP_DICT（0x13 / 0x93）**

REQ Payload：空（PayloadLen=0）

ACK Payload：
```
[EntryCount : BYTE  1B]   // 仅含已注册的 GROUP_ENTRY
[Entry × EntryCount] :
  [GroupID    : BYTE  1B]
  [CycleClass : BYTE  1B]   // 物理操作周期建议值，不参与 ACK 推断
  [NameLen    : BYTE  1B]
  [Name       : NameLen B]
  [DescLen    : BYTE  1B]
  [Desc       : DescLen B]
```

**3. ACK_REQ 决策规则**

| 写指令 | ACK_REQ 决策来源 | 说明 |
|--------|-----------------|------|
| WRITE_BY_ID | 上位机操作类型：周期写=0，手动写=1 | 不依赖 CycleClass 推断 |
| WRITE_GROUP | 上位机操作类型：周期写=0，手动写=1 | 不依赖 CycleClass 推断 |

两条规则完全一致，上位机业务层统一判断操作类型后直接置位 `Flags.ACK_REQ`，无需查阅参数表字段。

**4. WRITE_GROUP 全量写语义**

WRITE_GROUP 必须包含该 GroupID 下所有已注册变量，不支持子集写（子集写请使用 WRITE_BY_ID）。若 Count 或 ParamID 集合与注册信息不匹配，主站返回 ERR 帧（ErrCode=`0x09` 或 `0x0A`）。

**5. GROUP_ENTRY 纳入 DictHash 计算**

（按 GroupID 升序追加在 PARAM_ENTRY 之后）：

```
修订后：GroupID(1B) + NameLen(1B) + Name(NameLen B)
```

| 字段 | 参与 Hash | 理由 |
|------|-----------|------|
| GroupID | ✅ | 影响 READ_GROUP / WRITE_GROUP 组寻址 |
| NameLen + Name | ✅ | 影响组识别 |
| CycleClass | ❌ | 仅为建议值，不参与 ACK 推断，变更不影响协议行为 |
| DescLen + Desc | ❌ | 展示信息 |

> **修订说明**：原设计中 GROUP_ENTRY.CycleClass 参与 Hash，理由是「它是 WRITE_GROUP ACK_REQ 推断的唯一来源」。议题 3 修订后 ACK_REQ 改由上位机操作类型直接决定，该理由不再成立，故从 DictHash 中移除。

**6. 使用场景边界**

| 场景 | 推荐指令 | ACK 行为 |
|------|---------|--------|
| 多变量联动配置（Position/Velocity/Torque 等周期写） | WRITE_GROUP | ACK_REQ=0，fire & forget |
| 多变量联动手动写 | WRITE_GROUP | ACK_REQ=1，需确认写入状态 |
| Watch 窗口手动配置参数 | WRITE_BY_ID | ACK_REQ=1，可监控写入失败 |
| 单变量波形输出（高频跟随） | WRITE_BY_ID 单条 | ACK_REQ=0，fire & forget |

---

## 议题 8：SUBSCRIBE / NOTIFY 推送机制 ✅ 已冻结

### 背景
上位机 Watch 功能需要订阅任意变量并持续接收更新，若依赖 READ_BY_ID 周期轮询，则每次都需要上位机主动发帧，增加总线负载并提高上位机逻辑复杂度。引入订阅-推送机制后，上位机注册一次，主站周期主动推送，无需轮询。

### 决策

**1. 推送模式：Periodic Only**

当前版本仅支持固定周期推送（`Mode=0x00`）。Deadband 过滤（On-Change / Hybrid 模式）明确**不在 v2.0 实现范围内**，原因如下：
- 系统为点对点单节点架构，UDP 带宽充裕，无需过滤小波动以节省带宽
- 高频信号（如 Actual Current）已由 Trace Buffer 专用通道承担，无需 SUBSCRIBE 处理
- 嵌入式平台（STM32F103）资源有限，避免增加浮点比较与多类型 deadband 实现复杂度

`Mode` 字段作为扩展预留位保留。未来若系统演进至多节点/带宽受限场景，可通过 Minor 版本升级在新 ADR 中引入，完全向后兼容（老上位机永远发送 `Mode=0x00`）。

**2. CMD 分区**

| 区段 | 范围 | 说明 |
|------|------|------|
| 订阅管理（C→S） | `0x50~0x52` | PARAM_SUBSCRIBE / PARAM_UNSUBSCRIBE / SUBSCRIBE_CLEAR |
| 主站主动推送（S→C） | `0x40` | NOTIFY；`0x41~0x4F` 预留给未来告警等主动上报 |

订阅管理指令遵循 REQ\|0x80=ACK 惯例（`0x50→0xD0` 等）。NOTIFY（`0x40`）为主站主动发出帧，无对应 REQ，不遵循该惯例，高位不置 1，语义上属于「主站主动上报」类别。

**3. NOTIFY 帧行为**

- `Flags.ACK_REQ` 恒为 0，上位机静默处理丢包，无需回应
- 上位机可通过 SeqNum 跳变检测丢包（仅用于诊断，无重传机制）

**4. 断线 / 重连行为**

主站收到 HELLO_REQ（CMD=0x05）时，清除全部订阅槽位。订阅生命周期与连接会话绑定，断线即失效。上位机重连后需重新发起订阅。

**5. 槽位上限：实现端编译宏自行决定**

协议层不强制规定槽位上限，由实现端根据平台资源通过编译宏配置。

推荐值：

| 平台 | MAX_SUB_SLOTS | MAX_SUB_VARS（每槽）| 估算内存占用 |
|------|--------------|-------------------|------------|
| 工控机（4GB+） | 32 | 20 | ~4KB |
| 嵌入式底线（STM32F103，20KB SRAM） | 8 | 8 | ~520B |

**6. Payload 格式**（见 `FrameFormat_v2.0.md` §4.14~§4.17）

| CMD | 名称 | 关键字段 |
|-----|------|---------|
| 0x50 / 0xD0 | PARAM_SUBSCRIBE | SubID, Mode, Interval_ms, Count, ParamID列表 |
| 0x51 / 0xD1 | PARAM_UNSUBSCRIBE | SubID |
| 0x52 / 0xD2 | SUBSCRIBE_CLEAR | 无 REQ Payload；ACK 返回 ClearedCount |
| 0x40 | NOTIFY | SubID, Timestamp, Count, Value列表 |

**7. 新增错误码**

| ErrCode | 含义 |
|---------|------|
| `0x0B` | 订阅槽位已满（超出平台 MAX_SUB_SLOTS） |
| `0x0C` | SubID 不存在或已过期 |
| `0x0D` | 订阅变量数超出单槽上限（超出平台 MAX_SUB_VARS） |

---

## 议题 9：参数表语义定位与写操作模型 ✅ 已冻结（2026-05-17 新增）

### 决策背景

在议题 3 修订过程中发现，CycleClass 原本同时承载了「物理更新周期」和「ACK 推断依据」两个语义，导致参数表的职责边界模糊。本议题对参数表语义和写操作模型进行重新定位，统一设计原则。

### 决策

**原则一：参数表只描述变量的物理属性，不预定义操作行为。**

| 字段 | 语义 | 服务方向 |
|------|------|---------|
| `ParamID` | 变量唯一标识 | 寻址 |
| `DataType / ByteSize` | 数据类型与长度 | 解析 |
| `Access` | RO / RW | 写入合法性判断 |
| `GroupID` | 所属分组 | 组操作寻址 |
| `CycleClass` | 变量物理更新周期 | **读取侧**参考（订阅间隔 / 轮询建议） |
| `Unit / Desc` | 单位与描述 | 展示信息 |

**原则二：写操作由上位机业务层完全自主决定。**

- **周期写**：上位机勾选 `periodic_write`，按 CycleClass 建议周期自动生成 WRITE_GROUP 帧，`Flags.ACK_REQ=0`（fire & forget）
- **手动写入**：用户主动触发，使用 WRITE_BY_ID 或 WRITE_GROUP，`Flags.ACK_REQ=1`，需要 ACK 确认各 ParamID 写入状态

**原则三：功能-协议路径明确划分。**

| 上位机功能 | 协议路径 | 备注 |
|-----------|---------|------|
| Watch 窗口 | `SUBSCRIBE / READ_BY_ID` | 用户任意添加 ParamID |
| Scope 示波器 | `SUBSCRIBE`（5~10ms） | 周期推送，UDP 延时可容忍 |
| 高频采样 | `TRACE`（≥250μs） | 主站本地采集后上传 |
| 周期配置写 | `WRITE_GROUP`（ACK_REQ=0） | 强关联参数组，原子写入 |
| 手动参数写 | `WRITE_BY_ID / WRITE_GROUP`（ACK_REQ=1） | 需要确认写入状态 |

**原则四：GROUP 的职责定位。**

GROUP 专门服务于**结构化配置参数**场景——一组强关联变量需要原子写入保证时使用。Watch / Scope / 高频采样均不依赖 GROUP，使用 ParamID 粒度操作。
