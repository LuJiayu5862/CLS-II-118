# TcLCS-UDP Protocol v2.0 — 上下文快照（Context Snapshot）

> **快照时间**：2026-05-18
> **适用分支**：`dev/protocol-v2.0`
> **用途**：新 threading 开始时读取此文件即可恢复完整设计上下文

---

## 一、文档结构

| 文件 | 内容 | 状态 |
|------|------|------|
| `ADR_v2.0.md` | 架构决策记录，议题1~12 | ✅ 最新（2026-05-18）|
| `FrameFormat_v2.0.md` | 帧格式完整规范 | ✅ 最新（2026-05-18）|
| `README.md` | 设计概要与进度说明 | ⚠️ 待同步议题9~12 |

---

## 二、已冻结议题完整列表

### 议题 1：主站变量寻址方案 ✅
- **决策**：PVOID 指针 + MEMCPY
- 上电 bInit 周期注册一次，运行时 O(1) 定位

### 议题 2：GroupID 语义边界 ✅
- 0x00=不分组，0x01~0xFE=有效分组，0xFF=保留
- 同组读取保证原子性（同一 PLC 扫描周期内快照）

### 议题 3：ACK 确认机制 ✅（2026-05-17 修订）
- **决策**：`Flags.ACK_REQ` 由上位机根据操作类型直接置位，主站无条件响应
- 周期写 ACK_REQ=0（fire & forget），手动写 ACK_REQ=1
- **CycleClass 完全退出 ACK 推断**，仅服务于读取侧（订阅间隔/轮询建议）

### 议题 4：参数表动态性 + DictHash ✅
- 启动/手动拉取一次，运行时不自动重拉
- **DictHash = CRC16/MODBUS**
- 接口落点：GET_PARAM_DICT ACK、PING_ACK/PONG、HELLO_ACK

### 议题 4.5：自描述增强 ✅
- PARAM_ENTRY 新增 `Unit(8B定长)` + `Desc(变长≤64B)`
- 新增 `GET_ENUM_MAP`（0x12/0x92）
- 新增 DataType：STRING64(0x10)、STRING128(0x11)
- 原固定部分 17B（基础 9B + Unit 8B）

### 议题 5：HELLO 握手扩展 ✅
- HELLO_ACK 新增：DictHash(2B) + ParamCount(2B) + FW_Major/Minor/Patch(3B) + DevNameLen(1B) + DeviceName(变长)
- FW SemVer 兼容规则：Major 不一致拒绝连接，Minor 降级运行，Patch 静默
- 新增保留 ParamID：`0xFFEF` DeviceName(STRING32)

### 议题 6：UART 封装 ✅
- 2B 长度前缀外壳（方案 B1）
- UART 内层最大帧 254B，Payload 上限 239B
- 分页建议 PageSize=1（最保守），PageSize=2~3（典型）
- 不引入 RS-485 地址仲裁、SLIP、串口专用 CMD

### 议题 7：GROUP_ENTRY 注册机制 ✅（2026-05-18 第二次修订）
- **新增指令**：`GET_GROUP_DICT`（0x13/0x93）
- GROUP_ENTRY 结构：`GroupID(1B) + CycleClass(1B) + MemberCount(1B) + NameLen(1B) + Name + DescLen(1B) + Desc`
- **`MemberCount`：主站自动统计，不需手动配置**
- CycleClass 仅属读取侧建议，**不参与 ACK 推断**
- **WRITE_GROUP 语义改为 best-effort + 支持子集写**（Count 无需等于组内总数）
- DictHash 字节串：`GroupID(1B) + MemberCount(1B) + NameLen(1B) + Name`
- `0x0A` ErrCode 修订为越组写入（ParamID 不属于目标 GroupID）

### 议题 8：SUBSCRIBE / NOTIFY 推送 ✅
- CMD 分区：订阅管理 0x50~0x52，主站主动推送 0x40
- Periodic Only（Mode=0x00），Deadband 不在 v2.0 范围
- 断线即失效，生命周期与会话绑定
- 槽位上限由实现端编译宏自行决定

### 议题 9：参数表语义定位与写操作模型 ✅（2026-05-17 新增）
- 参数表只描述变量物理属性，不预定义操作行为
- CycleClass = 物理更新周期，读取侧参考
- 写操作由上位机业务层完全自主决定
- GROUP = 岂用于结构化配置参数写入场景

### 议题 10：PARAM_ENTRY 值域范围字段 ✅（2026-05-18 新增）
- PARAM_ENTRY 新增：`RangeFlags(1B) + MinVal(LREAL 8B) + MaxVal(LREAL 8B)`
- **PARAM_ENTRY 固定传输部分从 17B 增至 34B**
- `RangeFlags`：Bit0=MinValid，Bit1=MaxValid，0x00=无范围约束
- BOOL/STRING 类型强制 RangeFlags=0x00
- 主站写入前将待写入值转换为 LREAL 后与 MinVal/MaxVal 比较
- `ErrCode=0x08` 正式定义：**写入值超出注册值域范围**
- **`PARAM_DICT_PAGE_SIZE` 从 12 改为 10**（最坏单条 132B × 10 = 1327B ≤ 1385B）
- GET_PARAM_DICT REQ 新增 `PageSize(1B)`，0=默认，1~10=使用请求值，>10按 10 处理
- RangeFlags/MinVal/MaxVal 参与协议传输，**不参与 DictHash**

### 议题 11：写入前提条件 WriteEnable ✅（2026-05-18 新增）
- PARAM_ENTRY 新增纯主站运行时字段：
  - TwinCAT ST：`pWriteEnable : POINTER TO BOOL`，0=无条件可写
  - 嵌入式 C：`bool *p_write_enable`，NULL=无条件可写
- **不参与协议传输，不参与 DictHash**
- 写入判断顺序（WRITE_BY_ID 与 WRITE_GROUP 共用）：
  - ① ParamID 不存在 → 0x04
  - ② Access=RO → 0x05
  - ③ pWriteEnable 非空且值为 FALSE → 0x0E
  - ④ 值域超出 RangeFlags 约束 → 0x08
  - ⑤ MEMCPY 写入成功 → 0x00
- `ErrCode=0x0E` 新增：写入前提条件不满足

### 议题 12：GROUP_ENTRY MemberCount ✅（2026-05-18 新增）
- MemberCount 字段已统一至议题 7 GROUP_ENTRY 结构中（不单独存在）
- MemberCount 由主站在参数注册完成后自动统计
- **MemberCount 参与 DictHash**：组成员数变化影响上位机对组的理解

---

## 三、DictHash 字段参与规则（当前冻结版）

### PARAM_ENTRY 参与

```
ParamID(2B) + DataType(1B) + ByteSize(1B) + Access(1B) +
GroupID(1B) + NameLen(1B) + Name(NameLen B)
```

| 字段 | 参与 Hash | 理由 |
|------|-----------|------|
| ParamID | ✅ | 寻址 |
| DataType / ByteSize | ✅ | 解析 |
| Access | ✅ | 写入判断 |
| GroupID | ✅ | 组操作寻址 |
| NameLen + Name | ✅ | 识别 |
| CycleClass | ❌ | 读取侧建议值 |
| Unit / DescLen / Desc | ❌ | 展示信息 |
| RangeFlags / MinVal / MaxVal | ❌ | 写入保护，不影响寻址和结构 |

### GROUP_ENTRY 参与（按 GroupID 升序，追加在 PARAM_ENTRY 之后）

```
GroupID(1B) + MemberCount(1B) + NameLen(1B) + Name(NameLen B)
```

| 字段 | 参与 Hash | 理由 |
|------|-----------|------|
| GroupID | ✅ | 组寻址 |
| MemberCount | ✅ | 组规模展示和组操作构建 |
| NameLen + Name | ✅ | 组识别 |
| CycleClass | ❌ | 读取侧建议值 |
| DescLen + Desc | ❌ | 展示信息 |

---

## 四、PARAM_ENTRY 完整结构（当前冻结版）

```
ParamID    : UINT   2B
DataType   : BYTE   1B
ByteSize   : BYTE   1B
Access     : BYTE   1B
GroupID    : BYTE   1B
CycleClass : BYTE   1B   // 物理更新周期，读取侧参考，不参与 ACK/Hash
Unit       : BYTE[8] 8B  // 定长 UTF-8，不足补0
NameLen    : BYTE   1B
Name       : NameLen B   // 最长 32B
DescLen    : BYTE   1B
Desc       : DescLen B   // 最长 64B
RangeFlags : BYTE   1B   // Bit0=MinValid, Bit1=MaxValid
MinVal     : LREAL  8B   // 最小允许写入值
MaxVal     : LREAL  8B   // 最大允许写入值
────────────────────────────────────────────────────────────
固定部分 = 34B（原 17B + RangeFlags/MinVal/MaxVal 17B）
最坏单条 Entry = 34 + 1 + 32 + 1 + 64 = 132B

主站运行时字段（不参与协议传输，不参与 DictHash）：
pWriteEnable : POINTER TO BOOL   // TwinCAT ST，0=无条件可写
```

---

## 五、GROUP_ENTRY 完整结构（当前冻结版）

```
GroupID     : BYTE  1B
CycleClass  : BYTE  1B   // 组推荐操作频率，读取侧建议值，不参与 ACK/Hash
MemberCount : BYTE  1B   // 本组已注册 PARAM 数，主站自动统计
NameLen     : BYTE  1B
Name        : NameLen B  // 最长 32B
DescLen     : BYTE  1B
Desc        : DescLen B  // 最长 64B
```

---

## 六、ErrCode 完整速查表

| ErrCode | 含义 | 来源 |
|---------|------|------|
| 0x00 | 成功 | — |
| 0x01 | 未知 CMD | v1.1 继承 |
| 0x02 | PayloadLen 不合法 | v1.1 继承 |
| 0x03 | CRC 错误 | v1.1 继承 |
| 0x04 | ParamID 不存在 | v2.0 |
| 0x05 | 变量只读 | v2.0 |
| 0x06 | 参数表页码越界 | v2.0 |
| 0x07 | Trace Buffer 未就绪 | v2.0 |
| **0x08** | **写入值超出注册值域范围** | **议题10 正式定义** |
| 0x09 | GroupID 不存在或未注册 | v2.0 |
| **0x0A** | **越组写入（ParamID 不属于目标 GroupID）** | **议题7 修订** |
| 0x0B | 订阅槽位已满 | 议题8 |
| 0x0C | SubID 不存在或已过期 | 议题8 |
| 0x0D | 订阅变量数超出单槽上限 | 议题8 |
| **0x0E** | **写入前提条件不满足（pWriteEnable）** | **议题11 新增** |
| 0x0F~0xFF | 预留 | — |

---

## 七、上位机写入判断顺序（WRITE_BY_ID 与 WRITE_GROUP 共用）

```
① ParamID 不存在                    → 0x04
② Access = RO                       → 0x05
③ pWriteEnable 非空且值为 FALSE     → 0x0E
④ 值域超出 RangeFlags 约束           → 0x08
⑤ MEMCPY 写入成功                   → 0x00
```

**WRITE_GROUP 额外判断**：① 中若 ParamID 不属于目标 GroupID → 0x0A

---

## 八、分页参数当前冻结值

| 场景 | PARAM_DICT_PAGE_SIZE | 备注 |
|------|---------------------|------|
| UDP 默认 | **10** | 最坏单条 132B × 10 = 1327B ≤ 1385B |
| UART 最保守 | 1 | 最坏单条 132B ≤ 239B Payload 上限 |
| UART 典型 | 2~3 | 短 Name、无 Desc 场景 |

GET_PARAM_DICT REQ 新增：`PageSize(1B)`，0=默认，1~10=使用请求值，>10=按 10 处理。

---

## 九、待处理工作项

| 项目 | 状态 |
|------|------|
| README.md 同步议题9~12 | ⚠️ 待推送 |
| ParamEntryDataStructure.md （如有）同步 Range 字段 | ⚠️ 待确认是否存在 |
| CSharpParamDictionary.md 同步 PageSize + Range 解析 | ⚠️ 待确认是否存在 |
| Migration v1.1→v2.0 说明文档 | ⚠️ 待编写 |
| TRACE_UPLOAD 进一步 MTU 验算（8通道×LREAL×100 = 6400B） | ⚠️ 待确认 |

---

## 十、关键设计原则备忘

1. **CycleClass 只属读取侧**：不参与 ACK 推断，不参与 DictHash（PARAM 和 GROUP 均同）
2. **ACK_REQ 由上位机操作类型决定**：周期写=0，手动写=1，主站无条件响应
3. **WRITE_GROUP = best-effort**：支持子集写，逐 Item 独立判断，不保证原子性
4. **Range 字段不参与 DictHash**：写入保护，不影响结构
5. **MemberCount 参与 DictHash**：组成员数变化影响协议行为
6. **pWriteEnable 不参与协议传输**：纯主站运行时字段，不参与 DictHash
7. **ERR 返回策略**：单个写入失败进 WriteResult，不返回整帧 ERR；越组写入（ParamID 不属于目标 GroupID）进 WriteResult=0x0A
