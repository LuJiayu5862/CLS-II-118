using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using UDP;
using INIFileRW;

namespace CLS_II
{
    public partial class MainForm : Form
    {
        #region private_var
        private UInt32 _passport;
        private int[] isFormOpened = { 0, 0, 0 };       // 记录Form是否打开过
        private string _nowProjectFile = string.Empty;

        private enum WatchDataMode
        {
            DEC = 0,
            HEX = 1,
            OCT = 2,
            BIN = 3
        }
        #endregion

        #region public_var
        public string ProjectFile
        {
            get
            {
                return this._nowProjectFile;
            }
        }
        #endregion


        private void LoadAll(Form form)
        {
            if (form.Name == "MainForm")
            {
                MultiLanguage.LoadLanguage((Form)form, typeof(MainForm));
            }
            else if (form.Name == "Menu")
            {
                MultiLanguage.LoadLanguage((Form)form, typeof(Menu));
                form.Size = panel_Menu.Size;
                form.Dock = DockStyle.Fill;
            }
            else if (form.Name == "Watch")
            {
                MultiLanguage.LoadLanguage((Form)form, typeof(Watch));
            }
            else if (form.Name == "UdpTest")
            {
                MultiLanguage.LoadLanguage((Form)form, typeof(UdpTest));
            }
        }

        private void admin_onJudged(object sender, Login.JudgeEventArgs e)
        {
            GlobalVar.isAdministrator = e.Result;           
            if (GlobalVar.isAdministrator)
            {
                foreach (ToolStripMenuItem item in userToolStripMenuItem.DropDownItems)
                {
                    item.Checked = false;
                }
                administratorToolStripMenuItem.Checked = true;
                toolStripStatusLabel2.Text = administratorToolStripMenuItem.Text;
            }
        }

        private void MainConfigRefresh()
        {
            // SetItems
            foreach (ToolStripMenuItem item in languageToolStripMenuItem.DropDownItems)
            {
                if (item.Checked)
                {
                    MainConfig.ConfigInfo.SetItems.Language = item.Tag.ToString();
                }
            }

            // DebugItems
            MainConfig.ConfigInfo.DebugItems.isWatchVisible = watchToolStripMenuItem.Checked;
            MainConfig.ConfigInfo.DebugItems.isAutoWatch = autoWatchToolStripMenuItem.Checked;
        }

        private void LanguageChanged()
        {
            if (GlobalVar.isSimulation)
                modeToolStripDropDownButton.Text = testToolStripMenuItem.Text;
            else
                modeToolStripDropDownButton.Text = simulationToolStripMenuItem.Text;

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

            if (GlobalVar.isUdpConnceted)
            {
                if (MultiLanguage.DefaultLanguage == "zh")
                {
                    connectToolStripButton1.Text = "断开连接";
                }
                else
                {
                    connectToolStripButton1.Text = "Disconnect";
                }
                connectToolStripButton1.BackColor = Color.Orange;
            }
            else
            {
                if (MultiLanguage.DefaultLanguage == "zh")
                {
                    connectToolStripButton1.Text = "连接设备";
                }
                else
                {
                    connectToolStripButton1.Text = "Connect";
                }
                //resetToolStripButton.Enabled = false;
                //stopToolStripButton.Enabled = false;
                udpSendToolStripButton1.Enabled = false;
                udpSendToolStripButton1.BackColor = SystemColors.Control;
                connectToolStripButton1.BackColor = Color.PowderBlue;
            }


            
        }

        public void GenerateForm(string form, object sender)
        {
            // 反射生成窗体
            Form fm = (Form)Assembly.GetExecutingAssembly().CreateInstance(form);
            if (fm is null) return;
            //设置窗体没有边框 加入到Panel中
            fm.FormBorderStyle = FormBorderStyle.None;
            fm.TopLevel = false;
            fm.Parent = (Panel)sender;
            fm.ControlBox = false;
            fm.Dock = DockStyle.Fill;
            fm.Show();
        }

        public void GenerateForm(object form, object sender)
        {
            // 反射生成窗体
            Form fm = (Form)form;
            if (fm is null) return;
            //设置窗体没有边框 加入到Panel中
            fm.FormBorderStyle = FormBorderStyle.None;
            fm.TopLevel = false;
            fm.Parent = (Panel)sender;
            fm.ControlBox = false;
            fm.Dock = DockStyle.Fill;
            fm.Show();
        }

        private void CloseForm(Panel sender)
        {
            //遍历spContainer.Panel2中的控件，如果存夺Form控件，则将它关闭。
            foreach (Control item in sender.Controls)
            {
                if (item is Form)//如果是Form控件，就将它关闭掉
                {
                    Form objControl = (Form)item;
                    objControl.Close();
                }
            }
        }

        private async void ConnectDevice()
        {
            if (!GlobalVar.isUdpConnceted)
            {
                try
                {
                    SetDefaultRemoteHost(GlobalVar.szRemoteHost, GlobalVar.nPortIn, GlobalVar.nPortOut1, GlobalVar.nPortOut2);
                    InitUDP();

                    JdUdpClient.StartInstance(
                        JdConsts.szJdRemoteHost,
                        JdConsts.nJdPortSend,
                        JdConsts.nJdPortRecv);

                    await StartParamUdpAsync();

                    GlobalVar.isUdpConnceted = true;
                    udpStateToolStripStatusLabel.Text = GlobalVar.szRemoteHost;
                }
                catch (Exception err)
                {
                    MessageBox.Show(err.Message, "Error");
                    return;
                }
            }
            GlobalVar.isSendUdp = false;
            if (MultiLanguage.DefaultLanguage == "zh")
            {
                connectToolStripButton1.Text = "断开连接";
                udpSendToolStripButton1.Text = "连续发送";
            }
            else
            {
                connectToolStripButton1.Text = "Disconnect";
                udpSendToolStripButton1.Text = "Send UDP";
            }
            udpSendToolStripButton1.Enabled = true;
            connectToolStripButton1.BackColor = Color.Orange;
            udpSendToolStripButton1.BackColor = Color.PowderBlue;

            LoadTreeView2(GlobalVar.DeviceName);
        }

        private void DisconnectDevice()
        {
            GlobalVar.isSendUdp = false;
            if (GlobalVar.isUdpConnceted)
            {
                GlobalVar.isUdpConnceted = false;

                StopParamUdp();
                JdUdpClient.StopInstance();
                DisposeUDP();
                
                udpStateToolStripStatusLabel.Text = string.Empty;
            }
            if (MultiLanguage.DefaultLanguage == "zh")
            {
                connectToolStripButton1.Text = "连接设备";
                udpSendToolStripButton1.Text = "连续发送";
            }
            else
            {
                connectToolStripButton1.Text = "Connect";
                udpSendToolStripButton1.Text = "Send UDP";
            }
            udpSendToolStripButton1.Enabled = false;
            connectToolStripButton1.BackColor = Color.PowderBlue;
            udpSendToolStripButton1.BackColor = SystemColors.Control;

            if (treeView1.Nodes.Count > 0 && treeView2.Nodes.Count > 0)
            {
                treeView1.SelectedNode = treeView1.Nodes[0].Nodes[0];
                treeView1.Focus();
            }                
            treeView2.Nodes.Clear();
        }

        private void CreateProjectFile()
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
                treeView1.Nodes.Clear();
                treeView1.Nodes.Add(fileNameExt);
                treeView1.Nodes[0].Nodes.Add("Untitled Device");
                treeView1.ExpandAll();
                SaveProjectFile(localFilePath);
                LoadProjectFile(localFilePath);
                DisconnectDevice();
                treeView1.SelectedNode = treeView1.Nodes[0];
            }
        }

        private void OpenProjectFile()
        {
            string localFilePath = string.Empty;
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Project files(*.xrp)|*.xrp";
            openFileDialog.Multiselect = false;

            DialogResult result = openFileDialog.ShowDialog();
            if (result == DialogResult.OK)
            {
                localFilePath = openFileDialog.FileName;
                string name = iniFileRW.INIGetStringValue(localFilePath, "Device", "Name", string.Empty);
                if (string.IsNullOrEmpty(name))
                {
                    if (MultiLanguage.DefaultLanguage == "zh")
                    {
                        MessageBox.Show("工程文件错误，打开失败", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    else
                    {
                        MessageBox.Show("Project file error, opening failed", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                else
                {
                    GlobalVar.ProjectFile = localFilePath;
                    //GlobalVar.DeviceName = name;
                    //GlobalVar.szRemoteHost = iniFileRW.INIGetStringValue(localFilePath, "Device", "IP", "127.0.0.1");
                    //GlobalVar.AmsNetID = iniFileRW.INIGetStringValue(localFilePath, "Device", "AmsNetID", "127.0.0.1.1.1");
                    //CLSConsts.EnabledChannels = int.Parse(iniFileRW.INIGetStringValue(localFilePath, "Device", "ChannelCount", "10"));
                    LoadProjectFile(localFilePath);
                    DisconnectDevice();
                    treeView1.SelectedNode = treeView1.Nodes[0];
                }
            }
        }

        private void SaveProjectFile(string ProjectFile)
        {
            if (!string.IsNullOrEmpty(ProjectFile))
            {
                iniFileRW.INIWriteValue(ProjectFile, "Device", "Name", GlobalVar.DeviceName);
                iniFileRW.INIWriteValue(ProjectFile, "Device", "IP", GlobalVar.szRemoteHost);
                iniFileRW.INIWriteValue(ProjectFile, "Device", "ChannelCount", CLSConsts.EnabledChannels.ToString());
            }
        }

        private void ReadProjectFile(string ProjectFile)
        {
            if (!string.IsNullOrEmpty(ProjectFile))
            {
                GlobalVar.DeviceName = 
                    iniFileRW.INIGetStringValue(ProjectFile, "Device", "Name", "Untitled Device");
                GlobalVar.szRemoteHost = 
                    iniFileRW.INIGetStringValue(ProjectFile, "Device", "IP", "127.0.0.1");
                CLSConsts.EnabledChannels =
                    int.Parse(iniFileRW.INIGetStringValue(ProjectFile, "Device", "ChannelCount", "10"));
            }
        }

        private bool CloseProjectFile()
        {
            if (!string.IsNullOrEmpty(GlobalVar.ProjectFile))
            {
                DialogResult result = new DialogResult();
                if (MultiLanguage.DefaultLanguage == "zh")
                    result = MessageBox.Show("确认关闭工程吗？", "关闭工程", MessageBoxButtons.OKCancel);
                else
                    result = MessageBox.Show("Are you sure to close the project?", "Closing Project", MessageBoxButtons.OKCancel);
                if (result == DialogResult.OK)
                {
                    this._nowProjectFile = string.Empty;
                    GlobalVar.ProjectFile = string.Empty;
                    treeView1.Nodes.Clear();
                    treeView2.Nodes.Clear();
                    DisconnectDevice();
                    CloseForm(panel_Body);
                    CloseForm(panel_Watch);
                    toolStrip_UDP.Visible = false;
                    return true;
                }
                return false;
            }
            else
                return true;
        }

        private void LoadProjectFile(string ProjectFile)
        {
            string fileNameExt = ProjectFile.Substring(ProjectFile.LastIndexOf("\\") + 1,
                        ProjectFile.LastIndexOf(".") - ProjectFile.LastIndexOf("\\") - 1);
            
            ReadProjectFile(ProjectFile);

            string name = GlobalVar.DeviceName;
            treeView1.Nodes.Clear();
            treeView1.Nodes.Add(fileNameExt);
            treeView1.Nodes[0].Nodes.Add(name);
            treeView1.ExpandAll();

            toolStrip_UDP.Visible = true;
        }

        private void LoadTreeView2(string DeviceName)
        {
            treeView2.Nodes.Clear();
            treeView2.Nodes.Add(DeviceName);
            if (MultiLanguage.DefaultLanguage == "zh")
            {
                treeView2.Nodes[0].Nodes.Add("示波器");

                treeView2.Nodes[0].Nodes[treeView2.Nodes[0].Nodes.Count - 1].Nodes.Add("Y-T 示波器");
                treeView2.Nodes[0].Nodes[treeView2.Nodes[0].Nodes.Count - 1].Nodes.Add("X-Y 示波器");

                //treeView2.Nodes[0].Nodes.Add("参数加载/保存");
                //treeView2.Nodes[0].Nodes.Add("设备设置");
                treeView2.Nodes[0].Nodes.Add("UDP通讯仿真");
                //treeView2.Nodes[0].Nodes.Add("通道");

                //for (int i = 0; i < CLSConsts.EnabledChannels; i++)
                //{
                //    string nodeName = "通道" + (i + 1) + "<" + AdsInfo.AdsObject.AdsPort[i].Name + ">";
                //    treeView2.Nodes[0].Nodes[treeView2.Nodes[0].Nodes.Count - 1].Nodes.Add(nodeName);
                //    int index1, index2;
                //    index1 = treeView2.Nodes[0].Nodes.Count - 1;
                //    index2 = treeView2.Nodes[0].Nodes[index1].Nodes.Count - 1;
                //    treeView2.Nodes[0].Nodes[index1].Nodes[index2].Tag = i;
                //    if (!AdsInfo.AdsObject.AdsPort[i].Enabled)
                //    {                       
                //        treeView2.Nodes[0].Nodes[index1].Nodes[index2].ForeColor = Color.Red;
                //    }
                //    else if (!adsClients[i].isAvailable)
                //    {
                //        treeView2.Nodes[0].Nodes[index1].Nodes[index2].ForeColor = Color.Brown;
                //    }
                //}
            }
            else
            {
                treeView2.Nodes[0].Nodes.Add("Scope");

                treeView2.Nodes[0].Nodes[treeView2.Nodes[0].Nodes.Count - 1].Nodes.Add("Y-T Scope");
                treeView2.Nodes[0].Nodes[treeView2.Nodes[0].Nodes.Count - 1].Nodes.Add("X-Y Scope");

                //treeView2.Nodes[0].Nodes.Add("Parameter Load/Save");
                //treeView2.Nodes[0].Nodes.Add("Device Settings");
                treeView2.Nodes[0].Nodes.Add("UDP Simulator");
                //treeView2.Nodes[0].Nodes.Add("Channels");

                //for (int i = 0; i < CLSConsts.EnabledChannels; i++)
                //{
                //    string nodeName = "Channel" + (i + 1);
                //    treeView2.Nodes[0].Nodes[treeView2.Nodes[0].Nodes.Count - 1].Nodes.Add(nodeName);
                //    int index1, index2;
                //    index1 = treeView2.Nodes[0].Nodes.Count - 1;
                //    index2 = treeView2.Nodes[0].Nodes[index1].Nodes.Count - 1;
                //    treeView2.Nodes[0].Nodes[index1].Nodes[index2].Tag = i;
                //    if (!AdsInfo.AdsObject.AdsPort[i].Enabled)
                //    {
                //        treeView2.Nodes[0].Nodes[index1].Nodes[index2].ForeColor = Color.Red;
                //    }
                //    else if (!adsClients[i].isAvailable)
                //    {
                //        treeView2.Nodes[0].Nodes[index1].Nodes[index2].ForeColor = Color.Brown;
                //    }
                //}
            }
            treeView2.ExpandAll();
        }

    }
}
