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
| `ADR_v2.0.md` | ✅ 已全量更新，议题 1~7 全部冻结（含 Review Pass 修正） |
| `FrameFormat_v2.0.md` | ✅ 已全量更新（含 Review Pass 全部 10 项修正） |
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

**机制**：`Flags.ACK_REQ` 由上位机发帧时动态置位，主站被动响应。适用于 WRITE_BY_ID；WRITE_GROUP 见议题 7。

**多 ParamID 混合 CycleClass**：取所有目标变量中 CycleClass 编号最大（最保守）者的默认 ACK 策略。

**写入错误诊断保留 ParamID（RO，GroupID=0，CycleClass=MANUAL）**：

| ParamID | 名称 | 说明 |
|---------|------|------|
| 0xFFF0 | WriteErrorCount | 累计写入错误次数，上电清零 |
| 0xFFF1 | LastErrorParamID | 最近一次写入错误的 ParamID |
| 0xFFF2 | LastErrorCode | 最近一次写入错误的错误码 |

---

### 议题 4：参数表动态性 + DictHash ✅
**决策**：启动/手动拉取一次，运行时不自动重拉，但必须具备失配检出。

**DictHash = CRC16/MODBUS**，完整计算规则见 FrameFormat §七（PARAM_ENTRY + GROUP_ENTRY 共用一个 Hash）。

**接口落点**：GET_PARAM_DICT ACK 首字段、PING_ACK/PONG 追加 2B、HELLO_ACK 首字段。

---

### 议题 4.5：参数表自描述增强 ✅
**决策**：Entry 新增 Unit（8B 定长）+ Desc（变长 ≤64B）；新增 GET_ENUM_MAP；新增 STRING64(0x10) / STRING128(0x11)。

**新 PARAM_ENTRY 结构**：固定部分 17B（原 9B + Unit 8B），变长 Name+Desc 上限 96B。
**UDP 场景每页最大条目数 = 12**。

---

### 议题 5：HELLO 握手扩展 ✅
**决策**：HELLO_ACK（CMD=0x85）Payload 固定 8B + 变长 DeviceName（1~32B）。

```
[DictHash 2B][ParamCount 2B][FW_Major 1B][FW_Minor 1B][FW_Patch 1B][DevNameLen 1B][DeviceName]
```

**FW SemVer 规则**：Major 不一致→拒绝连接；Minor 上位机>主站→降级+警告；Patch 差异→静默兼容。

**新增保留 ParamID**：

| ParamID | 名称 | DataType | Access | 说明 |
|---------|------|----------|--------|------|
| 0xFFEF | DeviceName | STRING32 | RO | 握手时主站读取并填入 HELLO_ACK |

保留区段：`0xFFEF ~ 0xFFFF`

---

### 议题 6：串口场景封装 ✅
**决策**：UART 场景在原始 v2.0 帧外增加 2B 长度前缀外壳（方案 B1）。

| 项目 | UDP | UART |
|------|-----|------|
| 最大总帧长 | 1400B | 256B |
| 最大 Payload | 1385B | 239B |
| 分页建议 | 12条/页 | 4条/页 |

不引入：RS-485 地址仲裁、SLIP 编码、串口专用 CMD。

---

### 议题 7：GROUP_ENTRY 注册机制 ✅
**决策**：主站注册 GROUP_ENTRY，上位机通过 GET_GROUP_DICT（0x14/0x94）拉取；WRITE_GROUP 的 ACK_REQ 由 GROUP_ENTRY.CycleClass 推断，可由 params.json GroupID 级覆盖。

**GROUP_ENTRY 结构**：
```
GroupID(1B) + CycleClass(1B) + NameLen(1B) + Name(≤32B) + DescLen(1B) + Desc(≤64B)
```

**WRITE_GROUP 全量写语义**：必须包含该组所有已注册变量，不支持子集写（子集写用 WRITE_BY_ID）。Count 或 ParamID 不匹配则返回 ERR（0x09/0x0A）。

**ACK_REQ 推断规则统一**：

| 写指令 | 推断来源 | 覆盖方式 |
|--------|---------|--------|
| WRITE_BY_ID | ParamID.CycleClass（混合取最保守值） | params.json ParamID 级 |
| WRITE_GROUP | GROUP_ENTRY.CycleClass | params.json GroupID 级 |

**GROUP_ENTRY 纳入 DictHash**（按 GroupID 升序追加在 PARAM_ENTRY 之后）：
```
GroupID(1B) + CycleClass(1B) + NameLen(1B) + Name(NameLen B)
```
参与 Hash：GroupID, CycleClass, NameLen, Name；不参与：DescLen, Desc。

> 注：GROUP_ENTRY.CycleClass **参与** Hash（与 PARAM_ENTRY.CycleClass 不参与 Hash 不同），因为它是 WRITE_GROUP ACK_REQ 推断的唯一来源，变更必须触发重拉。

**未注册 GroupID 缺省行为**：Name=`"Group_0xXX"`，Desc=空，CycleClass=组内最保守值（fallback）。

---

## Review Pass 修正记录（2026-05-17）

| # | 位置 | 修正内容 |
|---|------|--------|
| 1 | §4.10 | WRITE_GROUP 明确全量写语义，禁止子集写，不匹配返回 ERR 0x09/0x0A |
| 2 | §五 ERR | 新增 ErrCode 0x09（GroupID 无效）和 0x0A（变量集不匹配） |
| 3 | §4.12 | 补充 TRACE_STATUS 请求帧（Payload 为空）说明 |
| 4 | §2.2 | FRAG 注释扩展为「多包传输场景」，不再限定参数表 |
| 5 | §4.7 | 补充多 ParamID 混合 CycleClass 时取最保守值的推断规则 |
| 6 | §4.4c | GET_GROUP_DICT 缺省行为描述移出 ACK 格式块，改为主站处理规则说明 |
| 7 | §七 | 新增 DictHash 设计取舍说明（PARAM_ENTRY vs GROUP_ENTRY 的 CycleClass 差异化处理理由） |
| 8 | §4.2 | 补充 FRAG 单页行为（单页不设 FRAG，多页全程设 FRAG，末片加 LAST_FRAG） |
| 9 | §4.13 | 补充 ChannelCount 不变式（恒等于 TRACE_CONFIG 配置值，不符视为帧错误） |
| 10 | §2.5 | 修正 ASCII 帧示意图右边界对齐 |

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
| `Migration_v1.1_to_v2.0.md` | v1.1 → v2.0 迁移指南，含端口/版本号切换步骤 |
