# TcLCS-UDP Communication Protocol v2.0 — 参数化通用通信框架

> 本目录存放 TcLCS-UDP 通信协议 **v2.0** 的设计文档。
> v2.0 在 v1.1 基础上引入「参数表自描述」机制，实现上位机零重编译、主站变量动态注册的通用化通信框架。
> ⚠️ **当前状态：设计讨论中，议题 1~12 已全部冻结，文档持续完善中。**

---

## 设计目标

1. **上位机免重编译**：通过 `.json` 配置文件驱动读写变量与 UI 绑定，最多修改配置文件即可适配新项目。
2. **主站动态注册**：主站（TwinCAT3 ST）通过 `PARAM_ENTRY` 结构体数组注册可读写变量，包含变量名、数据类型、字节长度、访问权限、分组ID、单位、推荐读取周期、描述等元信息。
3. **单变量 & 分组批量读写**：支持按 `ParamID` 单独读写，也支持按 `GroupID` 批量读写，减少命令往返开销。同组变量读取保证原子性（同一 PLC 扫描周期内快照 MEMCPY）。
4. **高频实时性**：运行阶段仅传输 `ParamID + Value`，理论支持 ≤2ms 通信周期；参数表握手仅在启动时执行一次。
5. **Trace Buffer 示波器**：主站内置采样缓冲区（最小 250μs 采样精度），上位机下发配置后批量取回，实现高精度波形显示。
6. **多场景封装**：应用层语义统一，UDP 为主场景，UART 点对点为未来兼容封装，无需新增 CMD。

---

## 通信阶段

| 阶段 | 时机 | 说明 |
|------|------|------|
| 握手 / 枚举 | 启动时一次 | HELLO 握手（获取 DictHash、FW 版本、DeviceName），按需拉取完整参数表 |
| 实时读写 | 运行期循环 | 按 ParamID / GroupID 高频读写；PING/PONG 顺带校验 DictHash |
| Trace 配置 | 按需触发 | 下发采样配置，上传 Buffer 数据 |

---

## 指令码速查（v2.0 正式定义）

> 完整 Payload 格式见 [`FrameFormat_v2.0.md`](./FrameFormat_v2.0.md)。

| CMD (REQ) | CMD (ACK) | 名称 | 说明 |
|-----------|-----------|------|------|
| `0x03` | `0x83` | PING / PONG | 心跳；PONG 携带 `DictHash` 用于失配检出 |
| `0x05` | `0x85` | HELLO / HELLO_ACK | 握手；ACK 携带 DictHash、ParamCount、FW 版本、DeviceName |
| `0x10` | `0x90` | GET_PARAM_DICT | 请求参数表（分页，每页 ≤10 条） |
| `0x11` | `0x91` | GET_PARAM_BY_ID | 查询单个 ParamID 完整元信息 |
| `0x12` | `0x92` | GET_ENUM_MAP | 查询枚举值-文本映射；非枚举类型返回 MapCount=0 |
| `0x13` | `0x93` | GET_GROUP_DICT | 请求已注册组信息列表（含组名、描述、推荐操作频率、成员数量） |
| `0x20` | `0xA0` | READ_BY_ID | 按 ParamID 列表批量读 |
| `0x21` | `0xA1` | WRITE_BY_ID | 按 ParamID 列表批量写 |
| `0x22` | `0xA2` | READ_GROUP | 按 GroupID 读整组（原子快照） |
| `0x23` | `0xA3` | WRITE_GROUP | 按 GroupID 写整组（best-effort，支持子集写） |
| `0x30`–`0x34` | `0xB0`–`0xB4` | TRACE_* | 示波器采样配置、启停、状态查询、数据上传 |
| `0x40` | — | NOTIFY | 主站主动推送订阅变量（无对应 REQ） |
| `0x50` | `0xD0` | PARAM_SUBSCRIBE | 注册周期订阅 |
| `0x51` | `0xD1` | PARAM_UNSUBSCRIBE | 取消指定订阅 |
| `0x52` | `0xD2` | SUBSCRIBE_CLEAR | 清除全部订阅 |
| `0xEE` | — | ERR | 错误响应 |

---

## 传输场景

### UDP 主场景

- **端口**：`5051`（v2.0 专用）；v1.1 端口 `5050` / `15000` 不受影响
- **最大帧长**：1400B（Header 12B + Payload ≤1385B + Trailer 3B）
- **参数表分页**：每页最多 **10 条**（含 RangeFlags/MinVal/MaxVal 扩展后最坏单条 132B × 10 = 1327B ≤ MTU 1385B）

### UART 兼容封装场景

> 应用层语义与 UDP **完全一致**，不引入新 CMD，不引入 RS-485 地址仲裁或 SLIP 编码。

在原始 v2.0 帧外增加 **2B 长度前缀外壳**：

```
[FrameLen   : UINT  2B]   // 内层 v2.0 完整帧字节数
[v2.0 Frame : FrameLen B] // 原封不动的标准 v2.0 帧
```

| 项目 | UDP 场景 | UART 场景 |
|------|----------|-----------|
| 最大总帧长（含外壳） | 1400B | 256B |
| 最大 Payload | 1385B | 239B |
| 参数表分页建议 | 10 条/页（默认） | 2~3 条/页（典型）；1 条/页（最保守） |
| 基准波特率 | — | 115200 bps |

---

## 关键设计决策（ADR 冻结议题）

| 议题 | 决策摘要 |
|------|----------|
| 议题 1：主站变量寻址 | PVOID 指针 + MEMCPY，上电 bInit 周期注册一次 |
| 议题 2：GroupID 语义 | 单 BYTE，0x00=不分组，0x01~0xFE=有效分组，0xFF=保留 |
| 议题 3：写操作确认（2026-05-17 修订） | ACK_REQ 由上位机操作类型直接置位；周期写=0（fire & forget），手动写=1；CycleClass 完全退出 ACK 推断，主站无条件响应 ACK_REQ 标志 |
| 议题 4：参数表动态性 | 启动/手动拉取，运行时不自动重拉，DictHash=CRC16/MODBUS 检出失配 |
| 议题 4.5：自描述增强 | PARAM_ENTRY 新增 Unit(8B) + Desc(变长≤64B)；新增 GET_ENUM_MAP(0x12)；新增 STRING64/128 |
| 议题 5：HELLO 握手扩展 | HELLO_ACK 携带 DictHash、ParamCount、FW SemVer、DeviceName(0xFFEF) |
| 议题 6：UART 封装 | 2B FrameLen 外壳，256B 总帧长上限；UART 推荐 PageSize=2~3，最保守 PageSize=1 |
| 议题 7：GROUP_ENTRY 注册机制 | 新增 GET_GROUP_DICT(0x13)；MemberCount 主站自动统计；WRITE_GROUP = best-effort + 支持子集写 |
| 议题 8：订阅推送 | Periodic Only；订阅管理 0x50~0x52，NOTIFY=0x40；断线即失效 |
| 议题 9：参数表语义定位 | 参数表只描述物理属性；写操作由上位机业务层完全自主决定 |
| 议题 10：值域范围字段 | PARAM_ENTRY 新增 RangeFlags/MinVal/MaxVal（固定部分 17B→34B）；PageSize 默认 12→10 |
| 议题 11：WriteEnable | pWriteEnable 纯主站运行时字段，不参与协议传输和 DictHash；ErrCode=0x0E |
| 议题 12：MemberCount | 主站自动统计，参与 DictHash |

> 完整决策依据见 [`ADR_v2.0.md`](./ADR_v2.0.md)。

---

## 保留 ParamID 区段（0xFFEF ~ 0xFFFF）

| ParamID | 名称 | DataType | Access | 说明 |
|---------|------|----------|--------|------|
| `0xFFEF` | DeviceName | STRING32 | RO | 握手时主站读取，填入 HELLO_ACK |
| `0xFFF0` | WriteErrorCount | UINT | RO | 累计写入错误次数，上电清零 |
| `0xFFF1` | LastErrorParamID | UINT | RO | 最近一次写入错误的 ParamID |
| `0xFFF2` | LastErrorCode | BYTE | RO | 最近一次写入错误的错误码 |
| `0xFFF3`–`0xFFFF` | — | — | — | 保留 |

---

## 文档列表

| 文件 | 状态 | 说明 |
|------|------|------|
| `FrameFormat_v2.0.md` | ✅ 已更新 | 完整帧格式规范（含 UART 封装章节） |
| `ADR_v2.0.md` | ✅ 已完成 | 设计决策记录，议题1~12全部冻结 |
| `Protocol_v2.0_Context_Snapshot.md` | ✅ 已更新 | 设计上下文快照，供跨 Thread 恢复使用 |
| `ParamEntry_DataStructure.md` | 🔲 待编写 | PARAM_ENTRY 结构体详细定义（ST 实现） |
| `TraceBuffer_Design.md` | 🔲 待编写 | Trace Buffer 功能块设计 |
| `CSharp_ParamDictionary.md` | 🔲 待编写 | C# 端参数表实现方案 |
| `Migration_v1.1_to_v2.0.md` | 🔲 待编写 | v1.1 → v2.0 迁移指南 |

---

> **技术栈**：主站 TwinCAT 3 XAE（IEC 61131-3 ST）；上位机 C# .NET 4.7.2 WinForms；通信 UDP 主场景 + UART 兼容封装；版本管理 GitHub `dev/protocol-v2.0` 分支。
