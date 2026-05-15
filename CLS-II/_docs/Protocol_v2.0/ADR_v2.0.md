# Protocol v2.0 设计决策记录（ADR）

> 每个议题讨论完成后即时存档，确保切换 Thread 后全量恢复设计决策上下文。
> **最后更新**：2026-05-15

---

## 议题 1：主站变量寻址方案 ✅ 已冻结

### 问题

主站收到 `READ_BY_ID` / `WRITE_BY_ID` 请求后，如何从 `ParamID` 映射到实际 PLC 变量并读写？

### 候选方案

| 方案 | 方式 | 优点 | 缺点 |
|------|------|------|------|
| A | `PVOID` 指针 + `MEMCPY` | O(1) 寻址，性能最优；增减变量只改注册表，`FB_ParamServer` 零改动 | 指针在 Runtime 重启后需重新初始化 |
| B | `CASE` 枚举 | 类型安全，调试友好 | 每增一个变量必须改代码并重新编译，违背 v2.0 核心目标 |
| C | 结构体基址 + `OFFSETOF` 偏移 | 语义更清晰 | `OFFSETOF` 在 TwinCAT ST 支持有限，实现复杂度高 |

### 决策 ✅

**选择方案 A：PVOID 指针 + MEMCPY。**

### 决策依据

1. TwinCAT 变量地址在同一次 Runtime 运行期间**完全稳定**，注册表在上电初始化阶段（`bInit` 工作周期执行一次）后全程有效。
2. TwinCAT ADS 底层本身就是指针 + MEMCPY 模型，是经过工业验证的成熟做法。
3. 方案 C 的安全优势在实际场景中几乎不体现，反而增加注册时的心智负担。

### 实现要点

```pascal
// 上电初始化阶段（bInit 工作周期）
ParamTable[1].ParamID   := 1;
ParamTable[1].pVar      := ADR(GVL.AxisVelocity);
ParamTable[1].DataType  := DT_REAL;
ParamTable[1].ByteSize  := 4;
ParamTable[1].Access    := ACCESS_RW;
ParamTable[1].GroupID   := 1;
ParamTable[1].CycleClass:= CYCLE_2MS;
ParamTable[1].Name      := 'AxisVelocity';

// READ 执行时
MEMCPY(pDest, ParamTable[i].pVar, ParamTable[i].ByteSize);

// WRITE 执行时
MEMCPY(ParamTable[i].pVar, pSrc, ParamTable[i].ByteSize);
```

---

## 议题 2：GroupID 语义边界 ✅ 已冻结

### 问题

GroupID 的语义边界涉及三个子问题：一个变量能否属于多个 Group、同组读写是否原子、GroupID=0 的语义。

### 子问题 2-1：一个变量能否属于多个 Group？

| 方案 | 字段设计 | 说明 |
|------|----------|------|
| 单 Group | `GroupID: BYTE`，单值 | 简单，PARAM_ENTRY 固定 9B + Name |
| 多 Group | `GroupMask: BYTE`，位掩码，最多 8 组 | 灵活，但同一变量属于多组的场景极少见 |

**决策 ✅：单 Group。** `GroupID: BYTE` 单值，每个变量归属唯一一个 Group。真正需要同一变量出现在多个读取场景时，可通过 `READ_BY_ID` 单独补充读取。

### 子问题 2-2：同一 Group 的读写是否保证原子性？

| 方案 | 说明 | 代价 |
|------|------|------|
| 原子读 | 主站在同一 PLC 扫描周期内一次性 MEMCPY 整组到发送缓冲区，再组帧 | 需安排好 ST 执行顺序（先拷贝快照再组帧）|
| 非原子读 | 逐条读，可能跨越多个 PLC 周期 | 同组变量时间戳不一致，示波器出现相位偏移 |

**决策 ✅：原子读。** 主站在同一任务周期内完成整组变量的快照拷贝，保证同组数据时间一致性。这对示波器和数据分析场景至关重要，实现代价低。

### 子问题 2-3：GroupID=0 的语义

**决策 ✅：GroupID=0 表示"不分组"**，该变量只能通过 `READ_BY_ID` / `WRITE_BY_ID` 单独访问，不参与任何 `READ_GROUP` / `WRITE_GROUP` 操作。适用于配置参数、慢变量等不需要批量高频读取的场景，语义合理。

### 冻结后的 GroupID 规则总结

```
GroupID = 0x00       → 不分组，仅支持单独寻址（READ_BY_ID / WRITE_BY_ID）
GroupID = 0x01~0xFE  → 有效分组，支持批量操作（READ_GROUP / WRITE_GROUP）
GroupID = 0xFF       → 保留
同一变量只能归属一个 GroupID（单 Group）
同组变量读取保证原子性（同一 PLC 扫描周期内快照）
```

---

## 议题 3：写操作确认机制 ✅ 已冻结

### 问题

`WRITE_BY_ID` 执行后，上位机是否需要确认？若需要，确认策略如何定义？同时，如何为写入错误提供可诊断性？

### 候选方案

| 方案 | 说明 | 优点 | 缺点 |
|------|------|------|------|
| A | 全部写操作都要 ACK | 确认率 100% | 高频写（2ms 轴控）带宽翻倍，不可接受 |
| B | Flags.ACK_REQ 手动控制 | 上位机完全自主 | 需要用户对每个写变量人工判断，配置负担重 |
| C | 按 CycleClass 自动推断默认策略 + params.json 局部覆盖 | 合理缺省，工程师可微调 | 需维护一个本地 JSON 配置文件 |

### 决策 ✅

**选择方案 C：按 CycleClass 自动推断默认 ACK 策略，再由本地 `params.json` 覆盖。**

### 决策依据与默认推断规则

| CycleClass | 默认 ACK 策略 | 理由 |
|------------|--------------|------|
| `CYCLE_2MS` (0x00) | 无 ACK（静默） | 高频轴控写，带宽敏感 |
| `CYCLE_10MS` (0x01) | 无 ACK（静默） | 中频控制，同上 |
| `CYCLE_100MS` (0x02) | 有 ACK | 中低频，可接受确认开销 |
| `CYCLE_1S` (0x03) | 有 ACK | 低频配置写，必须确认 |
| `CYCLE_MANUAL` (0x04) | 有 ACK | 手动触发写，必须确认 |

`params.json` 中可对任意 `ParamID` 单独设置 `"ack": true/false`，覆盖上述默认值。

### 写入错误诊断变量（保留 ParamID）

为保证写入错误可诊断，保留以下三个诊断 ParamID（只读，不参与分组）：

| ParamID | 名称 | 说明 |
|---------|------|------|
| `0xFFF0` | WriteErrorCount | 累计写入错误次数，上电清零 |
| `0xFFF1` | LastErrorParamID | 最近一次写入错误的 ParamID |
| `0xFFF2` | LastErrorCode | 最近一次写入错误的错误码 |

这三个变量：Access=RO，GroupID=0（不分组），CycleClass=CYCLE_MANUAL，可通过 `READ_BY_ID` 单独轮询。

---

## 议题 4：参数表动态性 ✅ 已冻结

### 问题

参数表是否需要支持运行时自动检测与自动重拉？若不自动重拉，上位机至少应具备"参数表已不匹配"的检出能力，并在检出后提示用户手动重新拉取。

### 候选方案

| 方案 | 说明 | 优点 | 缺点 |
|------|------|------|------|
| A | 上位机启动时或用户手动时拉取一次，之后不再检测 | 实现最简单 | 参数表变化后上位机无感知，可能继续按旧表解释参数 |
| B | 运行时自动轮询版本号 / 哈希，不一致时自动重拉 | 一致性最好 | 机制更复杂，且自动刷新会打断用户当前工作流 |
| C | 启动/手动拉取一次 + 运行时仅做失配检出，不自动重拉 | 工程上最务实；既保留轻量实现，又能避免"旧表误用" | 失配后仍需用户手动处理 |

### 决策 ✅

**选择方案 C：参数表只在上位机打开时自动拉取一次，或由用户手动拉取一次；运行时不做自动重拉，但必须具备参数表失配检出能力。**

当上位机检出参数表失配后，应立即报错，并要求用户手动重新拉取参数表；不采用后台静默刷新。

### 检出机制：DictHash

主站维护一个 `DictHash`，用于表示"影响参数读写与参数辨认"的参数表结构哈希。
上位机每次成功拉取参数表后缓存该 `DictHash`；后续通过心跳回包中的当前 `DictHash` 做比对。
若哈希不一致，则说明上位机当前缓存的参数表已失效，必须提示用户手动重新拉取。

### DictHash 参与字段

仅将那些会影响参数读写正确性、分组语义和参数辨认的字段纳入哈希：

- `ParamID`
- `DataType`
- `ByteSize`
- `Access`
- `GroupID`
- `NameLen`
- `Name`

### DictHash 不参与字段

以下字段不纳入哈希，因为它们不应导致"参数读写层面"的失配告警：

- `CycleClass`
- `Unit`
- `DescLen`
- `Desc`

其中：
- `CycleClass` 可由上位机用于轮询/ACK 默认策略推断，不影响主站端参数寻址与读写合法性。
- `Unit` / `Description` 属于显示与说明性元数据，允许后续优化而不触发参数表失配。
- `GroupID` **必须参与哈希**，因为它直接影响 `READ_GROUP` / `WRITE_GROUP` 的批量对象边界，属于主站定义的读写拓扑，不允许上位机随意改动。
- `Access` **必须参与哈希**，因为读写权限由主站端定义，是参数语义的一部分。

### DictHash 计算规则

`DictHash = CRC16/MODBUS(...)`

计算输入为：对所有 Entry 按 `ParamID` 升序排列后，依次拼接如下字段：

```text
ParamID  (2B)
DataType (1B)
ByteSize (1B)
Access   (1B)
GroupID  (1B)
NameLen  (1B)
Name     (NameLen B)
```

### 上位机行为规范

1. 上位机启动时自动执行一次 `GET_PARAM_DICT`，或由用户手动触发参数表拉取。
2. 拉取成功后缓存本次 `DictHash`。
3. 运行期间不自动重新拉取参数表。
4. 主站在 `PING_ACK / PONG` 中返回当前 `DictHash`。
5. 上位机收到心跳回包后比对哈希：
   - 一致：继续正常运行；
   - 不一致：立即报错，提示"参数表已变化，请手动重新拉取参数表"。
6. 重新拉取成功后，更新本地缓存的 `DictHash`。

### 接口落点（ADR 级结论）

- `GET_PARAM_DICT_ACK` 应携带 `DictHash`，供上位机在首次/手动拉取时缓存。
- `PING_ACK / PONG` 应在现有 `4B` 毫秒时间字段后追加 `2B DictHash`。
- v2.0 不考虑旧兼容包袱，按完整替换思路设计即可。
- `FrameFormat_v2.0.md` 细节修改延后，待全部议题结束后统一更新。

---

## 议题 4.5：参数表自描述增强 ✅ 已冻结

### 问题

现有参数表 Entry 仅含 ParamID、DataType、ByteSize、Access、GroupID、CycleClass、Name，缺少单位与描述信息，工程工具（上位机 GUI、自动文档生成）无法仅凭参数表自解释变量语义。同时枚举型变量缺乏枚举值-文本映射查询能力。

### 决策 ✅

**扩展参数表 Entry，新增 Unit（8B 定长）和 Description（变长 ≤ 64B）；新增指令 GET_ENUM_MAP（0x13/0x93）；新增 DataType STRING64/STRING128。**

### Entry 新结构

```
ParamID    : UINT  2B
DataType   : BYTE  1B
ByteSize   : BYTE  1B
Access     : BYTE  1B
GroupID    : BYTE  1B
CycleClass : BYTE  1B
Unit       : BYTE[8]  8B  // 定长，UTF-8，不足补0，例如 "rpm", "mm/s"
NameLen    : BYTE  1B
Name       : NameLen B   // UTF-8，最长 32B
DescLen    : BYTE  1B
Desc       : DescLen B   // UTF-8，最长 64B，0=无描述
```

固定部分从原 9B 增至 **17B**（新增 Unit 8B）；变长部分 Name + Desc 上限 96B。
每页最大条目数从 20 调整为 **12**，保证最坏情况（17+1+32+1+64=115B × 12=1380B）不超 MTU。

### GET_ENUM_MAP 指令

| CMD (REQ) | CMD (ACK) | 说明 |
|-----------|-----------|------|
| `0x13`    | `0x93`    | 查询指定 ParamID 的枚举值-文本映射表 |

REQ Payload：
```
[ParamID : UINT 2B]
```

ACK Payload：
```
[ParamID  : UINT  2B]
[MapCount : BYTE  1B]   // 枚举项数
[Entry × MapCount] :
  [EnumValue : INT   2B]   // 枚举数值（有符号）
  [TextLen   : BYTE  1B]
  [Text      : TextLen B]  // UTF-8，最长 32B
```

### 新增 DataType

| 值     | 类型      | ByteSize | TwinCAT ST 对应  |
|--------|-----------|----------|------------------|
| `0x10` | STRING64  | 64       | STRING(63)       |
| `0x11` | STRING128 | 128      | STRING(127)      |

---

## 议题 5：HELLO 握手扩展 ✅ 已冻结

### 问题

v1.1 的 `HELLO / HELLO_ACK`（CMD=0x05/0x85）仅作"我在线"确认，不携带任何设备元信息。v2.0 上位机连接建立后需要立即获取参数表哈希、设备标识、固件版本等信息，以完成初始化校验和 UI 渲染，握手是天然的获取时机。

### 候选携带字段分析

| 字段 | 必要性 | 理由 |
|------|--------|------|
| `DictHash` | ✅ 必须 | 握手即缓存，无需等参数表拉完再建立基准 |
| `ParamCount` | ✅ 必须 | 上位机可提前计算分页数量，优化进度显示 |
| `FirmwareVer` | ✅ 必须 | 支持新上位机连接旧设备的多版本并存场景，上位机据此判断功能兼容性 |
| `DeviceName` | ✅ 加入 | DevID 仅 1B 存在碰撞风险，DeviceName 提供可读的唯一设备标识，来源复用保留 ParamID `0xFFEF`，无额外维护负担 |
| `TraceCapacity` | ❌ 不加入 | 只在示波器功能启用时才需要，由 `TRACE_CONFIG_ACK` 的 `BufferCapacity` 字段按需返回即可 |
| `ProtoVersion` | ❌ 冗余 | Header 的 `Version` 字段已携带，无需重复 |

### 决策 ✅

**HELLO_ACK（CMD=0x85）Payload 扩展为如下结构：**

```
[DictHash   : UINT  2B]      // 当前参数表结构哈希，上位机握手即缓存
[ParamCount : UINT  2B]      // 参数表总条目数，预知分页数量
[FW_Major   : BYTE  1B]      // 固件主版本号
[FW_Minor   : BYTE  1B]      // 固件次版本号
[FW_Patch   : BYTE  1B]      // 固件补丁版本号
[DevNameLen : BYTE  1B]      // DeviceName 字节数（最长 32B）
[DeviceName : DevNameLen B]  // UTF-8，无 null 结尾，来源：保留 ParamID 0xFFEF
```

固定部分 **8B** + 变长 DeviceName（1~32B）。

### FirmwareVersion 语义边界

采用语义化版本控制（SemVer）三段式，各段触发条件如下：

| 版本段 | 触发条件 | CLS-II 典型示例 |
|--------|----------|-----------------|
| **Major**（主版本） | 破坏性变更，帧结构/指令码/握手方式根本重构，不向后兼容 | v1.x → v2.0，端口变更，Header 从 11B 扩至 12B |
| **Minor**（次版本） | 向后兼容的新增功能，新指令、新保留 ParamID、握手字段扩展 | 新增 GET_ENUM_MAP、新增 DictHash 机制、HELLO_ACK 字段扩展 |
| **Patch**（补丁） | 向后兼容的缺陷修复，不新增任何接口 | 修复 CRC 计算边界 bug，修正 ErrCode 返回逻辑 |

**上位机版本兼容判断规则：**

```
FW_Major 不一致  →  拒绝连接，提示"协议版本不兼容，请升级上位机或固件"
FW_Major 一致
  FW_Minor 上位机 > 主站  →  提示"固件版本较旧，部分功能可能不可用"（降级运行）
  FW_Minor 上位机 ≤ 主站  →  正常连接
FW_Patch         →  上位机静默忽略差异，始终兼容
```

### 新增保留 ParamID

| ParamID  | 名称       | DataType | Access | 说明 |
|----------|------------|----------|--------|------|
| `0xFFEF` | DeviceName | STRING32 | RO     | 设备名称，握手时由主站从此变量读取后填入 HELLO_ACK |

与已有的 `0xFFF0~0xFFF2` 诊断变量共同构成保留区段 `0xFFEF~0xFFFF`。

### 接口落点（ADR 级结论）

- `FrameFormat_v2.0.md` 中 HELLO_ACK Payload 格式待全部议题结束后统一更新。
- v2.0 按完整替换思路设计，不考虑 v1.1 HELLO_ACK 兼容。

---

## 议题 6：串口场景封装 ✅ 已冻结

### 问题

v2.0 当前以 UDP 为主场景设计，但未来可能需要兼容 UART/串口链路。  
串口是字节流传输，不具备 UDP 的天然报文边界，因此需要明确：串口场景是直接复用整套应用层帧，还是额外增加一层 UART 外壳来完成定界与重同步。

### 场景边界

本议题针对的串口场景边界为：

- 点对点通信（非 RS-485 多从总线）
- 以未来兼容性为目标，不作为当前主场景
- 设计基准波特率按 `115200 bps` 评估

### 候选方案

| 方案 | 说明 | 优点 | 缺点 |
|------|------|------|------|
| A | UART 直接传输原始 v2.0 帧（SOF + Header + Payload + CRC + EOF） | 应用层完全复用，零额外封装 | 串口是字节流，帧边界恢复依赖 SOF/EOF + PayloadLen + CRC，重同步复杂；Payload 内可能自然出现魔数 |
| B | 在原始 v2.0 帧外增加一层 UART 外壳 | 定界清晰，接收状态机简单，重同步容易 | 增加少量封装开销 |
| B1 | UART 外壳采用长度前缀 `[FrameLen: UINT 2B] + [v2.0 Frame]` | 实现最简单，仅增加 2B | 需要在串口收发层维护长度状态机 |
| B2 | UART 外壳采用 SLIP/转义编码 | 边界稳定 | 编解码更复杂，且存在字节膨胀 |

### 决策 ✅

**选择方案 B1：串口场景在原始 v2.0 帧外增加一个 2B 长度前缀外壳。**

即：

```text
[FrameLen   : UINT  2B]   // 内层 v2.0 完整帧字节数
[v2.0 Frame : FrameLen B] // 原封不动的标准 v2.0 帧
```

其中内层 `v2.0 Frame` 保持不变，仍包含：

```text
SOF(2B) + Header(12B) + Payload + CRC16(2B) + EOF(1B)
```

### 决策依据

1. UART 是字节流而非报文流，若直接依赖 SOF/EOF 做边界恢复，接收端状态机会更复杂。
2. 长度前缀仅增加 2B，代价极低，却能显著简化串口接收逻辑。
3. CRC 仍由内层 v2.0 帧负责，UART 外壳只负责定界，职责边界清晰。
4. 当前串口场景是未来兼容方案，不值得引入 SLIP 这类更复杂的转义层。

### 接收端状态机

接收端采用两状态模型：

```text
STATE_WAIT_LEN   -> 读取 2B FrameLen
STATE_RECV_BODY  -> 按 FrameLen 读取完整内层 v2.0 帧
                    -> CRC 校验通过：交给 v2.0 应用层处理
                    -> CRC 校验失败：丢弃并回到 STATE_WAIT_LEN
```

### 串口场景下的帧长约束

按 `115200 bps` 估算，UART 采用 8N1 时约等效为：

```text
115200 bps / 10 bits-per-byte ≈ 11520 Byte/s
```

若直接发送 UDP 场景允许的最大 1400B 帧，则单帧传输时间约为：

```text
1400 / 11520 ≈ 121 ms
```

这对串口场景过长，因此需要为 UART 场景单独收紧帧长上限。

### UART 专用约束

| 项目 | UDP 场景 | UART 场景 |
|------|----------|-----------|
| 最大总帧长 | 1400B | 256B |
| UART 外壳 | 无 | 2B FrameLen |
| 内层协议 | 原始 v2.0 | 原始 v2.0，完全复用 |
| 应用层 CMD | UDP 定义 | 完全相同，不新增 CMD |

在 UART 场景下，建议将**总帧长上限**约束为 `256B`（含内层 v2.0 帧，不含或含外壳由实现文档统一约定；FrameFormat 阶段再统一写死）。设计原则是不允许出现接近 UDP 上限的超大串口帧。

### 参数表分页策略

由于参数表 Entry 已在议题 4.5 扩展，若仍沿用 UDP 场景下的大页分页策略，则在 `115200 bps` 下单页传输时延会偏大。  
因此，**串口场景应使用更小的参数表分页粒度**，推荐按 `4 条/页` 控制，以缩短单页交互时延并提升交互体验。

### 不采用的复杂机制

本议题明确不引入以下机制：

- 不引入 RS-485 多从地址仲裁；
- 不引入串口专用新 CMD；
- 不引入 SLIP/转义编码；
- 不引入与 UDP 不同的应用层语义。

### 接口落点（ADR 级结论）

- `FrameFormat_v2.0.md` 在全部议题结束后统一补写 UART 外壳说明。
- README 中应明确：**UDP 是主场景，UART 仅为未来兼容封装，应用层协议保持一致。**
- UART 场景与 UDP 场景共享同一套 CMD、参数表、错误码和数据语义，仅链路层封装不同。
