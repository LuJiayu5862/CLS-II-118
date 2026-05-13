using MmTimer;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CLS_II
{
    public partial class MainForm : Form
    {
        // 创建多媒体定时器，周期10ms
        HPTimer mmTimer1;
        HPTimer mmTimer2;
        
        public MainForm()
        {
            InitializeComponent();            
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            // config.ini初始化
            MainConfig.ConfigFileInit();

            // 语言资源初始化
            MultiLanguage.SetDefaultLanguage(MainConfig.ConfigInfo.SetItems.Language);
            MultiLanguage.LoadLanguage(this, typeof(MainForm));           
            foreach (ToolStripMenuItem item in languageToolStripMenuItem.DropDownItems)
            {
                if (item.Tag.ToString() == MultiLanguage.DefaultLanguage)
                {
                    item.Checked = true;
                }
            }
            LanguageChanged();

            // toolstrip初始化
            

            // Watch窗口初始化
            watchToolStripMenuItem.Checked = panel_Watch.Visible = MainConfig.ConfigInfo.DebugItems.isWatchVisible;
            autoWatchToolStripMenuItem.Checked = MainConfig.ConfigInfo.DebugItems.isAutoWatch;
            panel_Watch.Height = 150;
            foreach (ToolStripMenuItem item in displayModeToolStripMenuItem.DropDownItems)
            {
                item.Checked = false;
            }
            switch (MainConfig.ConfigInfo.DebugItems.WatchMode)
            {
                case (int)WatchDataMode.DEC:
                    decToolStripMenuItem.Checked = true;
                    break;
                case (int)WatchDataMode.HEX:
                    hexToolStripMenuItem.Checked = true;
                    break;
                case (int)WatchDataMode.OCT:
                    octToolStripMenuItem.Checked = true;
                    break;
                case (int)WatchDataMode.BIN:
                    binToolStripMenuItem.Checked = true;
                    break;
            }

            // Body
            toolStrip_UDP.Visible = false;


            // Panel窗体初始化
            GenerateForm("CLS_II.Watch", panel_WatchBody);
            //GenerateForm("CLS_II.UdpTest", panel_Body);

            // StatusStrip初始化
            toolStripStatusLabel2.Text = "Default";
            if (!MainConfig.ConfigInfo.DebugItems.isAutoWatch)
                Watch._Watch.writeUpdateStyle("Single");      
            else
                Watch._Watch.writeUpdateStyle("Auto");

            // 打开普通定时器
            timer_Base.Enabled = true;
            timer_Base.Start();
            // 打开高精度定时器1
            mmTimer1 = new HPTimer(10);
            mmTimer1.Ticked += new EventHandler(mmTimer1_Ticked);
            mmTimer1.Start();
            // 打开高精度定时器2
            mmTimer2 = new HPTimer(20);
            mmTimer2.Ticked += new EventHandler(mmTimer2_Ticked);
            //mmTimer2.Start();
        }

        private void mmTimer1_Ticked(object sender, EventArgs e)
        {
            // UDP发送任务
            lock (UdpData.LCSControls)
            {
                if (GlobalVar.isSendUdp)
                {
                    udpClient.Send(Struct_Func.StructToBytes(UdpData.LCSControls));
                    JdUdpClient.Instance?.SendTx();
                }
            }
            lock (UdpData.LCSParams)
            {
                if (GlobalVar.isUdpConnceted && GlobalVar.isParamChanged)
                {
                    GlobalVar.isParamChanged = false;
                    udpClient.Send(Struct_Func.StructToBytes(UdpData.LCSParams), nPortOut2);
                }
            }
        }

        private void mmTimer2_Ticked(object sender, EventArgs e)
        {
            
        }

        private void languageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            string languageStr = ((ToolStripMenuItem)sender).Tag.ToString();
            foreach (ToolStripMenuItem item in languageToolStripMenuItem.DropDownItems)
            {
                item.Checked = false;
            }
            ((ToolStripMenuItem)sender).Checked = true;
            if (languageStr != String.Empty)
            {
                if (languageStr != MultiLanguage.DefaultLanguage)
                {
                    if (MultiLanguage.DefaultLanguage == "zh")
                    {
                        MessageBox.Show("为了完全应用更改，请重启软件。", "语言");
                    }
                    else
                    {
                        MessageBox.Show("To fully apply the changes, please restart the software.", "Language");
                    }
                    MultiLanguage.SetDefaultLanguage(languageStr);
                }
            }   
            //foreach (Form form in Application.OpenForms)
            //{
            //    LoadAll(form);
            //}
            //LanguageChanged();
        }

        private bool isFileChanged = false;
        private void timer1_Tick(object sender, EventArgs e)
        {
            if (GlobalVar.isTmpProjectFile)
            {
                GlobalVar.isTmpProjectFile = false;
                SaveProjectFile(GlobalVar.tmpProjectFile);
                LoadProjectFile(GlobalVar.tmpProjectFile);
            }

            if (GlobalVar.isProjectFileChanged)
            {
                if (treeView1.Nodes.Count > 0 && !isFileChanged)
                {
                    isFileChanged = true;
                    treeView1.Nodes[0].Text = GlobalVar.ProjectName + "*";
                }
            }
            else
            {
                if (treeView1.Nodes.Count > 0 && isFileChanged)
                {
                    isFileChanged = false;
                    treeView1.Nodes[0].Text = GlobalVar.ProjectName;
                }
            }
        }

        private void watchToolStripMenuItem_Click(object sender, EventArgs e)
        {
            watchToolStripMenuItem.Checked = !watchToolStripMenuItem.Checked;
            panel_Watch.Visible = watchToolStripMenuItem.Checked;           
        }

        private void hideToolStripMenuItem_Watch_Click(object sender, EventArgs e)
        {
            panel_Watch.Visible = false;
            watchToolStripMenuItem.Checked = false;
        }

        private void restoreToolStripMenuItem_Watch_Click(object sender, EventArgs e)
        {
            panel_Watch.Height = this.Height - 150;
        }

        private void minToolStripMenuItem_Watch_Click(object sender, EventArgs e)
        {
            panel_Watch.Height = 26;
        }

        private void userCheck_Click(object sender, EventArgs e)
        {
            if (sender != administratorToolStripMenuItem)
            {
                GlobalVar.isAdministrator = false;
                foreach (ToolStripMenuItem item in userToolStripMenuItem.DropDownItems)
                {
                    item.Checked = false;
                }
                ((ToolStripMenuItem)sender).Checked = true;
                toolStripStatusLabel2.Text = ((ToolStripMenuItem)sender).Text;
            }
            else if(!GlobalVar.isAdministrator)
            {
                Login login = new Login();
                login.onJudged += new Login.JudgeEventHandler(admin_onJudged);
                login.ShowDialog();               
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            MainConfigRefresh();
            MainConfig.WriteConfigFile();
            DialogResult result = new DialogResult();

            if (GlobalVar.isProjectFileChanged)
            {
                if (MultiLanguage.DefaultLanguage == "zh")
                {
                    result = MessageBox.Show("是否保存更改?", "保存", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
                }
                else
                {
                    result = MessageBox.Show("Do you want to save changes?", "Save", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
                }
                if (result == DialogResult.OK)
                {
                    SaveProjectFile(GlobalVar.ProjectFile);
                    GlobalVar.isProjectFileChanged = false;
                }
            }

            if (MultiLanguage.DefaultLanguage == "zh")
            {
                if (sender == restartToolStripMenuItem)
                    result = MessageBox.Show("确认要重启吗?", "重启", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
                else
                    result = MessageBox.Show("确认要关闭吗?", "关闭", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
            }
            else
            {
                if (sender == restartToolStripMenuItem)
                    result = MessageBox.Show("Do you want to restart?", "Restart", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
                else
                    result = MessageBox.Show("Do you want to close?", "Close", MessageBoxButtons.OKCancel, MessageBoxIcon.Information);
            }
            isClosing = result == DialogResult.OK ? true : false;
            if (result == DialogResult.Cancel)
            {
                if (e != null)
                    e.Cancel = true;
                else
                    return;
            }
            else
            {
                // 关闭定时器
                mmTimer1.Stop();
                mmTimer2.Stop();
                timer_Base.Stop();

                // 关闭子窗体
                CloseForm(panel_WatchBody);


                //CloseForm(panel_Menu);
                CloseForm(panel_Body);

                if (sender == restartToolStripMenuItem)
                {
                    System.Diagnostics.Process.Start(System.Reflection.Assembly.GetExecutingAssembly().Location);
                    MainForm_FormClosed(null, null);
                }
            }
        }

        private void restartToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MainForm_FormClosing(sender,null);           
        }

        private bool isClosing = false;
        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MainForm_FormClosing(null, null);
            if (isClosing)
                System.Environment.Exit(0);
        }

        private void autoWatchToolStripMenuItem_Click(object sender, EventArgs e)
        {
            autoWatchToolStripMenuItem.Checked = MainConfig.ConfigInfo.DebugItems.isAutoWatch = !autoWatchToolStripMenuItem.Checked;
            Watch._Watch.writeUpdateStyle(MainConfig.ConfigInfo.DebugItems.isAutoWatch ? "Auto" : "Single");
        }

        private void displayWatchMode_Click(object sender, EventArgs e)
        {
            foreach (ToolStripMenuItem item in displayModeToolStripMenuItem.DropDownItems)
            {
                item.Checked = false;
            }
            ((ToolStripMenuItem)sender).Checked = true;
            switch (((ToolStripMenuItem)sender).Tag)
            {
                case "DEC":
                    MainConfig.ConfigInfo.DebugItems.WatchMode = (int)WatchDataMode.DEC;
                    break;
                case "HEX":
                    MainConfig.ConfigInfo.DebugItems.WatchMode = (int)WatchDataMode.HEX;
                    break;
                case "OCT":
                    MainConfig.ConfigInfo.DebugItems.WatchMode = (int)WatchDataMode.OCT;
                    break;
                case "BIN":
                    MainConfig.ConfigInfo.DebugItems.WatchMode = (int)WatchDataMode.BIN;
                    break;
            }
        }

        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            //System.Environment.Exit(0);
        }

        private void singleWatchToolStripMenuItem_Click(object sender, EventArgs e)
        {
            GlobalVar.isSingleWatch = true;
        }

        private void testToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (sender == simulationToolStripMenuItem)
            {
                testToolStripMenuItem.Checked = false;
                modeToolStripDropDownButton.Text = simulationToolStripMenuItem.Text;
                GlobalVar.isSimulation = true;
                
            }
            else
            {
                simulationToolStripMenuItem.Checked = false;
                modeToolStripDropDownButton.Text = testToolStripMenuItem.Text;
                GlobalVar.isSimulation = false;
            }
            if (GlobalVar.isSimulation)
            {
                UdpData.LCSParams.wDataTP = 0;
                GlobalVar.isParamChanged = true;
            }
            else
            {
                GlobalVar.isParamChanged = true;
                UdpData.LCSParams.wDataTP = 1;
            }
        }

        private void connectToolStripButton1_Click(object sender, EventArgs e)
        {
            if (GlobalVar.isUdpConnceted)
            {
                DisconnectDevice();
            }
            else
            {
                ConnectDevice();
            }
        }

        private void udpSendToolStripButton1_Click(object sender, EventArgs e)
        {
            GlobalVar.isSendUdp = !GlobalVar.isSendUdp;
            if (!GlobalVar.isUdpConnceted)
                GlobalVar.isSendUdp = false;
            if (GlobalVar.isSendUdp)
            {
                if (MultiLanguage.DefaultLanguage == "zh")
                {
                    udpSendToolStripButton1.Text = "停止发送";
                }
                else
                {
                    udpSendToolStripButton1.Text = "Stop UDP";
                }
                udpSendToolStripButton1.BackColor = Color.Orange;
            }
            else
            {
                if (MultiLanguage.DefaultLanguage == "zh")
                {
                    udpSendToolStripButton1.Text = "连续发送";
                }
                else
                {
                    udpSendToolStripButton1.Text = "Send UDP";
                }
                udpSendToolStripButton1.BackColor = Color.PowderBlue;
            }
        }

        private void changeAllToolStripButton_Click(object sender, EventArgs e)
        {
            GlobalVar.isUdpAllAccept = true;
        }

        private void saveUDPToolStripButton_Click(object sender, EventArgs e)
        {
            GlobalVar.isSaveAperiod = true;
        }

        private void stopToolStripButton_Click(object sender, EventArgs e)
        {
            GlobalVar.isAllChannelStop = true;
        }

        private void resetToolStripButton_Click(object sender, EventArgs e)
        {
            GlobalVar.isAllChannelReset = true;
        }

        private void newToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (sender == newToolStripMenuItem)
            {
                bool valid = true;
                if (!string.IsNullOrEmpty(GlobalVar.ProjectFile))
                {
                    valid = CloseProjectFile();
                }
                if(valid)
                    CreateProjectFile();
            }
            else if (sender == openToolStripMenuItem)
            {
                bool valid = true;
                if (!string.IsNullOrEmpty(GlobalVar.ProjectFile))
                {
                    valid = CloseProjectFile();
                }
                if (valid)
                    OpenProjectFile();
            }
            else if (sender == CloseToolStripMenuItem)
            {
                CloseProjectFile();
            }
        }

        private void treeView1_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (treeView1.SelectedNode == treeView1.Nodes[0])
            {
                CloseForm(panel_Body);
                GenerateForm("CLS_II.ProjectInfo", panel_Body);
            }
            else if (treeView1.SelectedNode == treeView1.Nodes[0].Nodes[0])
            {
                CloseForm(panel_Body);
                GenerateForm("CLS_II.DeviceInfo", panel_Body);
            }
        }

        private void treeView2_AfterSelect(object sender, TreeViewEventArgs e)
        {
            string nodeselect = treeView2.SelectedNode.Text;
            string nodeSymbol = nodeselect;
            if (nodeselect.LastIndexOf("<") > 0)
                nodeselect = nodeselect.Substring(0, nodeselect.LastIndexOf("<"));
            switch (nodeselect)
            {
                case "示波器":
                case "Scope":
                    CloseForm(panel_Body);

                    break;
                case "Y-T 示波器":
                case "Y-T Scope":
                    CloseForm(panel_Body);
                    GenerateForm("CLS_II.ScopeView_YT", panel_Body);
                    break;
                case "X-Y 示波器":
                case "X-Y Scope":
                    CloseForm(panel_Body);
                    GenerateForm("CLS_II.ScopeView_XY", panel_Body);
                    break;
                case "参数加载/保存":
                case "Parameter Load/Save":
                    CloseForm(panel_Body);

                    break;
                case "设备设置":
                case "Device Settings":
                    CloseForm(panel_Body);

                    break;
                case "UDP通讯仿真":
                case "UDP Simulator":
                    CloseForm(panel_Body);
                    GenerateForm("CLS_II.UdpTest", panel_Body);
                    break;
                case "通道":
                case "Channels":
                    CloseForm(panel_Body);

                    break;
                //case "通道1":
                //case "通道2":
                //case "通道3":
                //case "通道4":
                //case "通道5":
                //case "通道6":
                //case "通道7":
                //case "通道8":
                //case "通道9":
                //case "通道10":
                //case "Channel1":
                //case "Channel2":
                //case "Channel3":
                //case "Channel4":
                //case "Channel5":
                //case "Channel6":
                //case "Channel7":
                //case "Channel8":
                //case "Channel9":
                //case "Channel10":
                //    CloseForm(panel_Body);
                //    {
                //        int index = int.Parse(nodeselect.Substring(nodeselect.Length - 1, 1));
                //        if (index == 0)
                //            index = 10;
                //        index = index - 1;
                //        string str = nodeSymbol;
                //        str = str.Replace("<",": ");
                //        str = str.Replace(">", "");
                //        // 生成窗体
                //        AdsTest fm = new AdsTest(str, AdsInfo.AdsObject.AdsPort[index],adsClients[index]);
                //        GenerateForm(fm, panel_Body);
                //    }
                //    break;

            }
        }

        private void treeView2_Leave(object sender, EventArgs e)
        {
            if (treeView2.SelectedNode != null)
            {
                treeView2.SelectedNode.BackColor = Color.SkyBlue;
            }
        }

        private void treeView2_BeforeSelect(object sender, TreeViewCancelEventArgs e)
        {
            if (treeView2.SelectedNode != null)
            {
                treeView2.SelectedNode.BackColor = SystemColors.Window;
            }
            if (treeView1.SelectedNode != null)
            {
                treeView1.SelectedNode.BackColor = SystemColors.Window;
                treeView1.SelectedNode = null;
            }
        }

        private void treeView1_Leave(object sender, EventArgs e)
        {
            if (treeView1.SelectedNode != null)
            {
                treeView1.SelectedNode.BackColor = Color.SkyBlue;
            }
        }

        private void treeView1_BeforeSelect(object sender, TreeViewCancelEventArgs e)
        {
            if (treeView1.SelectedNode != null)
            {
                treeView1.SelectedNode.BackColor = SystemColors.Window;
            }
            if (treeView2.SelectedNode != null)
            {
                treeView2.SelectedNode.BackColor = SystemColors.Window;
                treeView2.SelectedNode = null;
            }
        }

        private void reloadToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(GlobalVar.ProjectFile))
            {
                CloseForm(panel_Body);
                CloseForm(panel_Watch);
                LoadProjectFile(GlobalVar.ProjectFile);
                treeView1.SelectedNode = treeView1.Nodes[0];
                DisconnectDevice();
            }
        }

        private void saveToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveProjectFile(GlobalVar.ProjectFile);
            GlobalVar.isProjectFileChanged = false;
        }

        private void saveasToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrEmpty(GlobalVar.ProjectFile))
            {
                string localFilePath = string.Empty;
                string fileNameExt = string.Empty;
                SaveFileDialog saveFileDialog = new SaveFileDialog();
                saveFileDialog.Filter = "Project files(*.xrp)|*.xrp";
                saveFileDialog.FileName = "NewProject1";
                saveFileDialog.DefaultExt = "xrp";
                saveFileDialog.AddExtension = true;
                saveFileDialog.RestoreDirectory = true;

                DialogResult result = saveFileDialog.ShowDialog();
                if (result == DialogResult.OK)
                {
                    localFilePath = saveFileDialog.FileName;
                    fileNameExt = localFilePath.Substring(localFilePath.LastIndexOf("\\") + 1,
                        localFilePath.LastIndexOf(".") - localFilePath.LastIndexOf("\\") - 1);
                    this._nowProjectFile = localFilePath;
                    GlobalVar.ProjectFile = localFilePath;
                    SaveProjectFile(localFilePath);
                    LoadProjectFile(localFilePath);
                    GlobalVar.isProjectFileChanged = false;
                    DisconnectDevice();
                    if (treeView1.Nodes.Count > 0)
                        treeView1.SelectedNode = treeView1.Nodes[0];
                }
            }
        }

        private async void button1_Click(object sender, EventArgs e)
        {
            if (ParamUdpClient.Instance == null)
            {
                Debug.WriteLine("[Param] not connected");
                return;
            }

            try
            {
                TcFrame resp = await ParamPingAsync();

                // PONG 帧：Payload 为空（PayloadLen = 0），只需确认 CMD 是 PONG 即可
                if (resp.Header.Cmd == TcCmd.PONG)
                {
                    byte[] bytes = GetBytes(resp.Payload, 0, 4);
                    uint time = BitConverter.ToUInt32(bytes, 0);
                    Debug.WriteLine($"[Param] PONG ok ✅  seq={resp.Header.SeqNo}  payloadLen={resp.Header.PayloadLen} payload={time}");
                }
                else
                {
                    // 如果主站回了 ERR 帧，Payload[0] 是 TcStatus 错误码
                    TcStatus errCode = resp.Payload.Length > 0
                        ? (TcStatus)resp.Payload[0]
                        : TcStatus.INTERNAL;
                    Debug.WriteLine($"[Param] PING got unexpected CMD={resp.Header.Cmd}  err={errCode}");
                }
            }
            catch (TimeoutException) { Debug.WriteLine("[Param] PING timeout"); }
            catch (Exception ex) { Debug.WriteLine($"[Param] PING error: {ex.Message}"); }
        }

        private byte[] GetBytes(byte[] payload, int v1, int v2)
        {
            byte[] bytes = new byte[v2];
            for(int i=0;i<v2;i++)
            {
                bytes[i] = payload[i+v1];
            }
            return bytes;
        }

        // 以下测试完成后删除
        // ============================================================================
        //  放在你的 Form 类中（MainForm 或对应窗体）
        // ============================================================================

        // ── 读取按钮状态 ──────────────────────────────────────────────────────────
        private static int _readIndex = 0;  // 当前读取到第几组
        private static ushort _seqNo = 0;
        private static readonly string[] _readOrder = new[]
        {
    "CLSModel", "CLSParam", "CLS5K", "CLSConsts", "TestMDL",
    "CLSEnum", "XT", "YT", "DeviceInfo", "UdpDataCfg", "UdpParamCfg",
    "TcLCS_CtrlIn", "TcLCS_CtrlOut", "TcLCS_P"
};

        // ── CLS5K 按钮状态 ────────────────────────────────────────────────────────
        private bool _cls5kIsZeroed = false;  // true=当前已清零，false=原始值
        private ST_CLS5K _cls5kBackup = new ST_CLS5K();  // 备份原始值

        // =============================================================================
        //  读取按钮回调
        //  每按一次：发送 READ_REQ → 等待 READ_ACK → TryDeserialize → MessageBox 显示
        // =============================================================================
        private async void btnReadNext_Click(object sender, EventArgs e)
        {
            if (_readIndex >= _readOrder.Length)
            {
                MessageBox.Show("所有参数组已全部读取完毕！", "读取完成",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                _readIndex = 0;
                return;
            }

            string groupName = _readOrder[_readIndex];
            TcSubId subId = GroupNameToSubId(groupName);

            TcFrame ack;
            try
            {
                ack = await ParamReadAsync(subId);  // ← 就这一行，搞定收发
            }
            catch (TimeoutException)
            {
                MessageBox.Show($"读取 {groupName} 超时，请检查连接。", groupName,
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (IsErrFrame(ack, groupName))
            {
                MessageBox.Show($"{groupName} 返回错误帧。", groupName,
                                MessageBoxButtons.OK, MessageBoxIcon.Warning);
                //return;
            }
            else
            ParamData.TryDeserialize(ack);  // ← 解析存入全局变量

            string display = FormatGroupDisplay(groupName);
            MessageBox.Show(display, groupName, MessageBoxButtons.OK, MessageBoxIcon.None);

            _readIndex++;
            if (_readIndex >= _readOrder.Length)
            {
                MessageBox.Show("✅ 所有参数组已全部读取完毕！", "读取完成",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
                _readIndex = 0;
            }
        }

        // =============================================================================
        //  CLS5K 按钮回调
        //  红色状态（已清零）→ 绿色（写回原始值）
        //  绿色状态（原始值）→ 红色（写零）
        // =============================================================================
        private async void btnCLS5K_Click(object sender, EventArgs e)
        {
            Button btn = (Button)sender;

            if (!_cls5kIsZeroed)
            {
                // ── 当前是原始值 → 备份 → 写零 → 按钮变红 ──────────────────────
                lock (ParamData.LockCLS5K)
                    _cls5kBackup = ParamData.CLS_5K;  // 备份

                // 全部写零
                ST_CLS5K zero = new ST_CLS5K();  // 所有 double 默认 0.0
                lock (ParamData.LockCLS5K)
                    ParamData.CLS_5K = zero;

                bool ok = await WriteCLS5KAsync(ParamData.CLS_5K);
                if (!ok)
                {
                    MessageBox.Show("写入失败，请检查连接。", "CLS5K Write",
                                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    lock (ParamData.LockCLS5K)
                        ParamData.CLS_5K = _cls5kBackup;  // 写失败则还原内存
                    return;
                }

                btn.BackColor = Color.Red;
                btn.Text = "CLS5K（已清零）\r\n点击恢复";
                _cls5kIsZeroed = true;
                MessageBox.Show("CLS5K 已全部写零。", "CLS5K Write",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                // ── 当前是清零状态 → 写回备份 → 按钮变绿 ────────────────────────
                lock (ParamData.LockCLS5K)
                    ParamData.CLS_5K = _cls5kBackup;

                bool ok = await WriteCLS5KAsync(ParamData.CLS_5K);
                if (!ok)
                {
                    MessageBox.Show("写入失败，请检查连接。", "CLS5K Restore",
                                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                btn.BackColor = Color.LimeGreen;
                btn.Text = "CLS5K（已恢复）\r\n点击清零";
                _cls5kIsZeroed = false;
                MessageBox.Show("CLS5K 已恢复原始参数。", "CLS5K Restore",
                                MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        // =============================================================================
        //  辅助：发送 CLS5K WRITE_REQ 并等待 WRITE_ACK
        // =============================================================================
        private async Task<bool> WriteCLS5KAsync(ST_CLS5K data)
        {
            try
            {
                byte[] payload = ParamData.Serialize(data);
                TcFrame ack = await ParamWriteAsync(TcSubId.CLS5K, payload);  // ← 就这一行
                return !IsErrFrame(ack, "WriteCLS5K");
            }
            catch (TimeoutException)
            {
                return false;
            }
        }

        // =============================================================================
        //  辅助：SubID 名 → 枚举
        // =============================================================================
        private static TcSubId GroupNameToSubId(string name) => name switch
        {
            "CLSModel" => TcSubId.CLSModel,
            "CLSParam" => TcSubId.CLSParam,
            "CLS5K" => TcSubId.CLS5K,
            "CLSConsts" => TcSubId.CLSConsts,
            "TestMDL" => TcSubId.TestMDL,
            "CLSEnum" => TcSubId.CLSEnum,
            "XT" => TcSubId.XT,
            "YT" => TcSubId.YT,
            "DeviceInfo" => TcSubId.DeviceInfo,
            "UdpDataCfg" => TcSubId.UdpDataCfg,
            "UdpParamCfg" => TcSubId.UdpParamCfg,
            "TcLCS_CtrlIn" => TcSubId.TcLCS_CtrlIn,
            "TcLCS_CtrlOut" => TcSubId.TcLCS_CtrlOut,
            "TcLCS_P" => TcSubId.ALL,
            _ => throw new ArgumentException($"Unknown group: {name}")
        };

        // =============================================================================
        //  辅助：将全局变量格式化为弹窗显示字符串
        // =============================================================================
        private static string FormatGroupDisplay(string name)
        {
            switch (name)
            {
                case "CLSModel":
                    var m = ParamData.CLS_Model;
                    return $"bA={m.bA:F4}  bF={m.bF:F4}  bH={m.bH:F4}  bS={m.bS:F4}\n" +
                           $"kS={m.kS:F4}  kH={m.kH:F4}  kHV={m.kHV:F4}  kSV={m.kSV:F4}\n" +
                           $"kQ={m.kQ:F4}  kU={m.kU:F4}  mA={m.mA:F4}  mF={m.mF:F4}\n" +
                           $"Vbrk={m.Vbrk:F4}  Xbrk={m.Xbrk:F4}  FcA={m.FcA:F4}  FcF={m.FcF:F4}\n" +
                           $"bFA={m.bFA:F4}  dzF={m.dzF:F4}\n" +
                           $"NFC_P={m.NFC_P:F4}  NFC_I={m.NFC_I:F4}  NFC_D={m.NFC_D:F4}  NFC_N={m.NFC_N:F4}";

                case "CLSParam":
                    var p = ParamData.CLS_Param;
                    return $"AutoHoming={p.AutoHoming}\n" +
                           $"VPos={p.VPos:F4}  Jagment={p.Jagment:F4}  JagmentP={p.JagmentP:F4}  JagmentN={p.JagmentN:F4}\n" +
                           $"P0Home={p.P0Home:F4}  Foffset={p.Foffset:F4}  Fzero={p.Fzero:F4}  F0Aoff={p.F0Aoff:F4}\n" +
                           $"Poffset={p.Poffset:F4}  P0Trim={p.P0Trim:F4}  L0Trim={p.L0Trim:F4}\n" +
                           $"L0TravA={p.L0TravA:F4}  L0TravB={p.L0TravB:F4}\n" +
                           $"ShakerA={p.ShakerA:F4}  ShakerF={p.ShakerF:F4}  VT1={p.VT1:F4}  VT2={p.VT2:F4}";

                case "CLS5K":
                    var k = ParamData.CLS_5K;
                    return $"K0={k.K0:F4}  K1={k.K1:F4}  K2={k.K2:F4}\n" +
                           $"K3={k.K3:F4}  K4={k.K4:F4}  K5={k.K5:F4}\n" +
                           $"X0={k.X0:F4}  X1={k.X1:F4}  X2={k.X2:F4}\n" +
                           $"X3={k.X3:F4}  X4={k.X4:F4}  X5={k.X5:F4}\n" +
                           $"X6={k.X6:F4}  Ke={k.Ke:F4}";

                case "CLSConsts":
                    var c = ParamData.CLS_Consts;
                    return $"KUSEDEG={c.KUSEDEG}  CCWDIR={c.CCWDIR}  KFLMT={c.KFLMT}\n" +
                           $"KFmax={c.KFmax:F4}  KVmax={c.KVmax:F4}  Larm={c.Larm:F4}\n" +
                           $"KPR={c.KPR:F4}  KFR={c.KFR:F4}  KX2P={c.KX2P:F4}\n" +
                           $"KForceTo={c.KForceTo:F4}  KVelTo={c.KVelTo:F4}  KPosTo={c.KPosTo:F4}\n" +
                           $"KF2N={c.KF2N:F4}  KV2DPS={c.KV2DPS:F4}  KP2DEG={c.KP2DEG:F4}";

                case "TestMDL":
                    var t = ParamData.Test_MDL;
                    return $"Km={t.Km:F4}  Ks={t.Ks:F4}  Kp={t.Kp:F4}\n" +
                           $"Ka={t.Ka:F4}  Kq={t.Kq:F4}  bs={t.bs:F4}\n" +
                           $"bA={t.bA:F4}  Ksf={t.Ksf:F4}  bsf={t.bsf:F4}\n" +
                           $"DL={t.DL:F4}  PL={t.PL:F4}";

                case "CLSEnum":
                    var en = ParamData.CLS_Enum;
                    return $"DM_PP={en.DM_PP}  DM_PV={en.DM_PV}  DM_PT={en.DM_PT}  DM_HM={en.DM_HM}\n" +
                           $"DM_IP={en.DM_IP}  DM_CSP={en.DM_CSP}  DM_CSV={en.DM_CSV}  DM_CST={en.DM_CST}\n" +
                           $"STU_KQS={en.STU_KQS}\n" +
                           $"STU_FAULT={en.STU_FAULT}  STU_STOP={en.STU_STOP}  STU_NORDY={en.STU_NORDY}\n" +
                           $"STU_OPR={en.STU_OPR}  STU_HOMED={en.STU_HOMED}\n" +
                           $"DRV_SHUTDWN={en.DRV_SHUTDWN}  DRV_ENABLE={en.DRV_ENABLE}  DRV_RESET={en.DRV_RESET}\n" +
                           $"SW_FCW={en.SW_FCW}";

                case "XT":
                    var xt = ParamData.Param_XT;
                    var sbXT = new StringBuilder("aXT:\n");
                    for (int i = 0; i < 21; i++)
                        sbXT.Append($"[{i:D2}]={xt.aXT[i]:F4}  " + (i % 4 == 3 ? "\n" : ""));
                    return sbXT.ToString();

                case "YT":
                    var yt = ParamData.Param_YT;
                    var sbYT = new StringBuilder("aYT:\n");
                    for (int i = 0; i < 21; i++)
                        sbYT.Append($"[{i:D2}]={yt.aYT[i]:F4}  " + (i % 4 == 3 ? "\n" : ""));
                    return sbYT.ToString();

                case "DeviceInfo":
                    var d = ParamData.Device_Info;
                    return $"ID={d.ID}\nPosN={d.PosN:F4}\nPOSP={d.POSP:F4}\nTestMode={d.TestMode}";

                case "UdpDataCfg":
                    var ud = ParamData.UdpData_Cfg;
                    return $"LocalIP={ud.GetLocalIP()}  LocalPort={ud.LocalPort}\n" +
                           $"RemoteIP={ud.GetRemoteIP()}  RemotePort={ud.RemotePort}\n" +
                           $"bPeriodical={ud.bPeriodical}  Period={ud.Period}";

                case "UdpParamCfg":
                    var up = ParamData.UdpParam_Cfg;
                    return $"LocalIP={up.GetLocalIP()}  LocalPort={up.LocalPort}\n" +
                           $"RemoteIP={up.GetRemoteIP()}  RemotePort={up.RemotePort}\n" +
                           $"bPeriodical={up.bPeriodical}  Period={up.Period}";

                case "TcLCS_CtrlIn":
                    var u = ParamData.CtrlIn;
                    return $"CtrlCmd={u.CtrlCmd}  FnSwitch={u.FnSwitch}\n" +
                           $"fwdFric={u.fwdFric:F4}  jamPos={u.jamPos:F4}\n" +
                           $"TravA={u.TravA:F4}  TravB={u.TravB:F4}\n" +
                           $"fwdMassD={u.fwdMassD:F4}  fwdDampD={u.fwdDampD:F4}\n" +
                           $"FInput={u.FInput:F4}  Vap={u.Vap:F4}\n" +
                           $"VTrim={u.VTrim:F4}  FaOffset={u.FaOffset:F4}\n" +
                           $"FaGrad={u.FaGrad:F4}  trimInitP={u.trimInitP:F4}\n" +
                           $"Spare1={u.Spare1:F4}  Spare2={u.Spare2:F4}  Spare3={u.Spare3:F4}";

                case "TcLCS_CtrlOut":
                    var y = ParamData.CtrlOut;
                    return $"state={y.state}  safety={y.safety}  isFading={y.isFading}\n" +
                           $"fwdPosition={y.fwdPosition:F4}  fwdVelocity={y.fwdVelocity:F4}\n" +
                           $"fwdForce={y.fwdForce:F4}  cableForce={y.cableForce:F4}\n" +
                           $"trimPosition={y.trimPosition:F4}  aftPosition={y.aftPosition:F4}\n" +
                           $"motorPosition={y.motorPosition:F4}  motorVelocity={y.motorVelocity:F4}\n" +
                           $"sensorForce={y.sensorForce:F4}  commandForce={y.commandForce:F4}";

                default:
                    return "(无数据)";
            }
        }
    }
}
