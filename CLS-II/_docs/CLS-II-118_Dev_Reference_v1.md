# CLS-II-118 开发参考文档 v1
**创建日期**：2026-05-15  
**Repo**：LuJiayu5862/CLS-II-118  
**Branch**：main  
**用途**：本文件为全新上下文下 AI 执行代码任务的唯一可信参考，记录项目全貌、架构约束、及待执行的四个任务的完整设计规格。每次执行代码前必须先读本文件。

---

## 一、项目完整文件树（截至 2026-05-15）

```
CLS-II/
├── App.config
├── CLS-II.csproj
├── CreateProject.cs / .Designer.cs / .resx
├── FodyWeavers.xml
├── Login.cs / .Designer.cs / .Method.cs / .resx / .en.resx
├── packages.config
├── Program.cs
├── RegexMatch.cs
│
├── _docs/
│   ├── CLS-II-118_Handoff_Snapshot_v7.md   ← 上一轮交接（v6→v7已完成内容）
│   └── CLS-II-118_Dev_Reference_v1.md      ← 本文件
│
├── form_body/                               ← 功能子界面
│   ├── DeviceInfo.cs / .Designer.cs / .resx
│   ├── ProjectInfo.cs / .Designer.cs / .resx
│   ├── UdpTest.cs / .Designer.cs           ← 【待废弃】HandleTime逻辑需移植
│   └── UdpTest.*.resx (zh/en/byn/byn-ER)
│
├── Properties/
├── Resources/
│
├── src_communication/
│   ├── JdUdpClient.cs                      ← JD-61101 UDP客户端，Singleton，FireAndForget
│   ├── MainForm.ParamPoll.cs               ← ParamPoll架构核心，_pollTable注册读写行为
│   ├── MainForm.ParamUDP.cs                ← Param UDP收发辅助
│   ├── MainForm.UDP.cs                     ← 旧UDP辅助（基本废弃）
│   ├── ParamUdpClient.cs                   ← Param通道UDP客户端，Singleton
│   └── StructFunc.cs                       ← 结构体序列化工具
│
├── src_configFile/
│   └── MainConfig.cs                       ← ini配置核心，路径锚点
│
├── src_Dialog/
│   └── AdsVariableSample.cs / .Designer.cs / .resx  ← Watch变量选择对话框
│
├── src_GLV/
│   └── GlobalVar.cs                        ← 全局变量（含待标注旧UDP字段）
│
├── src_IOData/
│   ├── JdData.cs                           ← JdTxFrame / JdRxFrame class定义
│   ├── ParamData.cs                        ← 所有Param结构体 + 锁 + 序列化
│   ├── TcStringAttribute.cs
│   ├── UdpConfig.cs                        ← 【待废弃】旧UDP配置ini读写
│   └── UdpData.cs                          ← 旧UDP数据结构（LCSParams等）
│
├── src_main/
│   ├── MainForm.cs                         ← 主界面事件（treeView2_AfterSelect等）
│   ├── MainForm.Method.cs                  ← 主界面方法（LoadTreeView2、LoadProjectFile等）
│   ├── MainForm.Designer.cs
│   ├── MultiLanguage.cs
│   └── MainForm.*.resx (zh/en/en-AE/byn)
│
├── src_menu/
│   └── Menu.cs / .Designer.cs / .resx / .en.resx
│
└── src_watch_scope/
    ├── Watch.cs                            ← Watch主文件（v7大量重构）
    ├── Watch.Method.cs                     ← Watch方法（FormatByMode/Jd读写/高亮倒计时）
    ├── Watch.Designer.cs
    ├── WatchConfig.cs                      ← WatchConfig.ini读写
    ├── ScopeView_YT.cs / .Designer.cs / .resx
    └── ScopeView_XY.cs / .Designer.cs / .resx
```

---

## 二、已完成的架构约束（禁止改动，v7已定案）

| 约束项 | 文件 | 说明 |
|---|---|---|
| ParamPoll mmTimer2 | `MainForm.ParamPoll.cs` | `ConsumeWriteQueueOnce()` 在 `mmTimer2_Ticked`，1ms周期 |
| Watch写入语义方案A | `Watch.cs` / `Watch.Method.cs` | 写源变量 `ParamData.XXX`，不走ADS，mmTimer2消费 |
| CtrlIn FireAndForget | `MainForm.ParamPoll.cs` | `SendOnlyAsync`，不等ACK |
| JD SendTx | `MainForm.cs` `mmTimer1_Ticked` | `if(GlobalVar.isSendUdp)` 块内，`JdCountdown` 倒计时，`JdPeriod=10ms` |
| Jd与Param完全解耦 | `Watch.Method.cs` | `updateJdDataOnce()` / `updateParamDataOnce()` 各自独立 |
| updateUdpDataOnce 已废弃 | `MainForm.cs` | 保持注释，**绝对不解注释** |
| UdpData.LCSParams写法 | `MainForm.cs` | 注释段保持注释 |

---

## 三、关键类/方法速查

### MainForm.cs（`src_main/`）
- `Form1_Load()`：初始化 ini、Watch、双 mmTimer
- `mmTimer1_Ticked()`：JD SendTx（10ms cadence）+ 正弦测试波 + `OnHiResTick()`
- `mmTimer2_Ticked()`：`ConsumeWriteQueueOnce()`
- `treeView2_AfterSelect()`：`switch(nodeselect)` 按节点文本路由，调用 `GenerateForm("CLS_II.类名", panel_Body)`

### MainForm.Method.cs（`src_main/`）
- `LoadTreeView2()`：动态 `Add` treeView2 节点（**新节点在此处插入**）
- `LoadProjectFile(xrpPath)`：加载 xrp 后触发，**config路径重算在此处调用**
- `SaveProjectFile()`：保存 xrp
- `GenerateForm(string className, Panel panel)`：反射创建 Form 并嵌入 Panel
- `CloseForm(Panel panel)`：关闭并释放 Panel 中的子窗体
- `MainConfigRefresh()`：窗体关闭前刷新 ConfigInfo（WatchVisible 等）
- `ConnectDevice()` / `DisconnectDevice()`：连接/断开 Param+Jd UDP

### GlobalVar.cs（`src_GLV/`）
```csharp
// 旧字段（待标注 [Obsolete]，暂不删除，防止编译报错）
public static string szRemoteHost = "127.0.0.1";
public static int nPortIn = 1703, nPortOut1 = 1702, nPortOut2 = 1704;

// 独立的新通道配置（不改动）
public static class JdConsts { ... }    // IP/Port for JD-61101
public static class ParamConsts { ... } // IP/Port for Param通道
```

### MainConfig.cs（`src_configFile/`）
```csharp
public static string mainConfigFile = @".\MainConfig.ini";  // 固定，跟exe
public struct _FileItems {
    public string WatchFile;    // 当前：.\WatchConfig.ini（待改为跟xrp走）
    public string ProjectFile;  // 当前：.\ProjectConfig.ini（待改为跟xrp走）
    // 新增：
    // public string JdConstsFile;
    // public string ParamConstsFile;
}
```

### WatchConfig.cs（`src_watch_scope/`）
- `SetDefaultWatchConfigFile(path)`：设置 ini 路径，在 `MainConfig.ReadConfigFile()` 末尾调用
- 路径重构后，还需在 `LoadProjectFile()` 完成后再调用一次

### JdData.cs（`src_IOData/`）
```csharp
public static class JdData {
    public static JdTxFrame JdTx = new JdTxFrame();  // 可写（lock JdTx）
    public static JdRxFrame JdRx = new JdRxFrame();  // 只读（lock JdRx）
}
// JdTxFrame / JdRxFrame 均为 class（引用类型），字段用 Property 定义，
// Watch.Method.cs 中通过 GetProperties() 反射枚举
```

### Watch.Method.cs（`src_watch_scope/`）关键方法
- `updateJdDataOnce()`：读 JdTx/JdRx，调用 `FormatByMode()`，支持黄色高亮倒计时
- `GetJdFrame(string subName)`：lock后返回 JdTx 或 JdRx 引用
- `TryWriteJdValue()`：JdRx 只读返回 false，JdTx 反射写 Property
- `FormatByMode(object raw)`：按 DEC/HEX/OCT/BIN 格式化

### UdpConfig.cs（`src_IOData/`）【待废弃】
- 存储旧 UDP 通道参数（IP/Port/TotalChannels 等）
- ini 读写基于 `INIFileRW`
- 无 HandleTime 逻辑（HandleTime 在 `UdpTest.cs` 中）

### UdpTest.cs（`form_body/`）【待废弃，移植 HandleTime】
- `HandleTime()` / `isSaveAperiod` 周期保存逻辑必须移植到新 `ParamConfig.cs`
- DataGridView + Timer 100ms 定时刷新模式是新界面的参考模板

---

## 四、待执行任务规格（按顺序执行）

---

### 任务1：treeView2 新增节点 + 界面联动

**涉及文件**：
- `CLS-II/src_main/MainForm.Method.cs`（修改 `LoadTreeView2()`）
- `CLS-II/src_main/MainForm.cs`（修改 `treeView2_AfterSelect()`）

**MainForm.Method.cs — `LoadTreeView2()` 修改**：  
在现有节点（"示波器"/"UDP通讯仿真"等）的适当位置，追加以下两个新节点：

```csharp
// 中文节点
TreeNode jdParamNode = new TreeNode("JD参数读写");
TreeNode paramNode   = new TreeNode("Param参数读写");

// 英文节点（多语言，追加到对应英文父节点下）
TreeNode jdParamNode_en = new TreeNode("JD Params");
TreeNode paramNode_en   = new TreeNode("Param Config");
```

> ⚠️ 具体插入位置（哪个父节点下、排第几）需在执行前读取 `LoadTreeView2()` 全文确认。建议放在"参数加载/保存"节点之后，"设备设置"节点之前。

**MainForm.cs — `treeView2_AfterSelect()` 修改**：  
在现有 switch 中追加两组 case（中英文各一）：

```csharp
case "JD参数读写":
case "JD Params":
    CloseForm(panel_Body);
    GenerateForm("CLS_II.JdParamForm", panel_Body);
    break;

case "Param参数读写":
case "Param Config":
    CloseForm(panel_Body);
    GenerateForm("CLS_II.ParamForm", panel_Body);
    break;
```

**执行前必读**：`MainForm.Method.cs` 中 `LoadTreeView2()` 的完整内容，确认父节点名称和插入位置。

---

### 任务2：新建 JdParamForm.cs 和 ParamForm.cs

**涉及文件（全部新建）**：
- `CLS-II/form_body/JdParamForm.cs`
- `CLS-II/form_body/JdParamForm.Designer.cs`
- `CLS-II/form_body/ParamForm.cs`
- `CLS-II/form_body/ParamForm.Designer.cs`

#### 2A — JdParamForm.cs 设计规格

**界面结构**：
- `TabControl`（Dock=Fill）：`tabPage1 = "JdTx (读写)"`，`tabPage2 = "JdRx (只读)"`
- 每个 Tab 内：`DataGridView`（Dock=Fill）
  - 列定义：`Col0=字段名(ReadOnly)` / `Col1=类型(ReadOnly)` / `Col2=值(JdTx可编辑, JdRx只读)`
- 底部 StatusStrip 显示最后刷新时间戳

**数据流**：
- `Timer`（100ms）→ `RefreshJdDisplay()`
- `RefreshJdDisplay()`：通过 `Watch.GetJdFrame("Tx")` 和 `Watch.GetJdFrame("Rx")` 获取引用，用 `GetProperties()` 反射枚举字段，填充 DataGridView
- 写入：`DataGridView1_CellEndEdit`（JdTx tab）→ 调用 `Watch._Watch.TryWriteJdValue(varInfo, newVal)`

**约束**：
- 不直接访问 `JdData.JdTx/JdRx`，统一走 `Watch.GetJdFrame()` 以保持 lock 语义
- JdRx tab 的 DataGridView 设置 `ReadOnly = true`
- Timer 刷新时检测 `IsCurrentCellInEditMode`，若正在编辑则跳过该行（与 Watch.cs 同款保护）

#### 2B — ParamForm.cs 设计规格

**界面结构**：
- `TabControl`（Dock=Fill），每个 Tab 对应一个参数组（见下表）
- 每个 Tab 内：`DataGridView`（Dock=Fill）
  - 列：`Col0=字段名(ReadOnly)` / `Col1=类型(ReadOnly)` / `Col2=值`

**参数组 Tab 列表**：

| Tab 名 | 数据源 | 可写 | 备注 |
|---|---|---|---|
| CLSModel | `ParamData.CLS_Model` | ✅ | ST_CLSModel |
| CLSParam | `ParamData.CLS_Param` | ✅ | ST_CLSParam |
| CLS5K | `ParamData.CLS_5K` | ✅ | ST_CLS5K |
| CLSConsts | `ParamData.CLS_Consts` | ✅ | ST_CLSConsts |
| TestMDL | `ParamData.Test_MDL` | ✅ | ST_TestMDL |
| CLSEnum | `ParamData.CLS_Enum` | ✅ | ST_CLSEnum |
| XT | `ParamData.Param_XT` | ✅ | ST_XT（含数组） |
| YT | `ParamData.Param_YT` | ✅ | ST_YT（含数组） |
| DeviceInfo | `ParamData.Device_Info` | ✅ | ST_DeviceInfo |
| UdpDataCfg | `ParamData.UdpData_Cfg` | ✅ | ST_UdpCfg |
| UdpParamCfg | `ParamData.UdpParam_Cfg` | ✅ | ST_UdpCfg |
| CtrlIn | `ParamData.CtrlIn` | ✅ | TcLCS_CtrlIn |
| CtrlOut | `ParamData.CtrlOut` | ❌ | TcLCS_CtrlOut，只读 |

**数据流**：
- `Timer`（100ms）→ `RefreshParamDisplay()`
- `RefreshParamDisplay()`：反射枚举当前 Tab 对应的结构体字段（`GetFields()`），填充 DataGridView
- 写入：`DataGridView_CellEndEdit` → 反射写 `ParamData.对应结构体字段` → **无需调用任何 UDP 方法**，`MainForm.ParamPoll.cs` 的 `_pollTable` 会自动检测变化并处理

**约束**：
- 写入时需持对应的 lock（如 `lock(ParamData.LockCLS5K)`）再反射写入
- 每个 Tab 独立 Timer 刷新，或共用一个 Timer + switch(tabControl.SelectedIndex)
- CtrlOut Tab `ReadOnly = true`
- 数组字段（XT.aXT[21] / YT.aYT[21]）：每个数组元素展开为独立行（行名 = `aXT[0]` ... `aXT[20]`）

**执行前必读**：`src_IOData/ParamData.cs`，确认所有结构体字段名、类型和对应的 lock 对象。

---

### 任务3：MainConfig + 路径重构 + GlobalVar 清理

**涉及文件**：
- `CLS-II/src_configFile/MainConfig.cs`（修改）
- `CLS-II/src_main/MainForm.Method.cs`（修改 `LoadProjectFile()` 末尾）
- `CLS-II/src_GLV/GlobalVar.cs`（修改，添加 Obsolete 标注）

#### 3A — MainConfig.cs 修改规格

**`_FileItems` 结构体新增字段**：
```csharp
public struct _FileItems {
    public string WatchFile;       // 原有（路径语义改变，跟xrp走）
    public string ProjectFile;     // 原有（同上，历史兼容保留）
    public string JdConstsFile;    // 新增：JdConfig.ini 路径
    public string ParamConstsFile; // 新增：ParamConfig.ini 路径
}
```

**`CreateConfigFile()` 修改**：  
WatchFile / JdConstsFile / ParamConstsFile 的默认值改为相对 exe 的 `config\` 子文件夹（作为 fallback）：
```csharp
iniFileRW.INIWriteValue(mainConfigFile, "FileItems", "WatchFile",        @".\config\WatchConfig.ini");
iniFileRW.INIWriteValue(mainConfigFile, "FileItems", "JdConstsFile",     @".\config\JdConfig.ini");
iniFileRW.INIWriteValue(mainConfigFile, "FileItems", "ParamConstsFile",  @".\config\ParamConfig.ini");
```

**`ReadConfigFile()` 修改**：
```csharp
ConfigInfo.FileItems.JdConstsFile    = iniFileRW.INIGetStringValue(..., "JdConstsFile",    @".\config\JdConfig.ini");
ConfigInfo.FileItems.ParamConstsFile = iniFileRW.INIGetStringValue(..., "ParamConstsFile", @".\config\ParamConfig.ini");
// WatchFile 读取保持原有逻辑
// ⚠️ 此时不再在 ReadConfigFile 末尾调用 WatchConfig.SetDefaultWatchConfigFile()
//    改为在 LoadProjectFile() 末尾调用
```

**`WriteConfigFile()` 修改**：
```csharp
iniFileRW.INIWriteValue(mainConfigFile, "FileItems", "JdConstsFile",     ConfigInfo.FileItems.JdConstsFile);
iniFileRW.INIWriteValue(mainConfigFile, "FileItems", "ParamConstsFile",  ConfigInfo.FileItems.ParamConstsFile);
```

**新增静态方法 `RelocateConfigPaths(string xrpDir)`**：
```csharp
public static void RelocateConfigPaths(string xrpDir) {
    string configDir = Path.Combine(xrpDir, "config");
    if (!Directory.Exists(configDir))
        Directory.CreateDirectory(configDir);

    ConfigInfo.FileItems.WatchFile       = Path.Combine(configDir, "WatchConfig.ini");
    ConfigInfo.FileItems.JdConstsFile    = Path.Combine(configDir, "JdConfig.ini");
    ConfigInfo.FileItems.ParamConstsFile = Path.Combine(configDir, "ParamConfig.ini");

    // 持久化到 MainConfig.ini
    iniFileRW.INIWriteValue(mainConfigFile, "FileItems", "WatchFile",       ConfigInfo.FileItems.WatchFile);
    iniFileRW.INIWriteValue(mainConfigFile, "FileItems", "JdConstsFile",    ConfigInfo.FileItems.JdConstsFile);
    iniFileRW.INIWriteValue(mainConfigFile, "FileItems", "ParamConstsFile", ConfigInfo.FileItems.ParamConstsFile);

    // 通知 WatchConfig 使用新路径（若 ini 不存在则自动创建）
    WatchConfig.SetDefaultWatchConfigFile(ConfigInfo.FileItems.WatchFile);
}
```

#### 3B — MainForm.Method.cs 修改规格

**`LoadProjectFile(string filePath)` 末尾追加**：
```csharp
// 重算 config 路径（跟 xrp 所在目录走）
string xrpDir = Path.GetDirectoryName(filePath);
MainConfig.RelocateConfigPaths(xrpDir);
```

**`saveasToolStripMenuItem_Click`（在 MainForm.cs）末尾**，`LoadProjectFile(localFilePath)` 调用后同样触发（已通过 `LoadProjectFile` 内部逻辑自动触发，无需额外修改）。

#### 3C — GlobalVar.cs 修改规格

标注旧 UDP 字段为废弃（不删除，防止编译报错）：
```csharp
[Obsolete("旧UDP通道字段，已被 JdConsts / ParamConsts 替代，请勿在新代码中使用")]
public static string szRemoteHost = "127.0.0.1";

[Obsolete("旧UDP端口字段，已被 JdConsts / ParamConsts 替代，请勿在新代码中使用")]
public static int nPortIn = 1703, nPortOut1 = 1702, nPortOut2 = 1704;
```

> ⚠️ `CLSConsts.TotalChannels` 和 `CLSConsts.EnabledChannels` **不标注废弃**，可能仍被 Scope 等模块使用。

---

### 任务4：新建 ParamConfig.cs / JdConfig.cs，废弃 UdpConfig.cs / UdpTest.cs

**涉及文件**：
- `CLS-II/src_configFile/ParamConfig.cs`（新建）
- `CLS-II/src_configFile/JdConfig.cs`（新建）
- `CLS-II/src_IOData/UdpConfig.cs`（添加文件头废弃注释，不删除）
- `CLS-II/form_body/UdpTest.cs`（添加文件头废弃注释，不删除）

#### 4A — ParamConfig.cs 设计规格

**存储内容**：Param 通道的连接参数（对应 `ParamConsts` 的运行时可配置版本）

```csharp
namespace CLS_II {
    public static class ParamConfig {
        // 默认 ini 路径（由 MainConfig.RelocateConfigPaths 动态设置）
        public static string configFile = @".\config\ParamConfig.ini";

        public static string RemoteHost   = ParamConsts.szParamRemoteHost;
        public static int    PortSend     = ParamConsts.nParamPortSend;
        public static int    PortRecv     = ParamConsts.nParamPortRecv;
        public static byte   DeviceId     = ParamConsts.byParamDeviceId;

        // 移植自 UdpTest.cs：HandleTime / isSaveAperiod 周期保存逻辑
        // 新增：SaveFileDialog 自定义保存路径
        private static string _saveFilePath = string.Empty;

        public static void ConfigFileInit() { ... }   // 读或创建 ini
        public static void ReadConfigFile()  { ... }
        public static void WriteConfigFile() { ... }
        public static void HandleTime()      { ... }  // 检测 GlobalVar.isSaveAperiod，调用 SaveConfig()
        public static void SaveConfig()      { ... }  // 弹出 SaveFileDialog（若 _saveFilePath 为空），写 ini
    }
}
```

**HandleTime 逻辑（移植自 UdpTest.cs）**：
```csharp
public static void HandleTime() {
    if (GlobalVar.isSaveAperiod) {
        GlobalVar.isSaveAperiod = false;
        SaveConfig();
    }
}

public static void SaveConfig() {
    if (string.IsNullOrEmpty(_saveFilePath)) {
        SaveFileDialog dlg = new SaveFileDialog();
        dlg.Filter = "Config files (*.ini)|*.ini";
        dlg.InitialDirectory = Path.GetDirectoryName(configFile);
        dlg.FileName = "ParamConfig";
        dlg.DefaultExt = "ini";
        if (dlg.ShowDialog() == DialogResult.OK)
            _saveFilePath = dlg.FileName;
        else
            return;
    }
    WriteConfigFile(_saveFilePath);  // 重载版本，写到指定路径
}
```

#### 4B — JdConfig.cs 设计规格

**存储内容**：JD-61101 通道连接参数（对应 `JdConsts` 的运行时可配置版本）

```csharp
namespace CLS_II {
    public static class JdConfig {
        public static string configFile = @".\config\JdConfig.ini";

        public static string RemoteHost = JdConsts.szJdRemoteHost;
        public static int    PortSend   = JdConsts.nJdPortSend;
        public static int    PortRecv   = JdConsts.nJdPortRecv;

        public static void ConfigFileInit() { ... }
        public static void ReadConfigFile()  { ... }
        public static void WriteConfigFile() { ... }
    }
}
```

#### 4C — UdpConfig.cs / UdpTest.cs 废弃标注

在文件顶部（using 语句上方）添加：
```csharp
// ============================================================
// [OBSOLETE] 此文件已废弃（2026-05-15）
// 替代方案：src_configFile/ParamConfig.cs / JdConfig.cs
// 保留原因：防止旧引用编译报错，待全部引用清理后删除
// ============================================================
```

---

## 五、任务执行顺序与依赖关系

```
任务3（MainConfig路径重构）
    ↓ 必须先完成（任务4依赖 configFile 路径规范）
任务4（ParamConfig/JdConfig新建）
    ↓ 可并行或之后
任务1（treeView2节点）  ←→  任务2（JdParamForm/ParamForm新建）
    （两者独立，可同步执行）
```

**推荐执行顺序**：任务3 → 任务4 → 任务1 → 任务2

---

## 六、每次代码执行前的必读文件清单

| 任务 | 执行前必读文件 |
|---|---|
| 任务1 | `MainForm.Method.cs`（完整，确认 `LoadTreeView2` 内容）；`MainForm.cs`（`treeView2_AfterSelect`） |
| 任务2 | `src_IOData/ParamData.cs`（全文，确认结构体字段和lock）；`src_IOData/JdData.cs`；`Watch.Method.cs`（GetJdFrame/TryWriteJdValue签名） |
| 任务3 | `MainConfig.cs`（当前全文）；`MainForm.Method.cs`（`LoadProjectFile`完整内容）；`GlobalVar.cs` |
| 任务4 | `src_IOData/UdpConfig.cs`（全文，了解ini读写模式）；`form_body/UdpTest.cs`（`HandleTime`完整逻辑） |

---

## 七、已知风险与注意事项

1. **`WatchConfig.SetDefaultWatchConfigFile()` 调用时机变更**：任务3完成后，此调用从 `ReadConfigFile()` 末尾移到 `LoadProjectFile()` 末尾。若软件启动后未打开 xrp，Watch 窗口为空（设计如此，符合预期）。

2. **`LoadTreeView2()` 执行前必须先读其完整内容**：文档中仅描述插入位置的建议，实际父节点名称以代码为准。

3. **数组字段展开（XT/YT）**：`ParamData.Param_XT.aXT` 是 `float[]` 类型，反射 `GetFields()` 会返回数组本身，需要在 `RefreshParamDisplay()` 中特殊处理展开为多行。

4. **ParamForm 的 lock 对象**：每个参数组写入时需要对应的 lock，执行前必须从 `ParamData.cs` 确认每个结构体对应的 lock 字段名（如 `ParamData.LockCLS5K`、`ParamData.LockCLSModel` 等）。

5. **`GlobalVar.szRemoteHost` 旧引用检查**：标注 `[Obsolete]` 后编译器会产生警告，不会报错。若有代码仍依赖此字段，警告可接受，后续统一迁移。

6. **UdpConfig.cs / UdpTest.cs 废弃后**：相关 `GenerateForm("CLS_II.UdpTest", ...)` 的 treeView2 case 应同步注释（在 `treeView2_AfterSelect` 中注释掉"UDP通讯仿真"分支），防止运行时找不到界面崩溃。

---

*文档版本 v1 — 2026-05-15 — 基于 Handoff_Snapshot_v7 生成*
