# CLS-II-118 Handoff Snapshot v3
**生成时间**：2026-05-11  
**上一版本**：`CLS-II-118_Handoff_Snapshot_v2.md`  
**状态**：JD 通道已验证 ✅ / Param 通道编码完成未验证 ⚠️

---

## 重要链接（新 Threading 必读）

| 资源 | 链接 |
|---|---|
| 上位机项目 | https://github.com/LuJiayu5862/CLS-II-118 |
| 作者自建库 | https://github.com/LuJiayu5862/Library/tree/main |
| 通信协议 & Snapshot 文件夹 | https://github.com/LuJiayu5862/CLS-II-118/tree/main/CLS-II/_docs |
| JD-61101 协议文档 | `CLS-II/_docs/JD-61101-UDP通信协议.docx` |
| TcLCS-UDP 协议文档 | `CLS-II/_docs/TcLCS-UDP_Protocol_v1.1.docx` |
| TcLCS 附录C（SubID 列表） | `CLS-II/_docs/TcLCS-UDP_Protocol_v1.1_AppendixC.docx` |

---

## 项目概况

- **框架**：.NET Framework 4.7.2，WinForms，C# 8.0（`<LangVersion>8.0</LangVersion>`）
- **命名空间**：`CLS_II`（全项目统一，无子命名空间）
- **UDP 封装库**：作者自建 `UDP.UDPClient`（事件驱动，APM 模型）
  - 位于 `https://github.com/LuJiayu5862/Library/tree/main/SocketUDP`
  - 已通过 `UDP.dll` 引用进项目
- **高精度定时器**：`MmTimer.HPTimer`，`mmTimer1`周期 10ms，`mmTimer2` 周期 20ms（未启动，备用）

---

## C# 版本注意事项（务必遵守）

| 特性 | 状态 | 正确写法 |
|---|---|---|
| `new()` 目标类型推断 | ❌ C# 9.0 | 改为 `new ClassName()` |
| `Task.WaitAsync(ct)` | ❌ .NET 6+ | 改为 `Task.WhenAny` + `Task.Delay(Infinite, ct)` |
| `UdpClient.ReceiveAsync(ct)` | ❌ .NET 6+ | 改为 `Task.WhenAny` + `Task.Delay(Infinite, ct)` |
| `string?` / `T?` nullable 注解 | ❌ 需要 `<Nullable>enable</Nullable>` | 统一去掉 `?`，用传统 null 检查 |
| switch 表达式 | ✅ C# 8.0 支持 | 保留 |
| `async/await` | ✅ 支持 | 保留 |

---

## 通信架构总览

```
connectToolStripButton1_Click
    └─ ConnectDevice() / DisconnectDevice()
         ├─ InitUDP() / DisposeUDP()           ← 原有 UDP（LCS 控制/参数）
         ├─ JdUdpClient.StartInstance()        ← JD-61101 通道
         └─ StartParamUdp() / StopParamUdp()   ← TcLCS-Param 通道（待整合）

mmTimer1_Ticked（10ms，HPTimer）
    ├─ lock(LCSControls)  → udpClient.Send()   [周期，原有]
    ├─ isParamChanged     → udpClient.Send()   [非周期，原有]
    ├─ JdUdpClient.Instance?.SendTx()          [JD 周期发送，新]
    └─ （Param 周期参数 CtrlIn/CtrlOut 待接入）[新，未实现]
```

### 标志位约定（2026-05-11 确认）

- **只用一个** `GlobalVar.isUdpConnceted` 作为全部三路 UDP 的整体连接状态
- 连接/断开 UI 按钮是"原子操作"，三路同步创建/销毁，无需分标志位
- `GlobalVar.isSendUdp`：控制原有 LCS 周期发送
- `GlobalVar.isParamChanged`：原有 LCS 参数非周期触发

---

## 文件职责索引

### src_IOData/

| 文件 | 职责 |
|---|---|
| `JdData.cs` | JD-61101 协议常量、`JdRxFrame`、`JdTxFrame`、`JdCodec`、**`JdData`（全局缓冲，唯一真相源）** |
| `ParamData.cs` | TcLCS-UDP 帧定义、`TcLcsConstants`、`TcFrameHeader`、`TcFrame`、`TcCodec`、`TcSubId`、`TcSubIdSize`、`Crc16Modbus` |
| `UdpData.cs` | 原有 LCS `LCSInfos`（Rx）/ `LCSControls`（周期 Tx）/ `LCSParams`（非周期 Tx）|
| `GlobalVar.cs` | 全局标志位，含 `isUdpConnceted`、`isSendUdp`、`isParamChanged` |

### src_communication/

| 文件 | 职责 |
|---|---|
| `MainForm.UDP.cs` | 原有 LCS UDP：`InitUDP()`、`DisposeUDP()`、`client_onReceived`（写入 `UdpData.LCSInfos`）|
| `JdUdpClient.cs` | JD-61101 收发客户端（**单例**，已验证✅）|
| `MainForm.ParamUDP.cs` | TcLCS-Param 集成层：`StartParamUdp()`、`StopParamUdp()`、高层 API 包装 |
| `ParamUdpClient.cs` | TcLCS-UDP 请求/响应客户端（编码完成，**未验证**⚠️）|
| `StructFunc.cs` | 结构体←→字节数组工具 |

---

## JD-61101 通道（已验证 ✅）

### 协议要点（来自 JD-61101-UDP通信协议.docx，用户已确认）

- **帧长固定 20 字节**（DATA0~DATA19）
- PLC→上位机帧头：`0xA5 0xA5 0xA5`（DATA0~DATA2）
- 上位机→PLC帧头：`0x5A 0x5A 0x5A`
- **校验**：DATA19 = SUM(DATA3~DATA18) & 0xFF
- **脚蹬位移**：DATA7~DATA10，`int32` **大端**（Big-Endian），范围 [-18000, +18000]，左脚向前为负，右脚向前为正 ← **用户 2026-05-11 确认**
- DATA3 = DeviceNo = 0x01，DATA4 = DataLen = 0x0E

### 网络配置

- 设备端口：`192.168.118.118:15000`（上位机发送目标）
- 上位机本地：`:16000`（本地监听）
- 端口常量：`GlobalVar.JdConsts.{szJdRemoteHost, nJdPortSend=15000, nJdPortRecv=16000}`

### 架构（已实现）

```
JdUdpClient（单例）
  ├─ StartInstance(host, sendPort, recvPort) → 创建 UDP.UDPClient，注册回调
  ├─ StopInstance()                          → CleanUp，置 null
  ├─ SendTx()        ← mmTimer1_Ticked 每 10ms 调用（周期发送）
  ├─ SendClearFault() / SendResetPedal()     ← 脉冲发送便捷方法
  └─ Udp_OnReceived  → TryParseRx → lock(JdData.JdRx) 写入全局缓冲
```

### 数据存放（JdData.cs 末尾 JdData 静态类）

```csharp
public static class JdData
{
    public static readonly JdRxFrame JdRx = new JdRxFrame(); // PLC→上位机（onReceived写入）
    public static readonly JdTxFrame JdTx = new JdTxFrame(); // 上位机→PLC（mmTimer读取）
}
// 所有读写必须 lock(JdData.JdRx) / lock(JdData.JdTx)
```

### MainForm 侧调用

```csharp
// ConnectDevice()
JdUdpClient.StartInstance(GlobalVar.JdConsts.szJdRemoteHost,
    GlobalVar.JdConsts.nJdPortSend, GlobalVar.JdConsts.nJdPortRecv);

// DisconnectDevice()
JdUdpClient.StopInstance();

// mmTimer1_Ticked
if (GlobalVar.isUdpConnceted)
    JdUdpClient.Instance?.SendTx();
```

---

## TcLCS-Param 通道（编码完成，未验证 ⚠️）

### 协议要点（来自 TcLCS-UDP_Protocol_v1.1.docx，用户已确认）

- **帧结构**（总长 14~1400 B）：
  `Header(11B, pack=1, LE) + Payload(0~1386B) + Trailer(3B)`
- Header 字段：`SOF0(0xAA) SOF1(0x55) Ver(0x02) DevID CMD SubID SeqNo(2B LE) PayloadLen(2B LE) FragInfo`
- Trailer：`CRC16(2B LE, MODBUS, 覆盖 Header+Payload) + EOF(0x55)`
- **TcLCS_CtrlIn SIZEOF = 68B**（TwinCAT 在线 SIZEOF 确认）← **用户 2026-05-11 确认**

### 网络配置

- 主站（TwinCAT）：`192.168.118.118:5050`
- 上位机本地：`:8080`
- 端口常量：`ParamConsts.{szParamRemoteHost, nParamPortSend=5050, nParamPortRecv=8080}`

### SubID 分类（读写属性）

| 类型 | SubID | SIZEOF | 读写 |
|---|---|---|---|
| 周期 | `TcLCS_CtrlIn` | 68B | W |
| 周期 | `TcLCS_CtrlOut` | 52B | R |
| 非周期 | 其余所有 SubID | 见附录C | R/W |

### 架构（已实现，待验证）

```
ParamUdpClient（非单例，由 MainForm.ParamUDP.cs 持有 _param 字段）
  ├─ Start()              → 创建原生 UdpClient，启动 RxLoopAsync
  ├─ Stop()               → Cancel，关闭 socket，清空 _pending
  ├─ HelloAsync()         → HELLO 握手（任何业务前必须调用一次）
  ├─ ReadAsync(sub)       → READ_REQ，超时 300ms，重试 3 次
  ├─ WriteAsync(sub, buf) → WRITE_REQ，超时 300ms，重试 3 次
  ├─ PingAsync()          → PING
  ├─ SavePersistAsync()   → SAVE_PERSIST
  └─ RxLoopAsync          → Task.WhenAny 模拟可取消收包（net472 兼容）
```

### 待实现事项

1. **集成到 `ConnectDevice()` / `DisconnectDevice()`**：目前 `StartParamUdp()` / `StopParamUdp()` 仅在 `MainForm.ParamUDP.cs` 定义，尚未挂入 `ConnectDevice()`
2. **周期参数接入 mmTimer1**：`TcLCS_CtrlIn`（Write，10ms）、`TcLCS_CtrlOut`（Read，10ms 或 20ms）
3. **数据缓冲区**：仿照 `JdData`，在 `ParamData.cs` 末尾新增 `ParamData` 静态类（`TcCtrlIn byte[68]`、`TcCtrlOut byte[52]`）
4. **单例改造**（可选）：仿照 `JdUdpClient` 改为 `ParamUdpClient.Instance`，消除 `MainForm` 持有 `_param` 字段
5. **实机验证**：HELLO 握手、READ_REQ、WRITE_REQ

### MainForm 侧现有 API（MainForm.ParamUDP.cs）

```csharp
StartParamUdp()                       // 创建并 HELLO
StopParamUdp()                        // 停止
ParamReadAsync(TcSubId sub)
ParamWriteAsync(TcSubId sub, byte[])
ParamPingAsync()
ParamSavePersistAsync()
```

---

## 原有 LCS UDP 通道（参考，勿动）

- `MainForm.UDP.cs`：`udpClient = new UDPClient(host, portOut1, portIn, 2048)`
- 接收：`client_onReceived` → `lock(UdpData.LCSInfos)` → `BytesToStruct`
- 周期发送：`mmTimer1_Ticked` → `lock(LCSControls)` → `udpClient.Send()`
- 非周期：`isParamChanged = true` → 下一 tick 发送 `LCSParams` 到 `nPortOut2`

---

## 已知问题 / 待办

| 优先级 | 项目 | 说明 |
|---|---|---|
| 🔴 高 | `ParamUdpClient.cs` 中 `new()` 需改 | `_pending = new()` → `new ConcurrentDictionary<ushort, TaskCompletionSource<TcFrame>>()` |
| 🔴 高 | Param 通道集成到 ConnectDevice | `StartParamUdp()` 挂入 `ConnectDevice()`，`StopParamUdp()` 挂入 `DisconnectDevice()` |
| 🟡 中 | `ParamData` 静态缓冲类 | 在 `ParamData.cs` 末尾新增 `TcCtrlIn`/`TcCtrlOut` 全局缓冲 |
| 🟡 中 | CtrlIn/CtrlOut 周期收发接入 mmTimer1 | Write CtrlIn 10ms / Read CtrlOut 10~20ms |
| 🟡 中 | Param 实机验证 | HELLO → PING → READ → WRITE 逐步验证 |
| 🟢 低 | `JdUdpClient` Debug.WriteLine 清理 | `Udp_OnReceived` 末尾的 `Debug.WriteLine` 调试行，验证完后移除 |
| 🟢 低 | `MainForm.ParamUDP.cs` 可考虑删除 | 若 Param 改为单例，`_param` 字段和高层 API 可直接通过 `ParamUdpClient.Instance` 访问，该文件即可废弃 |

---

## 下一个 Threading 建议起手顺序

1. 阅读本文件 + `JD-61101-UDP通信协议.docx` + `TcLCS-UDP_Protocol_v1.1.docx`
2. 读取 `src_IOData/JdData.cs`、`src_IOData/ParamData.cs`、`src_communication/JdUdpClient.cs`、`src_communication/ParamUdpClient.cs` 确认当前代码状态
3. 修复 `ParamUdpClient.cs` 中 `new()` 编译错误
4. 将 Param 通道集成进 `ConnectDevice()` / `DisconnectDevice()`
5. 实机验证 Param HELLO 握手
