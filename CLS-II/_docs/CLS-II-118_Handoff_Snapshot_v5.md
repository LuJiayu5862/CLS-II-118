# CLS-II-118 开发交接快照 v5

> **生成时间**：2026-05-13
> **编写者**：AI Assistant（基于代码库完整读取）
> **接续自**：`CLS-II-118_Handoff_Snapshot_v4.md`
> **里程碑**：✅ ParamPoll 轮询/写队列框架完成；Watch 写入语义问题已识别待解决

---

## 一、本轮完成内容（v4 → v5）

### 1.1 ParamData.cs — 补全 TryDeserialize + Snap

在 v4 基础上新增：

- `ParamData.TryDeserialize(TcFrame)` 按 SubID 分发解析，覆盖全部 14 个 SubID + ALL 帧
- `ParamData.Snap` 嵌套静态类，包含差分写变量的快照：
  - 差分写类：`CLS_Model / CLS_Param / CLS_5K / CLS_Consts / Test_MDL / CLS_Enum / Param_XT / Param_YT`
  - 周期写类：`CtrlIn`
  - 只读类（DeviceInfo / UdpDataCfg / UdpParamCfg / CtrlOut / ALL）：**不声明快照，轮询读回直接覆盖源变量**

**⚠️ 重要 bug 修复（本轮确认）**：

ALL 帧（SubID=0x00，992B）中，PLC 端 `ST_TcLCS_P` 存在显式 4B 填充：

```
CLSEnum   : ST_CLSEnum;           // 624..651  (28B)
_pad0     : ARRAY[0..3] OF BYTE;  // 652..655  ( 4B) ← 必须保留
XT        : ST_XT;                // 656..823  (168B)
YT        : ST_YT;                // 824..991  (168B)
```

因此 `TryDeserialize` 的 `case ALL` 中，CLSEnum 块的 `off` 必须 `+= 32`（28B + 4B pad），而不是 `+= 28`。
否则 XT/YT 偏移差 4B，导致数组值移位乱码。

---

### 1.2 MainForm.ParamPoll.cs — 轮询/写队列框架（完全新建）

| 组件 | 说明 | 状态 |
|---|---|---|
| `PollMode` enum | `ReadOnly / PeriodWrite / DiffWrite` | ✅ |
| `TimerType` enum | `HiRes（mmTimer1）/ Soft（Task.Delay）` | ✅ |
| `PollEntry` | 轮询表条目 | ✅ |
| `WriteJob` | 写队列条目 | ✅ |
| `_pollTable` | 统一注册表，新增 SubID 只加一行 | ✅ |
| `OnHiResTick()` | mmTimer1 每 10ms 触发，驱动 HiRes 条目 | ✅ |
| `HasChanged()` | 字节级对比 `ParamData.XXX` vs `ParamData.Snap.XXX` | ✅ |
| `StructBytesEqual()` | 通用结构体字节对比 | ✅ |
| `EnqueueWrite()` | 序列化**源变量**后入写队列 | ✅ |
| `WriteQueueLoopAsync()` | 后台 Task 串行消费，写成功才 UpdateSnap | ✅ |
| `UpdateSnap()` | 写成功后 payload 反序列化回 Snap | ✅ |
| `PollReadOnceAsync()` | fire-and-forget 读操作 | ✅ |
| `SoftPollLoopAsync()` | Task.Delay 软定时，处理低频只读参数 | ✅ |
| `StartPollAndWrite()` | 启动轮询+写队列 Task | ✅ |
| `StopPollAndWrite()` | 停止并等待 Task | ✅ |

**当前 _pollTable 注册内容**：

| SubID | 周期 | 模式 | Timer | 优先级 |
|---|---|---|---|---|
| TcLCS_CtrlIn | 10ms | PeriodWrite | HiRes | 0 |
| CLSModel～YT（8项） | 10ms | DiffWrite | HiRes | 1 |
| TcLCS_CtrlOut | 10ms | ReadOnly | HiRes | 99 |
| ALL | 1000ms | ReadOnly | Soft | 99 |
| DeviceInfo | 2000ms | ReadOnly | Soft | 99 |
| UdpDataCfg | 2000ms | ReadOnly | Soft | 99 |
| UdpParamCfg | 2000ms | ReadOnly | Soft | 99 |

---

### 1.3 _docs/ParamPoll_Registration_Guide.md — 新增注册指南

完整说明如何新增差分写变量、只读变量的 5 步（差分写）/ 3 步（只读）流程，包含 ALL 帧偏移表和修改清单速查。

---

## 二、当前未闭环问题

### 2.1 ⚠️ Watch 写入语义未定（核心问题）

`Watch.Method.cs` 的 `TryWriteParamValue()` 当前改的是**源变量** `ParamData.XXX`。

用户提出"改成写快照变量"，但这需要先统一语义：

| 变量 | 当前语义 |
|---|---|
| `ParamData.XXX` | 实时值（PLC 读回 / UI 修改） |
| `ParamData.Snap.XXX` | **上次已成功发送至 PLC 的值** |

`HasChanged()` 比较的是 `ParamData.XXX` vs `ParamData.Snap.XXX`。
`EnqueueWrite()` 发送的是 `ParamData.XXX` 序列化结果。
写成功后 `UpdateSnap()` 将 payload 回写 `Snap`。

**结论**：`Snap` 不是"待写编辑缓冲区"，不能直接让 Watch 写入 Snap，否则会破坏 `HasChanged()` 的比较基准，导致差分写失效。

**正确架构选项（待下一 thread 定案）**：
- 方案 A：Watch 继续改**源变量** `ParamData.XXX`，差分写自动检测触发发包（当前实际已是此逻辑，只是 Watch.Method.cs 未接入）
- 方案 B：新增独立 `Pending.XXX` 缓冲区，Watch 编辑 Pending，写成功后同步到 ParamData 和 Snap
- 方案 C：废弃 Snap 架构，改为 UI 编辑时显式调用写入（不推荐）

---

### 2.2 ⚠️ Watch 卡顿问题（性能热点）

`Watch.Method.cs` 存在以下性能可疑点：

1. **`updateParamDataOnce()`**：每次刷新对每个监视变量做字符串切割、`GetParamStruct()`、反射 `GetField()`、值类型装箱
2. **`GetParamStruct()`**：返回 struct（值类型），必然装箱 `object`
3. **`updateScopeDataOnce()`**：`ScopeVarieties × records` 双重循环，O(n²)
4. **反射未缓存**：每次调用都重复 `GetField(fieldName)`，无缓存

**下一 thread 优先排查这四处。**

---

### 2.3 ALL 帧 off += 28 bug（本轮已修复，需确认代码）

已确认应改为 `off += 32`，但需确认实际代码已提交该修改。

---

## 三、下一个 Thread 任务清单（优先级排序）

### P0 — 先定架构，再改代码

1. 确认 Watch 编辑的语义目标：源变量 / Pending / Snap？
2. 基于定案再修改 `TryWriteParamValue()`

### P1 — 性能排查

3. 审查 `updateParamDataOnce()` 刷新路径
4. 缓存 `FieldInfo`，消除每次反射查找
5. 审查 `updateScopeDataOnce()` 双循环，考虑用字典索引

### P2 — 补充验证

6. 确认 ALL 帧 `off += 32` 已正确写入代码
7. 延迟测试（v4 遗留，`ReadAsync`/`WriteAsync` RTT 测量）

---

## 四、协议约束速查

| 约束 | 值 |
|---|---|
| SOF | `0xAA 0x55` |
| Version | `0x02`（v1.1） |
| EOF | `0x55` |
| Header | 11B |
| Trailer | CRC16(2B LE) + EOF(1B) = 3B |
| 最小帧 | 14B |
| 最大帧 | 1400B |
| CRC | CRC-16/MODBUS，Poly=0xA001，Init=0xFFFF |
| 只读 SubID | ALL(0x00)、TcLCS_CtrlOut(0x14) |
| 超时策略 | 300ms × 3次，连续3次失败重发HELLO |

---

## 五、SubID 尺寸速查表

| SubID | 名称 | 大小(B) | R/W |
|---|---|---|---|
| 0x00 | ALL | 992 | R |
| 0x01 | CLSModel | 176 | R/W |
| 0x02 | CLSParam | 144 | R/W |
| 0x03 | CLS5K | 112 | R/W |
| 0x04 | CLSConsts | 104 | R/W |
| 0x05 | TestMDL | 88 | R/W |
| 0x06 | CLSEnum | 28 | R/W |
| 0x07 | XT | 168 | R/W |
| 0x08 | YT | 168 | R/W |
| 0x10 | DeviceInfo | 16 | R/W |
| 0x11 | UdpDataCfg | 48 | R/W |
| 0x12 | UdpParamCfg | 48 | R/W |
| 0x13 | TcLCS_CtrlIn | 68 | R/W |
| 0x14 | TcLCS_CtrlOut | 52 | R |

---

## 六、ALL 帧偏移表（含 PLC 端 _pad0，关键！）

| 字段 | 起始 | 大小 | 备注 |
|---|---|---|---|
| CLSModel | 0 | 176 | |
| CLSParam | 176 | 144 | |
| CLS5K | 320 | 112 | |
| CLSConsts | 432 | 104 | |
| TestMDL | 536 | 88 | |
| CLSEnum | 624 | 28 | |
| _pad0 | 652 | 4 | PLC 显式填充，**off += 32（不是28）** |
| XT | 656 | 168 | |
| YT | 824 | 168 | |
| **合计** | | **992** | |

---

## 七、下一个 Thread 开场白

```
请先做上下文连续性检查，不要沿用上个线程关于 Watch 写 Snap 的结论。

项目：LuJiayu5862/CLS-II-118

请先阅读：
1. CLS-II/_docs/CLS-II-118_Handoff_Snapshot_v5.md（本文件）
2. CLS-II/src_watch_scope/Watch.cs
3. CLS-II/src_watch_scope/Watch.Method.cs
4. CLS-II/src_communication/MainForm.ParamPoll.cs
5. CLS-II/src_IOData/ParamData.cs

本次目标只有两件事：
1. Watch 现在很卡，找出根因，给出最小改动方案
2. 确定 Watch 编辑时写入的目标变量（源变量 / Pending / Snap），
   必须先核对 HasChanged() / EnqueueWrite() / UpdateSnap() 的现有语义再下结论

注意：
- HasChanged() 比较 ParamData.XXX vs ParamData.Snap.XXX
- EnqueueWrite() 发的是 ParamData.XXX
- UpdateSnap() 在写成功后才更新 Snap
- 所以未经论证不要把 Watch 改成写 Snap

请依次给出：
A. 性能瓶颈定位（从代码出发，不要猜）
B. 写入语义定案（方案A/B/C，见 snapshot v5 §2.1）
C. 最小改动方案
D. 如需重构，给出重构方案
```

---

*本快照由 AI Assistant 于 2026-05-13 基于本轮完整对话生成。*
