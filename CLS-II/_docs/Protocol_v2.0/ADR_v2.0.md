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

## 议题 3：写操作确认机制 🔲 讨论中

（待写入）

---

## 议题 4：参数表动态性 🔲 讨论中

（待写入）

---

## 议题 5：HELLO 握手扩展 🔲 讨论中

（待写入）

---

## 议题 6：串口场景封装 🔲 讨论中

（待写入）
