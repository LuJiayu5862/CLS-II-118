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

## 议题 4：参数表动态性 🔲 讨论中

（待写入）

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

## 议题 5：HELLO 握手扩展 🔲 讨论中

（待写入）

---

## 议题 6：串口场景封装 🔲 讨论中

（待写入）
