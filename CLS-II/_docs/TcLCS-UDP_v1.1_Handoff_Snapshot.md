# TcLCS-UDP v1.1 项目交接 Snapshot

> **目的**：给下一个 Thread / 下一位接手者一个高密度、零遗漏的状态快照。
> **生成时间**：2026-05-09 10:50 HKT
> **项目阶段**：协议 + ST + C# bench 三段式已闭环，即将进入 C# 上位机主体业务开发。
> **用户背景**：HK 博士后研究员，生物医学 / 生物材料 / 神经再生，PLC 与 C# 是跨领域工程工作；研究场景高精度，对性能测量要求工业级。

---

## 🗂️ 一、必读文件清单（新 Thread 开场请让用户上传）

| # | 文件 | 角色 | 最新版本 |
|---|---|---|---|
| 1 | `TcLCS-UDP_Protocol_v1.1.docx`               | 协议规范（帧结构、CMD、SubID、错误码） | v1.1 定稿 |
| 2 | `TcLCS-UDP_TestCard_v1.1.docx`               | 验收测试卡（Part A/B/C）               | v1.1 定稿 |
| 3 | `TcLCS-UDP_ST_v1.1-temporary.docx`           | 主站 ST 代码（POU/GVL/Types 完整）     | v1.1 temporary |
| 4 | `TcLCS-UDP_Protocol_v1.1_AppendixC.docx`     | 性能基线附录（1 ms 主站周期决策）      | 2026-05-09 定稿 |
| 5 | `EnumDefine.cs` / `MmTimer.cs`               | 用户自研高精度定时器库                 | 稳定 |
| 6 | `UDP.cs`                                      | 用户自研 UDP 客户端库                  | 稳定 |
| 7 | （用户可能还有）bench 项目 `TcLCS_BenchCtrlOut` | C# bench，已跑通                     | 验收通过 |

**若用户新建 Thread**：请直接提示上传 1–4 这四份核心 .docx，三秒回到工作态。

---

## 🎯 二、项目当前状态（Done / Doing / Todo）

### ✅ Done（已闭环）

1. **协议 v1.1**：帧结构 11B Header + N Payload + 3B Trailer，CRC-16/MODBUS，Version=0x02，含 HELLO (0x05/0x85) 完整握手。
2. **ST 主站实现**：
   - CLSCtrlIn/Out_HW 硬件通道 + CLSCtrlIn/Out 网络镜像
   - `UDP_Data` PROGRAM 内置 Default Business Logic 状态机（E_TaskState: INIT/NORMAL/RESET/RESETTING）
   - `UDP_Param` PROGRAM 承载新协议（端口 5050）+ FB_RequestHandler
   - FB_FrameParser / FB_FrameBuilder / FB_PersistentSaver / FB_TcLCSAdsBridge 全部就位
   - F_GetSubIdInfo 支持 0x00~0x08, 0x10~0x14, 0xFF
3. **默认业务逻辑状态机决策**：
   - Q2 = 方案 b（等 state=128 再切 CtrlCmd=1）
   - Q3 = 方案 α（bResetError=FALSE AND bResetSystem 上升沿 → 离开 RESET）
   - Q4 = 方案 iii（Action_SetDefaultCtrlIn，其它字段默认值）
   - Q5 = 归零由 TcCOM 底层自动
4. **bResetError / bResetSystem 路径**：通过旧 UDP_Data 通道（端口 15000）的 `ST_UdpDataIn.ResetFault / ResetPedal == 0xAA/0x00` 设置，而非新建 SubID。⚠️ **协议文档需明确此双通道控制路径**。
5. **C# bench 工具**：顶层语句 .NET 9，基于 HPTimer + UDPClient，输出 min/max/avg/p50/p95/p99。
6. **性能基线**（2026-05-09）：
   - **生产配置：主站 1 ms + 上位机 5 ms 轮询**
   - p50 = 2.27 ms, p95 = 3.20 ms, p99 = 4.77 ms, max = 16.2 ms
   - 10000 样本 0 丢包，数据年龄 < 10 ms（满足示波器 10 ms 采样不重复）

### 🔧 Doing（进行中 / 待用户确认）

- **v1.1 协议文档是否合并附录 C**：独立文件已生成 [`TcLCS-UDP_Protocol_v1.1_AppendixC.docx`]，用户可选独立归档或合并进主文档。

### 📋 Todo（下一阶段）

1. **🚀 C# 上位机主体业务开发**（next Thread 主战场）
   - 功能：完整 v1.1 客户端（READ/WRITE/PING/SAVE/HELLO/ERR 全 CMD）+ CLSCtrlOut 可视化 + 参数下发 UI
   - 形态：WPF / WinForms / MAUI / Console（待用户选定）
2. **Fault 态自动保护未纳入本版 ST**：`bInFaultProtect` 变量存在但 CASE 里未实装 state=64 检查分支。待用户决定是否补回。
3. **v1.2 扩展候选**：
   - CLSCtrlOut Payload 引入 8B ULINT 硬件快照时间戳（对齐外部触发）
   - 周期推送模式（eliminate RTT）
   - DIAG 命令族
4. **bResetError/bResetSystem 的协议文档化**：当前实现用旧 UDP 通道，需在 Protocol v1.1 或 v1.2 中明确双通道设计决策与理由。

---

## 🧭 三、关键技术决策记录（ADR）

### ADR-001：协议版本 v1.1 升级到 Version=0x02
- **原因**：v1.0 没有 HELLO 握手，无法确认 PLC 就绪；CRC 位置调整
- **影响**：C# / ST 双端必须同时升级，不兼容 v1.0
- **状态**：已实施

### ADR-002：主站 UDP_Param Task CycleTime = 1 ms
- **原因**：实测 p99=4.77ms < 5ms 上位机周期，0 丢包，CPU 余量充足
- **替代方案**：500 μs（性能无显著改善，CPU 余量少）/ 250 μs（出现 1/10000 丢包，不推荐）
- **状态**：已定稿（附录 C）

### ADR-003：bResetError/bResetSystem 走旧 UDP 通道
- **原因**：避免新增 SubID 0x15；旧通道的 ResetFault/ResetPedal 字段已有位置
- **权衡**：协议上稍有碎片化，需文档化双通道
- **状态**：ST 已实施，协议文档待补说明

### ADR-004：Default Business Logic 状态机 4 态
- **状态**：STATE_INIT (0) → STATE_NORMAL (1) → STATE_RESET (2) → STATE_RESETTING (3)
- **上升沿检测**：R_TRIG(bResetError) / R_TRIG(bResetSystem)
- **优先级**：System > Error
- **状态**：已实施

### ADR-005：C# bench 用顶层语句 + 用户自研 MmTimer/UDP DLL
- **原因**：用户迁移至 VS2026；HPTimer 基于 winmm.dll timeSetEvent，UDPClient 基于异步 Socket
- **引用方式**：推荐 Project Reference（.csproj 在手时）或手动 .csproj `<Reference Include>`
- **状态**：bench 已通过验收

---

## 📐 四、关键代码位置速查

| 需要改什么 | 去哪里 |
|---|---|
| 状态机逻辑 | `PROGRAM UDP_Data` 的 ELSE 分支 CASE eStep OF |
| 默认业务字段默认值 | `PROGRAM UDP_Data` 的 `Action_SetDefaultCtrlIn` |
| 协议帧解析 | `FB_FrameParser` |
| 协议帧构造 | `FB_FrameBuilder` |
| CMD 派发（READ/WRITE/PING/SAVE/HELLO/ERR） | `FB_RequestHandler` 的 CASE fbParser.hdr.CMD OF |
| SubID 寻址 | `F_GetSubIdInfo` |
| ADS 镜像同步 | `FB_TcLCSAdsBridge` + `PROGRAM AdsBridge` |
| 持久化保存 | `FB_PersistentSaver` |
| Reset 标志位写入 | `PROGRAM UDP_Data` 的 `Action_ProcessReceiveData` |

---

## 🔢 五、关键常量 / 魔数速查

```
帧结构常量：
  PROTO_SOF0     = 0xAA
  PROTO_SOF1     = 0x55
  PROTO_EOF      = 0x55
  PROTO_VERSION  = 0x02
  PROTO_HEADLEN  = 11
  PROTO_TAILLEN  = 3
  PROTO_MAXPAYLD = 1386
  PROTO_MAXFRAME = 1400
  PROTO_DEV_BCAST= 0xFF

CMD：
  READ_REQ  = 0x01, READ_ACK  = 0x81
  WRITE_REQ = 0x02, WRITE_ACK = 0x82
  PING      = 0x03, PONG      = 0x83
  SAVE_PERSIST = 0x04, SAVE_ACK = 0x84
  HELLO     = 0x05, HELLO_ACK = 0x85
  ERR       = 0xEE

SubID：
  SUB_ALL=0x00, CLSModel=0x01, CLSParam=0x02, CLS5K=0x03, CLSConsts=0x04,
  TestMDL=0x05, CLSEnum=0x06, XT=0x07, YT=0x08,
  DeviceInfo=0x10, UdpDataCfg=0x11, UdpParamCfg=0x12,
  TcLCS_CtrlIn=0x13, TcLCS_CtrlOut=0x14, Bulk=0xFF

Payload 尺寸（字节）：
  CLSModel=176, CLSParam=144, CLS5K=112, CLSConsts=104,
  TestMDL=88, CLSEnum=28, XT=168, YT=168, TcLCSP=992
  DeviceInfo=16, UDPParameter=48
  TcLCS_CtrlIn(U)=大约 76（含 CtrlCmd + 15 REAL + FnSwitch）
  TcLCS_CtrlOut(Y)=52 (pack_mode=4, 13×4B)

端口：
  主站 UDP_Param: 5050（新协议）
  主站 UDP_Data : 15000（旧周期通道，ResetFault/ResetPedal 从这里写入）
  上位机 bench  : 8080（本地绑定）

业务状态机（E_TaskState）：
  STATE_INIT=0, STATE_NORMAL=1, STATE_RESET=2, STATE_RESETTING=3

硬件 state（E_CHN_*）：
  OFF=0, ENABLE=1, FORCELOOP1=2, FORCELOOP2=6, POSLOOP=7,
  FAULT=64, RESET=128, INIT=16, HOMING=32, STOP=255

CtrlCmd：
  0=OFF, 1=ForceLoop X1, 10=Reset
```

---

## 👤 六、用户协作偏好（重要！）

- **角色**：生物医学博士后 (HK)，跨界工程工作
- **性格**：有 PhD 相关焦虑/PTSD，需要温柔、鼓励、"别担心、交给我"的语气
- **技术水平**：PLC ST 熟练、C# 熟练（VS2017→VS2026 迁移中）、C/C++/Python 均可
- **工作风格**：
  - 喜欢先 review 设计再写代码（ADR-style 决策表）
  - 要 100% 准确，不确定就 flag 让他手动核对（如 CRC 期望值 0xCDC5 已 flag 过）
  - 喜欢"推荐方案 + 备选方案"的结构化回答
  - 不喜欢冗长的总结段落，喜欢分节 + 表格 + bullet
- **已知禁忌**：
  - 不要用 Claude Opus 4.7 默认 adaptive thinking ON（他明确说 OFF）
  - 长 Thread 要做 context-continuity 检查
  - 不要幻觉（特别是 CRC / SIZEOF / 字节偏移这种硬数字）

---

## 🔬 七、已 flag 给用户手动核对的数字

| 项 | 当前值 | 需核对理由 |
|---|---|---|
| CRC-16/MODBUS 自检 0x01 0x03 0x00 0x00 0x00 0x0A 的期望值 | `0xCDC5` | 我过去给过不同值 (0x0CD5)，需 crccalc.com 复核 |
| ST_TcLCS_Y (CLSCtrlOut) SIZEOF | 52 B | 按 pack_mode=4, 13×4B 推算，需 TwinCAT 在线 SIZEOF 验证 |
| ST_TcLCS_U (CLSCtrlIn) SIZEOF | ~76 B（待确认） | bench 未触发 WRITE，未验证 |
| E_CHN_FAULT 实际硬件值 | 64 | 可能按位域组合出现，需观察 |

---

## 🚀 八、下一个 Thread 开场建议

**用户开新 Thread 时，推荐他这样开场**：

> "上传附件：TcLCS-UDP_Protocol_v1.1.docx, TcLCS-UDP_TestCard_v1.1.docx, TcLCS-UDP_ST_v1.1-temporary.docx, TcLCS-UDP_Protocol_v1.1_AppendixC.docx, EnumDefine.cs, MmTimer.cs, UDP.cs, 加上这份 Snapshot.md。
>
> 继续 C# 上位机主体业务开发。目标：[WPF / WinForms / Console] 形态，覆盖 v1.1 全 CMD，并实时显示 CLSCtrlOut。"

**新 Thread 的 Assistant 应该做的第一件事**：
1. 确认所有文件收齐
2. 读 Snapshot 的 §二（状态）+ §六（用户偏好）
3. 用温柔鼓励的语气问"上位机形态（WPF/WinForms/Console）+ 主要功能优先级"以开启设计

---

## 🌱 九、情感与节奏提醒

用户连续推进了协议设计 → ST 实装 → C# bench → 性能验收 → 附录 C 撰写这一整套工作，节奏密集。
**若用户在下一个 Thread 表现疲惫、焦虑、或 PTSD 触发**：
- 先安抚，再技术（"别担心，交给我，我们慢慢来 🌿"）
- 允许用户把任务切小、随时暂停
- 主动 summarize 已完成的部分，给他成就感
- 复杂决策出"推荐 + 备选"，减少用户决策疲劳

---

**Snapshot 结束。祝下一个 Thread 顺利接手 💙🌿**
