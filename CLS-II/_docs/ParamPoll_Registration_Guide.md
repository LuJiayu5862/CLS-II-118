# ParamPoll 轮询与差分写注册指南

> 适用文件：`CLS-II/src_communication/MainForm.ParamPoll.cs`、`CLS-II/src_IOData/ParamData.cs`
> 最后更新：2026-05-13

---

## 一、架构概览

```
mmTimer1 (winmm 硬实时, 10ms tick)
  ├─ PeriodWrite  周期写：每次 tick 都发 WRITE_REQ（如 CtrlIn）
  ├─ DiffWrite    差分写：源变量 ≠ 快照 → 入写队列
  └─ ReadOnly     只读高频：fire-and-forget 异步读

SoftPollLoopAsync (Task.Delay 软定时, 100ms tick)
  └─ ReadOnly 低频：DeviceInfo / UdpDataCfg / UdpParamCfg / ALL

WriteQueueLoopAsync (后台 Task，串行消费)
  └─ 按 Priority 升序发 WRITE_REQ，写成功 → 更新快照
     写失败/超时 → 快照不更新 → 下次差分检测自动重试
```

**双缓冲机制：**
- **源变量** `ParamData.XXX`：UI / 业务代码直接读写
- **快照** `ParamData.Snap.XXX`：上次已成功发送的值
- mmTimer1 每 10ms 对比源变量与快照，有差异才入写队列（差分写）

---

## 二、`_pollTable` 字段说明

```csharp
new PollEntry
{
    Sub       = TcSubId.XXX,          // SubID，唯一标识
    PeriodMs  = 10,                   // 检测/发送周期（ms）
    Mode      = PollMode.DiffWrite,   // 见下表
    Timer     = TimerType.HiRes,      // 见下表
    Priority  = 1,                    // 写队列优先级：0=最高，99=最低（只读填99无效）
}
```

| `Mode` | 说明 |
|---|---|
| `PeriodWrite` | 每次 tick 无条件发 WRITE_REQ（适用于 CtrlIn 等实时控制量） |
| `DiffWrite`   | 源变量 ≠ 快照时才发（适用于参数类写变量） |
| `ReadOnly`    | 定期发 READ_REQ，读回后覆盖源变量并同步快照（只读，不可写） |

| `Timer` | 说明 |
|---|---|
| `HiRes` | 挂 mmTimer1（winmm 硬实时，10ms 精度，适合高频） |
| `Soft`  | 挂 SoftPollLoopAsync（Task.Delay，100ms 精度，适合低频只读） |

---

## 三、新增变量完整步骤

以新增 `SubID=0x20`，差分写变量 `ST_NewParam` 为例，共 **4 个文件，4 步**。

### Step 1 — `ParamData.cs`：定义结构体 + 源变量 + Lock + Snap

```csharp
// 1. 结构体（严格对应 PLC DUT，Pack 与 TwinCAT 保持一致）
[StructLayout(LayoutKind.Sequential, Pack = 8)]
public struct ST_NewParam
{
    public double Value1;
    public double Value2;
}

// 2. 源变量 + Lock（加到 ParamData 类中）
public static ST_NewParam New_Param = new ST_NewParam();
public static readonly object LockNewParam = new object();

// 3. 快照（加到 ParamData.Snap 嵌套类中）
public static class Snap
{
    // ... 已有字段 ...
    public static ST_NewParam New_Param = new ST_NewParam();  // ← 加这行
}
```

### Step 2 — `ParamData.cs` 的 `TcSubId` 枚举：加 SubID

```csharp
public enum TcSubId : byte
{
    // ... 已有 ...
    NewParam = 0x20,  // ← 加这行
}
```

同步更新 `TcSubIdSize.Get()`：

```csharp
TcSubId.NewParam => 16,  // ← 实际字节数（SIZEOF）
```

### Step 3 — `ParamData.cs` 的 `TryDeserialize`：加 case

```csharp
case TcSubId.NewParam:
    lock (LockNewParam)
        New_Param = (ST_NewParam)Struct_Func.BytesToStruct(frame.Payload, New_Param);
    return true;
```

### Step 4 — `MainForm.ParamPoll.cs`：3 处 switch 各加 1 行

```csharp
// ① HasChanged()
case TcSubId.NewParam:
    return !StructBytesEqual(ParamData.New_Param, ParamData.Snap.New_Param);

// ② EnqueueWrite()
case TcSubId.NewParam:
    payload = Struct_Func.StructToBytes(ParamData.New_Param); break;

// ③ UpdateSnap()
case TcSubId.NewParam:
    ParamData.Snap.New_Param = (ST_NewParam)Struct_Func.BytesToStruct(payload, new ST_NewParam()); break;
```

同步更新 `SyncAllDiffSnaps()`（防止读回后快照与源产生虚假差异触发误写）：

```csharp
private static void SyncAllDiffSnaps()
{
    // ... 已有字段 ...
    ParamData.Snap.New_Param = ParamData.New_Param;  // ← 加这行
}
```

### Step 5 — `_pollTable` 加一行（完成！）

```csharp
new PollEntry { Sub=TcSubId.NewParam, PeriodMs=10, Mode=PollMode.DiffWrite, Timer=TimerType.HiRes, Priority=1 },
```

---

## 四、只读变量注册步骤（更简单）

只读变量**不需要** Snap、`HasChanged`、`EnqueueWrite`、`UpdateSnap`，只需 3 步：

**Step 1** — `ParamData.cs`：定义结构体 + 源变量 + Lock（同上 Step 1，无需 Snap）

**Step 2** — `TryDeserialize` 加 case（同上 Step 3）

**Step 3** — `_pollTable` 加一行：

```csharp
// 高频只读（挂 mmTimer1）
new PollEntry { Sub=TcSubId.NewReadOnly, PeriodMs=10,   Mode=PollMode.ReadOnly, Timer=TimerType.HiRes, Priority=99 },

// 低频只读（挂软定时）
new PollEntry { Sub=TcSubId.NewReadOnly, PeriodMs=2000, Mode=PollMode.ReadOnly, Timer=TimerType.Soft,  Priority=99 },
```

---

## 五、ALL 帧偏移表（重要！）

ALL（SubID=0x00）帧 Payload = **992B**，偏移由 PLC 端 `ST_TcLCS_P` 决定：

| 字段 | 起始偏移 | 大小 | 备注 |
|---|---|---|---|
| CLSModel  | 0   | 176 | |
| CLSParam  | 176 | 144 | |
| CLS5K     | 320 | 112 | |
| CLSConsts | 432 | 104 | |
| TestMDL   | 536 |  88 | |
| CLSEnum   | 624 |  28 | |
| _pad0     | 652 |   4 | PLC 端显式填充，`off += 32`（28+4） |
| XT        | 656 | 168 | |
| YT        | 824 | 168 | |
| **合计**  |     | **992** | |

> ⚠️ `TryDeserialize` 的 `case ALL` 中，CLSEnum 块必须 `off += 32`（不是 28），  
> 否则 XT/YT 偏移错位 4B，导致数组数值移位乱码。

---

## 六、修改清单速查

| 要做的事 | 需改文件 |
|---|---|
| 新增差分写变量 | `ParamData.cs`（4处）+ `MainForm.ParamPoll.cs`（4处）|
| 新增只读变量 | `ParamData.cs`（3处）+ `MainForm.ParamPoll.cs`（_pollTable 1行）|
| 修改轮询频率/优先级 | `_pollTable` 对应行 |
| 修改定时器类型（HiRes↔Soft）| `_pollTable` 对应行的 `Timer` 字段 |
