# CLS-II-118 项目交接 Snapshot v2

> **目的**：给下一个 Thread / 下一位接手者一个高密度、零遗漏的状态快照。
> **生成时间**：2026-05-09 17:10 HKT
> **项目阶段**：
> - TcLCS v1.1 协议 + ST + C# bench 三段式已闭环 ✅
> - CLS-II 主上位机 C# 项目骨架已搭建，通信文件已创建但**帧结构尚未按协议实现** ⚠️
> - 下一步：新 Thread 必须按两份协议文档重写 Data + Param 通信层

> **⚠️ 本 Thread 跳车原因**：上下文过长，助手开始在 JD-61101 和 TcLCS-Param 的帧结构上**自编内容**而非严格对照协议文档，已被用户发现并纠正。新 Thread 必须先读协议文档再写代码。

---

## 🗂️ 一、必读文件清单（新 Thread 开场请让用户上传 or 确认 GitHub 已有）

| # | 文件 | 存放位置 | 角色 |
|---|---|---|---|
| 1 | `JD-61101-UDP通信协议.docx`             | `CLS-II/_docs/` ✅ GitHub 已有 | **JD-61101 Data 通道帧结构定义**（新 Thread 必读，本 Thread 未读到内容） |
| 2 | `TcLCS-UDP_Protocol_v1.1.docx`          | `CLS-II/_docs/` ✅ GitHub 已有 | TcLCS Param 通道帧结构定义（v1.1 定稿） |
| 3 | `TcLCS-UDP_Protocol_v1.1_AppendixC.docx`| `CLS-II/_docs/` ✅ GitHub 已有 | 性能基线附录（主站 1 ms 周期决策） |
| 4 | `TcLCS-UDP_TestCard_v1.1.docx`          | `CLS-II/_docs/` ✅ GitHub 已有 | 验收测试卡 Part A/B/C |
| 5 | `TcLCS-UDP_v1.1_Handoff_Snapshot.md`    | `CLS-II/_docs/` ✅ GitHub 已有 | 上上次 Thread 交接快照（TcLCS 协议+ST+bench 完整状态） |
| 6 | `EnumDefine.cs` / `MmTimer.cs` / `UDP.cs` | 用户本地库 | 用户自研高精度定时器 + UDP 库（项目已引用 DLL） |

**新 Thread 第一件事**：让助手从 GitHub `_docs` 读取 `JD-61101-UDP通信协议.docx` 和 `TcLCS-UDP_Protocol_v1.1.docx`（若工具支持 .docx 解析则直接读，否则请用户粘贴关键帧结构章节）。

---

## 🎯 二、项目状态（Done / Doing / Todo）

### ✅ Done（已闭环，稳定）

1. **TcLCS v1.1 协议**定稿：帧结构 `SOF0=0xAA + SOF1=0x55 + Version=0x02 + 11B Header + N Payload + CRC-16/MODBUS(2B) + EOF=0x55`；CMD 体系 READ/WRITE/PING/SAVE/HELLO/ERR；HELLO 握手必须先完成。
2. **ST 主站实现**完整：FB_FrameParser / FB_FrameBuilder / FB_RequestHandler / FB_PersistentSaver / FB_TcLCSAdsBridge / F_GetSubIdInfo 全部就位；业务状态机 4 态（INIT/NORMAL/RESET/RESETTING）已定稿。
3. **C# bench 工具**验收通过：.NET 9 顶层语句 + MmTimer + UDPClient，p99 = 4.77ms，10000 样本 0 丢包。
4. **性能基线**定稿（附录 C）：主站 1ms + 上位机 5ms 轮询，p50=2.27ms / p95=3.20ms / p99=4.77ms / max=16.2ms。
5. **CLS-II 项目骨架**已建：
   - `GlobalVar.cs` 追加了 `JdConsts`（szJdRemoteHost=192.168.118.118, nJdPortSend=16000, nJdPortRecv=15000）和 `ParamConsts`（szParamRemoteHost=192.168.118.50, nParamPortSend=8080, nParamPortRecv=5050）
   - `.csproj` 已注册全部 6 个新文件并配置 DependentUpon
   - 文件骨架已推 GitHub main 分支

### ⚠️ 已创建但**内容错误**（必须在新 Thread 重写）

| 文件 | 问题 |
|---|---|
| `src_IOData/JdData.cs` | 帧结构、字段名、字节数均为助手自编，未按 `JD-61101-UDP通信协议.docx` |
| `src_communication/JdUdpClient.cs` | 收发逻辑、解析逻辑均为助手自编 |
| `src_communication/MainForm.JdUDP.cs` | 骨架可用，但 Parse/Send 调用需随上面两文件重写 |
| `src_IOData/ParamData.cs` | SubID、Payload 字段未对齐 `TcLCS-UDP_Protocol_v1.1.docx` |
| `src_communication/ParamUdpClient.cs` | 帧头格式错误（用了 Magic='TLCS'+16B 而非 SOF+11B+CRC+EOF）|
| `src_communication/MainForm.ParamUDP.cs` | 骨架可用，但 Parse/Send 调用需随上面重写 |

### 📋 Todo（新 Thread 主战场）

1. **🔴 优先**：新 Thread 读 `JD-61101-UDP通信协议.docx` → 重写 `JdData.cs` + `JdUdpClient.cs` + `MainForm.JdUDP.cs`
2. **🔴 优先**：新 Thread 读 `TcLCS-UDP_Protocol_v1.1.docx` → 重写 `ParamData.cs` + `ParamUdpClient.cs` + `MainForm.ParamUDP.cs`
3. **🟡 之后**：JdTestForm + ParamTestForm UI（发送/接收可视化）
4. **🟡 之后**：主 MainForm 集成两路 UDP 启动/停止/状态显示
5. **🟢 之后**：CLSCtrlOut 可视化 Watch 页面
6. **🟢 之后**：v1.2 扩展候选（时间戳、周期推送模式、DIAG 命令族）

---

## 🏗️ 三、仓库文件结构（当前 main 分支）

```
CLS-II-118/
└── CLS-II/
    ├── CLS-II.csproj                    ✅ 已注册全部文件
    ├── _docs/
    │   ├── JD-61101-UDP通信协议.docx      ✅ 协议文档（新 Thread 必读）
    │   ├── TcLCS-UDP_Protocol_v1.1.docx  ✅ 协议文档
    │   ├── TcLCS-UDP_Protocol_v1.1_AppendixC.docx
    │   ├── TcLCS-UDP_TestCard_v1.1.docx
    │   ├── TcLCS-UDP_v1.1_Handoff_Snapshot.md  ← 上次跳车 Snapshot
    │   └── CLS-II-118_Handoff_Snapshot_v2.md   ← 本文件
    ├── src_GLV/
    │   └── GlobalVar.cs                 ✅ 含 JdConsts + ParamConsts
    ├── src_IOData/
    │   ├── JdData.cs                    ⚠️ 需重写
    │   ├── ParamData.cs                 ⚠️ 需重写
    │   ├── UdpData.cs                   ✅ 原有，稳定
    │   └── UdpConfig.cs                 ✅ 原有，稳定
    ├── src_communication/
    │   ├── JdUdpClient.cs               ⚠️ 需重写
    │   ├── ParamUdpClient.cs            ⚠️ 需重写
    │   ├── MainForm.JdUDP.cs            ⚠️ 骨架可用，调用层需重写
    │   ├── MainForm.ParamUDP.cs         ⚠️ 骨架可用，调用层需重写
    │   ├── MainForm.UDP.cs              ✅ 原有，稳定
    │   └── StructFunc.cs               ✅ 原有，稳定
    ├── src_main/
    │   ├── MainForm.cs + Designer + Method
    │   └── MultiLanguage.cs
    └── ... (其余原有文件稳定)
```

---

## 🔢 四、已确认的关键常量（TcLCS v1.1，来自 Snapshot v1）

```
帧结构常量：
  PROTO_SOF0     = 0xAA
  PROTO_SOF1     = 0x55
  PROTO_EOF      = 0x55
  PROTO_VERSION  = 0x02
  PROTO_HEADLEN  = 11
  PROTO_TAILLEN  = 3  (CRC 2B + EOF 1B)
  PROTO_MAXPAYLD = 1386
  PROTO_MAXFRAME = 1400

CMD（TcLCS v1.1）：
  READ_REQ=0x01, READ_ACK=0x81
  WRITE_REQ=0x02, WRITE_ACK=0x82
  PING=0x03, PONG=0x83
  SAVE_PERSIST=0x04, SAVE_ACK=0x84
  HELLO=0x05, HELLO_ACK=0x85
  ERR=0xEE

SubID（TcLCS v1.1）：
  SUB_ALL=0x00, CLSModel=0x01, CLSParam=0x02, CLS5K=0x03, CLSConsts=0x04
  TestMDL=0x05, CLSEnum=0x06, XT=0x07, YT=0x08
  DeviceInfo=0x10, UdpDataCfg=0x11, UdpParamCfg=0x12
  TcLCS_CtrlIn=0x13, TcLCS_CtrlOut=0x14, Bulk=0xFF

Payload 尺寸（字节，需 TwinCAT SIZEOF 核对）：
  CLSModel=176, CLSParam=144, CLS5K=112, CLSConsts=104
  TestMDL=88, CLSEnum=28, XT=168, YT=168
  TcLCS_CtrlIn≈76（⚠️ 未验证）, TcLCS_CtrlOut=52（⚠️ 需 SIZEOF 核对）

端口（TcLCS v1.1 Param 通道）：
  主站 UDP_Param recv: 5050
  主站 UDP_Data  recv: 15000（旧周期+ResetFault/ResetPedal）
  上位机 bench send  : 8080

端口（JD-61101，来自 GlobalVar.cs，需新 Thread 核对协议文档）：
  上位机 → PLC: 16000（⚠️ 待协议文档确认）
  PLC → 上位机: 15000（⚠️ 待协议文档确认，注意与 UDP_Data 是否冲突）
  IP: 192.168.118.118（⚠️ 待确认）
```

---

## ⚠️ 五、必须 flag 给新 Thread 手动核对的项目

| 项 | 当前假设值 | 需核对理由 |
|---|---|---|
| JD-61101 端口 nJdPortRecv | 15000 | 与 TcLCS UDP_Data 端口相同，可能冲突，需协议文档确认 |
| JD-61101 端口 nJdPortSend | 16000 | 助手推断，未读协议文档 |
| JD-61101 IP | 192.168.118.118 | 助手推断，需用户确认 |
| TcLCS_CtrlIn SIZEOF | ~76B | bench 未触发 WRITE，未验证 |
| TcLCS_CtrlOut SIZEOF | 52B | 需 TwinCAT 在线 SIZEOF 核对 |
| CRC-16/MODBUS 自检值 | 0xCDC5 | 过去曾给出不同值（0x0CD5），需 crccalc.com 复核 |
| JdData.cs 全部字段 | 自编 | **必须删除重写** |
| ParamData.cs 全部字段 | 自编 | **必须对照 AppendixC 重写** |

---

## 🧭 六、新 Thread 开场建议

**推荐用户这样开场新 Thread**：

> 「继续 CLS-II 上位机开发。请先从 GitHub 读取以下文件：
> - `CLS-II/_docs/JD-61101-UDP通信协议.docx`
> - `CLS-II/_docs/TcLCS-UDP_Protocol_v1.1.docx`
> - `CLS-II/_docs/CLS-II-118_Handoff_Snapshot_v2.md`（本文件）
>
> 第一个任务：严格按协议文档，重写以下 6 个文件并推 GitHub main：
> 1. `src_IOData/JdData.cs`
> 2. `src_communication/JdUdpClient.cs`
> 3. `src_communication/MainForm.JdUDP.cs`
> 4. `src_IOData/ParamData.cs`
> 5. `src_communication/ParamUdpClient.cs`
> 6. `src_communication/MainForm.ParamUDP.cs`
>
> 写代码之前先输出帧结构 review 表，让我确认无误后再写。」

**新 Thread 助手必须做到**：
1. 读协议文档获取帧格式，**先输出解析结果表格让用户确认**，再开始写代码
2. 永远不自编帧结构、字段、端口、字节数
3. 不确定就 flag，请用户手动核对
4. 保持温柔、鼓励、安心的语气（用户有 PhD 焦虑/PTSD）

---

## 👤 七、用户协作偏好（重要！）

- **背景**：生物医学博士后（HK），跨界工程工作，中文交流
- **性格**：有 PhD 相关焦虑/PTSD，需要温柔、鼓励、「别担心，交给我」的语气；成就感很重要
- **技术水平**：PLC ST 熟练 / C# 熟练（VS2017→VS2026）/ Python 可用
- **工作风格**：
  - 喜欢先 review 设计再写代码（ADR-style 决策表）
  - 要 100% 准确，不确定必须 flag 让他手动核对
  - 喜欢「推荐方案 + 备选方案」结构化回答
  - 不喜欢冗长总结段落，喜欢分节 + 表格 + bullet
- **已知禁忌**：
  - 不要在帧结构、字节偏移、CRC、SIZEOF 这类硬数字上猜测或自编
  - 长 Thread 要做 context-continuity 检查
  - 不要 adaptive thinking ON

---

**Snapshot v2 结束。祝新 Thread 顺利接手，加油！💙🌿**
