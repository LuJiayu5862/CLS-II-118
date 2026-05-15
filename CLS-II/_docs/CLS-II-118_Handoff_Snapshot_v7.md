# CLS-II-118 Handoff Snapshot v7
**Date**: 2026-05-15
**Repo**: LuJiayu5862/CLS-II-118
**Branch**: main

---

## 一、本轮已完成事项（Threading v6 → v7）

### 1. JD-61101 UDP 通道接入 Watch（解耦 Param，独立 Jd 类别）

**文件**: `AdsVariableSample.cs`
- 新增独立方法 `SetJdTreeNode()`，构造函数末尾调用
- `JdTxFrame`（可写）和 `JdRxFrame`（只读）均通过 `GetProperties` 反射枚举
- Category = `"Jd"`，Port = `"JdTx"` / `"JdRx"`，不侵入 Param 任何代码

**文件**: `Watch.Method.cs`
- 新增 `updateJdDataOnce()`：独立读取 JdTx/JdRx，不混入 `updateParamDataOnce()`
- 新增 `GetJdFrame(string subName)`：lock(JdData.JdTx/JdRx) 后返回引用
- 新增 `TryWriteJdValue()`：JdRx 只读（Port != "JdTx" → return false），JdTx 反射写 Property，class 引用类型无需装箱
- `mmTimer1_Ticked` 追加 `updateJdDataOnce()` 调用

**文件**: `Watch.cs`
- `writeAllToolStripButton1_Click` 追加 `else if (category == "Jd")` 分支
- `dataGridView1_CellValidating` 追加 Jd 验证分支（复用 `matchUDPValue`）

**文件**: `MainForm.cs`
- `mmTimer1_Ticked` 的 `if (GlobalVar.isUdpConnceted)` 块内追加 `JdUdpClient.Instance?.SendTx();`
- ⚠️ 旧注释段（`udpClient.Send` / `isSendUdp`）保持注释，不解注释

---

### 2. Watch 进制显示切换（DEC/HEX/OCT/BIN，固定位宽）

**文件**: `Watch.Method.cs`（全部新增/修改，不涉及其他文件）

#### 新增方法
- `FormatByMode(object raw)`：统一格式化入口
  - 整数类型按 `MainConfig.ConfigInfo.DebugItems.WatchMode` 格式化
  - 浮点/Bool/String/其他 → 始终 `Convert.ToString`
  - 有符号负数按二进制补码显示（`sbyte/short/int/long` 转 `ulong` 再格式化）
  - 固定位宽：1B=2位HEX/3位OCT/8位BIN，2B=4/6/16，4B=8/11/32，8B=16/22/64
- `ToOctalUInt64(ulong v)`：ulong→八进制字符串（避免 long 溢出）
- `ToBinaryUInt64(ulong v)`：ulong→二进制字符串
- `IsIntegerType(Type t)`：判断是否整数类型
- `TryParseWithPrefix(string s, out ulong result, out bool prefixed)`：识别 `0x/0b/0o` 前缀解析
- `IsIntegerTypeName(string typeName)`：字符串类型名判断
- `HasIntPrefix(string s)`：前缀检测
- `TryParseIntegerWithPrefix(string typeName, string s)`：带范围校验的前缀整数验证

#### 修改方法
- `updateParamDataOnce()`：2处 `Convert.ToString(raw)` → `FormatByMode(raw)`（先比较旧值，再赋值）
- `updateJdDataOnce()`：1处 `Convert.ToString(...)` → `FormatByMode(...)`
- `ConvertStringToTargetType()`：整体替换，支持 `0x/0b/0o` 前缀自动识别，无前缀走原有十进制
- `matchUDPValue()`：头部插入整数前缀短路判断（`IsIntegerTypeName + HasIntPrefix → TryParseIntegerWithPrefix`）

**设计决策（已定案）**：
- 写入端：自动识别前缀，不绑定当前显示模式
- 模式切换后下一个 `timer1_Tick`（≤100ms）立即生效，无需手动触发
- `displayWatchMode_Click`（MainForm.cs）不改动，已写 `WatchMode`

---

### 3. Watch Value 列变化黄色高亮（1秒滚动计时）

**文件**: `Watch.Method.cs`
- 新增成员变量 `private int[] _highlightCountdown = new int[0];`（放在 `_scopeRecordIndex` 下方）
- `updateParamDataOnce()` 和 `updateJdDataOnce()` 中：**先比较旧值 != 新值 → Interlocked.Exchange(_highlightCountdown[i], 10)**，再赋值
- 计时单位：10 = 10×100ms = 1秒，每次新变化重置（滚动计时）

**文件**: `Watch.cs`
- `Watch_Load`：初始化 `_highlightCountdown = new int[WatchConfig.VarietyInfos.Count]`
- `addToolStripButton_Click`：records 重建后同步扩容
- `deleteToolStripButton_Click`：同步缩容
- `clearToolStripButton_Click`：重置为 `new int[0]`
- `timer1_Tick` 刷新循环：
  - `cnt > 0` → `Color.Yellow` + `Interlocked.Decrement`
  - `cnt == 0` → `Color.Empty`（清除单元格级覆盖，回退行级 DefaultCellStyle）
- 优先级：`OrangeRed`（行级）> `Yellow`（单元格级）> `Color.Empty`（默认）

---

### 4. Watch 输入方式重构（删除 textBox1，原生 DataGridView 编辑）

**根因**：`timer1_Tick` 每 100ms 刷新 Value 列会清空正在编辑的单元格内容，故原来用 textBox1 作中转拦截。

**解决方案（思路B）**：`timer1_Tick` 刷新时检测当前编辑行，跳过整行 UI 刷新，保护用户输入。

**文件**: `Watch.cs`
- 构造函数：删除 `dataGridView1.Controls.Add(textBox1)`，添加 `dataGridView1.EditMode = DataGridViewEditMode.EditOnKeystroke`
- `timer1_Tick`：整体替换，新增 `editingRow` 检测（`IsCurrentCellInEditMode && col == NextValue 或 Name`），循环中 `if (i == editingRow) continue`
- 以下方法/代码**全部删除**：
  - 成员变量 `isTextBoxHide1`, `isTextBoxHide2`
  - `timer1_Tick` 头部 textBox1 延迟逻辑（16行）
  - `addToolStripButton_Click` 末尾 `dataGridView1.CurrentCell = null; textBox1.Visible = false;`
  - `clearToolStripButton_Click` 中 `textBox1.Visible = false`
  - `Watch_Load` 中 `textBox1.Visible = false`
  - `dataGridView1_CellValidating` 中所有 5处 textBox1 操作块（每处4行）
  - `RedrawTextBox()` 整体
  - `dataGridView1_CurrentCellChanged()` 整体（全注释，空方法）
  - `textBox1_KeyDown()` 整体
  - `dataGridView1_KeyDown()` 整体
  - `ProcessCmdKey()` 整体
  - `dataGridView1_MouseClick()` 整体
  - `dataGridView1_Scroll()` 整体
- `textBox1` 控件从 Designer 删除（由用户在 VS 设计器操作）

---

## 二、已定案架构约束（禁止改动）

| 约束 | 说明 |
|---|---|
| ParamPoll mmTimer2 | 消费写队列 `ConsumeWriteQueueOnce()` 在 mmTimer2_Ticked，已完成 |
| Watch 写入语义方案A | 写源变量 `ParamData.XXX`，不走 ADS |
| CtrlIn FireAndForget=true | 走 `SendOnlyAsync`，不等 ACK |
| JD-61101 SendTx | `JdUdpClient.Instance?.SendTx()` 在 `if (GlobalVar.isUdpConnceted)` 块内，10ms周期 |
| Jd 与 Param 完全解耦 | 各自独立函数，互不侵入 |

---

## 三、下一Threading任务清单

### 任务1：MainForm.cs + treeView2 节点与界面联动

**背景**：`treeView2` 是左侧导航树，点击节点打开对应功能界面（Panel/Form嵌入）。
**目标**：为 JD-61101 和 Param 参数读写功能在 treeView2 添加节点，点击后打开对应界面。
**需要读取**：`CLS-II/src_main/MainForm.cs`（`treeView2_AfterSelect` 或类似事件）确认节点导航模式。

### 任务2：form_body — 新建 JD 参数读写界面、Param 参数读写界面

**参考文件**：`CLS-II/form_body/`（需先读取目录，找到 `UdpTest.cs` 确认界面结构）
**要求**：
- `JdParamForm.cs`：显示 JdTxFrame / JdRxFrame 字段，支持 JdTx 字段写入，JdRx 只读
- `ParamForm.cs`（或拆分多个子页面）：显示 ParamData 各结构体字段，支持读写
- 可基于 `UdpTest.cs` 的 DataGridView + 定时刷新模式改造

### 任务3：MainConfig.cs + MainForm.Method.cs — ini/config 路径重构

**当前问题**：
- `MainConfig.cs` 中 `mainConfigFile = @".\MainConfig.ini"` 为固定路径（exe同级）
- `WatchConfig.ini` 和 `ProjectConfig.ini` 也在 exe 同级
- `GlobalVar.cs` 中 `szRemoteHost`、`TotalChannels` 等旧 UDP 参数仍引入 ini

**目标**：
- `MainConfig.ini` 保留在 exe 同级（唯一固定路径）
- `WatchConfig.ini`、`ProjectConfig.ini` 迁移到 **xrp 文件同目录下的 `config/` 子文件夹**
- `MainConfig._FileItems` 新增 `JdConstsFile`、`ParamConstsFile` 等路径字段
- `GlobalVar.cs` 中废弃的 `szRemoteHost`、`TotalChannels` 等字段清理（或标注废弃）

### 任务4：UdpConfig.cs / UdpTest.cs 废弃 + 替代文件

**废弃**：
- `UdpConfig.cs`：原 UDP 参数配置文件读写（基于 ini），整体废弃
- `UdpTest.cs`：原 UDP 测试界面，废弃

**保留并移植**：
- `UdpTest.cs` 中的 `HandleTime` 功能（处理 `GlobalVar.isSaveAperiod` 周期保存）
  → 新增 `SaveFileDialog` 支持自定义配置文件名称和保存路径

**新建**：
- `ParamConfig.cs`（类比 `UdpConfig.cs`）：存储 Param 参数信息，默认路径为 `config/` 文件夹
- `JdConfig.cs`（类比）：存储 JD-61101 相关常量配置（IP、端口、DeviceNo等）

---

## 四、关键文件索引（下一Threading必读）

| 文件 | 说明 |
|---|---|
| `CLS-II/_docs/CLS-II-118_Handoff_Snapshot_v7.md` | 本文件（完整交接） |
| `CLS-II/src_configFile/MainConfig.cs` | ini 读写核心，路径重构的起点 |
| `CLS-II/src_GLV/GlobalVar.cs` | 全局变量，含待清理的旧 UDP 字段 |
| `CLS-II/src_main/MainForm.cs` | 主界面，treeView2 节点导航逻辑入口 |
| `CLS-II/src_main/MainForm.Method.cs` | 主界面方法，确认 ini 保存时机 |
| `CLS-II/form_body/UdpTest.cs` | 参考界面模板（DataGridView + 定时刷新 + HandleTime） |
| `CLS-II/form_body/UdpConfig.cs` | 参考 ini 读写模式（待废弃，先读后废） |
| `CLS-II/src_communication/JdUdpClient.cs` | JD-61101 通信层 |
| `CLS-II/src_IOData/JdData.cs` | JdTxFrame / JdRxFrame 数据结构 |
| `CLS-II/src_watch_scope/Watch.cs` | Watch 主文件（本轮已大量重构） |
| `CLS-II/src_watch_scope/Watch.Method.cs` | Watch 方法文件（本轮新增 FormatByMode 等） |
| `CLS-II/src_Dialog/AdsVariableSample.cs` | Watch 变量选择对话框（含 SetJdTreeNode） |

---

## 五、项目目录结构（截至本轮）

```
CLS-II/
├── _docs/                          ← 交接文档
├── form_body/                      ← 功能子界面（UdpTest待废弃，下轮新建JdParamForm/ParamForm）
├── src_communication/
│   ├── JdUdpClient.cs              ← JD-61101 UDP客户端（FireAndForget）
│   └── ...
├── src_configFile/
│   └── MainConfig.cs               ← ini配置，下轮重构路径
├── src_Dialog/
│   └── AdsVariableSample.cs        ← Watch变量选择对话框（已加SetJdTreeNode）
├── src_GLV/
│   └── GlobalVar.cs                ← 全局变量（含待清理旧字段）
├── src_IOData/
│   ├── JdData.cs                   ← JdTxFrame/JdRxFrame
│   └── ParamData.cs                ← Param数据（struct + lock）
├── src_main/
│   ├── MainForm.cs                 ← 主界面（已加SendTx）
│   └── MainForm.Method.cs
├── src_menu/
├── src_watch_scope/
│   ├── Watch.cs                    ← 本轮大量重构（删textBox1，加高亮，加editingRow跳过）
│   └── Watch.Method.cs             ← 本轮新增FormatByMode/Jd读写/高亮倒计时
└── ...
```

---

## 六、注意事项 & 已知风险

1. **`updateUdpDataOnce()`**：已注释（`//updateUdpDataOnce()`），不要解注释，UDP Feedback 通道已废弃
2. **`UdpConfig.cs` / `UdpTest.cs`**：下轮废弃前先完整读取，`HandleTime` 逻辑必须移植
3. **`GlobalVar.cs`**：`szRemoteHost`/`TotalChannels` 等字段在其他地方可能仍有引用，清理前需全局搜索
4. **config 文件夹**：WatchConfig.ini / ProjectConfig.ini 迁移涉及 `WatchConfig.SetDefaultWatchConfigFile()` 的调用时机，需确认 xrp 文件路径已知时才能确定 config 路径
