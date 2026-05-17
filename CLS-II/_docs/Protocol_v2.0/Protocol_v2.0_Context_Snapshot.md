# CLS-II Protocol v2.0 — 设计上下文快照

> **生成时间**：2026-05-17
> **仓库**：LuJiayu5862/CLS-II-118
> **分支**：dev/protocol-v2.0
> **文档路径**：`CLS-II/_docs/Protocol_v2.0/`
> **用途**：跨 Thread 上下文恢复，直接粘贴到新 Thread 开头即可继续工作。

---

## 当前文档状态

| 文件 | 状态 |
|------|------|
| `ADR_v2.0.md` | ✅ 已全量更新，议题 1~8 全部冻结 |
| `FrameFormat_v2.0.md` | ✅ 已全量更新（CMD 重排 + §3.5/§3.6 新增 + §4.14~§4.17 新增 + §3.4 排版修复）|
| `README.md` | ✅ 已更新（含 UART 场景说明、保留 ParamID 表、文档列表）|
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

---

### 议题 2：GroupID 语义边界 ✅
- `GroupID = 0x00` → 不分组，仅支持 READ_BY_ID / WRITE_BY_ID
- `GroupID = 0x01~0xFE` → 有效分组，支持 READ_GROUP / WRITE_GROUP
- `GroupID = 0xFF` → 保留
- 同组变量读取保证原子性（同一 PLC 扫描周期内快照 MEMCPY）

---

### 议题 3：写操作确认机制 ✅
按 CycleClass 自动推断默认 ACK 策略 + params.json 局部覆盖。

| CycleClass | 值 | 默认 ACK |
|------------|-----|---------|
| CYCLE_2MS | 0x00 | 无 ACK |
| CYCLE_10MS | 0x01 | 无 ACK |
| CYCLE_100MS | 0x02 | 有 ACK |
| CYCLE_1S | 0x03 | 有 ACK |
| CYCLE_MANUAL | 0x04 | 有 ACK |

多 ParamID 混合 CycleClass：取最保守值。写入错误诊断保留 ParamID：0xFFF0~0xFFF2。

---

### 议题 4：参数表动态性 + DictHash ✅
DictHash = CRC16/MODBUS。启动拉取一次，运行时不自动重拉。接口落点：GET_PARAM_DICT ACK / PONG / HELLO_ACK。

---

### 议题 4.5：参数表自描述增强 ✅
Entry 新增 Unit（8B）+ Desc（变长≤64B）。新增 GET_ENUM_MAP（CMD 0x12/0x92）。新增 STRING64(0x10) / STRING128(0x11)。固定部分 17B，每页最多 12 条。

---

### 议题 5：HELLO 握手扩展 ✅
HELLO_ACK 固定 8B + 变长 DeviceName（1~32B）。FW SemVer 三段兼容规则。新增保留 ParamID 0xFFEF（DeviceName）。保留区段：0xFFEF~0xFFFF。

---

### 议题 6：串口场景封装 ✅
UART 场景加 2B FrameLen 外壳（方案 B1）。最大帧长 256B，最大 Payload 239B，推荐 4 条/页。不引入 RS-485 仲裁/SLIP/专用 CMD。

---

### 议题 7：GROUP_ENTRY 注册机制 ✅
GROUP_ENTRY 结构：GroupID + CycleClass + NameLen + Name + DescLen + Desc。新增 GET_GROUP_DICT（CMD 0x13/0x93）。WRITE_GROUP 全量写语义，不支持子集写。GROUP_ENTRY.CycleClass 参与 DictHash（PARAM_ENTRY.CycleClass 不参与）。

---

### 议题 8：SUBSCRIBE / NOTIFY 推送机制 ✅
**决策**：Periodic Only，无 Deadband，无 ACK，断线清除，嵌入式兼容编译宏。

- `Mode=0x00` 为当前唯一合法值；`Mode` 字段预留用于未来扩展
- Deadband 明确不在 v2.0 范围
- NOTIFY（CMD=0x40）：主站主动发出，Flags.ACK_REQ 恒为 0
- 断线（收到 HELLO_REQ）自动清除全部订阅槽位
- 槽位上限：实现端编译宏决定；推荐工控机 32×20，嵌入式 8×8（~520B）
- 新增 ErrCode：0x0B（槽位满）/ 0x0C（SubID 不存在）/ 0x0D（变量数超限）

**CMD 分区**：

| 区段 | CMD | 名称 |
|------|-----|------|
| 主站主动推送 | `0x40` | NOTIFY |
| 订阅管理 | `0x50/0xD0` | PARAM_SUBSCRIBE |
| 订阅管理 | `0x51/0xD1` | PARAM_UNSUBSCRIBE |
| 订阅管理 | `0x52/0xD2` | SUBSCRIBE_CLEAR |

---

## CMD 总表速查（v2.0 最终）

| 区段 | CMD REQ | CMD ACK | 名称 |
|------|---------|---------|------|
| Legacy | 0x01~0x05 | 0x81~0x85 | READ_REQ / WRITE_REQ / PING / SAVE / HELLO |
| 参数表 | 0x10 | 0x90 | GET_PARAM_DICT |
| 参数表 | 0x11 | 0x91 | GET_PARAM_BY_ID |
| 参数表 | 0x12 | 0x92 | GET_ENUM_MAP |
| 参数表 | 0x13 | 0x93 | GET_GROUP_DICT |
| 读写 | 0x20 | 0xA0 | READ_BY_ID |
| 读写 | 0x21 | 0xA1 | WRITE_BY_ID |
| 读写 | 0x22 | 0xA2 | READ_GROUP |
| 读写 | 0x23 | 0xA3 | WRITE_GROUP |
| Trace | 0x30~0x34 | 0xB0~0xB4 | TRACE_CONFIG/START/STOP/STATUS/UPLOAD |
| 主动推送 | — | 0x40 | NOTIFY（主站主动） |
| 订阅管理 | 0x50 | 0xD0 | PARAM_SUBSCRIBE |
| 订阅管理 | 0x51 | 0xD1 | PARAM_UNSUBSCRIBE |
| 订阅管理 | 0x52 | 0xD2 | SUBSCRIBE_CLEAR |
| 错误 | — | 0xEE | ERR |

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
| `ParamEntry_DataStructure.md` | 编写 ST 端 PARAM_ENTRY + GROUP_ENTRY 结构体定义与注册宏 |
| `TraceBuffer_Design.md` | 设计 Trace Buffer 功能块（FB_TraceBuffer）ST 实现 |
| `CSharp_ParamDictionary.md` | C# 端 ParamDictionary 类设计，含 DictHash 校验逻辑 |
| `Migration_v1.1_to_v2.0.md` | v1.1 → v2.0 迁移指南，含端口/版本号/CMD 编号切换步骤 |
| FrameFormat §八 待确认事项 | 逐条处理 Flag 列表中的 8 个待确认项 |
