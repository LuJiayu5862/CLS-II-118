# Protocol v2.0 设计决策记录（ADR）

> 每个议题讨论完成后即时典谡，确保切换 Thread 后全量恢复设计决策上下文。
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

## 议题 2：GroupID 语义边界 🔲 讨论中

（待写入）

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
