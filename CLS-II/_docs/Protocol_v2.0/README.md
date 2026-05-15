# TcLCS-UDP Communication Protocol v2.0 — 参数化通用通信框架

> 本目录存放 TcLCS-UDP 通信协议 **v2.0** 的设计文档。
> v2.0 在 v1.1 基础上引入「参数表自描述」机制，实现上位机零重编译、主站变量动态注册的通用化通信框架。
> ⚠️ **当前状态：设计讨论中，协议细节待完善。**

## 设计目标

1. **上位机免重编译**：通过 `.json` 配置文件驱动读写变量与 UI 绑定，最多修改配置文件即可适配新项目。
2. **主站动态注册**：主站（TwinCAT3 ST / IgH C）通过 `PARAM_ENTRY` 结构体数组注册可读写变量，包含变量名、数据类型、字节长度、访问权限、分组ID、推荐读取周期等元信息。
3. **单变量 & 分组批量读写**：支持按 `ParamID` 单独读写，也支持按 `GroupID` 批量读写，减少命令往返开销。
4. **高频实时性**：运行阶段仅传输 `ParamID + Value`，理论支持 ≤2ms 通信周期；参数表握手仅在启动时执行一次。
5. **Trace Buffer 示波器**：主站内置采样缓冲区（最小 250μs 采样精度），上位机下发配置后批量取回，实现高精度波形显示。

## 通信阶段

| 阶段 | 时机 | 说明 |
|---|---|---|
| 握手 / 枚举 | 启动时一次 | 上位机拉取完整参数表（Parameter Dictionary） |
| 实时读写 | 运行期循环 | 按 ParamID / GroupID 高频读写 |
| Trace 配置 | 按需触发 | 下发采样配置，上传 Buffer 数据 |

## 新增指令集（草案）

| CmdType | 名称 | 方向 | 说明 |
|---|---|---|---|
| `0x10` | GET_PARAM_DICT | 上位机 → 主站 | 请求参数表（分页） |
| `0x11` | PARAM_DICT_RESP | 主站 → 上位机 | 参数表应答 |
| `0x12` | READ_BY_ID | 上位机 → 主站 | 按 ParamID 批量读 |
| `0x13` | READ_BY_ID_RESP | 主站 → 上位机 | 读取应答 |
| `0x14` | WRITE_BY_ID | 上位机 → 主站 | 按 ParamID 批量写 |
| `0x15` | WRITE_BY_ID_RESP | 主站 → 上位机 | 写入确认 |
| `0x16` | READ_GROUP | 上位机 → 主站 | 按 GroupID 批量读 |
| `0x17` | READ_GROUP_RESP | 主站 → 上位机 | 批量读应答 |
| `0x18` | WRITE_GROUP | 上位机 → 主站 | 按 GroupID 批量写 |
| `0x20` | TRACE_CONFIG | 上位机 → 主站 | 下发示波器采样配置 |
| `0x21` | TRACE_UPLOAD_REQ | 上位机 → 主站 | 请求上传 Trace Buffer |
| `0x22` | TRACE_UPLOAD_RESP | 主站 → 上位机 | 批量上传采样数据 |

## 文档列表（待补充）

| 文件 | 状态 | 说明 |
|---|---|---|
| `ParamEntry_DataStructure.md` | 🔲 待编写 | PARAM_ENTRY 结构体详细定义 |
| `FrameFormat_v2.0.md` | 🔲 待编写 | 完整帧格式规范 |
| `TraceBuffer_Design.md` | 🔲 待编写 | Trace Buffer 功能块设计 |
| `CSharp_ParamDictionary.md` | 🔲 待编写 | C# 端参数表实现方案 |
| `Migration_v1.1_to_v2.0.md` | 🔲 待编写 | v1.1 → v2.0 迁移指南 |

---

> 本协议兼容 TwinCAT 3（TC/BSD）主站与 IgH EtherCAT（Debian 12）主站，传输层复用现有 UDP socket。
> 串口场景下将 UDP Payload 封装为 `[SOF][Length][Payload][CRC][EOF]` UART 帧即可复用全部应用层逻辑。
