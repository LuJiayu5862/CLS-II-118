# CLS-II-118 开发交接快照 v4

> **生成时间**：2026-05-11  
> **编写者**：AI Assistant（基于代码库完整读取）  
> **接续自**：`TcLCS-UDP_v1.1_Handoff_Snapshot.md` / `CLS-II-118_Handoff_Snapshot_v3.md`  
> **里程碑**：✅ TcLCS-UDP Param 通道收发验证通过

---

## 一、当前项目整体状态

```
CLS-II-118/
└── CLS-II/
    ├── src_communication/         ← Param UDP 通信层（本次核心）
    │   ├── ParamUdpClient.cs      ✅ 完成 + 验证通过
    │   ├── MainForm.ParamUDP.cs   ✅ 完成 + 验证通过
    │   ├── MainForm.UDP.cs        ✅ 既有（JD 通道，沿用）
    │   └── JdUdpClient.cs         ✅ 既有（JD 通道，沿用）
    ├── src_IOData/
    │   ├── ParamData.cs           ✅ 完成（协议结构体 + 全局缓冲区）
    │   ├── UdpData.cs             ✅ 既有（JD 数据结构，沿用）
    │   └── UdpConfig.cs           ✅ 既有
    ├── src_main/
    │   ├── MainForm.cs            ✅ button1_Click PING 验证已写入
    │   └── MainForm.Method.cs     ✅ ConnectDevice/DisconnectDevice 集成 Param 通道
    └── _docs/
        └── *.md / *.docx          文档齐全
```

---

## 二、本阶段完成内容（v3 → v4）

### 2.1 ParamData.cs — 协议基础层（完全新建）

| 类/结构 | 作用 | 状态 |
|---|---|---|
| `TcLcsConstants` | SOF/EOF/偏移常量 | ✅ |
| `TcCmd` (enum) | 命令码 READ/WRITE/PING/HELLO/SAVE | ✅ |
| `TcSubId` (enum) | 14个SubID + ALL + Bulk | ✅ |
| `TcStatus` (enum) | 11个错误状态码 | ✅ |
| `TcSubIdSize` | SubID→Payload大小映射 + IsReadOnly() | ✅ |
| `TcFrameHeader` | 帧头结构 | ✅ |
| `TcFrame` | 完整帧（Header+Payload） | ✅ |
| `Crc16Modbus` | CRC-16/MODBUS 计算 + 自检 | ✅ |
| `TcCodec` | 帧构建(Build) + 帧解析(TryParse) | ✅ |
| `ST_CLSModel` | SubID 0x01，22×LREAL，176B，R/W | ✅ |
| `ST_CLSParam` | SubID 0x02，1×BOOL+17×LREAL，144B，R/W | ✅ |
| `ST_CLS5K` | SubID 0x03，14×LREAL，112B，R/W | ✅ |
| `ST_CLSConsts` | SubID 0x04，BOOL×2+UINT+12×LREAL，104B，R/W | ✅ |
| `ST_TestMDL` | SubID 0x05，11×LREAL，88B，R/W | ✅ |
| `ST_CLSEnum` | SubID 0x06，8×SINT+INT+9×UINT，28B，R/W | ✅ |
| `ST_XT` | SubID 0x07，ARRAY[0..20] LREAL，168B，R/W | ✅ |
| `ST_YT` | SubID 0x08，ARRAY[0..20] LREAL，168B，R/W | ✅ |
| `ST_DeviceInfo` | SubID 0x10，16B，R/W | ✅ |
| `ST_UDP_Parameter` | SubID 0x11/0x12，48B，R/W，含IP字符串辅助方法 | ✅ |
| `ST_TcLCS_U` | SubID 0x13（CtrlIn），68B，R/W | ✅ |
| `ST_TcLCS_Y` | SubID 0x14（CtrlOut），52B，R only | ✅ |
| `ParamData` | 全局静态缓冲区，13个struct + 13个锁对象 | ✅ |
| `ParamData.Serialize<T>()` | struct→byte[]，通用 | ✅ |
| `ParamData.SerializeCtrlIn()` | CtrlIn专用快捷序列化（带lock） | ✅ |
| `ParamData.TryDeserialize()` | TcFrame→按SubID分发→存入全局变量 | ✅ |

**重要设计决定（用户确认）**：
- `TcLCS_CtrlOut`（SubID 0x14）和 `ALL`（0x00）为 **只读**，`IsReadOnly()` 返回 true，WriteAsync 会抛异常保护
- `TcLCS_CtrlIn` SIZEOF = **68B**（经 TwinCAT 在线 SIZEOF 验证，2026-05-11）
- STRING(15) 字段使用 `[MarshalAs(UnmanagedType.ByValArray, SizeConst=16)]`，不使用 `unsafe fixed byte`

---

### 2.2 ParamUdpClient.cs — 通信客户端（完全新建）

**架构**：单例（`StartInstance` / `StopInstance`），模仿 `JdUdpClient` 生命周期管理。

**核心机制**：
- `ConcurrentDictionary<ushort, TaskCompletionSource<TcFrame>>` 做 SeqNo 匹配
- 超时 300ms，重试 3 次；连续 3 次超时自动触发 HELLO 恢复
- RxLoop 后台 Task 持续收包，非 pending 帧触发 `OnUnsolicited` 事件

**公开 API**：

```csharp
// 启动（ConnectDevice中调用）
ParamUdpClient.StartInstance(host, sendPort, recvPort, deviceId);

// 停止（DisconnectDevice中调用）
ParamUdpClient.StopInstance();

// 业务操作
await client.HelloAsync();           // HELLO握手（必须先调用）
await client.PingAsync();            // PING→PONG延迟测试
await client.ReadAsync(TcSubId.xxx); // 读取任意SubID
await client.WriteAsync(TcSubId.xxx, payload); // 写入（自动校验size+readonly）
await client.SavePersistAsync();     // 触发PERSISTENT落盘
```

---

### 2.3 MainForm.ParamUDP.cs — MainForm 高层包装（完全新建）

提供 MainForm 内直接调用的简洁 API：

```csharp
await ParamReadAsync(TcSubId sub)       // READ_REQ → READ_ACK
await ParamWriteAsync(TcSubId sub, byte[] payload)  // WRITE_REQ → WRITE_ACK
await ParamPingAsync()                  // PING → PONG
await ParamSavePersistAsync()           // SAVE_PERSIST → SAVE_ACK
```

**集成点**：
- `StartParamUdpAsync()` 在 `ConnectDevice()` 中调用，完成 HELLO 握手
- `StopParamUdp()` 在 `DisconnectDevice()` 中调用，清理资源

---

### 2.4 验证状态（2026-05-11）

| 测试项 | 结果 |
|---|---|
| PING → PONG（button1_Click） | ✅ 通过，seq/payloadLen/payload 均正确 |
| READ_REQ → READ_ACK（全部 SubID） | ✅ 通过（逐一读取弹窗验证） |
| WRITE_REQ → WRITE_ACK（CLS5K 清零/恢复） | ✅ 通过 |
| 收发延迟测量 | ⏳ **待测试（明日任务）** |
| 写入延迟测量 | ⏳ **待测试（明日任务）** |

---

## 三、下阶段任务清单（优先级排序）

### 任务 1：延迟验证 ⏳（明日首要）

测量以下指标并记录：
- `ReadAsync()` RTT（从 Send 到 TCS SetResult 的时间）
- `WriteAsync()` RTT
- 连续 13 个 SubID 全部读取的总耗时
- 建议用 `Stopwatch` 计时，结果写入 Debug.WriteLine 或弹窗

参考测试位置：`MainForm.cs` 的 button 回调或新建专用测试 button。

---

### 任务 2：界面输入输出适配 🔲

目标：将 ParamData 中的所有字段接入界面控件（TextBox / NumericUpDown / Label）。

需要适配的界面区域（参考现有 UdpTest 窗体风格）：
- CtrlIn：写入控件（可编辑），对应 ST_TcLCS_U 17个字段
- CtrlOut：只读显示，对应 ST_TcLCS_Y 13个字段
- CLSModel / CLSParam / CLS5K / CLSConsts / TestMDL：参数编辑 Panel
- DeviceInfo / UdpDataCfg / UdpParamCfg：配置面板
- XT / YT：表格或列表控件显示21个元素

---

### 任务 3：Watch.cs 数据接入 🔲（核心任务）

**背景**：现有 Watch 窗体已实现对 `UdpData`（JD通道数据）的实时监控、手动修改、示波器导入功能。  
**目标**：将 JD 数据和 Param 数据同时接入 Watch，统一监控界面。

**需要做的事**：

1. **数据源适配**
   - 现有 Watch 基于 `UdpWatch`（`UDPControls` / `UDPParams` / `UDPInfos` List）
   - 需新增 `ParamWatch` 静态类，模仿 `UdpWatch` 结构，用反射遍历 `ParamData` 的所有字段生成 Watch 条目

2. **实时更新接入**
   - JD 通道：`mmTimer1`（10ms）周期发送，`udpClient` 接收后刷新 `UdpData`
   - Param 通道：**非周期** Request/Response，需要决策"定时轮询"还是"手动刷新"策略
   - 建议：Param Watch 增加"自动轮询间隔"配置（如 100ms/500ms/手动）

3. **示波器数据源扩展**
   - 现有示波器（ScopeView_YT / ScopeView_XY）接入 `UdpData` 的 `_Feedback` 字段
   - 需扩展为可选择 JD 或 Param 数据源，key字段包括：
     - JD: `fwdPosition`, `fwdForce`, `fwdVelocity`, `cableForce`, `trimPosition`
     - Param: `CtrlOut.*`（同上字段但来自 Param 通道）

4. **关键文件**：
   - `Watch.cs` / `Watch.Method.cs`（待读取，下次开发开始时第一件事）
   - `ScopeView_YT.cs` / `ScopeView_XY.cs`（待读取）

---

## 四、关键常量与配置（下一个 Thread 必读）

```csharp
// src_GLV 或 src_configFile 中定义（需确认实际位置）
static class ParamConsts
{
    public const string szParamRemoteHost = "...";  // Param UDP 服务端 IP
    public const int nParamPortSend = ...;           // 服务端接收端口
    public const int nParamPortRecv = ...;           // 本机接收端口
    public const byte byParamDeviceId = 0x01;        // 设备 ID
}
```

---

## 五、协议约束速查（下一个 Thread 必读）

| 约束 | 值 |
|---|---|
| 帧头 SOF | `0xAA 0x55` |
| Version | `0x02`（v1.1） |
| EOF | `0x55` |
| Header 长度 | 11 B |
| Trailer | CRC16(2B LE) + EOF(1B) = 3B |
| 最小帧 | 14 B |
| 最大帧 | 1400 B |
| CRC算法 | CRC-16/MODBUS，Poly=0xA001，Init=0xFFFF |
| SeqNo=0 | 保留给 HELLO，业务 SeqNo 从 1 开始递增 |
| **只读 SubID** | `ALL`(0x00)、`TcLCS_CtrlOut`(0x14) |
| 超时策略 | 300ms × 3次重试，连续3次失败自动重发HELLO |

---

## 六、SubID 尺寸速查表

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

## 七、下一个 Thread 开始前的第一步

1. **读取以下文件**（尚未读取，Watch适配必需）：
   - `CLS-II/src_watch_scope/` 目录下所有文件
   - `CLS-II/src_GLV/` 目录下所有文件（全局变量定义）
   - `CLS-II/src_configFile/` 目录（ParamConsts 等配置常量）

2. **确认延迟测试结果**并更新此文档

3. **参考文档**：
   - `_docs/TcLCS-UDP_Protocol_v1.1.docx`（协议主文档）
   - `_docs/TcLCS-UDP_Protocol_v1.1_AppendixC.docx`（附录C）
   - `_docs/JD-61101-UDP通信协议.docx`（JD通道协议）

---

*本快照由 AI Assistant 于 2026-05-11 基于代码库完整读取后生成，所有字段尺寸经用户确认。*
