# CLS-II-118 开发交接快照 v6

> **生成时间**：2026-05-14
> **编写者**：AI Assistant（基于代码库完整读取 + 本轮完整对话）
> **接续自**：`CLS-II-118_Handoff_Snapshot_v5.md`
> **里程碑**：✅ ParamPoll 写队列架构升级完成；✅ Watch 优化全部落地；✅ JD收发框架已建立待验证

---

## 一、本轮完成内容（v5 → v6）

### 1.1 写队列架构升级（MainForm.ParamPoll.cs + MainForm.cs）

**原架构问题**：`WriteQueueLoopAsync` 使用 `Task.Delay(1ms)` 消费写队列，Windows 实际精度 0～15ms，无法保障 CtrlIn 5ms 周期实时性。

**新架构**：写队列消费由 `mmTimer2`（winmm 硬实时，1ms）驱动，彻底解决精度问题。

#### 新增内容：

| 组件 | 说明 | 状态 |
|---|---|---|
| `PollEntry.FireAndForget` | 新字段，标记周期写是否忽略 ACK | ✅ |
| `WriteJob.FireAndForget` | 对应写队列条目字段 | ✅ |
| `mmTimer2` 启动（1ms） | `Form1_Load` 中启动，`mmTimer2_Ticked` 调用 `ConsumeWriteQueueOnce` | ✅ |
| `ConsumeWriteQueueOnce()` | 替代 `WriteQueueLoopAsync`，由 mmTimer2 每 1ms 调用 | ✅ |
| FireAndForget 分流 | `FireAndForget=true` → `SendOnlyAsync`；`false` → `_serialWriteQueue` 串行 | ✅ |
| `_serialWriteQueue` | DiffWrite 专用串行队列（`ConcurrentQueue<WriteJob>`） | ✅ |
| `_serialBusy` | 原子锁，保证同时只有 1 个 DiffWrite 在途 | ✅ |
| `DrainSerialQueue()` | 串行队列驱动器，发完自动触发下一个 | ✅ |
| `SendSerialJobAsync()` | 串行发送：等 ACK → UpdateSnap；超时不更新（自动重试） | ✅ |
| `SendWriteJobAsync()` | FireAndForget 分支调用 `SendOnlyAsync`，普通分支调用 `WriteAsync` | ✅ |
| 移除 `WriteQueueLoopAsync` | 及对应 `_writeTask`、`_writeTask?.Wait()` | ✅ |

**CtrlIn _pollTable 条目**：
```csharp
new PollEntry {
    Sub=TcSubId.TcLCS_CtrlIn, PeriodMs=5,
    Mode=PollMode.PeriodWrite, Timer=TimerType.HiRes,
    Priority=0, FireAndForget=true
}
```

#### 最终发包路径：

```
mmTimer1 (10ms)
  → OnHiResTick()
      → PeriodWrite(CtrlIn): EnqueueWrite(FireAndForget=true)
      → DiffWrite: EnqueueWrite(FireAndForget=false)
      → ReadOnly: PollReadOnceAsync() fire-and-forget

mmTimer2 (1ms, winmm)
  → ConsumeWriteQueueOnce()
      → FireAndForget=true  → SendOnlyAsync (不等ACK)
      → FireAndForget=false → _serialWriteQueue → DrainSerialQueue
          → SendSerialJobAsync → WriteAsync (等ACK) → UpdateSnap

SoftPollLoop (Task, 100ms tick)
  → DiffWrite 软定时参数检测
  → ReadOnly 软定时只读
```

---

### 1.2 ParamUdpClient.cs — 新增 SendOnlyAsync

```csharp
/// <summary>
/// 只发不等：周期写控制量（CtrlIn）专用。
/// 不注册 _pending，不等 ACK，SeqNo 正常递增。
/// </summary>
public async Task SendOnlyAsync(TcSubId sub, ReadOnlyMemory<byte> payload)
{
    if (_udp == null) return;
    ushort seq = NextSeq();
    byte[] frame = TcCodec.Build(_deviceId, TcCmd.WRITE_REQ, sub, seq, payload.Span);
    await _udp.SendAsync(frame, frame.Length, _server).ConfigureAwait(false);
}
```

**SeqNo 安全性确认**：`NextSeq()` 是 `Interlocked.Increment` 原子递增，fire-and-forget 不影响其他请求的 SeqNo 匹配。ACK 回来后 `RxLoopAsync` 的 `TryRemove` 找不到对应 TCS 会走 `OnUnsolicited`，不泄漏字典。

---

### 1.3 MainForm.cs — Restart Bug 修复

**问题**：`restartToolStripMenuItem_Click` 调用 `MainForm_FormClosing` 后直接返回，新进程已启动但旧进程未关闭，导致出现副本。

**修复**：
```csharp
private void restartToolStripMenuItem_Click(object sender, EventArgs e)
{
    MainForm_FormClosing(sender, null);
    if (isClosing)
        Application.Exit();   // ★ 关闭旧进程
}
```

---

### 1.4 Watch.Method.cs — updateScopeDataOnce O(n²) 优化

**原问题**：双层循环，O(N×M) 字符串比较，N=ScopeVarieties数，M=records数。

**修复**：新增 `_scopeRecordIndex`（`Dictionary<string, int>`），在 `updateScopeListOnce()` 重建时同步构建索引，`updateScopeDataOnce()` 改为 O(N) 字典查找。

```csharp
// 新增成员
private Dictionary<string, int> _scopeRecordIndex = new Dictionary<string, int>();

// updateScopeListOnce() 末尾重建索引
_scopeRecordIndex.Clear();
lock (this.records)
    for (int j = 0; j < this.records.Count; j++)
        _scopeRecordIndex[this.records[j].Name] = j;

// updateScopeDataOnce() 内层循环替换为
if (!_scopeRecordIndex.TryGetValue(WatchConfig.ScopeVarieties[i].VarName, out int j))
    continue;
```

---

### 1.5 v5 遗留任务全部完成

| v5 遗留任务 | 状态 |
|---|---|
| Watch 反射缓存（`GetCachedField`） | ✅ v5已完成 |
| Watch 写入语义定案（方案A） | ✅ v5已完成 |
| STRING(N) 全链路支持 | ✅ v5已完成 |
| ALL 帧 off+=32 确认 | ✅ 已确认 |
| 延迟测试 RTT 测量（P99=4.7ms） | ✅ 已验证 |
| updateScopeDataOnce O(n²) 优化 | ✅ 本轮完成 |

---

## 二、当前未闭环问题

### 2.1 ⚠️ JD 收发验证（核心待办）

`JdUdpClient.cs` 框架已建立，结构如下：[cite:29]

| 组件 | 说明 | 状态 |
|---|---|---|
| `JdUdpClient`（单例） | 生命周期由 `ConnectDevice/DisconnectDevice` 管理 | ✅ 框架完成 |
| `SendTx()` | 从 `JdData.JdTx` 读取，调用 `JdCodec.BuildTx` 序列化发送 | ✅ 框架完成 |
| `Udp_OnReceived` | 解析后写入 `JdData.JdRx`（唯一真相源） | ✅ 框架完成 |
| `SendClearFault()` | 清除故障码脉冲发送 | ✅ 框架完成 |
| `SendResetPedal()` | 踏板复位脉冲发送 | ✅ 框架完成 |
| **实际联调验证** | PLC 端收发是否正确、JdRx 数据是否解析正确 | ⚠️ **待验证** |

**下一 thread 需要做的事**：
1. 阅读 `JdCodec.cs`（如存在）和 `JdData.cs`，确认协议帧格式与 `JD-61101-UDP通信协议.docx` 吻合
2. 确认 `mmTimer1_Ticked` 中 `JdUdpClient.Instance?.SendTx()` 是否已启用（当前代码中已注释掉）
3. 联调验证：`JdData.JdRx.PedalPosition` 能否正确读到踏板位置
4. 确认 `JdData.JdTx` 写入路径是否与上位机控制逻辑对接

---

## 三、协议约束速查（TcLCS-UDP v1.1）

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
| 超时策略 | 100ms × 1次，连续3次失败重发HELLO |
| CtrlIn 周期 | 5ms（mmTimer1，FireAndForget=true） |
| DiffWrite 检测周期 | 100ms（SoftPollLoop） |
| 写队列消费周期 | 1ms（mmTimer2，winmm） |
| RTT P99（实测） | 4.7ms |

---

## 四、SubID 尺寸速查表

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

## 五、ALL 帧偏移表（含 PLC 端 _pad0，关键！）

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

## 六、Watch 写入语义（定案，勿改）

| 变量 | 语义 |
|---|---|
| `ParamData.XXX` | 实时值（PLC 读回 / UI 修改），Watch 直接写这里 |
| `ParamData.Snap.XXX` | 上次已成功发送至 PLC 的值，仅由 `UpdateSnap()` 更新 |

- `HasChanged()` 比较 `ParamData.XXX` vs `ParamData.Snap.XXX`
- `EnqueueWrite()` 序列化 `ParamData.XXX` 发送
- `UpdateSnap()` 写成功后才更新 Snap
- **Watch 只写 `ParamData.XXX`（方案A），Snap 不是编辑缓冲区**

---

## 七、下一个 Thread 任务清单

### P0 — JD 收发验证

1. 阅读 `JdCodec.cs`、`JdData.cs`，核对协议帧格式
2. 确认 `mmTimer1_Ticked` 中 `SendTx()` 调用是否已启用
3. 联调验证 JD 收发是否正常
4. 确认 `JdData.JdTx` 写入路径与控制逻辑对接

### P1 — 视需求

5. Scope 示波器功能联调（`ScopeView_YT`、`ScopeView_XY`）
6. 项目文件 `.xrp` 读写逻辑完善

---

## 八、下一个 Thread 开场白

```
请先做上下文连续性检查。

项目：LuJiayu5862/CLS-II-118

请先阅读以下文件：
1. CLS-II/_docs/CLS-II-118_Handoff_Snapshot_v6.md（本文件，必读）
2. CLS-II/src_communication/JdUdpClient.cs
3. CLS-II/src_communication/MainForm.ParamPoll.cs
4. CLS-II/src_main/MainForm.cs
5. JdCodec.cs 和 JdData.cs（路径请先 list 目录确认）

本次目标：
验证 JD-61101 UDP 收发是否正确实现，包括：
A. JdCodec 协议帧格式与 JD-61101-UDP通信协议.docx 是否吻合
B. mmTimer1_Ticked 中 SendTx() 是否已启用
C. JdData.JdRx 接收数据路径是否完整
D. 如有问题，给出最小修复方案

注意：
- ParamPoll 架构（mmTimer2 消费写队列）已完成，不要改动
- Watch 写入语义已定案（方案A，写源变量），不要改动
- CtrlIn FireAndForget=true，走 SendOnlyAsync，不要改动
```

---

*本快照由 AI Assistant 于 2026-05-14 基于本轮完整对话生成。*
