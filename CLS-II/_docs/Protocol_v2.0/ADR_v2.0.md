# TcLCS-UDP Protocol v2.0 — 架构决策记录（ADR）

> **文档状态**：持续更新中
> **最后更新**：2026-05-18
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
| 参数表分页 | 10 条/页（见议题 10） | 推荐 PageSize=2~3，最保守 1 |
| CMD 集合 | v2.0 完整 | 完全相同，不新增 |

**不引入**：RS-485 地址仲裁、SLIP 编码、串口专用 CMD。

---

## 议题 7：GROUP_ENTRY 注册机制 ✅ 已冻结（2026-05-18 第二次修订）

### 背景

WRITE_GROUP 的 ACK_REQ 不再依赖 CycleClass 推断（见议题 3 修订）。同时，后续主站注册模型讨论中发现，GROUP 在上位机中还承担 UI 分组职责，例如 TreeView 和参数表格按组展示。此类场景并不要求强原子性，且组内部分变量可能因 Access、WriteEnable 或值域约束失败，但其他变量仍应允许写入。因此，WRITE_GROUP 语义从"全量原子写"修订为"尽力写入（best-effort）+ 支持子集写"。

### 决策

**1. GROUP_ENTRY 结构**

```
GroupID     : BYTE  1B
CycleClass  : BYTE  1B   // 组推荐操作频率 / 读取侧参考，不参与 ACK 推断
MemberCount : BYTE  1B   // 本组已注册 PARAM 条目数，主站自动统计
NameLen     : BYTE  1B
Name        : NameLen B  // UTF-8，最长 32B
DescLen     : BYTE  1B
Desc        : DescLen B  // UTF-8，最长 64B
```

`CycleClass` 仅描述组级推荐读取 / 操作频率，服务于上位机订阅间隔、轮询频率、UI 默认刷新建议等读取侧行为，不参与 ACK_REQ 推断。

**2. 新增指令 GET_GROUP_DICT（0x13 / 0x93）**

REQ Payload：空（PayloadLen=0）

ACK Payload：

```
[EntryCount : BYTE  1B]   // 仅含已注册的 GROUP_ENTRY
[Entry × EntryCount] :
  [GroupID     : BYTE  1B]
  [CycleClass  : BYTE  1B]   // 读取侧推荐频率，不参与 ACK 推断
  [MemberCount : BYTE  1B]   // 本组已注册 PARAM 条目数
  [NameLen     : BYTE  1B]
  [Name        : NameLen B]
  [DescLen     : BYTE  1B]
  [Desc        : DescLen B]
```

`MemberCount` 由主站在参数注册完成后自动统计，上位机可用其快速展示组规模，并辅助构建组内参数表和 WRITE_GROUP 操作。

**3. ACK_REQ 决策规则**

| 写指令 | ACK_REQ 决策来源 | 说明 |
|--------|-----------------|------|
| WRITE_BY_ID | 上位机操作类型：周期写=0，手动写=1 | 不依赖 CycleClass 推断 |
| WRITE_GROUP | 上位机操作类型：周期写=0，手动写=1 | 不依赖 CycleClass 推断 |

上位机业务层统一判断操作类型后直接置位 `Flags.ACK_REQ`，主站无条件响应 ACK_REQ 标志。CycleClass 完全退出 ACK 推断。

**4. WRITE_GROUP 尽力写入语义（best-effort）**

WRITE_GROUP 支持子集写，`Count` 无需等于该 GroupID 下已注册变量总数。帧内 ParamID 顺序不作要求，主站按 ParamID 逐条查表写入，可与 WRITE_BY_ID 复用同一套解析和校验逻辑。

主站对每个 Item 独立执行写入判断：

```
① ParamID 不存在 / 不属于目标 GroupID → WriteResult = 0x0A（越组写入）
② Access = RO                          → WriteResult = 0x05
③ pWriteEnable 非空且值为 FALSE        → WriteResult = 0x0E
④ 值域超出 RangeFlags 约束             → WriteResult = 0x08
⑤ 条件满足，MEMCPY 写入               → WriteResult = 0x00
```

无论某个 Item 成功或失败，主站均继续处理后续 Item。若 `Flags.ACK_REQ=1`，ACK 中逐条返回 `[ParamID(2B) + WriteResult(1B)]`，上位机根据每个变量的 WriteResult 显示成功 / 失败状态。

协议层不保证 WRITE_GROUP 原子性。若业务场景需要强关联一致性，上位机应在收到 ACK 后检查全部 WriteResult，并由业务层执行报警、回滚或重新写入策略。

**GROUP 的两种使用场景均被支持**：
- **UI 分组**（TreeView / 参数表格按组展示）→ 子集写，各自独立，部分失败上位机按 WriteResult 显示
- **强关联配置写入**（如 PID 三参数）→ 全量写，上位机收 ACK 后校验所有 WriteResult，有失败则业务层回滚 / 报警

**5. WRITE_GROUP 错误语义**

`ErrCode=0x0A` 定义为：WRITE_GROUP 中存在不属于目标 GroupID 的 ParamID（越组写入）。对于单个变量写入失败（只读、写入前提不满足、值域越界），不返回整帧 ERR，而是在 ACK 的该变量 `WriteResult` 中返回对应 ErrCode。

**6. GROUP_ENTRY 纳入 DictHash 计算**

GROUP_ENTRY 按 GroupID 升序追加在 PARAM_ENTRY 之后：

```
GroupID(1B) + MemberCount(1B) + NameLen(1B) + Name(NameLen B)
```

| 字段 | 参与 Hash | 理由 |
|------|-----------|------|
| GroupID | ✅ | 影响组寻址 |
| MemberCount | ✅ | 影响组规模展示与组操作构建 |
| NameLen + Name | ✅ | 影响组识别 |
| CycleClass | ❌ | 读取侧建议值，不影响协议行为 |
| DescLen + Desc | ❌ | 展示信息 |

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

---

## 议题 10：PARAM_ENTRY 值域范围字段 ✅ 已冻结（2026-05-18 新增）

### 背景

上位机进行手动写入时，需要在界面侧提供输入范围提示与预校验；主站也需要在最终写入前执行值域保护。原 PARAM_ENTRY 仅描述变量类型、长度、访问权限和展示信息，无法表达数值范围约束。

### 决策：PARAM_ENTRY 新增 RangeFlags / MinVal / MaxVal

PARAM_ENTRY 在 `Desc` 之后、运行时字段之前新增三个字段：

```
RangeFlags : BYTE   1B   // Bit0=MinValid, Bit1=MaxValid；0x00=无范围约束
MinVal     : LREAL  8B   // 最小值，RangeFlags.Bit0=0 时忽略
MaxVal     : LREAL  8B   // 最大值，RangeFlags.Bit1=0 时忽略
```

PARAM_ENTRY 固定传输部分从 17B 增至 **34B**。

### RangeFlags 语义

| Bit | 名称 | 含义 |
|-----|------|------|
| Bit0 | MinValid | 1=MinVal 有效，写入值不得小于 MinVal |
| Bit1 | MaxValid | 1=MaxVal 有效，写入值不得大于 MaxVal |
| Bit2~7 | Reserved | 保留，填 0 |

### 各 DataType 处理规则

| DataType | RangeFlags 规则 | 说明 |
|----------|----------------|------|
| BOOL | 强制 0x00 | BOOL 不做范围检查 |
| STRING8 / STRING16 / STRING32 / STRING64 / STRING128 | 强制 0x00 | 字符串不做数值范围检查 |
| ULINT | 可按工程实际使用，接受 LREAL/double 对大整数的精度限制 | 实际工控参数通常不在 uint64 极端值域范围做约束 |
| 其他数值类型 | 按需设置 | 写入时转换为 LREAL 后比较 |

### 主站写入检查规则

写入前，主站将待写入值按 DataType 转换为 LREAL 后执行范围判断：

```pascal
IF (RangeFlags AND 16#01) <> 0 THEN
    IF ValueAsLReal < MinVal THEN WriteResult := 16#08; END_IF
END_IF
IF (RangeFlags AND 16#02) <> 0 THEN
    IF ValueAsLReal > MaxVal THEN WriteResult := 16#08; END_IF
END_IF
```

`ErrCode=0x08` 从预留状态转为正式定义：**写入值超出注册值域范围**。

### GET_PARAM_DICT 分页修订（连带修订）

由于 PARAM_ENTRY 固定传输部分增至 34B，GET_PARAM_DICT REQ Payload 修订为：

```
[PageIndex : UINT 2B]
[PageSize  : BYTE 1B]   // 每页期望条目数；0=使用主站默认值
```

主站行为：

| PageSize | 行为 |
|----------|------|
| 0 | 使用主站默认 `PARAM_DICT_PAGE_SIZE = 10` |
| 1~10 | 使用请求值 |
| >10 | 按 10 处理，防止超过 UDP 安全上限 |

UART 场景由上位机根据链路限制设置较小 PageSize：
- 最保守：`PageSize=1`（最坏 Entry=132B，安全余量充足）
- 典型场景（Name≈10B，无 Desc）：`PageSize=2~3`

**`PARAM_DICT_PAGE_SIZE` 从 12 改为 10**（UDP 最坏 1327B ≤ MTU 1385B，安全余量 58B）。

### DictHash 规则

`RangeFlags / MinVal / MaxVal` **参与协议传输**（GET_PARAM_DICT / GET_PARAM_BY_ID），**不参与 DictHash**。

理由：范围约束用于界面预校验和主站写入保护，不影响 ParamID 寻址、DataType 解析、ByteSize 解析、Access 判断或 GroupID 分组行为。其变更不强制触发参数表结构重拉。

---

## 议题 11：写入前提条件 WriteEnable ✅ 已冻结（2026-05-18 新增）

### 背景

部分参数虽然 Access=RW，但只应在特定运行时状态下允许写入（例如：电机停止时才允许修改目标参数）。Access 字段只能表达静态只读 / 可写，无法表达运行时条件。

### 决策：PARAM_ENTRY 新增运行时写入前提指针

PARAM_ENTRY 增加一个纯主站运行时字段，**不参与协议传输，不参与 DictHash**：

| 平台 | 字段声明 | 空值语义 |
|------|----------|----------|
| TwinCAT ST | `pWriteEnable : POINTER TO BOOL` | `0`（NULL）= 无条件可写 |
| 嵌入式 C | `bool *p_write_enable` | `NULL` = 无条件可写 |

注册示例：

```pascal
// TwinCAT ST：无条件可写
ParamTable[i].pWriteEnable := 0;

// TwinCAT ST：条件写入（电机停止时才允许）
ParamTable[i].pWriteEnable := ADR(bMotorStopped);
```

```c
// C：无条件可写
entry.p_write_enable = NULL;

// C：条件写入
entry.p_write_enable = &g_motor_stopped;
```

### 主站写入判断顺序（WRITE_BY_ID 与 WRITE_GROUP 共用）

```
① ParamID 不存在                  → 0x04
② Access = RO                     → 0x05
③ pWriteEnable 非空且值为 FALSE   → 0x0E
④ 值域超出 RangeFlags 约束         → 0x08
⑤ MEMCPY 写入成功                 → 0x00
```

### 新增 ErrCode

| ErrCode | 含义 |
|---------|------|
| `0x0E` | 写入前提条件不满足，主站运行时状态拒绝写入 |

上位机无需感知 `pWriteEnable` 的存在，不通过 GET_PARAM_DICT 拉取该字段。收到 `WriteResult=0x0E` 时，界面提示"当前状态不允许写入"即可。

---

## 议题 12：GROUP_ENTRY 成员数量描述 MemberCount ✅ 已冻结（2026-05-18 新增）

### 背景

上位机需要快速知道每个 Group 包含多少个参数，以便构建 TreeView、参数表格、组操作界面和 WRITE_GROUP 预期项数。若仅依赖 PARAM_ENTRY.GroupID，上位机必须先完整拉取参数表再自行统计，效率较差。

### 决策：GET_GROUP_DICT 返回 MemberCount

GROUP_ENTRY 新增 `MemberCount` 字段，由主站在 PARAM_ENTRY 注册完成后自动统计，不需要手动配置。

完整 GET_GROUP_DICT ACK Entry 格式：

```
[GroupID     : BYTE  1B]
[CycleClass  : BYTE  1B]   // 读取侧推荐频率，不参与 ACK 推断
[MemberCount : BYTE  1B]   // 本组已注册 PARAM 条目数，主站自动统计
[NameLen     : BYTE  1B]
[Name        : NameLen B]
[DescLen     : BYTE  1B]
[Desc        : DescLen B]
```

### DictHash 规则

`MemberCount` **参与 DictHash**，GROUP_ENTRY 字节串更新为：

```
GroupID(1B) + MemberCount(1B) + NameLen(1B) + Name(NameLen B)
```

理由：组成员数量变化影响上位机对组规模、组展示和组操作的理解，属于影响协议行为的字段。`CycleClass` 仍不参与 DictHash。

---

## ErrCode 完整速查表

| ErrCode | 含义 | 状态 |
|---------|------|------|
| 0x00 | 成功 | ✅ 正式 |
| 0x01 | 未知 CMD | ✅ 正式 |
| 0x02 | PayloadLen 不合法 | ✅ 正式 |
| 0x03 | CRC 错误 | ✅ 正式 |
| 0x04 | ParamID 不存在 | ✅ 正式 |
| 0x05 | 变量只读（Access=RO） | ✅ 正式 |
| 0x06 | DataType 不匹配 | ✅ 正式 |
| 0x07 | 参数表未初始化 | ✅ 正式 |
| 0x08 | 写入值超出注册值域范围 | ✅ 正式（议题 10）|
| 0x09 | GroupID 不存在 | ✅ 正式 |
| 0x0A | 越组写入（ParamID 不属于目标 GroupID）| ✅ 正式（议题 7 修订）|
| 0x0B | 订阅槽位已满 | ✅ 正式（议题 8）|
| 0x0C | SubID 不存在或已过期 | ✅ 正式（议题 8）|
| 0x0D | 订阅变量数超出单槽上限 | ✅ 正式（议题 8）|
| 0x0E | 写入前提条件不满足 | ✅ 正式（议题 11）|
| 0x0F~0xFF | 预留 | — |
