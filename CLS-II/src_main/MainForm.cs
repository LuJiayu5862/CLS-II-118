using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using MmTimer;

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
    }
}
